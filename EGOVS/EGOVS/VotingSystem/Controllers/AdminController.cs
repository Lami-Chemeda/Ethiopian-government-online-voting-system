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
using VotingSystem.Services;
using VotingSystem.Data;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace VotingSystem.Controllers
{
    public class AdminController : Controller
    {
        private readonly string _connectionString;
        private readonly ILogger<AdminController> _logger;
        private readonly ICommentService _commentService;
        private readonly AppDbContext _context;
        private readonly IOCRService _ocrService;

        public AdminController(IConfiguration configuration, ILogger<AdminController> logger, 
            ICommentService commentService, AppDbContext context, IOCRService ocrService)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _logger = logger;
            _commentService = commentService;
            _context = context;
            _ocrService = ocrService;
        }

        // ========== OCR PROCESSING FOR ADMIN ==========
        [HttpPost]
        public async Task<IActionResult> ProcessIDPhotos(IFormFile FrontImage, IFormFile BackImage)
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                TempData["ErrorMessage"] = "Please login as admin to process ID photos.";
                return RedirectToAction("Login", "Home");
            }

            try
            {
                _logger.LogInformation("=== STARTING ID PHOTO PROCESSING FOR ADMIN USER REGISTRATION ===");

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

                _logger.LogInformation("Calling Ethiopian OCR service for admin user registration...");
                var result = await _ocrService.ProcessEthiopianIDAsync(frontImageBase64, backImageBase64);
                
                _logger.LogInformation("=== RAW OCR RESULT FOR ADMIN USER REGISTRATION ===");
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
                _logger.LogError(ex, "Error processing ID photos for admin user registration");
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

        // Dashboard and Navigation Actions
        public IActionResult Dashboard()
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                TempData["ErrorMessage"] = "Please login as admin to access dashboard.";
                return RedirectToAction("Login", "Home");
            }
            
            return View();
        }

        public IActionResult ManageSupervisor()
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                TempData["ErrorMessage"] = "Please login as admin to manage supervisors.";
                return RedirectToAction("Login", "Home");
            }
            
            return View();
        }

        // NEW: Backup Management Action
        public IActionResult BackupManagement()
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                TempData["ErrorMessage"] = "Please login as admin to access backup management.";
                return RedirectToAction("Login", "Home");
            }
            
            return View();
        }

        // NEW: Security Alert Action
        public IActionResult SecurityAlert()
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                TempData["ErrorMessage"] = "Please login as admin to view security alerts.";
                return RedirectToAction("Login", "Home");
            }
            
            return View();
        }

        // NEW: View Logs Action
        public IActionResult ViewLogs()
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                TempData["ErrorMessage"] = "Please login as admin to view logs.";
                return RedirectToAction("Login", "Home");
            }
            
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Comments()
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                TempData["ErrorMessage"] = "Please login as admin to view comments.";
                return RedirectToAction("Login", "Home");
            }
            
            try
            {
                adminNationalId = HttpContext.Session.GetString("AdminNationalId");
                
                // Get comments specifically for this admin OR general admin messages
                var adminComments = await _context.Comments
    .Where(c => c.ReceiverType == "Admin" && 
               (c.ReceiverNationalId == null || c.ReceiverNationalId == adminNationalId || c.ReceiverNationalId == "SYSTEM_ADMIN"))
    .OrderByDescending(c => c.CreatedAt)
    .Select(c => new CommentViewModel
    {
        Id = c.Id,
        Content = c.Content,
        SenderType = c.SenderType,
        SenderNationalId = c.SenderNationalId,
        SenderName = c.SenderName,
        ReceiverType = c.ReceiverType,
        ReceiverNationalId = c.ReceiverNationalId,
        ReceiverName = c.ReceiverName,
        CreatedAt = c.CreatedAt,
        CommentType = c.CommentType,
        Subject = c.Subject ?? "General Comment",
        IsRead = c.IsRead
    })
    .ToListAsync();

                // Get available supervisors for sending messages
                var availableSupervisors = await _context.Supervisors
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.FirstName)
                    .ThenBy(s => s.LastName)
                    .ToListAsync();

                var model = (adminComments, availableSupervisors);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading comments for admin");
                TempData["ErrorMessage"] = "Unable to load comments at the moment.";
                var model = (new List<CommentViewModel>(), new List<Supervisor>());
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCommentsData()
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                return Json(new { success = false, message = "Please login as admin first." });
            }
            
            try
            {
                adminNationalId = HttpContext.Session.GetString("AdminNationalId");
                
                var comments = await _context.Comments
                    .Where(c => c.ReceiverType == "Admin" && 
                               (c.ReceiverNationalId == null || c.ReceiverNationalId == adminNationalId || c.ReceiverNationalId == "SYSTEM_ADMIN"))
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => new
{
    id = c.Id,
    content = c.Content,
    senderName = c.SenderName,
    senderType = c.SenderType,
    receiverName = c.ReceiverName,
    receiverType = c.ReceiverType,
    createdAt = c.CreatedAt,
    commentType = c.CommentType,
    subject = c.Subject ?? "General Comment",
    isRead = c.IsRead
})
                    .ToListAsync();

                return Json(new { success = true, comments = comments });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting comments data");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateCommentToSupervisor(string content, string supervisorNationalId, string supervisorName)
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                TempData["ErrorMessage"] = "Please login as admin to send comments.";
                return RedirectToAction("Login", "Home");
            }
            
            var adminName = HttpContext.Session.GetString("AdminName") ?? "Admin";

            var result = await _commentService.CreateAdminToSupervisorCommentAsync(content, adminNationalId, adminName, supervisorNationalId, supervisorName);
            
            if (result)
                TempData["Success"] = "Comment sent to supervisor successfully!";
            else
                TempData["Error"] = "Failed to send comment to supervisor.";

            return RedirectToAction("Comments");
        }

        // POST: Admin/DeleteComment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment([FromBody] DeleteCommentRequest request)
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                return Json(new { success = false, message = "Please login as admin first." });
            }

            if (request == null)
            {
                return Json(new { success = false, message = "Invalid request." });
            }

            var commentId = request.CommentId;

            try
            {
                Console.WriteLine($"DeleteComment: CommentId={commentId}, AdminNationalId={adminNationalId}");

                var comment = await _context.Comments
                    .FirstOrDefaultAsync(c => c.Id == commentId && 
                           ((c.ReceiverType == "Admin" && (c.ReceiverNationalId == adminNationalId || c.ReceiverNationalId == "SYSTEM_ADMIN" || c.ReceiverNationalId == null)) ||
                            (c.SenderType == "Admin" && c.SenderNationalId == adminNationalId)));

                if (comment == null)
                {
                    Console.WriteLine($"Comment {commentId} not found or permission denied for admin {adminNationalId}");
                    return Json(new { success = false, message = "Comment not found or you don't have permission to delete it." });
                }

                _context.Comments.Remove(comment);
                await _context.SaveChangesAsync();
                
                Console.WriteLine($"SUCCESS: Comment {commentId} deleted by admin {adminNationalId}");
                return Json(new { success = true, message = "Comment deleted successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting comment {commentId} for admin {adminNationalId}: {ex.Message}");
                return Json(new { success = false, message = "Error deleting comment. Please try again." });
            }
        }

        // POST: Admin/MarkCommentAsRead
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkCommentAsRead([FromBody] MarkAsReadRequest request)
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                return Json(new { success = false, message = "Please login as admin first." });
            }

            if (request == null)
            {
                return Json(new { success = false, message = "Invalid request." });
            }

            try
            {
                Console.WriteLine($"MarkCommentAsRead: CommentId={request.CommentId}, AdminNationalId={adminNationalId}");

                var comment = await _context.Comments
                    .FirstOrDefaultAsync(c => c.Id == request.CommentId && 
                           c.ReceiverType == "Admin" && 
                           (c.ReceiverNationalId == adminNationalId || c.ReceiverNationalId == "SYSTEM_ADMIN" || c.ReceiverNationalId == null));

                if (comment == null)
                {
                    Console.WriteLine($"Comment {request.CommentId} not found for admin {adminNationalId}");
                    return Json(new { success = false, message = "Comment not found." });
                }

                comment.IsRead = true;
                _context.Comments.Update(comment);
                await _context.SaveChangesAsync();

                Console.WriteLine($"SUCCESS: Comment {request.CommentId} marked as read by admin {adminNationalId}");
                return Json(new { success = true, message = "Comment marked as read." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error marking comment {request.CommentId} as read: {ex.Message}");
                return Json(new { success = false, message = "Error marking comment as read." });
            }
        }

        // ========== SUPERVISOR REGISTRATION ==========
        [HttpPost]
        public async Task<IActionResult> RegisterSupervisor(
            [FromForm] string NationalId,
            [FromForm] string FirstName,
            [FromForm] string MiddleName,
            [FromForm] string LastName,
            [FromForm] string PhoneNumber,
            [FromForm] string Email,
            [FromForm] string Password,
            [FromForm] string Nationality,
            [FromForm] string Region,
            [FromForm] string Age,
            [FromForm] string Sex)
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                return Json(new { success = false, message = "Please login as admin to register supervisors." });
            }
            
            try
            {
                _logger.LogInformation("=== SIMPLIFIED SUPERVISOR REGISTRATION ===");
                _logger.LogInformation($"Received: NationalId={NationalId}, FirstName={FirstName}, LastName={LastName}");

                // Validation
                if (string.IsNullOrWhiteSpace(NationalId) ||
                    string.IsNullOrWhiteSpace(FirstName) ||
                    string.IsNullOrWhiteSpace(MiddleName) ||
                    string.IsNullOrWhiteSpace(LastName) ||
                    string.IsNullOrWhiteSpace(PhoneNumber) ||
                    string.IsNullOrWhiteSpace(Password) ||
                    string.IsNullOrWhiteSpace(Nationality) ||
                    string.IsNullOrWhiteSpace(Region) ||
                    string.IsNullOrWhiteSpace(Age) ||
                    string.IsNullOrWhiteSpace(Sex))
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

                var checkResult = await CheckNationalIdInAllTables(NationalId);
                if (checkResult.exists)
                {
                    return Json(new { success = false, message = checkResult.message });
                }

                var hashedPassword = HashPasswordBase64(Password);
                var currentTime = DateTime.Now;

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    var command = new SqlCommand(@"
                        INSERT INTO Supervisors 
                        (NationalId, Nationality, Region, PhoneNumber, FirstName, MiddleName, LastName, Age, Sex, Password, Email, IsActive, CreatedAt, UpdatedAt) 
                        VALUES (@NationalId, @Nationality, @Region, @PhoneNumber, @FirstName, @MiddleName, @LastName, @Age, @Sex, @Password, @Email, @IsActive, @CreatedAt, @UpdatedAt)", 
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
                    command.Parameters.AddWithValue("@Email", Email ?? "");
                    command.Parameters.AddWithValue("@IsActive", true);
                    command.Parameters.AddWithValue("@CreatedAt", currentTime);
                    command.Parameters.AddWithValue("@UpdatedAt", currentTime);

                    var result = await command.ExecuteNonQueryAsync();

                    if (result > 0)
                    {
                        _logger.LogInformation($"SUCCESS: Supervisor {NationalId} registered");
                        await LogSystemActivity(NationalId, "Supervisor", "Register", $"Supervisor {FirstName} {LastName} registered successfully");
                        return Json(new { success = true, message = "Supervisor registered successfully!" });
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
                return Json(new { success = false, message = "Database error: " + sqlEx.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during registration");
                return Json(new { success = false, message = "An unexpected error occurred: " + ex.Message });
            }
        }

        // ========== SUPERVISOR MANAGEMENT METHODS ==========
        [HttpGet]
        public async Task<IActionResult> GetSupervisors()
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                return Json(new { success = false, message = "Please login as admin to view supervisors." });
            }
            
            try
            {
                var supervisors = new List<Supervisor>();

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand(
                        "SELECT NationalId, Nationality, Region, PhoneNumber, FirstName, MiddleName, LastName, Age, Sex, Email, IsActive, CreatedAt, UpdatedAt FROM Supervisors ORDER BY FirstName, LastName", 
                        connection);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            supervisors.Add(new Supervisor
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
                                Email = reader.IsDBNull("Email") ? "" : reader.GetString("Email"),
                                IsActive = reader.GetBoolean("IsActive"),
                                CreatedAt = reader.GetDateTime("CreatedAt"),
                                UpdatedAt = reader.GetDateTime("UpdatedAt")
                            });
                        }
                    }
                }

                var supervisorList = supervisors.Select(s => new
                {
                    nationalId = s.NationalId,
                    firstName = s.FirstName,
                    middleName = s.MiddleName,
                    lastName = s.LastName,
                    email = s.Email,
                    phoneNumber = s.PhoneNumber,
                    region = s.Region,
                    nationality = s.Nationality,
                    age = s.Age,
                    sex = s.Sex,
                    isActive = s.IsActive,
                    createdAt = s.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                    updatedAt = s.UpdatedAt.ToString("yyyy-MM-dd HH:mm")
                }).ToList();

                return Json(new { success = true, supervisors = supervisorList });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting supervisors");
                return Json(new { success = false, message = "Error retrieving supervisors: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateSupervisor(
            [FromForm] string NationalId,
            [FromForm] string Email,
            [FromForm] string Password,
            [FromForm] string ConfirmPassword)
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                return Json(new { success = false, message = "Please login as admin to update supervisors." });
            }
            
            try
            {
                _logger.LogInformation($"Updating supervisor: {NationalId}");

                if (string.IsNullOrWhiteSpace(NationalId))
                {
                    return Json(new { success = false, message = "National ID is required." });
                }

                if (string.IsNullOrWhiteSpace(Email))
                {
                    return Json(new { success = false, message = "Email is required." });
                }

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
                    
                    var checkCommand = new SqlCommand("SELECT COUNT(*) FROM Supervisors WHERE NationalId = @NationalId", connection);
                    checkCommand.Parameters.AddWithValue("@NationalId", NationalId);
                    var exists = (int)await checkCommand.ExecuteScalarAsync() > 0;

                    if (!exists)
                    {
                        return Json(new { success = false, message = "Supervisor not found." });
                    }

                    string updateQuery = "UPDATE Supervisors SET Email = @Email, UpdatedAt = @UpdatedAt";
                    var command = new SqlCommand(updateQuery, connection);

                    command.Parameters.AddWithValue("@NationalId", NationalId);
                    command.Parameters.AddWithValue("@Email", Email);
                    command.Parameters.AddWithValue("@UpdatedAt", DateTime.Now);

                    if (!string.IsNullOrWhiteSpace(Password))
                    {
                        var hashedPassword = HashPasswordBase64(Password);
                        command.CommandText += ", Password = @Password";
                        command.Parameters.AddWithValue("@Password", hashedPassword);
                    }

                    command.CommandText += " WHERE NationalId = @NationalId";

                    var result = await command.ExecuteNonQueryAsync();

                    if (result > 0)
                    {
                        await LogAdminActivity($"Updated supervisor with National ID: {NationalId}");
                        await LogSystemActivity(NationalId, "Supervisor", "Update", $"Supervisor account updated");
                        return Json(new { success = true, message = "Supervisor updated successfully!" });
                    }
                    else
                    {
                        return Json(new { success = false, message = "No changes made or supervisor not found." });
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                _logger.LogError(sqlEx, "Database error during supervisor update");
                return Json(new { success = false, message = "Database error: " + sqlEx.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during supervisor update");
                return Json(new { success = false, message = "An unexpected error occurred: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteSupervisor([FromForm] string NationalId)
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                return Json(new { success = false, message = "Please login as admin to delete supervisors." });
            }
            
            try
            {
                _logger.LogInformation($"Deleting supervisor: {NationalId}");

                if (string.IsNullOrWhiteSpace(NationalId))
                {
                    return Json(new { success = false, message = "National ID is required." });
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    var getCommand = new SqlCommand("SELECT FirstName, LastName FROM Supervisors WHERE NationalId = @NationalId", connection);
                    getCommand.Parameters.AddWithValue("@NationalId", NationalId);
                    
                    string firstName = "";
                    string lastName = "";
                    
                    using (var reader = await getCommand.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            firstName = reader.GetString("FirstName");
                            lastName = reader.GetString("LastName");
                        }
                    }

                    var deleteCommand = new SqlCommand("DELETE FROM Supervisors WHERE NationalId = @NationalId", connection);
                    deleteCommand.Parameters.AddWithValue("@NationalId", NationalId);

                    var result = await deleteCommand.ExecuteNonQueryAsync();

                    if (result > 0)
                    {
                        await LogAdminActivity($"Deleted supervisor: {firstName} {lastName} ({NationalId})");
                        await LogSystemActivity(NationalId, "Supervisor", "Delete", $"Supervisor {firstName} {lastName} deleted from system");
                        return Json(new { success = true, message = $"Supervisor {firstName} {lastName} deleted successfully!" });
                    }
                    else
                    {
                        return Json(new { success = false, message = "Supervisor not found." });
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                _logger.LogError(sqlEx, "Database error during supervisor deletion");
                return Json(new { success = false, message = "Database error: " + sqlEx.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during supervisor deletion");
                return Json(new { success = false, message = "An unexpected error occurred: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ToggleSupervisorStatus([FromForm] string NationalId, [FromForm] string IsActive)
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                return Json(new { success = false, message = "Please login as admin to toggle supervisor status." });
            }
            
            try
            {
                _logger.LogInformation($"Toggling supervisor status: {NationalId} to {IsActive}");

                if (string.IsNullOrEmpty(NationalId))
                {
                    return Json(new { success = false, message = "National ID is required." });
                }

                if (!bool.TryParse(IsActive, out bool isActive))
                {
                    return Json(new { success = false, message = "Invalid status value." });
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand(
                        "UPDATE Supervisors SET IsActive = @IsActive, UpdatedAt = @UpdatedAt WHERE NationalId = @NationalId", 
                        connection);

                    command.Parameters.AddWithValue("@NationalId", NationalId);
                    command.Parameters.AddWithValue("@IsActive", isActive);
                    command.Parameters.AddWithValue("@UpdatedAt", DateTime.Now);

                    var result = await command.ExecuteNonQueryAsync();

                    if (result > 0)
                    {
                        var status = isActive ? "activated" : "deactivated";
                        await LogAdminActivity($"{status} supervisor: {NationalId}");
                        await LogSystemActivity(NationalId, "Supervisor", "Status Change", $"Supervisor account {status}");
                        return Json(new { success = true, message = $"Supervisor {status} successfully." });
                    }
                    else
                    {
                        return Json(new { success = false, message = "Supervisor not found." });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling supervisor status");
                return Json(new { success = false, message = "Error updating supervisor status: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> CheckNationalId(string nationalId)
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                return Json(new { exists = false, message = "Please login as admin first." });
            }
            
            if (string.IsNullOrEmpty(nationalId))
            {
                return Json(new { exists = false });
            }

            nationalId = nationalId.Trim();

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    var voterCommand = new SqlCommand("SELECT COUNT(*) FROM Voters WHERE NationalId = @NationalId", connection);
                    voterCommand.Parameters.AddWithValue("@NationalId", nationalId);
                    var voterCount = (int)await voterCommand.ExecuteScalarAsync();
                    
                    if (voterCount > 0)
                    {
                        return Json(new { 
                            exists = true, 
                            message = $"This National ID ({nationalId}) is already registered as Voter. Please use a different National ID." 
                        });
                    }

                    var adminCommand = new SqlCommand("SELECT COUNT(*) FROM Admins WHERE NationalId = @NationalId", connection);
                    adminCommand.Parameters.AddWithValue("@NationalId", nationalId);
                    var adminCount = (int)await adminCommand.ExecuteScalarAsync();
                    
                    if (adminCount > 0)
                    {
                        return Json(new { 
                            exists = true, 
                            message = $"This National ID ({nationalId}) is already registered as Admin. Please use a different National ID." 
                        });
                    }

                    var managerCommand = new SqlCommand("SELECT COUNT(*) FROM Managers WHERE NationalId = @NationalId", connection);
                    managerCommand.Parameters.AddWithValue("@NationalId", nationalId);
                    var managerCount = (int)await managerCommand.ExecuteScalarAsync();
                    
                    if (managerCount > 0)
                    {
                        return Json(new { 
                            exists = true, 
                            message = $"This National ID ({nationalId}) is already registered as Manager. Please use a different National ID." 
                        });
                    }

                    var supervisorCommand = new SqlCommand("SELECT COUNT(*) FROM Supervisors WHERE NationalId = @NationalId", connection);
                    supervisorCommand.Parameters.AddWithValue("@NationalId", nationalId);
                    var supervisorCount = (int)await supervisorCommand.ExecuteScalarAsync();
                    
                    if (supervisorCount > 0)
                    {
                        return Json(new { 
                            exists = true, 
                            message = $"This National ID ({nationalId}) is already registered as Supervisor. Please use a different National ID." 
                        });
                    }

                    var candidateCommand = new SqlCommand("SELECT COUNT(*) FROM Candidates WHERE NationalId = @NationalId", connection);
                    candidateCommand.Parameters.AddWithValue("@NationalId", nationalId);
                    var candidateCount = (int)await candidateCommand.ExecuteScalarAsync();
                    
                    if (candidateCount > 0)
                    {
                        return Json(new { 
                            exists = true, 
                            message = $"This National ID ({nationalId}) is already registered as Candidate. Please use a different National ID." 
                        });
                    }

                    return Json(new { exists = false });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking National ID");
                return Json(new { exists = false, message = "Error checking National ID availability." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSystemStats()
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                return Json(new { success = false, message = "Please login as admin to view system statistics." });
            }
            
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    var command = new SqlCommand(@"
                        SELECT 
                            (SELECT COUNT(*) FROM Voters) as TotalVoters,
                            (SELECT COUNT(*) FROM Voters WHERE CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE)) as NewVotersToday,
                            (SELECT COUNT(*) FROM Supervisors) as TotalSupervisors,
                            (SELECT COUNT(*) FROM Supervisors WHERE IsActive = 1) as ActiveSupervisors,
                            (SELECT COUNT(*) FROM Managers) as TotalManagers,
                            (SELECT COUNT(*) FROM Managers WHERE IsActive = 1) as ActiveManagers,
                            (SELECT COUNT(*) FROM Votes) as TotalVotes,
                            (SELECT COUNT(*) FROM Votes WHERE CAST(VoteDate AS DATE) = CAST(GETDATE() AS DATE)) as VotesToday,
                            (SELECT COUNT(*) FROM Comments) as TotalComments,
                            (SELECT COUNT(*) FROM Comments WHERE CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE)) as TodayComments
                    ", connection);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var stats = new
                            {
                                totalVoters = reader.GetInt32("TotalVoters"),
                                newVotersToday = reader.GetInt32("NewVotersToday"),
                                totalSupervisors = reader.GetInt32("TotalSupervisors"),
                                activeSupervisors = reader.GetInt32("ActiveSupervisors"),
                                totalManagers = reader.GetInt32("TotalManagers"),
                                activeManagers = reader.GetInt32("ActiveManagers"),
                                totalVotes = reader.GetInt32("TotalVotes"),
                                votesToday = reader.GetInt32("VotesToday"),
                                totalComments = reader.GetInt32("TotalComments"),
                                todayComments = reader.GetInt32("TodayComments")
                            };
                            return Json(new { success = true, stats = stats });
                        }
                    }
                }

                return Json(new { success = false, message = "No system statistics found." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system statistics");
                return Json(new { success = false, message = "Error retrieving system statistics: " + ex.Message });
            }
        }

        // ========== MANAGE ACCOUNTS - CRUD FOR ALL ACTORS ==========
        public IActionResult ManageAccounts()
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                TempData["ErrorMessage"] = "Please login as admin to manage accounts.";
                return RedirectToAction("Login", "Home");
            }
            
            return View();
        }

        // ========== GET ALL USERS FROM ALL TABLES ==========
        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                return Json(new { success = false, message = "Please login as admin to view users." });
            }
            
            try
            {
                var allUsers = new List<object>();

                // Get Admins - FIXED: Admin doesn't have CreatedAt, IsActive, etc.
                var admins = await _context.Admins
                    .Select(a => new
                    {
                        nationalId = a.NationalId,
                        firstName = a.FirstName,
                        middleName = a.MiddleName,
                        lastName = a.LastName,
                        email = "", // Admin doesn't have Email property
                        phoneNumber = a.PhoneNumber,
                        role = "Admin",
                        nationality = a.Nationality,
                        region = a.Region,
                        age = a.Age,
                        sex = a.Sex,
                        isActive = true, // Admin is always active
                        createdAt = DateTime.Now, // Use current time since Admin doesn't have CreatedAt
                        party = "", 
                        bio = "",
                        literate = "",
                        symbolName = "",
                        partyColor = ""
                    })
                    .ToListAsync();

                // Get Managers
                var managers = await _context.Managers
                    .Select(m => new
                    {
                        nationalId = m.NationalId,
                        firstName = m.FirstName,
                        middleName = m.MiddleName,
                        lastName = m.LastName,
                        email = m.Email ?? "",
                        phoneNumber = m.PhoneNumber,
                        role = "Manager",
                        nationality = m.Nationality,
                        region = m.Region,
                        age = m.Age,
                        sex = m.Sex,
                        isActive = m.IsActive,
                        createdAt = m.CreatedAt,
                        party = "",
                        bio = "",
                        literate = "",
                        symbolName = "",
                        partyColor = ""
                    })
                    .ToListAsync();

                // Get Supervisors
                var supervisors = await _context.Supervisors
                    .Select(s => new
                    {
                        nationalId = s.NationalId,
                        firstName = s.FirstName,
                        middleName = s.MiddleName,
                        lastName = s.LastName,
                        email = s.Email ?? "",
                        phoneNumber = s.PhoneNumber,
                        role = "Supervisor",
                        nationality = s.Nationality,
                        region = s.Region,
                        age = s.Age,
                        sex = s.Sex,
                        isActive = s.IsActive,
                        createdAt = s.CreatedAt,
                        party = "",
                        bio = "",
                        literate = "",
                        symbolName = "",
                        partyColor = ""
                    })
                    .ToListAsync();

                // Get Voters
                var voters = await _context.Voters
                    .Select(v => new
                    {
                        nationalId = v.NationalId,
                        firstName = v.FirstName,
                        middleName = v.MiddleName,
                        lastName = v.LastName,
                        email = "",
                        phoneNumber = v.PhoneNumber,
                        role = "Voter",
                        nationality = v.Nationality,
                        region = v.Region,
                        age = v.Age,
                        sex = v.Sex,
                        isActive = true, // Voter doesn't have IsActive property
                        createdAt = v.CreatedAt,
                        party = "",
                        bio = "",
                        literate = v.Literate,
                        symbolName = "",
                        partyColor = ""
                    })
                    .ToListAsync();

                // Get Candidates
                var candidates = await _context.Candidates
                    .Select(c => new
                    {
                        nationalId = c.NationalId,
                        firstName = c.FirstName,
                        middleName = c.MiddleName,
                        lastName = c.LastName,
                        email = "",
                        phoneNumber = c.PhoneNumber,
                        role = "Candidate",
                        nationality = c.Nationality,
                        region = c.Region,
                        age = c.Age,
                        sex = c.Sex,
                        isActive = c.IsActive,
                        createdAt = c.CreatedAt,
                        party = c.Party,
                        bio = c.Bio ?? "",
                        literate = "",
                        symbolName = c.SymbolName ?? "",
                        partyColor = c.PartyColor ?? ""
                    })
                    .ToListAsync();

                allUsers.AddRange(admins);
                allUsers.AddRange(managers);
                allUsers.AddRange(supervisors);
                allUsers.AddRange(voters);
                allUsers.AddRange(candidates);

                return Json(new { success = true, users = allUsers });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all users");
                return Json(new { success = false, message = "Error retrieving users: " + ex.Message });
            }
        }

        // ========== CREATE USER - FIXED TO HANDLE CANDIDATE IMAGES AS FILE PATHS ==========
        // ========== CREATE USER - FIXED TO HANDLE VOTER LITERACY ==========
[HttpPost]
public async Task<IActionResult> CreateUser()
{
    // ADDED: Authentication check
    var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
    if (string.IsNullOrEmpty(adminNationalId))
    {
        return Json(new { success = false, message = "Please login as admin to create users." });
    }
    
    try
    {
        _logger.LogInformation("=== ADMIN CREATE USER REQUEST (FORM DATA) ===");

        // Get form data
        var form = await Request.ReadFormAsync();
        
        var nationalId = form["NationalId"].ToString().Trim();
        var role = form["Role"].ToString();
        var firstName = form["FirstName"].ToString().Trim();
        var middleName = form["MiddleName"].ToString().Trim();
        var lastName = form["LastName"].ToString().Trim();
        var phoneNumber = form["PhoneNumber"].ToString().Trim();
        var password = form["Password"].ToString();
        var nationality = form["Nationality"].ToString();
        var region = form["Region"].ToString();
        var age = int.Parse(form["Age"].ToString());
        var sex = form["Sex"].ToString();
        var email = form["Email"].ToString();
        var username = form["Username"].ToString();
        var party = form["Party"].ToString();
        var bio = form["Bio"].ToString();
        var literate = form["Literate"].ToString();
        var imagePassword = form["ImagePassword"].ToString();
        var qrCodeData = form["QRCodeData"].ToString();
        var visualPIN = form["VisualPIN"].ToString();
        var prefersVisualLogin = bool.Parse(form["PrefersVisualLogin"].ToString());
        
        // Candidate-specific fields
        var symbolName = form["SymbolName"].ToString();
        var symbolUnicode = form["SymbolUnicode"].ToString();
        var partyColor = form["PartyColor"].ToString();

        _logger.LogInformation($"Creating user: {nationalId}, Role: {role}, Literate: {literate}");

        // Basic validation
        if (string.IsNullOrWhiteSpace(nationalId) || string.IsNullOrWhiteSpace(role))
        {
            return Json(new { success = false, message = "National ID and Role are required." });
        }

        // Check for duplicates
        var checkResult = await CheckNationalIdInAllTables(nationalId);
        if (checkResult.exists)
        {
            return Json(new { success = false, message = checkResult.message });
        }

        // Validate based on role
        if (role.ToLower() == "voter")
        {
            if (string.IsNullOrWhiteSpace(literate))
            {
                return Json(new { success = false, message = "Literacy status is required for voters." });
            }

            if (literate != "Yes" && literate != "No")
            {
                return Json(new { success = false, message = "Literacy status must be 'Yes' or 'No'." });
            }

            if (age < 18 || age > 120)
            {
                return Json(new { success = false, message = "Age must be between 18 and 120 for voters." });
            }

            // Validate phone number uniqueness for voters
            if (await CheckPhoneNumberExists(phoneNumber))
            {
                return Json(new { success = false, message = "Phone number already exists for another voter." });
            }
        }

        var hashedPassword = "";
        var currentTime = DateTime.Now;

        _logger.LogInformation($"Creating {role} with NationalId: {nationalId}");

        switch (role.ToLower())
        {
            case "admin":
                var admin = new Admin
                {
                    NationalId = nationalId,
                    FirstName = firstName,
                    MiddleName = middleName,
                    LastName = lastName,
                    PhoneNumber = phoneNumber,
                    Password = HashPasswordBase64(password ?? "default_password"),
                    Nationality = nationality,
                    Region = region,
                    Age = age,
                    Sex = sex
                };
                _context.Admins.Add(admin);
                break;

            case "manager":
                var manager = new Manager
                {
                    NationalId = nationalId,
                    FirstName = firstName,
                    MiddleName = middleName,
                    LastName = lastName,
                    PhoneNumber = phoneNumber,
                    Username = username ?? nationalId,
                    Password = HashPasswordBase64(password ?? "default_password"),
                    Nationality = nationality,
                    Region = region,
                    Age = age,
                    Sex = sex,
                    Email = email,
                    IsActive = true,
                    CreatedAt = currentTime,
                    UpdatedAt = currentTime
                };
                _context.Managers.Add(manager);
                break;

            case "supervisor":
                var supervisor = new Supervisor
                {
                    NationalId = nationalId,
                    FirstName = firstName,
                    MiddleName = middleName,
                    LastName = lastName,
                    PhoneNumber = phoneNumber,
                    Password = HashPasswordBase64(password ?? "default_password"),
                    Nationality = nationality,
                    Region = region,
                    Age = age,
                    Sex = sex,
                    Email = email,
                    IsActive = true,
                    CreatedAt = currentTime,
                    UpdatedAt = currentTime
                };
                _context.Supervisors.Add(supervisor);
                break;

            case "voter":
                // Handle voter based on literacy status (FIXED)
                string voterHashedPassword;
                string voterVisualPIN = "";
                bool voterPrefersVisualLogin = false;

                if (literate == "Yes")
                {
                    // Validate text password for literate users
                    if (string.IsNullOrWhiteSpace(password))
                    {
                        return Json(new { success = false, message = "Password is required for literate voters." });
                    }

                    if (password.Length < 6)
                    {
                        return Json(new { success = false, message = "Password must be at least 6 characters long for literate voters." });
                    }

                    voterHashedPassword = HashPasswordBase64(password);
                    voterVisualPIN = "🦁,☕,🌾,🏠"; // Default for literate users
                    voterPrefersVisualLogin = false;
                }
                else // Illiterate
                {
                    // Validate image password for illiterate users
                    if (string.IsNullOrWhiteSpace(imagePassword))
                    {
                        return Json(new { success = false, message = "Image password is required for illiterate voters." });
                    }

                    var selectedImages = imagePassword.Split(',').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                    if (selectedImages.Count != 4)
                    {
                        return Json(new { success = false, message = "Please select exactly 4 images for the password of illiterate voters." });
                    }

                    // Store image password in VisualPIN for illiterate users
                    voterVisualPIN = imagePassword;
                    voterHashedPassword = HashPasswordBase64("visual_login_default"); // Default password for illiterate users
                    voterPrefersVisualLogin = true;
                }

                var voter = new Voter
                {
                    NationalId = nationalId,
                    FirstName = firstName,
                    MiddleName = middleName,
                    LastName = lastName,
                    PhoneNumber = phoneNumber,
                    Password = voterHashedPassword,
                    Nationality = nationality,
                    Region = region,
                    Age = age,
                    Sex = sex,
                    CreatedAt = currentTime,
                    UpdatedAt = currentTime,
                    RegisterDate = currentTime,
                    Literate = literate,
                    QRCodeData = string.IsNullOrEmpty(qrCodeData) ? Guid.NewGuid().ToString() : qrCodeData,
                    VisualPIN = voterVisualPIN,
                    PrefersVisualLogin = voterPrefersVisualLogin
                };
                _context.Voters.Add(voter);
                break;

            case "candidate":
                // Handle candidate image uploads - SAVE AS FILES, NOT BYTE ARRAYS
                string photoUrl = null;
                string logoUrl = null;
                string symbolImagePath = null;

                // Process candidate photo
                var photoFile = form.Files["createPhotoUpload"];
                if (photoFile != null && photoFile.Length > 0)
                {
                    photoUrl = await SaveCandidateImage(photoFile, nationalId, "photo");
                    _logger.LogInformation($"Candidate photo saved: {photoUrl}");
                }

                // Process party logo
                var logoFile = form.Files["createLogoUpload"];
                if (logoFile != null && logoFile.Length > 0)
                {
                    logoUrl = await SaveCandidateImage(logoFile, nationalId, "logo");
                    _logger.LogInformation($"Party logo saved: {logoUrl}");
                }

                // Process symbol image
                var symbolFile = form.Files["createSymbolUpload"];
                if (symbolFile != null && symbolFile.Length > 0)
                {
                    symbolImagePath = await SaveCandidateImage(symbolFile, nationalId, "symbol");
                    _logger.LogInformation($"Symbol image saved: {symbolImagePath}");
                }
                else
                {
                    // Use default symbol path based on symbol name
                    symbolImagePath = $"/images/symbols/{symbolName?.ToLower() ?? "lion"}.png";
                }

                var candidate = new Candidate
                {
                    NationalId = nationalId,
                    FirstName = firstName,
                    MiddleName = middleName,
                    LastName = lastName,
                    PhoneNumber = phoneNumber,
                    Password = HashPasswordBase64(password ?? "default_password"),
                    Nationality = nationality,
                    Region = region,
                    Age = age,
                    Sex = sex,
                    Party = party ?? "Independent",
                    Bio = bio ?? "",
                    IsActive = true,
                    CreatedAt = currentTime,
                    UpdatedAt = currentTime,
                    SymbolName = symbolName ?? "Lion",
                    SymbolUnicode = symbolUnicode ?? "🦁",
                    PartyColor = partyColor ?? "#1d3557",
                    // Store the file paths instead of byte arrays
                    PhotoUrl = photoUrl,
                    Logo = logoUrl,
                    SymbolImagePath = symbolImagePath
                };
                _context.Candidates.Add(candidate);
                break;

            default:
                return Json(new { success = false, message = "Invalid role specified." });
        }

        await _context.SaveChangesAsync();
        
        adminNationalId = HttpContext.Session.GetString("AdminNationalId");
        await LogSystemActivity(adminNationalId ?? "SYSTEM", "Admin", "Create User", 
            $"Created {role} account for {firstName} {lastName} ({nationalId})");

        _logger.LogInformation($"SUCCESS: Created {role} account for {nationalId}");
        return Json(new { success = true, message = $"{role} account created successfully!" });
    }
    catch (DbUpdateException dbEx)
    {
        _logger.LogError(dbEx, "Database error creating user");
        string errorMessage = "Database error occurred while creating user.";
        if (dbEx.InnerException != null)
        {
            string innerMsg = dbEx.InnerException.Message;
            if (innerMsg.Contains("PRIMARY KEY") || innerMsg.Contains("duplicate"))
            {
                errorMessage = $"National ID is already registered.";
            }
            else if (innerMsg.Contains("UNIQUE") && innerMsg.Contains("PhoneNumber"))
            {
                errorMessage = "Phone number already registered for another voter.";
            }
        }
        return Json(new { success = false, message = errorMessage });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error creating user");
        return Json(new { success = false, message = "An unexpected error occurred: " + ex.Message });
    }
}

// Add this helper method to check phone number uniqueness
private async Task<bool> CheckPhoneNumberExists(string phoneNumber)
{
    try
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
    catch
    {
        return false;
    }
}

        // ========== HELPER METHOD TO SAVE CANDIDATE IMAGES ==========
        private async Task<string> SaveCandidateImage(IFormFile file, string nationalId, string imageType)
        {
            try
            {
                // Create directory if it doesn't exist
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "candidates");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // Generate unique filename
                var fileExtension = Path.GetExtension(file.FileName);
                var fileName = $"{nationalId}_{imageType}_{DateTime.Now:yyyyMMddHHmmss}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                // Save the file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Return the relative path for database storage
                return $"/images/candidates/{fileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving candidate {imageType} image");
                return null; // Return null if saving fails
            }
        }

        // ========== UPDATE USER ==========
        [HttpPost]
        public async Task<IActionResult> UpdateUser([FromBody] UpdateUserRequest request)
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                return Json(new { success = false, message = "Please login as admin to update users." });
            }
            
            try
            {
                if (request == null)
                {
                    return Json(new { success = false, message = "Invalid request data." });
                }

                switch (request.Role.ToLower())
                {
                    case "admin":
                        var admin = await _context.Admins.FindAsync(request.NationalId);
                        if (admin == null) return Json(new { success = false, message = "Admin not found." });
                        
                        // Admin doesn't have Email or IsActive properties
                        admin.PhoneNumber = request.PhoneNumber;
                        
                        if (!string.IsNullOrEmpty(request.Password))
                        {
                            admin.Password = HashPasswordBase64(request.Password);
                        }
                        break;

                    case "manager":
                        var manager = await _context.Managers.FindAsync(request.NationalId);
                        if (manager == null) return Json(new { success = false, message = "Manager not found." });
                        
                        manager.Email = request.Email;
                        manager.PhoneNumber = request.PhoneNumber;
                        manager.IsActive = request.IsActive;
                        manager.UpdatedAt = DateTime.Now;
                        
                        if (!string.IsNullOrEmpty(request.Password))
                        {
                            manager.Password = HashPasswordBase64(request.Password);
                        }
                        break;

                    case "supervisor":
                        var supervisor = await _context.Supervisors.FindAsync(request.NationalId);
                        if (supervisor == null) return Json(new { success = false, message = "Supervisor not found." });
                        
                        supervisor.Email = request.Email;
                        supervisor.PhoneNumber = request.PhoneNumber;
                        supervisor.IsActive = request.IsActive;
                        supervisor.UpdatedAt = DateTime.Now;
                        
                        if (!string.IsNullOrEmpty(request.Password))
                        {
                            supervisor.Password = HashPasswordBase64(request.Password);
                        }
                        break;

                    case "voter":
                        var voter = await _context.Voters.FindAsync(request.NationalId);
                        if (voter == null) return Json(new { success = false, message = "Voter not found." });
                        
                        // Voter doesn't have Email property
                        voter.PhoneNumber = request.PhoneNumber;
                        voter.UpdatedAt = DateTime.Now;
                        
                        if (!string.IsNullOrEmpty(request.Password))
                        {
                            voter.Password = HashPasswordBase64(request.Password);
                        }
                        break;

                    case "candidate":
                        var candidate = await _context.Candidates.FindAsync(request.NationalId);
                        if (candidate == null) return Json(new { success = false, message = "Candidate not found." });
                        
                        // Candidate doesn't have Email property
                        candidate.PhoneNumber = request.PhoneNumber;
                        candidate.IsActive = request.IsActive;
                        candidate.Party = request.Party ?? candidate.Party;
                        candidate.Bio = request.Bio ?? candidate.Bio;
                        candidate.UpdatedAt = DateTime.Now;
                        
                        if (!string.IsNullOrEmpty(request.Password))
                        {
                            candidate.Password = HashPasswordBase64(request.Password);
                        }
                        break;

                    default:
                        return Json(new { success = false, message = "Invalid role specified." });
                }

                await _context.SaveChangesAsync();
                await LogSystemActivity(HttpContext.Session.GetString("AdminNationalId"), "Admin", "Update User", 
                    $"Updated {request.Role} account for {request.NationalId}");

                return Json(new { success = true, message = $"{request.Role} account updated successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user");
                return Json(new { success = false, message = "Error updating user: " + ex.Message });
            }
        }

        // ========== DELETE USER ==========
        [HttpPost]
        public async Task<IActionResult> DeleteUser([FromBody] DeleteUserRequest request)
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                return Json(new { success = false, message = "Please login as admin to delete users." });
            }
            
            try
            {
                if (request == null)
                {
                    return Json(new { success = false, message = "Invalid request data." });
                }

                switch (request.Role.ToLower())
                {
                    case "admin":
                        var admin = await _context.Admins.FindAsync(request.NationalId);
                        if (admin == null) return Json(new { success = false, message = "Admin not found." });
                        _context.Admins.Remove(admin);
                        break;

                    case "manager":
                        var manager = await _context.Managers.FindAsync(request.NationalId);
                        if (manager == null) return Json(new { success = false, message = "Manager not found." });
                        _context.Managers.Remove(manager);
                        break;

                    case "supervisor":
                        var supervisor = await _context.Supervisors.FindAsync(request.NationalId);
                        if (supervisor == null) return Json(new { success = false, message = "Supervisor not found." });
                        _context.Supervisors.Remove(supervisor);
                        break;

                    case "voter":
                        var voter = await _context.Voters.FindAsync(request.NationalId);
                        if (voter == null) return Json(new { success = false, message = "Voter not found." });
                        _context.Voters.Remove(voter);
                        break;

                    case "candidate":
                        var candidate = await _context.Candidates.FindAsync(request.NationalId);
                        if (candidate == null) return Json(new { success = false, message = "Candidate not found." });
                        _context.Candidates.Remove(candidate);
                        break;

                    default:
                        return Json(new { success = false, message = "Invalid role specified." });
                }

                await _context.SaveChangesAsync();
                await LogSystemActivity(HttpContext.Session.GetString("AdminNationalId"), "Admin", "Delete User", 
                    $"Deleted {request.Role} account: {request.NationalId}");

                return Json(new { success = true, message = $"{request.Role} account deleted successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user");
                return Json(new { success = false, message = "Error deleting user: " + ex.Message });
            }
        }

        // ========== TOGGLE USER STATUS ==========
        [HttpPost]
        public async Task<IActionResult> ToggleUserStatus([FromBody] ToggleUserStatusRequest request)
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                return Json(new { success = false, message = "Please login as admin to toggle user status." });
            }
            
            try
            {
                if (request == null)
                {
                    return Json(new { success = false, message = "Invalid request data." });
                }

                switch (request.Role.ToLower())
                {
                    case "admin":
                        // Admin doesn't have IsActive property - can't toggle status
                        return Json(new { success = false, message = "Admin status cannot be toggled." });

                    case "manager":
                        var manager = await _context.Managers.FindAsync(request.NationalId);
                        if (manager == null) return Json(new { success = false, message = "Manager not found." });
                        manager.IsActive = request.IsActive;
                        manager.UpdatedAt = DateTime.Now;
                        break;

                    case "supervisor":
                        var supervisor = await _context.Supervisors.FindAsync(request.NationalId);
                        if (supervisor == null) return Json(new { success = false, message = "Supervisor not found." });
                        supervisor.IsActive = request.IsActive;
                        supervisor.UpdatedAt = DateTime.Now;
                        break;

                    case "voter":
                        // Voter doesn't have IsActive property - can't toggle status
                        return Json(new { success = false, message = "Voter status cannot be toggled." });

                    case "candidate":
                        var candidate = await _context.Candidates.FindAsync(request.NationalId);
                        if (candidate == null) return Json(new { success = false, message = "Candidate not found." });
                        candidate.IsActive = request.IsActive;
                        candidate.UpdatedAt = DateTime.Now;
                        break;

                    default:
                        return Json(new { success = false, message = "Invalid role specified." });
                }

                await _context.SaveChangesAsync();

                var status = request.IsActive ? "activated" : "deactivated";
                await LogSystemActivity(HttpContext.Session.GetString("AdminNationalId"), "Admin", "Toggle User Status", 
                    $"{status} {request.Role} account: {request.NationalId}");

                return Json(new { success = true, message = $"{request.Role} account {status} successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling user status");
                return Json(new { success = false, message = "Error updating user status: " + ex.Message });
            }
        }

        // ========== GET USER STATISTICS ==========
        [HttpGet]
        public async Task<IActionResult> GetUserStatistics()
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                return Json(new { success = false, message = "Please login as admin to view user statistics." });
            }
            
            try
            {
                var stats = new
                {
                    totalAdmins = await _context.Admins.CountAsync(),
                    activeAdmins = await _context.Admins.CountAsync(), // All admins are considered active
                    totalManagers = await _context.Managers.CountAsync(),
                    activeManagers = await _context.Managers.CountAsync(m => m.IsActive),
                    totalSupervisors = await _context.Supervisors.CountAsync(),
                    activeSupervisors = await _context.Supervisors.CountAsync(s => s.IsActive),
                    totalVoters = await _context.Voters.CountAsync(),
                    activeVoters = await _context.Voters.CountAsync(), // All voters are considered active
                    totalCandidates = await _context.Candidates.CountAsync(),
                    activeCandidates = await _context.Candidates.CountAsync(c => c.IsActive),
                    totalUsers = await _context.Admins.CountAsync() + await _context.Managers.CountAsync() + 
                                await _context.Supervisors.CountAsync() + await _context.Voters.CountAsync() + 
                                await _context.Candidates.CountAsync()
                };

                return Json(new { success = true, statistics = stats });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user statistics");
                return Json(new { success = false, message = "Error retrieving user statistics: " + ex.Message });
            }
        }

        // ========== ELECTION TIME MANAGEMENT ==========
        public IActionResult SetElectionTime()
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                TempData["ErrorMessage"] = "Please login as admin to set election time.";
                return RedirectToAction("Login", "Home");
            }
            
            return View();
        }

        // ADD THIS MISSING METHOD - Get Election Settings
        [HttpGet]
        public async Task<IActionResult> GetElectionSettings()
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                return Json(new { success = false, message = "Please login as admin to view election settings." });
            }
            
            try
            {
                var electionSettings = await _context.ElectionSettings
                    .OrderByDescending(e => e.Id)
                    .FirstOrDefaultAsync();

                if (electionSettings == null)
                {
                    // Return default settings if none exist
                    return Json(new { 
                        success = true, 
                        settings = new {
                            electionName = "Ethiopian National Election 2024",
                            startDate = DateTime.Now.AddDays(1).ToString("yyyy-MM-ddTHH:mm"),
                            endDate = DateTime.Now.AddDays(2).ToString("yyyy-MM-ddTHH:mm"),
                            region = "All Regions",
                            isActive = false,
                            status = "Inactive"
                        }
                    });
                }

                var status = GetElectionStatus(electionSettings);

                return Json(new { 
                    success = true, 
                    settings = new {
                        electionName = electionSettings.ElectionName,
                        startDate = electionSettings.StartDate.ToString("yyyy-MM-ddTHH:mm"),
                        endDate = electionSettings.EndDate.ToString("yyyy-MM-ddTHH:mm"),
                        region = electionSettings.Region,
                        isActive = electionSettings.IsActive,
                        status = status
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting election settings");
                return Json(new { success = false, message = "Error retrieving election settings: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateElectionSettings([FromBody] UpdateElectionSettingsRequest request)
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                return Json(new { success = false, message = "Please login as admin to update election settings." });
            }
            
            try
            {
                if (request == null)
                {
                    return Json(new { success = false, message = "Invalid request data." });
                }

                // Validate dates
                if (request.EndDate <= request.StartDate)
                {
                    return Json(new { success = false, message = "End date must be after start date." });
                }

                if (request.StartDate <= DateTime.Now)
                {
                    return Json(new { success = false, message = "Start date must be in the future." });
                }

                var electionSettings = await _context.ElectionSettings
                    .OrderByDescending(e => e.Id)
                    .FirstOrDefaultAsync();

                if (electionSettings == null)
                {
                    electionSettings = new ElectionSettings
                    {
                        CreatedAt = DateTime.Now
                    };
                    _context.ElectionSettings.Add(electionSettings);
                }

                electionSettings.ElectionName = request.ElectionName;
                electionSettings.StartDate = request.StartDate;
                electionSettings.EndDate = request.EndDate;
                electionSettings.Region = request.Region;
                electionSettings.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                await LogSystemActivity(HttpContext.Session.GetString("AdminNationalId") ?? "SYSTEM", "Admin", "Update Election Settings", 
                    $"Updated election settings: {request.ElectionName} from {request.StartDate} to {request.EndDate}");

                return Json(new { success = true, message = "Election settings updated successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating election settings");
                return Json(new { success = false, message = "Error updating election settings: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ToggleElectionStatus([FromBody] ToggleElectionStatusRequest request)
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                return Json(new { success = false, message = "Please login as admin to toggle election status." });
            }
            
            try
            {
                var electionSettings = await _context.ElectionSettings
                    .OrderByDescending(e => e.Id)
                    .FirstOrDefaultAsync();

                if (electionSettings == null)
                {
                    return Json(new { success = false, message = "No election settings found." });
                }

                // Validate if we can activate the election
                if (request.IsActive && electionSettings.StartDate <= DateTime.Now)
                {
                    return Json(new { success = false, message = "Cannot activate election. Start date must be in the future." });
                }

                electionSettings.IsActive = request.IsActive;
                electionSettings.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                var action = request.IsActive ? "activated" : "deactivated";
                await LogSystemActivity(HttpContext.Session.GetString("AdminNationalId") ?? "SYSTEM", "Admin", "Toggle Election Status", 
                    $"{action} election: {electionSettings.ElectionName}");

                return Json(new { success = true, message = $"Election {action} successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling election status");
                return Json(new { success = false, message = "Error updating election status: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> PublishResults([FromBody] PublishResultsRequest request)
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                return Json(new { success = false, message = "Please login as admin to publish results." });
            }
            
            try
            {
                var electionSettings = await _context.ElectionSettings
                    .OrderByDescending(e => e.Id)
                    .FirstOrDefaultAsync();

                if (electionSettings == null)
                {
                    return Json(new { success = false, message = "No election settings found." });
                }

                if (!electionSettings.HasElectionEnded())
                {
                    return Json(new { success = false, message = "Cannot publish results. Election has not ended yet." });
                }

                electionSettings.ResultsPublished = request.Publish;
                electionSettings.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                var action = request.Publish ? "published" : "unpublished";
                await LogSystemActivity(HttpContext.Session.GetString("AdminNationalId") ?? "SYSTEM", "Admin", "Publish Results", 
                    $"{action} election results for: {electionSettings.ElectionName}");

                return Json(new { success = true, message = $"Results {action} successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing results");
                return Json(new { success = false, message = "Error publishing results: " + ex.Message });
            }
        }

        // Helper method to get election status
        private string GetElectionStatus(ElectionSettings settings)
        {
            if (!settings.IsActive)
                return "Inactive";

            var now = DateTime.Now;
            if (now < settings.StartDate)
                return "Scheduled";
            else if (now >= settings.StartDate && now <= settings.EndDate)
                return "Ongoing";
            else
                return "Completed";
        }

        // ========== SECURITY ALERT METHODS ==========
        [HttpGet]
        public async Task<IActionResult> GetSecurityAlerts()
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                return Json(new { success = false, message = "Please login as admin to view security alerts." });
            }
            
            try
            {
                var alerts = await _context.SecurityAlerts
                    .OrderByDescending(a => a.AlertDate)
                    .Take(50)
                    .Select(a => new
                    {
                        id = a.Id,
                        alertType = a.AlertType,
                        description = a.Description,
                        severity = a.Severity,
                        alertDate = a.AlertDate,
                        isResolved = a.IsResolved,
                        nationalId = a.NationalId,
                        role = a.Role
                    })
                    .ToListAsync();

                return Json(new { success = true, alerts = alerts });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting security alerts");
                return Json(new { success = false, message = "Error retrieving security alerts: " + ex.Message });
            }
        }

        // REMOVED: ResolveAlert method
        // REMOVED: IgnoreAlert method  
        // REMOVED: GetAlertDetails method

        // ========== SYSTEM ACTIVITY LOGS METHODS ==========
        [HttpGet]
        public async Task<IActionResult> GetSystemLogs()
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                return Json(new { success = false, message = "Please login as admin to view system logs." });
            }
            
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    var command = new SqlCommand(@"
                        SELECT TOP 500 
                            Id, NationalId, Role, Action, Description, 
                            IpAddress, UserAgent, Timestamp, Status, AdditionalData
                        FROM SystemActivityLogs 
                        ORDER BY Timestamp DESC", connection);

                    var logs = new List<SystemActivityLog>();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            logs.Add(new SystemActivityLog
                            {
                                Id = reader.GetInt32("Id"),
                                NationalId = reader.IsDBNull("NationalId") ? "SYSTEM" : reader.GetString("NationalId"),
                                Role = reader.IsDBNull("Role") ? "System" : reader.GetString("Role"),
                                Action = reader.IsDBNull("Action") ? "Unknown" : reader.GetString("Action"),
                                Description = reader.IsDBNull("Description") ? "No description" : reader.GetString("Description"),
                                IpAddress = reader.IsDBNull("IpAddress") ? null : reader.GetString("IpAddress"),
                                UserAgent = reader.IsDBNull("UserAgent") ? null : reader.GetString("UserAgent"),
                                Timestamp = reader.GetDateTime("Timestamp"),
                                Status = reader.IsDBNull("Status") ? "Attempt" : reader.GetString("Status"),
                                AdditionalData = reader.IsDBNull("AdditionalData") ? null : reader.GetString("AdditionalData")
                            });
                        }
                    }

                    return Json(new { success = true, logs = logs });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system activity logs");
                return Json(new { success = false, message = "Error retrieving system logs: " + ex.Message });
            }
        }

        // ========== CLEAR LOGS FUNCTIONALITY ==========
        [HttpPost]
        public async Task<IActionResult> ClearLogs([FromBody] ClearLogsRequest request)
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                return Json(new { success = false, message = "Please login as admin to clear logs." });
            }
            
            try
            {
                adminNationalId = HttpContext.Session.GetString("AdminNationalId");
                if (string.IsNullOrEmpty(adminNationalId))
                {
                    return Json(new { success = false, message = "Please login first." });
                }

                _logger.LogInformation($"Admin {adminNationalId} clearing logs with type: {request?.ClearType}");

                if (request == null || string.IsNullOrEmpty(request.ClearType))
                {
                    return Json(new { success = false, message = "Invalid request." });
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    int deletedRows = 0;
                    
                    if (request.ClearType == "all")
                    {
                        var command = new SqlCommand("DELETE FROM SystemActivityLogs", connection);
                        deletedRows = await command.ExecuteNonQueryAsync();
                    }
                    else if (request.ClearType == "old")
                    {
                        var cutoffDate = DateTime.Now.AddDays(-30);
                        var command = new SqlCommand("DELETE FROM SystemActivityLogs WHERE Timestamp < @CutoffDate", connection);
                        command.Parameters.AddWithValue("@CutoffDate", cutoffDate);
                        deletedRows = await command.ExecuteNonQueryAsync();
                    }
                    else if (request.ClearType == "failed")
                    {
                        var command = new SqlCommand("DELETE FROM SystemActivityLogs WHERE Status = 'Failed'", connection);
                        deletedRows = await command.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        return Json(new { success = false, message = "Invalid clear type." });
                    }

                    await LogSystemActivity(adminNationalId, "Admin", "Clear Logs", 
                        $"Cleared {deletedRows} records (Type: {request.ClearType})");

                    return Json(new { 
                        success = true, 
                        message = $"Successfully cleared {deletedRows} log records.",
                        deletedCount = deletedRows
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing logs");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetLogStatistics()
        {
            // ADDED: Authentication check
            var adminNationalId = HttpContext.Session.GetString("AdminNationalId");
            if (string.IsNullOrEmpty(adminNationalId))
            {
                return Json(new { success = false, message = "Please login as admin to view log statistics." });
            }
            
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    var command = new SqlCommand(@"
                        SELECT 
                            COUNT(*) as TotalLogs,
                            COUNT(CASE WHEN Status = 'Failed' THEN 1 END) as FailedLogs,
                            COUNT(CASE WHEN Timestamp < DATEADD(day, -30, GETDATE()) THEN 1 END) as OldLogs,
                            MIN(Timestamp) as OldestLog,
                            MAX(Timestamp) as NewestLog
                        FROM SystemActivityLogs", 
                        connection);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var stats = new
                            {
                                totalLogs = reader.GetInt32("TotalLogs"),
                                failedLogs = reader.GetInt32("FailedLogs"),
                                oldLogs = reader.GetInt32("OldLogs"),
                                oldestLog = reader.IsDBNull("OldestLog") ? null : (DateTime?)reader.GetDateTime("OldestLog"),
                                newestLog = reader.IsDBNull("NewestLog") ? null : (DateTime?)reader.GetDateTime("NewestLog")
                            };
                            
                            return Json(new { success = true, stats = stats });
                        }
                    }
                    
                    return Json(new { success = false, message = "No log statistics found." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting log statistics");
                return Json(new { success = false, message = "Error retrieving log statistics: " + ex.Message });
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
                    
                    var voterCommand = new SqlCommand("SELECT COUNT(*) FROM Voters WHERE NationalId = @NationalId", connection);
                    voterCommand.Parameters.AddWithValue("@NationalId", nationalId);
                    var voterCount = (int)await voterCommand.ExecuteScalarAsync();
                    
                    if (voterCount > 0)
                    {
                        return (true, $"This National ID ({nationalId}) is already registered as Voter. Please use a different National ID.");
                    }

                    var adminCommand = new SqlCommand("SELECT COUNT(*) FROM Admins WHERE NationalId = @NationalId", connection);
                    adminCommand.Parameters.AddWithValue("@NationalId", nationalId);
                    var adminCount = (int)await adminCommand.ExecuteScalarAsync();
                    
                    if (adminCount > 0)
                    {
                        return (true, $"This National ID ({nationalId}) is already registered as Admin. Please use a different National ID.");
                    }

                    var managerCommand = new SqlCommand("SELECT COUNT(*) FROM Managers WHERE NationalId = @NationalId", connection);
                    managerCommand.Parameters.AddWithValue("@NationalId", nationalId);
                    var managerCount = (int)await managerCommand.ExecuteScalarAsync();
                    
                    if (managerCount > 0)
                    {
                        return (true, $"This National ID ({nationalId}) is already registered as Manager. Please use a different National ID.");
                    }

                    var supervisorCommand = new SqlCommand("SELECT COUNT(*) FROM Supervisors WHERE NationalId = @NationalId", connection);
                    supervisorCommand.Parameters.AddWithValue("@NationalId", nationalId);
                    var supervisorCount = (int)await supervisorCommand.ExecuteScalarAsync();
                    
                    if (supervisorCount > 0)
                    {
                        return (true, $"This National ID ({nationalId}) is already registered as Supervisor. Please use a different National ID.");
                    }

                    var candidateCommand = new SqlCommand("SELECT COUNT(*) FROM Candidates WHERE NationalId = @NationalId", connection);
                    candidateCommand.Parameters.AddWithValue("@NationalId", nationalId);
                    var candidateCount = (int)await candidateCommand.ExecuteScalarAsync();
                    
                    if (candidateCount > 0)
                    {
                        return (true, $"This National ID ({nationalId}) is already registered as Candidate. Please use a different National ID.");
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

        private async Task LogAdminActivity(string activityDescription)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand(@"
                        INSERT INTO AdminActivities (ActivityDescription, ActivityDate) 
                        VALUES (@ActivityDescription, @ActivityDate)", 
                        connection);

                    command.Parameters.AddWithValue("@ActivityDescription", activityDescription);
                    command.Parameters.AddWithValue("@ActivityDate", DateTime.Now);

                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to log admin activity");
            }
        }

        private async Task LogSystemActivity(string nationalId, string role, string action, string description)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand(@"
                        INSERT INTO SystemActivityLogs (NationalId, Role, Action, Description, Timestamp) 
                        VALUES (@NationalId, @Role, @Action, @Description, @Timestamp)", 
                        connection);

                    command.Parameters.AddWithValue("@NationalId", nationalId ?? "SYSTEM");
                    command.Parameters.AddWithValue("@Role", role);
                    command.Parameters.AddWithValue("@Action", action);
                    command.Parameters.AddWithValue("@Description", description);
                    command.Parameters.AddWithValue("@Timestamp", DateTime.Now);

                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to log system activity");
            }
        }

        private string GetTimeAgo(DateTime date)
        {
            var timeSpan = DateTime.Now - date;
            
            if (timeSpan.TotalDays >= 1)
                return $"{(int)timeSpan.TotalDays} day(s) ago";
            else if (timeSpan.TotalHours >= 1)
                return $"{(int)timeSpan.TotalHours} hour(s) ago";
            else if (timeSpan.TotalMinutes >= 1)
                return $"{(int)timeSpan.TotalMinutes} minute(s) ago";
            else
                return "Just now";
        }

        // ========== REQUEST MODELS ==========
        public class ClearLogsRequest
        {
            public string ClearType { get; set; }
        }

        public class DeleteBackupRequest
        {
            public string FileName { get; set; }
        }

        public class RestoreBackupRequest
        {
            public string FileName { get; set; }
        }

        public class ResolveAlertRequest
        {
            public int AlertId { get; set; }
        }

        public class IgnoreAlertRequest
        {
            public int AlertId { get; set; }
        }

        // ========== REQUEST MODELS FOR MANAGE ACCOUNTS ==========
        public class CreateUserRequest
        {
            public string NationalId { get; set; }
            public string FirstName { get; set; }
            public string MiddleName { get; set; }
            public string LastName { get; set; }
            public string Email { get; set; }
            public string PhoneNumber { get; set; }
            public string Password { get; set; }
            public string Role { get; set; }
            public string Nationality { get; set; }
            public string Region { get; set; }
            public int Age { get; set; }
            public string Sex { get; set; }
            public string Username { get; set; }
            public string Party { get; set; }
            public string Bio { get; set; }
            public string Literate { get; set; }
            public string ImagePassword { get; set; }
        }

        public class UpdateUserRequest
        {
            public string NationalId { get; set; }
            public string Role { get; set; }
            public string Email { get; set; }
            public string PhoneNumber { get; set; }
            public string Password { get; set; }
            public bool IsActive { get; set; }
            public string Party { get; set; }
            public string Bio { get; set; }
        }

        public class DeleteUserRequest
        {
            public string NationalId { get; set; }
            public string Role { get; set; }
        }

        public class ToggleUserStatusRequest
        {
            public string NationalId { get; set; }
            public string Role { get; set; }
            public bool IsActive { get; set; }
        }

        // ========== ELECTION SETTINGS REQUEST MODELS ==========
        public class UpdateElectionSettingsRequest
        {
            public string ElectionName { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public string Region { get; set; }
        }

        public class ToggleElectionStatusRequest
        {
            public bool IsActive { get; set; }
        }

        public class PublishResultsRequest
        {
            public bool Publish { get; set; }
        }
    }
}