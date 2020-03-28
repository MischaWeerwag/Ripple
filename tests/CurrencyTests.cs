using System;
using System.Collections.Generic;
using Xunit;

namespace Ibasa.Ripple.Tests
{
    public class Currencytests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestZero(bool sign)
        {
            var currency = new CurrencyValue(sign, 0, 0);
            Assert.Equal(currency, new CurrencyValue(0m));
        }

        [Theory]
        [InlineData(false, 80, 9999_9999_9999_9999)]
        [InlineData(false, 13, 7922_8162_5142_6434)]
        [InlineData(true, 13, 7922_8162_5142_6434)]
        [InlineData(true, 80, 9999_9999_9999_9999)]
        public void TestOutOfDecimalRange(bool isPositive, int exponent, ulong mantissa)
        {
            var currency = new CurrencyValue(isPositive, exponent, mantissa);
            var exc = Assert.Throws<OverflowException>(() => (decimal)currency);
            Assert.Equal("Value was either too large or too small for a Decimal.", exc.Message);
        }

        [Theory]
        [InlineData(-1, "-1")]
        [InlineData(0, "0")]
        [InlineData(1, "1")]
        public void TestToString(decimal value, string expected)
        {
            Assert.Equal(expected, new CurrencyValue(value).ToString());
        }

        [Theory]
        [InlineData(true, -96, 1000_0000_0000_0000)]
        [InlineData(true, -96, 9999_9999_9999_9999)]
        [InlineData(true, 0, 1000_0000_0000_0000)]
        [InlineData(true, 0, 9999_9999_9999_9999)]
        [InlineData(true, 80, 1000_0000_0000_0000)]
        [InlineData(true, 80, 9999_9999_9999_9999)]
        [InlineData(false, -96, 1000_0000_0000_0000)]
        [InlineData(false, -96, 9999_9999_9999_9999)]
        [InlineData(false, 0, 1000_0000_0000_0000)]
        [InlineData(false, 0, 9999_9999_9999_9999)]
        [InlineData(false, 80, 1000_0000_0000_0000)]
        [InlineData(false, 80, 9999_9999_9999_9999)]
        public void TestIssuedRoundTrip(bool isPositive, int exponent, ulong mantissa)
        {
            var currency = new CurrencyValue(isPositive, exponent, mantissa);
            var str = currency.ToString();
            Assert.Equal(currency, CurrencyValue.Parse(str));
        }

        public static IEnumerable<object[]> decimals
        {
            get
            {
                yield return new object[] { -79228162514264330000000000000m };
                yield return new object[] { -1m };
                yield return new object[] { -1e-28m };
                yield return new object[] { 0m };
                yield return new object[] { 1e-28m };
                yield return new object[] { 1m };
                yield return new object[] { 79228162514264330000000000000m };
            }
        }

        [Theory]
        [MemberData(nameof(decimals))]
        public void TestDecimalRoundTrip(decimal value)
        {
            var currency = new CurrencyValue(value);
            Assert.Equal(value, (decimal)currency);
        }
    }
}
