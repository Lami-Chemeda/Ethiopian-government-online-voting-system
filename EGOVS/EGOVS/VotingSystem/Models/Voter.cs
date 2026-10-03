using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VotingSystem.Models
{
    [Table("Voters")]
    public class Voter
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

        [Required(ErrorMessage = "Literate status is required")]
        [StringLength(50, ErrorMessage = "Literate status cannot exceed 50 characters")]
        public string Literate { get; set; } = "Yes";

        public DateTime RegisterDate { get; set; } = DateTime.Now;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string QRCodeData { get; set; } = Guid.NewGuid().ToString();

        [StringLength(50)]
        public string VisualPIN { get; set; } = "🦁,☕,🌾,🏠";

        public bool PrefersVisualLogin { get; set; } = true;

        // Computed property for full name
        [NotMapped]
        public string FullName => $"{FirstName} {MiddleName} {LastName}";
    }
}