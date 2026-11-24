using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using BlazorQuiz.Data;
using BlazorQuiz.Models;

namespace BlazorQuiz.Services;

public interface IQuizService
{
    Task<List<Quiz>> GetAllQuizzesAsync();
    Task<Quiz?> GetQuizByIdAsync(int id);
    Task<Quiz> CreateQuizAsync(Quiz quiz);
    Task<Quiz> UpdateQuizAsync(Quiz quiz);
    Task<bool> DeleteQuizAsync(int id);
    Task<QuizSession> CreateQuizSessionAsync(int quizId, string hostUserId);
    Task<QuizSession?> GetActiveQuizSessionAsync(string sessionCode);
    Task<QuizSession?> GetQuizSessionAsync(string sessionCode);
    Task<List<QuizParticipant>> GetParticipantsAsync(int sessionId);
    Task<QuizParticipant> AddParticipantAsync(int sessionId, string playerName, string connectionId);
    Task<QuizAnswer> SubmitAnswerAsync(int sessionId, int questionId, int participantId, int selectedOptionIndex);
    Task<List<LeaderboardEntry>> GetLeaderboardAsync(int sessionId);
    Task<List<QuestionLeaderboardEntry>> GetQuestionLeaderboardAsync(int sessionId, int questionId);
    Task<int?> GetLastAnsweredQuestionIdAsync(int sessionId);
    Task SetSessionProgressAsync(int sessionId, int questionIndex);
    Task EndSessionAsync(int sessionId);
}

