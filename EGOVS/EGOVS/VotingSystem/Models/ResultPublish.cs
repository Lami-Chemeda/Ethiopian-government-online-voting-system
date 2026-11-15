using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VotingSystem.Models
{
    [Table("ResultPublishes")]
    public class ResultPublish
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ResultId { get; set; }

        [Required]
        [StringLength(50)]
        public string CandidateNationalId { get; set; }

        [Required]
        [StringLength(300)]
        public string CandidateName { get; set; }

        [Required]
        [StringLength(100)]
        public string Party { get; set; }

        [Required]
        public int VoteCount { get; set; }

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal Percentage { get; set; }

        [Required]
        public bool IsWinner { get; set; } = false;

        [Required]
        public bool IsApproved { get; set; } = false;

        [StringLength(100)]
        public string ApprovedBy { get; set; }

        public DateTime? ApprovedDate { get; set; }

        [Required]
        public DateTime PublishedDate { get; set; } = DateTime.Now;

        // Navigation property
        [ForeignKey("CandidateNationalId")]
        public virtual Candidate Candidate { get; set; }
    }
}