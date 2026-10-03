using System.ComponentModel.DataAnnotations;

namespace VotingSystem.Models
{
    public class VoterRegistrationDto
    {
        [Required(ErrorMessage = "National ID is required")]
        public string NationalId { get; set; }

        [Required(ErrorMessage = "First name is required")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Middle name is required")]
        public string MiddleName { get; set; }

        [Required(ErrorMessage = "Last name is required")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Nationality is required")]
        public string Nationality { get; set; } = "Ethiopian";

        [Required(ErrorMessage = "Region is required")]
        public string Region { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [RegularExpression(@"^(?:\+251|0)?9\d{8}$", ErrorMessage = "Please enter a valid Ethiopian phone number")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Age is required")]
        [Range(18, 120, ErrorMessage = "Age must be between 18 and 120")]
        public int Age { get; set; }

        [Required(ErrorMessage = "Sex is required")]
        public string Sex { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Confirm password is required")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; }

        [Required(ErrorMessage = "Face image is required")]
        public IFormFile FaceImage { get; set; }
    }
}