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
/// BL-521 FIX3 item 2(a) — THE WIRE OF THE ADMINISTRATOR SYNC, on the PRODUCTION
/// <see cref="PlatformAdministratorProvisioningService"/> (only the HTTP handler underneath is fake). Auth finds the account by
/// the address the record had BEFORE the change and moves it to the new one, so the previous address must travel in the body
/// under the name Auth binds (<c>previousEmail</c>), with the platform tenant Auth's tenant resolution requires on this
/// route; a refusal from Auth must reach the caller as a failure (the handler then writes nothing).
/// </summary>
public sealed class PlatformAdministratorProvisioningSyncWireTests
{
    private const string TestOnlyKey = "bl521-fix3-test-only-internal-key";

    [Fact]
    public async Task The_sync_posts_the_new_and_the_previous_address_to_the_auth_sync_route()
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
            "new.address@bl521.test", "probe", "Probe", "PlatformAdmin", ["SupportAdmin"], PreviousEmail: "old.address@bl521.test"), CancellationToken.None);

        Assert.NotNull(seen);
        Assert.Equal(HttpMethod.Post, seen!.Method);
        Assert.Equal("/api/platform-auth/platform-admins/sync", seen.RequestUri!.AbsolutePath);
        // Auth's tenant resolution answers this route 400 "Missing Tenant" without one (only the Development bypass hid it)
        Assert.Equal("00000000-0000-0000-0000-000000000001", Assert.Single(seen.Headers.GetValues("X-Tenant-Id")));
        using var json = JsonDocument.Parse(body!);
        Assert.Equal("new.address@bl521.test", json.RootElement.GetProperty("email").GetString());
        Assert.Equal("old.address@bl521.test", json.RootElement.GetProperty("previousEmail").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, """{"message":"platform user not found"}""")]
    [InlineData(HttpStatusCode.Conflict, """{"message":"email already exists"}""")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "")]
    public async Task A_refusal_from_auth_is_a_failure_for_the_caller(HttpStatusCode status, string reply)
    {
        var service = Service(_ => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(reply) }));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SyncAsync(new PlatformAdministratorProvisioningSyncRequest(
            "new.address@bl521.test", "probe", "Probe", "PlatformAdmin", ["SupportAdmin"], PreviousEmail: "old.address@bl521.test"), CancellationToken.None));
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
