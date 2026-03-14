using System;
using System.ComponentModel.DataAnnotations;

namespace VotingSystem.Models
{
    public class ElectionSettings
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Election name is required")]
        [StringLength(200, ErrorMessage = "Election name cannot exceed 200 characters")]
        [Display(Name = "Election Name")]
        public string ElectionName { get; set; } = "Ethiopian National Election 2024";

        [Required(ErrorMessage = "Start date and time is required")]
        [Display(Name = "Start Date & Time")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "End date and time is required")]
        [Display(Name = "End Date & Time")]
        public DateTime EndDate { get; set; }

        [Display(Name = "Active Election")]
        public bool IsActive { get; set; } = false;

        [Required]
        [StringLength(100)]
        public string Region { get; set; } = "All Regions";

        [Display(Name = "Results Published")]
        public bool ResultsPublished { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Validation method to ensure end time is after start time
        public bool IsValidTimeRange()
        {
            return EndDate > StartDate;
        }

        // Check if election is currently ongoing
        public bool IsElectionOngoing()
        {
            var now = DateTime.Now;
            return IsActive && now >= StartDate && now <= EndDate;
        }

        // Check if election has ended
        public bool HasElectionEnded()
        {
            return IsActive && DateTime.Now > EndDate;
        }

        // Check if election hasn't started yet
        public bool HasElectionNotStarted()
        {
            return IsActive && DateTime.Now < StartDate;
        }

        // Get time remaining until election starts
        public TimeSpan TimeUntilStart()
        {
            return StartDate - DateTime.Now;
        }

        // Get time remaining until election ends
        public TimeSpan TimeUntilEnd()
        {
            return EndDate - DateTime.Now;
        }

        // Check if voting is allowed right now
        public bool IsVotingAllowed()
        {
            return IsActive && DateTime.Now >= StartDate && DateTime.Now <= EndDate;
        }

        // Get current election status with detailed message
        public string GetVotingStatusMessage()
        {
            if (!IsActive)
                return "Election is not active. Voting is disabled.";

            var now = DateTime.Now;
            
            if (now < StartDate)
            {
                var timeLeft = TimeUntilStart();
                return $"Voting will start in {timeLeft.Days} days, {timeLeft.Hours} hours, and {timeLeft.Minutes} minutes.";
            }
            else if (now > EndDate)
            {
                return "Voting period has ended. You can no longer cast your vote.";
            }
            else
            {
                var timeLeft = TimeUntilEnd();
                return $"Voting is ongoing. {timeLeft.Hours} hours and {timeLeft.Minutes} minutes remaining.";
            }
        }
    }
}