using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using VotingSystem.Models;
using Microsoft.EntityFrameworkCore;
using VotingSystem.Data;
using System.IO;
using VotingSystem.Services;

namespace VotingSystem.Controllers
{
    public class ManagerController : Controller
    {
        private readonly string _connectionString;
        private readonly ILogger<ManagerController> _logger;
        private readonly AppDbContext _context;
        private readonly IOCRService _ocrService;

        public ManagerController(IConfiguration configuration, ILogger<ManagerController> logger, AppDbContext context, IOCRService ocrService)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _logger = logger;
            _context = context;
            _ocrService = ocrService;
        }

        // Helper method to check manager session
        private bool IsManagerLoggedIn()
        {
            var managerNationalId = HttpContext.Session.GetString("ManagerNationalId");
            var managerName = HttpContext.Session.GetString("ManagerName");
            
            return !string.IsNullOrEmpty(managerNationalId) && !string.IsNullOrEmpty(managerName);
        }

        // Redirect to login if not authenticated
        private IActionResult RedirectToLogin()
        {
            TempData["ErrorMessage"] = "Please login to access this page.";
            return RedirectToAction("Login", "Home");
        }

        // ========== DASHBOARD ==========
        public IActionResult Dashboard()
        {
            // Check if manager is logged in
            var managerNationalId = HttpContext.Session.GetString("ManagerNationalId");
            var managerName = HttpContext.Session.GetString("ManagerName");
            
            if (string.IsNullOrEmpty(managerNationalId) || string.IsNullOrEmpty(managerName))
            {
                return RedirectToLogin();
            }

            ViewBag.ManagerName = managerName;
            return View();
        }

        // ========== CREATE VOTER ==========
        public IActionResult CreateVoter()
        {
            // Check if manager is logged in
            var managerNationalId = HttpContext.Session.GetString("ManagerNationalId");
            var managerName = HttpContext.Session.GetString("ManagerName");
            
            if (string.IsNullOrEmpty(managerNationalId) || string.IsNullOrEmpty(managerName))
            {
                return RedirectToLogin();
            }

            ViewBag.ManagerName = managerName;
            return View();
        }

        // ========== UPDATE VOTER ==========
        public IActionResult UpdateVoter()
        {
            // Check if manager is logged in
            var managerNationalId = HttpContext.Session.GetString("ManagerNationalId");
            var managerName = HttpContext.Session.GetString("ManagerName");
            
            if (string.IsNullOrEmpty(managerNationalId) || string.IsNullOrEmpty(managerName))
            {
                return RedirectToLogin();
            }

            ViewBag.ManagerName = managerName;
            return View();
        }

        // ========== DELETE VOTER ==========
        public IActionResult DeleteVoter()
        {
            // Check if manager is logged in
            var managerNationalId = HttpContext.Session.GetString("ManagerNationalId");
            var managerName = HttpContext.Session.GetString("ManagerName");
            
            if (string.IsNullOrEmpty(managerNationalId) || string.IsNullOrEmpty(managerName))
            {
                return RedirectToLogin();
            }

            ViewBag.ManagerName = managerName;
            return View();
        }

        // ========== VOTER LIST ==========
        public IActionResult VoterList()
        {
            // Check if manager is logged in
            var managerNationalId = HttpContext.Session.GetString("ManagerNationalId");
            var managerName = HttpContext.Session.GetString("ManagerName");
            
            if (string.IsNullOrEmpty(managerNationalId) || string.IsNullOrEmpty(managerName))
            {
                return RedirectToLogin();
            }

            ViewBag.ManagerName = managerName;
            return View();
        }

