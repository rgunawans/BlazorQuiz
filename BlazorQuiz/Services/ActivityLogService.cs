using BlazorQuiz.Data;
using BlazorQuiz.Models;
using Microsoft.EntityFrameworkCore;

namespace BlazorQuiz.Services
{
    public interface IActivityLogService
    {
        Task LogActivityAsync(string activity, string? details = null, string level = "Info", string? userId = null, string? userName = null, string? source = null);
        Task<List<ActivityLog>> GetRecentActivitiesAsync(int count = 50);
        Task<List<ActivityLog>> GetActivitiesBySourceAsync(string source, int count = 50);
    }

    /// <summary>Bounded in-memory queue; when full the oldest entries are dropped instead of blocking the quiz.</summary>
    public class ActivityLogQueue
    {
        private readonly System.Threading.Channels.Channel<ActivityLog> _channel =
            System.Threading.Channels.Channel.CreateBounded<ActivityLog>(new System.Threading.Channels.BoundedChannelOptions(10000)
            {
                FullMode = System.Threading.Channels.BoundedChannelFullMode.DropOldest,
                SingleReader = true
            });

        public void Enqueue(ActivityLog log) => _channel.Writer.TryWrite(log);

        public System.Threading.Channels.ChannelReader<ActivityLog> Reader => _channel.Reader;
    }

    /// <summary>Drains the queue and inserts logs in batches: one SaveChanges per batch instead of one per log line.</summary>
    public class ActivityLogWriter : BackgroundService
    {
        private const int MaxBatchSize = 200;
        private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(1);

        private readonly ActivityLogQueue _queue;
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<ActivityLogWriter> _logger;

        public ActivityLogWriter(ActivityLogQueue queue, IDbContextFactory<ApplicationDbContext> contextFactory, ILogger<ActivityLogWriter> logger)
        {
            _queue = queue;
            _contextFactory = contextFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var batch = new List<ActivityLog>(MaxBatchSize);
            try
            {
                while (await _queue.Reader.WaitToReadAsync(stoppingToken))
                {
                    await Task.Delay(FlushInterval, stoppingToken); // let a burst accumulate
                    while (batch.Count < MaxBatchSize && _queue.Reader.TryRead(out var log))
                    {
                        batch.Add(log);
                    }
                    await FlushAsync(batch);
                }
            }
            catch (OperationCanceledException)
            {
                // shutting down: write what is still queued
                while (_queue.Reader.TryRead(out var log)) batch.Add(log);
                await FlushAsync(batch);
            }
        }

        private async Task FlushAsync(List<ActivityLog> batch)
        {
            if (batch.Count == 0) return;
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                context.ActivityLogs.AddRange(batch);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write {Count} activity logs", batch.Count);
            }
            finally
            {
                batch.Clear();
            }
        }
    }

    public class ActivityLogService : IActivityLogService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ITimeZoneService _timeZoneService;
        private readonly ActivityLogQueue _queue;

        public ActivityLogService(IDbContextFactory<ApplicationDbContext> contextFactory, IHttpContextAccessor httpContextAccessor, ITimeZoneService timeZoneService, ActivityLogQueue queue)
        {
            _queue = queue;
            _contextFactory = contextFactory;
            _httpContextAccessor = httpContextAccessor;
            _timeZoneService = timeZoneService;
        }

        public Task LogActivityAsync(string activity, string? details = null, string level = "Info", string? userId = null, string? userName = null, string? source = null)
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;

                // Captured now (scoped services/HttpContext are not available to the background writer)
                _queue.Enqueue(new ActivityLog
                {
                    Activity = activity,
                    Details = details,
                    Level = level,
                    UserId = userId,
                    UserName = userName,
                    Source = source,
                    CreatedAt = _timeZoneService.GetIndonesianTime(),
                    IpAddress = httpContext?.Connection?.RemoteIpAddress?.ToString(),
                    UserAgent = httpContext?.Request?.Headers["User-Agent"].ToString()
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to queue activity log: {ex.Message}");
            }
            return Task.CompletedTask;
        }

        public async Task<List<ActivityLog>> GetRecentActivitiesAsync(int count = 50)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.ActivityLogs
                .OrderByDescending(a => a.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<ActivityLog>> GetActivitiesBySourceAsync(string source, int count = 50)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.ActivityLogs
                .Where(a => a.Source == source)
                .OrderByDescending(a => a.CreatedAt)
                .Take(count)
                .ToListAsync();
        }
    }
}