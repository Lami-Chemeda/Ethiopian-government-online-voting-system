using Microsoft.EntityFrameworkCore;
using VotingSystem.Models;

namespace VotingSystem.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Your existing DbSets
        public DbSet<Voter> Voters { get; set; }
        public DbSet<Candidate> Candidates { get; set; }
        public DbSet<Manager> Managers { get; set; }
        public DbSet<Admin> Admins { get; set; }
        public DbSet<Supervisor> Supervisors { get; set; }
        public DbSet<Vote> Votes { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<ResultPublish> ResultPublishes { get; set; }
        public DbSet<SecurityAlert> SecurityAlerts { get; set; }
        public DbSet<SystemActivityLog> SystemActivityLogs { get; set; }
        // NEW: Add ElectionSettings DbSet
        public DbSet<ElectionSettings> ElectionSettings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // NEW: Add configuration for ElectionSettings
            modelBuilder.Entity<ElectionSettings>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.Property(e => e.ElectionName)
                      .IsRequired()
                      .HasMaxLength(200)
                      .HasDefaultValue("Ethiopian National Election 2024");

                entity.Property(e => e.StartDate)
                      .IsRequired();

                entity.Property(e => e.EndDate)
                      .IsRequired();

                entity.Property(e => e.IsActive)
                      .HasDefaultValue(false);

                entity.Property(e => e.Region)
                      .IsRequired()
                      .HasMaxLength(100)
                      .HasDefaultValue("All Regions");

                entity.Property(e => e.ResultsPublished)
                      .HasDefaultValue(false);

                entity.Property(e => e.CreatedAt)
                      .HasDefaultValueSql("GETDATE()");

                entity.Property(e => e.UpdatedAt)
                      .HasDefaultValueSql("GETDATE()");
            });

            // Your existing configurations remain exactly the same
            // Configure Voter entity
            modelBuilder.Entity<Voter>(entity =>
            {
                entity.HasKey(v => v.NationalId);
                
                entity.HasIndex(v => v.NationalId)
                      .HasDatabaseName("IX_Voters_NationalId");
                      
                entity.HasIndex(v => v.PhoneNumber)
                      .HasDatabaseName("IX_Voters_PhoneNumber")
                      .IsUnique();

                entity.Property(v => v.NationalId)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(v => v.Nationality)
                      .IsRequired()
                      .HasMaxLength(50)
                      .HasDefaultValue("Ethiopian");

                entity.Property(v => v.Region)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(v => v.PhoneNumber)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(v => v.FirstName)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(v => v.MiddleName)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(v => v.LastName)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(v => v.Age)
                      .IsRequired();

                entity.Property(v => v.Sex)
                      .IsRequired()
                      .HasMaxLength(20);

                entity.Property(v => v.Password)
                      .IsRequired()
                      .HasMaxLength(255);

                entity.Property(v => v.Literate)
                      .IsRequired()
                      .HasMaxLength(50)
                      .HasDefaultValue("Yes");

                // === VISUAL LOGIN FIELDS ===
                entity.Property(v => v.QRCodeData)
                      .HasMaxLength(500)
                      .HasDefaultValueSql("NEWID()");

                entity.Property(v => v.VisualPIN)
                      .HasMaxLength(50)
                      .HasDefaultValue("🦁,☕,🌾,🏠");

                entity.Property(v => v.PrefersVisualLogin)
                      .HasDefaultValue(true);

                entity.Property(v => v.RegisterDate)
                      .HasDefaultValueSql("GETDATE()");

                entity.Property(v => v.CreatedAt)
                      .HasDefaultValueSql("GETDATE()");

                entity.Property(v => v.UpdatedAt)
                      .HasDefaultValueSql("GETDATE()");
            });

            // Configure Candidate entity
            modelBuilder.Entity<Candidate>(entity =>
            {
                entity.HasKey(c => c.NationalId);
                
                entity.HasIndex(c => c.NationalId)
                      .HasDatabaseName("IX_Candidates_NationalId");
                      
                entity.HasIndex(c => c.PhoneNumber)
                      .HasDatabaseName("IX_Candidates_PhoneNumber")
                      .IsUnique();

                entity.Property(c => c.NationalId)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(c => c.Nationality)
                      .IsRequired()
                      .HasMaxLength(50)
                      .HasDefaultValue("Ethiopian");

                entity.Property(c => c.Region)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(c => c.PhoneNumber)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(c => c.FirstName)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(c => c.MiddleName)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(c => c.LastName)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(c => c.Age)
                      .IsRequired();

                entity.Property(c => c.Sex)
                      .IsRequired()
                      .HasMaxLength(20);

                entity.Property(c => c.Password)
                      .IsRequired()
                      .HasMaxLength(255);

                entity.Property(c => c.Party)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(c => c.Bio)
                      .HasColumnType("NVARCHAR(MAX)");

                entity.Property(c => c.PhotoUrl)
                      .HasMaxLength(500);

                entity.Property(c => c.Logo)
                      .HasMaxLength(500);

                // === VISUAL VOTING FIELDS ===
                entity.Property(c => c.SymbolName)
                      .IsRequired()
                      .HasMaxLength(100)
                      .HasDefaultValue("Lion");

                entity.Property(c => c.SymbolImagePath)
                      .HasMaxLength(500)
                      .HasDefaultValue("/images/symbols/lion.png");

                entity.Property(c => c.SymbolUnicode)
                      .HasMaxLength(50)
                      .HasDefaultValue("🦁");

                entity.Property(c => c.PartyColor)
                      .HasMaxLength(50)
                      .HasDefaultValue("#1d3557");

                entity.Property(c => c.IsActive)
                      .HasDefaultValue(true);

                entity.Property(c => c.CreatedAt)
                      .HasDefaultValueSql("GETDATE()");

                entity.Property(c => c.UpdatedAt)
                      .HasDefaultValueSql("GETDATE()");
            });

            // Configure Manager entity
            modelBuilder.Entity<Manager>(entity =>
            {
                entity.HasKey(m => m.NationalId);
                
                entity.HasIndex(m => m.NationalId)
                      .HasDatabaseName("IX_Managers_NationalId");
                      
                entity.HasIndex(m => m.PhoneNumber)
                      .HasDatabaseName("IX_Managers_PhoneNumber")
                      .IsUnique();

                entity.Property(m => m.NationalId)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(m => m.Nationality)
                      .IsRequired()
                      .HasMaxLength(50)
                      .HasDefaultValue("Ethiopian");

                entity.Property(m => m.Region)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(m => m.PhoneNumber)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(m => m.FirstName)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(m => m.MiddleName)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(m => m.LastName)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(m => m.Age)
                      .IsRequired();

                entity.Property(m => m.Sex)
                      .IsRequired()
                      .HasMaxLength(20);

                entity.Property(m => m.Password)
                      .IsRequired()
                      .HasMaxLength(255);

                entity.Property(m => m.Email)
                      .HasMaxLength(100);

                entity.Property(m => m.Username)
                      .HasMaxLength(100);

                entity.Property(m => m.IsActive)
                      .HasDefaultValue(true);

                entity.Property(m => m.CreatedAt)
                      .HasDefaultValueSql("GETDATE()");

                entity.Property(m => m.UpdatedAt)
                      .HasDefaultValueSql("GETDATE()");
            });

            // Configure Admin entity
            modelBuilder.Entity<Admin>(entity =>
            {
                entity.HasKey(a => a.NationalId);
                
                entity.HasIndex(a => a.NationalId)
                      .HasDatabaseName("IX_Admins_NationalId");
                      
                entity.HasIndex(a => a.PhoneNumber)
                      .HasDatabaseName("IX_Admins_PhoneNumber")
                      .IsUnique();

                entity.Property(a => a.NationalId)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(a => a.Nationality)
                      .IsRequired()
                      .HasMaxLength(50)
                      .HasDefaultValue("Ethiopian");

                entity.Property(a => a.Region)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(a => a.PhoneNumber)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(a => a.FirstName)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(a => a.MiddleName)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(a => a.LastName)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(a => a.Age)
                      .IsRequired();

                entity.Property(a => a.Sex)
                      .IsRequired()
                      .HasMaxLength(20);

                entity.Property(a => a.Password)
                      .IsRequired()
                      .HasMaxLength(255);
            });

            // Configure Supervisor entity
            modelBuilder.Entity<Supervisor>(entity =>
            {
                entity.HasKey(s => s.NationalId);
                
                entity.HasIndex(s => s.NationalId)
                      .HasDatabaseName("IX_Supervisors_NationalId");
                      
                entity.HasIndex(s => s.PhoneNumber)
                      .HasDatabaseName("IX_Supervisors_PhoneNumber")
                      .IsUnique();

                entity.Property(s => s.NationalId)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(s => s.Nationality)
                      .IsRequired()
                      .HasMaxLength(50)
                      .HasDefaultValue("Ethiopian");

                entity.Property(s => s.Region)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(s => s.PhoneNumber)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(s => s.FirstName)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(s => s.MiddleName)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(s => s.LastName)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(s => s.Age)
                      .IsRequired();

                entity.Property(s => s.Sex)
                      .IsRequired()
                      .HasMaxLength(20);

                entity.Property(s => s.Password)
                      .IsRequired()
                      .HasMaxLength(255);

                entity.Property(s => s.Email)
                      .HasMaxLength(100);

                entity.Property(s => s.IsActive)
                      .HasDefaultValue(true);

                entity.Property(s => s.CreatedAt)
                      .HasDefaultValueSql("GETDATE()");

                entity.Property(s => s.UpdatedAt)
                      .HasDefaultValueSql("GETDATE()");
            });

            // Configure Vote entity
            modelBuilder.Entity<Vote>(entity =>
            {
                entity.HasKey(v => v.Id);
                
                entity.Property(v => v.VoterNationalId)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(v => v.CandidateNationalId)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(v => v.VoteDate)
                      .HasDefaultValueSql("GETDATE()");

                entity.Property(v => v.IPAddress)
                      .HasMaxLength(45);

                // Configure relationships
                entity.HasOne(v => v.Voter)
                      .WithMany()
                      .HasForeignKey(v => v.VoterNationalId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(v => v.Candidate)
                      .WithMany(c => c.Votes)
                      .HasForeignKey(v => v.CandidateNationalId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure Comment entity
            modelBuilder.Entity<Comment>(entity =>
            {
                entity.HasKey(c => c.Id);
                
                entity.Property(c => c.Content)
                      .IsRequired()
                      .HasMaxLength(1000);

                entity.Property(c => c.SenderType)
                      .IsRequired()
                      .HasMaxLength(20);

                entity.Property(c => c.SenderNationalId)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(c => c.SenderName)
                      .HasMaxLength(200);

                entity.Property(c => c.ReceiverType)
                      .IsRequired()
                      .HasMaxLength(20);

                entity.Property(c => c.ReceiverNationalId)
                      .HasMaxLength(50);

                entity.Property(c => c.ReceiverName)
                      .HasMaxLength(200);

                entity.Property(c => c.CommentType)
                      .HasMaxLength(50);

                entity.Property(c => c.CreatedAt)
                      .HasDefaultValueSql("GETDATE()");

                // REMOVE the Voter relationship since we don't need it for the new structure
                // No navigation properties needed for the hierarchical comment system
            });
          // Configure SecurityAlert entity
modelBuilder.Entity<SecurityAlert>(entity =>
{
    entity.HasKey(s => s.Id);
    
    entity.Property(s => s.AlertType)
          .IsRequired()
          .HasMaxLength(100);

    entity.Property(s => s.Description)
          .IsRequired()
          .HasMaxLength(500);

    entity.Property(s => s.Severity)
          .IsRequired()
          .HasMaxLength(20);

    entity.Property(s => s.AlertDate)
          .HasDefaultValueSql("GETDATE()");

    entity.Property(s => s.IsResolved)
          .HasDefaultValue(false);

    entity.Property(s => s.ResolvedBy)
          .HasMaxLength(50);

    entity.Property(s => s.NationalId)
          .HasMaxLength(50);

    entity.Property(s => s.Role)
          .HasMaxLength(20);

    entity.Property(s => s.AdditionalData)
          .HasMaxLength(1000);
});

// Configure SystemActivityLog entity
modelBuilder.Entity<SystemActivityLog>(entity =>
{
    entity.HasKey(s => s.Id);
    
    entity.Property(s => s.NationalId)
          .HasMaxLength(50);

    entity.Property(s => s.Role)
          .IsRequired()
          .HasMaxLength(20);

    entity.Property(s => s.Action)
          .IsRequired()
          .HasMaxLength(100);

    entity.Property(s => s.Description)
          .IsRequired()
          .HasMaxLength(500);

    entity.Property(s => s.IpAddress)
          .HasMaxLength(45);

    entity.Property(s => s.UserAgent)
          .HasMaxLength(500);

    entity.Property(s => s.Timestamp)
          .HasDefaultValueSql("GETDATE()");

    entity.Property(s => s.Status)
          .IsRequired()
          .HasMaxLength(20);

    entity.Property(s => s.AdditionalData)
          .HasMaxLength(1000);
});
            // Configure ResultPublish entity
            modelBuilder.Entity<ResultPublish>(entity =>
            {
                entity.HasKey(r => r.ResultId);
                
                entity.Property(r => r.ResultId)
                      .ValueGeneratedOnAdd()
                      .HasColumnName("ResultId");

                entity.Property(r => r.CandidateNationalId)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(r => r.CandidateName)
                      .IsRequired()
                      .HasMaxLength(300);

                entity.Property(r => r.Party)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(r => r.VoteCount)
                      .IsRequired();

                entity.Property(r => r.Percentage)
                      .IsRequired()
                      .HasColumnType("decimal(5,2)");

                entity.Property(r => r.IsWinner)
                      .IsRequired()
                      .HasDefaultValue(false);

                entity.Property(r => r.IsApproved)
                      .IsRequired()
                      .HasDefaultValue(false);

                entity.Property(r => r.ApprovedBy)
                      .HasMaxLength(100);

                entity.Property(r => r.ApprovedDate)
                      .IsRequired(false);

                entity.Property(r => r.PublishedDate)
                      .IsRequired()
                      .HasDefaultValueSql("GETDATE()");

                // Configure relationship with Candidate
                entity.HasOne(r => r.Candidate)
                      .WithMany()
                      .HasForeignKey(r => r.CandidateNationalId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
