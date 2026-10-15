using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Auth;

/// <summary>
/// BL-521 FIX5 item 1 — THE STATUS CLIENT NEVER FOLLOWS A REDIRECT. Auth asks Platform about an administrator with the
/// internal API key in a custom header (<c>X-Internal-Api-Key</c>); HttpClient drops the Authorization header on a redirect but
/// never a custom one, so a client that followed a 3xx handed the key to whatever host the answer named — and since FIX4 the
/// answer also decides the roles a token is minted with. Measured the way it happens: the PRODUCTION Api (the production DI
/// registration of the typed status client, the production platform sign-in) over a test-owned mongod, with a REAL loopback
/// server standing in for Platform that answers 302 to a second path on the same host. The trap path is never reached; the
/// sign-in answers 503 (no answer: nothing minted, no session revoked), not a 200 and not a "no".
///
/// <para>The same registration bounds the call in time: a Platform that never answers is no answer after the configured
/// timeout, not a sign-in that hangs.</para>
///
/// <para>Disposable platform users only — never the seeded admin.</para>
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class PlatformStatusClientRedirectTests : IClassFixture<PlatformStatusClientRedirectTests.Host>, IClassFixture<PlatformStatusClientRedirectTests.LoopbackLifetime>
{
    private static readonly Guid PlatformTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const string Password = "Redirect!Probe2026";
    private const string TestOnlyInternalKey = "bl521-fix5-test-only-internal-key";

    private readonly Host _host;

    public PlatformStatusClientRedirectTests(Host host, LoopbackLifetime _) => _host = host;

    /// <summary>The production Api, with the Platform base address pointed at the loopback server (the typed client's own
    /// registration — its primary handler included — is left exactly as production registers it).</summary>
    public sealed class Host : AccountKindAcceptance.AuthTestHost
    {
        protected override void ConfigureTestServices(IServiceCollection services)
        {
            base.ConfigureTestServices(services);
            services.PostConfigure<PlatformServiceOptions>(options =>
            {
                options.BaseUrl = LoopbackPlatform.Instance.Url;
                options.InternalApiKey = TestOnlyInternalKey;
                options.TimeoutSeconds = 1;
            });
        }
    }

    public sealed class LoopbackLifetime : IDisposable
    {
        public void Dispose() => LoopbackPlatform.Instance.Dispose();
    }

    [Fact]
    public async Task A_redirect_from_platform_is_never_followed_and_the_sign_in_answers_503_without_revoking_anything()
    {
        LoopbackPlatform.Instance.Reset();
        var seeded = await SeedAsync("redirect");

        var response = await SignInAsync(seeded.Email);

        Assert.True(response.StatusCode == HttpStatusCode.ServiceUnavailable, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        Assert.True(LoopbackPlatform.Instance.Asked > 0, "the sign-in never reached the loopback Platform: this test proves nothing.");
        Assert.Equal(0, LoopbackPlatform.Instance.Trapped);
        Assert.Null((await RefreshRowAsync(seeded.RefreshToken))!.RevokedAt);
    }

    [Fact]
    public async Task A_platform_that_never_answers_is_no_answer_after_the_timeout_not_a_hung_sign_in()
    {
        LoopbackPlatform.Instance.Reset();
        var seeded = await SeedAsync("hang");
        var started = DateTime.UtcNow;

        var response = await SignInAsync(seeded.Email);

        Assert.True(response.StatusCode == HttpStatusCode.ServiceUnavailable, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(8), "the sign-in waited for the hanging Platform instead of giving up at its timeout.");
        Assert.True(LoopbackPlatform.Instance.Asked > 0);
        Assert.Null((await RefreshRowAsync(seeded.RefreshToken))!.RevokedAt);
    }

    private sealed record Seeded(Guid UserId, string Email, string RefreshToken);

    private async Task<HttpResponseMessage> SignInAsync(string email)
    {
        using var client = _host.Client(tenantHeader: PlatformTenantId);
        return await client.PostAsJsonAsync("api/platform-auth/login", new { email, password = Password, rememberMe = false });
    }

    /// <param name="mode">"redirect" or "hang": the loopback Platform decides by the address it is asked about.</param>
    private async Task<Seeded> SeedAsync(string mode)
    {
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(PlatformTenantId);
        var user = new User($"{mode}.{Guid.NewGuid():N}@bl521.test", sp.GetRequiredService<IPasswordHasher>().Hash(Password), "Redirect", "Probe", PlatformTenantId);
        user.ConfirmEmail();
        user.SetPlatformActorType("platform_admin");
        user = await sp.GetRequiredService<IUserRepository>().CreateAsync(user, CancellationToken.None);
        var raw = $"existing-{Guid.NewGuid():N}";
        await sp.GetRequiredService<IRefreshTokenRepository>().CreateAsync(new RefreshToken(
            user.Id, sp.GetRequiredService<IRefreshTokenHasher>().Hash(raw), DateTime.UtcNow.AddDays(7), "127.0.0.1",
            PlatformTenantId, "platform_admin", "bl521-fix5"), CancellationToken.None);
        return new Seeded(user.Id, user.Email, raw);
    }

    private async Task<RefreshToken?> RefreshRowAsync(string rawToken)
    {
        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(PlatformTenantId);
        return await scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>().GetByTokenAsync(rawToken, CancellationToken.None);
    }

    /// <summary>
    /// A loopback stand-in for Platform: a status ask for an address that starts with "redirect." is answered 302 to
    /// <c>/trap</c> on the same host; one that starts with "hang." is not answered for ten seconds. Counts what it was asked
    /// and how often the trap path was reached.
    /// </summary>
    internal sealed class LoopbackPlatform : IDisposable
    {
        private static readonly Lazy<LoopbackPlatform> Shared = new(() => new LoopbackPlatform());
        private readonly HttpListener _listener = new();
        private int _asked;
        private int _trapped;

        public static LoopbackPlatform Instance => Shared.Value;

        private LoopbackPlatform()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            Url = $"http://127.0.0.1:{port}";
            _listener.Prefixes.Add(Url + "/");
            _listener.Start();
            _ = Task.Run(LoopAsync);
        }

        public string Url { get; }
        public int Asked => Volatile.Read(ref _asked);
        public int Trapped => Volatile.Read(ref _trapped);

        public void Reset()
        {
            Interlocked.Exchange(ref _asked, 0);
            Interlocked.Exchange(ref _trapped, 0);
        }

        private async Task LoopAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch
                {
                    return;
                }

                _ = Task.Run(() => AnswerAsync(context));
            }
        }

        private async Task AnswerAsync(HttpListenerContext context)
        {
            try
            {
                if (context.Request.Url!.AbsolutePath.StartsWith("/trap", StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref _trapped);
                    context.Response.StatusCode = 200;
                }
                else
                {
                    Interlocked.Increment(ref _asked);
                    var email = Uri.UnescapeDataString(context.Request.QueryString["email"] ?? string.Empty);
                    if (email.StartsWith("hang.", StringComparison.Ordinal))
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10));
                        context.Response.StatusCode = 200;
                    }
                    else
                    {
                        context.Response.StatusCode = 302;
                        context.Response.RedirectLocation = Url + "/trap";
                    }
                }

                var bytes = Encoding.UTF8.GetBytes("{}");
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
            }
            catch
            {
                // the caller gave up (timeout) — nothing left to answer
            }
        }

        public void Dispose()
        {
            _listener.Stop();
            _listener.Close();
        }
    }
}
