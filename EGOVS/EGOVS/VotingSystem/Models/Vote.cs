using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VotingSystem.Models
{
    public class Vote
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string VoterNationalId { get; set; }
        
        [Required]
        [StringLength(50)]
        public string CandidateNationalId { get; set; }
        
        [Required]
        public DateTime VoteDate { get; set; } = DateTime.Now;
        
        [StringLength(45)]
        public string IPAddress { get; set; }
        
        // Navigation properties
        [ForeignKey("VoterNationalId")]
        public virtual Voter Voter { get; set; }
        
        [ForeignKey("CandidateNationalId")]
        public virtual Candidate Candidate { get; set; }
    }
}