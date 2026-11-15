using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VotingSystem.Models;
using VotingSystem.Data;
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System.Linq;
using VotingSystem.Services;

namespace VotingSystem.Controllers
{
    public class SupervisorController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private readonly ICommentService _commentService;

        public SupervisorController(AppDbContext context, IWebHostEnvironment environment, IConfiguration configuration, ICommentService commentService)
        {
            _context = context;
            _environment = environment;
            _configuration = configuration;
            _commentService = commentService;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // GET: Supervisor/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var supervisorNationalId = HttpContext.Session.GetString("SupervisorNationalId");
            if (string.IsNullOrEmpty(supervisorNationalId))
            {
                TempData["ErrorMessage"] = "Please login as supervisor to access dashboard.";
                return RedirectToAction("Login", "Home");
            }
            return View();
        }

        // GET: Supervisor/ManageCandidate
        public IActionResult ManageCandidate()
        {
            var supervisorNationalId = HttpContext.Session.GetString("SupervisorNationalId");
            if (string.IsNullOrEmpty(supervisorNationalId))
            {
                TempData["ErrorMessage"] = "Please login as supervisor to manage candidates.";
                return RedirectToAction("Login", "Home");
            }
            return View();
        }

        // GET: Supervisor/ManageManager
        public IActionResult ManageManager()
        {
            var supervisorNationalId = HttpContext.Session.GetString("SupervisorNationalId");
            if (string.IsNullOrEmpty(supervisorNationalId))
            {
                TempData["ErrorMessage"] = "Please login as supervisor to manage managers.";
                return RedirectToAction("Login", "Home");
            }
            return View();
        }

[HttpGet]
public async Task<IActionResult> Comment()
{
    var supervisorNationalId = HttpContext.Session.GetString("SupervisorNationalId");
    if (string.IsNullOrEmpty(supervisorNationalId))
    {
        TempData["ErrorMessage"] = "Please login as supervisor to view comments.";
        return RedirectToAction("Login", "Home");
    }

    try
    {
        // Get voters
        var availableVoters = await _context.Voters.ToListAsync();

        // Get available admins  
        var availableAdmins = await _context.Admins.ToListAsync();
        var availableAdminsAsObjects = availableAdmins.Cast<object>().ToList();

        // SIMPLE DIRECT QUERY - Get messages sent TO this supervisor
        var receivedComments = await _context.Comments
            .Where(c => c.ReceiverType == "Supervisor" && c.ReceiverNationalId == supervisorNationalId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CommentViewModel
            {
                Id = c.Id,
                Content = c.Content,
                SenderType = c.SenderType,
                SenderName = c.SenderName,
                ReceiverType = c.ReceiverType, 
                ReceiverName = c.ReceiverName,
                CreatedAt = c.CreatedAt,
                CommentType = c.CommentType,
                Subject = c.Subject ?? "General Comment",
                IsRead = c.IsRead
            })
            .ToListAsync();

        var sentComments = await _context.Comments
            .Where(c => c.SenderType == "Supervisor" && c.SenderNationalId == supervisorNationalId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CommentViewModel
            {
                Id = c.Id,
                Content = c.Content,
                SenderType = c.SenderType,
                SenderName = c.SenderName,
                ReceiverType = c.ReceiverType,
                ReceiverName = c.ReceiverName,
                CreatedAt = c.CreatedAt,
                CommentType = c.CommentType,
                Subject = c.Subject ?? "General Comment",
                IsRead = c.IsRead
            })
            .ToListAsync();

        var model = (receivedComments, sentComments, availableVoters, availableAdminsAsObjects);
        return View(model);
    }
    catch (Exception ex)
    {
        var availableVoters = await _context.Voters.ToListAsync();
        var availableAdmins = await _context.Admins.ToListAsync();
        var availableAdminsAsObjects = availableAdmins.Cast<object>().ToList();

        var model = (new List<CommentViewModel>(), new List<CommentViewModel>(), availableVoters, availableAdminsAsObjects);
        TempData["ErrorMessage"] = "Unable to load comments.";
        return View(model);
    }
}       // POST: Supervisor/CreateCommentToAdmin
// POST: Supervisor/CreateCommentToAdmin
[HttpPost]
public async Task<IActionResult> CreateCommentToAdmin(string content, string adminNationalId, string adminName, string subject)
{
    var supervisorNationalId = HttpContext.Session.GetString("SupervisorNationalId");
    var supervisorName = HttpContext.Session.GetString("SupervisorName") ?? "Supervisor";

    try
    {
        // FIXED: Added the missing adminName parameter
        var result = await _commentService.CreateSupervisorToAdminCommentAsync(
            content, supervisorNationalId, supervisorName, adminNationalId, adminName, subject);

        if (result)
        {
            TempData["Success"] = "Message sent to administration successfully!";
        }
        else
        {
            TempData["Error"] = "Failed to send message to administration.";
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error creating comment to admin: {ex.Message}");
        TempData["Error"] = "Failed to send message to administration.";
    }

    return RedirectToAction("Comment");
}

// ADD THESE NEW METHODS TO SupervisorController.cs:
[HttpGet]
public async Task<IActionResult> DirectDatabaseCheck()
{
    var supervisorNationalId = HttpContext.Session.GetString("SupervisorNationalId");
    
    try
    {
        // DIRECT DATABASE QUERY - No service, no complex logic
        var directComments = await _context.Comments
            .Where(c => c.ReceiverType == "Supervisor" && c.ReceiverNationalId == supervisorNationalId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new 
            {
                Id = c.Id,
                Content = c.Content,
                SenderName = c.SenderName,
                SenderType = c.SenderType,
                ReceiverName = c.ReceiverName,
                ReceiverType = c.ReceiverType,
                CommentType = c.CommentType,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

        return Json(new {
            success = true,
            supervisorNationalId = supervisorNationalId,
            directCommentCount = directComments.Count,
            directComments = directComments
        });
    }
    catch (Exception ex)
    {
        return Json(new { success = false, error = ex.Message });
    }
}
// POST: Supervisor/DeleteComment
// FIXED: DeleteComment method with proper parameter binding - EXACTLY like VoterController
// FIXED: DeleteComment method with proper parameter binding
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> DeleteComment([FromBody] DeleteCommentRequest request)
{
    var supervisorNationalId = HttpContext.Session.GetString("SupervisorNationalId");
    if (string.IsNullOrEmpty(supervisorNationalId))
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
        Console.WriteLine($"DeleteComment: CommentId={commentId}, SupervisorNationalId={supervisorNationalId}");

        // FIXED: Added proper parentheses around the OR condition
        var comment = await _context.Comments
            .FirstOrDefaultAsync(c => c.Id == commentId && 
                           ((c.ReceiverType == "Supervisor" && c.ReceiverNationalId == supervisorNationalId) ||
                            (c.SenderType == "Supervisor" && c.SenderNationalId == supervisorNationalId)));

        if (comment == null)
        {
            Console.WriteLine($"Comment {commentId} not found or permission denied for supervisor {supervisorNationalId}");
            return Json(new { success = false, message = "Comment not found or you don't have permission to delete it." });
        }

        // Delete the comment
        _context.Comments.Remove(comment);
        await _context.SaveChangesAsync();
        
        Console.WriteLine($"SUCCESS: Comment {commentId} deleted by supervisor {supervisorNationalId}");
        return Json(new { success = true, message = "Comment deleted successfully!" });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error deleting comment {commentId} for supervisor {supervisorNationalId}: {ex.Message}");
        return Json(new { success = false, message = "Error deleting comment. Please try again." });
    }
}

// FIXED: MarkCommentAsRead with proper parameter binding
// FIXED: MarkCommentAsRead with proper parameter binding
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> MarkCommentAsRead([FromBody] MarkAsReadRequest request)
{
    var supervisorNationalId = HttpContext.Session.GetString("SupervisorNationalId");
    if (string.IsNullOrEmpty(supervisorNationalId))
    {
        return Json(new { success = false, message = "Please login first." });
    }

    if (request == null)
    {
        return Json(new { success = false, message = "Invalid request." });
    }

    try
    {
        Console.WriteLine($"MarkCommentAsRead: CommentId={request.CommentId}, SupervisorNationalId={supervisorNationalId}");

        // Find the comment that belongs to this supervisor
        var comment = await _context.Comments
            .FirstOrDefaultAsync(c => c.Id == request.CommentId && 
                           c.ReceiverType == "Supervisor" && 
                           c.ReceiverNationalId == supervisorNationalId);

        if (comment == null)
        {
            Console.WriteLine($"Comment {request.CommentId} not found for supervisor {supervisorNationalId}");
            return Json(new { success = false, message = "Comment not found." });
        }

        // Mark as read (only update IsRead property)
        comment.IsRead = true;

        _context.Comments.Update(comment);
        await _context.SaveChangesAsync();

        Console.WriteLine($"SUCCESS: Comment {request.CommentId} marked as read by supervisor {supervisorNationalId}");
        return Json(new { success = true, message = "Comment marked as read." });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error marking comment {request.CommentId} as read: {ex.Message}");
        return Json(new { success = false, message = "Error marking comment as read." });
    }
}
        // POST: Supervisor/CreateCommentToVoter
        [HttpPost]
        public async Task<IActionResult> CreateCommentToVoter(string content, string voterNationalIds, string voterNames, string subject)
        {
            var supervisorNationalId = HttpContext.Session.GetString("SupervisorNationalId");
            var supervisorName = HttpContext.Session.GetString("SupervisorName") ?? "Supervisor";

            try
            {
                if (string.IsNullOrEmpty(voterNationalIds))
                {
                    TempData["Error"] = "Please select at least one voter.";
                    return RedirectToAction("Comment");
                }

                if (string.IsNullOrEmpty(content))
                {
                    TempData["Error"] = "Please enter a message.";
                    return RedirectToAction("Comment");
                }

                var voterIdList = voterNationalIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
                var voterNameList = voterNames.Split(',', StringSplitOptions.RemoveEmptyEntries);

                bool allSuccess = true;

                for (int i = 0; i < voterIdList.Length; i++)
                {
                    var voterId = voterIdList[i].Trim();
                    var voterName = i < voterNameList.Length ? voterNameList[i].Trim() : "Voter";

                    var result = await _commentService.CreateSupervisorToVoterCommentAsync(
                        content, supervisorNationalId, supervisorName, voterId, voterName, subject);

                    if (!result)
                    {
                        allSuccess = false;
                    }
                }

                if (allSuccess)
                {
                    TempData["Success"] = $"Message sent to {voterIdList.Length} voter(s) successfully!";
                }
                else
                {
                    TempData["Warning"] = "Message sent, but some voters may not have received it.";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR creating comments to voters: {ex.Message}");
                TempData["Error"] = $"Failed to send message to voters. Error: {ex.Message}";
            }

            return RedirectToAction("Comment");
        }
[HttpGet]
public async Task<IActionResult> DebugSupervisorComments()
{
    var supervisorNationalId = HttpContext.Session.GetString("SupervisorNationalId");
    if (string.IsNullOrEmpty(supervisorNationalId))
    {
        return Json(new { success = false, message = "Please login as supervisor first." });
    }

    try
    {
        Console.WriteLine($"=== DEBUG SUPERVISOR COMMENTS ===");
        Console.WriteLine($"Supervisor NationalId from session: {supervisorNationalId}");
        
        // Get ALL comments from database
        var allComments = await _context.Comments.ToListAsync();
        Console.WriteLine($"Total comments in database: {allComments.Count}");
        
        // Show all comments for debugging
        foreach (var comment in allComments)
        {
            Console.WriteLine($"Comment ID: {comment.Id}");
            Console.WriteLine($"  Sender: {comment.SenderName} ({comment.SenderType}) - {comment.SenderNationalId}");
            Console.WriteLine($"  Receiver: {comment.ReceiverName} ({comment.ReceiverType}) - {comment.ReceiverNationalId}");
            Console.WriteLine($"  Content: {comment.Content}");
            Console.WriteLine($"  Created: {comment.CreatedAt}");
            Console.WriteLine($"  CommentType: {comment.CommentType}");
            Console.WriteLine($"---");
        }

        // Get comments specifically for this supervisor
        var supervisorComments = allComments.Where(c => 
            c.ReceiverType == "Supervisor" && c.ReceiverNationalId == supervisorNationalId
        ).ToList();

        Console.WriteLine($"Comments found for supervisor {supervisorNationalId}: {supervisorComments.Count}");
        
        foreach (var comment in supervisorComments)
        {
            Console.WriteLine($"FOUND FOR SUPERVISOR: ID={comment.Id}, Sender={comment.SenderName}, Receiver={comment.ReceiverName}");
        }

        return Json(new { 
            success = true, 
            supervisorNationalId = supervisorNationalId,
            totalComments = allComments.Count,
            supervisorCommentsCount = supervisorComments.Count,
            allComments = allComments.Select(c => new {
                id = c.Id,
                sender = $"{c.SenderName} ({c.SenderType})",
                receiver = $"{c.ReceiverName} ({c.ReceiverType})",
                receiverNationalId = c.ReceiverNationalId,
                content = c.Content,
                createdAt = c.CreatedAt,
                commentType = c.CommentType
            }),
            supervisorComments = supervisorComments.Select(c => new {
                id = c.Id,
                sender = $"{c.SenderName} ({c.SenderType})",
                receiver = $"{c.ReceiverName} ({c.ReceiverType})",
                content = c.Content,
                createdAt = c.CreatedAt
            })
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"DEBUG ERROR: {ex.Message}");
        return Json(new { success = false, error = ex.Message });
    }
}
        // GET: Supervisor/PostResults
        public IActionResult PostResults()
        {
            var supervisorNationalId = HttpContext.Session.GetString("SupervisorNationalId");
            if (string.IsNullOrEmpty(supervisorNationalId))
            {
                TempData["ErrorMessage"] = "Please login as supervisor to post results.";
                return RedirectToAction("Login", "Home");
            }
            return View();
        }

        // ========== ELECTION STATISTICS ==========
        [HttpGet]
        public async Task<IActionResult> GetElectionStats()
        {
            var supervisorNationalId = HttpContext.Session.GetString("SupervisorNationalId");
            if (string.IsNullOrEmpty(supervisorNationalId))
            {
                return Json(new { success = false, message = "Please login first." });
            }

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var tableCheckCommand = new SqlCommand(@"
                        SELECT TABLE_NAME 
                        FROM INFORMATION_SCHEMA.TABLES 
                        WHERE TABLE_TYPE = 'BASE TABLE'", 
                        connection);

                    var existingTables = new List<string>();
                    using (var reader = await tableCheckCommand.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            existingTables.Add(reader.GetString(0));
                        }
                    }

                    Console.WriteLine("Existing tables: " + string.Join(", ", existingTables));

                    var queryParts = new List<string>();

                    if (existingTables.Contains("Voters"))
                    {
                        queryParts.Add("(SELECT COUNT(*) FROM Voters) as TotalVoters");
                    }
                    else
                    {
                        queryParts.Add("0 as TotalVoters");
                    }

                    if (existingTables.Contains("Candidates"))
                    {
                        queryParts.Add("(SELECT COUNT(*) FROM Candidates WHERE IsActive = 1) as TotalCandidates");
                    }
                    else
                    {
                        queryParts.Add("0 as TotalCandidates");
                    }

                    if (existingTables.Contains("Votes"))
                    {
                        queryParts.Add("(SELECT COUNT(*) FROM Votes) as TotalVotes");
                    }
                    else
                    {
                        queryParts.Add("0 as TotalVotes");
                    }

                    if (existingTables.Contains("Voters") && existingTables.Contains("Votes"))
                    {
                        queryParts.Add(@"
                            CASE 
                                WHEN (SELECT COUNT(*) FROM Voters) > 0 
                                THEN CAST((SELECT COUNT(*) FROM Votes) * 100.0 / (SELECT COUNT(*) FROM Voters) AS DECIMAL(5,2))
                                ELSE 0 
                            END as VoterTurnout");
                    }
                    else
                    {
                        queryParts.Add("0 as VoterTurnout");
                    }

                    if (existingTables.Contains("ResultPublishes"))
                    {
                        queryParts.Add("(SELECT COUNT(*) FROM ResultPublishes WHERE IsApproved = 1) as PublishedResults");
                    }
                    else
                    {
                        queryParts.Add("0 as PublishedResults");
                    }

                    string finalQuery = "SELECT " + string.Join(", ", queryParts);

                    Console.WriteLine("Executing query: " + finalQuery);

                    var command = new SqlCommand(finalQuery, connection);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var stats = new
                            {
                                TotalVoters = reader.GetInt32(0),
                                TotalCandidates = reader.GetInt32(1),
                                TotalVotes = reader.GetInt32(2),
                                VoterTurnout = reader.GetDecimal(3),
                                PublishedResults = reader.GetInt32(4)
                            };
                            
                            Console.WriteLine($"Stats retrieved - Voters: {stats.TotalVoters}, Candidates: {stats.TotalCandidates}, Votes: {stats.TotalVotes}, Turnout: {stats.VoterTurnout}%");
                            
                            return Json(new { success = true, stats = stats });
                        }
                    }

                    return Json(new { 
                        success = true, 
                        stats = new {
                            TotalVoters = 0,
                            TotalCandidates = 0,
                            TotalVotes = 0,
                            VoterTurnout = 0m,
                            PublishedResults = 0
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting election statistics: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                
                return Json(new { 
                    success = true, 
                    stats = new {
                        TotalVoters = 0,
                        TotalCandidates = 0,
                        TotalVotes = 0,
                        VoterTurnout = 0m,
                        PublishedResults = 0
                    },
                    message = "Using default values due to: " + ex.Message
                });
            }
        }

        // ========== ELECTION RESULT MANAGEMENT ==========
        [HttpGet]
        public async Task<IActionResult> GetElectionResults()
        {
            try
            {
                Console.WriteLine("=== GetElectionResults Supervisor Called ===");

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var debugVotesCmd = new SqlCommand("SELECT COUNT(*) FROM Votes", connection);
                    var totalVotesCount = (int)await debugVotesCmd.ExecuteScalarAsync();
                    Console.WriteLine($"DEBUG: Total votes in database: {totalVotesCount}");

                    var debugCandidatesCmd = new SqlCommand("SELECT COUNT(*) FROM Candidates WHERE IsActive = 1", connection);
                    var totalCandidatesCount = (int)await debugCandidatesCmd.ExecuteScalarAsync();
                    Console.WriteLine($"DEBUG: Total active candidates in database: {totalCandidatesCount}");

                    var command = new SqlCommand(@"
                        SELECT 
                            c.NationalId,
                            c.FirstName + ' ' + c.MiddleName + ' ' + c.LastName as CandidateName,
                            c.Party,
                            ISNULL(v.VoteCount, 0) as VoteCount,
                            CASE 
                                WHEN (SELECT COUNT(*) FROM Votes) > 0 
                                THEN CAST(ISNULL(v.VoteCount, 0) * 100.0 / (SELECT COUNT(*) FROM Votes) AS DECIMAL(5,2))
                                ELSE 0 
                            END as Percentage
                        FROM Candidates c
                        LEFT JOIN (
                            SELECT CandidateNationalId, COUNT(*) as VoteCount 
                            FROM Votes 
                            GROUP BY CandidateNationalId
                        ) v ON c.NationalId = v.CandidateNationalId
                        WHERE c.IsActive = 1
                        ORDER BY VoteCount DESC", 
                        connection);

                    var candidates = new List<object>();
                    int totalVotes = 0;

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var voteCount = reader.GetInt32(3);
                            totalVotes += voteCount;
                            
                            var candidate = new
                            {
                                nationalId = reader.GetString(0),
                                candidateName = reader.GetString(1),
                                party = reader.GetString(2),
                                voteCount = voteCount,
                                percentage = reader.GetDecimal(4),
                                isWinner = false
                            };
                            candidates.Add(candidate);
                            Console.WriteLine($"DEBUG: Candidate {reader.GetString(1)} - Votes: {voteCount}");
                        }
                    }

                    Console.WriteLine($"DEBUG: Total votes calculated: {totalVotes}");
                    Console.WriteLine($"DEBUG: Number of candidates processed: {candidates.Count}");

                    if (candidates.Count > 0 && totalVotes > 0)
                    {
                        var maxVotes = candidates.Max(c => (int)c.GetType().GetProperty("voteCount").GetValue(c));
                        Console.WriteLine($"DEBUG: Max votes among candidates: {maxVotes}");
                        
                        var updatedCandidates = new List<object>();
                        foreach (var candidate in candidates)
                        {
                            var candidateVotes = (int)candidate.GetType().GetProperty("voteCount").GetValue(candidate);
                            var isWinner = candidateVotes == maxVotes && candidateVotes > 0;
                            
                            var updatedCandidate = new
                            {
                                nationalId = candidate.GetType().GetProperty("nationalId").GetValue(candidate),
                                candidateName = candidate.GetType().GetProperty("candidateName").GetValue(candidate),
                                party = candidate.GetType().GetProperty("party").GetValue(candidate),
                                voteCount = candidateVotes,
                                percentage = candidate.GetType().GetProperty("percentage").GetValue(candidate),
                                isWinner = isWinner
                            };
                            updatedCandidates.Add(updatedCandidate);
                            
                            if (isWinner)
                                Console.WriteLine($"DEBUG: Winner identified: {updatedCandidate.candidateName}");
                        }
                        candidates = updatedCandidates;
                    }

                    return Json(new { 
                        success = true, 
                        elections = new[] { new {
                            position = "Ethiopian Government Election",
                            totalVotes = totalVotes,
                            candidates = candidates
                        }},
                        debugInfo = new {
                            databaseVotes = totalVotesCount,
                            databaseCandidates = totalCandidatesCount,
                            processedCandidates = candidates.Count
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in GetElectionResults: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return Json(new { 
                    success = false, 
                    message = "Error retrieving election results: " + ex.Message
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> PublishResults([FromForm] string announcementText)
        {
            var supervisorNationalId = HttpContext.Session.GetString("SupervisorNationalId");
            if (string.IsNullOrEmpty(supervisorNationalId))
            {
                return Json(new { success = false, message = "Please login as supervisor first." });
            }

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var clearCommand = new SqlCommand("DELETE FROM ResultPublishes", connection);
                    await clearCommand.ExecuteNonQueryAsync();

                    var resultsCommand = new SqlCommand(@"
                        SELECT 
                            c.NationalId,
                            c.FirstName + ' ' + c.MiddleName + ' ' + c.LastName as CandidateName,
                            c.Party,
                            COUNT(v.Id) as VoteCount
                        FROM Candidates c
                        LEFT JOIN Votes v ON c.NationalId = v.CandidateNationalId
                        WHERE c.IsActive = 1
                        GROUP BY c.NationalId, c.FirstName, c.MiddleName, c.LastName, c.Party
                        ORDER BY VoteCount DESC", 
                        connection);

                    var candidates = new List<(string NationalId, string Name, string Party, int Votes)>();
                    int totalVotes = 0;

                    using (var reader = await resultsCommand.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var votes = reader.GetInt32(3);
                            candidates.Add((
                                reader.GetString(0),
                                reader.GetString(1),
                                reader.GetString(2),
                                votes
                            ));
                            totalVotes += votes;
                        }
                    }

                    if (candidates.Count > 0)
                    {
                        var maxVotes = candidates.Max(c => c.Votes);
                        
                        foreach (var candidate in candidates)
                        {
                            var percentage = totalVotes > 0 ? Math.Round((candidate.Votes * 100.0m) / totalVotes, 2) : 0;
                            var isWinner = candidate.Votes == maxVotes && candidate.Votes > 0;

                            var insertCommand = new SqlCommand(@"
                                INSERT INTO ResultPublishes 
                                (CandidateNationalId, CandidateName, Party, VoteCount, Percentage, IsWinner, IsApproved, PublishedDate, ApprovedBy)
                                VALUES (@CandidateNationalId, @CandidateName, @Party, @VoteCount, @Percentage, @IsWinner, 1, @PublishedDate, @ApprovedBy)", 
                                connection);

                            insertCommand.Parameters.AddWithValue("@CandidateNationalId", candidate.NationalId);
                            insertCommand.Parameters.AddWithValue("@CandidateName", candidate.Name);
                            insertCommand.Parameters.AddWithValue("@Party", candidate.Party);
                            insertCommand.Parameters.AddWithValue("@VoteCount", candidate.Votes);
                            insertCommand.Parameters.AddWithValue("@Percentage", percentage);
                            insertCommand.Parameters.AddWithValue("@IsWinner", isWinner);
                            insertCommand.Parameters.AddWithValue("@PublishedDate", DateTime.Now);
                            insertCommand.Parameters.AddWithValue("@ApprovedBy", $"Supervisor ({HttpContext.Session.GetString("SupervisorNationalId")})");

                            await insertCommand.ExecuteNonQueryAsync();
                        }

                        await LogSupervisorActivity("Published election results with announcement: " + announcementText);
                        return Json(new { 
                            success = true, 
                            message = "Election results published successfully!",
                            totalCandidates = candidates.Count,
                            totalVotes = totalVotes
                        });
                    }
                    else
                    {
                        return Json(new { success = false, message = "No active candidates found to publish results for." });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error publishing election results: {ex.Message}");
                return Json(new { success = false, message = "Error publishing results: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetPublishedResults()
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var command = new SqlCommand(@"
                        SELECT 
                            CandidateNationalId,
                            CandidateName,
                            Party,
                            VoteCount,
                            Percentage,
                            IsWinner,
                            PublishedDate,
                            ApprovedBy
                        FROM ResultPublishes 
                        WHERE IsApproved = 1
                        ORDER BY VoteCount DESC", 
                        connection);

                    var results = new List<object>();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            results.Add(new
                            {
                                candidateNationalId = reader.GetString(0),
                                candidateName = reader.GetString(1),
                                party = reader.GetString(2),
                                voteCount = reader.GetInt32(3),
                                percentage = reader.GetDecimal(4),
                                isWinner = reader.GetBoolean(5),
                                publishedDate = reader.GetDateTime(6).ToString("yyyy-MM-dd HH:mm"),
                                approvedBy = reader.IsDBNull(7) ? "System" : reader.GetString(7)
                            });
                        }
                    }

                    return Json(new { 
                        success = true, 
                        publishedResults = results,
                        count = results.Count
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting published results: {ex.Message}");
                return Json(new { success = false, message = "Error retrieving published results: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> DebugElectionData()
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var votesCmd = new SqlCommand("SELECT COUNT(*) as TotalVotes FROM Votes", connection);
                    var totalVotes = (int)await votesCmd.ExecuteScalarAsync();

                    var candidatesCmd = new SqlCommand("SELECT COUNT(*) as TotalCandidates FROM Candidates WHERE IsActive = 1", connection);
                    var totalCandidates = (int)await candidatesCmd.ExecuteScalarAsync();

                    var votersCmd = new SqlCommand("SELECT COUNT(*) as TotalVoters FROM Voters", connection);
                    var totalVoters = (int)await votersCmd.ExecuteScalarAsync();

                    var sampleVotesCmd = new SqlCommand(@"
                        SELECT TOP 5 v.Id, v.VoterNationalId, v.CandidateNationalId, v.VoteDate, 
                               c.FirstName + ' ' + c.LastName as CandidateName
                        FROM Votes v
                        LEFT JOIN Candidates c ON v.CandidateNationalId = c.NationalId
                        ORDER BY v.VoteDate DESC", connection);

                    var sampleVotes = new List<object>();
                    using (var reader = await sampleVotesCmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            sampleVotes.Add(new
                            {
                                id = reader.GetInt32(0),
                                voterId = reader.GetString(1),
                                candidateId = reader.GetString(2),
                                voteDate = reader.GetDateTime(3),
                                candidateName = reader.IsDBNull(4) ? "Unknown" : reader.GetString(4)
                            });
                        }
                    }

                    var candidateVotesCmd = new SqlCommand(@"
                        SELECT c.NationalId, c.FirstName + ' ' + c.LastName as CandidateName, 
                               COUNT(v.Id) as VoteCount
                        FROM Candidates c
                        LEFT JOIN Votes v ON c.NationalId = v.CandidateNationalId
                        WHERE c.IsActive = 1
                        GROUP BY c.NationalId, c.FirstName, c.LastName
                        ORDER BY VoteCount DESC", connection);

                    var candidateVotes = new List<object>();
                    using (var reader = await candidateVotesCmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            candidateVotes.Add(new
                            {
                                nationalId = reader.GetString(0),
                                candidateName = reader.GetString(1),
                                voteCount = reader.GetInt32(2)
                            });
                        }
                    }

                    return Json(new
                    {
                        success = true,
                        debugInfo = new
                        {
                            totalVoters = totalVoters,
                            totalCandidates = totalCandidates,
                            totalVotes = totalVotes,
                            voterTurnout = totalVoters > 0 ? Math.Round((totalVotes * 100.0) / totalVoters, 2) : 0,
                            sampleVotes = sampleVotes,
                            candidateVotes = candidateVotes,
                            connectionString = _connectionString.Contains("Password") ? 
                                _connectionString.Replace(_connectionString.Split('=')[3].Split(';')[0], "***") : 
                                _connectionString
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        // ========== MANAGER MANAGEMENT METHODS ==========
        [HttpPost]
        public async Task<IActionResult> RegisterManager(
            [FromForm] string NationalId,
            [FromForm] string FirstName,
            [FromForm] string MiddleName,
            [FromForm] string LastName,
            [FromForm] string PhoneNumber,
            [FromForm] string Email,
            [FromForm] string Username,
            [FromForm] string Password,
            [FromForm] string Nationality,
            [FromForm] string Region,
            [FromForm] string Age,
            [FromForm] string Sex)
        {
            var supervisorNationalId = HttpContext.Session.GetString("SupervisorNationalId");
            if (string.IsNullOrEmpty(supervisorNationalId))
            {
                return Json(new { success = false, message = "Please login as supervisor first." });
            }

            try
            {
                Console.WriteLine("=== MANAGER REGISTRATION ===");
                Console.WriteLine($"Received: NationalId={NationalId}, FirstName={FirstName}, LastName={LastName}");

                if (string.IsNullOrWhiteSpace(NationalId) ||
                    string.IsNullOrWhiteSpace(FirstName) ||
                    string.IsNullOrWhiteSpace(MiddleName) ||
                    string.IsNullOrWhiteSpace(LastName) ||
                    string.IsNullOrWhiteSpace(PhoneNumber) ||
                    string.IsNullOrWhiteSpace(Username) ||
                    string.IsNullOrWhiteSpace(Password) ||
                    string.IsNullOrWhiteSpace(Nationality) ||
                    string.IsNullOrWhiteSpace(Region) ||
                    string.IsNullOrWhiteSpace(Age) ||
                    string.IsNullOrWhiteSpace(Sex))
                {
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

                if (await CheckUsernameExists(Username))
                {
                    return Json(new { success = false, message = "Username already exists. Please choose a different username." });
                }

                if (await CheckPhoneNumberExists(PhoneNumber))
                {
                    return Json(new { success = false, message = "Phone number already exists. Please use a different phone number." });
                }

                var hashedPassword = HashPasswordBase64(Password);
                var currentTime = DateTime.Now;

                var manager = new Manager
                {
                    NationalId = NationalId.Trim(),
                    Nationality = Nationality.Trim(),
                    Region = Region.Trim(),
                    PhoneNumber = PhoneNumber.Trim(),
                    FirstName = FirstName.Trim(),
                    MiddleName = MiddleName.Trim(),
                    LastName = LastName.Trim(),
                    Age = age,
                    Sex = Sex.Trim(),
                    Password = hashedPassword,
                    Email = Email?.Trim() ?? "",
                    Username = Username.Trim(),
                    IsActive = true,
                    CreatedAt = currentTime,
                    UpdatedAt = currentTime
                };

                _context.Managers.Add(manager);
                await _context.SaveChangesAsync();

                Console.WriteLine($"SUCCESS: Manager {NationalId} registered");
                return Json(new { success = true, message = "Manager registered successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating manager: {ex.Message}");
                return Json(new { success = false, message = "An unexpected error occurred: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetManagers()
        {
            var supervisorNationalId = HttpContext.Session.GetString("SupervisorNationalId");
            if (string.IsNullOrEmpty(supervisorNationalId))
            {
                return Json(new { success = false, message = "Please login as supervisor first." });
            }

            try
            {
                var managers = await _context.Managers
                    .Select(m => new 
                    { 
                        nationalId = m.NationalId,
                        firstName = m.FirstName,
                        middleName = m.MiddleName,
                        lastName = m.LastName,
                        email = m.Email,
                        username = m.Username,
                        phoneNumber = m.PhoneNumber,
                        region = m.Region,
                        nationality = m.Nationality,
                        age = m.Age,
                        sex = m.Sex,
                        isActive = m.IsActive,
                        createdAt = m.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                        updatedAt = m.UpdatedAt.ToString("yyyy-MM-dd HH:mm")
                    })
                    .OrderBy(m => m.firstName)
                    .ThenBy(m => m.lastName)
                    .ToListAsync();

                return Json(new { success = true, managers = managers });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error retrieving managers: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateManager(
            [FromForm] string NationalId,
            [FromForm] string Email,
            [FromForm] string Username,
            [FromForm] string Password,
            [FromForm] string ConfirmPassword)
        {
            var supervisorNationalId = HttpContext.Session.GetString("SupervisorNationalId");
            if (string.IsNullOrEmpty(supervisorNationalId))
            {
                return Json(new { success = false, message = "Please login as supervisor first." });
            }

            try
            {
                Console.WriteLine($"Updating manager: {NationalId}");

                if (string.IsNullOrWhiteSpace(NationalId))
                {
                    return Json(new { success = false, message = "National ID is required." });
                }

                if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Username))
                {
                    return Json(new { success = false, message = "Email and Username are required." });
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

                var manager = await _context.Managers.FindAsync(NationalId);
                if (manager == null)
                {
                    return Json(new { success = false, message = "Manager not found." });
                }

                if (manager.Username != Username && await CheckUsernameExists(Username))
                {
                    return Json(new { success = false, message = "Username already exists. Please choose a different username." });
                }

                manager.Email = Email;
                manager.Username = Username;
                manager.UpdatedAt = DateTime.Now;

                if (!string.IsNullOrWhiteSpace(Password))
                {
                    manager.Password = HashPasswordBase64(Password);
                }

                _context.Managers.Update(manager);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Manager updated successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating manager: {ex.Message}");
                return Json(new { success = false, message = "An unexpected error occurred: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteManager([FromForm] string NationalId)
        {
            var supervisorNationalId = HttpContext.Session.GetString("SupervisorNationalId");
            if (string.IsNullOrEmpty(supervisorNationalId))
            {
                return Json(new { success = false, message = "Please login as supervisor first." });
            }

            try
            {
                Console.WriteLine($"Deleting manager: {NationalId}");

                if (string.IsNullOrWhiteSpace(NationalId))
                {
                    return Json(new { success = false, message = "National ID is required." });
                }

                var manager = await _context.Managers.FindAsync(NationalId);
                if (manager == null)
                {
                    return Json(new { success = false, message = "Manager not found." });
                }

                _context.Managers.Remove(manager);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = $"Manager {manager.FirstName} {manager.LastName} deleted successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting manager: {ex.Message}");
                return Json(new { success = false, message = "An unexpected error occurred: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ToggleManagerStatus([FromForm] string NationalId, [FromForm] string IsActive)
        {
            var supervisorNationalId = HttpContext.Session.GetString("SupervisorNationalId");
            if (string.IsNullOrEmpty(supervisorNationalId))
            {
                return Json(new { success = false, message = "Please login as supervisor first." });
            }

            try
            {
                Console.WriteLine($"Toggling manager status: {NationalId} to {IsActive}");

                if (string.IsNullOrEmpty(NationalId))
                {
                    return Json(new { success = false, message = "National ID is required." });
                }

                if (!bool.TryParse(IsActive, out bool isActive))
                {
                    return Json(new { success = false, message = "Invalid status value." });
                }

                var manager = await _context.Managers.FindAsync(NationalId);
                if (manager == null)
                {
                    return Json(new { success = false, message = "Manager not found." });
                }

                manager.IsActive = isActive;
                manager.UpdatedAt = DateTime.Now;

                _context.Managers.Update(manager);
                await _context.SaveChangesAsync();

                var status = isActive ? "activated" : "deactivated";
                return Json(new { success = true, message = $"Manager {status} successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error toggling manager status: {ex.Message}");
                return Json(new { success = false, message = "Error updating manager status: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> CheckNationalId(string nationalId)
        {
            var supervisorNationalId = HttpContext.Session.GetString("SupervisorNationalId");
            if (string.IsNullOrEmpty(supervisorNationalId))
            {
                return Json(new { exists = false, message = "Please login first." });
            }

            if (string.IsNullOrEmpty(nationalId))
            {
                return Json(new { exists = false });
            }

            nationalId = nationalId.Trim();

            try
            {
                var checkResult = await CheckNationalIdInAllTables(nationalId);
                if (checkResult.exists)
                {
                    return Json(new { exists = true, message = checkResult.message });
                }

                return Json(new { exists = false });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking National ID: {ex.Message}");
                return Json(new { exists = false, message = "Error checking National ID availability." });
            }
        }

        // ========== CANDIDATE MANAGEMENT METHODS ==========
        [HttpPost]
        public async Task<IActionResult> RegisterCandidate(
            [FromForm] string NationalId,
            [FromForm] string FirstName,
            [FromForm] string MiddleName,
            [FromForm] string LastName,
            [FromForm] string PhoneNumber,
            [FromForm] string Party,
            [FromForm] string Bio,
            [FromForm] string Password,
            [FromForm] string Nationality,
            [FromForm] string Region,
            [FromForm] string Age,
            [FromForm] string Sex,
            [FromForm] IFormFile PhotoFile = null,
            [FromForm] IFormFile LogoFile = null)
        {
            var supervisorNationalId = HttpContext.Session.GetString("SupervisorNationalId");
            if (string.IsNullOrEmpty(supervisorNationalId))
            {
                return Json(new { success = false, message = "Please login as supervisor first." });
            }

            try
            {
                Console.WriteLine("=== CANDIDATE REGISTRATION ===");
                Console.WriteLine($"Received: NationalId={NationalId}, FirstName={FirstName}, LastName={LastName}, Party={Party}");

                if (string.IsNullOrWhiteSpace(NationalId) ||
                    string.IsNullOrWhiteSpace(FirstName) ||
                    string.IsNullOrWhiteSpace(MiddleName) ||
                    string.IsNullOrWhiteSpace(LastName) ||
                    string.IsNullOrWhiteSpace(PhoneNumber) ||
                    string.IsNullOrWhiteSpace(Party) ||
                    string.IsNullOrWhiteSpace(Password) ||
                    string.IsNullOrWhiteSpace(Nationality) ||
                    string.IsNullOrWhiteSpace(Region) ||
                    string.IsNullOrWhiteSpace(Age) ||
                    string.IsNullOrWhiteSpace(Sex))
                {
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

                if (await _context.Candidates.AnyAsync(c => c.PhoneNumber == PhoneNumber))
                {
                    return Json(new { success = false, message = "Phone number already exists. Please use a different phone number." });
                }

                string photoUrl = null;
                if (PhotoFile != null && PhotoFile.Length > 0)
                {
                    photoUrl = await HandleCandidatePhotoUpload(PhotoFile);
                }

                string logoUrl = null;
                if (LogoFile != null && LogoFile.Length > 0)
                {
                    logoUrl = await HandleCandidateLogoUpload(LogoFile);
                }

                var hashedPassword = HashPasswordBase64(Password);
                var currentTime = DateTime.Now;

                var candidate = new Candidate
                {
                    NationalId = NationalId.Trim(),
                    Nationality = Nationality.Trim(),
                    Region = Region.Trim(),
                    PhoneNumber = PhoneNumber.Trim(),
                    FirstName = FirstName.Trim(),
                    MiddleName = MiddleName.Trim(),
                    LastName = LastName.Trim(),
                    Age = age,
                    Sex = Sex.Trim(),
                    Password = hashedPassword,
                    Party = Party.Trim(),
                    Bio = string.IsNullOrWhiteSpace(Bio) ? null : Bio.Trim(),
                    PhotoUrl = photoUrl,
                    Logo = logoUrl,
                    SymbolName = "Lion",
                    SymbolImagePath = "/images/symbols/lion.png",
                    SymbolUnicode = "🦁",
                    PartyColor = "#1d3557",
                    IsActive = true,
                    CreatedAt = currentTime,
                    UpdatedAt = currentTime
                };

                Console.WriteLine($"Creating candidate with NationalId: {candidate.NationalId}");

                _context.Candidates.Add(candidate);
                
                try
                {
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"SUCCESS: Candidate {NationalId} registered");
                    return Json(new { success = true, message = "Candidate registered successfully!" });
                }
                catch (DbUpdateException dbEx)
                {
                    Console.WriteLine($"Database error: {dbEx.InnerException?.Message}");
                    
                    if (dbEx.InnerException != null && dbEx.InnerException.Message.Contains("PK_Candidates"))
                    {
                        return Json(new { success = false, message = "Candidate with this National ID already exists." });
                    }
                    else
                    {
                        return Json(new { success = false, message = "Database error: " + dbEx.InnerException?.Message });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating candidate: {ex.Message}");
                return Json(new { success = false, message = "An unexpected error occurred: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCandidates()
        {
            var supervisorNationalId = HttpContext.Session.GetString("SupervisorNationalId");
            if (string.IsNullOrEmpty(supervisorNationalId))
            {
                return Json(new { success = false, message = "Please login as supervisor first." });
            }

            try
            {
                var candidates = await _context.Candidates
                    .Select(c => new 
                    { 
                        nationalId = c.NationalId,
                        firstName = c.FirstName,
                        middleName = c.MiddleName,
                        lastName = c.LastName,
                        phoneNumber = c.PhoneNumber,
                        party = c.Party,
                        bio = c.Bio ?? "",
                        photoUrl = c.PhotoUrl ?? "",
                        logo = c.Logo ?? "",
                        nationality = c.Nationality,
                        region = c.Region,
                        age = c.Age,
                        sex = c.Sex,
                        isActive = c.IsActive,
                        createdAt = c.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                        updatedAt = c.UpdatedAt.ToString("yyyy-MM-dd HH:mm"),
                        voteCount = 0
                    })
                    .OrderBy(c => c.firstName)
                    .ThenBy(c => c.lastName)
                    .ToListAsync();

                return Json(new { success = true, candidates = candidates });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetCandidates: {ex.Message}");
                return Json(new { 
                    success = false, 
                    message = "Error retrieving candidates: " + ex.Message,
                    candidates = new List<object>() 
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateCandidate(
            [FromForm] string NationalId,
            [FromForm] string Party,
            [FromForm] string Bio,
            [FromForm] string Password,
            [FromForm] string ConfirmPassword,
            [FromForm] IFormFile PhotoFile = null,
            [FromForm] IFormFile LogoFile = null)
        {
            var supervisorNationalId = HttpContext.Session.GetString("SupervisorNationalId");
            if (string.IsNullOrEmpty(supervisorNationalId))
            {
                return Json(new { success = false, message = "Please login as supervisor first." });
            }

            try
            {
                Console.WriteLine($"Updating candidate: {NationalId}");

                if (string.IsNullOrWhiteSpace(NationalId))
                {
                    return Json(new { success = false, message = "National ID is required." });
                }

                if (string.IsNullOrWhiteSpace(Party))
                {
                    return Json(new { success = false, message = "Party is required." });
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

                var candidate = await _context.Candidates.FindAsync(NationalId);
                if (candidate == null)
                {
                    return Json(new { success = false, message = "Candidate not found." });
                }

                if (PhotoFile != null && PhotoFile.Length > 0)
                {
                    if (!string.IsNullOrEmpty(candidate.PhotoUrl))
                    {
                        DeleteOldCandidateFile(candidate.PhotoUrl);
                    }
                    candidate.PhotoUrl = await HandleCandidatePhotoUpload(PhotoFile);
                }

                if (LogoFile != null && LogoFile.Length > 0)
                {
                    if (!string.IsNullOrEmpty(candidate.Logo))
                    {
                        DeleteOldCandidateFile(candidate.Logo);
                    }
                    candidate.Logo = await HandleCandidateLogoUpload(LogoFile);
                }

                candidate.Party = Party;
                candidate.Bio = string.IsNullOrWhiteSpace(Bio) ? null : Bio.Trim();
                candidate.UpdatedAt = DateTime.Now;

                if (!string.IsNullOrWhiteSpace(Password))
                {
                    candidate.Password = HashPasswordBase64(Password);
                }

                _context.Candidates.Update(candidate);
                
                try
                {
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "Candidate updated successfully!" });
                }
                catch (DbUpdateException dbEx)
                {
                    Console.WriteLine($"Database update error: {dbEx.InnerException?.Message}");
                    return Json(new { success = false, message = "Database error: " + dbEx.InnerException?.Message });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating candidate: {ex.Message}");
                return Json(new { success = false, message = "An unexpected error occurred: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCandidate([FromForm] string NationalId)
        {
            var supervisorNationalId = HttpContext.Session.GetString("SupervisorNationalId");
            if (string.IsNullOrEmpty(supervisorNationalId))
            {
                return Json(new { success = false, message = "Please login as supervisor first." });
            }

            try
            {
                Console.WriteLine($"Deleting candidate: {NationalId}");

                if (string.IsNullOrWhiteSpace(NationalId))
                {
                    return Json(new { success = false, message = "National ID is required." });
                }

                var candidate = await _context.Candidates.FindAsync(NationalId);
                if (candidate == null)
                {
                    return Json(new { success = false, message = "Candidate not found." });
                }

                if (!string.IsNullOrEmpty(candidate.PhotoUrl))
                {
                    DeleteOldCandidateFile(candidate.PhotoUrl);
                }

                if (!string.IsNullOrEmpty(candidate.Logo))
                {
                    DeleteOldCandidateFile(candidate.Logo);
                }

                _context.Candidates.Remove(candidate);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = $"Candidate {candidate.FirstName} {candidate.LastName} deleted successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting candidate: {ex.Message}");
                return Json(new { success = false, message = "An unexpected error occurred: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ToggleCandidateStatus([FromForm] string NationalId, [FromForm] string IsActive)
        {
            var supervisorNationalId = HttpContext.Session.GetString("SupervisorNationalId");
            if (string.IsNullOrEmpty(supervisorNationalId))
            {
                return Json(new { success = false, message = "Please login as supervisor first." });
            }

            try
            {
                Console.WriteLine($"Toggling candidate status: {NationalId} to {IsActive}");

                if (string.IsNullOrEmpty(NationalId))
                {
                    return Json(new { success = false, message = "National ID is required." });
                }

                if (!bool.TryParse(IsActive, out bool isActive))
                {
                    return Json(new { success = false, message = "Invalid status value." });
                }

                var candidate = await _context.Candidates.FindAsync(NationalId);
                if (candidate == null)
                {
                    return Json(new { success = false, message = "Candidate not found." });
                }

                candidate.IsActive = isActive;
                candidate.UpdatedAt = DateTime.Now;

                _context.Candidates.Update(candidate);
                await _context.SaveChangesAsync();

                var status = isActive ? "activated" : "deactivated";
                return Json(new { success = true, message = $"Candidate {status} successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error toggling candidate status: {ex.Message}");
                return Json(new { success = false, message = "Error updating candidate status: " + ex.Message });
            }
        }

        #region Helper Methods

        private async Task<(bool exists, string message)> CheckNationalIdInAllTables(string nationalId)
        {
            try
            {
                var voterCount = await _context.Voters.CountAsync(v => v.NationalId == nationalId);
                if (voterCount > 0)
                {
                    return (true, $"This National ID ({nationalId}) is already registered as Voter. Please use a different National ID.");
                }

                var adminCount = await _context.Admins.CountAsync(a => a.NationalId == nationalId);
                if (adminCount > 0)
                {
                    return (true, $"This National ID ({nationalId}) is already registered as Admin. Please use a different National ID.");
                }

                var managerCount = await _context.Managers.CountAsync(m => m.NationalId == nationalId);
                if (managerCount > 0)
                {
                    return (true, $"This National ID ({nationalId}) is already registered as Manager. Please use a different National ID.");
                }

                var supervisorCount = await _context.Supervisors.CountAsync(s => s.NationalId == nationalId);
                if (supervisorCount > 0)
                {
                    return (true, $"This National ID ({nationalId}) is already registered as Supervisor. Please use a different National ID.");
                }

                var candidateCount = await _context.Candidates.CountAsync(c => c.NationalId == nationalId);
                if (candidateCount > 0)
                {
                    return (true, $"This National ID ({nationalId}) is already registered as Candidate. Please use a different National ID.");
                }

                return (false, "");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking National ID in all tables: {ex.Message}");
                return (false, "Error checking National ID availability.");
            }
        }

        private async Task<bool> CheckUsernameExists(string username)
        {
            return await _context.Managers.AnyAsync(m => m.Username == username);
        }

        private async Task<bool> CheckPhoneNumberExists(string phoneNumber)
        {
            return await _context.Managers.AnyAsync(m => m.PhoneNumber == phoneNumber);
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

        private async Task<string> HandleCandidatePhotoUpload(IFormFile photoFile)
        {
            if (photoFile == null || photoFile.Length == 0)
                return null;

            try
            {
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "images", "candidates", "photos");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(photoFile.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await photoFile.CopyToAsync(stream);
                }

                return $"/images/candidates/photos/{uniqueFileName}";
            }
            catch (Exception ex)
            {
                throw new Exception("Error uploading candidate photo: " + ex.Message);
            }
        }

        private async Task<string> HandleCandidateLogoUpload(IFormFile logoFile)
        {
            // FIXED: Properly call the method instead of method group
            if (logoFile == null || logoFile.Length == 0)
                return null;

            try
            {
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "images", "candidates", "logos");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(logoFile.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await logoFile.CopyToAsync(stream);
                }

                return $"/images/candidates/logos/{uniqueFileName}";
            }
            catch (Exception ex)
            {
                throw new Exception("Error uploading candidate logo: " + ex.Message);
            }
        }

        private void DeleteOldCandidateFile(string fileUrl)
        {
            try
            {
                if (!string.IsNullOrEmpty(fileUrl))
                {
                    var fileName = Path.GetFileName(fileUrl);
                    var fileType = fileUrl.Contains("/logos/") ? "logos" : "photos";
                    var filePath = Path.Combine(_environment.WebRootPath, "images", "candidates", fileType, fileName);
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error deleting old candidate file: " + ex.Message);
            }
        }

        private async Task LogSupervisorActivity(string activityDescription)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var command = new SqlCommand(@"
                        INSERT INTO SupervisorActivities (ActivityDescription, ActivityDate) 
                        VALUES (@ActivityDescription, @ActivityDate)", 
                        connection);

                    command.Parameters.AddWithValue("@ActivityDescription", activityDescription);
                    command.Parameters.AddWithValue("@ActivityDate", DateTime.Now);

                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to log supervisor activity: " + ex.Message);
            }
        }

        #endregion
        // Add these request models at the bottom of SupervisorController.cs

    }

}