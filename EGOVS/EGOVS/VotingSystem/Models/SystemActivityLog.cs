using System;
using System.ComponentModel.DataAnnotations;

namespace VotingSystem.Models
{
    public class SystemActivityLog
    {
        [Key]
        public int Id { get; set; }
        
        [StringLength(50)]
        public string NationalId { get; set; } // "SYSTEM" for system events
        
        [Required]
        [StringLength(20)]
        public string Role { get; set; } // "Admin", "Voter", "System", etc.
        
        [Required]
        [StringLength(100)]
        public string Action { get; set; } // "Login Attempt", "Vote Cast", etc.
        
        [Required]
        [StringLength(500)]
        public string Description { get; set; }
        
        [StringLength(45)]
        public string IpAddress { get; set; }
        
        [StringLength(500)]
        public string UserAgent { get; set; }
        
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        [Required]
        [StringLength(20)]
        public string Status { get; set; } // "Success", "Failed", "Attempt"
        
        [StringLength(1000)]
        public string AdditionalData { get; set; }
    }
}