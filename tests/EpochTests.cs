using System;
using Xunit;

namespace Ibasa.Ripple.Tests
{
    public class EpochTests
    {
        [Fact]
        public void TestZero()
        {
            Assert.Equal(new DateTime(2000, 1, 1), Epoch.ToDateTime(0));
            Assert.Equal(0u, Epoch.FromDateTime(new DateTime(2000, 1, 1)));
            Assert.Equal(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero), Epoch.ToDateTimeOffset(0));
            Assert.Equal(0u, Epoch.FromDateTimeOffset(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void TestIsUtc()
        {
            var dateTime = Epoch.ToDateTime(0);
            Assert.Equal(DateTimeKind.Utc, dateTime.Kind);
        }

        [Fact]
        public void TestIsZeroOffset()
        {
            var dateTimeOffset = Epoch.ToDateTimeOffset(0);
            Assert.Equal(TimeSpan.Zero, dateTimeOffset.Offset);
        }

        [Theory]
        [InlineData(uint.MinValue)]
        [InlineData(uint.MaxValue)]
        [InlineData(1U)]
        [InlineData(4294967294U)]
        [InlineData(2147483647U)]
        public void TestDateTimeRoundTrip(uint timestamp)
        {
            Assert.Equal(timestamp, Epoch.FromDateTime(Epoch.ToDateTime(timestamp)));
        }

        [Theory]
        [InlineData(uint.MinValue)]
        [InlineData(uint.MaxValue)]
        [InlineData(1U)]
        [InlineData(4294967294U)]
        [InlineData(2147483647U)]
        public void TestDateTimeOffsetRoundTrip(uint timestamp)
        {
            Assert.Equal(timestamp, Epoch.FromDateTimeOffset(Epoch.ToDateTimeOffset(timestamp)));
        }

        [Fact]
        public void TestPreEphoch()
        {
            ArgumentOutOfRangeException exc;

            var dateTime = new DateTime(1999, 12, 30);
            exc = Assert.Throws<ArgumentOutOfRangeException>(() => Epoch.FromDateTime(dateTime));
            Assert.Equal("dateTime", exc.ParamName);
            Assert.Equal(dateTime, exc.ActualValue);
            Assert.Equal("dateTime is before the ripple epoch of 2000-01-01 (Parameter 'dateTime')\r\nActual value was 30/12/1999 00:00:00.", exc.Message);

            var dateTimeOffset = new DateTimeOffset(1999, 12, 30, 0, 0, 0, TimeSpan.Zero);
            exc = Assert.Throws<ArgumentOutOfRangeException>(() => Epoch.FromDateTimeOffset(dateTimeOffset));
            Assert.Equal("dateTimeOffset", exc.ParamName);
            Assert.Equal(dateTimeOffset, exc.ActualValue);
            Assert.Equal("dateTimeOffset is before the ripple epoch of 2000-01-01 (Parameter 'dateTimeOffset')\r\nActual value was 30/12/1999 00:00:00 +00:00.", exc.Message);
        }
    }
}
