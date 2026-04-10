namespace ProjectManagementSystem.Common
{
    public static class AppTime
    {
        private static readonly TimeZoneInfo ChinaTimeZone = ResolveChinaTimeZone();

        public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ChinaTimeZone);

        public static DateTime Today => Now.Date;

        public static DateTime ConvertFromUtc(DateTime utcTime)
        {
            if (utcTime.Kind == DateTimeKind.Utc)
            {
                return TimeZoneInfo.ConvertTimeFromUtc(utcTime, ChinaTimeZone);
            }

            return utcTime;
        }

        public static DateTime ConvertToChinaTime(DateTime value)
        {
            if (value == default)
            {
                return value;
            }

            return value.Kind switch
            {
                DateTimeKind.Utc => TimeZoneInfo.ConvertTimeFromUtc(value, ChinaTimeZone),
                DateTimeKind.Local => TimeZoneInfo.ConvertTime(value, ChinaTimeZone),
                DateTimeKind.Unspecified => value,
                _ => value
            };
        }

        private static TimeZoneInfo ResolveChinaTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
            }
        }
    }
}