using System;
using Xunit;

namespace Ibasa.Ripple.Tests
{
    public class Hash256Tests
    {
        [Fact]
        public void TestDefault()
        {
            var hash = new Hash256();
            Assert.Equal("0000000000000000000000000000000000000000000000000000000000000000", hash.ToString());
        }

        [Theory]
        [InlineData("0000000000000000000000000000000000000000000000000000000000000000")]
        [InlineData("8ED765AEBBD6767603C2C9375B2679AEC76E6A8133EF59F04F9FC1AAA70E41AF")]
        public void TestRoundTrip(string hex)
        {
            var hash = new Hash256(hex);
            Assert.Equal(hex, hash.ToString());
        }
    }
    public class Hash128Tests
    {
        [Fact]
        public void TestDefault()
        {
            var hash = new Hash128();
            Assert.Equal("00000000000000000000000000000000", hash.ToString());
        }

        [Theory]
        [InlineData("00000000000000000000000000000000")]
        [InlineData("C76E6A8133EF59F04F9FC1AAA70E41AF")]
        [InlineData("8ED765AEBBD6767603C2C9375B2679AE")]
        public void TestRoundTrip(string hex)
        {
            var hash = new Hash128(hex);
            Assert.Equal(hex, hash.ToString());
        }
    }
}
