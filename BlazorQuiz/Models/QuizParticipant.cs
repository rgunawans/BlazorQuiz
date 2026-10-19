using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlazorQuiz.Models;

public class QuizParticipant
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int QuizSessionId { get; set; }

    [Required]
    [StringLength(100)]
    public string PlayerName { get; set; } = string.Empty;

    [StringLength(200)]
    public string ConnectionId { get; set; } = string.Empty;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    public int TotalCorrectAnswers { get; set; } = 0;

    public int TotalAnswers { get; set; } = 0;

    public int TotalScore { get; set; } = 0;

    // Navigation property
    [ForeignKey("QuizSessionId")]
    public virtual QuizSession QuizSession { get; set; } = null!;

    public virtual ICollection<QuizAnswer> Answers { get; set; } = new List<QuizAnswer>();
}