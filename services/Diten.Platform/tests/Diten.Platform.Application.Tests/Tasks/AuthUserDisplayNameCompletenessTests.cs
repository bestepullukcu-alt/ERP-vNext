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

    private static AuthUserDisplayNameClient Client(Auth auth) => new(
        auth,
        Options.Create(new AuthServiceOptions { BaseUrl = "http://auth.test", InternalApiKey = "test-key-not-a-secret" }),
        new Tenant(),
        new MemoryCache(new MemoryCacheOptions()),
        NullLogger<AuthUserDisplayNameClient>.Instance);

    private sealed class Auth(HttpStatusCode status, object[] rows, bool throws = false) : HttpMessageHandler, IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (throws) { throw new HttpRequestException("connection refused"); }
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(JsonSerializer.Serialize(rows), Encoding.UTF8, "application/json")
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
