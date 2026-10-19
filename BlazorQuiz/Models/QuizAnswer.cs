using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlazorQuiz.Models;

public class QuizAnswer
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int QuizSessionId { get; set; }

    [Required]
    public int QuestionId { get; set; }

    [Required]
    public int ParticipantId { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int SelectedOptionIndex { get; set; }

    public bool IsCorrect { get; set; }

    public int TimeTakenMs { get; set; } = 0;

    public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;

    public int Points { get; set; } = 0;

    // Navigation properties
    [ForeignKey("QuizSessionId")]
    public virtual QuizSession QuizSession { get; set; } = null!;

    [ForeignKey("QuestionId")]
    public virtual Question Question { get; set; } = null!;

    [ForeignKey("ParticipantId")]
    public virtual QuizParticipant Participant { get; set; } = null!;
}