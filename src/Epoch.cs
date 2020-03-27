using System;

namespace Ibasa.Ripple
{
    public static class Epoch
    {
        private static DateTime epoch = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static uint FromDateTime(DateTime dateTime)
        {
            var utc = dateTime.ToUniversalTime();
            if (utc < epoch)
            {
                throw new ArgumentOutOfRangeException("dateTime", "dateTime is before the ripple epoch of 2000-01-01");
            }

            return (uint)(utc.Subtract(epoch).TotalSeconds);
        }

        public static DateTime ToDateTime(uint timestamp)
        {
            return epoch.AddSeconds(timestamp);
        }
    }
}