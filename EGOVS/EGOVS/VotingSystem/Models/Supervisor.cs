using System;
using System.ComponentModel.DataAnnotations;

namespace VotingSystem.Models
{
    public class Supervisor
    {
        [Key]
        [Required]
        [StringLength(50)]
        public string NationalId { get; set; }

        [Required]
        [StringLength(50)]
        public string Nationality { get; set; } = "Ethiopian";

        [Required]
        [StringLength(100)]
        public string Region { get; set; }

        [Required]
        [StringLength(50)]
        public string PhoneNumber { get; set; }

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(100)]
        public string MiddleName { get; set; }

        [Required]
        [StringLength(100)]
        public string LastName { get; set; }

        [Required]
        [Range(18, 120)]
        public int Age { get; set; }

        [Required]
        [StringLength(20)]
        public string Sex { get; set; }

        [Required]
        [StringLength(255)]
        public string Password { get; set; }

        [StringLength(100)]
        public string Email { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}