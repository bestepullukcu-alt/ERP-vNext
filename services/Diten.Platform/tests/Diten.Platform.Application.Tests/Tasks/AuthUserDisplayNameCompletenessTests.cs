using System.Net;
using System.Text;
using System.Text.Json;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Infrastructure.Services;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// ATT-FIX1 E1 — the AuthService name client says whether its answer is COMPLETE: a missing name after AuthService
/// answered means "no name there"; after a failed request it means "not known now". Measured on the production client.
/// </summary>
public sealed class AuthUserDisplayNameCompletenessTests
{
    [Fact]
    public async Task An_answered_request_is_complete_even_when_a_person_has_no_name()
    {
        var known = Guid.NewGuid();
        var unknown = Guid.NewGuid();
        var client = Client(new Auth(HttpStatusCode.OK, [new { id = known, displayName = "Ayşe" }]));

        var result = await client.ResolveCheckedAsync([known, unknown]);

        Assert.True(result.Complete);
        Assert.Equal("Ayşe", result.Names[known]);
        Assert.False(result.Names.ContainsKey(unknown));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task A_failed_request_is_incomplete(HttpStatusCode status)
    {
        var result = await Client(new Auth(status, [])).ResolveCheckedAsync([Guid.NewGuid()]);

        Assert.False(result.Complete);
        Assert.Empty(result.Names);
    }

    [Fact]
    public async Task A_request_that_throws_is_incomplete()
    {
        var result = await Client(new Auth(HttpStatusCode.OK, [], throws: true)).ResolveCheckedAsync([Guid.NewGuid()]);

        Assert.False(result.Complete);
    }

    // ATT-FIX2 — one failed chunk of several makes the whole answer incomplete.
    [Fact]
    public async Task One_failed_chunk_of_two_makes_the_answer_incomplete_and_two_answered_chunks_complete()
    {
        var ids = Enumerable.Range(0, 150).Select(_ => Guid.NewGuid()).ToList();

        var halfDown = await Client(new Auth(HttpStatusCode.OK, [], failFromCall: 2)).ResolveCheckedAsync(ids);
        var allUp = await Client(new Auth(HttpStatusCode.OK, [])).ResolveCheckedAsync(ids);

        Assert.False(halfDown.Complete);
        Assert.True(allUp.Complete);
    }

    [Fact]
    public async Task An_unconfigured_AuthService_is_incomplete()
    {
        var client = new AuthUserDisplayNameClient(
            new Auth(HttpStatusCode.OK, []), Options.Create(new AuthServiceOptions { BaseUrl = "", InternalApiKey = "" }),
            new Tenant(), new MemoryCache(new MemoryCacheOptions()), NullLogger<AuthUserDisplayNameClient>.Instance);

        Assert.False((await client.ResolveCheckedAsync([Guid.NewGuid()])).Complete);
    }

    [Fact]
    public async Task An_empty_payload_is_incomplete()
    {
        Assert.False((await Client(new Auth(HttpStatusCode.OK, null)).ResolveCheckedAsync([Guid.NewGuid()])).Complete);
    }

    // ATT-FIX2 — the client asks its OWN named HttpClient, registered with a short timeout (not the 100 s default).
    [Fact]
    public async Task The_client_uses_its_named_http_client_whose_timeout_is_short()
    {
        var auth = new Auth(HttpStatusCode.OK, []);
        await Client(auth).ResolveCheckedAsync([Guid.NewGuid()]);
        Assert.Equal([AuthUserDisplayNameClient.HttpClientName], auth.ClientNames);

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        AuthUserDisplayNameClient.AddAuthDisplayNameHttpClient(services);
        var factory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<IHttpClientFactory>(Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services));
        var timeout = factory.CreateClient(AuthUserDisplayNameClient.HttpClientName).Timeout;
        Assert.True(timeout <= TimeSpan.FromSeconds(5), $"timeout {timeout}");
    }

    private static AuthUserDisplayNameClient Client(Auth auth) => new(
        auth,
        Options.Create(new AuthServiceOptions { BaseUrl = "http://auth.test", InternalApiKey = "test-key-not-a-secret" }),
        new Tenant(),
        new MemoryCache(new MemoryCacheOptions()),
        NullLogger<AuthUserDisplayNameClient>.Instance);

    private sealed class Auth(HttpStatusCode status, object[]? rows, bool throws = false, int failFromCall = 0) : HttpMessageHandler, IHttpClientFactory
    {
        private int _calls;
        public List<string> ClientNames { get; } = [];

        public HttpClient CreateClient(string name) { ClientNames.Add(name); return new(this, disposeHandler: false); }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            _calls++;
            if (throws) { throw new HttpRequestException("connection refused"); }
            if (failFromCall > 0 && _calls >= failFromCall) { return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)); }
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(rows is null ? "null" : JsonSerializer.Serialize(rows), Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class Tenant : ITenantContext
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public bool IsResolved => true;
        public bool IsPlatformContext => false;
        public Guid? TargetTenantId => null;
        public void SetTenant(Guid tenantId) { }
        public void SetPlatformContext(Guid targetTenantId) { }
        public void ClearTenant() { }
    }
}
