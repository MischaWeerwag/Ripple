using FsCheck;
using FsCheck.Xunit;
using Xunit;
using System;

namespace Ibasa.Ripple.Tests
{
    public class Base58Tests
    {
        [Property]
        public Property RoundTrip()
        {
            return Prop.ForAll(
                Arb.From<byte[]>(),
                bytes =>
                {
                    var base58 = Base58.ConvertTo(bytes);
                    // Oversize the buffer
                    var result = new byte[bytes.Length + 8];
                    var count = Base58.ConvertFrom(base58, result);
                    // trim to decoded size
                    result = result.AsSpan().Slice(0, count).ToArray();
                    Assert.Equal(bytes.Length, count);
                    Assert.Equal(bytes, result);
                });
        }
    }

    public class Base58CheckTests
    { 
        [Theory]
        [InlineData("r4nmanwKSE6GpkTCrBjz8uanrGZabbpSfp")]
        [InlineData("r3kmLJN5D28dHuH8vZNUZpMC43pEHpaocV")]
        [InlineData("rEopG7qc7ZWFMvCSH3GYeJJ7GPAQnKmxgw")]
        public void TestAccount(string base58)
        {
            var bytes = new byte[21];
            var count = Base58Check.ConvertFrom(base58, bytes);
            Assert.Equal(count, bytes.Length);
            Assert.Equal(base58, Base58Check.ConvertTo(bytes));
        }

        [Theory]
        [InlineData("sn259rEFXrQrWyx3Q7XneWcwV6dfL")]
        [InlineData("sp6JS7f14BuwFY8Mw6bTtLKWauoUs")]
        [InlineData("saGwBRReqUNKuWNLpUAq8i8NkXEPN")]
        public void TestSecp256k1Seed(string base58)
        {
            var bytes = new byte[17];
            var count = Base58Check.ConvertFrom(base58, bytes);
            Assert.Equal(count, bytes.Length);
            Assert.Equal(base58, Base58Check.ConvertTo(bytes));
        }

        [Theory]
        [InlineData("sEdTM1uX8pu2do5XvTnutH6HsouMaM2")]
        [InlineData("sEdSJHS4oiAdz7w2X2ni1gFiqtbJHqE")]
        [InlineData("sEdV19BLfeQeKdEXyYA4NhjPJe6XBfG")]
        public void TestEd25519Seed(string base58)
        {
            var bytes = new byte[19];
            Base58Check.ConvertFrom(base58, bytes);
            Assert.Equal(base58, Base58Check.ConvertTo(bytes));
        }

        [Property]
        public Property RoundTrip()
        {
            return Prop.ForAll(
                Arb.From<byte[]>(),
                bytes =>
                {
                    var base58 = Base58Check.ConvertTo(bytes);
                    // Oversize the buffer
                    var result = new byte[bytes.Length + 8];
                    var count = Base58Check.ConvertFrom(base58, result);
                    // trim to decoded size
                    result = result.AsSpan().Slice(0, count).ToArray();
                    Assert.Equal(bytes.Length, count);
                    Assert.Equal(bytes, result);
                });
        }
    }
}
