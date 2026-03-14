using System.Collections.Generic;
using System.Threading.Tasks;
using VotingSystem.Models;

namespace VotingSystem.Services
{
    public interface ICommentService
    {
        Task<List<Voter>> GetAvailableVotersAsync();
        Task<List<CommentViewModel>> GetCommentsForVoterAsync(string voterNationalId);
        Task<List<CommentViewModel>> GetCommentsForSupervisorAsync(string supervisorNationalId);
        Task<List<CommentViewModel>> GetSupervisorSentCommentsAsync(string supervisorNationalId);
        Task<List<CommentViewModel>> GetCommentsFromSupervisorsAsync();
        Task<List<CommentViewModel>> GetAdminSentCommentsAsync(string adminNationalId);
        Task<List<Supervisor>> GetAvailableSupervisorsAsync();
        Task<bool> CreateAdminToSupervisorCommentAsync(string content, string adminNationalId, string adminName, string supervisorNationalId, string supervisorName);
        Task<bool> CreateSupervisorToVoterCommentAsync(string content, string supervisorNationalId, string supervisorName, string voterNationalId, string voterName, string subject);
        
        // FIXED: Add the missing parameters to match implementation
        Task<bool> CreateSupervisorToAdminCommentAsync(string content, string supervisorNationalId, string supervisorName, string adminNationalId, string adminName, string subject);
        
        Task<bool> CreateVoterToAdminCommentAsync(string content, string voterNationalId, string voterName);
        
        // ADD THESE NEW METHODS FOR DELETE FUNCTIONALITY:
        Task<bool> DeleteCommentAsync(int commentId, string currentUserNationalId, string currentUserType);
        Task<bool> MarkCommentAsReadAsync(int commentId);
    }
}