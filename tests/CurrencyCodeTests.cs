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
            Assert.False(code.IsStandard);
            Assert.Equal("XRP", code.ToString());
        }

        [Theory]
        [InlineData("8000000000000000000000000000000000000000")]
        [InlineData("8ED765AEBBD6767603C2C9375B2679AEC76E6A81")]
        [InlineData("00000000B6C5046EA00A0307874EEC2312F1B9BD")]
        [InlineData("00D765AEBBD6767603C2C9375B2679AEC76E6A81")]
        [InlineData("GBP")]
        [InlineData("USD")]
        [InlineData("XRP")]
        public void TestRoundTrip(string input)
        {
            var code = new CurrencyCode(input);
            Assert.Equal(input, code.ToString());
        }

        [Theory]
        [InlineData("8000000000000000000000000000000000000000", false)]
        [InlineData("8ED765AEBBD6767603C2C9375B2679AEC76E6A81", false)]
        [InlineData("00000000B6C5046EA00A0307874EEC2312F1B9BD", false)]
        [InlineData("00D765AEBBD6767603C2C9375B2679AEC76E6A81", false)]
        [InlineData("GBP", true)]
        [InlineData("USD", true)]
        [InlineData("XRP", false)]
        public void TestIsStandard(string input, bool expected)
        {
            var code = new CurrencyCode(input);
            Assert.Equal(expected, code.IsStandard);
        }

        [Fact]
        public void TestXrpHexCode()
        {
            var code = new CurrencyCode("0000000000000000000000000000000000000000");
            Assert.Equal(CurrencyCode.XRP, code);
            Assert.Equal("XRP", code.ToString());
        }

        [Fact]
        public void TestXrpStandardCode()
        {
            var code = new CurrencyCode("XRP");
            Assert.Equal(CurrencyCode.XRP, code);
            Assert.Equal("XRP", code.ToString());
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
