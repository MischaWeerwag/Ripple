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

        [Theory]
        [InlineData("shF2uPgigYYtNf18Hr3T3vcz56qQA", "rpA1ah984RJuHQD62Af9aLZSXetMoVkxoN")]
        [InlineData("saw6aVtyPq5GoYQyELPGExwigH8sX", "rsUc54BeaqwZmbdFByUpi17koAXEFbRpaL")]
        [InlineData("snY7jedC5sCqREpRt99y6f4aKxWJe", "rJzPvpe6biTPHrTB11DuQDnfU6jEzLgheZ")]
        [InlineData("shHHvEhCC6WdkJNV1AfBmqw3XJ3Uv", "rJgzgUKLw6dUoDFXgbHNb2euXdMcNn6ioh")]
        [InlineData("shbwxB2QWXVwviXArd8tNTu2Yu9LD", "r4q4qa57DsL6p6RtTwmh3WEmLZnP6jqdez")]
        [InlineData("snMp2HgqGPR2iWrGPt7FjYvBmRvNN", "rDq6a58wfbANJsR2H44zttaMihHFkV9hbm")]
        public void TestSeedToAccountId(string seed, string account)
        {
            var secret = new Seed(seed);
            var address = new AccountId(account);

            secret.Secp256k1KeyPair(out var _, out var keyPair);
            var publicKey = keyPair.GetCanonicalPublicKey();
            Assert.Equal(address, AccountId.FromPublicKey(publicKey));
        }
    }
}
