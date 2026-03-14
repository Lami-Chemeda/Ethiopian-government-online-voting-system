using Microsoft.EntityFrameworkCore;
using VotingSystem.Data;
using VotingSystem.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();

// Database configuration
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();