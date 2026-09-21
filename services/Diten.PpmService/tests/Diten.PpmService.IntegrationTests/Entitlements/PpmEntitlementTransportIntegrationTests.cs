using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Diten.PpmService.Application.Common;
using Diten.PpmService.Infrastructure;
using Diten.PpmService.Infrastructure.Entitlements;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Diten.PpmService.IntegrationTests.Entitlements;

public sealed class PpmEntitlementTransportIntegrationTests
{
    [Theory]
    [InlineData("untrusted-root")]
    [InlineData("hostname")]
    [InlineData("expired")]
    public async Task Actual_factory_invalid_tls_fails_before_http_or_credentials(string mode)
    {
        var host = new TlsHost(mode);
        try
        {
            await using var provider = Provider(host, trust: mode != "untrusted-root");
            using var scope = provider.CreateScope();
            await Assert.ThrowsAsync<PpmEntitlementDependencyException>(() =>
                scope.ServiceProvider.GetRequiredService<IPpmEntitlementDecisionClient>().IsAllowedAsync(Guid.NewGuid(), default));
            Assert.True(host.AcceptedConnections > 0);
            Assert.Empty(host.Requests);
        }
        finally { await host.DisposeAsync(); }
        Assert.True(host.CleanupVerified);
    }

    [Fact]
    public async Task Actual_factory_trusted_requests_are_isolated_and_trace_logs_redact_service_key()
    {
        var host = new TlsHost();
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var logs = new CapturedLogs();
        var filter = new TrustFilter(host, true);
        try
        {
            await using var provider = Provider(host, secret: secret, logs: logs, filter: filter);
            var tenants = new[] { Guid.NewGuid(), Guid.NewGuid() };
            var correlations = new[] { Guid.NewGuid(), Guid.NewGuid() };
            await Task.WhenAll(tenants.Select(async (tenant, index) =>
            {
                using var scope = provider.CreateScope();
                var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
                var context = new DefaultHttpContext();
                context.Request.Headers["Authorization"] = "Bearer poison-incoming";
                context.Request.Headers["X-Tenant-Id"] = Guid.NewGuid().ToString();
                context.Request.Headers["Cookie"] = "poison=incoming";
                context.Request.Headers["X-PPM-Service-Key"] = "poison-incoming-key";
                context.Request.Headers["X-Correlation-Id"] = correlations[index].ToString("D");
                accessor.HttpContext = context;
                Assert.True(await scope.ServiceProvider.GetRequiredService<IPpmEntitlementDecisionClient>().IsAllowedAsync(tenant, default));
            }));
            Assert.Equal(1, filter.HandlerCount);
            Assert.Equal(2, host.Requests.Count);
            foreach (var request in host.Requests)
            {
                Assert.StartsWith("GET /api/internal/ppm/tenants/", request[0], StringComparison.Ordinal);
                var index = Array.FindIndex(tenants, tenant => request[0].Contains(tenant.ToString("D"), StringComparison.Ordinal));
                Assert.True(index >= 0);
                Assert.True(Header(request, "X-PPM-Service-Key") == secret, "The dedicated key must reach only the selected provider.");
                Assert.Equal(correlations[index].ToString("D"), Header(request, "X-Correlation-Id"));
                Assert.Null(Header(request, "Authorization")); Assert.Null(Header(request, "X-Tenant-Id")); Assert.Null(Header(request, "Cookie"));
            }
            Assert.Contains(logs.Messages, message => message.Contains("X-PPM-Service-Key", StringComparison.OrdinalIgnoreCase) && message.Contains("*", StringComparison.Ordinal));
            Assert.False(logs.Messages.Any(message => message.Contains(secret, StringComparison.Ordinal)), "HTTP trace logs must redact the service credential.");
            Assert.DoesNotContain(logs.Messages, message => message.Contains("poison-incoming", StringComparison.Ordinal));
        }
        finally { await host.DisposeAsync(); }
        Assert.True(host.CleanupVerified);
    }

    [Theory]
    [InlineData(302)]
    [InlineData(307)]
    [InlineData(308)]
    public async Task Actual_factory_redirect_does_not_connect_to_destination(int status)
    {
        var host = new TlsHost(); var sink = new TlsHost();
        try
        {
            host.Status = status; host.Redirect = sink.Origin;
            await using var provider = Provider(host);
            using var scope = provider.CreateScope();
            await Assert.ThrowsAsync<PpmEntitlementDependencyException>(() =>
                scope.ServiceProvider.GetRequiredService<IPpmEntitlementDecisionClient>().IsAllowedAsync(Guid.NewGuid(), default));
            Assert.Single(host.Requests);
            Assert.Equal(0, sink.AcceptedConnections);
            Assert.Empty(sink.Requests);
        }
        finally { await host.DisposeAsync(); await sink.DisposeAsync(); }
        Assert.True(host.CleanupVerified); Assert.True(sink.CleanupVerified);
    }

