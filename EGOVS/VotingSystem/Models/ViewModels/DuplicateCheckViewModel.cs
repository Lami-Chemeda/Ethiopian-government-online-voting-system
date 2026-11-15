using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using VotingSystem.Services;
using VotingSystem.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;

namespace VotingSystem.Controllers
{
    [Authorize]
    public class BiometricController : Controller
    {
        private readonly IBiometricService _biometricService;
        private readonly IDuplicateDetectionService _duplicateDetectionService;

        public BiometricController(IBiometricService biometricService, IDuplicateDetectionService duplicateDetectionService)
        {
            _biometricService = biometricService;
            _duplicateDetectionService = duplicateDetectionService;
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(BiometricRegistrationViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Convert image to base64 or process for face recognition
                    using var stream = model.FaceImage.OpenReadStream();
                    using var memoryStream = new MemoryStream();
                    await stream.CopyToAsync(memoryStream);
                    var imageData = Convert.ToBase64String(memoryStream.ToArray());

                    // Generate face hash
                    var faceHash = await _biometricService.GenerateFaceHashAsync(imageData);

                    // Check for duplicates
                    var isDuplicate = await _duplicateDetectionService.CheckFaceRecognitionDuplicateAsync(faceHash);
                    if (isDuplicate)
                    {
                        ModelState.AddModelError("", "This face is already registered in the system.");
                        return View(model);
                    }

                    // Register biometric
                    var success = await _biometricService.RegisterFaceBiometricAsync(model.VoterId, faceHash);
                    if (success)
                    {
                        TempData["SuccessMessage"] = "Face biometric registered successfully!";
                        return RedirectToAction("Dashboard", "Voter");
                    }
                    else
                    {
                        ModelState.AddModelError("", "Failed to register biometric data.");
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error processing face image: " + ex.Message);
                }
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult Verify()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Verify(FaceVerificationViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Process verification image
                    using var stream = model.FaceImage.OpenReadStream();
                    using var memoryStream = new MemoryStream();
                    await stream.CopyToAsync(memoryStream);
                    var imageData = Convert.ToBase64String(memoryStream.ToArray());

                    var faceHash = await _biometricService.GenerateFaceHashAsync(imageData);

                    // In real scenario, you would get voterId from the hash lookup
                    var verified = await _biometricService.VerifyFaceBiometricAsync(0, faceHash); // Need proper implementation

                    if (verified)
                    {
                        TempData["SuccessMessage"] = "Face verification successful!";
                        return RedirectToAction("CastVote", "Voter");
                    }
                    else
                    {
                        ModelState.AddModelError("", "Face verification failed. Please try again.");
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error during verification: " + ex.Message);
                }
            }
            return View(model);
        }
    }
}