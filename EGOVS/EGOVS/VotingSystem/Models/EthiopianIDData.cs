using System;
using System.ComponentModel.DataAnnotations;

namespace VotingSystem.Models
{
    public class EthiopianIDData
    {
        public string NationalId { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public string PhoneNumber { get; set; }
        public string Nationality { get; set; }
        public string Region { get; set; }
        public string Sex { get; set; }
        public int Age { get; set; }
        public string DateOfBirth { get; set; }
        public string CalendarType { get; set; }
        
        // Confidence property completely removed
        public bool Success { get; set; }
        public string Error { get; set; }
    }
}