using System;
using System.Collections.Generic;
using Xunit;

namespace Ibasa.Ripple.Tests
{
    public class AccountIdTests
    {
        public static IEnumerable<object[]> accounts
        {
            get
            {
                yield return new object[] { "r4nmanwKSE6GpkTCrBjz8uanrGZabbpSfp" };
                yield return new object[] { "r3kmLJN5D28dHuH8vZNUZpMC43pEHpaocV" };
                yield return new object[] { "rEopG7qc7ZWFMvCSH3GYeJJ7GPAQnKmxgw" };
                yield return new object[] { "rrrrrrrrrrrrrrrrrrrrrhoLvTp" };
            }
        }

        [Theory]
        [MemberData(nameof(accounts))]
        public void TestRoundTrip(string base58)
        {
            var account = new AccountId(base58);
            Assert.Equal(base58, account.ToString());
        }

        [Theory]
        [MemberData(nameof(accounts))]
        public void TestCopy(string base58)
        {
            var account = new AccountId(base58);
            var copy = account;
            Assert.Equal(account.ToString(), copy.ToString());
        }

        [Theory]
        [MemberData(nameof(accounts))]
        public void TestBoxing(string base58)
        {
            var account = new AccountId(base58);
            var copy = (object)account;
            Assert.Equal(account.ToString(), copy.ToString());
        }

        [Theory]
        [InlineData("0330E7FC9D56BB25D6893BA3F317AE5BCF33B3291BD63DB32654A313222F7FD020", "rHb9CJAWyB4rj91VRWn96DkukG4bwdtyTh")]
        [InlineData("02565C453B8D74C194379C39B2B2AB68E7EFA203815248AE8769C1AD5AE10048E1", "rLVTBQ4pSQcj5rouKERrEwan1SRvC1grXH")]
        public void TestFromPublicKey(string publicKey, string expected)
        {
            var bytes = new byte[33];
            Base16.DecodeFromUtf8(System.Text.Encoding.UTF8.GetBytes(publicKey), bytes, out var _, out var _);
            var account = AccountId.FromPublicKey(bytes);
            Assert.Equal(expected, account.ToString());
        }
    }
}
