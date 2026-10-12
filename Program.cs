using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using VotingSystem.Data;
using VotingSystem.Models;
using VotingSystem.Services;
using System.Runtime.InteropServices;
using System.Reflection;

// Fix for Tesseract Linux native library initialization and dependency preloading
if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    try
    {
        var baseDir = AppContext.BaseDirectory;
        var currentDir = Directory.GetCurrentDirectory();

        string[] candidateDirs = new[]
        {
            Path.Combine(baseDir, "x64"),
            baseDir,
            Path.Combine(currentDir, "x64"),
            Path.Combine(currentDir, "EGOVS", "EGOVS", "VotingSystem", "x64"),
            Path.Combine(currentDir, "VotingSystem", "x64")
        };

        string nativeDir = candidateDirs.FirstOrDefault(d => Directory.Exists(d) && (File.Exists(Path.Combine(d, "libtesseract.so.5")) || File.Exists(Path.Combine(d, "libtesseract50.so")))) 
            ?? Path.Combine(baseDir, "x64");

        Console.WriteLine($"[Tesseract Init] Native library directory: {nativeDir}");

        // Preload dependencies in topological order so dlopen never fails
        string[] preloadLibs = new[]
        {
            "libdeflate.so.0",
            "libjbig.so.0",
            "libLerc.so.4",
            "libwebp.so.7",
            "libwebpmux.so.3",
            "libtiff.so.6",
            "libgif.so.7",
            "libopenjp2.so.7",
            "libarchive.so.13",
            "libleptonica.so.6",
            "libleptonica-1.82.0.so",
            "libtesseract.so.5",
            "libtesseract50.so"
        };

        if (Directory.Exists(nativeDir))
        {
            foreach (var libName in preloadLibs)
            {
                var fullPath = Path.Combine(nativeDir, libName);
                if (File.Exists(fullPath))
                {
                    try
                    {
                        NativeLibrary.Load(fullPath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Tesseract Init] Notice while preloading {libName}: {ex.Message}");
                    }
                }
            }
        }

        // Copy files to target output directories if missing
        var targetDirs = new[] { baseDir, Path.Combine(baseDir, "x64") };
        foreach (var dir in targetDirs)
        {
            if (!Directory.Exists(dir)) { try { Directory.CreateDirectory(dir); } catch { } }
            if (Directory.Exists(nativeDir) && nativeDir != dir)
            {
                foreach (var file in Directory.GetFiles(nativeDir, "*.so*"))
                {
                    var dest = Path.Combine(dir, Path.GetFileName(file));
                    if (!File.Exists(dest)) { try { File.Copy(file, dest, true); } catch { } }
                }
            }
        }

        // Register DllImportResolver for Tesseract assembly
        NativeLibrary.SetDllImportResolver(typeof(Tesseract.TesseractEngine).Assembly, (libraryName, assembly, searchPath) =>
        {
            if (libraryName.Contains("lept", StringComparison.OrdinalIgnoreCase))
            {
                var candidates = new[] { "libleptonica-1.82.0.so", "liblept.so.5", "libleptonica.so.6", "liblept.so" };
                foreach (var c in candidates)
                {
                    var p = Path.Combine(nativeDir, c);
                    if (File.Exists(p) && NativeLibrary.TryLoad(p, out var handle)) return handle;
                    if (NativeLibrary.TryLoad(c, assembly, searchPath, out handle)) return handle;
                }
            }
            if (libraryName.Contains("tess", StringComparison.OrdinalIgnoreCase))
            {
                var candidates = new[] { "libtesseract50.so", "libtesseract.so.5", "libtesseract.so" };
                foreach (var c in candidates)
                {
                    var p = Path.Combine(nativeDir, c);
                    if (File.Exists(p) && NativeLibrary.TryLoad(p, out var handle)) return handle;
                    if (NativeLibrary.TryLoad(c, assembly, searchPath, out handle)) return handle;
                }
            }
            if (NativeLibrary.TryLoad(libraryName, assembly, searchPath, out var defHandle))
            {
                return defHandle;
            }
            return IntPtr.Zero;
        });

        Console.WriteLine("[Tesseract Init] Tesseract native resolver configured successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Tesseract Init] Warning setting up Tesseract libraries: {ex.Message}");
    }
}

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "The DefaultConnection connection string is required. Configure ConnectionStrings__DefaultConnection in the hosting environment.");
}

