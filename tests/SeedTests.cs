using System;
using Xunit;

namespace Ibasa.Ripple.Tests
{
    public class SeedTests
    {
        [Theory]
        [InlineData("snoPBrXtMeMyMHUVTgbuqAfg1SUTb")]
        public void TestRoundTrip(string base58)
        {
            var account = new Seed(base58);
            Assert.Equal(base58, account.ToString());
        }
    }
}
