using System;
using Xunit;

namespace Ibasa.Ripple.Tests
{
    public class HashTests
    {
        [Fact]
        public void TestDefault()
        {
            var hash = new Hash();
            Assert.Equal("0000000000000000000000000000000000000000000000000000000000000000", hash.ToString());
        }

        [Theory]
        [InlineData("0000000000000000000000000000000000000000000000000000000000000000")]
        [InlineData("8ED765AEBBD6767603C2C9375B2679AEC76E6A8133EF59F04F9FC1AAA70E41AF")]
        public void TestRoundTrip(string hex)
        {
            var hash = new Hash(hex);
            Assert.Equal(hex, hash.ToString());
        }
    }
}
