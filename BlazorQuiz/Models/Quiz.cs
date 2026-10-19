using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlazorQuiz.Models;

public class Quiz
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }

    [StringLength(450)] // Standard ASP.NET Identity user id length
    public string? CreatedBy { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation property - keeping as List for compatibility with existing code
    public virtual List<Question> Questions { get; set; } = new List<Question>();
    
    // Navigation property for quiz sessions
    public virtual ICollection<QuizSession> QuizSessions { get; set; } = new List<QuizSession>();
}