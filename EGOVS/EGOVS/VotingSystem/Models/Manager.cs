using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VotingSystem.Models
{
    [Table("Managers")]
    public class Manager
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

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string Email { get; set; }

        [StringLength(100)]
        public string Username { get; set; }

        public bool IsActive { get; set; } = true;
    }
}