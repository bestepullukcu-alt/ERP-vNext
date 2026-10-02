using System.Net;
using System.Text;
using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Application.Tests.Email;

/// <summary>
/// BL-454 — AuthService asking Platform who a tenant's e-mail is from. The real client over a scripted
/// <see cref="HttpMessageHandler"/>: what it sends, what it makes of each answer, and that no answer at all never
/// becomes an exception or a long wait.
/// </summary>
public sealed class PlatformTenantEmailIdentityClientTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private const string Answer =
        """{"data":{"tenantId":"11111111-2222-3333-4444-555555555555","displayName":" Diten Pharma ","language":"tr","senderName":null,"replyToEmail":"ik@ditenpharma.test"},"isSuccessful":true}""";

    [Fact]
    public async Task It_asks_the_internal_endpoint_with_the_internal_key_and_reads_the_four_values()
    {
        var http = new Scripted(_ => Task.FromResult(Json(HttpStatusCode.OK, Answer)));

        var identity = await Client(http).GetAsync(Tenant, CancellationToken.None);

        var request = Assert.Single(http.Requests);
        Assert.Equal("/api/internal/tenants/11111111-2222-3333-4444-555555555555/email-identity", request.Path);
        Assert.Equal("internal-key", request.ApiKey);
        Assert.NotNull(identity);
        Assert.Equal("Diten Pharma", identity!.DisplayName);
        Assert.Equal("tr", identity.Language);
        Assert.Null(identity.SenderName);
        Assert.Equal("ik@ditenpharma.test", identity.ReplyToEmail);
    }

    [Fact]
    public async Task An_answer_is_kept_for_a_short_while_and_then_asked_again()
    {
        var now = DateTimeOffset.Parse("2026-10-02T10:00:00Z");
        var cache = new TenantEmailIdentityCache(() => now);
        var http = new Scripted(_ => Task.FromResult(Json(HttpStatusCode.OK, Answer)));

        await Client(http, cache).GetAsync(Tenant, CancellationToken.None);
        await Client(http, cache).GetAsync(Tenant, CancellationToken.None);
        Assert.Single(http.Requests);

        now = now.Add(TenantEmailIdentityCache.Lifetime).AddSeconds(1);
        await Client(http, cache).GetAsync(Tenant, CancellationToken.None);
        Assert.Equal(2, http.Requests.Count);
        Assert.True(TenantEmailIdentityCache.Lifetime <= TimeSpan.FromMinutes(10));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task A_refusal_is_no_identity_and_is_not_remembered(HttpStatusCode status)
    {
        var cache = new TenantEmailIdentityCache();
        var http = new Scripted(_ => Task.FromResult(Json(status, """{"isSuccessful":false,"errors":["x"]}""")));

        Assert.Null(await Client(http, cache).GetAsync(Tenant, CancellationToken.None));
        Assert.Null(await Client(http, cache).GetAsync(Tenant, CancellationToken.None));

        Assert.Equal(2, http.Requests.Count);
    }

    [Fact]
    public async Task A_Platform_that_is_down_is_no_identity_not_an_exception()
    {
        var http = new Scripted(_ => Task.FromException<HttpResponseMessage>(new HttpRequestException("Connection refused")));

        Assert.Null(await Client(http).GetAsync(Tenant, CancellationToken.None));
    }

    [Fact]
    public async Task A_Platform_that_does_not_answer_in_time_is_given_up_on()
    {
        var http = new Scripted(async ct =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return Json(HttpStatusCode.OK, Answer);
        });
        var client = Client(http, timeout: TimeSpan.FromMilliseconds(150));

        var started = DateTimeOffset.UtcNow;
        var identity = await client.GetAsync(Tenant, CancellationToken.None);

        Assert.Null(identity);
        Assert.True(DateTimeOffset.UtcNow - started < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void The_longest_an_email_waits_for_Platform_is_two_seconds()
    {
        Assert.True(PlatformTenantEmailIdentityClient.Timeout <= TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task A_garbled_or_empty_answer_is_no_identity()
    {
        Assert.Null(await Client(new Scripted(_ => Task.FromResult(Json(HttpStatusCode.OK, "not json")))).GetAsync(Tenant, CancellationToken.None));
        Assert.Null(await Client(new Scripted(_ => Task.FromResult(Json(HttpStatusCode.OK, """{"data":null}""")))).GetAsync(Tenant, CancellationToken.None));
        Assert.Null(await Client(new Scripted(_ => Task.FromResult(Json(HttpStatusCode.OK, """{"data":{"displayName":" "}}""")))).GetAsync(Tenant, CancellationToken.None));
    }

    [Fact]
    public async Task Without_a_configured_key_or_a_tenant_Platform_is_not_called()
    {
        var http = new Scripted(_ => Task.FromResult(Json(HttpStatusCode.OK, Answer)));

        Assert.Null(await Client(http, apiKey: "").GetAsync(Tenant, CancellationToken.None));
        Assert.Null(await Client(http).GetAsync(Guid.Empty, CancellationToken.None));

        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task The_callers_own_cancellation_is_still_a_cancellation()
    {
        var http = new Scripted(async ct =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return Json(HttpStatusCode.OK, Answer);
        });
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(http).GetAsync(Tenant, cancel.Token));
    }

    private static PlatformTenantEmailIdentityClient Client(
        Scripted http, TenantEmailIdentityCache? cache = null, string apiKey = "internal-key", TimeSpan? timeout = null) => new(
        new HttpClient(http, disposeHandler: false)
        {
            BaseAddress = new Uri("http://platform.invalid"),
            Timeout = timeout ?? PlatformTenantEmailIdentityClient.Timeout
        },
        cache ?? new TenantEmailIdentityCache(),
        Options.Create(new PlatformServiceOptions { InternalApiKey = apiKey }),
        NullLogger<PlatformTenantEmailIdentityClient>.Instance);

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class Scripted : HttpMessageHandler
    {
        private readonly Func<CancellationToken, Task<HttpResponseMessage>> _respond;

        public Scripted(Func<CancellationToken, Task<HttpResponseMessage>> respond) => _respond = respond;

        public List<(string Path, string? ApiKey)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((
                request.RequestUri!.AbsolutePath,
                request.Headers.TryGetValues("X-Internal-Api-Key", out var values) ? values.Single() : null));
            return _respond(cancellationToken);
        }
    }
}
