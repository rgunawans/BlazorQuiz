using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlazorQuiz.Models;

public class Question
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int QuizId { get; set; }

    [Required]
    [StringLength(1000)]
    public string Text { get; set; } = string.Empty;

    [Required]
    public string OptionsJson { get; set; } = string.Empty;

    [Required]
    [Range(0, int.MaxValue)]
    public int CorrectOptionIndex { get; set; }

    public int OrderIndex { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    [ForeignKey("QuizId")]
    public virtual Quiz Quiz { get; set; } = null!;

    // Navigation property for answers
    public virtual ICollection<QuizAnswer> QuizAnswers { get; set; } = new List<QuizAnswer>();

    // Helper property that's not mapped to database
    [NotMapped]
    public List<string> Options
    {
        get => string.IsNullOrEmpty(OptionsJson) 
            ? new List<string>() 
            : System.Text.Json.JsonSerializer.Deserialize<List<string>>(OptionsJson) ?? new List<string>();
        set => OptionsJson = System.Text.Json.JsonSerializer.Serialize(value);
    }
}