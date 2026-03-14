using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VotingSystem.Data;
using VotingSystem.Models;
using VotingSystem.Services;
using System.Diagnostics;
using System.Threading.Tasks;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace VotingSystem.Controllers
{
    public class TempPasswordRecord
    {
        public string NationalId { get; set; }
        public string Password { get; set; }
        public string HashedPassword { get; set; }
        public DateTime ExpiryTime { get; set; }
        public string UserType { get; set; }
        public bool IsUsed { get; set; }
    }

    // Visual Login Model
    public class VisualLoginModel
    {
        public string FrontImageBase64 { get; set; }
        public string BackImageBase64 { get; set; }
        public string NationalId { get; set; }
        public string[] SelectedSymbols { get; set; }
        public List<string> SelectedImages { get; set; } = new List<string>();
        public List<int> SelectedImageIds { get; set; } = new List<int>(); // NEW: Added for image IDs
        public IFormFile NationalIdFront { get; set; }
        public IFormFile NationalIdBack { get; set; }
        public string ExtractedNationalId { get; set; }
    }

    // Request models for One-Time Password
    public class OneTimePasswordRequest
    {
        public string NationalId { get; set; }
    }

    public class VerifyOneTimePasswordRequest
    {
        public string NationalId { get; set; }
        public string[] SelectedImages { get; set; }
        public int[] SelectedImageIds { get; set; } // NEW: Added for image IDs
    }

    public class UpdateImagePasswordRequest
    {
        public string NationalId { get; set; }
        public string[] NewImages { get; set; }
        public int[] NewImageIds { get; set; } // NEW: Added for image IDs
    }

    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IOCRService _ocrService;
        private readonly ILogger<HomeController> _logger;
        private readonly ILoginTrackingService _loginTrackingService;
        private readonly IConfiguration _configuration;
        private static readonly Dictionary<string, TempPasswordRecord> _tempPasswords = new Dictionary<string, TempPasswordRecord>();

        // Image mapping for ID to emoji conversion
        private static readonly Dictionary<int, string> ImageIdToEmoji = new Dictionary<int, string>
        {
            { 1, "🌍" },   // Globe
            { 2, "🏔️" },   // Mountain
            { 3, "☕" },    // Coffee
            { 4, "🌾" },   // Wheat
            { 5, "🐃" },   // Buffalo
            { 6, "🦁" },   // Lion
            { 7, "✈️" },   // Airplane
            { 8, "🏛️" },   // Building
            { 9, "📜" },   // Scroll
            { 10, "🔰" }   // Shield
        };

        private static readonly Dictionary<string, int> EmojiToImageId = new Dictionary<string, int>
        {
            { "🌍", 1 },
            { "🏔️", 2 },
            { "☕", 3 },
            { "🌾", 4 },
            { "🐃", 5 },
            { "🦁", 6 },
            { "✈️", 7 },
            { "🏛️", 8 },
            { "📜", 9 },
            { "🔰", 10 }
        };

        public HomeController(AppDbContext context, IOCRService ocrService, ILogger<HomeController> logger, 
                            ILoginTrackingService loginTrackingService, IConfiguration configuration)
        {
            _context = context;
            _ocrService = ocrService;
            _logger = logger;
            _loginTrackingService = loginTrackingService;
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contact(ContactForm model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    model.SubmittedAt = DateTime.Now;
                    model.IsRead = false;
                    
                    _logger.LogInformation($"Contact form submitted: Name: {model.Name}, Email: {model.Email}, Subject: {model.Subject}, Message: {model.Message}");
                    
                    TempData["SuccessMessage"] = "Thank you for your message! We'll get back to you soon.";
                    return RedirectToAction("Contact");
                }
                
                TempData["ErrorMessage"] = "Please fill all required fields correctly.";
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving contact form");
                TempData["ErrorMessage"] = "An error occurred while sending your message. Please try again.";
                return View(model);
            }
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password)
        {
            string loginStatus = "Failed";
            string loginDetails = "";
            string userRole = "";
            string userName = "";

            try
            {
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    loginDetails = "National ID and password are required.";
                    TempData["ErrorMessage"] = loginDetails;
                    
                    await _loginTrackingService.LogLoginAttemptAsync(username ?? "Unknown", "Unknown", "Failed", 
                        "Login failed: Missing National ID or password", HttpContext, "MissingCredentials");
                    return View();
                }

                username = username.Trim();
                password = password.Trim();

                if (!IsValidEthiopianNationalId(username))
                {
                    loginDetails = "Please enter a valid Ethiopian National ID (15-17 digits).";
                    TempData["ErrorMessage"] = loginDetails;
                    
                    await _loginTrackingService.LogLoginAttemptAsync(username, "Unknown", "Failed", 
                        "Login failed: Invalid National ID format", HttpContext, "InvalidNationalId");
                    return View();
                }

                _logger.LogInformation($"Login attempt for National ID: {username}");

                await _loginTrackingService.LogLoginAttemptAsync(username, "Unknown", "Attempt", 
                    $"Login attempt started for National ID: {username}", HttpContext, "RegularLogin");

                // Clean expired temporary passwords first
                CleanExpiredTempPasswords();

                // 1. Check Admin table
                var admin = await _context.Admins
                    .Where(a => a.NationalId == username)
                    .Select(a => new 
                    {
                        a.NationalId,
                        a.Password,
                        a.FirstName,
                        a.MiddleName,
                        a.LastName
                    })
                    .FirstOrDefaultAsync();
                
                if (admin != null)
                {
                    _logger.LogInformation($"Admin found: {admin.NationalId}");
                    userRole = "Admin";
                    userName = $"{admin.FirstName} {admin.MiddleName} {admin.LastName}";
                    
                    bool isTempValid = IsTempPasswordValid(username, password);
                    bool isDbPasswordValid = admin.Password == password;
                    
                    if (isDbPasswordValid || isTempValid)
                    {
                        if (isTempValid)
                        {
                            if (_tempPasswords.TryGetValue(username, out var tempRecord))
                            {
                                tempRecord.IsUsed = true;
                                _logger.LogInformation($"Temporary password marked as used for login: {username}");
                            }
                            _tempPasswords.Remove(username);
                            _logger.LogInformation($"Temporary password used and removed for: {username}");
                        }
                        
                        HttpContext.Session.SetString("AdminNationalId", admin.NationalId);
                        HttpContext.Session.SetString("AdminName", $"{admin.FirstName} {admin.MiddleName} {admin.LastName}");
                        HttpContext.Session.SetString("UserRole", "Admin");
                        HttpContext.Session.SetString("LoginNationalId", admin.NationalId);
                        
                        loginStatus = "Success";
                        loginDetails = $"Admin login successful: {userName}";
                        TempData["SuccessMessage"] = $"Welcome Admin {admin.FirstName}!";
                        
                        await _loginTrackingService.LogLoginAttemptAsync(username, "Admin", "Success", 
                            loginDetails, HttpContext, "PasswordCorrect");
                        await _loginTrackingService.LogUserActivityAsync(username, "Admin", "Post-Login", 
                            $"Admin successfully logged into the system", HttpContext, "DashboardAccess");
                            
                        return RedirectToAction("Dashboard", "Admin");
                    }
                    else
                    {
                        loginStatus = "Failed";
                        loginDetails = "Password is incorrect for Admin account.";
                        TempData["ErrorMessage"] = loginDetails;
                        
                        await _loginTrackingService.LogLoginAttemptAsync(username, "Admin", "Failed", 
                            "Login failed: Incorrect password for Admin account", HttpContext, "WrongPassword");
                        return View();
                    }
                }

                // 2. Check Supervisor table
                string hashedInputPassword = HashPasswordBase64(password);
                _logger.LogInformation($"Hashed input password: {hashedInputPassword}");

                var supervisor = await _context.Supervisors
                    .Where(s => s.NationalId == username)
                    .Select(s => new 
                    {
                        s.NationalId,
                        s.Password,
                        s.FirstName,
                        s.MiddleName,
                        s.LastName,
                        s.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (supervisor != null)
                {
                    _logger.LogInformation($"Supervisor found: {supervisor.NationalId}");
                    userRole = "Supervisor";
                    userName = $"{supervisor.FirstName} {supervisor.MiddleName} {supervisor.LastName}";
                    
                    if (!supervisor.IsActive)
                    {
                        loginStatus = "Failed";
                        loginDetails = "Your supervisor account has been deactivated. Please contact administrator.";
                        TempData["ErrorMessage"] = loginDetails;
                        
                        await _loginTrackingService.LogLoginAttemptAsync(username, "Supervisor", "Failed", 
                            "Login failed: Supervisor account deactivated", HttpContext, "AccountInactive");
                        return View();
                    }
                    
                    bool isTempValid = IsTempPasswordValid(username, password);
                    bool isDbPasswordValid = supervisor.Password == hashedInputPassword;
                    
                    if (isDbPasswordValid || isTempValid)
                    {
                        if (isTempValid)
                        {
                            if (_tempPasswords.TryGetValue(username, out var tempRecord))
                            {
                                tempRecord.IsUsed = true;
                                _logger.LogInformation($"Temporary password marked as used for login: {username}");
                            }
                            _tempPasswords.Remove(username);
                            _logger.LogInformation($"Temporary password used and removed for: {username}");
                        }
                        
                        HttpContext.Session.SetString("SupervisorNationalId", supervisor.NationalId);
                        HttpContext.Session.SetString("SupervisorName", $"{supervisor.FirstName} {supervisor.MiddleName} {supervisor.LastName}");
                        HttpContext.Session.SetString("UserRole", "Supervisor");
                        HttpContext.Session.SetString("LoginNationalId", supervisor.NationalId);
                        
                        loginStatus = "Success";
                        loginDetails = $"Supervisor login successful: {userName}";
                        TempData["SuccessMessage"] = $"Welcome Supervisor {supervisor.FirstName}!";
                        
                        await _loginTrackingService.LogLoginAttemptAsync(username, "Supervisor", "Success", 
                            loginDetails, HttpContext, "PasswordCorrect");
                        await _loginTrackingService.LogUserActivityAsync(username, "Supervisor", "Post-Login", 
                            $"Supervisor successfully logged into the system", HttpContext, "DashboardAccess");
                            
                        return RedirectToAction("Dashboard", "Supervisor");
                    }
                    else
                    {
                        loginStatus = "Failed";
                        loginDetails = "Password is incorrect for Supervisor account.";
                        TempData["ErrorMessage"] = loginDetails;
                        
                        await _loginTrackingService.LogLoginAttemptAsync(username, "Supervisor", "Failed", 
                            "Login failed: Incorrect password for Supervisor account", HttpContext, "WrongPassword");
                        return View();
                    }
                }

                // 3. Check Manager table
                var manager = await _context.Managers
                    .Where(m => m.NationalId == username)
                    .Select(m => new 
                    {
                        m.NationalId,
                        m.Password,
                        m.FirstName,
                        m.MiddleName,
                        m.LastName,
                        m.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (manager != null)
                {
                    _logger.LogInformation($"Manager found: {manager.NationalId}");
                    userRole = "Manager";
                    userName = $"{manager.FirstName} {manager.MiddleName} {manager.LastName}";
                    
                    if (!manager.IsActive)
                    {
                        loginStatus = "Failed";
                        loginDetails = "Your manager account has been deactivated. Please contact administrator.";
                        TempData["ErrorMessage"] = loginDetails;
                        
                        await _loginTrackingService.LogLoginAttemptAsync(username, "Manager", "Failed", 
                            "Login failed: Manager account deactivated", HttpContext, "AccountInactive");
                        return View();
                    }
                    
                    bool isTempValid = IsTempPasswordValid(username, password);
                    bool isDbPasswordValid = manager.Password == hashedInputPassword;
                    
                    if (isDbPasswordValid || isTempValid)
                    {
                        if (isTempValid)
                        {
                            if (_tempPasswords.TryGetValue(username, out var tempRecord))
                            {
                                tempRecord.IsUsed = true;
                                _logger.LogInformation($"Temporary password marked as used for login: {username}");
                            }
                            _tempPasswords.Remove(username);
                            _logger.LogInformation($"Temporary password used and removed for: {username}");
                        }
                        
                        HttpContext.Session.SetString("ManagerNationalId", manager.NationalId);
                        HttpContext.Session.SetString("ManagerName", $"{manager.FirstName} {manager.MiddleName} {manager.LastName}");
                        HttpContext.Session.SetString("UserRole", "Manager");
                        HttpContext.Session.SetString("LoginNationalId", manager.NationalId);
                        
                        loginStatus = "Success";
                        loginDetails = $"Manager login successful: {userName}";
                        TempData["SuccessMessage"] = $"Welcome Manager {manager.FirstName}!";
                        
                        await _loginTrackingService.LogLoginAttemptAsync(username, "Manager", "Success", 
                            loginDetails, HttpContext, "PasswordCorrect");
                        await _loginTrackingService.LogUserActivityAsync(username, "Manager", "Post-Login", 
                            $"Manager successfully logged into the system", HttpContext, "DashboardAccess");
                            
                        return RedirectToAction("Dashboard", "Manager");
                    }
                    else
                    {
                        loginStatus = "Failed";
                        loginDetails = "Password is incorrect for Manager account.";
                        TempData["ErrorMessage"] = loginDetails;
                        
                        await _loginTrackingService.LogLoginAttemptAsync(username, "Manager", "Failed", 
                            "Login failed: Incorrect password for Manager account", HttpContext, "WrongPassword");
                        return View();
                    }
                }

                // 4. Check Voter table
                var voter = await _context.Voters
                    .Where(v => v.NationalId == username)
                    .Select(v => new 
                    {
                        v.NationalId,
                        v.Password,
                        v.FirstName,
                        v.MiddleName,
                        v.LastName,
                        v.Literate
                    })
                    .FirstOrDefaultAsync();

                if (voter != null)
                {
                    _logger.LogInformation($"Voter found: {voter.NationalId}, Literate: {voter.Literate}");
                    userRole = "Voter";
                    userName = $"{voter.FirstName} {voter.MiddleName} {voter.LastName}";
                    
                    if (voter.Literate == "No")
                    {
                        loginStatus = "Failed";
                        loginDetails = "This account requires visual login. Please use the 'Visual Login for Illiterate Voters' option.";
                        TempData["ErrorMessage"] = loginDetails;
                        
                        await _loginTrackingService.LogLoginAttemptAsync(username, "Voter", "Failed", 
                            "Login failed: Voter requires visual login (illiterate)", HttpContext, "RequiresVisualLogin");
                        return View();
                    }
                    
                    bool isTempValid = IsTempPasswordValid(username, password);
                    bool isDbPasswordValid = voter.Password == hashedInputPassword;
                    
                    if (isDbPasswordValid || isTempValid)
                    {
                        if (isTempValid)
                        {
                            if (_tempPasswords.TryGetValue(username, out var tempRecord))
                            {
                                tempRecord.IsUsed = true;
                                _logger.LogInformation($"Temporary password marked as used for login: {username}");
                            }
                            _tempPasswords.Remove(username);
                            _logger.LogInformation($"Temporary password used and removed for: {username}");
                        }
                        
                        HttpContext.Session.SetString("VoterNationalId", voter.NationalId);
                        HttpContext.Session.SetString("VoterName", $"{voter.FirstName} {voter.MiddleName} {voter.LastName}");
                        HttpContext.Session.SetString("UserRole", "Voter");
                        HttpContext.Session.SetString("LoginNationalId", voter.NationalId);
                        
                        loginStatus = "Success";
                        loginDetails = $"Voter login successful: {userName}";
                        TempData["SuccessMessage"] = $"Welcome Voter {voter.FirstName}!";
                        
                        await _loginTrackingService.LogLoginAttemptAsync(username, "Voter", "Success", 
                            loginDetails, HttpContext, "PasswordCorrect");
                        await _loginTrackingService.LogUserActivityAsync(username, "Voter", "Post-Login", 
                            $"Voter successfully logged into the system", HttpContext, "DashboardAccess");
                            
                        return RedirectToAction("Dashboard", "Voter");
                    }
                    else
                    {
                        loginStatus = "Failed";
                        loginDetails = "Password is incorrect for Voter account.";
                        TempData["ErrorMessage"] = loginDetails;
                        
                        await _loginTrackingService.LogLoginAttemptAsync(username, "Voter", "Failed", 
                            "Login failed: Incorrect password for Voter account", HttpContext, "WrongPassword");
                        return View();
                    }
                }

                // 5. If no user found
                loginStatus = "Failed";
                loginDetails = $"National ID '{username}' was not found in the system.";
                TempData["ErrorMessage"] = loginDetails;
                
                await _loginTrackingService.LogLoginAttemptAsync(username, "Unknown", "Failed", 
                    "Login failed: National ID not found in system", HttpContext, "NationalIdNotFound");
                return View();

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login process");
                loginStatus = "Failed";
                loginDetails = $"Login error: {ex.Message}";
                TempData["ErrorMessage"] = "An error occurred during login. Please try again.";
                
                await _loginTrackingService.LogLoginAttemptAsync(username, userRole ?? "Unknown", "Failed", 
                    $"Login failed due to system error: {ex.Message}", HttpContext, "SystemError");
                return View();
            }
        }

        // ========== VOTER REGISTRATION WITH OCR FUNCTIONALITY ==========

        public IActionResult Register()
        {
            return View();
        }

        // OCR Processing for Voter Registration
        [HttpPost]
        public async Task<IActionResult> ProcessIDPhotos(IFormFile FrontImage, IFormFile BackImage)
        {
            try
            {
                _logger.LogInformation("=== STARTING ID PHOTO PROCESSING FOR VOTER REGISTRATION ===");

                if (FrontImage == null || FrontImage.Length == 0)
                {
                    return Json(new { success = false, error = "Front ID image is required." });
                }

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp" };
                var maxFileSize = 5 * 1024 * 1024;

                if (FrontImage.Length > maxFileSize)
                {
                    return Json(new { success = false, error = "Front image size must be less than 5MB." });
                }

                if (BackImage != null && BackImage.Length > maxFileSize)
                {
                    return Json(new { success = false, error = "Back image size must be less than 5MB." });
                }

                string frontImageBase64 = null;
                string backImageBase64 = null;

                using (var ms = new MemoryStream())
                {
                    await FrontImage.CopyToAsync(ms);
                    frontImageBase64 = Convert.ToBase64String(ms.ToArray());
                    _logger.LogInformation($"Front image converted to base64, length: {frontImageBase64.Length}");
                }

                if (BackImage != null && BackImage.Length > 0)
                {
                    using (var ms = new MemoryStream())
                    {
                        await BackImage.CopyToAsync(ms);
                        backImageBase64 = Convert.ToBase64String(ms.ToArray());
                        _logger.LogInformation($"Back image converted to base64, length: {backImageBase64.Length}");
                    }
                }

                _logger.LogInformation("Calling Ethiopian OCR service for voter registration...");
                var result = await _ocrService.ProcessEthiopianIDAsync(frontImageBase64, backImageBase64);
                
                _logger.LogInformation("=== RAW OCR RESULT FOR VOTER REGISTRATION ===");
                _logger.LogInformation($"NationalId: {result?.NationalId ?? "NULL"}");
                _logger.LogInformation($"FirstName: {result?.FirstName ?? "NULL"}");
                _logger.LogInformation($"MiddleName: {result?.MiddleName ?? "NULL"}");
                _logger.LogInformation($"LastName: {result?.LastName ?? "NULL"}");
                _logger.LogInformation($"PhoneNumber: {result?.PhoneNumber ?? "NULL"}");
                _logger.LogInformation($"Sex: {result?.Sex ?? "NULL"}");
                _logger.LogInformation($"DateOfBirth: {result?.DateOfBirth ?? "NULL"}");
                _logger.LogInformation($"Region: {result?.Region ?? "NULL"}");
                _logger.LogInformation($"Nationality: {result?.Nationality ?? "NULL"}");
                _logger.LogInformation($"Age: {result?.Age ?? 0}");
                _logger.LogInformation($"Success: {result?.Success ?? false}");
                _logger.LogInformation($"Error: {result?.Error ?? "NULL"}");
                _logger.LogInformation("=== END RAW OCR RESULT ===");

                if (result != null && result.Success)
                {
                    int age = result.Age;
                    if (age <= 0 && !string.IsNullOrEmpty(result.DateOfBirth))
                    {
                        age = CalculateAgeFromDateOfBirth(result.DateOfBirth);
                    }

                    return Json(new { 
                        success = true, 
                        data = new {
                            nationalId = result.NationalId ?? "",
                            firstName = result.FirstName ?? "",
                            middleName = result.MiddleName ?? "",
                            lastName = result.LastName ?? "",
                            phoneNumber = result.PhoneNumber ?? "",
                            nationality = result.Nationality ?? "Ethiopian",
                            region = result.Region ?? "",
                            sex = result.Sex ?? "",
                            age = age > 0 ? age : 0,
                            dateOfBirth = result.DateOfBirth ?? ""
                        },
                        message = "ID information extracted successfully!"
                    });
                }
                else
                {
                    string errorMessage = result?.Error ?? "Could not extract information from ID card.";
                    return Json(new { 
                        success = false, 
                        error = $"{errorMessage} Please ensure the ID image is clear and all text is visible." 
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing ID photos for voter registration");
                return Json(new { 
                    success = false, 
                    error = $"Failed to process ID: {ex.Message}" 
                });
            }
        }

        private int CalculateAgeFromDateOfBirth(string dateOfBirth)
        {
            try
            {
                DateTime dob;
                if (DateTime.TryParse(dateOfBirth, out dob))
                {
                    var today = DateTime.Today;
                    var age = today.Year - dob.Year;
                    if (dob.Date > today.AddYears(-age)) age--;
                    return age >= 18 && age <= 120 ? age : 0;
                }
                
                if (dateOfBirth.Contains("/"))
                {
                    var parts = dateOfBirth.Split('/');
                    if (parts.Length == 3 && int.TryParse(parts[2], out int year))
                    {
                        var gregorianYear = year;
                        if (year < 1000)
                        {
                            gregorianYear = year + 8;
                        }
                        var age = DateTime.Now.Year - gregorianYear;
                        return age >= 18 && age <= 120 ? age : 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating age from date of birth");
            }
            return 0;
        }

        // National ID Check for Voter Registration
        [HttpGet]
        public async Task<IActionResult> CheckNationalId(string nationalId)
        {
            if (string.IsNullOrEmpty(nationalId))
            {
                return Json(new { exists = false, message = "" });
            }

            nationalId = nationalId.Trim();

            try
            {
                var checkResult = await CheckNationalIdInAllTablesDetailed(nationalId);
                return Json(new { exists = checkResult.exists, message = checkResult.message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking National ID");
                return Json(new { 
                    exists = false, 
                    message = "Error checking National ID availability." 
                });
            }
        }

        // Enhanced method to check National ID across ALL tables with detailed messages
        private async Task<(bool exists, string message)> CheckNationalIdInAllTablesDetailed(string nationalId)
        {
            try
            {
                _logger.LogInformation($"Comprehensive National ID check: {nationalId}");

                // Check Voters table
                var voter = await _context.Voters
                    .AsNoTracking()
                    .Where(v => v.NationalId == nationalId)
                    .Select(v => new { v.FirstName, v.LastName })
                    .FirstOrDefaultAsync();
                
                if (voter != null)
                {
                    return (true, $"National ID {nationalId} is already registered as Voter: {voter.FirstName} {voter.LastName}");
                }

                // Check Admins table
                var admin = await _context.Admins
                    .AsNoTracking()
                    .Where(a => a.NationalId == nationalId)
                    .Select(a => new { a.FirstName, a.LastName })
                    .FirstOrDefaultAsync();
                
                if (admin != null)
                {
                    return (true, $"National ID {nationalId} is already registered as Admin: {admin.FirstName} {admin.LastName}");
                }

                // Check Managers table
                var manager = await _context.Managers
                    .AsNoTracking()
                    .Where(m => m.NationalId == nationalId)
                    .Select(m => new { m.FirstName, m.LastName })
                    .FirstOrDefaultAsync();
                
                if (manager != null)
                {
                    return (true, $"National ID {nationalId} is already registered as Manager: {manager.FirstName} {manager.LastName}");
                }

                // Check Supervisors table
                var supervisor = await _context.Supervisors
                    .AsNoTracking()
                    .Where(s => s.NationalId == nationalId)
                    .Select(s => new { s.FirstName, s.LastName })
                    .FirstOrDefaultAsync();
                
                if (supervisor != null)
                {
                    return (true, $"National ID {nationalId} is already registered as Supervisor: {supervisor.FirstName} {supervisor.LastName}");
                }

                // Check Candidates table
                var candidate = await _context.Candidates
                    .AsNoTracking()
                    .Where(c => c.NationalId == nationalId)
                    .Select(c => new { c.FirstName, c.LastName })
                    .FirstOrDefaultAsync();
                
                if (candidate != null)
                {
                    return (true, $"National ID {nationalId} is already registered as Candidate: {candidate.FirstName} {candidate.LastName}");
                }

                _logger.LogInformation($"National ID {nationalId} is available for registration");
                return (false, "This National ID is available for registration.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in comprehensive National ID check");
                return (true, "Error checking National ID availability. Please try again.");
            }
        }

        // Add this helper method to HomeController class
        private string CleanNationalId(string nationalId)
        {
            if (string.IsNullOrEmpty(nationalId))
                return nationalId;
            
            return Regex.Replace(nationalId, @"[^\d]", "");
        }

        // Voter Registration - Enhanced with comprehensive duplicate checking
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            [Bind("NationalId,Nationality,Region,PhoneNumber,FirstName,MiddleName,LastName,Age,Sex,Password")] Voter voter, 
            string Literate, 
            string ConfirmPassword,
            string ImagePassword) // This should contain comma-separated image IDs now
        {
            DateTime? tempExpiryTime = null; // Declare variable at method scope
            string tempPassword = null; // Declare variable at method scope
            
            try
            {
                _logger.LogInformation("=== STARTING VOTER REGISTRATION ===");

                // Manual validation instead of relying on ModelState
                if (string.IsNullOrWhiteSpace(voter.NationalId) ||
                    string.IsNullOrWhiteSpace(voter.FirstName) ||
                    string.IsNullOrWhiteSpace(voter.LastName) ||
                    string.IsNullOrWhiteSpace(voter.PhoneNumber) ||
                    string.IsNullOrWhiteSpace(Literate))
                {
                    TempData["ErrorMessage"] = "All required fields must be filled.";
                    return View(voter);
                }

                // Clean and validate inputs
                voter.NationalId = voter.NationalId.Trim();
                voter.FirstName = voter.FirstName.Trim();
                voter.MiddleName = voter.MiddleName?.Trim() ?? "";
                voter.LastName = voter.LastName.Trim();
                voter.PhoneNumber = voter.PhoneNumber.Trim();
                voter.Region = voter.Region?.Trim() ?? "";
                voter.Nationality = voter.Nationality?.Trim() ?? "Ethiopian";
                voter.Literate = Literate;

                // Validate Ethiopian National ID
                if (!IsValidEthiopianNationalId(voter.NationalId))
                {
                    TempData["ErrorMessage"] = "Please enter a valid Ethiopian National ID (15-17 digits).";
                    return View(voter);
                }

                // ENHANCED: Comprehensive duplicate National ID check across ALL tables
                var duplicateCheck = await CheckNationalIdInAllTablesDetailed(voter.NationalId);
                if (duplicateCheck.exists)
                {
                    _logger.LogWarning($"Duplicate National ID detected: {voter.NationalId} - {duplicateCheck.message}");
                    TempData["ErrorMessage"] = duplicateCheck.message;
                    ModelState.AddModelError("NationalId", duplicateCheck.message);
                    return View(voter);
                }

                // Check for duplicate phone number in Voters table only
                var existingVoterByPhone = await _context.Voters
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v => v.PhoneNumber == voter.PhoneNumber);
                
                if (existingVoterByPhone != null)
                {
                    _logger.LogWarning($"Duplicate phone number detected: {voter.PhoneNumber}");
                    TempData["ErrorMessage"] = "Phone number already registered as a voter.";
                    ModelState.AddModelError("PhoneNumber", "This phone number is already registered.");
                    return View(voter);
                }

                // Validate age
                if (voter.Age < 18 || voter.Age > 120)
                {
                    TempData["ErrorMessage"] = "Age must be between 18 and 120.";
                    ModelState.AddModelError("Age", "Age must be between 18 and 120.");
                    return View(voter);
                }

                // Validate sex
                if (voter.Sex != "Male" && voter.Sex != "Female")
                {
                    TempData["ErrorMessage"] = "Sex must be Male or Female.";
                    ModelState.AddModelError("Sex", "Sex must be Male or Female.");
                    return View(voter);
                }

                // Handle password based on literacy
                if (voter.Literate == "Yes")
                {
                    if (string.IsNullOrWhiteSpace(voter.Password))
                    {
                        TempData["ErrorMessage"] = "Password is required for literate users.";
                        ModelState.AddModelError("Password", "Password is required for literate users.");
                        return View(voter);
                    }

                    if (voter.Password.Length < 6)
                    {
                        TempData["ErrorMessage"] = "Password must be at least 6 characters long.";
                        ModelState.AddModelError("Password", "Password must be at least 6 characters long.");
                        return View(voter);
                    }

                    if (voter.Password != ConfirmPassword)
                    {
                        TempData["ErrorMessage"] = "Password and confirmation password do not match.";
                        ModelState.AddModelError("Password", "Password and confirmation password do not match.");
                        return View(voter);
                    }

                    // Hash the text password
                    voter.Password = HashPasswordBase64(voter.Password);
                    voter.VisualPIN = "1,3,4,8"; // Default image IDs for literate users (can be changed)
                }
                else
                {
                    // For illiterate voters, generate a temporary password meeting the criteria
                    tempPassword = GenerateSecureTemporaryPassword();
                    
                    // Store the temporary password in memory for login
                    tempExpiryTime = DateTime.Now.AddMinutes(10);
                    _tempPasswords[voter.NationalId] = new TempPasswordRecord
                    {
                        NationalId = voter.NationalId,
                        Password = tempPassword,
                        HashedPassword = HashPasswordBase64(tempPassword),
                        ExpiryTime = tempExpiryTime.Value,
                        UserType = "Voter",
                        IsUsed = false
                    };
                    
                    // Hash and store the temporary password in database
                    voter.Password = HashPasswordBase64(tempPassword);
                    
                    if (string.IsNullOrWhiteSpace(ImagePassword))
                    {
                        TempData["ErrorMessage"] = "Image password is required for illiterate users.";
                        ModelState.AddModelError("ImagePassword", "Please select 4 images as your password.");
                        return View(voter);
                    }

                    var selectedImageIds = ImagePassword.Split(',').Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
                    if (selectedImageIds.Count != 4)
                    {
                        TempData["ErrorMessage"] = "Please select exactly 4 images for your password.";
                        ModelState.AddModelError("ImagePassword", "Please select exactly 4 images.");
                        return View(voter);
                    }

                    // Validate that all selected IDs are valid (1-10)
                    foreach (var idStr in selectedImageIds)
                    {
                        if (!int.TryParse(idStr, out int id) || id < 1 || id > 10)
                        {
                            TempData["ErrorMessage"] = $"Invalid image ID selected: {idStr}. Please select valid images (1-10).";
                            ModelState.AddModelError("ImagePassword", "Please select valid images.");
                            return View(voter);
                        }
                    }

                    // Store the image IDs as comma-separated string (e.g., "1,3,5,7")
                    voter.VisualPIN = string.Join(",", selectedImageIds);
                }

                // Set common voter properties
                voter.RegisterDate = DateTime.Now;
                voter.CreatedAt = DateTime.Now;
                voter.UpdatedAt = DateTime.Now;

                // Set default values for visual login
                voter.QRCodeData = Guid.NewGuid().ToString();
                voter.PrefersVisualLogin = (voter.Literate == "No");

                _logger.LogInformation($"Attempting to save voter: {voter.NationalId}, Literate: {voter.Literate}, VisualPIN: {voter.VisualPIN}");

                try
                {
                    _context.Voters.Add(voter);
                    int recordsAffected = await _context.SaveChangesAsync();

                    if (recordsAffected > 0)
                    {
                        _logger.LogInformation($"SUCCESS: Voter {voter.NationalId} registered successfully as {(voter.Literate == "Yes" ? "Literate" : "Illiterate")}");
                        
                        // Log registration activity
                        await _loginTrackingService.LogUserActivityAsync(voter.NationalId, "Voter", "Registration", 
                            $"Voter registered: {voter.FirstName} {voter.LastName} as {(voter.Literate == "Yes" ? "Literate" : "Illiterate")}", HttpContext);
                        
                        if (voter.Literate == "No")
                        {
                            // For illiterate voters, show temporary password and visual login instruction
                            TempData["IlliterateVoterSuccess"] = "true";
                            TempData["GeneratedPassword"] = tempPassword;
                            TempData["ExpiryTime"] = tempExpiryTime?.ToString("yyyy-MM-dd HH:mm:ss");
                            TempData["VoterNationalId"] = voter.NationalId;
                            TempData["SuccessMessage"] = $"✅ Voter {voter.FirstName} {voter.LastName} (ID: {voter.NationalId}) registered successfully as Illiterate!<br><br>" +
                                                      $"<strong>Temporary Password:</strong> {tempPassword}<br>" +
                                                      $"<strong>Password Expires:</strong> {tempExpiryTime?.ToString("yyyy-MM-dd HH:mm:ss")}<br><br>" +
                                                      $"<strong>Important:</strong> This account requires visual login. Please use the 'Visual Login for Illiterate Voters' option after logging in with this temporary password to set your image password.";
                        }
                        else
                        {
                            TempData["SuccessMessage"] = $"✅ Voter {voter.FirstName} {voter.LastName} (ID: {voter.NationalId}) registered successfully as Literate!";
                        }
                        
                        TempData["RegistrationSuccess"] = "true";
                        
                        return View(new Voter());
                    }
                    else
                    {
                        _logger.LogWarning("No records were affected during save");
                        TempData["ErrorMessage"] = "Registration failed. Please try again.";
                        return View(voter);
                    }
                }
                catch (DbUpdateException dbEx)
                {
                    _logger.LogError(dbEx, "Database error during voter registration");
                    
                    string errorMessage = "Registration failed due to database error.";
                    if (dbEx.InnerException != null)
                    {
                        string innerMsg = dbEx.InnerException.Message;
                        _logger.LogError($"Inner Exception: {innerMsg}");
                        
                        if (innerMsg.Contains("PRIMARY KEY") || innerMsg.Contains("duplicate"))
                        {
                            var detailedCheck = await CheckNationalIdInAllTablesDetailed(voter.NationalId);
                            errorMessage = detailedCheck.exists ? detailedCheck.message : $"National ID {voter.NationalId} is already registered.";
                        }
                        else if (innerMsg.Contains("UNIQUE") && innerMsg.Contains("PhoneNumber"))
                        {
                            errorMessage = "Phone number already registered.";
                        }
                    }
                    
                    TempData["ErrorMessage"] = errorMessage;
                    return View(voter);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during voter registration");
                TempData["ErrorMessage"] = $"❌ An unexpected error occurred: {ex.Message}";
                return View(voter);
            }
        }

        // ========== VISUAL LOGIN FUNCTIONALITY ==========

        [HttpGet]
        public IActionResult VisualLogin()
        {
            return View();
        }

        // NEW METHOD: Process National ID for Visual Login (Simplified)
        [HttpPost]
        public async Task<IActionResult> ExtractNationalIDFromImage(IFormFile frontImage, IFormFile backImage)
        {
            try
            {
                _logger.LogInformation("Extracting National ID from image for visual login");

                if (frontImage == null || frontImage.Length == 0)
                {
                    return Json(new { success = false, message = "Front ID image is required." });
                }

                string frontImageBase64;
                string backImageBase64 = null;

                using (var ms = new MemoryStream())
                {
                    await frontImage.CopyToAsync(ms);
                    frontImageBase64 = Convert.ToBase64String(ms.ToArray());
                }

                if (backImage != null && backImage.Length > 0)
                {
                    using (var ms = new MemoryStream())
                    {
                        await backImage.CopyToAsync(ms);
                        backImageBase64 = Convert.ToBase64String(ms.ToArray());
                    }
                }

                var ocrResult = await _ocrService.ProcessEthiopianIDAsync(frontImageBase64, backImageBase64);
                
                if (ocrResult != null && ocrResult.Success && !string.IsNullOrEmpty(ocrResult.NationalId))
                {
                    // Clean the National ID
                    string cleanNationalId = CleanNationalId(ocrResult.NationalId);
                    
                    // Check if this is a voter and is illiterate
                    var voter = await _context.Voters.FirstOrDefaultAsync(v => v.NationalId == cleanNationalId);
                    
                    if (voter != null)
                    {
                        if (voter.Literate == "Yes")
                        {
                            return Json(new { 
                                success = false, 
                                message = "This account is registered as literate. Please use the regular login." 
                            });
                        }

                        return Json(new { 
                            success = true, 
                            nationalId = cleanNationalId,
                            firstName = ocrResult.FirstName ?? "",
                            lastName = ocrResult.LastName ?? "",
                            message = "National ID extracted successfully!"
                        });
                    }
                    else
                    {
                        return Json(new { 
                            success = false, 
                            message = "National ID not found in voter database." 
                        });
                    }
                }
                else
                {
                    return Json(new { 
                        success = false, 
                        message = "Could not read National ID from ID card. Please ensure the image is clear." 
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting National ID from image");
                return Json(new { 
                    success = false, 
                    message = "Failed to process ID card. Please try again." 
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessVisualLogin(VisualLoginModel model)
        {
            try
            {
                _logger.LogInformation("=== STARTING VISUAL LOGIN PROCESS ===");

                // Log visual login attempt
                await _loginTrackingService.LogLoginAttemptAsync(model.ExtractedNationalId ?? "Unknown", "Voter", "Attempt", 
                    "Visual login attempt for illiterate voter", HttpContext);

                // Validate National ID
                if (string.IsNullOrEmpty(model.ExtractedNationalId))
                {
                    await _loginTrackingService.LogLoginAttemptAsync("Unknown", "Voter", "Failed", 
                        "Visual login failed - Could not read National ID", HttpContext);
                    return Json(new { success = false, message = "Could not read National ID. Please try again." });
                }

                // Check if we have image IDs (new method) or emojis (backward compatibility)
                List<int> selectedImageIds = new List<int>();
                
                if (model.SelectedImageIds != null && model.SelectedImageIds.Any())
                {
                    // NEW: Using image IDs
                    selectedImageIds = model.SelectedImageIds;
                }
                else if (model.SelectedImages != null && model.SelectedImages.Any())
                {
                    // OLD: Convert emojis to image IDs (backward compatibility)
                    foreach (var emoji in model.SelectedImages)
                    {
                        if (EmojiToImageId.ContainsKey(emoji))
                        {
                            selectedImageIds.Add(EmojiToImageId[emoji]);
                        }
                    }
                }

                if (selectedImageIds.Count != 4)
                {
                    await _loginTrackingService.LogLoginAttemptAsync(model.ExtractedNationalId, "Voter", "Failed", 
                        "Visual login failed - Invalid number of images selected", HttpContext);
                    return Json(new { success = false, message = "Please select exactly 4 images for your password." });
                }

                // Check if National ID exists in voter table and is illiterate
                var voter = await _context.Voters.FirstOrDefaultAsync(v => v.NationalId == model.ExtractedNationalId);
                if (voter == null)
                {
                    await _loginTrackingService.LogLoginAttemptAsync(model.ExtractedNationalId, "Voter", "Failed", 
                        "Visual login failed - National ID not found", HttpContext);
                    return Json(new { success = false, message = "National ID not found in voter database." });
                }

                // Check if voter is actually illiterate
                if (voter.Literate == "Yes")
                {
                    await _loginTrackingService.LogLoginAttemptAsync(model.ExtractedNationalId, "Voter", "Failed", 
                        "Visual login failed - Account is literate", HttpContext);
                    return Json(new { 
                        success = false, 
                        message = "This account is registered as literate. Please use the regular login with National ID and password." 
                    });
                }

                // Convert selected image IDs to string format
                string selectedImagePassword = string.Join(",", selectedImageIds.Select(id => id.ToString()));

                // Debug logging to see what we're comparing
                _logger.LogInformation($"=== VISUAL LOGIN DEBUG ===");
                _logger.LogInformation($"NationalId: {model.ExtractedNationalId}");
                _logger.LogInformation($"Stored VisualPIN in DB: '{voter.VisualPIN}'");
                _logger.LogInformation($"Selected image IDs: {string.Join(",", selectedImageIds)}");
                _logger.LogInformation($"Submitted image password string: '{selectedImagePassword}'");
                
                // Check if VisualPIN is empty or null
                if (string.IsNullOrWhiteSpace(voter.VisualPIN))
                {
                    _logger.LogError($"VisualPIN is empty or null for voter: {model.ExtractedNationalId}");
                    await _loginTrackingService.LogLoginAttemptAsync(model.ExtractedNationalId, "Voter", "Failed", 
                        "Visual login failed - No visual password set", HttpContext);
                    return Json(new { success = false, message = "No visual password set for this account. Please use forgot password." });
                }

                // IMPORTANT: Clean both strings before comparison
                string storedPassword = (voter.VisualPIN ?? "").Trim();
                string submittedPassword = (selectedImagePassword ?? "").Trim();

                _logger.LogInformation($"=== COMPARISON DEBUG ===");
                _logger.LogInformation($"Stored (cleaned): '{storedPassword}'");
                _logger.LogInformation($"Submitted (cleaned): '{submittedPassword}'");
                _logger.LogInformation($"Length stored: {storedPassword.Length}");
                _logger.LogInformation($"Length submitted: {submittedPassword.Length}");
                _logger.LogInformation($"Are equal? {storedPassword == submittedPassword}");

                // Compare image ID sequences
                bool isPasswordValid = storedPassword.Equals(submittedPassword, StringComparison.Ordinal);

                if (!isPasswordValid)
                {
                    _logger.LogWarning($"Password mismatch for {model.ExtractedNationalId}. DB: '{storedPassword}', Submitted: '{submittedPassword}'");
                    await _loginTrackingService.LogLoginAttemptAsync(model.ExtractedNationalId, "Voter", "Failed", 
                        $"Visual login failed - Incorrect image password. Expected: {storedPassword}, Got: {submittedPassword}", HttpContext);
                    return Json(new { success = false, message = "Incorrect image password. Please try again." });
                }

                // SUCCESS: Login the voter
                HttpContext.Session.SetString("VoterNationalId", voter.NationalId);
                HttpContext.Session.SetString("VoterName", $"{voter.FirstName} {voter.LastName}");
                HttpContext.Session.SetString("UserRole", "Voter");
                HttpContext.Session.SetString("LoginNationalId", voter.NationalId);

                _logger.LogInformation($"SUCCESS: Visual login for illiterate voter {voter.NationalId}");

                // Log successful visual login
                await _loginTrackingService.LogLoginAttemptAsync(voter.NationalId, "Voter", "Success", 
                    $"Visual login successful for illiterate voter: {voter.FirstName} {voter.LastName}", HttpContext);
                    
                // Log user activity
                await _loginTrackingService.LogUserActivityAsync(voter.NationalId, "Voter", "VisualLogin", 
                    $"Illiterate voter logged in via visual authentication", HttpContext);

                // IMPORTANT: Return proper redirect URL to castvote page
                return Json(new { 
                    success = true, 
                    message = "Login successful! Welcome to Ethiopian E-Voting System.",
                    redirectUrl = Url.Action("castvote", "Voter")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in visual login process");
                await _loginTrackingService.LogLoginAttemptAsync(model?.ExtractedNationalId ?? "Unknown", "Voter", "Failed", 
                    $"Visual login error: {ex.Message}", HttpContext);
                return Json(new { success = false, message = $"Login error: {ex.Message}" });
            }
        }

        // ========== SYMBOL-BASED VISUAL LOGIN ==========

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessIDScan([FromBody] VisualLoginModel model)
        {
            try
            {
                _logger.LogInformation("=== PROCESSING ID SCAN FOR VISUAL LOGIN ===");

                if (model == null || string.IsNullOrEmpty(model.FrontImageBase64))
                {
                    return Json(new { success = false, error = "Front ID image is required." });
                }

                // Process ID using your existing OCR service
                _logger.LogInformation("Calling Ethiopian OCR service for visual login...");
                var result = await _ocrService.ProcessEthiopianIDAsync(model.FrontImageBase64, model.BackImageBase64);
                
                _logger.LogInformation($"OCR Result - NationalId: {result?.NationalId}, Success: {result?.Success}");

                if (result != null && result.Success && !string.IsNullOrEmpty(result.NationalId))
                {
                    // Clean the National ID
                    string cleanNationalId = CleanNationalId(result.NationalId);
                    
                    // Check if this is a voter and is illiterate
                    var voter = await _context.Voters.FirstOrDefaultAsync(v => v.NationalId == cleanNationalId);
                    
                    if (voter != null)
                    {
                        if (voter.Literate == "Yes")
                        {
                            return Json(new { 
                                success = false, 
                                error = "This account is registered as literate. Please use the regular login with National ID and password." 
                            });
                        }

                        return Json(new { 
                            success = true, 
                            nationalId = cleanNationalId,
                            firstName = result.FirstName ?? "",
                            lastName = result.LastName ?? "",
                            message = "ID scanned successfully! Now select your symbol password."
                        });
                    }
                    else
                    {
                        return Json(new { 
                            success = false, 
                            error = "National ID not found in voter database." 
                        });
                    }
                }
                else
                {
                    string errorMessage = result?.Error ?? "Could not read National ID from ID card.";
                    return Json(new { 
                        success = false, 
                        error = $"{errorMessage} Please ensure the ID image is clear and try again." 
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing ID scan for visual login");
                return Json(new { 
                    success = false, 
                    error = "Failed to process ID card. Please try again." 
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyVisualLogin([FromBody] VisualLoginModel model)
        {
            try
            {
                // Check if we have image IDs or emojis
                List<int> selectedImageIds = new List<int>();
                
                if (model.SelectedImageIds != null && model.SelectedImageIds.Any())
                {
                    selectedImageIds = model.SelectedImageIds.ToList();
                }
                else if (model.SelectedImages != null && model.SelectedImages.Any())
                {
                    // Convert emojis to image IDs
                    foreach (var emoji in model.SelectedImages)
                    {
                        if (EmojiToImageId.ContainsKey(emoji))
                        {
                            selectedImageIds.Add(EmojiToImageId[emoji]);
                        }
                    }
                }

                if (selectedImageIds.Count != 4)
                {
                    return Json(new { success = false, message = "Please select exactly 4 images" });
                }

                string imagePassword = string.Join(",", selectedImageIds);

                _logger.LogInformation($"Symbol-based login attempt - NationalId: {model.NationalId}, Image IDs: {imagePassword}");

                // Log visual login attempt
                await _loginTrackingService.LogLoginAttemptAsync(model.NationalId, "Voter", "Attempt", 
                    "Symbol-based visual login attempt", HttpContext);

                // Check ONLY Voter table for matching National ID
                var voter = await _context.Voters.FirstOrDefaultAsync(v => v.NationalId == model.NationalId);
                
                if (voter != null)
                {
                    // Debug logging
                    _logger.LogInformation($"Found voter: {voter.NationalId}, VisualPIN: '{voter.VisualPIN}', Submitted IDs: '{imagePassword}'");
                    
                    if (voter.Literate == "Yes")
                    {
                        await _loginTrackingService.LogLoginAttemptAsync(model.NationalId, "Voter", "Failed", 
                            "Symbol login failed - Account is literate", HttpContext);
                        return Json(new { 
                            success = false, 
                            message = "This account is registered as literate. Please use the regular login with National ID and password." 
                        });
                    }

                    // Check if VisualPIN is empty
                    if (string.IsNullOrEmpty(voter.VisualPIN))
                    {
                        _logger.LogError($"VisualPIN is empty for voter: {model.NationalId}");
                        await _loginTrackingService.LogLoginAttemptAsync(model.NationalId, "Voter", "Failed", 
                            "Symbol login failed - No visual password set", HttpContext);
                        return Json(new { 
                            success = false, 
                            message = "No visual password set for this account. Please use forgot password." 
                        });
                    }

                    // Compare image ID sequences
                    string storedPassword = voter.VisualPIN.Trim();
                    string submittedPassword = imagePassword.Trim();
                    
                    _logger.LogInformation($"Comparing: Stored='{storedPassword}' vs Submitted='{submittedPassword}'");
                    
                    bool isMatch = storedPassword.Equals(submittedPassword, StringComparison.Ordinal);

                    if (!isMatch)
                    {
                        await _loginTrackingService.LogLoginAttemptAsync(model.NationalId, "Voter", "Failed", 
                            "Symbol login failed - Incorrect image password", HttpContext);
                        return Json(new { 
                            success = false, 
                            message = "Incorrect image password. Please try again."
                        });
                    }

                    // SUCCESS: Login the voter
                    HttpContext.Session.SetString("VoterNationalId", voter.NationalId);
                    HttpContext.Session.SetString("VoterName", $"{voter.FirstName} {voter.LastName}");
                    HttpContext.Session.SetString("UserRole", "Voter");
                    HttpContext.Session.SetString("LoginNationalId", voter.NationalId);
                    
                    _logger.LogInformation($"Visual login successful for illiterate voter: {voter.NationalId}");
                    
                    // Log successful visual login
                    await _loginTrackingService.LogLoginAttemptAsync(voter.NationalId, "Voter", "Success", 
                        $"Symbol-based visual login successful: {voter.FirstName} {voter.LastName}", HttpContext);
                        
                    // Log user activity
                    await _loginTrackingService.LogUserActivityAsync(voter.NationalId, "Voter", "SymbolLogin", 
                        $"Illiterate voter logged in via symbol authentication", HttpContext);
                    
                    return Json(new { 
                        success = true, 
                        redirectUrl = Url.Action("castvote", "Voter"),
                        message = "Welcome Voter! Redirecting to voting page..."
                    });
                }

                _logger.LogWarning($"Visual login failed - No voter found with NationalId: {model.NationalId}");
                
                // Log failed visual login
                await _loginTrackingService.LogLoginAttemptAsync(model.NationalId, "Voter", "Failed", 
                    "Symbol login failed - Invalid National ID or image password", HttpContext);
                    
                return Json(new { 
                    success = false, 
                    message = "Invalid National ID or image password. Please try again."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during visual login");
                
                // Log failed login due to error
                await _loginTrackingService.LogLoginAttemptAsync(model?.NationalId ?? "Unknown", "Voter", "Failed", 
                    $"Symbol login error: {ex.Message}", HttpContext);
                    
                return Json(new { 
                    success = false, 
                    message = "An error occurred during login. Please try again."
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SetupVisualPassword(string nationalId, string[] symbols)
        {
            try
            {
                if (string.IsNullOrEmpty(nationalId) || symbols == null || symbols.Length != 4)
                {
                    return Json(new { success = false, message = "National ID and 4 symbols are required" });
                }

                var voter = await _context.Voters.FirstOrDefaultAsync(v => v.NationalId == nationalId);

                if (voter == null)
                {
                    return Json(new { success = false, message = "Voter not found" });
                }

                if (voter.Literate == "Yes")
                {
                    return Json(new { 
                        success = false, 
                        message = "Literate voters cannot use visual passwords. Please use text password." 
                    });
                }

                // Convert emojis to image IDs
                List<int> imageIds = new List<int>();
                foreach (var symbol in symbols)
                {
                    if (EmojiToImageId.ContainsKey(symbol))
                    {
                        imageIds.Add(EmojiToImageId[symbol]);
                    }
                    else
                    {
                        return Json(new { success = false, message = $"Invalid symbol: {symbol}" });
                    }
                }

                // Set the visual password as image IDs
                voter.VisualPIN = string.Join(",", imageIds);
                voter.PrefersVisualLogin = true;
                voter.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                // Log visual password setup
                await _loginTrackingService.LogUserActivityAsync(nationalId, "Voter", "VisualPasswordSetup", 
                    "Visual password setup completed", HttpContext);

                return Json(new { 
                    success = true, 
                    message = "Visual password set successfully! You can now use symbol login."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting up visual password");
                return Json(new { success = false, message = "Error setting up visual password" });
            }
        }

        // ========== FORGOT PASSWORD FUNCTIONALITY ==========
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string nationalId, string firstName, string middleName, string lastName, string phoneNumber, string region, string nationality)
        {
            try
            {
                _logger.LogInformation($"=== FORGOT PASSWORD START ===");

                if (string.IsNullOrEmpty(nationalId) || string.IsNullOrEmpty(firstName) || 
                    string.IsNullOrEmpty(lastName) || string.IsNullOrEmpty(phoneNumber))
                {
                    TempData["ErrorMessage"] = "Please fill all required fields (National ID, First Name, Last Name, Phone Number).";
                    return View();
                }

                nationalId = nationalId.Trim();
                firstName = firstName.Trim();
                middleName = middleName?.Trim() ?? "";
                lastName = lastName.Trim();
                phoneNumber = phoneNumber.Trim();
                region = region?.Trim() ?? "";
                nationality = nationality?.Trim() ?? "Ethiopian";

                if (!IsValidEthiopianNationalId(nationalId))
                {
                    TempData["ErrorMessage"] = "Please enter a valid Ethiopian National ID (15-17 digits).";
                    return View();
                }

                _logger.LogInformation($"Processing forgot password for: {nationalId}");

                // Log password reset attempt
                await _loginTrackingService.LogUserActivityAsync(nationalId, "Unknown", "PasswordResetRequest", 
                    "Password reset requested", HttpContext);

                var result = await HandlePasswordReset(nationalId, firstName, middleName, lastName, phoneNumber, region, nationality);
                
                if (result.success)
                {
                    // Check if this is an illiterate voter
                    var voter = await _context.Voters.FirstOrDefaultAsync(v => v.NationalId == nationalId);
                    if (voter != null && voter.Literate == "No")
                    {
                        // For illiterate voters, show special message
                        TempData["IlliterateVoter"] = "true";
                        TempData["RetrievedPassword"] = result.password;
                        TempData["RetrievedNationalId"] = nationalId;
                        TempData["ExpiryTime"] = result.expiryTime?.ToString("yyyy-MM-dd HH:mm:ss");
                        TempData["SuccessMessage"] = $"✅ Temporary password generated for illiterate voter!<br><br>" +
                                                    $"<strong>Important:</strong> This account requires visual login. After logging in with this temporary password, please use the 'Visual Login for Illiterate Voters' option to set your image password.<br><br>" +
                                                    $"<strong>Temporary Password:</strong> {result.password}<br>" +
                                                    $"<strong>Password Expires:</strong> {result.expiryTime?.ToString("yyyy-MM-dd HH:mm:ss")}";
                        
                        _logger.LogInformation($"SUCCESS: Temporary password generated for illiterate voter {nationalId}");
                        
                        // Log successful password reset for illiterate voter
                        await _loginTrackingService.LogUserActivityAsync(nationalId, result.userType ?? "Voter", "PasswordResetSuccess", 
                            "Temporary password generated for illiterate voter", HttpContext);
                    }
                    else
                    {
                        TempData["RetrievedPassword"] = result.password;
                        TempData["RetrievedNationalId"] = nationalId;
                        TempData["ExpiryTime"] = result.expiryTime?.ToString("yyyy-MM-dd HH:mm:ss");
                        TempData["SuccessMessage"] = result.message;
                        _logger.LogInformation($"SUCCESS: Temporary password generated for {nationalId}");
                        
                        // Log successful password reset
                        await _loginTrackingService.LogUserActivityAsync(nationalId, result.userType ?? "Unknown", "PasswordResetSuccess", 
                            "Temporary password generated successfully", HttpContext);
                    }
                }
                else
                {
                    TempData["ErrorMessage"] = result.message;
                    _logger.LogWarning($"FAILED: {result.message} for {nationalId}");
                    
                    // Log failed password reset
                    await _loginTrackingService.LogUserActivityAsync(nationalId, "Unknown", "PasswordResetFailed", 
                        $"Password reset failed: {result.message}", HttpContext);
                }

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UNHANDLED ERROR in ForgotPassword");
                TempData["ErrorMessage"] = "An unexpected error occurred. Please try again.";
                
                // Log password reset error
                await _loginTrackingService.LogUserActivityAsync(nationalId ?? "Unknown", "Unknown", "PasswordResetError", 
                    $"Password reset error: {ex.Message}", HttpContext);
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePassword(string nationalId, string currentPassword, string newPassword, string confirmPassword)
        {
            try
            {
                _logger.LogInformation($"=== PASSWORD UPDATE START ===");
                _logger.LogInformation($"Password update attempt for: {nationalId}");

                // Log password update attempt
                await _loginTrackingService.LogUserActivityAsync(nationalId, "Unknown", "PasswordUpdateAttempt", 
                    "Password update attempted", HttpContext);

                // Validate inputs
                if (string.IsNullOrEmpty(nationalId) || string.IsNullOrEmpty(currentPassword) || 
                    string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmPassword))
                {
                    _logger.LogWarning($"Missing fields - NationalId: {nationalId}, CurrentPassword: {!string.IsNullOrEmpty(currentPassword)}, NewPassword: {!string.IsNullOrEmpty(newPassword)}, ConfirmPassword: {!string.IsNullOrEmpty(confirmPassword)}");
                    
                    // Store the retrieved password data so the form stays populated
                    if (TempData["RetrievedPassword"] != null)
                    {
                        TempData["RetrievedPassword"] = TempData["RetrievedPassword"];
                        TempData["RetrievedNationalId"] = TempData["RetrievedNationalId"];
                        TempData["ExpiryTime"] = TempData["ExpiryTime"];
                        TempData["SuccessMessage"] = TempData["SuccessMessage"];
                    }
                    
                    TempData["UpdatePasswordError"] = "All fields are required.";
                    return RedirectToAction("ForgotPassword");
                }

                if (newPassword != confirmPassword)
                {
                    // Store the retrieved password data
                    if (TempData["RetrievedPassword"] != null)
                    {
                        TempData["RetrievedPassword"] = TempData["RetrievedPassword"];
                        TempData["RetrievedNationalId"] = TempData["RetrievedNationalId"];
                        TempData["ExpiryTime"] = TempData["ExpiryTime"];
                        TempData["SuccessMessage"] = TempData["SuccessMessage"];
                    }
                    
                    TempData["UpdatePasswordError"] = "New password and confirm password do not match.";
                    return RedirectToAction("ForgotPassword");
                }

                // Validate new password meets criteria
                if (!IsPasswordValid(newPassword))
                {
                    // Store the retrieved password data
                    if (TempData["RetrievedPassword"] != null)
                    {
                        TempData["RetrievedPassword"] = TempData["RetrievedPassword"];
                        TempData["RetrievedNationalId"] = TempData["RetrievedNationalId"];
                        TempData["ExpiryTime"] = TempData["ExpiryTime"];
                        TempData["SuccessMessage"] = TempData["SuccessMessage"];
                    }
                    
                    TempData["UpdatePasswordError"] = "New password must:<br>" +
                                                     "• Be at least 6 characters long<br>" +
                                                     "• Contain at least one letter (A-Z or a-z)<br>" +
                                                     "• Contain at least one number (0-9)<br>" +
                                                     "• Contain at least one special character (!@#$%^&* etc.)";
                    return RedirectToAction("ForgotPassword");
                }

                _logger.LogInformation($"Processing password update for: {nationalId}");

                // ENHANCED: Check if current password is valid (either temporary password or actual password)
                bool isValidCurrentPassword = false;
                string userType = "";

                // First check if it's a valid temporary password
                if (IsTempPasswordValid(nationalId, currentPassword))
                {
                    isValidCurrentPassword = true;
                    if (_tempPasswords.TryGetValue(nationalId, out var tempRecord))
                    {
                        userType = tempRecord.UserType;
                        // Mark temporary password as used since user is changing it
                        tempRecord.IsUsed = true;
                        _logger.LogInformation($"Temporary password marked as used for: {nationalId}");
                    }
                }
                else
                {
                    // Check against actual passwords in database
                    var voter = await _context.Voters.FirstOrDefaultAsync(v => v.NationalId == nationalId);
                    if (voter != null)
                    {
                        string hashedCurrentPassword = HashPasswordBase64(currentPassword);
                        if (voter.Password == hashedCurrentPassword)
                        {
                            isValidCurrentPassword = true;
                            userType = "Voter";
                        }
                    }

                    if (!isValidCurrentPassword)
                    {
                        var admin = await _context.Admins.FirstOrDefaultAsync(a => a.NationalId == nationalId);
                        if (admin != null && admin.Password == currentPassword)
                        {
                            isValidCurrentPassword = true;
                            userType = "Admin";
                        }
                    }

                    if (!isValidCurrentPassword)
                    {
                        var manager = await _context.Managers.FirstOrDefaultAsync(m => m.NationalId == nationalId);
                        if (manager != null)
                        {
                            string hashedCurrentPassword = HashPasswordBase64(currentPassword);
                            if (manager.Password == hashedCurrentPassword)
                            {
                                isValidCurrentPassword = true;
                                userType = "Manager";
                            }
                        }
                    }

                    if (!isValidCurrentPassword)
                    {
                        var supervisor = await _context.Supervisors.FirstOrDefaultAsync(s => s.NationalId == nationalId);
                        if (supervisor != null)
                        {
                            string hashedCurrentPassword = HashPasswordBase64(currentPassword);
                            if (supervisor.Password == hashedCurrentPassword)
                            {
                                isValidCurrentPassword = true;
                                userType = "Supervisor";
                            }
                        }
                    }
                }

                if (!isValidCurrentPassword)
                {
                    // Store the retrieved password data
                    if (TempData["RetrievedPassword"] != null)
                    {
                        TempData["RetrievedPassword"] = TempData["RetrievedPassword"];
                        TempData["RetrievedNationalId"] = TempData["RetrievedNationalId"];
                        TempData["ExpiryTime"] = TempData["ExpiryTime"];
                        TempData["SuccessMessage"] = TempData["SuccessMessage"];
                    }
                    
                    TempData["UpdatePasswordError"] = "Current password is incorrect.";
                    return RedirectToAction("ForgotPassword");
                }

                // Update password based on user type
                bool updateSuccess = false;

                switch (userType)
                {
                    case "Voter":
                        var voter = await _context.Voters.FirstOrDefaultAsync(v => v.NationalId == nationalId);
                        if (voter != null)
                        {
                            // Check if voter is illiterate
                            if (voter.Literate == "No")
                            {
                                // For illiterate voters, don't allow password change through this method
                                TempData["UpdatePasswordError"] = "Illiterate voters cannot change password here. Please use the visual login system.";
                                return RedirectToAction("ForgotPassword");
                            }
                            
                            voter.Password = HashPasswordBase64(newPassword);
                            voter.UpdatedAt = DateTime.Now;
                            await _context.SaveChangesAsync();
                            updateSuccess = true;
                            _logger.LogInformation($"SUCCESS: Voter password updated for {nationalId}");
                        }
                        break;

                    case "Admin":
                        var admin = await _context.Admins.FirstOrDefaultAsync(a => a.NationalId == nationalId);
                        if (admin != null)
                        {
                            admin.Password = newPassword;
                            await _context.SaveChangesAsync();
                            updateSuccess = true;
                            _logger.LogInformation($"SUCCESS: Admin password updated for {nationalId}");
                        }
                        break;

                    case "Manager":
                        var manager = await _context.Managers.FirstOrDefaultAsync(m => m.NationalId == nationalId);
                        if (manager != null)
                        {
                            manager.Password = HashPasswordBase64(newPassword);
                            manager.UpdatedAt = DateTime.Now;
                            await _context.SaveChangesAsync();
                            updateSuccess = true;
                            _logger.LogInformation($"SUCCESS: Manager password updated for {nationalId}");
                        }
                        break;

                    case "Supervisor":
                        var supervisor = await _context.Supervisors.FirstOrDefaultAsync(s => s.NationalId == nationalId);
                        if (supervisor != null)
                        {
                            supervisor.Password = HashPasswordBase64(newPassword);
                            supervisor.UpdatedAt = DateTime.Now;
                            await _context.SaveChangesAsync();
                            updateSuccess = true;
                            _logger.LogInformation($"SUCCESS: Supervisor password updated for {nationalId}");
                        }
                        break;
                }

                if (updateSuccess)
                {
                    // CRITICAL FIX: Immediately remove temporary password from memory
                    if (_tempPasswords.ContainsKey(nationalId))
                    {
                        _tempPasswords.Remove(nationalId);
                        _logger.LogInformation($"TEMPORARY PASSWORD IMMEDIATELY REMOVED for {nationalId} after successful password change");
                    }

                    // Also clear any session data related to temporary passwords
                    HttpContext.Session.Remove($"TempPwd_{nationalId}");
                    
                    TempData["UpdatePasswordSuccess"] = "✅ Your password has been successfully updated! You can now login with your new password.";
                    _logger.LogInformation($"SUCCESS: Password updated and temporary password cleared for {nationalId}");
                    
                    // Log successful password update
                    await _loginTrackingService.LogUserActivityAsync(nationalId, userType, "PasswordUpdateSuccess", 
                        "Password updated successfully", HttpContext);
                    
                    // Clear the temporary password display
                    TempData["RetrievedPassword"] = null;
                    TempData["RetrievedNationalId"] = null;
                    TempData["ExpiryTime"] = null;
                    TempData["SuccessMessage"] = null;
                }
                else
                {
                    // Store the retrieved password data
                    if (TempData["RetrievedPassword"] != null)
                    {
                        TempData["RetrievedPassword"] = TempData["RetrievedPassword"];
                        TempData["RetrievedNationalId"] = TempData["RetrievedNationalId"];
                        TempData["ExpiryTime"] = TempData["ExpiryTime"];
                        TempData["SuccessMessage"] = TempData["SuccessMessage"];
                    }
                    
                    TempData["UpdatePasswordError"] = "❌ Failed to update password. Please try again.";
                    
                    // Log failed password update
                    await _loginTrackingService.LogUserActivityAsync(nationalId, userType, "PasswordUpdateFailed", 
                        "Password update failed", HttpContext);
                }

                return RedirectToAction("ForgotPassword");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating password");
                
                // Store the retrieved password data
                if (TempData["RetrievedPassword"] != null)
                {
                    TempData["RetrievedPassword"] = TempData["RetrievedPassword"];
                    TempData["RetrievedNationalId"] = TempData["RetrievedNationalId"];
                    TempData["ExpiryTime"] = TempData["ExpiryTime"];
                    TempData["SuccessMessage"] = TempData["SuccessMessage"];
                }
                
                TempData["UpdatePasswordError"] = "❌ An error occurred while updating your password. Please try again.";
                
                // Log password update error
                await _loginTrackingService.LogUserActivityAsync(nationalId, "Unknown", "PasswordUpdateError", 
                    $"Password update error: {ex.Message}", HttpContext);
                return RedirectToAction("ForgotPassword");
            }
            finally
            {
                _logger.LogInformation("=== PASSWORD UPDATE END ===");
            }
        }

        private async Task<(bool success, string password, DateTime? expiryTime, string message, string userType)> HandlePasswordReset(string nationalId, string firstName, string middleName, string lastName, string phoneNumber, string region, string nationality)
        {
            try
            {
                _logger.LogInformation($"Searching for user: {nationalId}");

                // Clean expired temporary passwords first
                CleanExpiredTempPasswords();

                // 1. Check Voters table
                var voter = await _context.Voters
                    .FirstOrDefaultAsync(v => v.NationalId == nationalId && 
                                     v.FirstName.ToLower() == firstName.ToLower() && 
                                     v.LastName.ToLower() == lastName.ToLower() &&
                                     v.PhoneNumber == phoneNumber);
                
                if (voter != null)
                {
                    _logger.LogInformation($"VOTER FOUND: {voter.NationalId}, Literate: {voter.Literate}");
                    
                    // Check if voter is illiterate
                    if (voter.Literate == "No")
                    {
                        // Generate secure temporary password meeting criteria
                        string tempPassword = GenerateSecureTemporaryPassword();
                        _logger.LogInformation($"Generated secure temp password for illiterate voter: {tempPassword}");
                        
                        DateTime expiryTime = DateTime.Now.AddMinutes(10);
                        string hashedTempPassword = HashPasswordBase64(tempPassword);
                        
                        _tempPasswords[nationalId] = new TempPasswordRecord
                        {
                            NationalId = nationalId,
                            Password = tempPassword,
                            HashedPassword = hashedTempPassword,
                            ExpiryTime = expiryTime,
                            UserType = "Voter",
                            IsUsed = false
                        };
                        
                        voter.Password = hashedTempPassword;
                        voter.UpdatedAt = DateTime.Now;
                        
                        await _context.SaveChangesAsync();
                        
                        string message = $"Temporary password generated successfully! This password will expire at {expiryTime:HH:mm:ss}.";
                        return (true, tempPassword, expiryTime, message, "Voter");
                    }
                    else
                    {
                        // For literate voters, generate secure temporary password
                        string tempPassword = GenerateSecureTemporaryPassword();
                        _logger.LogInformation($"Generated secure temp password for literate voter: {tempPassword}");
                        
                        DateTime expiryTime = DateTime.Now.AddMinutes(10);
                        string hashedTempPassword = HashPasswordBase64(tempPassword);
                        
                        _tempPasswords[nationalId] = new TempPasswordRecord
                        {
                            NationalId = nationalId,
                            Password = tempPassword,
                            HashedPassword = hashedTempPassword,
                            ExpiryTime = expiryTime,
                            UserType = "Voter",
                            IsUsed = false
                        };
                        
                        voter.Password = hashedTempPassword;
                        voter.UpdatedAt = DateTime.Now;
                        
                        await _context.SaveChangesAsync();
                        
                        string message = $"New temporary password generated successfully! This password will expire at {expiryTime:HH:mm:ss}.";
                        return (true, tempPassword, expiryTime, message, "Voter");
                    }
                }

                // 2. Check Admins table
                var admin = await _context.Admins
                    .FirstOrDefaultAsync(a => a.NationalId == nationalId && 
                                     a.FirstName.ToLower() == firstName.ToLower() && 
                                     a.LastName.ToLower() == lastName.ToLower());
                
                if (admin != null)
                {
                    _logger.LogInformation($"ADMIN FOUND: {admin.NationalId}");
                    
                    string tempPassword = GenerateSecureTemporaryPassword();
                    DateTime expiryTime = DateTime.Now.AddMinutes(10);
                    
                    _tempPasswords[nationalId] = new TempPasswordRecord
                    {
                        NationalId = nationalId,
                        Password = tempPassword,
                        HashedPassword = tempPassword,
                        ExpiryTime = expiryTime,
                        UserType = "Admin",
                        IsUsed = false
                    };
                    
                    admin.Password = tempPassword;
                    await _context.SaveChangesAsync();
                    
                    string message = $"Temporary password generated successfully! This password will expire at {expiryTime:HH:mm:ss}.";
                    return (true, tempPassword, expiryTime, message, "Admin");
                }

                // 3. Check Managers table
                var manager = await _context.Managers
                    .FirstOrDefaultAsync(m => m.NationalId == nationalId && 
                                     m.FirstName.ToLower() == firstName.ToLower() && 
                                     m.LastName.ToLower() == lastName.ToLower() &&
                                     m.PhoneNumber == phoneNumber);
                
                if (manager != null)
                {
                    _logger.LogInformation($"MANAGER FOUND: {manager.NationalId}");
                    
                    string tempPassword = GenerateSecureTemporaryPassword();
                    DateTime expiryTime = DateTime.Now.AddMinutes(10);
                    string hashedTempPassword = HashPasswordBase64(tempPassword);
                    
                    _tempPasswords[nationalId] = new TempPasswordRecord
                    {
                        NationalId = nationalId,
                        Password = tempPassword,
                        HashedPassword = hashedTempPassword,
                        ExpiryTime = expiryTime,
                        UserType = "Manager",
                        IsUsed = false
                    };
                    
                    manager.Password = hashedTempPassword;
                    manager.UpdatedAt = DateTime.Now;
                    
                    await _context.SaveChangesAsync();
                    
                    string message = $"New temporary password generated successfully! This password will expire at {expiryTime:HH:mm:ss}.";
                    return (true, tempPassword, expiryTime, message, "Manager");
                }

                // 4. Check Supervisors table
                var supervisor = await _context.Supervisors
                    .FirstOrDefaultAsync(s => s.NationalId == nationalId && 
                                     s.FirstName.ToLower() == firstName.ToLower() && 
                                     s.LastName.ToLower() == lastName.ToLower() &&
                                     s.PhoneNumber == phoneNumber);
                
                if (supervisor != null)
                {
                    _logger.LogInformation($"SUPERVISOR FOUND: {supervisor.NationalId}");
                    
                    string tempPassword = GenerateSecureTemporaryPassword();
                    DateTime expiryTime = DateTime.Now.AddMinutes(10);
                    string hashedTempPassword = HashPasswordBase64(tempPassword);
                    
                    _tempPasswords[nationalId] = new TempPasswordRecord
                    {
                        NationalId = nationalId,
                        Password = tempPassword,
                        HashedPassword = hashedTempPassword,
                        ExpiryTime = expiryTime,
                        UserType = "Supervisor",
                        IsUsed = false
                    };
                    
                    supervisor.Password = hashedTempPassword;
                    supervisor.UpdatedAt = DateTime.Now;
                    
                    await _context.SaveChangesAsync();
                    
                    string message = $"New temporary password generated successfully! This password will expire at {expiryTime:HH:mm:ss}.";
                    return (true, tempPassword, expiryTime, message, "Supervisor");
                }

                _logger.LogWarning($"NO USER FOUND: {nationalId}");
                return (false, null, null, "❌ No matching account found. Please check your information and try again.", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in HandlePasswordReset");
                return (false, null, null, "❌ An error occurred while processing your request. Please try again.", null);
            }
        }

        // ENHANCED: Improved temporary password validation with strict expiry
        private bool IsTempPasswordValid(string nationalId, string password)
        {
            try
            {
                CleanExpiredTempPasswords();
                
                if (_tempPasswords.TryGetValue(nationalId, out var tempRecord))
                {
                    if (tempRecord.Password == password && DateTime.Now <= tempRecord.ExpiryTime && !tempRecord.IsUsed)
                    {
                        _logger.LogInformation($"Valid temporary password used for: {nationalId}");
                        return true;
                    }
                    else
                    {
                        _tempPasswords.Remove(nationalId);
                        if (DateTime.Now > tempRecord.ExpiryTime)
                        {
                            _logger.LogInformation($"Temporary password EXPIRED for: {nationalId}");
                        }
                        else if (tempRecord.IsUsed)
                        {
                            _logger.LogInformation($"Temporary password ALREADY USED for: {nationalId}");
                        }
                        else
                        {
                            _logger.LogInformation($"Temporary password INVALID for: {nationalId}");
                        }
                        return false;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking temporary password validity");
                return false;
            }
        }

        // ENHANCED: Clean expired temporary passwords
        private void CleanExpiredTempPasswords()
        {
            try
            {
                var expiredKeys = _tempPasswords
                    .Where(kvp => DateTime.Now > kvp.Value.ExpiryTime || kvp.Value.IsUsed)
                    .Select(kvp => kvp.Key)
                    .ToList();
                    
                foreach (var key in expiredKeys)
                {
                    _tempPasswords.Remove(key);
                    _logger.LogInformation($"Cleaned expired/used temporary password for: {key}");
                }
                
                if (expiredKeys.Count > 0)
                {
                    _logger.LogInformation($"Cleaned up {expiredKeys.Count} expired/used temporary passwords");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning expired temporary passwords");
            }
        }

        // Generate a secure temporary password meeting all criteria
        private string GenerateSecureTemporaryPassword()
        {
            const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lowercase = "abcdefghijklmnopqrstuvwxyz";
            const string numbers = "0123456789";
            const string specials = "!@#$%^&*";
            
            Random random = new Random();
            StringBuilder password = new StringBuilder();
            
            // Ensure at least one of each required character type
            password.Append(uppercase[random.Next(uppercase.Length)]);
            password.Append(lowercase[random.Next(lowercase.Length)]);
            password.Append(numbers[random.Next(numbers.Length)]);
            password.Append(specials[random.Next(specials.Length)]);
            
            // Add remaining characters to make minimum 6 characters
            string allChars = uppercase + lowercase + numbers + specials;
            for (int i = 4; i < 8; i++) // Generate 8-character password
            {
                password.Append(allChars[random.Next(allChars.Length)]);
            }
            
            // Shuffle the password
            string shuffledPassword = new string(password.ToString().OrderBy(c => random.Next()).ToArray());
            
            _logger.LogInformation($"Generated secure password meeting criteria: {shuffledPassword}");
            return shuffledPassword;
        }

        // Generate a simple temporary password (backward compatibility)
        private string GenerateTemporaryPassword()
        {
            return GenerateSecureTemporaryPassword(); // Use the secure version
        }

        // Validate password meets criteria
        private bool IsPasswordValid(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 6)
                return false;
            
            // Check for at least one letter
            if (!Regex.IsMatch(password, @"[A-Za-z]"))
                return false;
            
            // Check for at least one number
            if (!Regex.IsMatch(password, @"[0-9]"))
                return false;
            
            // Check for at least one special character
            if (!Regex.IsMatch(password, @"[!@#$%^&*]"))
                return false;
            
            return true;
        }

       public IActionResult Logout()
{
    var userRole = HttpContext.Session.GetString("UserRole");
    var nationalId = HttpContext.Session.GetString($"{userRole}NationalId");
    var userName = HttpContext.Session.GetString("VoterName") ?? 
                  HttpContext.Session.GetString("ManagerName") ?? 
                  HttpContext.Session.GetString("SupervisorName") ??
                  HttpContext.Session.GetString("AdminName");
    
    _logger.LogInformation($"User {userName} ({userRole}) logged out");
    
    if (!string.IsNullOrEmpty(nationalId))
    {
        _loginTrackingService.LogUserActivityAsync(nationalId, userRole, "Logout", 
            $"{userRole} logged out: {userName}", HttpContext).Wait();
    }

    // ============ ADD CACHE CONTROL HEADERS FOR LOGOUT ============
    Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
    Response.Headers["Pragma"] = "no-cache";
    Response.Headers["Expires"] = "0";
    // ==============================================================
    
    // Clear all session data
    HttpContext.Session.Clear();
    
    // Clear all cookies
    foreach (var cookie in Request.Cookies.Keys)
    {
        Response.Cookies.Delete(cookie);
    }
    
    TempData["SuccessMessage"] = "You have been logged out successfully.";
    return RedirectToAction("Index");
}

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            _logger.LogError($"Error occurred. Request ID: {requestId}");
            return View(new ErrorViewModel { RequestId = requestId });
        }

        // ========== HELPER METHODS ==========

        private async Task<(bool exists, string message)> CheckNationalIdInAllTables(string nationalId)
        {
            try
            {
                _logger.LogInformation($"Checking National ID: {nationalId} in all tables");

                // Check Voters table
                var voterExists = await _context.Voters
                    .AsNoTracking()
                    .AnyAsync(v => v.NationalId == nationalId);
                
                if (voterExists)
                {
                    _logger.LogInformation($"National ID {nationalId} found in Voters table");
                    
                    var voter = await _context.Voters
                        .AsNoTracking()
                        .Where(v => v.NationalId == nationalId)
                        .Select(v => new { v.FirstName, v.LastName })
                        .FirstOrDefaultAsync();
                        
                    string voterName = voter != null ? $"{voter.FirstName} {voter.LastName}" : "a voter";
                    return (true, $"❌ This National ID is already registered as {voterName}.");
                }

                // Check Admins table
                var adminExists = await _context.Admins
                    .AsNoTracking()
                    .AnyAsync(a => a.NationalId == nationalId);
                
                if (adminExists)
                {
                    _logger.LogInformation($"National ID {nationalId} found in Admins table");
                    return (true, $"❌ This National ID is already registered as Admin.");
                }

                // Check Managers table
                var managerExists = await _context.Managers
                    .AsNoTracking()
                    .AnyAsync(m => m.NationalId == nationalId);
                
                if (managerExists)
                {
                    _logger.LogInformation($"National ID {nationalId} found in Managers table");
                    return (true, $"❌ This National ID is already registered as Manager.");
                }

                // Check Supervisors table
                var supervisorExists = await _context.Supervisors
                    .AsNoTracking()
                    .AnyAsync(s => s.NationalId == nationalId);
                
                if (supervisorExists)
                {
                    _logger.LogInformation($"National ID {nationalId} found in Supervisors table");
                    return (true, $"❌ This National ID is already registered as Supervisor.");
                }

                // Check Candidates table
                var candidateExists = await _context.Candidates
                    .AsNoTracking()
                    .AnyAsync(c => c.NationalId == nationalId);
                
                if (candidateExists)
                {
                    _logger.LogInformation($"National ID {nationalId} found in Candidates table");
                    return (true, $"❌ This National ID is already registered as Candidate.");
                }

                _logger.LogInformation($"National ID {nationalId} is available for registration");
                return (false, "✅ This National ID is available for registration.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking National ID in all tables");
                return (true, "❌ Error checking National ID availability. Please try again.");
            }
        }

        private string HashPasswordBase64(string password)
        {
            if (string.IsNullOrEmpty(password))
                return password;

            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        private bool IsValidEthiopianNationalId(string nationalId)
        {
            if (string.IsNullOrEmpty(nationalId))
                return false;

            string cleanId = Regex.Replace(nationalId, @"[^\d]", "");
            
            return cleanId.Length >= 15 && cleanId.Length <= 17 && 
                   cleanId.All(char.IsDigit);
        }

        private bool IsValidEthiopianPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrEmpty(phoneNumber))
                return false;

            string cleaned = Regex.Replace(phoneNumber, @"[^\d+]", "");
            
            return cleaned.StartsWith("+251") && cleaned.Length == 13 ||
                   cleaned.StartsWith("251") && cleaned.Length == 12 ||
                   cleaned.StartsWith("09") && cleaned.Length == 10 ||
                   cleaned.StartsWith("9") && cleaned.Length == 9;
        }

        // Add to class variables
        private static readonly Dictionary<string, OneTimeImagePassword> _oneTimeImagePasswords = new Dictionary<string, OneTimeImagePassword>();

        // Add this class inside HomeController class
        public class OneTimeImagePassword
        {
            public string NationalId { get; set; }
            public string[] ImageIds { get; set; } // 4 random images (as emojis for display)
            public int[] ImageIdNumbers { get; set; } // 4 random image IDs (for comparison)
            public DateTime ExpiryTime { get; set; }
            public bool IsUsed { get; set; }
        }

        // Add this method to generate one-time image password
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateOneTimeImagePassword([FromBody] OneTimePasswordRequest request)
        {
            try
            {
                string nationalId = request.NationalId;
                nationalId = CleanNationalId(nationalId);
                CleanExpiredOneTimePasswords();

                var voter = await _context.Voters.FirstOrDefaultAsync(v => v.NationalId == nationalId);
                if (voter == null)
                {
                    return Json(new { success = false, message = "National ID not found." });
                }

                if (voter.Literate == "Yes")
                {
                    return Json(new { success = false, message = "This account is literate. Please use regular password reset." });
                }

                // Generate 4 random image IDs from available 10 images
                Random random = new Random();
                var allImageIds = Enumerable.Range(1, 10).ToList(); // IDs 1-10
                var randomImageIds = allImageIds.OrderBy(x => random.Next()).Take(4).ToArray();
                
                // Convert to emojis for display
                var randomEmojis = randomImageIds.Select(id => ImageIdToEmoji.ContainsKey(id) ? ImageIdToEmoji[id] : "❓").ToArray();

                // Store one-time password
                _oneTimeImagePasswords[nationalId] = new OneTimeImagePassword
                {
                    NationalId = nationalId,
                    ImageIds = randomEmojis,
                    ImageIdNumbers = randomImageIds,
                    ExpiryTime = DateTime.Now.AddMinutes(10),
                    IsUsed = false
                };

                // Log activity
                await _loginTrackingService.LogUserActivityAsync(nationalId, "Voter", "OneTimeImagePasswordGenerated", 
                    "One-time image password generated for visual login", HttpContext);

                return Json(new { 
                    success = true, 
                    images = randomEmojis,
                    imageIds = randomImageIds, // Also send IDs for frontend if needed
                    message = "Use these 4 images to login. You will then set a new password."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating one-time image password");
                return Json(new { success = false, message = "Error generating one-time password." });
            }
        }

        // Add this method to verify one-time image password
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOneTimeImagePassword([FromBody] VerifyOneTimePasswordRequest request)
        {
            try
            {
                string nationalId = request.NationalId;
                int[] selectedImageIds = request.SelectedImageIds;
                string[] selectedImages = request.SelectedImages;
                nationalId = CleanNationalId(nationalId);
                CleanExpiredOneTimePasswords();

                if (string.IsNullOrEmpty(nationalId))
                {
                    return Json(new { success = false, message = "National ID is required." });
                }

                // Check if we have image IDs or emojis
                List<int> finalSelectedImageIds = new List<int>();
                
                if (selectedImageIds != null && selectedImageIds.Length == 4)
                {
                    finalSelectedImageIds = selectedImageIds.ToList();
                }
                else if (selectedImages != null && selectedImages.Length == 4)
                {
                    // Convert emojis to image IDs
                    foreach (var emoji in selectedImages)
                    {
                        if (EmojiToImageId.ContainsKey(emoji))
                        {
                            finalSelectedImageIds.Add(EmojiToImageId[emoji]);
                        }
                        else
                        {
                            return Json(new { success = false, message = "Invalid image selected." });
                        }
                    }
                }
                else
                {
                    return Json(new { success = false, message = "Please select exactly 4 images." });
                }

                if (!_oneTimeImagePasswords.TryGetValue(nationalId, out var oneTimePassword))
                {
                    return Json(new { success = false, message = "One-time password expired or not found. Please generate a new one." });
                }

                if (oneTimePassword.IsUsed)
                {
                    return Json(new { success = false, message = "This one-time password has already been used." });
                }

                // Check if selected image IDs match
                var selectedSet = new HashSet<int>(finalSelectedImageIds);
                var expectedSet = new HashSet<int>(oneTimePassword.ImageIdNumbers);

                if (selectedSet.SetEquals(expectedSet))
                {
                    oneTimePassword.IsUsed = true;
                    
                    // Set session to indicate we're in change password mode
                    HttpContext.Session.SetString("ChangePasswordMode", nationalId);
                    
                    // Log activity
                    await _loginTrackingService.LogLoginAttemptAsync(nationalId, "Voter", "Success", 
                        "Logged in with one-time image password", HttpContext);

                    return Json(new { 
                        success = true, 
                        message = "Success! Now set your new 4-image password.",
                        requiresNewPassword = true
                    });
                }
                else
                {
                    await _loginTrackingService.LogLoginAttemptAsync(nationalId, "Voter", "Failed", 
                        "One-time image password incorrect", HttpContext);
                    return Json(new { success = false, message = "Incorrect images. Please try again." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying one-time image password");
                return Json(new { success = false, message = "Verification failed. Please try again." });
            }
        }

        // Add this method to update image password
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateImagePassword([FromBody] UpdateImagePasswordRequest request)
        {
            try
            {
                string nationalId = request.NationalId;
                int[] newImageIds = request.NewImageIds;
                string[] newImages = request.NewImages;
                
                if (string.IsNullOrEmpty(nationalId))
                {
                    return Json(new { success = false, message = "National ID is required." });
                }

                // Check if we have image IDs or emojis
                List<int> finalNewImageIds = new List<int>();
                
                if (newImageIds != null && newImageIds.Length == 4)
                {
                    finalNewImageIds = newImageIds.ToList();
                    
                    // Validate image IDs
                    foreach (var id in finalNewImageIds)
                    {
                        if (id < 1 || id > 10)
                        {
                            return Json(new { success = false, message = $"Invalid image ID: {id}" });
                        }
                    }
                }
                else if (newImages != null && newImages.Length == 4)
                {
                    // Convert emojis to image IDs
                    foreach (var emoji in newImages)
                    {
                        if (EmojiToImageId.ContainsKey(emoji))
                        {
                            finalNewImageIds.Add(EmojiToImageId[emoji]);
                        }
                        else
                        {
                            return Json(new { success = false, message = $"Invalid image: {emoji}" });
                        }
                    }
                }
                else
                {
                    return Json(new { success = false, message = "Please select exactly 4 images for your new password." });
                }

                var voter = await _context.Voters.FirstOrDefaultAsync(v => v.NationalId == nationalId);
                if (voter == null)
                {
                    return Json(new { success = false, message = "Voter not found." });
                }

                if (voter.Literate == "Yes")
                {
                    return Json(new { success = false, message = "Literate voters cannot use image passwords." });
                }

                // Update the visual password with image IDs
                voter.VisualPIN = string.Join(",", finalNewImageIds);
                voter.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                // Clear one-time password
                _oneTimeImagePasswords.Remove(nationalId);
                HttpContext.Session.Remove("ChangePasswordMode");

                // Log successful update
                await _loginTrackingService.LogUserActivityAsync(nationalId, "Voter", "ImagePasswordUpdated", 
                    "Image password updated successfully", HttpContext);

                // Now log the user in
                HttpContext.Session.SetString("VoterNationalId", voter.NationalId);
                HttpContext.Session.SetString("VoterName", $"{voter.FirstName} {voter.LastName}");
                HttpContext.Session.SetString("UserRole", "Voter");
                HttpContext.Session.SetString("LoginNationalId", voter.NationalId);

                return Json(new { 
                    success = true, 
                    message = "Password updated successfully! Logging you in...",
                    redirectUrl = Url.Action("castvote", "Voter")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating image password");
                return Json(new { success = false, message = "Error updating password. Please try again." });
            }
        }

        // Helper method to clean expired one-time passwords
        private void CleanExpiredOneTimePasswords()
        {
            var expiredKeys = _oneTimeImagePasswords
                .Where(kvp => DateTime.Now > kvp.Value.ExpiryTime || kvp.Value.IsUsed)
                .Select(kvp => kvp.Key)
                .ToList();
                
            foreach (var key in expiredKeys)
            {
                _oneTimeImagePasswords.Remove(key);
            }
        }

        // Add this method to check what's stored in the database
        [HttpGet]
        public async Task<IActionResult> CheckVisualPassword(string nationalId)
        {
            try
            {
                var voter = await _context.Voters
                    .Where(v => v.NationalId == nationalId)
                    .Select(v => new { v.NationalId, v.VisualPIN, v.Literate, v.FirstName, v.LastName })
                    .FirstOrDefaultAsync();
                    
                if (voter == null)
                {
                    return Json(new { success = false, message = "Voter not found" });
                }
                
                // Convert image IDs to emojis for display
                string visualPINWithEmojis = "";
                if (!string.IsNullOrEmpty(voter.VisualPIN))
                {
                    var imageIds = voter.VisualPIN.Split(',');
                    var emojis = imageIds.Select(id => 
                        int.TryParse(id, out int imageId) && ImageIdToEmoji.ContainsKey(imageId) 
                            ? ImageIdToEmoji[imageId] 
                            : "❓").ToArray();
                    visualPINWithEmojis = string.Join(",", emojis);
                }
                
                return Json(new { 
                    success = true, 
                    nationalId = voter.NationalId,
                    visualPIN = voter.VisualPIN, // Image IDs stored
                    visualPINWithEmojis = visualPINWithEmojis, // Emojis for display
                    literate = voter.Literate,
                    name = $"{voter.FirstName} {voter.LastName}",
                    message = $"VisualPIN stored as image IDs: '{voter.VisualPIN}'"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking visual password");
                return Json(new { success = false, message = "Error checking password" });
            }
        }
    }

    public class ContactForm
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; }
        
        [Required]
        [EmailAddress]
        public string Email { get; set; }
        
        [Required]
        [StringLength(200)]
        public string Subject { get; set; }
        
        [Required]
        [StringLength(1000)]
        public string Message { get; set; }
        
        public DateTime SubmittedAt { get; set; }
        public bool IsRead { get; set; }
    }

    public class ErrorViewModel
    {
        public string RequestId { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}