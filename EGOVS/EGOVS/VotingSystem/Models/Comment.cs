// Models/Comment.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VotingSystem.Models
{
    public class Comment
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(1000)]
        public string Content { get; set; }
        
        [Required]
        [StringLength(20)]
        public string SenderType { get; set; } // "Admin", "Supervisor", "Voter"
        
        [Required]
        [StringLength(50)]
        public string SenderNationalId { get; set; }
        
        [StringLength(200)]
        public string SenderName { get; set; }
        
        [Required]
        [StringLength(20)]
        public string ReceiverType { get; set; } // "Admin", "Supervisor", "Voter"
        
        [StringLength(50)]
        public string ReceiverNationalId { get; set; }
        
        [StringLength(200)]
        public string ReceiverName { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        // Comment type for easy filtering
        [StringLength(50)]
        public string CommentType { get; set; } // "SupervisorToVoter", "AdminToSupervisor", "SupervisorToAdmin", "VoterToAdmin"
        
        // Add these properties for better tracking
        public bool IsRead { get; set; } = false;
        
        [StringLength(100)]
        public string Subject { get; set; } = "General Comment";
    }

    // ViewModel for displaying comments
  // ViewModel for displaying comments
public class CommentViewModel
{
    public int Id { get; set; }
    public string Content { get; set; }
    public string SenderName { get; set; }
    public string SenderType { get; set; }
    public string SenderNationalId { get; set; } // ADD THIS MISSING PROPERTY
    public string ReceiverName { get; set; }
    public string ReceiverType { get; set; }
    public string ReceiverNationalId { get; set; } // ADD THIS MISSING PROPERTY
    public DateTime CreatedAt { get; set; }
    public string CommentType { get; set; }
    public string Subject { get; set; }
    public bool IsRead { get; set; }
    
    // Helper properties for display
    public string TimeAgo 
    { 
        get 
        {
            var timeSpan = DateTime.Now - CreatedAt;
            if (timeSpan.TotalDays >= 1)
                return $"{(int)timeSpan.TotalDays} days ago";
            else if (timeSpan.TotalHours >= 1)
                return $"{(int)timeSpan.TotalHours} hours ago";
            else
                return $"{(int)timeSpan.TotalMinutes} minutes ago";
        }
    }
}

    // Model for creating comments
    public class CreateCommentModel
    {
        [Required(ErrorMessage = "Comment content is required")]
        [StringLength(1000, ErrorMessage = "Comment cannot exceed 1000 characters")]
        public string Content { get; set; }
        
        [Required(ErrorMessage = "Receiver type is required")]
        public string ReceiverType { get; set; }
        
        public string ReceiverNationalId { get; set; }
        
        public string ReceiverName { get; set; }
        
        [StringLength(100)]
        public string Subject { get; set; } = "General Comment";
    }
}