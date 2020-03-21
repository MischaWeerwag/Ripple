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
    }
}
