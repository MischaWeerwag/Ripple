using System;
using Xunit;

namespace Ibasa.Ripple.Tests
{
    public class AccountIdTests
    {
        [Theory]
        [InlineData("r4nmanwKSE6GpkTCrBjz8uanrGZabbpSfp")]
        [InlineData("r3kmLJN5D28dHuH8vZNUZpMC43pEHpaocV")]
        [InlineData("rEopG7qc7ZWFMvCSH3GYeJJ7GPAQnKmxgw")]
        public void TestRoundTrip(string base58)
        {
            var account = new AccountId(base58);
            Assert.Equal(base58, account.ToString());
        }
    }
}
