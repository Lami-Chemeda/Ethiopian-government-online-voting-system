using Microsoft.EntityFrameworkCore;
using VotingSystem.Data;
using VotingSystem.Services;

var builder = WebApplication.CreateBuilder(args);

// ================= SERVICES =================

// MVC
builder.Services.AddControllersWithViews();

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Login Tracking
builder.Services.AddScoped<ILoginTrackingService, LoginTrackingService>();

// Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// HTTP Client
builder.Services.AddHttpClient();

// OCR Services
builder.Services.AddScoped<IOCRService, TesseractOCRService>();
builder.Services.AddScoped<EthiopianOCRService>();
builder.Services.AddScoped<TextToSpeechService>();

// Comment Service
builder.Services.AddScoped<ICommentService, CommentService>();

// Logging
builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddConsole();
    loggingBuilder.AddDebug();
    loggingBuilder.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
    loggingBuilder.AddFilter("VotingSystem.Services.TesseractOCRService", LogLevel.Information);
});

var app = builder.Build();

// ================= PIPELINE =================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();

    // Ensure tessdata folder exists
    var tessDataPath = Path.Combine(Directory.GetCurrentDirectory(), "tessdata");
    if (!Directory.Exists(tessDataPath))
    {
        Directory.CreateDirectory(tessDataPath);
        Console.WriteLine($"Created tessdata directory at: {tessDataPath}");
    }
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Disable Cache
app.Use(async (context, next) =>
{
    context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
    context.Response.Headers["Pragma"] = "no-cache";
    context.Response.Headers["Expires"] = "0";
    await next();
});

app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();