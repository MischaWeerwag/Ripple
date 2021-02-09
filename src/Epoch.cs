using System;

namespace Ibasa.Ripple
{
    internal static class Epoch
    {
        private static DateTimeOffset epoch = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public static uint FromDateTime(DateTime dateTime)
        {
            var utc = dateTime.ToUniversalTime();
            if (utc < epoch)
            {
                throw new ArgumentOutOfRangeException("dateTime", dateTime, "dateTime is before the ripple epoch of 2000-01-01");
            }

            return (uint)(utc.Subtract(epoch.UtcDateTime).TotalSeconds);
        }

        public static DateTime ToDateTime(uint timestamp)
        {
            return epoch.AddSeconds(timestamp).UtcDateTime;
        }
        public static uint FromDateTimeOffset(DateTimeOffset dateTimeOffset)
        {
            var utc = dateTimeOffset.ToUniversalTime();
            if (utc < epoch)
            {
                throw new ArgumentOutOfRangeException("dateTimeOffset", dateTimeOffset, "dateTimeOffset is before the ripple epoch of 2000-01-01");
            }

            return (uint)(utc.Subtract(epoch).TotalSeconds);
        }

        public static DateTimeOffset ToDateTimeOffset(uint timestamp)
        {
            return epoch.AddSeconds(timestamp);
        }
    }
}