public class QuizService : IQuizService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ITimeZoneService _timeZoneService;
    private readonly IMemoryCache _cache;
    private readonly IQuizEventBus _eventBus;

    public const int QuestionTimeMs = 15000;
    private const int GracePeriodMs = 2000;

    public QuizService(IDbContextFactory<ApplicationDbContext> contextFactory, ITimeZoneService timeZoneService, IMemoryCache cache, IQuizEventBus eventBus)
    {
        _eventBus = eventBus;
        _contextFactory = contextFactory;
        _timeZoneService = timeZoneService;
        _cache = cache;
    }

    public async Task<List<Quiz>> GetAllQuizzesAsync()
    {
        // Caching all quizzes for 1 minute
        return await _cache.GetOrCreateAsync("all_quizzes", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Quizzes
                .Include(q => q.Questions)
                .Where(q => q.IsActive)
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync();
        });
    }

    public async Task<Quiz?> GetQuizByIdAsync(int id)
    {
        // Caching quiz by id for 1 minute
        return await _cache.GetOrCreateAsync($"quiz_{id}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Quizzes
                .Include(q => q.Questions.OrderBy(qu => qu.OrderIndex))
                .FirstOrDefaultAsync(q => q.Id == id);
        });
    }

    private void InvalidateQuizCache(int quizId)
    {
        _cache.Remove("all_quizzes");
        _cache.Remove($"quiz_{quizId}");
    }

    public async Task<Quiz> CreateQuizAsync(Quiz quiz)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        quiz.CreatedAt = _timeZoneService.GetIndonesianTime(); // Use Indonesian time

        // Set order index for questions
        for (int i = 0; i < quiz.Questions.Count; i++)
        {
            quiz.Questions.ElementAt(i).OrderIndex = i;
        }

        context.Quizzes.Add(quiz);
        await context.SaveChangesAsync();
        InvalidateQuizCache(quiz.Id);
        return quiz;
    }

    public async Task<Quiz> UpdateQuizAsync(Quiz quiz)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var stored = await context.Quizzes.Include(q => q.Questions).FirstOrDefaultAsync(q => q.Id == quiz.Id)
            ?? throw new InvalidOperationException("Quiz not found");

        stored.Title = quiz.Title;
        stored.Description = quiz.Description;
        stored.UpdatedAt = _timeZoneService.GetIndonesianTime(); // Use Indonesian time

        // Questions missing from the edited list are deleted; the rest are updated or added
        var keptIds = quiz.Questions.Where(q => q.Id != 0).Select(q => q.Id).ToHashSet();
        context.Questions.RemoveRange(stored.Questions.Where(q => !keptIds.Contains(q.Id)));

        for (int i = 0; i < quiz.Questions.Count; i++)
        {
            var edited = quiz.Questions[i];
            var existing = edited.Id == 0 ? null : stored.Questions.FirstOrDefault(q => q.Id == edited.Id);
            if (existing == null)
            {
                stored.Questions.Add(new Question
                {
                    Text = edited.Text,
                    OptionsJson = edited.OptionsJson,
                    CorrectOptionIndex = edited.CorrectOptionIndex,
                    OrderIndex = i,
                    CreatedAt = _timeZoneService.GetIndonesianTime()
                });
                continue;
            }
            existing.Text = edited.Text;
            existing.OptionsJson = edited.OptionsJson;
            existing.CorrectOptionIndex = edited.CorrectOptionIndex;
            existing.OrderIndex = i;
        }

        await context.SaveChangesAsync();
        InvalidateQuizCache(quiz.Id);
        return stored;
    }

    public async Task<bool> DeleteQuizAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var quiz = await context.Quizzes.FindAsync(id);
        if (quiz == null) return false;

        quiz.IsActive = false;
        await context.SaveChangesAsync();
        InvalidateQuizCache(id);
        return true;
    }

    public async Task<QuizSession> CreateQuizSessionAsync(int quizId, string hostUserId)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var session = new QuizSession
            {
                QuizId = quizId,
                SessionCode = GenerateSessionCode(),
                HostUserId = hostUserId,
                StartedAt = _timeZoneService.GetIndonesianTime(), // Use Indonesian time
                IsActive = true,
                CurrentQuestionIndex = -1
            };

            context.QuizSessions.Add(session);
            try
            {
                await context.SaveChangesAsync();
                return session;
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                // Session code collided with the unique index: retry once with a new code
            }
        }
    }

    public Task<QuizSession?> GetActiveQuizSessionAsync(string sessionCode) => LoadSessionAsync(sessionCode, activeOnly: true);

    // Any state, including finished sessions (used to keep showing the final leaderboard)
    public Task<QuizSession?> GetQuizSessionAsync(string sessionCode) => LoadSessionAsync(sessionCode, activeOnly: false);

    private async Task<QuizSession?> LoadSessionAsync(string sessionCode, bool activeOnly)
    {
        // Session row only (unique index on SessionCode). Quiz + ordered questions come from the shared
        // quiz cache and participants are loaded on demand via GetParticipantsAsync.
        QuizSession? session;
        using (var context = await _contextFactory.CreateDbContextAsync())
        {
            session = await context.QuizSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SessionCode == sessionCode && (!activeOnly || s.IsActive));
        }

        if (session == null) return null;

        session.Quiz = (await GetQuizByIdAsync(session.QuizId))!;
        return session;
    }

    public async Task<List<QuizParticipant>> GetParticipantsAsync(int sessionId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.QuizParticipants
            .AsNoTracking()
            .Where(p => p.QuizSessionId == sessionId)
            .ToListAsync();
    }

    public async Task<QuizParticipant> AddParticipantAsync(int sessionId, string playerName, string connectionId)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var existingParticipant = await context.QuizParticipants
                .FirstOrDefaultAsync(p => p.QuizSessionId == sessionId && p.PlayerName == playerName);

            if (existingParticipant != null)
            {
                // Update connection id and reactivate
                existingParticipant.ConnectionId = connectionId;
                existingParticipant.IsActive = true;
                await context.SaveChangesAsync();
                _cache.Remove(LeaderboardKey(sessionId));
                return existingParticipant;
            }

            context.QuizParticipants.Add(new QuizParticipant
            {
                QuizSessionId = sessionId,
                PlayerName = playerName,
                ConnectionId = connectionId,
                JoinedAt = _timeZoneService.GetIndonesianTime(),
                IsActive = true
            });

            try
            {
                await context.SaveChangesAsync();
                _cache.Remove(LeaderboardKey(sessionId));
                return context.ChangeTracker.Entries<QuizParticipant>().First().Entity;
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                // Same name joined in parallel (unique index): loop once and take the existing row
            }
        }
    }

    public async Task<QuizAnswer> SubmitAnswerAsync(int sessionId, int questionId, int participantId, int selectedOptionIndex)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var existingAnswer = await context.QuizAnswers
            .FirstOrDefaultAsync(a => a.QuizSessionId == sessionId &&
                                    a.QuestionId == questionId &&
                                    a.ParticipantId == participantId);
        if (existingAnswer != null)
        {
            return existingAnswer; // Don't allow multiple answers for same question
        }

        var question = await context.Questions.FindAsync(questionId);
        if (question == null) throw new InvalidOperationException("Question not found");

        // Participant and question must both belong to this session
        var sessionQuizId = await context.QuizSessions.AsNoTracking()
            .Where(s => s.Id == sessionId).Select(s => (int?)s.QuizId).FirstOrDefaultAsync();
        var participantInSession = await context.QuizParticipants
            .AnyAsync(p => p.Id == participantId && p.QuizSessionId == sessionId);
        if (sessionQuizId != question.QuizId || !participantInSession)
            throw new InvalidOperationException("Participant or question does not belong to this session");

        // Time is measured on the server; unknown start (e.g. app restart) counts as the slowest answer
        var elapsedMs = _eventBus.GetElapsedMs(sessionId, questionId) ?? QuestionTimeMs;
        if (selectedOptionIndex < 0 || elapsedMs > QuestionTimeMs + GracePeriodMs)
        {
            selectedOptionIndex = -1; // timeout or answer after the deadline
            elapsedMs = QuestionTimeMs;
        }

        var isCorrect = selectedOptionIndex == question.CorrectOptionIndex;
        var points = isCorrect ? Math.Max(100 - (elapsedMs / 100), 10) : 0;

        var answer = new QuizAnswer
        {
            QuizSessionId = sessionId,
            QuestionId = questionId,
            ParticipantId = participantId,
            SelectedOptionIndex = selectedOptionIndex,
            IsCorrect = isCorrect,
            TimeTakenMs = elapsedMs,
            AnsweredAt = _timeZoneService.GetIndonesianTime(),
            Points = points
        };

        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            context.QuizAnswers.Add(answer);
            await context.SaveChangesAsync();

            // Atomic increments: concurrent answers can't overwrite each other's totals
            var correctInc = isCorrect ? 1 : 0;
            await context.QuizParticipants
                .Where(p => p.Id == participantId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.TotalAnswers, p => p.TotalAnswers + 1)
                    .SetProperty(p => p.TotalCorrectAnswers, p => p.TotalCorrectAnswers + correctInc)
                    .SetProperty(p => p.TotalScore, p => p.TotalScore + points));

            await transaction.CommitAsync();
            _cache.Remove(LeaderboardKey(sessionId));
            return answer;
        }
        catch (DbUpdateException)
        {
            // Unique index (session, question, participant): a parallel submit won the race
            var winner = await _contextFactory.CreateDbContextAsync();
            using (winner)
            {
                var saved = await winner.QuizAnswers.FirstOrDefaultAsync(a => a.QuizSessionId == sessionId &&
                    a.QuestionId == questionId && a.ParticipantId == participantId);
                if (saved != null) return saved;
            }
            throw;
        }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, SemaphoreSlim> LeaderboardLocks = new();

    private static string LeaderboardKey(int sessionId) => $"leaderboard_{sessionId}";

    public async Task<List<LeaderboardEntry>> GetLeaderboardAsync(int sessionId)
    {
        var key = LeaderboardKey(sessionId);
        if (_cache.TryGetValue(key, out List<LeaderboardEntry>? cached) && cached != null)
        {
            return cached;
        }

        // Single-flight: concurrent callers wait for one query instead of each hitting the DB
        var gate = LeaderboardLocks.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            if (_cache.TryGetValue(key, out cached) && cached != null)
            {
                return cached;
            }

            var leaderboard = await LoadLeaderboardAsync(sessionId);
            _cache.Set(key, leaderboard, TimeSpan.FromSeconds(5)); // answers/joins invalidate it sooner
            return leaderboard;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<List<LeaderboardEntry>> LoadLeaderboardAsync(int sessionId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var leaderboard = await context.QuizParticipants
            .AsNoTracking()
            .Where(p => p.QuizSessionId == sessionId && p.IsActive)
            .Select(p => new LeaderboardEntry
            {
                ParticipantName = p.PlayerName,
                TotalScore = p.TotalScore,
                CorrectAnswers = p.TotalCorrectAnswers,
                TotalAnswers = p.TotalAnswers,
                Accuracy = p.TotalAnswers > 0 ? (double)p.TotalCorrectAnswers / p.TotalAnswers * 100 : 0,
                AverageTimeMs = p.Answers.Any() ? (int)p.Answers.Average(a => a.TimeTakenMs) : 0,
                LastAnswerTime = p.Answers.Any() ? p.Answers.Max(a => a.AnsweredAt) : p.JoinedAt
            })
            .OrderByDescending(l => l.TotalScore)
            .ThenBy(l => l.AverageTimeMs)
            .ToListAsync();

        for (int i = 0; i < leaderboard.Count; i++)
        {
            leaderboard[i].Rank = i + 1;
        }

        return leaderboard;
    }

    public async Task<List<QuestionLeaderboardEntry>> GetQuestionLeaderboardAsync(int sessionId, int questionId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        return await context.QuizAnswers
            .Where(a => a.QuizSessionId == sessionId && a.QuestionId == questionId)
            .Select(a => new QuestionLeaderboardEntry
            {
                UserName = a.Participant.PlayerName,
                TimeTaken = a.TimeTakenMs,
                IsCorrect = a.IsCorrect,
                Points = a.Points
            })
            .OrderBy(l => l.TimeTaken)
            .ToListAsync();
    }

    private static string GenerateSessionCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return string.Create(6, chars, (span, alphabet) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
            }
        });
    }

    public async Task<int?> GetLastAnsweredQuestionIdAsync(int sessionId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.QuizAnswers
            .Where(a => a.QuizSessionId == sessionId)
            .OrderByDescending(a => a.AnsweredAt)
            .Select(a => (int?)a.QuestionId)
            .FirstOrDefaultAsync();
    }

    public async Task SetSessionProgressAsync(int sessionId, int questionIndex)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        await context.QuizSessions
            .Where(s => s.Id == sessionId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CurrentQuestionIndex, questionIndex));
    }

    public async Task EndSessionAsync(int sessionId)
    {
        var endedAt = _timeZoneService.GetIndonesianTime();
        using var context = await _contextFactory.CreateDbContextAsync();
        await context.QuizSessions
            .Where(s => s.Id == sessionId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsActive, false)
                .SetProperty(x => x.EndedAt, endedAt));

        // Ended session: free its clock, pending request and leaderboard lock
        _eventBus.ClearSession(sessionId);
        ForgetSessionState(_cache, sessionId);
    }

    /// <summary>Removes the cached leaderboard and the single-flight lock of a session.</summary>
    public static void ForgetSessionState(IMemoryCache cache, int sessionId)
    {
        cache.Remove(LeaderboardKey(sessionId));
        LeaderboardLocks.TryRemove(sessionId, out _);
    }
}