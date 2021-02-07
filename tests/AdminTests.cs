using System;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Docker.DotNet;
using Docker.DotNet.Models;
using System.Collections.Generic;

namespace Ibasa.Ripple.Tests
{
    public abstract class AdminTestsSetup : IDisposable
    {
        private static string config = @"
[server]
port_rpc_admin_local
port_ws_admin_local

# port_peer
# ssl_key = /etc/ssl/private/server.key
# ssl_cert = /etc/ssl/certs/server.crt

[port_rpc_admin_local]
port = 5005
ip = 0.0.0.0
admin = 0.0.0.0
protocol = http

[port_ws_admin_local]
port = 6006
ip = 0.0.0.0
admin = 0.0.0.0
protocol = ws

# [port_peer]
# port = 51235
# ip = 0.0.0.0
# protocol = peer

[node_size]
small

# tiny
# small
# medium
# large
# huge

[node_db]
type=NuDB
path=/var/lib/rippled/db/nudb
advisory_delete=0

# How many ledgers do we want to keep (history)?
# Integer value that defines the number of ledgers
# between online deletion events
online_delete=256

[ledger_history]
# How many ledgers do we want to keep (history)?
# Integer value (ledger count)
# or (if you have lots of TB SSD storage): 'full'
256

[database_path]
/var/lib/rippled/db

[debug_logfile]
/var/log/rippled/debug.log

[sntp_servers]
time.windows.com
time.apple.com
time.nist.gov
pool.ntp.org

[network_id]
1

[ips]
r.altnet.rippletest.net 51235

[validators_file]
validators.txt

[rpc_startup]
{ ""command"": ""log_level"", ""severity"": ""debug"" }

# severity (order: lots of information .. only errors)
# debug
# info
# warn
# error
# fatal

[ssl_verify]
0
";

        private static string validators = @"
[validator_list_sites]
https://vl.altnet.rippletest.net

[validator_list_keys]
ED264807102805220DA0F312E71FC2C69E1552C9C5790F6C25E3729DEB573D5860
";

        public AdminTestsSetup()
        {
            var configDirectory =
                System.IO.Directory.CreateDirectory(
                    System.IO.Path.Combine(
                        System.IO.Path.GetTempPath(),
                        System.IO.Path.GetRandomFileName()));

            System.IO.File.WriteAllText(
                System.IO.Path.Combine(configDirectory.FullName, "rippled.cfg"),
                config);


            System.IO.File.WriteAllText(
                System.IO.Path.Combine(configDirectory.FullName, "validators.txt"),
                validators);

            Client = new DockerClientConfiguration(null, TimeSpan.FromMinutes(1.0)).CreateClient();

            // Pull the latest image 
            var imagesCreateParameters = new ImagesCreateParameters
            {
                FromImage = "xrptipbot/rippled",
                Tag = "latest"
            };
            var progress = new Progress<JSONMessage>();
            //Client.Images.CreateImageAsync(imagesCreateParameters, null, progress).Wait();

            var createParameters = new CreateContainerParameters();
            createParameters.Volumes = new Dictionary<string, EmptyStruct>(new [] {
                KeyValuePair.Create("/config", new EmptyStruct()),
            });
            createParameters.Image = imagesCreateParameters.FromImage + ":" + imagesCreateParameters.Tag;
            createParameters.HostConfig = new HostConfig { 
                Binds = new [] { configDirectory + ":/config:ro" },
                PublishAllPorts = true 
            };
            createParameters.Cmd = new[] { "-a", "--start" };

            var container = Client.Containers.CreateContainerAsync(createParameters).Result;
            ID = container.ID;

            var startParameters = new ContainerStartParameters();
            var started = Client.Containers.StartContainerAsync(ID, startParameters).Result;
            if(!started)
            {
                Dispose();
                throw new Exception("Could not start rippled container");
            }

            var inspect = Client.Containers.InspectContainerAsync(ID).Result;

            foreach(var port in inspect.NetworkSettings.Ports)
            {
                if(port.Key == "5005/tcp")
                {
                    HttpPort = port.Value[0].HostPort;
                }

                if (port.Key == "6006/tcp")
                {
                    WsPort = port.Value[0].HostPort;
                }
            }

            // Check we can ping the server
            var address = new Uri("http://localhost:" + this.HttpPort);
            var httpClient = new HttpClient();
            httpClient.BaseAddress = address;
            var api = new JsonRpcApi(httpClient);
            try
            {
                for (int i = 0; i < 10; ++i)
                {
                    try
                    {
                        api.Ping().Wait();
                        break;
                    }
                    catch
                    {
                        if (i == 9)
                        {
                            Dispose();
                            throw;
                        }
                        System.Threading.Thread.Sleep(500);
                    }
                }
            }
            finally
            {
                api.DisposeAsync().AsTask().Wait();
            }
        }

