using BlazorQuiz.Data;
using BlazorQuiz.Models;
using BlazorQuiz.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace BlazorQuiz.Tests;

public sealed class QuizServiceScoringTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly QuizService _service;
    private readonly FakeEventBus _bus = new();

    public QuizServiceScoringTests()
    {
        _connection.Open();
        var factory = new TestFactory(_connection);
        using (var db = factory.CreateDbContext())
        {
            db.Database.EnsureCreated();
        }

        _service = new QuizService(factory, new FixedTimeZone(), new MemoryCache(new MemoryCacheOptions()), _bus);
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Second_answer_for_same_question_is_ignored_and_scored_once()
    {
        var (sessionId, questionId, participantId) = await SeedAsync();
        _bus.ElapsedMs = 1000;

        var first = await _service.SubmitAnswerAsync(sessionId, questionId, participantId, 1);
        var second = await _service.SubmitAnswerAsync(sessionId, questionId, participantId, 1);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(90, first.Points); // 100 - 1000/100
        var leaderboard = await _service.GetLeaderboardAsync(sessionId);
        Assert.Equal(90, leaderboard.Single().TotalScore);
        Assert.Equal(1, leaderboard.Single().TotalAnswers);
    }

    [Fact]
    public async Task Answer_after_deadline_scores_zero()
    {
        var (sessionId, questionId, participantId) = await SeedAsync();
        _bus.ElapsedMs = QuizService.QuestionTimeMs + 5000;

        var answer = await _service.SubmitAnswerAsync(sessionId, questionId, participantId, 1);

        Assert.False(answer.IsCorrect);
        Assert.Equal(0, answer.Points);
    }

    [Fact]
    public async Task Slowest_correct_answer_gets_minimum_points()
    {
        var (sessionId, questionId, participantId) = await SeedAsync();
        _bus.ElapsedMs = QuizService.QuestionTimeMs;

        var answer = await _service.SubmitAnswerAsync(sessionId, questionId, participantId, 1);

        Assert.True(answer.IsCorrect);
        Assert.Equal(10, answer.Points); // floor of the speed bonus
    }

    [Fact]
    public async Task Answer_from_participant_of_another_session_is_rejected()
    {
        var (sessionId, questionId, _) = await SeedAsync();
        var (_, _, otherParticipantId) = await SeedAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SubmitAnswerAsync(sessionId, questionId, otherParticipantId, 1));
    }

    [Fact]
    public async Task Update_removes_deleted_questions_and_keeps_edits()
    {
        var quiz = await _service.CreateQuizAsync(new Quiz
        {
            Title = "T",
            Questions =
            {
                new Question { Text = "A", Options = ["a", "b"], CorrectOptionIndex = 0 },
                new Question { Text = "B", Options = ["a", "b"], CorrectOptionIndex = 1 }
            }
        });

        var edit = new Quiz { Id = quiz.Id, Title = "T2", Questions = { new Question { Id = quiz.Questions[1].Id, Text = "B2", Options = ["a", "b"], CorrectOptionIndex = 1 } } };
        await _service.UpdateQuizAsync(edit);

        var saved = await _service.GetQuizByIdAsync(quiz.Id);
        Assert.Equal("T2", saved!.Title);
        Assert.Equal("B2", saved.Questions.Single().Text);
    }

    private async Task<(int SessionId, int QuestionId, int ParticipantId)> SeedAsync()
    {
        var quiz = await _service.CreateQuizAsync(new Quiz
        {
            Title = "Test",
            Questions = { new Question { Text = "Q", Options = ["a", "b"], CorrectOptionIndex = 1 } }
        });
        var session = await _service.CreateQuizSessionAsync(quiz.Id, "host");
        var participant = await _service.AddParticipantAsync(session.Id, "player", "conn");
        return (session.Id, quiz.Questions.Single().Id, participant.Id);
    }

    private sealed class FakeEventBus : IQuizEventBus
    {
        public int ElapsedMs { get; set; }
        public int? GetElapsedMs(int sessionId, int questionId) => ElapsedMs;
        public IDisposable Subscribe(int sessionId, Func<QuizEvent, Task> handler) => throw new NotSupportedException();
        public Task PublishAsync(QuizEvent quizEvent) => Task.CompletedTask;
        public void RequestLeaderboardRefresh(int sessionId) { }
        public void StartQuestion(int sessionId, int questionId) { }
        public void RequestNextQuestion(int sessionId) { }
        public bool ConsumeNextQuestionRequest(int sessionId) => false;
    }

    private sealed class FixedTimeZone : ITimeZoneService
    {
        public DateTime GetIndonesianTime() => new(2026, 1, 1, 12, 0, 0);
        public DateTime ConvertUtcToIndonesian(DateTime utcDateTime) => utcDateTime.AddHours(7);
        public string GetIndonesianTimeZoneId() => "SE Asia Standard Time";
        public void LogTimeZoneInfo() { }
    }

    private sealed class TestFactory(SqliteConnection connection) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
    }
}
