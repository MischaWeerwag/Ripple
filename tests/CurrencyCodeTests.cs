using System;
using Xunit;

namespace Ibasa.Ripple.Tests
{
    public class CurrencyCodeTests
    {
        [Fact]
        public void TestDefault()
        {
            var code = new CurrencyCode();
            Assert.True(code.IsStandard);
            Assert.Equal("\0\0\0", code.ToString());
        }

        [Theory]
        [InlineData("8000000000000000000000000000000000000000")]
        [InlineData("8ED765AEBBD6767603C2C9375B2679AEC76E6A81")]
        [InlineData("GBP")]
        [InlineData("USD")]
        public void TestRoundTrip(string input)
        {
            var code = new CurrencyCode(input);
            Assert.Equal(input, code.ToString());
        }

        [Theory]
        [InlineData("0000000000000000000000000000000000000000")]
        [InlineData("00D765AEBBD6767603C2C9375B2679AEC76E6A81")]
        public void TestZeroHexFails(string input)
        {
            var exc = Assert.Throws<ArgumentException>(() => new CurrencyCode(input));
            Assert.Equal("hex code first byte can not be zero (Parameter 'code')", exc.Message);
        }

        [Theory]
        [InlineData("a.d", "'.' is not a valid standard currency code character (Parameter 'code')")]
        [InlineData("   ", "' ' is not a valid standard currency code character (Parameter 'code')")]
        [InlineData("\0\0\0", "'\0' is not a valid standard currency code character (Parameter 'code')")]
        public void TestInvalidStandardCode(string input, string error)
        {
            var exc = Assert.Throws<ArgumentException>(() => new CurrencyCode(input));
            Assert.Equal(error, exc.Message);
        }
    }
}