    [Theory]
    [InlineData("http")]
    [InlineData("userinfo")]
    [InlineData("empty-userinfo")]
    [InlineData("path")]
    [InlineData("query")]
    [InlineData("fragment")]
    public async Task Actual_factory_unacceptable_origin_has_no_network(string mode)
    {
        var host = new TlsHost();
        try
        {
            var origin = mode switch
            {
                "http" => host.Origin.ToString().Replace("https:", "http:", StringComparison.Ordinal),
                "userinfo" => host.Origin.ToString().Replace("https://", "https://user@", StringComparison.Ordinal),
                "empty-userinfo" => host.Origin.ToString().Replace("https://", "https://@", StringComparison.Ordinal),
                "path" => host.Origin + "other", "query" => host.Origin + "?target=other", _ => host.Origin + "#other"
            };
            await using var provider = Provider(host, origin: origin);
            using var scope = provider.CreateScope();
            await Assert.ThrowsAsync<PpmEntitlementDependencyException>(() =>
                scope.ServiceProvider.GetRequiredService<IPpmEntitlementDecisionClient>().IsAllowedAsync(Guid.NewGuid(), default));
            Assert.Equal(0, host.AcceptedConnections); Assert.Empty(host.Requests);
        }
        finally { await host.DisposeAsync(); }
        Assert.True(host.CleanupVerified);
    }

    [Fact]
    public async Task Actual_factory_cookie_response_is_not_carried_to_next_request()
    {
        var host = new TlsHost();
        try
        {
            await using var provider = Provider(host);
            using var scope = provider.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<IPpmEntitlementDecisionClient>();
            Assert.True(await client.IsAllowedAsync(Guid.NewGuid(), default));
            Assert.True(await client.IsAllowedAsync(Guid.NewGuid(), default));
            Assert.Equal(2, host.Requests.Count);
            Assert.All(host.Requests, request => Assert.Null(Header(request, "Cookie")));
        }
        finally { await host.DisposeAsync(); }
        Assert.True(host.CleanupVerified);
    }

    private static ServiceProvider Provider(TlsHost host, bool trust = true, string? origin = null,
        string? secret = null, CapturedLogs? logs = null, TrustFilter? filter = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PpmEntitlementDecision:Enabled"] = "true", ["PpmEntitlementDecision:BaseUrl"] = origin ?? host.Origin.ToString(),
            ["PpmEntitlementDecision:ServiceCredential"] = secret ?? Convert.ToHexString(RandomNumberGenerator.GetBytes(24)),
            ["PpmEntitlementDecision:TimeoutSeconds"] = "5"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging(builder => { builder.SetMinimumLevel(LogLevel.Trace); if (logs is not null) builder.AddProvider(logs); });
        services.AddInfrastructure(configuration);
        services.AddSingleton<IHttpMessageHandlerBuilderFilter>(filter ?? new TrustFilter(host, trust));
        return services.BuildServiceProvider();
    }

