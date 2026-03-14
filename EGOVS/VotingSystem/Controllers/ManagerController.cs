using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VotingSystem.Models;
using VotingSystem.Data;
using VotingSystem.Services;
using VotingSystem.Models.ViewModels;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;

namespace VotingSystem.Controllers
{
    [Authorize(Roles = "Manager")]
    public class DashboardViewModel
    {
        public int TotalVoters { get; set; }
        public int ActiveVoters { get; set; }
        public int PendingAssistance { get; set; }
        public int BiometricRegistered { get; set; }
        public int Regions { get; set; }
        public int TotalManagers { get; set; }
    }

    public class ManagerController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IPasswordHasherService _passwordHasher;
        private readonly IDuplicateDetectionService _duplicateDetection;
        private readonly IIlliterateVoterService _illiterateVoterService;
        private readonly IEncryptionService _encryptionService;
        private readonly ILogger<ManagerController> _logger;

        public ManagerController(
            AppDbContext context,
            IPasswordHasherService passwordHasher,
            IDuplicateDetectionService duplicateDetection,
            IIlliterateVoterService illiterateVoterService,
            IEncryptionService encryptionService,
            ILogger<ManagerController> logger)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _duplicateDetection = duplicateDetection;
            _illiterateVoterService = illiterateVoterService;
            _encryptionService = encryptionService;
            _logger = logger;
        }

        // GET: Manager/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var currentManager = await GetCurrentManagerAsync();
                if (currentManager == null)
                {
                    return RedirectToAction("Login", "Home");
                }

                var dashboardStats = new DashboardViewModel
                {
                    TotalVoters = await _context.Voters.CountAsync(v => v.ManagerId == currentManager.Id),
                    ActiveVoters = await _context.Voters.CountAsync(v => v.Status == "Active" && v.ManagerId == currentManager.Id),
                    PendingAssistance = await _context.VoterAssistance
                        .CountAsync(a => !a.IsApproved && a.Voter.ManagerId == currentManager.Id),
                    BiometricRegistered = await _context.Voters
                        .CountAsync(v => !string.IsNullOrEmpty(v.FaceRecognitionHash) && v.ManagerId == currentManager.Id),
                    Regions = await _context.Voters
                        .Where(v => v.ManagerId == currentManager.Id)
                        .Select(v => v.Region)
                        .Distinct()
                        .CountAsync(),
                    TotalManagers = await _context.Managers.CountAsync(m => m.IsActive)
                };
                
                return View(dashboardStats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading manager dashboard");
                TempData["ErrorMessage"] = "Error loading dashboard";
                return View(new DashboardViewModel());
            }
        }

        // GET: Manager/CreateVoter
        public IActionResult CreateVoter()
        {
            return View();
        }

        // POST: Manager/CreateVoter
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateVoter(Voter voter, string confirmPassword)
        {
            try
            {
                ModelState.Remove("VoterId");
                ModelState.Remove("EncryptedNationalId");
                ModelState.Remove("IV");
                ModelState.Remove("FaceRecognitionHash");
                ModelState.Remove("PasswordSalt");
                ModelState.Remove("ManagerId");
                ModelState.Remove("CreatedAt");
                ModelState.Remove("UpdatedAt");

                if (ModelState.IsValid)
                {
                    // Check password confirmation
                    if (voter.Password != confirmPassword)
                    {
                        TempData["ErrorMessage"] = "Passwords do not match.";
                        return View(voter);
                    }

                    // Check for duplicate voter
                    var (isDuplicate, duplicateMessage) = await _duplicateDetection.CheckDuplicateVoterAsync(voter);
                    if (isDuplicate)
                    {
                        TempData["ErrorMessage"] = duplicateMessage;
                        return View(voter);
                    }

                    var currentManager = await GetCurrentManagerAsync();
                    if (currentManager == null)
                    {
                        TempData["ErrorMessage"] = "Manager not found.";
                        return View(voter);
                    }

                    // Set default values
                    voter.CreatedAt = DateTime.Now;
                    voter.UpdatedAt = DateTime.Now;
                    voter.ManagerId = currentManager.Id;
                    voter.IsVerified = true;
                    voter.Status = "Active";

                    // Hash the password with salt
                    var (hashedPassword, salt) = _passwordHasher.HashPassword(voter.Password);
                    voter.Password = hashedPassword;
                    voter.PasswordSalt = salt;

                    // Encrypt national ID
                    if (!string.IsNullOrEmpty(voter.NationalId))
                    {
                        var (encryptedData, iv) = _encryptionService.EncryptNationalId(voter.NationalId);
                        voter.EncryptedNationalId = encryptedData;
                        voter.IV = iv;
                    }

                    _context.Voters.Add(voter);
                    await _context.SaveChangesAsync();

                    // Log the action
                    await LogAudit("Manager", currentManager.Id, currentManager.ManagerId, 
                        "VoterCreated", $"Created voter {voter.VoterId}");

                    TempData["SuccessMessage"] = $"Voter {voter.FirstName} {voter.LastName} created successfully with ID: {voter.VoterId}!";
                    return RedirectToAction(nameof(VoterList));
                }

                TempData["ErrorMessage"] = "Please correct the errors below.";
                return View(voter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating voter");
                TempData["ErrorMessage"] = "An error occurred while creating the voter. Please try again.";
                return View(voter);
            }
        }

        // GET: Manager/VoterList
        public async Task<IActionResult> VoterList()
        {
            try
            {
                var currentManager = await GetCurrentManagerAsync();
                if (currentManager == null)
                {
                    return RedirectToAction("Login", "Home");
                }

                var voters = await _context.Voters
                    .Where(v => v.ManagerId == currentManager.Id)
                    .OrderByDescending(v => v.CreatedAt)
                    .Select(v => new VoterListViewModel
                    {
                        Id = v.Id,
                        VoterId = v.VoterId,
                        FirstName = v.FirstName,
                        LastName = v.LastName,
                        Age = v.Age,
                        Region = v.Region,
                        Kebele = v.Kebele,
                        Status = v.Status,
                        IsVerified = v.IsVerified,
                        HasBiometric = !string.IsNullOrEmpty(v.FaceRecognitionHash),
                        IsIlliterate = v.IsIlliterate,
                        RequiresAssistance = v.RequiresAssistance,
                        CreatedAt = v.CreatedAt
                    })
                    .ToListAsync();

                return View(voters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading voter list");
                TempData["ErrorMessage"] = "Error loading voter list";
                return View(new List<VoterListViewModel>());
            }
        }

        // GET: Manager/ViewVoter/5
        public async Task<IActionResult> ViewVoter(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return NotFound();
                }

                var voter = await _context.Voters
                    .FirstOrDefaultAsync(v => v.VoterId == id);

                if (voter == null)
                {
                    return NotFound();
                }

                // Check if current manager owns this voter
                var currentManager = await GetCurrentManagerAsync();
                if (voter.ManagerId != currentManager?.Id)
                {
                    TempData["ErrorMessage"] = "Access denied.";
                    return RedirectToAction("VoterList");
                }

                return View(voter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error viewing voter: {VoterId}", id);
                TempData["ErrorMessage"] = "Error loading voter details";
                return RedirectToAction("VoterList");
            }
        }

        // GET: Manager/EditVoter/5
        public async Task<IActionResult> EditVoter(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return NotFound();
                }

                var voter = await _context.Voters
                    .FirstOrDefaultAsync(v => v.VoterId == id);

                if (voter == null)
                {
                    return NotFound();
                }

                // Check if current manager owns this voter
                var currentManager = await GetCurrentManagerAsync();
                if (voter.ManagerId != currentManager?.Id)
                {
                    TempData["ErrorMessage"] = "Access denied.";
                    return RedirectToAction("VoterList");
                }

                return View(voter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading edit voter: {VoterId}", id);
                TempData["ErrorMessage"] = "Error loading voter for editing";
                return RedirectToAction("VoterList");
            }
        }

        // POST: Manager/EditVoter/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditVoter(string id, Voter voter, string newPassword)
        {
            try
            {
                if (id != voter.VoterId)
                {
                    return NotFound();
                }

                ModelState.Remove("Password");
                ModelState.Remove("PasswordSalt");
                ModelState.Remove("EncryptedNationalId");
                ModelState.Remove("IV");
                ModelState.Remove("ManagerId");
                ModelState.Remove("CreatedAt");

                if (ModelState.IsValid)
                {
                    var existingVoter = await _context.Voters
                        .FirstOrDefaultAsync(v => v.VoterId == id);

                    if (existingVoter == null)
                    {
                        TempData["ErrorMessage"] = "Voter not found.";
                        return View(voter);
                    }

                    // Check if current manager owns this voter
                    var currentManager = await GetCurrentManagerAsync();
                    if (existingVoter.ManagerId != currentManager?.Id)
                    {
                        TempData["ErrorMessage"] = "Access denied.";
                        return RedirectToAction("VoterList");
                    }

                    // Update voter properties
                    existingVoter.FirstName = voter.FirstName;
                    existingVoter.LastName = voter.LastName;
                    existingVoter.Age = voter.Age;
                    existingVoter.Sex = voter.Sex;
                    existingVoter.Nationality = voter.Nationality;
                    existingVoter.Region = voter.Region;
                    existingVoter.Zone = voter.Zone;
                    existingVoter.Woreda = voter.Woreda;
                    existingVoter.Kebele = voter.Kebele;
                    existingVoter.Status = voter.Status;
                    existingVoter.IsIlliterate = voter.IsIlliterate;
                    existingVoter.RequiresAssistance = voter.RequiresAssistance;
                    existingVoter.UpdatedAt = DateTime.Now;

                    // Update password if provided
                    if (!string.IsNullOrEmpty(newPassword))
                    {
                        var (hashedPassword, salt) = _passwordHasher.HashPassword(newPassword);
                        existingVoter.Password = hashedPassword;
                        existingVoter.PasswordSalt = salt;
                    }

                    await _context.SaveChangesAsync();

                    // Log the action
                    await LogAudit("Manager", currentManager.Id, currentManager.ManagerId, 
                        "VoterUpdated", $"Updated voter {voter.VoterId}");

                    TempData["SuccessMessage"] = $"Voter {voter.FirstName} {voter.LastName} updated successfully!";
                    return RedirectToAction(nameof(VoterList));
                }

                TempData["ErrorMessage"] = "Please correct the errors below.";
                return View(voter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating voter: {VoterId}", id);
                TempData["ErrorMessage"] = "An error occurred while updating the voter.";
                return View(voter);
            }
        }

        // GET: Manager/UpdateVoter
        public IActionResult UpdateVoter()
        {
            return View();
        }

        // GET: Manager/DeleteVoter
        public IActionResult DeleteVoter()
        {
            return View();
        }

        // POST: Manager/DeleteVoter
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteVoter(string voterId)
        {
            try
            {
                if (string.IsNullOrEmpty(voterId))
                {
                    TempData["ErrorMessage"] = "Voter ID is required.";
                    return RedirectToAction("DeleteVoter");
                }

                var voter = await _context.Voters
                    .FirstOrDefaultAsync(v => v.VoterId == voterId);

                if (voter == null)
                {
                    TempData["ErrorMessage"] = "Voter not found.";
                    return RedirectToAction("DeleteVoter");
                }

                // Check if current manager owns this voter
                var currentManager = await GetCurrentManagerAsync();
                if (voter.ManagerId != currentManager?.Id)
                {
                    TempData["ErrorMessage"] = "Access denied. You can only delete voters under your management.";
                    return RedirectToAction("DeleteVoter");
                }

                var voterName = $"{voter.FirstName} {voter.LastName}";
                
                // Remove related records first
                var assistanceRequests = await _context.VoterAssistance
                    .Where(a => a.VoterId == voter.Id)
                    .ToListAsync();
                _context.VoterAssistance.RemoveRange(assistanceRequests);

                var biometrics = await _context.VoterBiometrics
                    .Where(b => b.VoterId == voter.Id)
                    .ToListAsync();
                _context.VoterBiometrics.RemoveRange(biometrics);

                // Remove the voter
                _context.Voters.Remove(voter);
                await _context.SaveChangesAsync();

                // Log the action
                await LogAudit("Manager", currentManager.Id, currentManager.ManagerId, 
                    "VoterDeleted", $"Deleted voter {voterId}");

                TempData["SuccessMessage"] = $"Voter {voterName} (ID: {voterId}) has been permanently deleted.";
                return RedirectToAction("DeleteVoter");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting voter: {VoterId}", voterId);
                TempData["ErrorMessage"] = "An error occurred while deleting the voter.";
                return RedirectToAction("DeleteVoter");
            }
        }

        // GET: Manager/RegisterIlliterateVoter
        public IActionResult RegisterIlliterateVoter()
        {
            return View();
        }

       // POST: Manager/RegisterIlliterateVoter
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> RegisterIlliterateVoter(IlliterateVoterViewModel model)
{
    try
    {
        if (ModelState.IsValid)
        {
            var voter = await _context.Voters
                .FirstOrDefaultAsync(v => v.VoterId == model.VoterId);

            if (voter == null)
            {
                TempData["ErrorMessage"] = "Voter not found.";
                return View(model);
            }

            // Check if current manager owns this voter
            var currentManager = await GetCurrentManagerAsync();
            if (voter.ManagerId != currentManager?.Id)
            {
                TempData["ErrorMessage"] = "Access denied.";
                return View(model);
            }

            // Create assistance request
            var assistance = new VoterAssistance
            {
                VoterId = voter.Id,
                AssistantName = model.AssistantName,
                AssistantRelationship = model.AssistantRelationship,
                AssistanceType = model.AssistanceType,
                Reason = model.Reason, // Add this line
                ApprovedByManagerId = currentManager.Id,
                IsApproved = true, // Auto-approve for now, can be changed
                CreatedAt = DateTime.Now
            };

            // Update voter record
            voter.IsIlliterate = true;
            voter.RequiresAssistance = true;
            voter.UpdatedAt = DateTime.Now;

            _context.VoterAssistance.Add(assistance);
            await _context.SaveChangesAsync();

            // Log the action
            await LogAudit("Manager", currentManager.Id, currentManager.ManagerId, 
                "IlliterateVoterRegistered", $"Registered illiterate voter {model.VoterId}");

            TempData["SuccessMessage"] = $"Illiterate voter registration for {voter.FirstName} {voter.LastName} completed successfully!";
            return RedirectToAction("VoterList");
        }

        TempData["ErrorMessage"] = "Please correct the errors below.";
        return View(model);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error registering illiterate voter: {VoterId}", model.VoterId);
        TempData["ErrorMessage"] = "An error occurred while registering the illiterate voter.";
        return View(model);
    }
}

                    // Create assistance request
                    var assistance = new VoterAssistance
                    {
                        VoterId = voter.Id,
                        AssistantName = model.AssistantName,
                        AssistantRelationship = model.AssistantRelationship,
                        AssistanceType = model.AssistanceType,
                        ApprovedByManagerId = currentManager.Id,
                        IsApproved = true, // Auto-approve for now, can be changed
                        CreatedAt = DateTime.Now
                    };

                    // Update voter record
                    voter.IsIlliterate = true;
                    voter.RequiresAssistance = true;
                    voter.UpdatedAt = DateTime.Now;

                    _context.VoterAssistance.Add(assistance);
                    await _context.SaveChangesAsync();

                    // Log the action
                    await LogAudit("Manager", currentManager.Id, currentManager.ManagerId, 
                        "IlliterateVoterRegistered", $"Registered illiterate voter {model.VoterId}");

                    TempData["SuccessMessage"] = $"Illiterate voter registration for {voter.FirstName} {voter.LastName} completed successfully!";
                    return RedirectToAction("VoterList");
                }

                TempData["ErrorMessage"] = "Please correct the errors below.";
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering illiterate voter: {VoterId}", model.VoterId);
                TempData["ErrorMessage"] = "An error occurred while registering the illiterate voter.";
                return View(model);
            }
        }

        // GET: Manager/ApproveAssistance
        public async Task<IActionResult> ApproveAssistance()
        {
            try
            {
                var currentManager = await GetCurrentManagerAsync();
                if (currentManager == null)
                {
                    return RedirectToAction("Login", "Home");
                }

                var pendingAssistance = await _context.VoterAssistance
                    .Include(a => a.Voter)
                    .Where(a => !a.IsApproved && a.Voter.ManagerId == currentManager.Id)
                    .Select(a => new AssistanceApprovalViewModel
                    {
                        AssistanceId = a.Id,
                        VoterName = $"{a.Voter.FirstName} {a.Voter.LastName}",
                        VoterId = a.Voter.VoterId,
                        AssistantName = a.AssistantName,
                        Relationship = a.AssistantRelationship,
                        AssistanceType = a.AssistanceType,
                        RequestDate = a.CreatedAt
                    })
                    .ToListAsync();

                return View(pendingAssistance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading assistance approval page");
                TempData["ErrorMessage"] = "Error loading assistance requests";
                return View(new List<AssistanceApprovalViewModel>());
            }
        }

        // POST: Manager/ApproveAssistance
        [HttpPost]
        public async Task<IActionResult> ApproveAssistance(int assistanceId)
        {
            try
            {
                var currentManager = await GetCurrentManagerAsync();
                var success = await _illiterateVoterService.ApproveAssistanceAsync(assistanceId, currentManager.Id);
                
                if (success)
                {
                    await LogAudit("Manager", currentManager.Id, currentManager.ManagerId, 
                        "AssistanceApproved", $"Approved assistance request {assistanceId}");
                    
                    return Json(new { success = true, message = "Assistance request approved successfully!" });
                }
                else
                {
                    return Json(new { success = false, message = "Failed to approve assistance request." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving assistance request: {AssistanceId}", assistanceId);
                return Json(new { success = false, message = "Error approving request" });
            }
        }

        // POST: Manager/RejectAssistance
        [HttpPost]
        public async Task<IActionResult> RejectAssistance(int assistanceId)
        {
            try
            {
                var assistance = await _context.VoterAssistance
                    .Include(a => a.Voter)
                    .FirstOrDefaultAsync(a => a.Id == assistanceId);

                if (assistance != null)
                {
                    // Check if current manager owns this voter
                    var currentManager = await GetCurrentManagerAsync();
                    if (assistance.Voter.ManagerId != currentManager?.Id)
                    {
                        return Json(new { success = false, message = "Access denied." });
                    }

                    _context.VoterAssistance.Remove(assistance);
                    await _context.SaveChangesAsync();
                    
                    await LogAudit("Manager", currentManager.Id, currentManager.ManagerId, 
                        "AssistanceRejected", $"Rejected assistance request {assistanceId}");
                    
                    return Json(new { success = true, message = "Assistance request rejected successfully!" });
                }
                return Json(new { success = false, message = "Assistance request not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting assistance request: {AssistanceId}", assistanceId);
                return Json(new { success = false, message = "Error rejecting request" });
            }
        }

        // GET: Manager/GetAssistanceDetails
[HttpGet]
public async Task<IActionResult> GetAssistanceDetails(int id)
{
    try
    {
        var assistance = await _context.VoterAssistance
            .Include(a => a.Voter)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (assistance == null)
        {
            return Json(new { success = false, message = "Assistance request not found" });
        }

        var details = new
        {
            voterName = $"{assistance.Voter.FirstName} {assistance.Voter.LastName}",
            voterId = assistance.Voter.VoterId,
            region = assistance.Voter.Region,
            zone = assistance.Voter.Zone,
            kebele = assistance.Voter.Kebele,
            age = assistance.Voter.Age,
            assistantName = assistance.AssistantName,
            relationship = assistance.AssistantRelationship,
            assistanceType = assistance.AssistanceType,
            requestDate = assistance.CreatedAt,
            reason = assistance.Reason // This line was causing the error
        };

        return Json(new { success = true, 
            voterName = details.voterName,
            voterId = details.voterId,
            region = details.region,
            zone = details.zone,
            kebele = details.kebele,
            age = details.age,
            assistantName = details.assistantName,
            relationship = details.relationship,
            assistanceType = details.assistanceType,
            requestDate = details.requestDate,
            reason = details.reason
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error getting assistance details: {AssistanceId}", id);
        return Json(new { success = false, message = "Error loading details" });
    }
}

        // GET: Manager/VerifyVoter
        [HttpGet]
        public async Task<IActionResult> VerifyVoter(string id)
        {
            try
            {
                var voter = await _context.Voters
                    .FirstOrDefaultAsync(v => v.VoterId == id);

                if (voter == null)
                {
                    return Json(new { success = false, message = "Voter not found" });
                }

                // Check if current manager owns this voter
                var currentManager = await GetCurrentManagerAsync();
                if (voter.ManagerId != currentManager?.Id)
                {
                    return Json(new { success = false, message = "Access denied" });
                }

                var voterInfo = new
                {
                    id = voter.Id,
                    voterId = voter.VoterId,
                    firstName = voter.FirstName,
                    lastName = voter.LastName,
                    region = voter.Region,
                    age = voter.Age,
                    isVerified = voter.IsVerified,
                    hasBiometric = !string.IsNullOrEmpty(voter.FaceRecognitionHash)
                };

                return Json(new { success = true, voter = voterInfo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying voter: {VoterId}", id);
                return Json(new { success = false, message = "Error verifying voter" });
            }
        }

        // GET: Manager/VerifyVoterForBiometric
        [HttpGet]
        public async Task<IActionResult> VerifyVoterForBiometric(string id)
        {
            try
            {
                var voter = await _context.Voters
                    .FirstOrDefaultAsync(v => v.VoterId == id);

                if (voter == null)
                {
                    return Json(new { success = false, message = "Voter not found" });
                }

                // Check if current manager owns this voter
                var currentManager = await GetCurrentManagerAsync();
                if (voter.ManagerId != currentManager?.Id)
                {
                    return Json(new { success = false, message = "Access denied" });
                }

                var voterInfo = new
                {
                    id = voter.Id,
                    voterId = voter.VoterId,
                    firstName = voter.FirstName,
                    lastName = voter.LastName,
                    region = voter.Region,
                    age = voter.Age,
                    isVerified = voter.IsVerified,
                    hasBiometric = !string.IsNullOrEmpty(voter.FaceRecognitionHash)
                };

                return Json(new { success = true, voter = voterInfo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying voter for biometric: {VoterId}", id);
                return Json(new { success = false, message = "Error verifying voter" });
            }
        }

        // Search voter by Voter ID
        [HttpGet]
        public async Task<IActionResult> SearchVoterById(string voterId)
        {
            try
            {
                if (string.IsNullOrEmpty(voterId))
                {
                    return Json(null);
                }

                var voter = await _context.Voters
                    .FirstOrDefaultAsync(v => v.VoterId == voterId);

                if (voter != null)
                {
                    // Check if current manager owns this voter
                    var currentManager = await GetCurrentManagerAsync();
                    if (voter.ManagerId != currentManager?.Id)
                    {
                        return Json(new { error = "Access denied" });
                    }

                    return Json(new 
                    {
                        id = voter.Id,
                        voterId = voter.VoterId,
                        firstName = voter.FirstName,
                        lastName = voter.LastName,
                        age = voter.Age,
                        sex = voter.Sex,
                        nationality = voter.Nationality,
                        region = voter.Region,
                        zone = voter.Zone,
                        woreda = voter.Woreda,
                        kebele = voter.Kebele,
                        status = voter.Status,
                        isIlliterate = voter.IsIlliterate,
                        requiresAssistance = voter.RequiresAssistance,
                        hasBiometric = !string.IsNullOrEmpty(voter.FaceRecognitionHash),
                        createdAt = voter.CreatedAt.ToString("yyyy-MM-dd")
                    });
                }

                return Json(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching voter by ID: {VoterId}", voterId);
                return Json(new { error = "Search error occurred" });
            }
        }

        // Helper method to get current manager
        private async Task<Manager> GetCurrentManagerAsync()
        {
            var username = User.Identity.Name;
            if (string.IsNullOrEmpty(username))
            {
                return null;
            }

            return await _context.Managers
                .FirstOrDefaultAsync(m => m.Username == username && m.IsActive);
        }

        // Helper method to log audit trail
        private async Task LogAudit(string userType, int userId, string userCode, string action, string description)
        {
            try
            {
                var auditLog = new AuditLog
                {
                    UserType = userType,
                    UserId = userId,
                    UserCode = userCode,
                    Action = action,
                    Description = description,
                    IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Timestamp = DateTime.Now
                };

                _context.AuditLog.Add(auditLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging audit trail");
            }
        }

        private bool VoterExists(int id)
        {
            return _context.Voters.Any(e => e.Id == id);
        }
    }

    // View Models
    public class VoterListViewModel
    {
        public int Id { get; set; }
        public string VoterId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int Age { get; set; }
        public string Region { get; set; }
        public string Kebele { get; set; }
        public string Status { get; set; }
        public bool IsVerified { get; set; }
        public bool HasBiometric { get; set; }
        public bool IsIlliterate { get; set; }
        public bool RequiresAssistance { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}