        // ========== OCR PROCESSING FOR MANAGER ==========
        [HttpPost]
        public async Task<IActionResult> ProcessIDPhotos(IFormFile FrontImage, IFormFile BackImage)
        {
            try
            {
                // Check if manager is logged in
                if (!IsManagerLoggedIn())
                {
                    return Json(new { success = false, error = "Please login to access this feature." });
                }

                _logger.LogInformation("=== STARTING ID PHOTO PROCESSING FOR MANAGER VOTER REGISTRATION ===");

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

                _logger.LogInformation("Calling Ethiopian OCR service for manager voter registration...");
                var result = await _ocrService.ProcessEthiopianIDAsync(frontImageBase64, backImageBase64);
                
                _logger.LogInformation("=== RAW OCR RESULT FOR MANAGER VOTER REGISTRATION ===");
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
                    // Calculate age from date of birth if available and age is not provided
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
                _logger.LogError(ex, "Error processing ID photos for manager voter registration");
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
                
                // Try different date formats
                if (dateOfBirth.Contains("/"))
                {
                    var parts = dateOfBirth.Split('/');
                    if (parts.Length == 3 && int.TryParse(parts[2], out int year))
                    {
                        // Handle both Gregorian and Ethiopian dates
                        var gregorianYear = year;
                        if (year < 1000) // Likely Ethiopian year
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

        // ========== DASHBOARD STATISTICS ==========
        [HttpGet]
        public async Task<IActionResult> GetDashboardStats()
        {
            try
            {
                // Check if manager is logged in
                if (!IsManagerLoggedIn())
                {
                    return Json(new { success = false, message = "Please login to access this feature." });
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    var command = new SqlCommand(@"
                        SELECT 
                            (SELECT COUNT(*) FROM Voters) as TotalVoters,
                            (SELECT COUNT(*) FROM Voters WHERE CAST(RegisterDate AS DATE) = CAST(GETDATE() AS DATE)) as TodayRegistrations,
                            (SELECT COUNT(*) FROM Voters WHERE Sex = 'Male') as MaleVoters,
                            (SELECT COUNT(*) FROM Voters WHERE Sex = 'Female') as FemaleVoters,
                            (SELECT COUNT(*) FROM Voters WHERE Literate = 'Yes') as LiterateVoters,
                            (SELECT COUNT(*) FROM Voters WHERE Literate = 'No') as IlliterateVoters
                    ", connection);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var stats = new
                            {
                                totalVoters = reader.GetInt32("TotalVoters"),
                                todayRegistrations = reader.GetInt32("TodayRegistrations"),
                                maleVoters = reader.GetInt32("MaleVoters"),
                                femaleVoters = reader.GetInt32("FemaleVoters"),
                                literateVoters = reader.GetInt32("LiterateVoters"),
                                illiterateVoters = reader.GetInt32("IlliterateVoters")
                            };
                            return Json(new { success = true, stats = stats });
                        }
                    }
                }

                return Json(new { success = false, message = "No statistics found." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard statistics");
                return Json(new { success = false, message = "Error retrieving dashboard statistics: " + ex.Message });
            }
        }

        // ========== ENHANCED VOTER REGISTRATION WITH LITERACY SUPPORT ==========
        [HttpPost]
        public async Task<IActionResult> RegisterVoter(
            [FromForm] string NationalId,
            [FromForm] string FirstName,
            [FromForm] string MiddleName,
            [FromForm] string LastName,
            [FromForm] string PhoneNumber,
            [FromForm] string Password,
            [FromForm] string Nationality,
            [FromForm] string Region,
            [FromForm] string Age,
            [FromForm] string Sex,
            [FromForm] string Literate,
            [FromForm] string ImagePassword = null)
        {
            try
            {
                // Check if manager is logged in
                if (!IsManagerLoggedIn())
                {
                    return Json(new { success = false, message = "Please login to access this feature." });
                }

                _logger.LogInformation("=== MANAGER VOTER REGISTRATION ===");
                _logger.LogInformation($"Received: NationalId={NationalId}, FirstName={FirstName}, LastName={LastName}, Literate={Literate}");

                // Validation (keep existing validation)
                if (string.IsNullOrWhiteSpace(NationalId) ||
                    string.IsNullOrWhiteSpace(FirstName) ||
                    string.IsNullOrWhiteSpace(MiddleName) ||
                    string.IsNullOrWhiteSpace(LastName) ||
                    string.IsNullOrWhiteSpace(PhoneNumber) ||
                    string.IsNullOrWhiteSpace(Nationality) ||
                    string.IsNullOrWhiteSpace(Region) ||
                    string.IsNullOrWhiteSpace(Age) ||
                    string.IsNullOrWhiteSpace(Sex) ||
                    string.IsNullOrWhiteSpace(Literate))
                {
                    _logger.LogWarning("Missing required fields");
                    return Json(new { success = false, message = "All required fields must be filled." });
                }

                if (!int.TryParse(Age, out int age) || age < 18 || age > 120)
                {
                    return Json(new { success = false, message = "Age must be between 18 and 120." });
                }

                if (Sex != "Male" && Sex != "Female")
                {
                    return Json(new { success = false, message = "Sex must be Male or Female." });
                }

                if (Literate != "Yes" && Literate != "No")
                {
                    return Json(new { success = false, message = "Literacy status must be Yes or No." });
                }

                // Check for duplicates
                var checkResult = await CheckNationalIdInAllTables(NationalId);
                if (checkResult.exists)
                {
                    return Json(new { success = false, message = checkResult.message });
                }

                // Check phone number uniqueness
                if (await CheckPhoneNumberExists(PhoneNumber))
                {
                    return Json(new { success = false, message = "Phone number already exists. Please use a different phone number." });
                }

                string hashedPassword;
                string visualPIN = "";
                bool prefersVisualLogin = false;

                // Handle password based on literacy status
                if (Literate == "Yes")
                {
                    // Validate text password for literate users
                    if (string.IsNullOrWhiteSpace(Password))
                    {
                        return Json(new { success = false, message = "Password is required for literate users." });
                    }

                    if (Password.Length < 6)
                    {
                        return Json(new { success = false, message = "Password must be at least 6 characters long." });
                    }

                    hashedPassword = HashPasswordBase64(Password);
                    visualPIN = "🦁,☕,🌾,🏠"; // Default for literate users
                    prefersVisualLogin = false;
                }
                else
                {
                    // Validate image password for illiterate users
                    if (string.IsNullOrWhiteSpace(ImagePassword))
                    {
                        return Json(new { success = false, message = "Image password is required for illiterate users." });
                    }

                    var selectedImages = ImagePassword.Split(',').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                    if (selectedImages.Count != 4)
                    {
                        return Json(new { success = false, message = "Please select exactly 4 images for the password." });
                    }

                    // Store image password in VisualPIN for illiterate users
                    visualPIN = ImagePassword;
                    hashedPassword = HashPasswordBase64("visual_login_default"); // Default password for illiterate users
                    prefersVisualLogin = true;
                }

                // Generate QR Code Data
                string qrCodeData = Guid.NewGuid().ToString();
                var currentTime = DateTime.Now;

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    // Updated SQL command with QRCodeData field
                    var command = new SqlCommand(@"
                        INSERT INTO Voters 
                        (NationalId, Nationality, Region, PhoneNumber, FirstName, MiddleName, LastName, Age, Sex, Password, Literate, VisualPIN, QRCodeData, PrefersVisualLogin, RegisterDate, CreatedAt, UpdatedAt) 
                        VALUES (@NationalId, @Nationality, @Region, @PhoneNumber, @FirstName, @MiddleName, @LastName, @Age, @Sex, @Password, @Literate, @VisualPIN, @QRCodeData, @PrefersVisualLogin, @RegisterDate, @CreatedAt, @UpdatedAt)", 
                        connection);

                    command.Parameters.AddWithValue("@NationalId", NationalId);
                    command.Parameters.AddWithValue("@Nationality", Nationality);
                    command.Parameters.AddWithValue("@Region", Region);
                    command.Parameters.AddWithValue("@PhoneNumber", PhoneNumber);
                    command.Parameters.AddWithValue("@FirstName", FirstName);
                    command.Parameters.AddWithValue("@MiddleName", MiddleName);
                    command.Parameters.AddWithValue("@LastName", LastName);
                    command.Parameters.AddWithValue("@Age", age);
                    command.Parameters.AddWithValue("@Sex", Sex);
                    command.Parameters.AddWithValue("@Password", hashedPassword);
                    command.Parameters.AddWithValue("@Literate", Literate);
                    command.Parameters.AddWithValue("@VisualPIN", visualPIN);
                    command.Parameters.AddWithValue("@QRCodeData", qrCodeData);
                    command.Parameters.AddWithValue("@PrefersVisualLogin", prefersVisualLogin);
                    command.Parameters.AddWithValue("@RegisterDate", currentTime);
                    command.Parameters.AddWithValue("@CreatedAt", currentTime);
                    command.Parameters.AddWithValue("@UpdatedAt", currentTime);

                    var result = await command.ExecuteNonQueryAsync();

                    if (result > 0)
                    {
                        _logger.LogInformation($"SUCCESS: Voter {NationalId} registered as {(Literate == "Yes" ? "Literate" : "Illiterate")} with QRCode: {qrCodeData}");
                        await LogManagerActivity($"Registered new voter: {FirstName} {LastName} ({NationalId}) as {(Literate == "Yes" ? "Literate" : "Illiterate")} with QR Code");
                        return Json(new { success = true, message = $"Voter registered successfully as {(Literate == "Yes" ? "Literate" : "Illiterate")}!" });
                    }
                    else
                    {
                        return Json(new { success = false, message = "Registration failed. Please try again." });
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                _logger.LogError(sqlEx, "Database error during registration");
                string errorMessage = "Registration failed due to database error.";
                if (sqlEx.InnerException != null)
                {
                    string innerMsg = sqlEx.InnerException.Message;
                    if (innerMsg.Contains("PRIMARY KEY") || innerMsg.Contains("duplicate"))
                    {
                        errorMessage = $"National ID {NationalId} is already registered.";
                    }
                    else if (innerMsg.Contains("UNIQUE") && innerMsg.Contains("PhoneNumber"))
                    {
                        errorMessage = "Phone number already registered.";
                    }
                }
                return Json(new { success = false, message = errorMessage });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during registration");
                return Json(new { success = false, message = "An unexpected error occurred: " + ex.Message });
            }
        }

        // ========== VOTER MANAGEMENT METHODS ==========

        [HttpGet]
        public async Task<IActionResult> GetVoters()
        {
            try
            {
                // Check if manager is logged in
                if (!IsManagerLoggedIn())
                {
                    return Json(new { success = false, message = "Please login to access this feature." });
                }

                var voters = new List<Voter>();

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand(
                        "SELECT NationalId, Nationality, Region, PhoneNumber, FirstName, MiddleName, LastName, Age, Sex, Literate, RegisterDate, CreatedAt, UpdatedAt, QRCodeData FROM Voters ORDER BY FirstName, LastName", 
                        connection);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            voters.Add(new Voter
                            {
                                NationalId = reader.GetString("NationalId"),
                                Nationality = reader.GetString("Nationality"),
                                Region = reader.GetString("Region"),
                                PhoneNumber = reader.GetString("PhoneNumber"),
                                FirstName = reader.GetString("FirstName"),
                                MiddleName = reader.GetString("MiddleName"),
                                LastName = reader.GetString("LastName"),
                                Age = reader.GetInt32("Age"),
                                Sex = reader.GetString("Sex"),
                                Literate = reader.GetString("Literate"),
                                RegisterDate = reader.GetDateTime("RegisterDate"),
                                CreatedAt = reader.GetDateTime("CreatedAt"),
                                UpdatedAt = reader.GetDateTime("UpdatedAt"),
                                QRCodeData = reader.IsDBNull(reader.GetOrdinal("QRCodeData")) ? null : reader.GetString("QRCodeData")
                            });
                        }
                    }
                }

                var voterList = voters.Select(v => new
                {
                    nationalId = v.NationalId,
                    firstName = v.FirstName,
                    middleName = v.MiddleName,
                    lastName = v.LastName,
                    phoneNumber = v.PhoneNumber,
                    region = v.Region,
                    nationality = v.Nationality,
                    age = v.Age,
                    sex = v.Sex,
                    literate = v.Literate,
                    registerDate = v.RegisterDate.ToString("yyyy-MM-dd HH:mm"),
                    createdAt = v.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                    updatedAt = v.UpdatedAt.ToString("yyyy-MM-dd HH:mm"),
                    qrCodeData = v.QRCodeData ?? "Not Generated"
                }).ToList();

                return Json(new { success = true, voters = voterList });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting voters");
                return Json(new { success = false, message = "Error retrieving voters: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateVoter(
            [FromForm] string NationalId,
            [FromForm] string PhoneNumber,
            [FromForm] string Password,
            [FromForm] string ConfirmPassword,
            [FromForm] string Literate = null,
            [FromForm] string ImagePassword = null)
        {
            try
            {
                // Check if manager is logged in
                if (!IsManagerLoggedIn())
                {
                    return Json(new { success = false, message = "Please login to access this feature." });
                }

                _logger.LogInformation($"Updating voter: {NationalId}");

                // Validation
                if (string.IsNullOrWhiteSpace(NationalId))
                {
                    return Json(new { success = false, message = "National ID is required." });
                }

                if (string.IsNullOrWhiteSpace(PhoneNumber))
                {
                    return Json(new { success = false, message = "Phone number is required." });
                }

                // Password validation if provided
                if (!string.IsNullOrWhiteSpace(Password))
                {
                    if (Password != ConfirmPassword)
                    {
                        return Json(new { success = false, message = "Password and Confirm Password do not match." });
                    }

                    if (Password.Length < 6)
                    {
                        return Json(new { success = false, message = "Password must be at least 6 characters long." });
                    }
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    // First, check if voter exists
                    var checkCommand = new SqlCommand("SELECT COUNT(*) FROM Voters WHERE NationalId = @NationalId", connection);
                    checkCommand.Parameters.AddWithValue("@NationalId", NationalId);
                    var exists = (int)await checkCommand.ExecuteScalarAsync() > 0;

                    if (!exists)
                    {
                        return Json(new { success = false, message = "Voter not found." });
                    }

                    // Check if phone number is already used by another voter
                    var phoneCheckCommand = new SqlCommand("SELECT COUNT(*) FROM Voters WHERE PhoneNumber = @PhoneNumber AND NationalId != @NationalId", connection);
                    phoneCheckCommand.Parameters.AddWithValue("@PhoneNumber", PhoneNumber);
                    phoneCheckCommand.Parameters.AddWithValue("@NationalId", NationalId);
                    var phoneExists = (int)await phoneCheckCommand.ExecuteScalarAsync() > 0;

                    if (phoneExists)
                    {
                        return Json(new { success = false, message = "Phone number already exists. Please use a different phone number." });
                    }

                    // Build update query based on provided fields
                    string updateQuery = "UPDATE Voters SET PhoneNumber = @PhoneNumber, UpdatedAt = @UpdatedAt";
                    var command = new SqlCommand(updateQuery, connection);

                    command.Parameters.AddWithValue("@NationalId", NationalId);
                    command.Parameters.AddWithValue("@PhoneNumber", PhoneNumber);
                    command.Parameters.AddWithValue("@UpdatedAt", DateTime.Now);

                    // Add password update if provided
                    if (!string.IsNullOrWhiteSpace(Password))
                    {
                        var hashedPassword = HashPasswordBase64(Password);
                        command.CommandText += ", Password = @Password";
                        command.Parameters.AddWithValue("@Password", hashedPassword);
                    }

                    // Add literacy status update if provided
                    if (!string.IsNullOrWhiteSpace(Literate))
                    {
                        command.CommandText += ", Literate = @Literate";
                        command.Parameters.AddWithValue("@Literate", Literate);

                        // Handle visual PIN based on literacy status
                        if (Literate == "Yes")
                        {
                            command.CommandText += ", VisualPIN = @VisualPIN";
                            command.Parameters.AddWithValue("@VisualPIN", "🦁,☕,🌾,🏠");
                        }
                        else if (!string.IsNullOrWhiteSpace(ImagePassword))
                        {
                            command.CommandText += ", VisualPIN = @VisualPIN";
                            command.Parameters.AddWithValue("@VisualPIN", ImagePassword);
                        }
                    }

                    command.CommandText += " WHERE NationalId = @NationalId";

                    var result = await command.ExecuteNonQueryAsync();

                    if (result > 0)
                    {
                        await LogManagerActivity($"Updated voter with National ID: {NationalId}");
                        return Json(new { success = true, message = "Voter updated successfully!" });
                    }
                    else
                    {
                        return Json(new { success = false, message = "No changes made or voter not found." });
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                _logger.LogError(sqlEx, "Database error during voter update");
                return Json(new { success = false, message = "Database error: " + sqlEx.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during voter update");
                return Json(new { success = false, message = "An unexpected error occurred: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteVoter([FromForm] string NationalId)
        {
            try
            {
                // Check if manager is logged in
                if (!IsManagerLoggedIn())
                {
                    return Json(new { success = false, message = "Please login to access this feature." });
                }

                _logger.LogInformation($"Deleting voter: {NationalId}");

                if (string.IsNullOrWhiteSpace(NationalId))
                {
                    return Json(new { success = false, message = "National ID is required." });
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    // First, get voter details for logging
                    var getCommand = new SqlCommand("SELECT FirstName, LastName, Literate FROM Voters WHERE NationalId = @NationalId", connection);
                    getCommand.Parameters.AddWithValue("@NationalId", NationalId);
                    
                    string firstName = "";
                    string lastName = "";
                    string literate = "";
                    
                    using (var reader = await getCommand.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            firstName = reader.GetString("FirstName");
                            lastName = reader.GetString("LastName");
                            literate = reader.GetString("Literate");
                        }
                    }

                    // Now delete the voter
                    var deleteCommand = new SqlCommand("DELETE FROM Voters WHERE NationalId = @NationalId", connection);
                    deleteCommand.Parameters.AddWithValue("@NationalId", NationalId);

                    var result = await deleteCommand.ExecuteNonQueryAsync();

                    if (result > 0)
                    {
                        await LogManagerActivity($"Deleted voter: {firstName} {lastName} ({NationalId}) - Literacy: {literate}");
                        return Json(new { success = true, message = $"Voter {firstName} {lastName} deleted successfully!" });
                    }
                    else
                    {
                        return Json(new { success = false, message = "Voter not found." });
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                _logger.LogError(sqlEx, "Database error during voter deletion");
                return Json(new { success = false, message = "Database error: " + sqlEx.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during voter deletion");
                return Json(new { success = false, message = "An unexpected error occurred: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> CheckNationalId(string nationalId)
        {
            try
            {
                // Check if manager is logged in
                if (!IsManagerLoggedIn())
                {
                    return Json(new { exists = false, message = "Please login to access this feature." });
                }

                if (string.IsNullOrEmpty(nationalId))
                {
                    return Json(new { exists = false });
                }

                nationalId = nationalId.Trim();

                var checkResult = await CheckNationalIdInAllTables(nationalId);
                return Json(new { exists = checkResult.exists, message = checkResult.message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking National ID");
                return Json(new { exists = false, message = "Error checking National ID availability." });
            }
        }

        // ========== HELPER METHODS ==========

        private async Task<(bool exists, string message)> CheckNationalIdInAllTables(string nationalId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    // Check in Voters table
                    var voterCommand = new SqlCommand("SELECT COUNT(*) FROM Voters WHERE NationalId = @NationalId", connection);
                    voterCommand.Parameters.AddWithValue("@NationalId", nationalId);
                    var voterCount = (int)await voterCommand.ExecuteScalarAsync();
                    
                    if (voterCount > 0)
                    {
                        // Get voter details for better error message
                        var voterDetailsCommand = new SqlCommand("SELECT FirstName, LastName, Literate FROM Voters WHERE NationalId = @NationalId", connection);
                        voterDetailsCommand.Parameters.AddWithValue("@NationalId", nationalId);
                        
                        using (var reader = await voterDetailsCommand.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                string firstName = reader.GetString("FirstName");
                                string lastName = reader.GetString("LastName");
                                string literate = reader.GetString("Literate");
                                return (true, $"This National ID ({nationalId}) is already registered as Voter: {firstName} {lastName} (Literate: {literate})");
                            }
                        }
                        return (true, $"This National ID ({nationalId}) is already registered as Voter.");
                    }

                    // Check in Admins table
                    var adminCommand = new SqlCommand("SELECT COUNT(*) FROM Admins WHERE NationalId = @NationalId", connection);
                    adminCommand.Parameters.AddWithValue("@NationalId", nationalId);
                    var adminCount = (int)await adminCommand.ExecuteScalarAsync();
                    
                    if (adminCount > 0)
                    {
                        return (true, $"This National ID ({nationalId}) is already registered as Admin.");
                    }

                    // Check in Managers table
                    var managerCommand = new SqlCommand("SELECT COUNT(*) FROM Managers WHERE NationalId = @NationalId", connection);
                    managerCommand.Parameters.AddWithValue("@NationalId", nationalId);
                    var managerCount = (int)await managerCommand.ExecuteScalarAsync();
                    
                    if (managerCount > 0)
                    {
                        return (true, $"This National ID ({nationalId}) is already registered as Manager.");
                    }

                    // Check in Supervisors table
                    var supervisorCommand = new SqlCommand("SELECT COUNT(*) FROM Supervisors WHERE NationalId = @NationalId", connection);
                    supervisorCommand.Parameters.AddWithValue("@NationalId", nationalId);
                    var supervisorCount = (int)await supervisorCommand.ExecuteScalarAsync();
                    
                    if (supervisorCount > 0)
                    {
                        return (true, $"This National ID ({nationalId}) is already registered as Supervisor.");
                    }

                    // Check in Candidates table
                    var candidateCommand = new SqlCommand("SELECT COUNT(*) FROM Candidates WHERE NationalId = @NationalId", connection);
                    candidateCommand.Parameters.AddWithValue("@NationalId", nationalId);
                    var candidateCount = (int)await candidateCommand.ExecuteScalarAsync();
                    
                    if (candidateCount > 0)
                    {
                        return (true, $"This National ID ({nationalId}) is already registered as Candidate.");
                    }

                    return (false, "");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking National ID in all tables");
                return (false, "Error checking National ID availability.");
            }
        }

        private async Task<bool> CheckPhoneNumberExists(string phoneNumber)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var command = new SqlCommand("SELECT COUNT(*) FROM Voters WHERE PhoneNumber = @PhoneNumber", connection);
                command.Parameters.AddWithValue("@PhoneNumber", phoneNumber);
                var count = (int)await command.ExecuteScalarAsync();
                return count > 0;
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

        private async Task LogManagerActivity(string activityDescription)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand(@"
                        INSERT INTO ManagerActivities (ActivityDescription, ActivityDate) 
                        VALUES (@ActivityDescription, @ActivityDate)", 
                        connection);

                    command.Parameters.AddWithValue("@ActivityDescription", activityDescription);
                    command.Parameters.AddWithValue("@ActivityDate", DateTime.Now);

                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to log manager activity");
            }
        }

        // Search voter by National ID (for update/delete forms)
        [HttpGet]
        public async Task<IActionResult> SearchVoterByNationalId(string nationalId)
        {
            try
            {
                // Check if manager is logged in
                if (!IsManagerLoggedIn())
                {
                    return Json(new { success = false, message = "Please login to access this feature." });
                }

                if (string.IsNullOrEmpty(nationalId))
                {
                    return Json(null);
                }
                
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand(
                        "SELECT NationalId, FirstName, MiddleName, LastName, Age, Sex, Nationality, Region, PhoneNumber, Literate, RegisterDate, CreatedAt FROM Voters WHERE NationalId = @NationalId", 
                        connection);
                    command.Parameters.AddWithValue("@NationalId", nationalId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return Json(new 
                            {
                                nationalId = reader.GetString("NationalId"),
                                firstName = reader.GetString("FirstName"),
                                middleName = reader.GetString("MiddleName"),
                                lastName = reader.GetString("LastName"),
                                age = reader.GetInt32("Age"),
                                sex = reader.GetString("Sex"),
                                nationality = reader.GetString("Nationality"),
                                region = reader.GetString("Region"),
                                phoneNumber = reader.GetString("PhoneNumber"),
                                literate = reader.GetString("Literate"),
                                registerDate = reader.GetDateTime("RegisterDate").ToString("yyyy-MM-dd HH:mm"),
                                createdAt = reader.GetDateTime("CreatedAt").ToString("yyyy-MM-dd HH:mm")
                            });
                        }
                    }
                }

                return Json(null);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Search error: {ex.Message}");
                return Json(null);
            }
        }

        // Get voter details for dropdown population
        [HttpGet]
        public async Task<IActionResult> GetVoterDetails(string nationalId)
        {
            try
            {
                // Check if manager is logged in
                if (!IsManagerLoggedIn())
                {
                    return Json(new { success = false, message = "Please login to access this feature." });
                }

                if (string.IsNullOrEmpty(nationalId))
                {
                    return Json(new { success = false, message = "National ID is required." });
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand(
                        "SELECT NationalId, FirstName, MiddleName, LastName, PhoneNumber, Age, Sex, Nationality, Region, Literate, RegisterDate FROM Voters WHERE NationalId = @NationalId", 
                        connection);
                    command.Parameters.AddWithValue("@NationalId", nationalId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var voter = new
                            {
                                nationalId = reader.GetString("NationalId"),
                                firstName = reader.GetString("FirstName"),
                                middleName = reader.GetString("MiddleName"),
                                lastName = reader.GetString("LastName"),
                                phoneNumber = reader.GetString("PhoneNumber"),
                                age = reader.GetInt32("Age"),
                                sex = reader.GetString("Sex"),
                                nationality = reader.GetString("Nationality"),
                                region = reader.GetString("Region"),
                                literate = reader.GetString("Literate"),
                                registerDate = reader.GetDateTime("RegisterDate").ToString("yyyy-MM-dd HH:mm")
                            };
                            return Json(new { success = true, voter = voter });
                        }
                    }
                }

                return Json(new { success = false, message = "Voter not found." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting voter details");
                return Json(new { success = false, message = "Error retrieving voter details: " + ex.Message });
            }
        }

        // ========== ADD LOGOUT ACTION ==========
        [HttpPost]
        public IActionResult Logout()
        {
            try
            {
                var managerName = HttpContext.Session.GetString("ManagerName");
                var managerNationalId = HttpContext.Session.GetString("ManagerNationalId");
                
                _logger.LogInformation($"Manager logout: {managerName} ({managerNationalId})");
                
                // Clear session
                HttpContext.Session.Clear();
                
                TempData["SuccessMessage"] = "You have been logged out successfully.";
                return RedirectToAction("Login", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during manager logout");
                return RedirectToAction("Login", "Home");
            }
        }
    }
}