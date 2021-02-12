using FsCheck;
using FsCheck.Xunit;
using System;
using System.Collections.Generic;
using Xunit;

namespace Ibasa.Ripple.Tests
{
    public class CurrencyTests
    {
        public static Arbitrary<Currency> Arb
        {
            get
            {
                return FsCheck.Arb.From<decimal>().Convert(d => (Currency)d, c => (decimal)c);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestZero(bool sign)
        {
            var currency = new Currency(sign, 0, 0);
            Assert.Equal(currency, new Currency(0m));
        }

        [Fact]
        public void TestConstants()
        {
            Assert.Equal(new Currency(-1m), Currency.MinusOne);
            Assert.Equal(new Currency(0m), Currency.Zero);
            Assert.Equal(new Currency(1m), Currency.One);
        }

        [Theory]
        [InlineData(false, 80, 9999_9999_9999_9999)]
        [InlineData(false, 13, 7922_8162_5142_6434)]
        [InlineData(true, 13, 7922_8162_5142_6434)]
        [InlineData(true, 80, 9999_9999_9999_9999)]
        public void TestOutOfDecimalRange(bool isPositive, int exponent, ulong mantissa)
        {
            var currency = new Currency(isPositive, exponent, mantissa);
            var exc = Assert.Throws<OverflowException>(() => (decimal)currency);
            Assert.Equal("Value was either too large or too small for a Decimal.", exc.Message);
        }

        [Fact]
        public void TestDecimalUnderflow()
        {
            var decimals = new decimal[]
            {
                1.0000000000000000000000000001m,
                1.000000000000000000000000001m,
                1.00000000000000000000000001m,
                1.0000000000000000000000001m,
                1.000000000000000000000001m,
                1.00000000000000000000001m,
                1.0000000000000000000001m,
                1.000000000000000000001m,
                1.00000000000000000001m,
                1.0000000000000000001m,
                1.000000000000000001m,
                1.00000000000000001m,
                1.0000000000000001m,
            };
            foreach (var d in decimals)
            {
                Assert.NotEqual(Decimal.One, d);
                var c = new Currency(d);
                Assert.Equal(Currency.One, c);
            }
                
            Assert.NotEqual(Currency.One, new Currency(1.000000000000001m));
        }

        [Theory]
        [InlineData("-1", "-1")]
        [InlineData("0.09999999999999999", "9999999999999999E-17")]
        [InlineData("0.00000000000000001", "1E-17")]
        [InlineData("0.9999999999999999", "0.9999999999999999")]
        [InlineData("0.0000000000000001", "0.0000000000000001")]
        [InlineData("9.99", "9.99")]
        [InlineData("0.99", "0.99")]
        [InlineData("0.01", "0.01")]
        [InlineData("0.1", "0.1")]
        [InlineData("0.0", "0")]
        [InlineData("0", "0")]
        [InlineData("1", "1")]
        [InlineData("1.0", "1")]
        [InlineData("10", "10")]
        [InlineData("99", "99")]
        [InlineData("9.9", "9.9")]
        [InlineData("99.9", "99.9")]
        [InlineData("1000000000000000", "1000000000000000")]
        [InlineData("9999999999999999", "9999999999999999")]
        [InlineData("10000000000000000", "1E16")]
        [InlineData("99999999999999990", "9999999999999999E1")]
        public void TestToString(string value, string expected)
        {
            var decimalValue = decimal.Parse(value);
            Assert.Equal(expected, new Currency(decimalValue).ToString());
            Assert.Equal(expected, Currency.Parse(value).ToString());
        }

        [Theory]
        [InlineData(true, -96, 1000_0000_0000_0000)]
        [InlineData(true, -96, 9999_9999_9999_9999)]
        [InlineData(true, 0, 1000_0000_0000_0000)]
        [InlineData(true, 0, 9999_9999_9999_9999)]
        [InlineData(true, 80, 1000_0000_0000_0000)]
        [InlineData(true, 80, 9999_9999_9999_9999)]
        [InlineData(true, 0, 0)]
        [InlineData(false, 0, 0)]
        [InlineData(false, -96, 1000_0000_0000_0000)]
        [InlineData(false, -96, 9999_9999_9999_9999)]
        [InlineData(false, 0, 1000_0000_0000_0000)]
        [InlineData(false, 0, 9999_9999_9999_9999)]
        [InlineData(false, 80, 1000_0000_0000_0000)]
        [InlineData(false, 80, 9999_9999_9999_9999)]
        public void TestExplicitRoundTrip(bool isPositive, int exponent, ulong mantissa)
        {
            var currency = new Currency(isPositive, exponent, mantissa);
            var str = currency.ToString();
            Assert.Equal(currency, Currency.Parse(str));
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
            var currency = new Currency(value);
            Assert.Equal(value, (decimal)currency);
        }

        [Property]
        public Property TestToStringRoundTrip()
        {
            return Prop.ForAll(Arb, c =>
            {
                var str = c.ToString();
                var rt = Currency.Parse(str);
                Assert.Equal(c, rt);
            });
        }

        [Property]
        public Property TestOrdering()
        {
            return Prop.ForAll(Arb, Arb, Arb, (v1, v2, v3) =>
                {
                    if (v1 < v2 && v2 < v3)
                    {
                        Assert.True(v1 < v3, "v1 < v3");
                    }
                    if (v2 < v1 && v1 < v3)
                    {
                        Assert.True(v2 < v3, "v2 < v3");
                    }
                    if (v3 < v1 && v1 < v2)
                    {
                        Assert.True(v3 < v2, "v3 < v2");
                    }
                    if (v2 < v3 && v3 < v1)
                    {
                        Assert.True(v2 < v1, "v2 < v1");
                    }
                });
        }
    }
}
