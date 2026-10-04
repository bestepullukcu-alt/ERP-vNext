using System.Net;
using System.Text.Json;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Infrastructure.Services;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.PlatformAdministrators;

/// <summary>
/// BL-521 — THE WIRE OF THE ADMINISTRATOR SYNC AND PROVISION, on the PRODUCTION <see cref="PlatformAdministratorProvisioningService"/>
/// (only the HTTP handler underneath is fake). FIX3: both routes carry the platform tenant Auth's tenant resolution requires
/// on them, and a refusal from Auth reaches the caller as a failure. FIX4: the address only finds the account (an
/// administrator's e-mail never changes — no previous address travels any more) and the record's status travels under the
/// name Auth binds (<c>status</c>); the provision route's tenant header has its own test (item 4).
/// </summary>
public sealed class PlatformAdministratorProvisioningSyncWireTests
{
    private const string TestOnlyKey = "bl521-fix3-test-only-internal-key";

    [Fact]
    public async Task The_sync_posts_the_address_and_the_records_status_to_the_auth_sync_route()
    {
        HttpRequestMessage? seen = null;
        string? body = null;
        var service = Service(async request =>
        {
            seen = request;
            body = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        await service.SyncAsync(new PlatformAdministratorProvisioningSyncRequest(
            "the.address@bl521.test", "probe", "Probe", "PlatformAdmin", ["SupportAdmin"], "Suspended"), CancellationToken.None);

        Assert.NotNull(seen);
        Assert.Equal(HttpMethod.Post, seen!.Method);
        Assert.Equal("/api/platform-auth/platform-admins/sync", seen.RequestUri!.AbsolutePath);
        // Auth's tenant resolution answers this route 400 "Missing Tenant" without one (only the Development bypass hid it)
        Assert.Equal("00000000-0000-0000-0000-000000000001", Assert.Single(seen.Headers.GetValues("X-Tenant-Id")));
        using var json = JsonDocument.Parse(body!);
        Assert.Equal("the.address@bl521.test", json.RootElement.GetProperty("email").GetString());
        Assert.Equal("Suspended", json.RootElement.GetProperty("status").GetString());
        Assert.False(json.RootElement.TryGetProperty("previousEmail", out _));
    }

    /// <summary>FIX4 item 4 — the provision route needs the platform tenant too (FIX3 added it with a test for the sync
    /// only): without it Auth answers 400 "Missing Tenant" outside Development and no administrator can be invited.</summary>
    [Fact]
    public async Task The_provision_posts_to_the_auth_provision_route_with_the_platform_tenant()
    {
        HttpRequestMessage? seen = null;
        var service = Service(request =>
        {
            seen = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"emailSent":true}""") });
        });

        var result = await service.ProvisionAsync(new PlatformAdministratorProvisioningRequest(
            "invited@bl521.test", "invited", "Invited Probe", "PlatformAdmin", ["ReadOnly"], RequirePasswordChange: true), CancellationToken.None);

        Assert.NotNull(seen);
        Assert.Equal(HttpMethod.Post, seen!.Method);
        Assert.Equal("/api/platform-auth/platform-admins/provision", seen.RequestUri!.AbsolutePath);
        Assert.Equal("00000000-0000-0000-0000-000000000001", Assert.Single(seen.Headers.GetValues("X-Tenant-Id")));
        Assert.True(result.EmailSent);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, """{"message":"platform user not found"}""")]
    [InlineData(HttpStatusCode.Conflict, """{"message":"username already exists"}""")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "")]
    public async Task A_refusal_from_auth_is_a_failure_for_the_caller(HttpStatusCode status, string reply)
    {
        var service = Service(_ => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(reply) }));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SyncAsync(new PlatformAdministratorProvisioningSyncRequest(
            "the.address@bl521.test", "probe", "Probe", "PlatformAdmin", ["SupportAdmin"], "Active"), CancellationToken.None));
    }

    private static PlatformAdministratorProvisioningService Service(Func<HttpRequestMessage, Task<HttpResponseMessage>> reply) =>
        new(new StubFactory(new StubHandler(reply)),
            Options.Create(new AuthServiceOptions { BaseUrl = "http://auth.bl521.test", InternalApiKey = TestOnlyKey }),
            NullLogger<PlatformAdministratorProvisioningService>.Instance);

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> reply) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => reply(request);
    }
}
