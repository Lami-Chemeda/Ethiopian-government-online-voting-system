using System;
using System.ComponentModel.DataAnnotations;

namespace VotingSystem.Models
{
    public class SecurityAlert
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string AlertType { get; set; } // "Failed Login", "Suspicious Activity", etc.
        
        [Required]
        [StringLength(500)]
        public string Description { get; set; }
        
        [Required]
        [StringLength(20)]
        public string Severity { get; set; } // "Critical", "High", "Medium", "Low"
        
        public DateTime AlertDate { get; set; } = DateTime.Now;
        
        public bool IsResolved { get; set; } = false;
        
        public string ResolvedBy { get; set; }
        
        public DateTime? ResolvedDate { get; set; }
        
        [StringLength(1000)]
        public string AdditionalData { get; set; } // JSON data with details
        
        public string NationalId { get; set; } // Related user if any
        
        public string Role { get; set; } // User role
    }
}