// Add services to the container
builder.Services.AddControllersWithViews();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Database configuration
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));
// Add Login Tracking Service
builder.Services.AddScoped<ILoginTrackingService, LoginTrackingService>();
// Add session services
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Register HTTP Client for OCR services
builder.Services.AddHttpClient();

// Register OCR Services - Use TesseractOCRService for real OCR processing
builder.Services.AddScoped<IOCRService, TesseractOCRService>();

// Register additional services
builder.Services.AddScoped<EthiopianOCRService>();
builder.Services.AddScoped<TextToSpeechService>();

// Register Comment Service
builder.Services.AddScoped<ICommentService, CommentService>();

// Add logging
builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddConsole();
    loggingBuilder.AddDebug();
    loggingBuilder.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
    loggingBuilder.AddFilter("VotingSystem.Services.TesseractOCRService", LogLevel.Information);
});

// Build the application
var app = builder.Build();

app.UseForwardedHeaders();

// Automatically verify database connection, ensure schema exists, and seed default admin
try
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("[Database] Testing database connection...");
    if (dbContext.Database.CanConnect())
    {
        logger.LogInformation("[Database] Connected successfully to database!");
        dbContext.Database.EnsureCreated();

        if (!dbContext.Admins.Any())
        {
            dbContext.Admins.Add(new Admin
            {
                NationalId = "123456789012345",
                Nationality = "Ethiopian",
                Region = "Addis Ababa",
                PhoneNumber = "0912345678",
                FirstName = "Abebe",
                MiddleName = "Kebede",
                LastName = "Alemu",
                Age = 30,
                Sex = "Male",
                Password = "Admin@123"
            });
            dbContext.SaveChanges();
            logger.LogInformation("[Database] Seeded initial administrator (ID: 123456789012345, Password: Admin@123)");
        }
    }
    else
    {
        logger.LogWarning("[Database Warning] Could not connect to the configured database. On Render, ensure the 'ConnectionStrings__DefaultConnection' environment variable is set to your cloud SQL Server.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[Database Warning] Database initialization check: {ex.Message}");
}

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
    
    // Ensure tessdata directory exists in development
    var tessDataPath = Path.Combine(Directory.GetCurrentDirectory(), "tessdata");
    if (!Directory.Exists(tessDataPath))
    {
        Directory.CreateDirectory(tessDataPath);
        Console.WriteLine($"Created tessdata directory at: {tessDataPath}");
        Console.WriteLine("Please ensure eng.traineddata and amh.traineddata are placed in this directory.");
    }
    else
    {
        Console.WriteLine($"Tessdata directory exists at: {tessDataPath}");
        
        // Check if language files exist
        var engPath = Path.Combine(tessDataPath, "eng.traineddata");
        var amhPath = Path.Combine(tessDataPath, "amh.traineddata");
        
        Console.WriteLine($"English language file exists: {File.Exists(engPath)}");
        Console.WriteLine($"Amharic language file exists: {File.Exists(amhPath)}");
    }
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// ============ ADD THIS MIDDLEWARE FOR CACHE CONTROL ============
app.Use(async (context, next) =>
{
    // Add no-cache headers to ALL responses
    context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
    context.Response.Headers["Pragma"] = "no-cache";
    context.Response.Headers["Expires"] = "0";
    
    await next();
});
// ===============================================================

app.UseAuthorization();
app.UseSession();

app.MapGet("/robots.txt", (HttpRequest request, IConfiguration configuration) =>
{
    var publicBaseUrl = configuration["Seo:PublicBaseUrl"]?.TrimEnd('/')
        ?? $"{request.Scheme}://{request.Host}";
    var sitemapUrl = $"{publicBaseUrl}/sitemap.xml";
    var robots = string.Join('\n',
        "User-agent: *",
        "Allow: /",
        "Disallow: /Admin",
        "Disallow: /Manager",
        "Disallow: /Supervisor",
        "Disallow: /Voter",
        "Disallow: /Home/Login",
        "Disallow: /Home/Register",
        $"Sitemap: {sitemapUrl}",
        "");

    return Results.Text(robots, "text/plain");
});

app.MapGet("/sitemap.xml", (HttpRequest request, IConfiguration configuration) =>
{
    var publicBaseUrl = configuration["Seo:PublicBaseUrl"]?.TrimEnd('/')
        ?? $"{request.Scheme}://{request.Host}";
    var escapedBaseUrl = System.Security.SecurityElement.Escape(publicBaseUrl);
    var sitemap = $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
          <url>
            <loc>{escapedBaseUrl}/</loc>
          </url>
        </urlset>
        """;

    return Results.Content(sitemap, "application/xml");
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();