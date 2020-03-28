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

        [Fact]
        public void TestRoundTrip()
        {
            foreach (var sign in new bool[] { true, false })
            {
                for (int exponent = -96; exponent <= 80; ++exponent)
                {
                    for (ulong mantissa = 1000_0000_0000_0000; mantissa <= 9999_9999_9999_9999; ++mantissa)
                    {
                        var currency = CurrencyValue.FromIssued(sign, exponent, mantissa);
                        var dec = (decimal)currency;
                        Assert.Equal(currency, CurrencyValue.FromIssued(dec));
                    }
                }
            }
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
