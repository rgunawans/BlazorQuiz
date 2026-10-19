namespace BlazorQuiz.Models;

public class LeaderboardEntry
{
    public int Rank { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public int TotalScore { get; set; }
    public int CorrectAnswers { get; set; }
    public int TotalAnswers { get; set; }
    public double Accuracy { get; set; }
    public int AverageTimeMs { get; set; }
    public DateTime LastAnswerTime { get; set; }
}

public class QuestionLeaderboardEntry
{
    public string UserName { get; set; } = string.Empty;
    public int TimeTaken { get; set; } // ms
    public bool IsCorrect { get; set; }
    public int Points { get; set; }
}