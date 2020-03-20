using System;
using Xunit;

namespace Ibasa.Ripple.Tests
{
    public class SeedTests
    {
        [Theory]
        [InlineData("snoPBrXtMeMyMHUVTgbuqAfg1SUTb")]
        [InlineData("sp6JS7f14BuwFY8Mw6bTtLKWauoUs")]
        public void TestRoundTrip(string base58)
        {
            var seed = new Seed(base58);
            Assert.Equal(base58, seed.ToString());
        }

        [Fact]
        public void TestKeyPair()
        {
            var account = TestAccount.Create();
            var secret = new Seed(account.Secret);
            Assert.Equal(account.Secret, secret.ToString());
            var address = new AccountId(account.Address);
            Assert.Equal(account.Address, address.ToString());

            secret.Secp256k1KeyPair(out var rootPublicKey, out var rootPrivateKey, out var publicKey, out var privateKey);

            Assert.Equal(address, AccountId.FromPublicKey(publicKey));
        }
    }
}
