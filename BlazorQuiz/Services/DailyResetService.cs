using BlazorQuiz.Data;
using BlazorQuiz.Models;
using Microsoft.EntityFrameworkCore;

namespace BlazorQuiz.Services
{
    public class DailyResetService : BackgroundService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ITimeZoneService _timeZoneService;
        private readonly ILogger<DailyResetService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public DailyResetService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ITimeZoneService timeZoneService,
            ILogger<DailyResetService> logger,
            IServiceProvider serviceProvider)
        {
            _contextFactory = contextFactory;
            _timeZoneService = timeZoneService;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var nowWib = _timeZoneService.GetIndonesianTime();
                var nextMidnightWib = nowWib.Date.AddDays(1);
                var delay = nextMidnightWib - nowWib;

                if (delay <= TimeSpan.Zero)
                {
                    delay = TimeSpan.FromMinutes(1);
                }

                _logger.LogInformation("Jadwal reset data harian pada {NextReset} WIB (dalam {Hours:F1} jam).",
                    nextMidnightWib, delay.TotalHours);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                    await PerformResetAsync();
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Terjadi kesalahan saat reset data otomatis jam 00:00 WIB.");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }
        }

        public async Task PerformResetAsync()
        {
            _logger.LogInformation("Memulai pembersihan data harian (00:00 WIB)...");
            await using var db = _contextFactory.CreateDbContext();

            db.QuizAnswers.RemoveRange(db.QuizAnswers);
            db.QuizParticipants.RemoveRange(db.QuizParticipants);
            db.QuizSessions.RemoveRange(db.QuizSessions);
            db.ActivityLogs.RemoveRange(db.ActivityLogs);
            await db.SaveChangesAsync();

            await EnsureDefaultQuizAsync(db);

            using var scope = _serviceProvider.CreateScope();
            await DataSeeder.SeedDefaultUserAsync(scope.ServiceProvider);

            _logger.LogInformation("Pembersihan data harian 00:00 WIB selesai.");
        }

        public static async Task EnsureDefaultQuizAsync(ApplicationDbContext db)
        {
            if (!await db.Quizzes.AnyAsync())
            {
                var defaultQuiz = new Quiz
                {
                    Title = "Kuis Demo: Trivia Pengetahuan Umum",
                    Description = "Kuis demo bawaan untuk mencoba fitur Host dan Player BlazorQuiz.",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    Questions = new List<Question>
                    {
                        new Question
                        {
                            Text = "Apa ibu kota negara Indonesia saat ini?",
                            OptionsJson = System.Text.Json.JsonSerializer.Serialize(new List<string> { "Bandung", "Jakarta", "Surabaya", "Nusantara" }),
                            CorrectOptionIndex = 1,
                            OrderIndex = 1,
                            CreatedAt = DateTime.UtcNow
                        },
                        new Question
                        {
                            Text = "Planet manakah yang dijuluki sebagai Planet Merah?",
                            OptionsJson = System.Text.Json.JsonSerializer.Serialize(new List<string> { "Venus", "Mars", "Jupiter", "Merkurius" }),
                            CorrectOptionIndex = 1,
                            OrderIndex = 2,
                            CreatedAt = DateTime.UtcNow
                        },
                        new Question
                        {
                            Text = "Berapa hasil dari 15 x 6?",
                            OptionsJson = System.Text.Json.JsonSerializer.Serialize(new List<string> { "80", "90", "100", "75" }),
                            CorrectOptionIndex = 1,
                            OrderIndex = 3,
                            CreatedAt = DateTime.UtcNow
                        }
                    }
                };

                db.Quizzes.Add(defaultQuiz);
                await db.SaveChangesAsync();
            }
        }
    }
}
