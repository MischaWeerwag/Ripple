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
        }

        [Fact]
        public void TestIsUtc()
        {
            var datetime = Epoch.ToDateTime(0);
            Assert.Equal(DateTimeKind.Utc, datetime.Kind);
        }

        [Theory]
        [InlineData(uint.MinValue)]
        [InlineData(uint.MaxValue)]
        [InlineData(1U)]
        [InlineData(4294967294U)]
        [InlineData(2147483647U)]
        public void TestRoundTrip(uint timestamp)
        {
            Assert.Equal(timestamp, Epoch.FromDateTime(Epoch.ToDateTime(timestamp)));
        }

        [Fact]
        public void TestPreEphoch()
        {
            var exc = Assert.Throws<ArgumentOutOfRangeException>(() =>
                Epoch.FromDateTime(new DateTime(1999, 12, 30)));
            Assert.Equal("dateTime", exc.ParamName);
            Assert.Equal("dateTime is before the ripple epoch of 2000-01-01 (Parameter 'dateTime')", exc.Message);
        }
    }
}