        public void Accept()
        {
            var execParameters = new ContainerExecCreateParameters {
                Cmd = new[] { "/opt/ripple/bin/rippled", "ledger_accept", "--conf", "/etc/opt/ripple/rippled.cfg" },
            };

            var exec = Client.Exec.ExecCreateContainerAsync(ID, execParameters).Result;

            Client.Exec.StartContainerExecAsync(exec.ID).Wait();
        }

        public abstract Api Api { get; }

        public DockerClient Client;
        public string ID;

        public string WsPort;
        public string HttpPort;

        public virtual void Dispose()
        {
            var removeParameters = new ContainerRemoveParameters();
            removeParameters.Force = true;
            Client.Containers.RemoveContainerAsync(ID, removeParameters).Wait();

            Client.Dispose();
        }
    }

    public class JsonRpcAdminTestsSetup : AdminTestsSetup
    {
        public readonly JsonRpcApi RpcApi;

        public override Api Api { get { return RpcApi; } }

        public JsonRpcAdminTestsSetup()
        {
            var address = new Uri("http://localhost:" + this.HttpPort);
            var httpClient = new HttpClient();
            httpClient.BaseAddress = address;
            RpcApi = new JsonRpcApi(httpClient);
        }

        public override void Dispose()
        {
            RpcApi.DisposeAsync().AsTask().Wait();
            base.Dispose();
        }
    }

    public class JsonRpcAdminTests : AdminTests, IClassFixture<JsonRpcAdminTestsSetup>
    {
        readonly new JsonRpcApi Api;

        public JsonRpcAdminTests(JsonRpcAdminTestsSetup setup) : base(setup)
        {
            this.Api = setup.RpcApi;
        }
    }

    public class WebSocketAdminTestsSetup : AdminTestsSetup
    {
        public readonly WebSocketApi SocketApi;
        public override Api Api { get { return SocketApi; } }

        public WebSocketAdminTestsSetup()
        {
            var address = new Uri("ws://localhost:" + this.WsPort);
            var clientWebSocket = new ClientWebSocket();
            clientWebSocket.ConnectAsync(address, CancellationToken.None).Wait();
            SocketApi = new WebSocketApi(clientWebSocket);
        }

        public override void Dispose()
        {
            SocketApi.DisposeAsync().AsTask().Wait();
            base.Dispose();
        }
    }

    public class WebSocketAdminTests : AdminTests, IClassFixture<WebSocketAdminTestsSetup>
    {
        readonly new WebSocketApi Api;

        public WebSocketAdminTests(WebSocketAdminTestsSetup setup) : base(setup)
        {
            this.Api = setup.SocketApi;
        }
    }

    public abstract class AdminTests
    {
        protected readonly AdminTestsSetup Setup;
        protected Api Api { get { return Setup.Api; } }

        public AdminTests(AdminTestsSetup setup)
        {
            this.Setup = setup;
        }

        [Fact(Skip = "Causing issues on CI")]
        public async void TestPing()
        {
            // Bit of a sanity check that all the docker setup is ok
            await Api.Ping();
        }

        [Theory(Skip = "Causing issues on CI")]
        [InlineData(null)]
        [InlineData(SeedType.Secp256k1)]
        [InlineData(SeedType.Ed25519)]
        public async void TestWalletPropose_NoEntropy(SeedType? seedType)
        {
            var request = new WalletProposeRequest
            {
                KeyType = seedType,
            };
            var response = await Api.WalletPropose(request);
            var keyType = seedType == SeedType.Ed25519 ? "ed25519" : "secp256k1";
            Assert.Equal(keyType, response.KeyType);
            Assert.NotNull(response.AccountId);
            Assert.NotNull(response.PublicKey);
            Assert.NotNull(response.MasterSeed);

            var seed = new Seed(Base16.Decode(response.MasterSeed), seedType ?? SeedType.Secp256k1);
            seed.KeyPair(out _, out var keyPair);
            var publicKey = keyPair.GetCanonicalPublicKey();
            Assert.Equal(response.PublicKey, Base16.Encode(publicKey));
            var accountId = AccountId.FromPublicKey(publicKey);
            Assert.Equal(response.AccountId, accountId.ToString());
        }
    }
}