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

            secret.KeyPair(out var _, out var keyPair);
            var publicKey = keyPair.GetCanonicalPublicKey();
            Assert.Equal(address, AccountId.FromPublicKey(publicKey));
        }

        public static IEnumerable<object[]> entropies
        {
            get
            {
                yield return new object[] { "CF2DE378FBDD7E2EE87D486DFB5A7BFF", KeyType.Secp256k1, "sn259rEFXrQrWyx3Q7XneWcwV6dfL" };
                yield return new object[] { "00000000000000000000000000000000", KeyType.Secp256k1, "sp6JS7f14BuwFY8Mw6bTtLKWauoUs" };
                yield return new object[] { "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF", KeyType.Secp256k1, "saGwBRReqUNKuWNLpUAq8i8NkXEPN" };

                yield return new object[] { "4C3A1D213FBDFB14C7C28D609469B341", KeyType.Ed25519, "sEdTM1uX8pu2do5XvTnutH6HsouMaM2" };
                yield return new object[] { "00000000000000000000000000000000", KeyType.Ed25519, "sEdSJHS4oiAdz7w2X2ni1gFiqtbJHqE" };
                yield return new object[] { "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF", KeyType.Ed25519, "sEdV19BLfeQeKdEXyYA4NhjPJe6XBfG" };
            }
        }

        [Theory]
        [MemberData(nameof(entropies))]
        public void TestSeedConstructor(string entropy, KeyType type, string expected)
        {
            var bytes = Base16.Decode(entropy);
            var secret = new Seed(bytes, type);
            Assert.Equal(secret.ToString(), expected);
        }
    }
}