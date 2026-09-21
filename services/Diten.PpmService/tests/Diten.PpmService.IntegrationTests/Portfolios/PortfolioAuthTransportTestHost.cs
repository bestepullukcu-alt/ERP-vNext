using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Diten.PpmService.Application.Common;
using Diten.PpmService.Infrastructure.Portfolios;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Diten.PpmService.IntegrationTests.Portfolios;

// Run-owned TLS bridge. Custom trust exists only in clients created here; nothing is
// installed in a machine store. Its ticket seam does not prove PPM API authentication.
internal sealed class PortfolioAuthTransportTestHost : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly ConcurrentBag<Task> _connections = [];
    private readonly ConcurrentBag<HttpClient> _clients = [];
    private readonly ConcurrentBag<ServiceProvider> _providers = [];
    private int _accepted;
    private readonly X509Certificate2 _root;
    private readonly X509Certificate2 _server;
    private readonly HttpClient? _forward;
    private readonly Task _accept;
    private int _requests;
    private int _credentials;
    public int AcceptedConnections => Volatile.Read(ref _accepted);
    public int Requests => Volatile.Read(ref _requests);
    public int CredentialRequests => Volatile.Read(ref _credentials);
    public Uri Origin { get; }
    public Uri? Redirect { get; set; }
    public HttpStatusCode? FailureStatus { get; set; }
    public bool CleanupVerified { get; private set; }

    public PortfolioAuthTransportTestHost(PortfolioAuthProviderProcessHost? provider = null, string certificateMode = "valid")
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
        _forward = provider?.CreateForwardingClient();
        _listener.Start();
        Origin = new Uri($"https://localhost:{((IPEndPoint)_listener.LocalEndpoint).Port}/");
        _accept = AcceptAsync();
    }

    public HttpClient Client(bool trustRoot = true)
    {
        var handler = PortfolioAuthTrustedTarget.CreateHttpHandler();
        if (trustRoot)
        {
            handler.ServerCertificateCustomValidationCallback = (_, certificate, _, errors) =>
            {
                if (certificate is null || (errors & (SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateNotAvailable)) != 0) return false;
                using var chain = new X509Chain();
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.CustomTrustStore.Add(_root);
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck; // Ephemeral test CA has no revocation service.
                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
                chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.1"));
                return chain.Build(certificate);
            };
        }
        var client = new HttpClient(handler) { BaseAddress = Origin, Timeout = TimeSpan.FromSeconds(10) };
        _clients.Add(client); return client;
    }

    public PortfolioAuthorityClient Authority(HttpClient client, Guid tenant, Guid actor, string? savedToken)
    {
        var options = Options.Create(new PortfolioAuthorityOptions
        {
            Enabled = true, TimeoutSeconds = 10, ApprovedAuthOrigin = Origin.GetLeftPart(UriPartial.Authority),
            NonProductionEnvironmentName = "TransportTest", TrustProfileOwner = "run-owned-test",
            TrustProfileApprovalReference = "isolated-test-profile"
        });
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", actor.ToString("D")), new Claim("tenant_id", tenant.ToString("D")) }, "Bearer"));
        var services = new ServiceCollection().AddSingleton<IAuthenticationService>(new TestTicket(savedToken)).BuildServiceProvider();
        _providers.Add(services);
        var context = new DefaultHttpContext { User = principal, RequestServices = services };
        context.Request.Headers["X-Tenant-Id"] = tenant.ToString("D");
        // A poison raw header proves that it cannot replace the saved authentication token.
        context.Request.Headers.Authorization = "Bearer raw-header-must-not-forward";
        var trusted = new TrustedContext(tenant, actor);
        return new(client, options, new PortfolioAuthRequestContext(new TestContextAccessor { HttpContext = context }, trusted, trusted),
            new PortfolioAuthTrustedTarget(options, new TestEnvironment()));
    }

    private async Task AcceptAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var socket = await _listener.AcceptTcpClientAsync(_stop.Token);
                Interlocked.Increment(ref _accepted);
                _connections.Add(ServeAsync(socket));
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
                Interlocked.Increment(ref _requests);
                var authorization = lines.FirstOrDefault(x => x.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))?.Split(':', 2)[1].Trim();
                var tenant = lines.FirstOrDefault(x => x.StartsWith("X-Tenant-Id:", StringComparison.OrdinalIgnoreCase))?.Split(':', 2)[1].Trim();
                if (authorization is not null) Interlocked.Increment(ref _credentials);
                var path = lines[0].Split(' ')[1];
                HttpResponseMessage response;
                if (FailureStatus is { } failure) response = new(failure);
                else if (Redirect is not null) { response = new(HttpStatusCode.Redirect); response.Headers.Location = Redirect; }
                else if (_forward is not null)
                {
                    if (!lines[0].StartsWith("GET /api/users/", StringComparison.Ordinal)) throw new IOException("Test bridge route rejected.");
                    using var request = new HttpRequestMessage(HttpMethod.Get, path);
                    if (authorization is not null) request.Headers.TryAddWithoutValidation("Authorization", authorization);
                    if (tenant is not null) request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenant);
                    response = await _forward.SendAsync(request, _stop.Token);
                }
                else response = new(HttpStatusCode.OK) { Content = new StringContent("{\"data\":[],\"statusCode\":200,\"isSuccessful\":true}") };
                using (response)
                {
                    var body = await response.Content.ReadAsByteArrayAsync(_stop.Token);
                    var location = response.Headers.Location is null ? "" : $"Location: {response.Headers.Location}\r\n";
                    var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {(int)response.StatusCode} Test\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\n{location}Connection: close\r\n\r\n");
                    await tls.WriteAsync(header, _stop.Token); await tls.WriteAsync(body, _stop.Token);
                }
            }
            catch (AuthenticationException) { } // Expected for negative TLS tests; no HTTP bytes were accepted.
            catch (IOException) { } // Peer closes after a certificate rejection.
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (CleanupVerified) return;
        _stop.Cancel(); _listener.Stop();
        foreach (var client in _clients) client.Dispose();
        await _accept; await Task.WhenAll(_connections);
        foreach (var provider in _providers) await provider.DisposeAsync();
        _forward?.Dispose(); _server.Dispose(); _root.Dispose(); _stop.Dispose();
        CleanupVerified = true;
    }

    private sealed class TestContextAccessor : IHttpContextAccessor { public HttpContext? HttpContext { get; set; } }
    private sealed record TrustedContext(Guid TenantId, Guid ActorId) : ITenantContext, ICurrentActorContext;
    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "TransportTest";
        public string ApplicationName { get; set; } = "PPM isolated transport tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    private sealed class TestTicket(string? token) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            if (scheme != "Bearer") return Task.FromResult(AuthenticateResult.NoResult());
            var properties = new AuthenticationProperties();
            if (token is not null) properties.StoreTokens(new[] { new AuthenticationToken { Name = "access_token", Value = token } });
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(context.User, properties, "Bearer")));
        }
        public Task ChallengeAsync(HttpContext c, string? s, AuthenticationProperties? p) => throw new NotSupportedException();
        public Task ForbidAsync(HttpContext c, string? s, AuthenticationProperties? p) => throw new NotSupportedException();
        public Task SignInAsync(HttpContext c, string? s, ClaimsPrincipal u, AuthenticationProperties? p) => throw new NotSupportedException();
        public Task SignOutAsync(HttpContext c, string? s, AuthenticationProperties? p) => throw new NotSupportedException();
    }
}
