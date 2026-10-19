namespace BlazorQuiz.Models;

public class QuestionDto
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public List<string> Options { get; set; } = new List<string>();
    public int CorrectOptionIndex { get; set; }
    public int OrderIndex { get; set; }

    public static QuestionDto FromQuestion(Question question)
    {
        return new QuestionDto
        {
            Id = question.Id,
            Text = question.Text,
            Options = question.Options.ToList(),
            CorrectOptionIndex = question.CorrectOptionIndex,
            OrderIndex = question.OrderIndex
        };
    }
}