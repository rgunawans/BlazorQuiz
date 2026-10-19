using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using BlazorQuiz.Models;

namespace BlazorQuiz.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        public DbSet<Quiz> Quizzes { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<QuizSession> QuizSessions { get; set; }
        public DbSet<QuizParticipant> QuizParticipants { get; set; }
        public DbSet<QuizAnswer> QuizAnswers { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Quiz entity
            modelBuilder.Entity<Quiz>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.CreatedBy).HasMaxLength(450);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.IsActive);
            });

            // Configure Question entity
            modelBuilder.Entity<Question>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Text).IsRequired().HasMaxLength(1000);
                entity.Property(e => e.OptionsJson).IsRequired();
                entity.HasIndex(e => new { e.QuizId, e.OrderIndex });

                entity.HasOne(q => q.Quiz)
                      .WithMany(quiz => quiz.Questions)
                      .HasForeignKey(q => q.QuizId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure QuizSession entity
            modelBuilder.Entity<QuizSession>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SessionCode).IsRequired().HasMaxLength(100);
                entity.Property(e => e.HostUserId).IsRequired().HasMaxLength(450);
                entity.HasIndex(e => e.SessionCode).IsUnique();
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.StartedAt);

                entity.HasOne(s => s.Quiz)
                      .WithMany(q => q.QuizSessions)
                      .HasForeignKey(s => s.QuizId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure QuizParticipant entity
            modelBuilder.Entity<QuizParticipant>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.PlayerName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ConnectionId).HasMaxLength(200);
                entity.HasIndex(e => new { e.QuizSessionId, e.PlayerName }).IsUnique();
                entity.HasIndex(e => e.ConnectionId);

                entity.HasOne(p => p.QuizSession)
                      .WithMany(s => s.Participants)
                      .HasForeignKey(p => p.QuizSessionId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure QuizAnswer entity
            modelBuilder.Entity<QuizAnswer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.QuizSessionId, e.QuestionId, e.ParticipantId }).IsUnique();
                entity.HasIndex(e => e.AnsweredAt);
                entity.HasIndex(e => e.IsCorrect);

                entity.HasOne(a => a.QuizSession)
                      .WithMany(s => s.Answers)
                      .HasForeignKey(a => a.QuizSessionId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.Question)
                      .WithMany(q => q.QuizAnswers)
                      .HasForeignKey(a => a.QuestionId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(a => a.Participant)
                      .WithMany(p => p.Answers)
                      .HasForeignKey(a => a.ParticipantId)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            // Configure ActivityLog entity
            modelBuilder.Entity<ActivityLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Activity).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Details).HasMaxLength(500);
                entity.Property(e => e.Level).IsRequired().HasMaxLength(100);
                entity.Property(e => e.UserId).HasMaxLength(450);
                entity.Property(e => e.UserName).HasMaxLength(100);
                entity.Property(e => e.Source).HasMaxLength(200);
                entity.Property(e => e.IpAddress).HasMaxLength(50);
                entity.Property(e => e.UserAgent).HasMaxLength(500);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.Level);
                entity.HasIndex(e => e.Source);
            });
        }
    }
}
