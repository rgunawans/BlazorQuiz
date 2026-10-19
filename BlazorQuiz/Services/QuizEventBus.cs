using System.Collections.Concurrent;
using BlazorQuiz.Models;

namespace BlazorQuiz.Services;

public enum QuizEventType
{
    Question,
    RevealAnswer,
    QuizCompleted,
    LeaderboardUpdated,
    ScoreboardUpdated,
    ParticipantJoined,
    ParticipantTimeout,
    StatusUpdate,
    NextQuestionRequested
}

public class QuizEvent
{
    public QuizEventType Type { get; init; }
    public int SessionId { get; init; }
    public QuestionDto? Question { get; init; }
    public string? PlayerName { get; init; }
    public int TimeTaken { get; init; }
    public bool IsCorrect { get; init; }
    public int Points { get; init; }
    public int CurrentQuestionNumber { get; init; }
    public int TotalQuestions { get; init; }
    public bool Completed { get; init; }
}

public interface IQuizEventBus
{
    IDisposable Subscribe(int sessionId, Func<QuizEvent, Task> handler);
    Task PublishAsync(QuizEvent quizEvent);
    void RequestLeaderboardRefresh(int sessionId);
    void StartQuestion(int sessionId, int questionId);
    int? GetElapsedMs(int sessionId, int questionId);
    void RequestNextQuestion(int sessionId);
    bool ConsumeNextQuestionRequest(int sessionId);
}

/// <summary>
/// In-process replacement for the SignalR hub loopback: Blazor Server components already run on the server,
/// so events are delivered directly. It also holds the server-side question start time used for scoring.
/// </summary>
public class QuizEventBus : IQuizEventBus
{
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<Guid, Func<QuizEvent, Task>>> _subscribers = new();
    private readonly ConcurrentDictionary<int, (int QuestionId, long StartTimestamp)> _activeQuestions = new();
    private readonly ConcurrentDictionary<int, bool> _nextRequests = new();
    private readonly ConcurrentDictionary<int, byte> _pendingLeaderboardRefresh = new();
    private static readonly TimeSpan LeaderboardRefreshDelay = TimeSpan.FromSeconds(1);
    private readonly ILogger<QuizEventBus> _logger;

    public QuizEventBus(ILogger<QuizEventBus> logger)
    {
        _logger = logger;
    }

    public IDisposable Subscribe(int sessionId, Func<QuizEvent, Task> handler)
    {
        var id = Guid.NewGuid();
        var handlers = _subscribers.GetOrAdd(sessionId, _ => new());
        handlers[id] = handler;
        return new Subscription(() =>
        {
            if (_subscribers.TryGetValue(sessionId, out var current))
            {
                current.TryRemove(id, out _);
            }
        });
    }

    public Task PublishAsync(QuizEvent quizEvent)
    {
        if (!_subscribers.TryGetValue(quizEvent.SessionId, out var handlers) || handlers.IsEmpty)
        {
            return Task.CompletedTask;
        }

        return Task.WhenAll(handlers.Values.Select(h => SafeInvokeAsync(h, quizEvent)));
    }

    /// <summary>
    /// Coalesces LeaderboardUpdated: any number of answers within the delay window produce a single event,
    /// so 20 answers no longer trigger 20 x N leaderboard queries.
    /// </summary>
    public void RequestLeaderboardRefresh(int sessionId)
    {
        if (!_pendingLeaderboardRefresh.TryAdd(sessionId, 0)) return;

        _ = Task.Run(async () =>
        {
            await Task.Delay(LeaderboardRefreshDelay);
            _pendingLeaderboardRefresh.TryRemove(sessionId, out _);
            await PublishAsync(new QuizEvent { Type = QuizEventType.LeaderboardUpdated, SessionId = sessionId });
        });
    }

    private async Task SafeInvokeAsync(Func<QuizEvent, Task> handler, QuizEvent quizEvent)
    {
        try
        {
            await handler(quizEvent);
        }
        catch (Exception ex)
        {
            // A disposed circuit must never break delivery to the other subscribers
            _logger.LogDebug(ex, "Quiz event {Type} handler failed", quizEvent.Type);
        }
    }

    public void StartQuestion(int sessionId, int questionId)
    {
        _activeQuestions[sessionId] = (questionId, System.Diagnostics.Stopwatch.GetTimestamp());
    }

    public int? GetElapsedMs(int sessionId, int questionId)
    {
        if (_activeQuestions.TryGetValue(sessionId, out var active) && active.QuestionId == questionId)
        {
            return (int)System.Diagnostics.Stopwatch.GetElapsedTime(active.StartTimestamp).TotalMilliseconds;
        }
        return null;
    }

    public void RequestNextQuestion(int sessionId) => _nextRequests[sessionId] = true;

    public bool ConsumeNextQuestionRequest(int sessionId) => _nextRequests.TryRemove(sessionId, out _);

    private sealed class Subscription : IDisposable
    {
        private Action? _onDispose;
        public Subscription(Action onDispose) => _onDispose = onDispose;
        public void Dispose() => Interlocked.Exchange(ref _onDispose, null)?.Invoke();
    }
}
