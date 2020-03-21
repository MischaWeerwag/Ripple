using System;
using System.Collections.Generic;
using Xunit;

namespace Ibasa.Ripple.Tests
{
    public class SeedTests
    {
        public static IEnumerable<object[]> seeds
        {
            get
            {
                yield return new object[] { "snoPBrXtMeMyMHUVTgbuqAfg1SUTb" };
                yield return new object[] { "sp6JS7f14BuwFY8Mw6bTtLKWauoUs" };
            }
        }

        [Theory]
        [MemberData(nameof(seeds))]
        public void TestRoundTrip(string base58)
        {
            var seed = new Seed(base58);
            Assert.Equal(base58, seed.ToString());
        }

        [Theory]
        [MemberData(nameof(seeds))]
        public void TestCopy(string base58)
        {
            var seed = new Seed(base58);
            var copy = seed;
            Assert.Equal(seed.ToString(), copy.ToString());
        }

        [Theory]
        [MemberData(nameof(seeds))]
        public void TestBoxing(string base58)
        {
            var seed = new Seed(base58);
            var copy = (object)seed;
            Assert.Equal(seed.ToString(), copy.ToString());
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
