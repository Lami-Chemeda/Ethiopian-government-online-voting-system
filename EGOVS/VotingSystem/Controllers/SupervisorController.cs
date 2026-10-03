using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VotingSystem.Models;
using VotingSystem.Data;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using System.Security.Cryptography;
using System.Text;

namespace VotingSystem.Controllers
{
    public class SupervisorController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public SupervisorController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // GET: Supervisor/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            return View();
        }

        // GET: Supervisor/ManageCandidate
        public IActionResult ManageCandidate()
        {
            return View();
        }

        // GET: Supervisor/ManageManager
        public IActionResult ManageManager()
        {
            return View();
        }

        // GET: Supervisor/Comment
        public IActionResult Comment()
        {
            return View();
        }

        // GET: Supervisor/ElectionResult
        public IActionResult ElectionResult()
        {
            return View();
        }

        // GET: Supervisor/LoginManager
        public IActionResult LoginManager()
        {
            return View();
        }

        #region Candidate Management API Methods

        // GET: /Supervisor/GetCandidates
        [HttpGet]
        public async Task<JsonResult> GetCandidates()
        {
            try
            {
                var candidates = await _context.Candidates
                    .Select(c => new 
                    { 
                        id = c.Id,
                        firstName = c.FirstName,
                        lastName = c.LastName,
                        party = c.Party,
                        position = c.Position,
                        bio = c.Bio ?? "",
                        photoUrl = c.PhotoUrl ?? ""
                    })
                    .ToListAsync();

                return Json(new { success = true, candidates = candidates });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: /Supervisor/CreateCandidate (with file upload support)
        [HttpPost]
        public async Task<JsonResult> CreateCandidate()
        {
            try
            {
                var firstName = Request.Form["firstName"].ToString();
                var lastName = Request.Form["lastName"].ToString();
                var party = Request.Form["party"].ToString();
                var position = Request.Form["position"].ToString();
                var bio = Request.Form["bio"].ToString();
                var photoFile = Request.Form.Files["photoFile"];

                if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) || 
                    string.IsNullOrEmpty(party) || string.IsNullOrEmpty(position))
                {
                    return Json(new { success = false, message = "Please fill in all required fields." });
                }

                string photoUrl = null;
                if (photoFile != null && photoFile.Length > 0)
                {
                    photoUrl = await HandleFileUpload(photoFile);
                }

                var candidate = new Candidate
                {
                    FirstName = firstName.Trim(),
                    LastName = lastName.Trim(),
                    Party = party.Trim(),
                    Position = position.Trim(),
                    Bio = string.IsNullOrEmpty(bio) ? null : bio.Trim(),
                    PhotoUrl = photoUrl,
                    CreatedAt = DateTime.Now
                };

                _context.Candidates.Add(candidate);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Candidate created successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error creating candidate: " + ex.Message });
            }
        }

        // POST: /Supervisor/UpdateCandidate (with file upload support)
        [HttpPost]
        public async Task<JsonResult> UpdateCandidate()
        {
            try
            {
                var idStr = Request.Form["id"].ToString();
                var firstName = Request.Form["firstName"].ToString();
                var lastName = Request.Form["lastName"].ToString();
                var party = Request.Form["party"].ToString();
                var position = Request.Form["position"].ToString();
                var bio = Request.Form["bio"].ToString();
                var photoFile = Request.Form.Files["photoFile"];

                if (string.IsNullOrEmpty(idStr) || !int.TryParse(idStr, out int id))
                {
                    return Json(new { success = false, message = "Invalid candidate ID!" });
                }

                if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) || 
                    string.IsNullOrEmpty(party) || string.IsNullOrEmpty(position))
                {
                    return Json(new { success = false, message = "Please fill in all required fields." });
                }

                var candidate = await _context.Candidates.FindAsync(id);
                if (candidate == null)
                {
                    return Json(new { success = false, message = "Candidate not found!" });
                }

                // Handle file upload if a new photo is provided
                if (photoFile != null && photoFile.Length > 0)
                {
                    // Delete old photo if exists
                    if (!string.IsNullOrEmpty(candidate.PhotoUrl))
                    {
                        DeleteOldPhoto(candidate.PhotoUrl);
                    }
                    candidate.PhotoUrl = await HandleFileUpload(photoFile);
                }

                // Update candidate fields
                candidate.FirstName = firstName.Trim();
                candidate.LastName = lastName.Trim();
                candidate.Party = party.Trim();
                candidate.Position = position.Trim();
                candidate.Bio = string.IsNullOrEmpty(bio) ? null : bio.Trim();

                _context.Candidates.Update(candidate);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Candidate updated successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error updating candidate: " + ex.Message });
            }
        }

        // POST: /Supervisor/DeleteCandidate - HARD DELETE (PERMANENT REMOVAL)
        [HttpPost]
        public async Task<JsonResult> DeleteCandidate(int? id)
        {
            try
            {
                // Try to get ID from URL parameter first
                if (id.HasValue)
                {
                    Console.WriteLine($"HARD DELETING candidate with ID from URL: {id.Value}");
                    var candidate = await _context.Candidates.FindAsync(id.Value);
                    if (candidate == null)
                    {
                        return Json(new { success = false, message = "Candidate not found!" });
                    }

                    // Delete photo file if exists
                    if (!string.IsNullOrEmpty(candidate.PhotoUrl))
                    {
                        DeleteOldPhoto(candidate.PhotoUrl);
                    }

                    // HARD DELETE - Remove from database completely
                    _context.Candidates.Remove(candidate);
                    int changes = await _context.SaveChangesAsync();
                    
                    Console.WriteLine($"Candidate HARD DELETED successfully. Changes: {changes} rows affected");

                    return Json(new { success = true, message = "Candidate deleted successfully!" });
                }

                // If no URL parameter, try form data
                var idStr = Request.Form["id"].ToString();
                
                if (string.IsNullOrEmpty(idStr) || !int.TryParse(idStr, out int formId))
                {
                    return Json(new { success = false, message = "Invalid candidate ID!" });
                }

                Console.WriteLine($"HARD DELETING candidate with ID from form: {formId}");

                var formCandidate = await _context.Candidates.FindAsync(formId);
                if (formCandidate == null)
                {
                    return Json(new { success = false, message = "Candidate not found!" });
                }

                // Delete photo file if exists
                if (!string.IsNullOrEmpty(formCandidate.PhotoUrl))
                {
                    DeleteOldPhoto(formCandidate.PhotoUrl);
                }

                // HARD DELETE - Remove from database completely
                _context.Candidates.Remove(formCandidate);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Candidate deleted successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DeleteCandidate: {ex.Message}");
                return Json(new { success = false, message = "Error deleting candidate: " + ex.Message });
            }
        }

        // File upload helper method
        private async Task<string> HandleFileUpload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return null;

            try
            {
                // Create uploads directory if it doesn't exist
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "images", "candidates");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                // Generate unique file name
                var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Return relative URL for web access
                return $"/images/candidates/{uniqueFileName}";
            }
            catch (Exception ex)
            {
                throw new Exception("Error uploading file: " + ex.Message);
            }
        }

        // Delete old photo file
        private void DeleteOldPhoto(string photoUrl)
        {
            try
            {
                if (!string.IsNullOrEmpty(photoUrl))
                {
                    var fileName = Path.GetFileName(photoUrl);
                    var filePath = Path.Combine(_environment.WebRootPath, "images", "candidates", fileName);
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't throw - we don't want photo deletion to break the main operation
                Console.WriteLine("Error deleting old photo: " + ex.Message);
            }
        }

        #endregion

        #region Manager Management API Methods

        // GET: /Supervisor/GetManagers
        [HttpGet]
        public async Task<JsonResult> GetManagers()
        {
            try
            {
                var managers = await _context.Managers
                    .Select(m => new 
                    { 
                        id = m.Id,
                        firstName = m.FirstName,
                        lastName = m.LastName,
                        email = m.Email,
                        username = m.Username,
                        isActive = m.IsActive,
                        createdAt = m.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                        updatedAt = m.UpdatedAt.ToString("yyyy-MM-dd HH:mm")
                    })
                    .OrderBy(m => m.username) // Sort by username instead of last name
                    .ThenBy(m => m.firstName)
                    .ToListAsync();

                return Json(new { success = true, managers = managers });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: /Supervisor/CreateManager - WITH DEBUG LOGGING
        [HttpPost]
        public async Task<JsonResult> CreateManager()
        {
            try
            {
                var firstName = Request.Form["firstName"].ToString();
                var lastName = Request.Form["lastName"].ToString();
                var email = Request.Form["email"].ToString();
                var username = Request.Form["username"].ToString();
                var password = Request.Form["password"].ToString();

                Console.WriteLine($"=== CREATING MANAGER ===");
                Console.WriteLine($"First Name: {firstName}");
                Console.WriteLine($"Last Name: {lastName}");
                Console.WriteLine($"Email: {email}");
                Console.WriteLine($"Username: {username}");
                Console.WriteLine($"Password: {password}");

                if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) || 
                    string.IsNullOrEmpty(email) || string.IsNullOrEmpty(username) || 
                    string.IsNullOrEmpty(password))
                {
                    return Json(new { success = false, message = "Please fill in all required fields." });
                }

                // Validate email format
                if (!IsValidEmail(email))
                {
                    return Json(new { success = false, message = "Please enter a valid email address." });
                }

                // Check password strength
                if (password.Length < 6)
                {
                    return Json(new { success = false, message = "Password must be at least 6 characters long." });
                }

                // Check if manager with same email or username already exists
                var existingManager = await _context.Managers
                    .FirstOrDefaultAsync(m => m.Email == email || m.Username == username);
                
                if (existingManager != null)
                {
                    return Json(new { success = false, message = "Manager with this email or username already exists!" });
                }

                // Hash the password and log it
                string hashedPassword = HashPassword(password);
                Console.WriteLine($"Password hash: {hashedPassword}");
                Console.WriteLine($"Password hash length: {hashedPassword.Length}");

                var manager = new Manager
                {
                    FirstName = firstName.Trim(),
                    LastName = lastName.Trim(),
                    Email = email.Trim().ToLower(),
                    Username = username.Trim(),
                    Password = hashedPassword, // Store encrypted password
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Managers.Add(manager);
                await _context.SaveChangesAsync();

                Console.WriteLine($"Manager created successfully with ID: {manager.Id}");
                return Json(new { success = true, message = "Manager created successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating manager: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return Json(new { success = false, message = "Error creating manager: " + ex.Message });
            }
        }

        // POST: /Supervisor/UpdateManager
        [HttpPost]
        public async Task<JsonResult> UpdateManager()
        {
            try
            {
                var idStr = Request.Form["id"].ToString();
                var firstName = Request.Form["firstName"].ToString();
                var lastName = Request.Form["lastName"].ToString();
                var email = Request.Form["email"].ToString();
                var username = Request.Form["username"].ToString();
                var password = Request.Form["password"].ToString();
                var isActiveStr = Request.Form["isActive"].ToString();

                if (string.IsNullOrEmpty(idStr) || !int.TryParse(idStr, out int id))
                {
                    return Json(new { success = false, message = "Invalid manager ID!" });
                }

                if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) || 
                    string.IsNullOrEmpty(email) || string.IsNullOrEmpty(username))
                {
                    return Json(new { success = false, message = "Please fill in all required fields." });
                }

                // Validate email format
                if (!IsValidEmail(email))
                {
                    return Json(new { success = false, message = "Please enter a valid email address." });
                }

                var manager = await _context.Managers.FindAsync(id);
                if (manager == null)
                {
                    return Json(new { success = false, message = "Manager not found!" });
                }

                // Check if email or username is being changed and if they already exist
                if (manager.Email != email || manager.Username != username)
                {
                    var existingManager = await _context.Managers
                        .FirstOrDefaultAsync(m => (m.Email == email || m.Username == username) && m.Id != id);
                    
                    if (existingManager != null)
                    {
                        return Json(new { success = false, message = "Another manager with this email or username already exists!" });
                    }
                }

                manager.FirstName = firstName.Trim();
                manager.LastName = lastName.Trim();
                manager.Email = email.Trim().ToLower();
                manager.Username = username.Trim();
                manager.UpdatedAt = DateTime.Now;

                // Update password only if provided
                if (!string.IsNullOrEmpty(password))
                {
                    if (password.Length < 6)
                    {
                        return Json(new { success = false, message = "Password must be at least 6 characters long." });
                    }
                    manager.Password = HashPassword(password);
                }

                // Update IsActive status
                if (!string.IsNullOrEmpty(isActiveStr) && bool.TryParse(isActiveStr, out bool isActive))
                {
                    manager.IsActive = isActive;
                }

                _context.Managers.Update(manager);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Manager updated successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error updating manager: " + ex.Message });
            }
        }

        // POST: /Supervisor/DeleteManager - HARD DELETE (PERMANENT REMOVAL)
        [HttpPost]
        public async Task<JsonResult> DeleteManager(int? id)
        {
            try
            {
                // Try to get ID from URL parameter first
                if (id.HasValue)
                {
                    Console.WriteLine($"HARD DELETING manager with ID from URL: {id.Value}");
                    var manager = await _context.Managers.FindAsync(id.Value);
                    if (manager == null)
                    {
                        return Json(new { success = false, message = "Manager not found!" });
                    }

                    // HARD DELETE - Remove from database completely
                    _context.Managers.Remove(manager);
                    int changes = await _context.SaveChangesAsync();
                    
                    Console.WriteLine($"Manager HARD DELETED successfully. Changes: {changes} rows affected");

                    return Json(new { success = true, message = "Manager deleted successfully!" });
                }

                // If no URL parameter, try form data
                var idStr = Request.Form["id"].ToString();
                
                if (string.IsNullOrEmpty(idStr) || !int.TryParse(idStr, out int formId))
                {
                    return Json(new { success = false, message = "Invalid manager ID!" });
                }

                Console.WriteLine($"HARD DELETING manager with ID from form: {formId}");

                var formManager = await _context.Managers.FindAsync(formId);
                if (formManager == null)
                {
                    return Json(new { success = false, message = "Manager not found!" });
                }

                // HARD DELETE - Remove from database completely
                _context.Managers.Remove(formManager);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Manager deleted successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DeleteManager: {ex.Message}");
                return Json(new { success = false, message = "Error deleting manager: " + ex.Message });
            }
        }

        // POST: /Supervisor/ToggleManagerStatus
        [HttpPost]
        public async Task<JsonResult> ToggleManagerStatus([FromBody] ManagerStatusModel model)
        {
            try
            {
                var manager = await _context.Managers.FindAsync(model.Id);
                if (manager == null)
                {
                    return Json(new { success = false, message = "Manager not found!" });
                }

                manager.IsActive = model.IsActive;
                manager.UpdatedAt = DateTime.Now;

                _context.Managers.Update(manager);
                await _context.SaveChangesAsync();

                var status = model.IsActive ? "activated" : "deactivated";
                return Json(new { success = true, message = $"Manager {status} successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error updating manager status: " + ex.Message });
            }
        }

        #endregion

        #region Manager Login Methods - TRADITIONAL FORM BASED LIKE VOTER REGISTRATION

        // POST: /Supervisor/LoginManager - TRADITIONAL FORM SUBMISSION WITH DEBUG LOGGING
        [HttpPost]
        public async Task<IActionResult> LoginManager(string username, string password)
        {
            try
            {
                Console.WriteLine($"=== LOGIN ATTEMPT ===");
                Console.WriteLine($"Username: {username}");
                Console.WriteLine($"Password: {password}");

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    TempData["ErrorMessage"] = "Please enter both username and password.";
                    return View();
                }

                // Find manager by username (USING USERNAME INSTEAD OF FIRST NAME)
                var manager = await _context.Managers
                    .FirstOrDefaultAsync(m => m.Username == username);

                Console.WriteLine($"Manager found: {manager != null}");

                if (manager == null)
                {
                    Console.WriteLine("Manager not found in database");
                    TempData["ErrorMessage"] = "Invalid username or password.";
                    return View();
                }

                Console.WriteLine($"Manager details - ID: {manager.Id}, Username: {manager.Username}, IsActive: {manager.IsActive}");
                Console.WriteLine($"Stored password hash: {manager.Password}");
                Console.WriteLine($"Stored password length: {manager.Password?.Length}");

                // Check if manager is active
                if (!manager.IsActive)
                {
                    Console.WriteLine("Manager account is inactive");
                    TempData["ErrorMessage"] = "Your account has been deactivated. Please contact administrator.";
                    return View();
                }

                // Hash the provided password and compare with stored hash
                string hashedPassword = HashPassword(password);
                Console.WriteLine($"Provided password hash: {hashedPassword}");
                Console.WriteLine($"Provided password length: {hashedPassword.Length}");
                Console.WriteLine($"Passwords match: {manager.Password == hashedPassword}");

                // Compare hashed passwords
                if (manager.Password != hashedPassword)
                {
                    Console.WriteLine("PASSWORD COMPARISON FAILED!");
                    TempData["ErrorMessage"] = "Invalid username or password.";
                    return View();
                }

                // Login successful
                Console.WriteLine("LOGIN SUCCESSFUL!");
                HttpContext.Session.SetInt32("ManagerId", manager.Id);
                HttpContext.Session.SetString("ManagerUsername", manager.Username);
                HttpContext.Session.SetString("ManagerName", $"{manager.FirstName} {manager.LastName}");
                HttpContext.Session.SetString("ManagerEmail", manager.Email);

                TempData["SuccessMessage"] = "Login successful!";
                return RedirectToAction("Dashboard");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                TempData["ErrorMessage"] = "Error during login: " + ex.Message;
                return View();
            }
        }

        // POST: /Supervisor/LogoutManager
        [HttpPost]
        public async Task<IActionResult> LogoutManager()
        {
            try
            {
                HttpContext.Session.Remove("ManagerId");
                HttpContext.Session.Remove("ManagerUsername");
                HttpContext.Session.Remove("ManagerName");
                HttpContext.Session.Remove("ManagerEmail");

                TempData["SuccessMessage"] = "Logout successful!";
                return RedirectToAction("LoginManager");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error during logout: " + ex.Message;
                return RedirectToAction("Dashboard");
            }
        }

        // GET: /Supervisor/GetCurrentManager
        [HttpGet]
        public JsonResult GetCurrentManager()
        {
            try
            {
                var managerId = HttpContext.Session.GetInt32("ManagerId");
                var username = HttpContext.Session.GetString("ManagerUsername");
                var name = HttpContext.Session.GetString("ManagerName");
                var email = HttpContext.Session.GetString("ManagerEmail");

                if (managerId == null || string.IsNullOrEmpty(username))
                {
                    return Json(new { success = false, message = "No manager logged in" });
                }

                return Json(new { 
                    success = true, 
                    manager = new {
                        id = managerId,
                        username = username,
                        name = name,
                        email = email
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Voter Management Methods

        // GET: Supervisor/VoterList
        public async Task<IActionResult> VoterList()
        {
            var voters = await _context.Voters
                .OrderBy(v => v.Region)
                .ThenBy(v => v.Zone)
                .ThenBy(v => v.LastName)
                .ToListAsync();
            return View(voters);
        }

        // GET: Supervisor/CandidateList
        public async Task<IActionResult> CandidateList()
        {
            var candidates = await _context.Candidates
                .OrderBy(c => c.Position)
                .ThenBy(c => c.Party)
                .ThenBy(c => c.LastName)
                .ToListAsync();
            return View(candidates);
        }

        // GET: Supervisor/ManagerList
        public async Task<IActionResult> ManagerList()
        {
            var managers = await _context.Managers
                .OrderBy(m => m.Username) // Sort by username instead of last name
                .ThenBy(m => m.FirstName)
                .ToListAsync();
            return View(managers);
        }

        // GET: Supervisor/ViewVoter/5
        public async Task<IActionResult> ViewVoter(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var voter = await _context.Voters
                .FirstOrDefaultAsync(m => m.Id == id);
            if (voter == null)
            {
                return NotFound();
            }

            return View(voter);
        }

        // GET: Supervisor/ViewCandidate/5
        public async Task<IActionResult> ViewCandidate(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var candidate = await _context.Candidates
                .FirstOrDefaultAsync(m => m.Id == id);
            if (candidate == null)
            {
                return NotFound();
            }

            return View(candidate);
        }

        // GET: Supervisor/ViewManager/5
        public async Task<IActionResult> ViewManager(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var manager = await _context.Managers
                .FirstOrDefaultAsync(m => m.Id == id);
            if (manager == null)
            {
                return NotFound();
            }

            return View(manager);
        }

        #endregion

        #region Reports and Results

        // GET: Supervisor/Reports
        public async Task<IActionResult> Reports()
        {
            // Get report data
            var voterStats = await _context.Voters
                .GroupBy(v => v.Region)
                .Select(g => new RegionStats 
                { 
                    Region = g.Key, 
                    VoterCount = g.Count(),
                    ActiveVoters = g.Count(v => v.Status == "Active")
                })
                .ToListAsync();

            var candidateStats = await _context.Candidates
                .GroupBy(c => c.Party)
                .Select(g => new PartyStats
                {
                    Party = g.Key,
                    CandidateCount = g.Count()
                })
                .ToListAsync();

            var managerStats = await _context.Managers
                .GroupBy(m => m.IsActive)
                .Select(g => new ManagerStats
                {
                    IsActive = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            ViewBag.VoterStats = voterStats;
            ViewBag.CandidateStats = candidateStats;
            ViewBag.ManagerStats = managerStats;

            return View();
        }

        // GET: Supervisor/ElectionResults
        public async Task<IActionResult> ElectionResults()
        {
            var results = await _context.Candidates
                .Select(c => new ElectionResult
                {
                    CandidateName = $"{c.FirstName} {c.LastName}",
                    Party = c.Party,
                    Position = c.Position,
                    VoteCount = 0 // This would come from Votes table in real implementation
                })
                .ToListAsync();

            return View(results);
        }

        #endregion

        #region Helper Methods

        // Improved password hashing method using SHA256
        private string HashPassword(string password)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(password));

                // Convert byte array to a string
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2")); // x2 for hexadecimal
                }
                return builder.ToString();
            }
        }

        // Email validation helper
        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        // Check if manager is logged in
        private bool IsManagerLoggedIn()
        {
            return HttpContext.Session.GetInt32("ManagerId") != null;
        }

        // Get current manager ID
        private int? GetCurrentManagerId()
        {
            return HttpContext.Session.GetInt32("ManagerId");
        }

        #endregion
    }

    #region Model Classes

    // Model classes for candidate operations
    public class CandidateCreateModel
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Party { get; set; }
        public string Position { get; set; }
        public string Bio { get; set; }
        public string PhotoUrl { get; set; }
    }

    public class CandidateUpdateModel
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Party { get; set; }
        public string Position { get; set; }
        public string Bio { get; set; }
        public string PhotoUrl { get; set; }
    }

    public class CandidateDeleteModel
    {
        public int Id { get; set; }
    }

    // Model classes for manager operations
    public class ManagerCreateModel
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class ManagerUpdateModel
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool IsActive { get; set; }
    }

    public class ManagerDeleteModel
    {
        public int Id { get; set; }
    }

    public class ManagerStatusModel
    {
        public int Id { get; set; }
        public bool IsActive { get; set; }
    }

    public class ManagerLoginModel
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    // Helper classes for reports
    public class RegionStats
    {
        public string Region { get; set; }
        public int VoterCount { get; set; }
        public int ActiveVoters { get; set; }
    }

    public class PartyStats
    {
        public string Party { get; set; }
        public int CandidateCount { get; set; }
    }

    public class ManagerStats
    {
        public bool IsActive { get; set; }
        public int Count { get; set; }
    }

    public class ElectionResult
    {
        public string CandidateName { get; set; }
        public string Party { get; set; }
        public string Position { get; set; }
        public int VoteCount { get; set; }
    }

    #endregion
}