namespace EcommerceApp.Helpers
{
    public static class DateTimeExtensions
    {
        public static DateTime ToCairoTime(this DateTime utcDateTime)
        {
            var normalized = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
            var timeZoneId = OperatingSystem.IsWindows() ? "Egypt Standard Time" : "Africa/Cairo";
            return TimeZoneInfo.ConvertTimeFromUtc(
                normalized,
                TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
        }
    }
}
