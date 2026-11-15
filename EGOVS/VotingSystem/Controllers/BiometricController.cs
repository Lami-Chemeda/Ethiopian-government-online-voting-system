using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using VotingSystem.Services;
using VotingSystem.Models;
using VotingSystem.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using VotingSystem.Data;

namespace VotingSystem.Controllers
{
    [Authorize]
    public class BiometricController : Controller
    {
        private readonly IFaceRecognitionService _faceRecognitionService;
        private readonly IBiometricService _biometricService;
        private readonly IDuplicateDetectionService _duplicateDetectionService;
        private readonly IIlliterateVoterService _illiterateVoterService;
        private readonly IEncryptionService _encryptionService;
        private readonly AppDbContext _context;
        private readonly ILogger<BiometricController> _logger;

        public BiometricController(
            IFaceRecognitionService faceRecognitionService,
            IBiometricService biometricService,
            IDuplicateDetectionService duplicateDetectionService,
            IIlliterateVoterService illiterateVoterService,
            IEncryptionService encryptionService,
            AppDbContext context,
            ILogger<BiometricController> logger)
        {
            _faceRecognitionService = faceRecognitionService;
            _biometricService = biometricService;
            _duplicateDetectionService = duplicateDetectionService;
            _illiterateVoterService = illiterateVoterService;
            _encryptionService = encryptionService;
            _context = context;
            _logger = logger;
        }

        // GET: Biometric/Register
        [HttpGet]
        public IActionResult Register()
        {
            try
            {
                var model = new BiometricRegistrationViewModel();
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading biometric registration page");
                TempData["ErrorMessage"] = "Error loading biometric registration page";
                return RedirectToAction("Dashboard", "Manager");
            }
        }

