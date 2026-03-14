using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VotingSystem.Data;
using VotingSystem.Models;
using System.Security.Cryptography;
using System.Text;

namespace VotingSystem.Controllers
{
    public class VoterController : Controller
    {
        private readonly AppDbContext _context;

        public VoterController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Voter/Register - Display registration form
        public IActionResult Register()
        {
            return View();
        }

        // POST: Voter/Register - Handle registration form submission
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(Voter voter, string ConfirmPassword)
        {
            try
            {
                // Check if passwords match
                if (voter.Password != ConfirmPassword)
                {
                    return Json(new { success = false, message = "Passwords do not match." });
                }

                // Remove ConfirmPassword from ModelState to avoid validation issues
                ModelState.Remove("ConfirmPassword");

                if (ModelState.IsValid)
                {
                    // Check age requirement
                    if (voter.Age < 18)
                    {
                        return Json(new { success = false, message = "Voter must be at least 18 years old." });
                    }

                    // Check for duplicate voter
                    var existingVoter = await _context.Voters
                        .FirstOrDefaultAsync(v => 
                            v.FirstName == voter.FirstName && 
                            v.LastName == voter.LastName && 
                            v.Age == voter.Age &&
                            v.Kebele == voter.Kebele);

                    if (existingVoter != null)
                    {
                        return Json(new { success = false, message = "A voter with similar details already exists." });
                    }

                    // Check if face recognition hash already exists (if provided)
                    if (!string.IsNullOrEmpty(voter.FaceRecognitionHash))
                    {
                        var faceExists = await _context.Voters
                            .FirstOrDefaultAsync(v => v.FaceRecognitionHash == voter.FaceRecognitionHash);
                        
                        if (faceExists != null)
                        {
                            return Json(new { success = false, message = "Face recognition data already registered." });
                        }
                    }

                    // Hash the password before storing
                    voter.Password = HashPassword(voter.Password);
                    
                    // Set timestamps
                    voter.CreatedAt = DateTime.Now;
                    voter.UpdatedAt = DateTime.Now;

                    // Add voter to database
                    _context.Voters.Add(voter);
                    await _context.SaveChangesAsync();

                    return Json(new { 
                        success = true, 
                        message = $"Registration successful! Your Voter ID is: {voter.Id}. You can now login." 
                    });
                }
                else
                {
                    // Get validation errors
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return Json(new { 
                        success = false, 
                        message = "Please fix the validation errors.", 
                        errors = errors 
                    });
                }
            }
            catch (DbUpdateException dbEx)
            {
                return Json(new { 
                    success = false, 
                    message = $"Database error: {dbEx.InnerException?.Message ?? dbEx.Message}" 
                });
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    message = $"Error: {ex.Message}" 
                });
            }
        }

        // Password hashing method
        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        // Voter Login - Display login form
        public IActionResult Login()
        {
            // If already logged in, redirect to dashboard
            if (IsVoterLoggedIn())
            {
                return RedirectToAction("Dashboard", "Voter");
            }
            return View();
        }

        // Handle voter login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(int voterId, string password)
        {
            try
            {
                if (voterId <= 0 || string.IsNullOrEmpty(password))
                {
                    return Json(new { success = false, message = "Please enter both Voter ID and Password." });
                }

                // Hash the provided password
                var hashedPassword = HashPassword(password);

                // Find voter by ID and password
                var voter = await _context.Voters
                    .FirstOrDefaultAsync(v => v.Id == voterId && v.Password == hashedPassword);

                if (voter == null)
                {
                    return Json(new { success = false, message = "Invalid Voter ID or Password." });
                }

                // Check if voter has already voted
                var hasVoted = await _context.Votes.AnyAsync(v => v.VoterId == voterId);
                if (hasVoted)
                {
                    return Json(new { success = false, message = "You have already voted. Each voter can only vote once." });
                }

                // Store voter ID in session
                HttpContext.Session.SetInt32("VoterId", voterId);
                HttpContext.Session.SetString("VoterName", $"{voter.FirstName} {voter.LastName}");

                return Json(new { 
                    success = true, 
                    message = "Login successful!",
                    voterId = voter.Id,
                    voterName = $"{voter.FirstName} {voter.LastName}"
                });
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    message = $"Error during login: {ex.Message}" 
                });
            }
        }

        // Check if voter is logged in
        private bool IsVoterLoggedIn()
        {
            return HttpContext.Session.GetInt32("VoterId") != null;
        }

        // Get current voter ID from session
        private int? GetCurrentVoterId()
        {
            return HttpContext.Session.GetInt32("VoterId");
        }

        // Get current voter name from session
        private string GetCurrentVoterName()
        {
            return HttpContext.Session.GetString("VoterName");
        }

        // Dashboard - Main voter dashboard
        public IActionResult Dashboard() 
        {
            if (!IsVoterLoggedIn())
            {
                return RedirectToAction("Login", "Voter");
            }
            
            ViewBag.VoterName = GetCurrentVoterName();
            ViewBag.VoterId = GetCurrentVoterId();
            
            // Check if voter has already voted
            var hasVoted = _context.Votes.Any(v => v.VoterId == GetCurrentVoterId());
            ViewBag.HasVoted = hasVoted;
            
            return View();
        }

        // View Candidate - Display all candidates
        public async Task<IActionResult> SeeCandidate()
        {
            if (!IsVoterLoggedIn())
            {
                return RedirectToAction("Login", "Voter");
            }

            var candidates = await _context.Candidates
                .Where(c => c.IsActive)
                .OrderBy(c => c.Position)
                .ThenBy(c => c.Party)
                .ThenBy(c => c.LastName)
                .ToListAsync();
            return View(candidates);
        }

        // Get candidates for AJAX requests
        [HttpGet]
        public async Task<IActionResult> GetCandidates()
        {
            try
            {
                var candidates = await _context.Candidates
                    .Where(c => c.IsActive)
                    .Select(c => new
                    {
                        id = c.Id,
                        firstName = c.FirstName,
                        lastName = c.LastName,
                        party = c.Party,
                        position = c.Position,
                        bio = c.Bio,
                        photoUrl = c.PhotoUrl,
                        isActive = c.IsActive,
                        createdAt = c.CreatedAt
                    })
                    .ToListAsync();

                return Json(new { success = true, candidates = candidates });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error loading candidates: {ex.Message}" });
            }
        }

        // Cast Vote - Display voting interface
        public async Task<IActionResult> CastVote()
        {
            // Check if voter is logged in
            if (!IsVoterLoggedIn())
            {
                return RedirectToAction("Login", "Voter");
            }

            // Check if voter has already voted
            var voterId = GetCurrentVoterId().Value;
            var hasVoted = await _context.Votes.AnyAsync(v => v.VoterId == voterId);
            if (hasVoted)
            {
                TempData["ErrorMessage"] = "You have already voted. Each voter can only vote once.";
                return RedirectToAction("Dashboard", "Voter");
            }

            var candidates = await _context.Candidates
                .Where(c => c.IsActive)
                .ToListAsync();
            return View(candidates);
        }

        // Handle vote submission - FIXED VERSION with proper redirect
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CastVote(int candidateId)
        {
            try
            {
                // Check if voter is logged in
                if (!IsVoterLoggedIn())
                {
                    return Json(new { success = false, message = "Please login first to cast your vote." });
                }

                var voterId = GetCurrentVoterId().Value;

                // Validate inputs
                if (candidateId <= 0)
                {
                    return Json(new { success = false, message = "Invalid candidate selection." });
                }

                // Check if candidate exists and is active
                var candidate = await _context.Candidates
                    .FirstOrDefaultAsync(c => c.Id == candidateId && c.IsActive);
                
                if (candidate == null)
                {
                    return Json(new { success = false, message = "Candidate not found or is inactive." });
                }

                // Check if voter exists
                var voter = await _context.Voters
                    .FirstOrDefaultAsync(v => v.Id == voterId);
                
                if (voter == null)
                {
                    return Json(new { success = false, message = "Voter not found." });
                }

                // Check if voter has already voted
                var existingVote = await _context.Votes
                    .FirstOrDefaultAsync(v => v.VoterId == voterId);
                
                if (existingVote != null)
                {
                    return Json(new { success = false, message = "You have already voted. Each voter can only vote once." });
                }

                // Create new vote that matches database schema
                var vote = new Vote
                {
                    VoterId = voterId,
                    CandidateId = candidateId,
                    VoteDate = DateTime.Now,
                    Status = "Completed"
                };

                // Add vote to database
                _context.Votes.Add(vote);
                await _context.SaveChangesAsync();

                // Return success with redirect information
                return Json(new { 
                    success = true, 
                    message = $"Vote cast successfully for {candidate.FirstName} {candidate.LastName}!",
                    redirectUrl = Url.Action("Dashboard", "Voter")
                });
            }
            catch (DbUpdateException dbEx)
            {
                return Json(new { 
                    success = false, 
                    message = "Database error occurred while processing your vote. Please try again." 
                });
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    message = $"An error occurred while processing your vote: {ex.Message}" 
                });
            }
        }

        // View Results - Display election results
        public async Task<IActionResult> ViewResult()
        {
            if (!IsVoterLoggedIn())
            {
                return RedirectToAction("Login", "Voter");
            }

            var results = await _context.Candidates
                .Select(c => new
                {
                    Candidate = c,
                    VoteCount = _context.Votes.Count(v => v.CandidateId == c.Id)
                })
                .OrderByDescending(r => r.VoteCount)
                .ToListAsync();

            return View(results);
        }

        // Get results for AJAX requests
        [HttpGet]
        public async Task<IActionResult> GetResults()
        {
            try
            {
                var results = await _context.Candidates
                    .Select(c => new
                    {
                        candidateId = c.Id,
                        candidateName = $"{c.FirstName} {c.LastName}",
                        party = c.Party,
                        position = c.Position,
                        voteCount = _context.Votes.Count(v => v.CandidateId == c.Id),
                        photoUrl = c.PhotoUrl
                    })
                    .OrderByDescending(r => r.voteCount)
                    .ToListAsync();

                return Json(new { success = true, results = results });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error loading results: {ex.Message}" });
            }
        }

        // Send Comment - Display comment form
        public async Task<IActionResult> SendComment()
        {
            if (!IsVoterLoggedIn())
            {
                return RedirectToAction("Login", "Voter");
            }

            // Get candidates for the dropdown
            var candidates = await _context.Candidates
                .Where(c => c.IsActive)
                .OrderBy(c => c.Position)
                .ThenBy(c => c.FirstName)
                .ToListAsync();
            
            ViewBag.Candidates = candidates;
            return View();
        }

        // Handle comment submission - FIXED VERSION with DateTime
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendComment(int candidateId, string commentText)
        {
            try
            {
                if (!IsVoterLoggedIn())
                {
                    return Json(new { success = false, message = "Please login first to send a comment." });
                }

                var voterId = GetCurrentVoterId().Value;

                if (string.IsNullOrEmpty(commentText))
                {
                    return Json(new { success = false, message = "Please enter a comment." });
                }

                if (candidateId <= 0)
                {
                    return Json(new { success = false, message = "Please select a candidate." });
                }

                // Check if candidate exists
                var candidate = await _context.Candidates
                    .FirstOrDefaultAsync(c => c.Id == candidateId && c.IsActive);
                
                if (candidate == null)
                {
                    return Json(new { success = false, message = "Candidate not found or is inactive." });
                }

                // Create comment using your database schema with DateTime
                var comment = new Models.Comment
                {
                    VoterId = voterId,
                    CandidateId = candidateId,
                    CommentText = commentText,
                    CreatedAt = DateTime.Now, // Now using DateTime directly
                    IsApproved = false
                };

                _context.Comments.Add(comment);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Comment submitted successfully! It will be visible after admin approval." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // Get approved comments for display
        [HttpGet]
        public async Task<IActionResult> GetComments()
        {
            try
            {
                var comments = await _context.Comments
                    .Where(c => c.IsApproved)
                    .Include(c => c.Voter)
                    .Include(c => c.Candidate)
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => new
                    {
                        id = c.Id,
                        voterName = $"{c.Voter.FirstName} {c.Voter.LastName}",
                        candidateName = $"{c.Candidate.FirstName} {c.Candidate.LastName}",
                        position = c.Candidate.Position,
                        commentText = c.CommentText,
                        createdAt = c.CreatedAt.ToString("yyyy-MM-dd HH:mm")
                    })
                    .Take(50)
                    .ToListAsync();

                return Json(new { success = true, comments = comments });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error loading comments: {ex.Message}" });
            }
        }

        // Get comments for specific candidate
        [HttpGet]
        public async Task<IActionResult> GetCandidateComments(int candidateId)
        {
            try
            {
                var comments = await _context.Comments
                    .Where(c => c.CandidateId == candidateId && c.IsApproved)
                    .Include(c => c.Voter)
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => new
                    {
                        id = c.Id,
                        voterName = $"{c.Voter.FirstName} {c.Voter.LastName}",
                        commentText = c.CommentText,
                        createdAt = c.CreatedAt.ToString("yyyy-MM-dd HH:mm")
                    })
                    .Take(20)
                    .ToListAsync();

                return Json(new { success = true, comments = comments });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error loading comments: {ex.Message}" });
            }
        }

        // Voter Profile
        public IActionResult Profile() 
        {
            if (!IsVoterLoggedIn())
            {
                return RedirectToAction("Login", "Voter");
            }
            return View();
        }

        // Get voter profile data
        [HttpGet]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                if (!IsVoterLoggedIn())
                {
                    return Json(new { success = false, message = "Please login first." });
                }

                var voterId = GetCurrentVoterId().Value;

                var voter = await _context.Voters
                    .Where(v => v.Id == voterId)
                    .Select(v => new
                    {
                        id = v.Id,
                        firstName = v.FirstName,
                        lastName = v.LastName,
                        age = v.Age,
                        nationality = v.Nationality,
                        region = v.Region,
                        zone = v.Zone,
                        woreda = v.Woreda,
                        kebele = v.Kebele,
                        status = v.Status,
                        sex = v.Sex,
                        createdAt = v.CreatedAt.ToString("yyyy-MM-dd")
                    })
                    .FirstOrDefaultAsync();

                if (voter == null)
                {
                    return Json(new { success = false, message = "Voter not found." });
                }

                return Json(new { success = true, voter = voter });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error loading profile: {ex.Message}" });
            }
        }

        // View Inbox - Display voter's comments
        public async Task<IActionResult> Inbox()
        {
            if (!IsVoterLoggedIn())
            {
                return RedirectToAction("Login", "Voter");
            }

            var voterId = GetCurrentVoterId().Value;

            var comments = await _context.Comments
                .Where(c => c.VoterId == voterId)
                .Include(c => c.Candidate)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return View(comments);
        }

        // Get inbox messages for AJAX
        [HttpGet]
        public async Task<IActionResult> GetInboxMessages()
        {
            try
            {
                if (!IsVoterLoggedIn())
                {
                    return Json(new { success = false, message = "Please login first." });
                }

                var voterId = GetCurrentVoterId().Value;

                var messages = await _context.Comments
                    .Where(c => c.VoterId == voterId)
                    .Include(c => c.Candidate)
                    .Select(c => new
                    {
                        id = c.Id,
                        candidateName = $"{c.Candidate.FirstName} {c.Candidate.LastName}",
                        commentText = c.CommentText,
                        isApproved = c.IsApproved,
                        createdAt = c.CreatedAt.ToString("yyyy-MM-dd HH:mm")
                    })
                    .OrderByDescending(c => c.createdAt)
                    .ToListAsync();

                return Json(new { success = true, messages = messages });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error loading messages: {ex.Message}" });
            }
        }

        // Alternative names for compatibility
        public IActionResult Vote()
        {
            return RedirectToAction("CastVote");
        }

        public IActionResult Results()
        {
            return RedirectToAction("ViewResult");
        }

        // Logout
        [HttpPost]
        public IActionResult Logout() 
        {
            HttpContext.Session.Remove("VoterId");
            HttpContext.Session.Remove("VoterName");
            return RedirectToAction("Index", "Home");
        }

        // Utility method to check if voter exists
        private async Task<bool> VoterExists(int id)
        {
            return await _context.Voters.AnyAsync(e => e.Id == id);
        }
    }
}