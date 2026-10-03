using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace VotingSystem.Models
{
    public class VisualLoginModel
    {
        public string NationalId { get; set; }
        
        [Required]
        public List<string> SelectedImages { get; set; } = new List<string>();
        
        public IFormFile NationalIdFront { get; set; }
        public IFormFile NationalIdBack { get; set; }
        
        public string ExtractedNationalId { get; set; }
    }
}