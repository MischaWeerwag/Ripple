using System;
using Xunit;

namespace Ibasa.Ripple.Tests
{
    public class Base58Tests
    {
        [Theory]
        [InlineData("")]
        [InlineData("bpHzbE")]
        [InlineData("rpshnaf39wBUDNEGHJKLM4PQRST7VWXYZ2bcdeCg65jkm8oFqi1tuvAxyz")]
        [InlineData("sn259rEFXrQrWyx3Q7XneWcwV6dfL")]
        [InlineData("sEdTM1uX8pu2do5XvTnutH6HsouMaM2")]
        [InlineData("rJrRMgiRgrU6hDF4pgu5DXQdWyPbY35ErN")]
        [InlineData("rnaC7gW34M77Kneb78s")]
        [InlineData("sEdSJHS4oiAdz7w2X2ni1gFiqtbJHqE")]
        [InlineData("rLUEXYuLiQptky37CqLcm9USQpPiz5rkpD")]
        public void TestRoundTrip(string data)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(data);
            var base58 = Base58.ConvertTo(bytes);
            Array.Clear(bytes, 0, bytes.Length);
            Base58.ConvertFrom(base58, bytes);
            var result = System.Text.Encoding.UTF8.GetString(bytes);
            Assert.Equal(data, result);
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
            Base58Check.ConvertFrom(base58, bytes);
            Assert.Equal(base58, Base58Check.ConvertTo(bytes));
        }

        [Theory]
        [InlineData("sn259rEFXrQrWyx3Q7XneWcwV6dfL")]
        [InlineData("sp6JS7f14BuwFY8Mw6bTtLKWauoUs")]
        [InlineData("saGwBRReqUNKuWNLpUAq8i8NkXEPN")]
        public void TestSecp256k1Seed(string base58)
        {
            var bytes = new byte[17];
            Base58Check.ConvertFrom(base58, bytes);
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
    }
}
