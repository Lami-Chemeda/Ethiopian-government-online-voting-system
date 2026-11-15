using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VotingSystem.Models
{
    [Table("Candidates")]
    public class Candidate
    {
        [Key]
        [Required(ErrorMessage = "National ID is required")]
        [StringLength(50, ErrorMessage = "National ID cannot exceed 50 characters")]
        public string NationalId { get; set; }

        [Required(ErrorMessage = "Nationality is required")]
        [StringLength(50, ErrorMessage = "Nationality cannot exceed 50 characters")]
        public string Nationality { get; set; } = "Ethiopian";

        [Required(ErrorMessage = "Region is required")]
        [StringLength(100, ErrorMessage = "Region cannot exceed 100 characters")]
        public string Region { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [StringLength(50, ErrorMessage = "Phone number cannot exceed 50 characters")]
        [Phone(ErrorMessage = "Invalid phone number format")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "First name is required")]
        [StringLength(100, ErrorMessage = "First name cannot exceed 100 characters")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Middle name is required")]
        [StringLength(100, ErrorMessage = "Middle name cannot exceed 100 characters")]
        public string MiddleName { get; set; }

        [Required(ErrorMessage = "Last name is required")]
        [StringLength(100, ErrorMessage = "Last name cannot exceed 100 characters")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Age is required")]
        [Range(18, 120, ErrorMessage = "Age must be between 18 and 120")]
        public int Age { get; set; }

        [Required(ErrorMessage = "Sex is required")]
        [StringLength(20, ErrorMessage = "Sex cannot exceed 20 characters")]
        [RegularExpression("^(Male|Female)$", ErrorMessage = "Sex must be either Male or Female")]
        public string Sex { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [StringLength(255, ErrorMessage = "Password cannot exceed 255 characters")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters long")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Party is required")]
        [StringLength(100, ErrorMessage = "Party cannot exceed 100 characters")]
        public string Party { get; set; }

        public string Bio { get; set; }

        [StringLength(500, ErrorMessage = "Photo URL cannot exceed 500 characters")]
        public string PhotoUrl { get; set; }

        [StringLength(500, ErrorMessage = "Logo URL cannot exceed 500 characters")]
        public string Logo { get; set; }

        // === VISUAL VOTING FIELDS ===
        [Required]
        [StringLength(100)]
        public string SymbolName { get; set; } = "Lion";

        [StringLength(500)]
        public string SymbolImagePath { get; set; } = "/images/symbols/lion.png";

        [StringLength(50)]
        public string SymbolUnicode { get; set; } = "🦁";

        [StringLength(50)]
        public string PartyColor { get; set; } = "#1d3557";

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public virtual ICollection<Vote> Votes { get; set; }

        // Computed property for full name
        [NotMapped]
        public string FullName => $"{FirstName} {MiddleName} {LastName}";
    }
}