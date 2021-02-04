using System;
using System.Collections.Generic;
using Xunit;

namespace Ibasa.Ripple.Tests
{
    public class XrpAmountTests
    {
        [Theory]
        [InlineData(0ul, "0 XRP")]
        [InlineData(1ul, "0.000001 XRP")]
        [InlineData(1_000ul, "0.001 XRP")]
        [InlineData(1_000_000ul, "1 XRP")]
        [InlineData(1_000_000_000ul, "1000 XRP")]
        [InlineData(1_000_000_000_000ul, "1000000 XRP")]
        [InlineData(100_000_000_000_000_000ul, "1000000000 XRP")]
        public void TestToString(ulong drops, string expected)
        {
            var amount = new XrpAmount(drops);
            Assert.Equal(expected, amount.ToString());
        }
    }

    public class IssuedAmountTests
    {
        [Theory]
        [InlineData("r4nmanwKSE6GpkTCrBjz8uanrGZabbpSfp", "GBP", "100", "100 GBP(r4nmanwKSE6GpkTCrBjz8uanrGZabbpSfp)")]
        public void TestToString(string issuer, string currencyCode, string value, string expected)
        {
            var amount = new IssuedAmount(new AccountId(issuer), new CurrencyCode(currencyCode), Currency.Parse(value));
            Assert.Equal(expected, amount.ToString());
        }
    }

    public class AmountTests
    {
        [Theory]
        [InlineData("r4nmanwKSE6GpkTCrBjz8uanrGZabbpSfp", "GBP", "100", "100 GBP(r4nmanwKSE6GpkTCrBjz8uanrGZabbpSfp)")]
        public void TestToString(string issuer, string currencyCode, string value, string expected)
        {
            var amount = new Amount(new AccountId(issuer), new CurrencyCode(currencyCode), Currency.Parse(value));
            Assert.Equal(expected, amount.ToString());
        }
    }
}
