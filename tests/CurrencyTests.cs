using System;
using System.Buffers;
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
            var currency = CurrencyValue.FromIssued(sign, 0, 0);
            Assert.Equal(currency, CurrencyValue.FromIssued(0m));
        }

        [Theory]
        [InlineData(-1, "-1")]
        [InlineData(0, "0")]
        [InlineData(1, "1")]
        public void TestToString(decimal value, string expected)
        {
            Assert.Equal(expected, CurrencyValue.FromIssued(value).ToString());
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
            var currency = CurrencyValue.FromIssued(isPositive, exponent, mantissa);
            var str = currency.ToString();
            Assert.Equal(currency, CurrencyValue.ParseIssued(str));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4_611_686_018_427_387_901)]
        [InlineData(4_611_686_018_427_387_902)]
        [InlineData(4_611_686_018_427_387_903)]
        public void TestDropRoundTrip(ulong drops)
        {
            var currency = CurrencyValue.FromDrops(drops);
            var str = currency.ToString();
            Assert.Equal(currency, CurrencyValue.ParseDrops(str));
        }

        public static IEnumerable<object[]> decimals
        {
            get
            {
                yield return new object[] { -79228162514264330000000000000m };
                yield return new object[] { -1m };
                yield return new object[] { -1e-28m };
                yield return new object[] { 0m};
                yield return new object[] { 1e-28m };
                yield return new object[] { 1m};
                yield return new object[] { 79228162514264330000000000000m};
            }
        }

        [Theory]
        [MemberData(nameof(decimals))]
        public void TestDecimalRoundTrip(decimal value)
        {
            var currency = CurrencyValue.FromIssued(value);
            Assert.Equal(value, (decimal)currency);
        }
    }
}
