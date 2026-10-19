using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlazorQuiz.Models;

public class QuizSession
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int QuizId { get; set; }

    [Required]
    [StringLength(100)]
    public string SessionCode { get; set; } = string.Empty;

    [Required]
    [StringLength(450)] // Standard ASP.NET Identity user id length
    public string HostUserId { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? EndedAt { get; set; }

    public bool IsActive { get; set; } = true;

    public int CurrentQuestionIndex { get; set; } = -1;

    public DateTime? CurrentQuestionStartTime { get; set; }

    // Navigation properties
    [ForeignKey("QuizId")]
    public virtual Quiz Quiz { get; set; } = null!;

    public virtual ICollection<QuizParticipant> Participants { get; set; } = new List<QuizParticipant>();
    
    public virtual ICollection<QuizAnswer> Answers { get; set; } = new List<QuizAnswer>();
}