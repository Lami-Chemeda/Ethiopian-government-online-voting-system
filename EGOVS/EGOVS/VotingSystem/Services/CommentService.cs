using Microsoft.EntityFrameworkCore;
using VotingSystem.Data;
using VotingSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VotingSystem.Services
{
    public class CommentService : ICommentService
    {
        private readonly AppDbContext _context;

        public CommentService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Voter>> GetAvailableVotersAsync()
        {
            try
            {
                Console.WriteLine("=== DEBUG: Getting available voters ===");
                
                var voters = await _context.Voters
                    .OrderBy(v => v.FirstName)
                    .ThenBy(v => v.LastName)
                    .ToListAsync();

                Console.WriteLine($"=== DEBUG: Found {voters.Count} voters ===");
                
                // Log voter details for debugging
                foreach (var voter in voters.Take(5))
                {
                    Console.WriteLine($"Voter: {voter.FirstName} {voter.LastName} - ID: {voter.NationalId}");
                }

                return voters;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== DEBUG: Error getting voters: {ex.Message} ===");
                Console.WriteLine($"=== DEBUG: Stack trace: {ex.StackTrace} ===");
                return new List<Voter>();
            }
        }

        public async Task<List<CommentViewModel>> GetCommentsForVoterAsync(string voterNationalId)
        {
            return await _context.Comments
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
        }

        // FIXED METHOD: Correctly retrieves messages for supervisors
        public async Task<List<CommentViewModel>> GetCommentsForSupervisorAsync(string supervisorNationalId)
{
    try
    {
        Console.WriteLine($"=== GET COMMENTS FOR SUPERVISOR ===");
        Console.WriteLine($"Supervisor NationalId: '{supervisorNationalId}'");

        if (string.IsNullOrEmpty(supervisorNationalId))
        {
            return new List<CommentViewModel>();
        }

        // SIMPLE DIRECT APPROACH - Get all comments for this supervisor
        var comments = await _context.Comments
            .Where(c => c.ReceiverType == "Supervisor" && c.ReceiverNationalId == supervisorNationalId)
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

        Console.WriteLine($"Found {comments.Count} comments for supervisor '{supervisorNationalId}'");

        return comments;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error in GetCommentsForSupervisorAsync: {ex.Message}");
        return new List<CommentViewModel>();
    }
}
          public async Task<List<CommentViewModel>> GetSupervisorSentCommentsAsync(string supervisorNationalId)
        {
            return await _context.Comments
                .Where(c => c.SenderType == "Supervisor" && c.SenderNationalId == supervisorNationalId)
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
        }

        public async Task<List<CommentViewModel>> GetCommentsFromSupervisorsAsync()
        {
            return await _context.Comments
                .Where(c => c.ReceiverType == "Admin" && c.SenderType == "Supervisor")
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
        }

        public async Task<List<CommentViewModel>> GetAdminSentCommentsAsync(string adminNationalId)
        {
            return await _context.Comments
                .Where(c => c.SenderType == "Admin" && c.SenderNationalId == adminNationalId)
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
        }

        public async Task<List<Supervisor>> GetAvailableSupervisorsAsync()
        {
            return await _context.Supervisors
                .Where(s => s.IsActive)
                .OrderBy(s => s.FirstName)
                .ThenBy(s => s.LastName)
                .ToListAsync();
        }

        public async Task<bool> CreateAdminToSupervisorCommentAsync(string content, string adminNationalId, string adminName, string supervisorNationalId, string supervisorName)
{
    try
    {
        Console.WriteLine($"=== DEBUG: Creating Admin to Supervisor comment ===");
        Console.WriteLine($"Admin: {adminName} ({adminNationalId})");
        Console.WriteLine($"Target Supervisor: {supervisorName} ({supervisorNationalId})");
        Console.WriteLine($"Content: {content}");

        // FIX: Trim and normalize IDs to prevent whitespace issues
        var normalizedAdminId = adminNationalId?.Trim();
        var normalizedSupervisorId = supervisorNationalId?.Trim();

        var comment = new Comment
        {
            Content = content,
            SenderType = "Admin",
            SenderNationalId = normalizedAdminId,
            SenderName = adminName,
            ReceiverType = "Supervisor",
            ReceiverNationalId = normalizedSupervisorId,
            ReceiverName = supervisorName,
            CreatedAt = DateTime.Now,
            CommentType = "AdminToSupervisor",
            Subject = "Administrative Message",
            IsRead = false
        };

        _context.Comments.Add(comment);
        await _context.SaveChangesAsync();
        
        Console.WriteLine($"=== DEBUG: Successfully created comment with ID: {comment.Id} ===");
        Console.WriteLine($"=== DEBUG: Comment details - Sender: {comment.SenderName}({comment.SenderType}), Receiver: {comment.ReceiverName}({comment.ReceiverType}), ReceiverNationalId: '{comment.ReceiverNationalId}' ===");
        
        // VERIFY the comment was saved correctly
        var savedComment = await _context.Comments.FindAsync(comment.Id);
        Console.WriteLine($"=== VERIFICATION: Saved ReceiverNationalId: '{savedComment?.ReceiverNationalId}' ===");
        
        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"=== DEBUG: Error creating AdminToSupervisor comment: {ex.Message} ===");
        Console.WriteLine($"=== DEBUG: Stack trace: {ex.StackTrace} ===");
        return false;
    }
}

        public async Task<bool> CreateSupervisorToVoterCommentAsync(string content, string supervisorNationalId, string supervisorName, string voterNationalId, string voterName, string subject)
        {
            try
            {
                var comment = new Comment
                {
                    Content = content,
                    SenderType = "Supervisor",
                    SenderNationalId = supervisorNationalId,
                    SenderName = supervisorName,
                    ReceiverType = "Voter",
                    ReceiverNationalId = voterNationalId,
                    ReceiverName = voterName,
                    CreatedAt = DateTime.Now,
                    CommentType = "SupervisorToVoter",
                    Subject = subject ?? "Voting Information",
                    IsRead = false
                };

                _context.Comments.Add(comment);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> CreateSupervisorToAdminCommentAsync(string content, string supervisorNationalId, string supervisorName, string adminNationalId, string adminName, string subject)
        {
            try
            {
                var comment = new Comment
                {
                    Content = content,
                    SenderType = "Supervisor",
                    SenderNationalId = supervisorNationalId,
                    SenderName = supervisorName,
                    ReceiverType = "Admin",
                    ReceiverNationalId = string.IsNullOrEmpty(adminNationalId) ? null : adminNationalId,
                    ReceiverName = string.IsNullOrEmpty(adminName) ? "Administration" : adminName,
                    CreatedAt = DateTime.Now,
                    CommentType = "SupervisorToAdmin",
                    Subject = subject ?? "System Feedback",
                    IsRead = false
                };

                _context.Comments.Add(comment);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating supervisor to admin comment: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CreateVoterToAdminCommentAsync(string content, string voterNationalId, string voterName)
        {
            try
            {
                var comment = new Comment
                {
                    Content = content,
                    SenderType = "Voter",
                    SenderNationalId = voterNationalId,
                    SenderName = voterName,
                    ReceiverType = "Admin",
                    ReceiverNationalId = null,
                    ReceiverName = "Administration",
                    CreatedAt = DateTime.Now,
                    CommentType = "VoterToAdmin",
                    Subject = "Voter Feedback",
                    IsRead = false
                };

                _context.Comments.Add(comment);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteCommentAsync(int commentId, string currentUserNationalId, string currentUserType)
        {
            try
            {
                var comment = await _context.Comments.FindAsync(commentId);
                if (comment == null)
                    return false;

                // Security check: Users can only delete their own sent comments or comments received by them
                bool canDelete = (comment.SenderNationalId == currentUserNationalId && comment.SenderType == currentUserType) ||
                                (comment.ReceiverNationalId == currentUserNationalId && comment.ReceiverType == currentUserType);

                if (!canDelete)
                    return false;

                _context.Comments.Remove(comment);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> MarkCommentAsReadAsync(int commentId)
        {
            try
            {
                var comment = await _context.Comments.FindAsync(commentId);
                if (comment == null)
                    return false;

                comment.IsRead = true;
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}