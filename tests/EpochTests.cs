using System;
using Xunit;

namespace Ibasa.Ripple.Tests
{
    public class EpochTests
    {
        [Fact]
        public void TestZero()
        {
            Assert.Equal(new DateTime(2000, 1, 1), Epoch.ToDateTime(0));
            Assert.Equal(0u, Epoch.FromDateTime(new DateTime(2000, 1, 1)));
        }
    }
}
