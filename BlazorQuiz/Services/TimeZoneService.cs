using System;

namespace BlazorQuiz.Services
{
    public interface ITimeZoneService
    {
        DateTime GetIndonesianTime();
        DateTime ConvertUtcToIndonesian(DateTime utcDateTime);
        string GetIndonesianTimeZoneId();
        void LogTimeZoneInfo(); // For debugging purposes
    }

    public class TimeZoneService : ITimeZoneService
    {
        private const string INDONESIAN_TIMEZONE_ID = "SE Asia Standard Time"; // GMT+7 (Windows timezone ID)
        private static readonly TimeZoneInfo _indonesianTimeZone;

        static TimeZoneService()
        {
            try
            {
                // Try to get the Indonesian timezone (GMT+7)
                _indonesianTimeZone = TimeZoneInfo.FindSystemTimeZoneById(INDONESIAN_TIMEZONE_ID);
            }
            catch
            {
                // Fallback: create a custom timezone for GMT+7 if system timezone is not available
                _indonesianTimeZone = TimeZoneInfo.CreateCustomTimeZone(
                    "Indonesia Standard Time",
                    TimeSpan.FromHours(7),
                    "Indonesia Standard Time",
                    "WIB"
                );
            }
        }

        public DateTime GetIndonesianTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _indonesianTimeZone);
        }

        public DateTime ConvertUtcToIndonesian(DateTime utcDateTime)
        {
            if (utcDateTime.Kind != DateTimeKind.Utc)
            {
                // If the datetime is not UTC, assume it's already local or unspecified
                // Convert it to UTC first
                utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
            }

            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, _indonesianTimeZone);
        }

        public string GetIndonesianTimeZoneId()
        {
            return _indonesianTimeZone.Id;
        }

        public void LogTimeZoneInfo()
        {
            var utcNow = DateTime.UtcNow;
            var indonesianNow = GetIndonesianTime();
            
            Console.WriteLine($"TimeZone Debug Info:");
            Console.WriteLine($"  UTC Time: {utcNow:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"  Indonesian Time: {indonesianNow:yyyy-MM-dd HH:mm:ss} WIB");
            Console.WriteLine($"  Timezone ID: {_indonesianTimeZone.Id}");
            Console.WriteLine($"  Timezone Name: {_indonesianTimeZone.DisplayName}");
            Console.WriteLine($"  Offset: {_indonesianTimeZone.BaseUtcOffset}");
        }
    }
}