    private static string? Header(string[] request, string name) => request.FirstOrDefault(line =>
        line.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase))?.Split(':', 2)[1].Trim();

    // Inspect the real production factory's handler, then add only this run's test CA.
    // No handler replacement, trust-all callback, environment proxy edit or machine store change.
    private sealed class TrustFilter(TlsHost host, bool trust) : IHttpMessageHandlerBuilderFilter
    {
        public int HandlerCount { get; private set; }
        public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) => builder =>
        {
            next(builder);
            var handler = Assert.IsType<HttpClientHandler>(builder.PrimaryHandler);
            Assert.False(handler.AllowAutoRedirect); Assert.False(handler.UseProxy); Assert.False(handler.UseCookies);
            Assert.True(handler.CheckCertificateRevocationList); Assert.Null(handler.ServerCertificateCustomValidationCallback);
            HandlerCount++;
            if (trust) handler.ServerCertificateCustomValidationCallback = host.Validate;
        };
    }

    private sealed class CapturedLogs : ILoggerProvider
    {
        public ConcurrentBag<string> Messages { get; } = [];
        public ILogger CreateLogger(string categoryName) => new Logger(Messages);
        public void Dispose() { }
        private sealed class Logger(ConcurrentBag<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => messages.Add(formatter(state, exception));
        }
    }

    private sealed class TlsHost : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly ConcurrentBag<Task> _connections = [];
        private readonly X509Certificate2 _root;
        private readonly X509Certificate2 _server;
        private readonly Task _accept;
        private int _accepted;
        public int AcceptedConnections => Volatile.Read(ref _accepted);
        public ConcurrentBag<string[]> Requests { get; } = [];
        public Uri Origin { get; }
        public int Status { get; set; } = 200;
        public Uri? Redirect { get; set; }
        public bool CleanupVerified { get; private set; }
        public TlsHost(string certificateMode = "valid")
        {
            using var key = RSA.Create(2048);
            var ca = new CertificateRequest("CN=PPM disposable transport test root", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            ca.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            ca.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
            _root = ca.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-3), DateTimeOffset.UtcNow.AddDays(2));
            using var serverKey = RSA.Create(2048);
            var leaf = new CertificateRequest("CN=localhost", serverKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            leaf.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            leaf.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
            leaf.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
            var names = new SubjectAlternativeNameBuilder(); names.AddDnsName(certificateMode == "hostname" ? "wrong.invalid" : "localhost");
            leaf.CertificateExtensions.Add(names.Build());
            using var issued = leaf.Create(_root, DateTimeOffset.UtcNow.AddDays(-2), certificateMode == "expired" ? DateTimeOffset.UtcNow.AddDays(-1) : DateTimeOffset.UtcNow.AddDays(1), RandomNumberGenerator.GetBytes(16));
            _server = issued.CopyWithPrivateKey(serverKey);
            _listener.Start();
            Origin = new Uri($"https://localhost:{((IPEndPoint)_listener.LocalEndpoint).Port}/");
            _accept = AcceptAsync();
        }

        public bool Validate(HttpRequestMessage request, X509Certificate2? certificate, X509Chain? suppliedChain, SslPolicyErrors errors)
        {
            if (certificate is null || (errors & (SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateNotAvailable)) != 0) return false;
            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(_root);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck; // This disposable CA has no revocation service; runtime is unchanged.
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
            chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.1"));
            return chain.Build(certificate);
        }

        private async Task AcceptAsync()
        {
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    var socket = await _listener.AcceptTcpClientAsync(_stop.Token);
                    Interlocked.Increment(ref _accepted); _connections.Add(ServeAsync(socket));
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
            catch (SocketException) when (_stop.IsCancellationRequested) { }
        }

        private async Task ServeAsync(TcpClient socket)
        {
            using (socket)
            using (var tls = new SslStream(socket.GetStream(), false))
            {
                try
                {
                    await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = _server, EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13 }, _stop.Token);
                    var bytes = new List<byte>(); var single = new byte[1];
                    while (bytes.Count < 32768)
                    {
                        if (await tls.ReadAsync(single, _stop.Token) == 0) return;
                        bytes.Add(single[0]);
                        if (bytes.Count >= 4 && bytes.TakeLast(4).SequenceEqual(new byte[] { 13, 10, 13, 10 })) break;
                    }
                    var lines = Encoding.ASCII.GetString(bytes.ToArray()).Split("\r\n", StringSplitOptions.None);
                    Requests.Add(lines);
                    var path = lines[0].Split(' ')[1].Split('/');
                    var tenant = path.Length > 5 && Guid.TryParse(path[5], out var id) ? id : Guid.Empty;
                    var body = Encoding.UTF8.GetBytes($$"""{"tenantId":"{{tenant:D}}","moduleCode":"PPM","isAllowed":true,"resolvedAtUtc":"2026-01-01T00:00:00+00:00","expiresAtUtc":null}""");
                    var location = Redirect is null ? "" : $"Location: {Redirect}\r\n";
                    var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {Status} Test\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\n{location}Set-Cookie: test=must-not-return; Path=/\r\nConnection: close\r\n\r\n");
                    await tls.WriteAsync(header, _stop.Token); await tls.WriteAsync(body, _stop.Token);
                }
                catch (AuthenticationException) { }
                catch (IOException) { }
                catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (CleanupVerified) return;
            _stop.Cancel(); _listener.Stop();
            await _accept; await Task.WhenAll(_connections);
            _server.Dispose(); _root.Dispose(); _stop.Dispose();
            CleanupVerified = true;
        }
    }
}
