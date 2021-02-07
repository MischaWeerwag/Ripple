using System.Threading.Tasks;
using Xunit;
using Docker.DotNet;

namespace Ibasa.Ripple.Tests
{
    public class DockerDotNetTests
    {
        [Fact(Skip = "Causing issues on CI")]
        public async Task SanityCheck()
        {
            var client = new DockerClientConfiguration().CreateClient();
            var version = await client.System.GetVersionAsync();
            Assert.NotNull(version.KernelVersion);
            Assert.NotNull(version.APIVersion);
            Assert.NotNull(version.Version);
        }
    }
}
