using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GatewayProtocol;
using Grpc.Core;
using Grpc.Core.Logging;
using NLog;
using NUnit.Framework;
using Zeebe.Client.Api.Builder;

namespace Zeebe.Client
{
    [TestFixture]
    public class ZeebeClientTest
    {
        private static readonly string ServerCertPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "chain.cert.pem");
        private static readonly string ClientCertPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "chain.cert.pem");
        private static readonly string ServerKeyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "private.key.pem");

        private static readonly string WrongCertPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "server.crt");

        [Test]
        public void ShouldThrowExceptionAfterDispose()
        {
            // given
            var zeebeClient = ZeebeClient.Builder()
                    .UseGatewayAddress("localhost:26500")
                    .UsePlainText()
                    .Build();

            // when
            zeebeClient.Dispose();

            // then
            var aggregateException = Assert.Throws<AggregateException>(
                () => zeebeClient.TopologyRequest().Send().Wait());

            Assert.AreEqual(1, aggregateException.InnerExceptions.Count);

            var catchedExceptions = aggregateException.InnerExceptions[0];
            Assert.IsTrue(catchedExceptions.Message.Contains("ZeebeClient was already disposed."));
            Assert.IsInstanceOf(typeof(ObjectDisposedException), catchedExceptions);
        }

        [Test]
        public void ShouldNotThrowExceptionWhenDisposingMultipleTimes()
        {
            // given
            var zeebeClient = ZeebeClient.Builder()
                .UseGatewayAddress("localhost:26500")
                .UsePlainText()
                .Build();

            // when
            zeebeClient.Dispose();

            // then
            Assert.DoesNotThrow(() => zeebeClient.Dispose());
        }        

        [Test]
        public async Task ShouldUseTransportEncryption()
        {
            // given
            GrpcEnvironment.SetLogger(new ConsoleLogger());

            var keyCertificatePairs = new List<KeyCertificatePair>();
            var serverCert = File.ReadAllText(ServerCertPath);
            keyCertificatePairs.Add(new KeyCertificatePair(serverCert, File.ReadAllText(ServerKeyPath)));
            var channelCredentials = new SslServerCredentials(keyCertificatePairs);

            var server = new Server();
            server.Ports.Add(new ServerPort("0.0.0.0", 26505, channelCredentials));

            var testService = new GatewayTestService();
            var serviceDefinition = Gateway.BindService(testService);
            server.Services.Add(serviceDefinition);
            server.Start();

            // client
            var zeebeClient = ZeebeClient.Builder()
                    .UseGatewayAddress("0.0.0.0:26505")
                    .UseTransportEncryption(ClientCertPath)
                    .Build();

            // when
            var publishMessageResponse = await zeebeClient
                .NewPublishMessageCommand()
                .MessageName("messageName")
                .CorrelationKey("p-1")
                .Send();

            // then
            Assert.NotNull(publishMessageResponse);
        }

        [Test]
        public async Task ShouldUseTransportEncryptionWithServerCert()
        {
            // given
            GrpcEnvironment.SetLogger(new ConsoleLogger());

            var keyCertificatePairs = new List<KeyCertificatePair>();
            var serverCert = File.ReadAllText(ServerCertPath);
            keyCertificatePairs.Add(new KeyCertificatePair(serverCert, File.ReadAllText(ServerKeyPath)));
            var channelCredentials = new SslServerCredentials(keyCertificatePairs);

            var server = new Server();
            server.Ports.Add(new ServerPort("0.0.0.0", 26505, channelCredentials));

            var testService = new GatewayTestService();
            var serviceDefinition = Gateway.BindService(testService);
            server.Services.Add(serviceDefinition);
            server.Start();

            // client
            var zeebeClient = ZeebeClient.Builder()
                .UseGatewayAddress("0.0.0.0:26505")
                .UseTransportEncryption(ServerCertPath)
                .Build();

            // when
            var publishMessageResponse = await zeebeClient
                .NewPublishMessageCommand()
                .MessageName("messageName")
                .CorrelationKey("p-1")
                .Send();

            // then
            Assert.NotNull(publishMessageResponse);
        }

        [Test]
        public async Task ShouldFailOnWrongCert()
        {
            // given
            GrpcEnvironment.SetLogger(new ConsoleLogger());

            var keyCertificatePairs = new List<KeyCertificatePair>();
            var serverCert = File.ReadAllText(ServerCertPath);
            keyCertificatePairs.Add(new KeyCertificatePair(serverCert, File.ReadAllText(ServerKeyPath)));
            var channelCredentials = new SslServerCredentials(keyCertificatePairs);

            var server = new Server();
            server.Ports.Add(new ServerPort("0.0.0.0", 26505, channelCredentials));

            var testService = new GatewayTestService();
            var serviceDefinition = Gateway.BindService(testService);
            server.Services.Add(serviceDefinition);
            server.Start();

            // client
            var zeebeClient = ZeebeClient.Builder()
                .UseGatewayAddress("0.0.0.0:26505")
                .UseTransportEncryption(WrongCertPath)
                .Build();

            // when
            try
            {
                await zeebeClient
                    .NewPublishMessageCommand()
                    .MessageName("messageName")
                    .CorrelationKey("p-1")
                    .Send();
                Assert.Fail();
            }
            catch (RpcException rpcException)
            {
                // expected
                Assert.AreEqual(rpcException.Status.StatusCode, StatusCode.Unavailable);
            }
        }

        [Test]
        public async Task ShouldUseCredentialsProvider()
        {
            // given
            GrpcEnvironment.SetLogger(new ConsoleLogger());

            Metadata sentMetadata = null;

            var keyCertificatePairs = new List<KeyCertificatePair>();
            var serverCert = File.ReadAllText(ServerCertPath);
            keyCertificatePairs.Add(new KeyCertificatePair(serverCert, File.ReadAllText(ServerKeyPath)));
            var channelCredentials = new SslServerCredentials(keyCertificatePairs);

            var server = new Server();
            server.Ports.Add(new ServerPort("0.0.0.0", 26505, channelCredentials));

            var testService = new GatewayTestService();
            testService.ConsumeRequestHeaders(metadata => {
                Console.WriteLine(metadata.Count);
                sentMetadata = metadata; 
            });
            var serviceDefinition = Gateway.BindService(testService);
            server.Services.Add(serviceDefinition);
            server.Start();

            // client
            var zeebeClient = ZeebeClient.Builder()
                .UseGatewayAddress("0.0.0.0:26505")
                .UseTransportEncryption(ClientCertPath)
                .UseCredentialsProvider(new SimpleCredentialsProvider())
                .Build();

            // when
            await zeebeClient.TopologyRequest().Send();
            await zeebeClient.TopologyRequest().Send();
            var topology = await zeebeClient.TopologyRequest().Send();

            // then
            Assert.NotNull(sentMetadata);

            var auth = sentMetadata.Get("Authorization".ToLower());
            Assert.NotNull(auth);
            Assert.IsTrue(auth.Value.Contains("Basic dXNlcjpwYXNzd29yZAo="));

            var customValue = sentMetadata.Get("CustomHeader".ToLower());
            Assert.NotNull(customValue);
            Assert.IsTrue(customValue.Value.Contains("CustomValue"));
        }

        [Test]
        public async Task ShouldUseAccessToken()
        {
            // given
            GrpcEnvironment.SetLogger(new ConsoleLogger());

            var keyCertificatePairs = new List<KeyCertificatePair>();
            var serverCert = File.ReadAllText(ServerCertPath);
            keyCertificatePairs.Add(new KeyCertificatePair(serverCert, File.ReadAllText(ServerKeyPath)));
            var channelCredentials = new SslServerCredentials(keyCertificatePairs);

            var server = new Server();
            server.Ports.Add(new ServerPort("0.0.0.0", 26505, channelCredentials));

            var testService = new GatewayTestService();
            var serviceDefinition = Gateway.BindService(testService);
            server.Services.Add(serviceDefinition);
            server.Start();

            // client
            var zeebeClient = ZeebeClient.Builder()
                .UseGatewayAddress("0.0.0.0:26505")
                .UseTransportEncryption(ClientCertPath)
                .UseAccessToken("token")
                .Build();

            // when
            await zeebeClient.TopologyRequest().Send();
            await zeebeClient.TopologyRequest().Send();
            var topology = await zeebeClient.TopologyRequest().Send();

            // then
            Assert.NotNull(topology);
        }

        [Test]
        public async Task ShouldUseAccessTokenSupplier()
        {
            // given
            GrpcEnvironment.SetLogger(new ConsoleLogger());

            var keyCertificatePairs = new List<KeyCertificatePair>();
            var serverCert = File.ReadAllText(ServerCertPath);
            keyCertificatePairs.Add(new KeyCertificatePair(serverCert, File.ReadAllText(ServerKeyPath)));
            var channelCredentials = new SslServerCredentials(keyCertificatePairs);

            var server = new Server();
            server.Ports.Add(new ServerPort("0.0.0.0", 26505, channelCredentials));

            var testService = new GatewayTestService();
            var serviceDefinition = Gateway.BindService(testService);
            server.Services.Add(serviceDefinition);
            server.Start();

            // client
            var accessTokenSupplier = new SimpleAccessTokenSupplier();
            var zeebeClient = ZeebeClient.Builder()
                .UseGatewayAddress("0.0.0.0:26505")
                .UseTransportEncryption(ClientCertPath)
                .UseAccessTokenSupplier(accessTokenSupplier)
                .Build();

            // when
            await zeebeClient.TopologyRequest().Send();
            await zeebeClient.TopologyRequest().Send();
            var topology = await zeebeClient.TopologyRequest().Send();

            // then
            Assert.NotNull(topology);
            Assert.AreEqual(3, accessTokenSupplier.Count);
        }

        private class SimpleCredentialsProvider : ICredentialsProvider
        {
            public void ApplyCredentials(AuthInterceptorContext context, Metadata metadata)
            {
                metadata.Add("Authorization", "Basic dXNlcjpwYXNzd29yZAo=");
                metadata.Add("CustomHeader", "CustomValue");
            }
        }

        private class SimpleAccessTokenSupplier : IAccessTokenSupplier
        {
            public int Count { get; private set; }

            public Task<string> GetAccessTokenForRequestAsync(
                string authUri = null,
                CancellationToken cancellationToken = default(CancellationToken))
            {
                Count++;
                return Task.FromResult("token");
            }
        }
    }
}