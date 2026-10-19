using System.ComponentModel.DataAnnotations;

namespace BlazorQuiz.Models
{
    public class ActivityLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Activity { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Details { get; set; }

        [Required]
        [StringLength(100)]
        public string Level { get; set; } = "Info"; // Info, Warning, Error

        [StringLength(450)]
        public string? UserId { get; set; }

        [StringLength(100)]
        public string? UserName { get; set; }

        [StringLength(200)]
        public string? Source { get; set; } // e.g., "QuizHost", "QuizUser", "QuestionEditor"

        public DateTime CreatedAt { get; set; } // Now stores Indonesian time (GMT+7)

        [StringLength(50)]
        public string? IpAddress { get; set; }

        [StringLength(500)]
        public string? UserAgent { get; set; }

        // Helper property to display formatted Indonesian time
        public string FormattedCreatedAt => CreatedAt.ToString("dd/MM/yyyy HH:mm:ss") + " WIB";
    }
}