        // POST: Biometric/ProcessFace
        [HttpPost]
        public async Task<IActionResult> ProcessFace([FromBody] FaceProcessRequest request)
        {
            try
            {
                _logger.LogInformation("Processing face registration for voter: {VoterId}", request.VoterId);

                if (string.IsNullOrEmpty(request.ImageData))
                {
                    return Json(new { success = false, message = "No image data provided" });
                }

                // Generate face encoding and hash
                var faceEncoding = await _faceRecognitionService.GenerateFaceEncodingAsync(request.ImageData);
                var faceHash = _faceRecognitionService.GenerateFaceHash(request.ImageData);

                // Check for duplicates
                var isDuplicate = await _duplicateDetectionService.CheckFaceRecognitionDuplicateAsync(faceEncoding);
                if (isDuplicate)
                {
                    _logger.LogWarning("Duplicate face detected for voter: {VoterId}", request.VoterId);
                    
                    // Get duplicate voter information
                    var duplicateVoter = await _context.Voters
                        .FirstOrDefaultAsync(v => v.FaceRecognitionHash == faceEncoding);
                    
                    var duplicateInfo = duplicateVoter != null ? new
                    {
                        voterId = duplicateVoter.VoterId,
                        voterName = $"{duplicateVoter.FirstName} {duplicateVoter.LastName}",
                        region = duplicateVoter.Region
                    } : null;

                    return Json(new 
                    { 
                        success = false, 
                        isDuplicate = true,
                        duplicateInfo = duplicateInfo,
                        message = "This face is already registered in the system. Duplicate registration prevented." 
                    });
                }

                // Register biometric if voter ID is provided
                if (!string.IsNullOrEmpty(request.VoterId) && int.TryParse(request.VoterId, out int voterId))
                {
                    var registrationResult = await _biometricService.RegisterFaceBiometricAsync(voterId, faceEncoding);
                    if (!registrationResult)
                    {
                        return Json(new { success = false, message = "Failed to register biometric data" });
                    }

                    _logger.LogInformation("Biometric registration successful for voter ID: {VoterId}", voterId);
                }

                return Json(new 
                { 
                    success = true, 
                    faceHash = faceHash,
                    faceEncoding = faceEncoding,
                    message = "Face processed successfully",
                    redirectUrl = Url.Action("Dashboard", "Voter")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing face image for voter: {VoterId}", request.VoterId);
                return Json(new { success = false, message = $"Error processing face: {ex.Message}" });
            }
        }

        // POST: Biometric/VerifyFace
        [HttpPost]
        public async Task<IActionResult> VerifyFace([FromBody] FaceVerifyRequest request)
        {
            try
            {
                _logger.LogInformation("Face verification request for type: {VerificationType}", request.VerificationType);

                if (string.IsNullOrEmpty(request.ImageData))
                {
                    return Json(new { success = false, message = "No image data provided" });
                }

                // Generate face encoding for verification
                var faceEncoding = await _faceRecognitionService.GenerateFaceEncodingAsync(request.ImageData);
                
                // Find voter with matching face encoding
                var voter = await _context.Voters
                    .FirstOrDefaultAsync(v => v.FaceRecognitionHash == faceEncoding);

                if (voter != null)
                {
                    // Calculate confidence score
                    var confidence = await _faceRecognitionService.CompareFacesAsync(faceEncoding, voter.FaceRecognitionHash);
                    
                    var verificationResult = new
                    {
                        success = true,
                        confidence = confidence,
                        isMatch = confidence > 70.0, // 70% confidence threshold
                        voterId = voter.VoterId,
                        voterName = $"{voter.FirstName} {voter.LastName}",
                        message = confidence > 70.0 ? "Face verification successful" : "Low confidence match"
                    };

                    _logger.LogInformation("Face verification successful for voter: {VoterId} with confidence: {Confidence}", 
                        voter.VoterId, confidence);

                    return Json(verificationResult);
                }
                else
                {
                    _logger.LogWarning("No matching face found in database");
                    return Json(new 
                    { 
                        success = false, 
                        confidence = 0.0,
                        isMatch = false,
                        message = "No matching face found in our system" 
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during face verification");
                return Json(new { success = false, message = $"Error during verification: {ex.Message}" });
            }
        }

        // POST: Biometric/CheckDuplicate
        [HttpPost]
        public async Task<IActionResult> CheckDuplicate([FromBody] FaceCheckRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.ImageData))
                {
                    return Json(new { isDuplicate = false, message = "No image data provided" });
                }

                var faceEncoding = await _faceRecognitionService.GenerateFaceEncodingAsync(request.ImageData);
                var isDuplicate = await _faceRecognitionService.IsFaceDuplicateAsync(faceEncoding);
                
                if (isDuplicate)
                {
                    var duplicateVoter = await _context.Voters
                        .FirstOrDefaultAsync(v => v.FaceRecognitionHash == faceEncoding);
                    
                    var duplicateInfo = duplicateVoter != null ? new
                    {
                        voterId = duplicateVoter.VoterId,
                        voterName = $"{duplicateVoter.FirstName} {duplicateVoter.LastName}",
                        region = duplicateVoter.Region
                    } : null;

                    return Json(new 
                    { 
                        isDuplicate = true,
                        duplicateInfo = duplicateInfo,
                        message = "Duplicate face detected in system" 
                    });
                }

                return Json(new { 
                    isDuplicate = false,
                    message = "No duplicate face found" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for duplicate face");
                return Json(new { isDuplicate = false, message = $"Error checking duplicate: {ex.Message}" });
            }
        }

        // GET: Biometric/Verify
        [HttpGet]
        public IActionResult Verify()
        {
            try
            {
                var model = new FaceVerificationViewModel();
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading face verification page");
                TempData["ErrorMessage"] = "Error loading verification page";
                return RedirectToAction("Index", "Home");
            }
        }

        // POST: Biometric/VerifyVoter
        [HttpPost]
        public async Task<IActionResult> VerifyVoter([FromBody] VoterVerificationRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.VoterId) || string.IsNullOrEmpty(request.ImageData))
                {
                    return Json(new { success = false, message = "Voter ID and image data are required" });
                }

                // Find voter by ID
                var voter = await _context.Voters
                    .FirstOrDefaultAsync(v => v.VoterId == request.VoterId);

                if (voter == null)
                {
                    return Json(new { success = false, message = "Voter not found" });
                }

                // Verify face
                var faceEncoding = await _faceRecognitionService.GenerateFaceEncodingAsync(request.ImageData);
                var isMatch = voter.FaceRecognitionHash == faceEncoding;

                if (isMatch)
                {
                    _logger.LogInformation("Voter verification successful for: {VoterId}", request.VoterId);
                    return Json(new 
                    { 
                        success = true, 
                        message = "Voter verification successful",
                        voter = new
                        {
                            id = voter.Id,
                            voterId = voter.VoterId,
                            name = $"{voter.FirstName} {voter.LastName}",
                            region = voter.Region,
                            isVerified = voter.IsVerified
                        }
                    });
                }
                else
                {
                    _logger.LogWarning("Face verification failed for voter: {VoterId}", request.VoterId);
                    return Json(new { success = false, message = "Face verification failed" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during voter verification for: {VoterId}", request.VoterId);
                return Json(new { success = false, message = $"Verification error: {ex.Message}" });
            }
        }

        // GET: Biometric/GetVoterBiometricStatus/{voterId}
        [HttpGet]
        public async Task<IActionResult> GetVoterBiometricStatus(string voterId)
        {
            try
            {
                var voter = await _context.Voters
                    .FirstOrDefaultAsync(v => v.VoterId == voterId);

                if (voter == null)
                {
                    return Json(new { success = false, message = "Voter not found" });
                }

                var biometricStatus = new
                {
                    hasBiometric = !string.IsNullOrEmpty(voter.FaceRecognitionHash),
                    faceRegistered = !string.IsNullOrEmpty(voter.FaceRecognitionHash),
                    registrationDate = voter.UpdatedAt,
                    isVerified = voter.IsVerified
                };

                return Json(new { success = true, status = biometricStatus });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting biometric status for voter: {VoterId}", voterId);
                return Json(new { success = false, message = "Error retrieving biometric status" });
            }
        }

        // POST: Biometric/UpdateBiometric
        [HttpPost]
        public async Task<IActionResult> UpdateBiometric([FromBody] UpdateBiometricRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.VoterId) || string.IsNullOrEmpty(request.ImageData))
                {
                    return Json(new { success = false, message = "Voter ID and image data are required" });
                }

                var voter = await _context.Voters
                    .FirstOrDefaultAsync(v => v.VoterId == request.VoterId);

                if (voter == null)
                {
                    return Json(new { success = false, message = "Voter not found" });
                }

                // Generate new face encoding
                var faceEncoding = await _faceRecognitionService.GenerateFaceEncodingAsync(request.ImageData);

                // Check if this new face is a duplicate of another voter
                var isDuplicate = await _context.Voters
                    .AnyAsync(v => v.FaceRecognitionHash == faceEncoding && v.VoterId != request.VoterId);

                if (isDuplicate)
                {
                    return Json(new { success = false, message = "This face is already registered for another voter" });
                }

                // Update voter's face recognition data
                voter.FaceRecognitionHash = faceEncoding;
                voter.UpdatedAt = DateTime.Now;

                // Update biometrics table
                var biometric = await _context.VoterBiometrics
                    .FirstOrDefaultAsync(b => b.VoterId == voter.Id);

                if (biometric != null)
                {
                    biometric.FaceEncodingHash = faceEncoding;
                    biometric.CreatedAt = DateTime.Now;
                }
                else
                {
                    var newBiometric = new VoterBiometric
                    {
                        VoterId = voter.Id,
                        FaceEncodingHash = faceEncoding,
                        CreatedAt = DateTime.Now
                    };
                    _context.VoterBiometrics.Add(newBiometric);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Biometric data updated successfully for voter: {VoterId}", request.VoterId);
                return Json(new { success = true, message = "Biometric data updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating biometric data for voter: {VoterId}", request.VoterId);
                return Json(new { success = false, message = $"Error updating biometric data: {ex.Message}" });
            }
        }

        // GET: Biometric/Stats
        [HttpGet]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Stats()
        {
            try
            {
                var totalVoters = await _context.Voters.CountAsync();
                var votersWithBiometric = await _context.Voters
                    .CountAsync(v => !string.IsNullOrEmpty(v.FaceRecognitionHash));
                
                var duplicateAttempts = await _context.AuditLog
                    .CountAsync(a => a.Action == "DuplicateFaceDetection");
                
                var recentRegistrations = await _context.Voters
                    .Where(v => !string.IsNullOrEmpty(v.FaceRecognitionHash))
                    .OrderByDescending(v => v.UpdatedAt)
                    .Take(10)
                    .Select(v => new
                    {
                        v.VoterId,
                        Name = $"{v.FirstName} {v.LastName}",
                        v.Region,
                        v.UpdatedAt
                    })
                    .ToListAsync();

                var stats = new
                {
                    TotalVoters = totalVoters,
                    VotersWithBiometric = votersWithBiometric,
                    BiometricRegistrationRate = totalVoters > 0 ? (votersWithBiometric * 100.0 / totalVoters) : 0,
                    DuplicateAttempts = duplicateAttempts,
                    RecentRegistrations = recentRegistrations
                };

                return View(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading biometric statistics");
                TempData["ErrorMessage"] = "Error loading statistics";
                return RedirectToAction("Dashboard", "Admin");
            }
        }

        // POST: Biometric/BulkVerify
        [HttpPost]
        [Authorize(Roles = "Admin,Supervisor")]
        public async Task<IActionResult> BulkVerify([FromBody] BulkVerifyRequest request)
        {
            try
            {
                if (request.VoterIds == null || !request.VoterIds.Any())
                {
                    return Json(new { success = false, message = "No voter IDs provided" });
                }

                var results = new List<BulkVerifyResult>();

                foreach (var voterId in request.VoterIds)
                {
                    var voter = await _context.Voters
                        .FirstOrDefaultAsync(v => v.VoterId == voterId);

                    if (voter != null)
                    {
                        results.Add(new BulkVerifyResult
                        {
                            VoterId = voterId,
                            HasBiometric = !string.IsNullOrEmpty(voter.FaceRecognitionHash),
                            IsVerified = voter.IsVerified,
                            Name = $"{voter.FirstName} {voter.LastName}",
                            Region = voter.Region
                        });
                    }
                    else
                    {
                        results.Add(new BulkVerifyResult
                        {
                            VoterId = voterId,
                            HasBiometric = false,
                            IsVerified = false,
                            Name = "Not Found",
                            Region = "N/A",
                            Error = "Voter not found"
                        });
                    }
                }

                return Json(new 
                { 
                    success = true, 
                    results = results,
                    summary = new
                    {
                        total = results.Count,
                        withBiometric = results.Count(r => r.HasBiometric),
                        verified = results.Count(r => r.IsVerified)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk verification");
                return Json(new { success = false, message = $"Bulk verification error: {ex.Message}" });
            }
        }

        // GET: Biometric/Help
        [HttpGet]
        public IActionResult Help()
        {
            return View();
        }

        // Utility method to log audit trail
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
    }

    // Request and Response Models
    public class FaceProcessRequest
    {
        public string ImageData { get; set; }
        public string VoterId { get; set; }
        public string Timestamp { get; set; }
    }

    public class FaceVerifyRequest
    {
        public string ImageData { get; set; }
        public string VerificationType { get; set; }
    }

    public class FaceCheckRequest
    {
        public string ImageData { get; set; }
    }

    public class VoterVerificationRequest
    {
        public string VoterId { get; set; }
        public string ImageData { get; set; }
    }

    public class UpdateBiometricRequest
    {
        public string VoterId { get; set; }
        public string ImageData { get; set; }
    }

    public class BulkVerifyRequest
    {
        public List<string> VoterIds { get; set; }
    }

    public class BulkVerifyResult
    {
        public string VoterId { get; set; }
        public bool HasBiometric { get; set; }
        public bool IsVerified { get; set; }
        public string Name { get; set; }
        public string Region { get; set; }
        public string Error { get; set; }
    }
}