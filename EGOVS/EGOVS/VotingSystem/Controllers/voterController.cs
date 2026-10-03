using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using VotingSystem.Models;
using VotingSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using VotingSystem.Services;

namespace VotingSystem.Controllers
{
    public class VoterController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<VoterController> _logger;
        private readonly string _connectionString;
        private readonly ICommentService _commentService;

        public VoterController(AppDbContext context, ILogger<VoterController> logger, IConfiguration configuration, ICommentService commentService)
        {
            _context = context;
            _logger = logger;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _commentService = commentService;
        }

        [HttpGet]
        public IActionResult Dashboard()
        {
            var voterNationalId = HttpContext.Session.GetString("VoterNationalId");
            var voterName = HttpContext.Session.GetString("VoterName");
            
            if (string.IsNullOrEmpty(voterNationalId))
            {
                return RedirectToAction("Login", "Home");
            }

            ViewBag.VoterName = voterName;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> SeeCandidate()
        {
            var voterNationalId = HttpContext.Session.GetString("VoterNationalId");
            if (string.IsNullOrEmpty(voterNationalId))
            {
                return RedirectToAction("Login", "Home");
            }

            var candidates = await _context.Candidates
                .OrderBy(c => c.Party)
                .ThenBy(c => c.FirstName)
                .ToListAsync();

            return View(candidates);
        }

        [HttpGet]
        public async Task<IActionResult> CastVote()
        {
            var voterNationalId = HttpContext.Session.GetString("VoterNationalId");
            if (string.IsNullOrEmpty(voterNationalId))
            {
                return RedirectToAction("Login", "Home");
            }

            // Check if already voted
            var hasVoted = await _context.Votes.AnyAsync(v => v.VoterNationalId == voterNationalId);
            if (hasVoted)
            {
                TempData["ErrorMessage"] = "You have already cast your vote!";
                return RedirectToAction("Dashboard");
            }

            // Get election settings
            var electionSettings = await _context.ElectionSettings
                .OrderByDescending(e => e.Id)
                .FirstOrDefaultAsync();

            var currentTime = DateTime.Now;
            ViewBag.ElectionSettings = electionSettings;
            ViewBag.CurrentTime = currentTime;
            ViewBag.IsElectionActive = electionSettings?.IsActive == true && 
                                     currentTime >= electionSettings.StartDate && 
                                     currentTime <= electionSettings.EndDate;

            var candidates = await _context.Candidates
                .Where(c => c.IsActive)
                .OrderBy(c => c.Party)
                .ThenBy(c => c.FirstName)
                .ToListAsync();

            return View(candidates);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CastVote(string candidateNationalId)
        {
            try
            {
                var voterNationalId = HttpContext.Session.GetString("VoterNationalId");
                if (string.IsNullOrEmpty(voterNationalId))
                {
                    return Json(new { success = false, message = "Please login to vote." });
                }

                if (string.IsNullOrEmpty(candidateNationalId))
                {
                    return Json(new { success = false, message = "Please select a candidate." });
                }

                // Check election time
                var electionSettings = await _context.ElectionSettings
                    .OrderByDescending(e => e.Id)
                    .FirstOrDefaultAsync();

                var currentTime = DateTime.Now;
                bool isElectionActive = electionSettings?.IsActive == true && 
                                      currentTime >= electionSettings.StartDate && 
                                      currentTime <= electionSettings.EndDate;

                if (!isElectionActive)
                {
                    string errorMessage = "Voting is not currently active. ";
                    
                    if (electionSettings == null || !electionSettings.IsActive)
                    {
                        errorMessage += "The election has not been activated by the administrator.";
                    }
                    else if (currentTime < electionSettings.StartDate)
                    {
                        errorMessage += $"Voting will start on {electionSettings.StartDate:yyyy-MM-dd HH:mm}.";
                    }
                    else if (currentTime > electionSettings.EndDate)
                    {
                        errorMessage += $"Voting ended on {electionSettings.EndDate:yyyy-MM-dd HH:mm}.";
                    }
                    else
                    {
                        errorMessage += "Please check the election schedule.";
                    }

                    return Json(new { success = false, message = errorMessage });
                }

                var candidate = await _context.Candidates
                    .FirstOrDefaultAsync(c => c.NationalId == candidateNationalId && c.IsActive);
                
                if (candidate == null)
                {
                    return Json(new { success = false, message = "Invalid candidate selected." });
                }

                var voter = await _context.Voters.FirstOrDefaultAsync(v => v.NationalId == voterNationalId);
                if (voter == null)
                {
                    return Json(new { success = false, message = "Voter not found." });
                }

                var hasVoted = await _context.Votes.AnyAsync(v => v.VoterNationalId == voterNationalId);
                if (hasVoted)
                {
                    return Json(new { success = false, message = "You have already cast your vote!" });
                }

                var vote = new Vote
                {
                    VoterNationalId = voterNationalId,
                    CandidateNationalId = candidateNationalId,
                    VoteDate = DateTime.Now,
                    IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                };

                voter.UpdatedAt = DateTime.Now;

                _context.Votes.Add(vote);
                _context.Voters.Update(voter);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Vote cast successfully for voter {voterNationalId} for candidate {candidateNationalId}");

                return Json(new { 
                    success = true, 
                    message = "Your vote has been cast successfully!",
                    redirectUrl = Url.Action("Dashboard", "Voter")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error casting vote");
                return Json(new { success = false, message = "An error occurred while casting your vote. Please try again." });
            }
        }

        [HttpPost]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            TempData["SuccessMessage"] = "Logged out successfully!";
            return RedirectToAction("Login", "Home");
        }

        [HttpGet]
        public async Task<JsonResult> GetVoterStats()
        {
            var voterNationalId = HttpContext.Session.GetString("VoterNationalId");
            if (string.IsNullOrEmpty(voterNationalId))
            {
                return Json(new { success = false });
            }

            try
            {
                var totalCandidates = await _context.Candidates.CountAsync(c => c.IsActive);
                var totalVotes = await _context.Votes.CountAsync();
                var hasVoted = await _context.Votes.AnyAsync(v => v.VoterNationalId == voterNationalId);

                // Get election status
                var electionSettings = await _context.ElectionSettings
                    .OrderByDescending(e => e.Id)
                    .FirstOrDefaultAsync();

                var currentTime = DateTime.Now;
                bool isElectionActive = electionSettings?.IsActive == true && 
                                      currentTime >= electionSettings.StartDate && 
                                      currentTime <= electionSettings.EndDate;

                return Json(new { 
                    success = true,
                    totalCandidates,
                    totalVotes,
                    hasVoted,
                    isElectionActive,
                    electionStartDate = electionSettings?.StartDate.ToString("yyyy-MM-dd HH:mm"),
                    electionEndDate = electionSettings?.EndDate.ToString("yyyy-MM-dd HH:mm")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting voter stats");
                return Json(new { success = false });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendComment(string content)
        {
            try
            {
                var voterNationalId = HttpContext.Session.GetString("VoterNationalId");
                if (string.IsNullOrEmpty(voterNationalId))
                {
                    return RedirectToAction("Login", "Home");
                }

                if (string.IsNullOrEmpty(content))
                {
                    TempData["ErrorMessage"] = "Comment content cannot be empty.";
                    return View();
                }

                var voter = await _context.Voters.FirstOrDefaultAsync(v => v.NationalId == voterNationalId);
                if (voter == null)
                {
                    TempData["ErrorMessage"] = "Voter not found.";
                    return View();
                }

                var result = await _commentService.CreateVoterToAdminCommentAsync(
                    content, voterNationalId, $"{voter.FirstName} {voter.LastName}");

                if (result)
                {
                    TempData["SuccessMessage"] = "Your comment has been submitted successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to submit your comment. Please try again.";
                }

                return RedirectToAction("Dashboard");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting comment");
                TempData["ErrorMessage"] = "An error occurred while submitting your comment.";
                return View();
            }
        }

        [HttpGet]
        public async Task<IActionResult> Comments()
        {
            var voterNationalId = HttpContext.Session.GetString("VoterNationalId");
            if (string.IsNullOrEmpty(voterNationalId))
            {
                TempData["ErrorMessage"] = "Please login to view comments.";
                return RedirectToAction("Login", "Home");
            }

            try
            {
                var comments = await _context.Comments
                    .Where(c => c.ReceiverType == "Voter" && c.ReceiverNationalId == voterNationalId)
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

                foreach (var comment in comments.Where(c => !c.IsRead))
                {
                    await _commentService.MarkCommentAsReadAsync(comment.Id);
                }

                return View(comments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading comments for voter {VoterNationalId}", voterNationalId);
                TempData["ErrorMessage"] = "Unable to load comments at the moment.";
                return View(new List<CommentViewModel>());
            }
        }

        // FIXED: DeleteComment method with proper parameter binding
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment([FromBody] DeleteCommentRequest request)
        {
            var voterNationalId = HttpContext.Session.GetString("VoterNationalId");
            if (string.IsNullOrEmpty(voterNationalId))
            {
                return Json(new { success = false, message = "Please login first." });
            }

            if (request == null)
            {
                return Json(new { success = false, message = "Invalid request." });
            }

            var commentId = request.CommentId;

            try
            {
                _logger.LogInformation($"DeleteComment: CommentId={commentId}, VoterNationalId={voterNationalId}");

                // Find the comment directly
                var comment = await _context.Comments
                    .FirstOrDefaultAsync(c => c.Id == commentId && 
                                           c.ReceiverType == "Voter" && 
                                           c.ReceiverNationalId == voterNationalId);

                if (comment == null)
                {
                    _logger.LogWarning($"Comment {commentId} not found or permission denied for voter {voterNationalId}");
                    return Json(new { success = false, message = "Comment not found or you don't have permission to delete it." });
                }

                // Delete the comment
                _context.Comments.Remove(comment);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation($"SUCCESS: Comment {commentId} deleted by voter {voterNationalId}");
                return Json(new { success = true, message = "Comment deleted successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting comment {commentId} for voter {voterNationalId}");
                return Json(new { success = false, message = "Error deleting comment. Please try again." });
            }
        }

        // FIXED: MarkCommentAsRead with proper parameter binding
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkCommentAsRead([FromBody] MarkAsReadRequest request)
        {
            var voterNationalId = HttpContext.Session.GetString("VoterNationalId");
            if (string.IsNullOrEmpty(voterNationalId))
            {
                return Json(new { success = false, message = "Please login first." });
            }

            if (request == null)
            {
                return Json(new { success = false, message = "Invalid request." });
            }

            try
            {
                var result = await _commentService.MarkCommentAsReadAsync(request.CommentId);
                return Json(new { success = result, message = result ? "Comment marked as read." : "Failed to mark comment as read." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking comment {CommentId} as read", request.CommentId);
                return Json(new { success = false, message = "Error marking comment as read." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ViewResult()
        {
            var voterNationalId = HttpContext.Session.GetString("VoterNationalId");
            if (string.IsNullOrEmpty(voterNationalId))
            {
                return RedirectToAction("Login", "Home");
            }

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var command = new SqlCommand(@"
                        SELECT 
                            CandidateName,
                            Party,
                            VoteCount,
                            Percentage,
                            IsWinner,
                            PublishedDate
                        FROM ResultPublishes 
                        WHERE IsApproved = 1
                        ORDER BY VoteCount DESC", 
                        connection);

                    var results = new List<object>();
                    int totalVotes = 0;

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var voteCount = reader.GetInt32(reader.GetOrdinal("VoteCount"));
                            totalVotes += voteCount;
                            
                            var result = new
                            {
                                CandidateName = reader.GetString(reader.GetOrdinal("CandidateName")),
                                Party = reader.GetString(reader.GetOrdinal("Party")),
                                VoteCount = voteCount,
                                Percentage = reader.GetDecimal(reader.GetOrdinal("Percentage")),
                                IsWinner = reader.GetBoolean(reader.GetOrdinal("IsWinner")),
                                PublishedDate = reader.GetDateTime(reader.GetOrdinal("PublishedDate")).ToString("yyyy-MM-dd HH:mm")
                            };
                            results.Add(result);
                        }
                    }

                    ViewBag.TotalVotes = totalVotes;
                    ViewBag.HasResults = results.Count > 0;
                    
                    return View(results);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading election results");
                TempData["ErrorMessage"] = "An error occurred while loading election results.";
                return View(new List<object>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> ViewPublishedResults()
        {
            var voterNationalId = HttpContext.Session.GetString("VoterNationalId");
            if (string.IsNullOrEmpty(voterNationalId))
            {
                return RedirectToAction("Login", "Home");
            }

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var command = new SqlCommand(@"
                        SELECT 
                            CandidateName,
                            Party,
                            VoteCount,
                            Percentage,
                            IsWinner,
                            PublishedDate
                        FROM ResultPublishes 
                        WHERE IsApproved = 1
                        ORDER BY VoteCount DESC", 
                        connection);

                    var results = new List<object>();
                    int totalVotes = 0;

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var voteCount = reader.GetInt32(reader.GetOrdinal("VoteCount"));
                            totalVotes += voteCount;
                            
                            results.Add(new
                            {
                                CandidateName = reader.GetString(reader.GetOrdinal("CandidateName")),
                                Party = reader.GetString(reader.GetOrdinal("Party")),
                                VoteCount = voteCount,
                                Percentage = reader.GetDecimal(reader.GetOrdinal("Percentage")),
                                IsWinner = reader.GetBoolean(reader.GetOrdinal("IsWinner")),
                                PublishedDate = reader.GetDateTime(reader.GetOrdinal("PublishedDate")).ToString("yyyy-MM-dd HH:mm")
                            });
                        }
                    }

                    ViewBag.TotalVotes = totalVotes;
                    ViewBag.HasResults = results.Count > 0;
                    
                    return View("ViewResult", results);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading election results");
                TempData["ErrorMessage"] = "An error occurred while loading election results.";
                return View("ViewResult", new List<object>());
            }
        }
    }

    // Add these request models at the bottom of the same file
    public class DeleteCommentRequest
    {
        public int CommentId { get; set; }
    }

    public class MarkAsReadRequest
    {
        public int CommentId { get; set; }
    }
}