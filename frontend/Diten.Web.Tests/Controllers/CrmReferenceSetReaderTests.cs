using System.Net;
using System.Text;
using Diten.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-BRD-TENANT-CRM-SETS step 2 — the shared CRM Web reference set reader (<see cref="CrmReferenceSetReader"/>), measured
/// on the production class against a stub Gateway: consumable-sets route first (caller's token + tenant, no scope_key);
/// a set the Platform does not list falls back to the old consumer path and is remembered; an unrecognised refusal (the old
/// Platform's <c>{}</c> 404) falls back for that read only; <c>reference_set_not_published</c> is final.
/// </summary>
public sealed class CrmReferenceSetReaderTests
{
    private const string Gateway = "http://gateway.test";
    private const string Tenant = "97c50000-0000-0000-0000-000000000001";
    private const string Token = "tenant-user-token";

    private const string Values = """{"isSuccessful":true,"data":{"setCode":"account-type","versionNumber":3,"items":[{"code":"hospital","label":"Hospital","isActive":true,"sortOrder":10}]}}""";
    private const string NotListed = """{"isSuccessful":false,"statusCode":404,"errors":["reference_set_not_tenant_accessible","The reference set is not available on this route."]}""";
    private const string NotPublished = """{"isSuccessful":false,"statusCode":404,"errors":["reference_set_not_published","No published version."]}""";
    private const string GlobalRefusal = """{"isSuccessful":false,"statusCode":400,"errors":["scope_key_not_allowed_for_global"]}""";

    private static string Consumable(string set) => $"{Gateway}/api/lookups/reference-data/consumable-sets/{set}/published-values";
    private static string Consumer(string set) => $"{Gateway}/api/v1/reference-data/sets/{set}/published-values";

    [Fact]
    public async Task The_consumable_route_is_asked_first_with_the_callers_token_and_tenant_and_no_scope_key()
    {
        var gateway = new StubGateway(_ => (HttpStatusCode.OK, Values));
        var reader = Reader(gateway, new CrmReferenceSetRouting());

        using var response = await reader.ReadAsync("account-type", Token, Tenant);

        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.OK, response!.StatusCode);
        Assert.Equal(Values, await response.Content.ReadAsStringAsync());
        var request = Assert.Single(gateway.Requests);
        Assert.Equal(Consumable("account-type"), request.Uri);
        Assert.DoesNotContain("scope_key", request.Uri);
        Assert.Equal($"Bearer {Token}", request.Authorization);
        Assert.Equal(Tenant, request.Tenant);
    }

    [Fact]
    public async Task A_set_the_platform_does_not_list_falls_back_to_the_consumer_path_and_is_remembered()
    {
        var gateway = new StubGateway(uri => uri.Contains("/consumable-sets/")
            ? (HttpStatusCode.NotFound, NotListed)
            : (HttpStatusCode.OK, Values));
        var routing = new CrmReferenceSetRouting();
        var reader = Reader(gateway, routing);

        using (var first = await reader.ReadAsync("unlisted-set", Token, Tenant))
        {
            Assert.Equal(HttpStatusCode.OK, first!.StatusCode);
            Assert.Equal(Values, await first.Content.ReadAsStringAsync());
        }

        Assert.Equal(
            new[] { Consumable("unlisted-set"), $"{Consumer("unlisted-set")}?scope_key={Tenant}" },
            gateway.Requests.Select(r => r.Uri));
        Assert.All(gateway.Requests, r => Assert.Equal(Tenant, r.Tenant));
        Assert.True(routing.IsKnownNotConsumable("UNLISTED-SET "));

        gateway.Requests.Clear();
        using (var second = await reader.ReadAsync("unlisted-set", Token, Tenant))
        {
            Assert.Equal(HttpStatusCode.OK, second!.StatusCode);
        }

        // Remembered: the second read goes straight to the consumer path — one request, not two.
        Assert.Equal($"{Consumer("unlisted-set")}?scope_key={Tenant}", Assert.Single(gateway.Requests).Uri);
    }

    [Fact]
    public async Task A_global_set_on_the_consumer_path_is_retried_without_the_scope_key_only_on_the_services_signal()
    {
        var gateway = new StubGateway(uri =>
            uri.Contains("/consumable-sets/") ? (HttpStatusCode.NotFound, NotListed)
            : uri.Contains("scope_key=") ? (HttpStatusCode.BadRequest, GlobalRefusal)
            : (HttpStatusCode.OK, Values));
        var reader = Reader(gateway, new CrmReferenceSetRouting());

        using var response = await reader.ReadAsync("GLOBAL_SET", Token, Tenant);

        Assert.Equal(HttpStatusCode.OK, response!.StatusCode);
        Assert.Equal(Consumer("GLOBAL_SET"), gateway.Requests.Last().Uri);
        Assert.Equal(3, gateway.Requests.Count);
    }

    [Fact]
    public async Task A_tenant_set_refused_on_the_consumer_path_is_not_retried_without_the_key()
    {
        // The administrator-only consumer path refuses a non-admin (403): that answer stands — no keyless retry.
        var gateway = new StubGateway(uri => uri.Contains("/consumable-sets/")
            ? (HttpStatusCode.NotFound, NotListed)
            : (HttpStatusCode.Forbidden, """{"errors":["forbidden"]}"""));
        var reader = Reader(gateway, new CrmReferenceSetRouting());

        using var response = await reader.ReadAsync("unlisted-set", Token, Tenant);

        Assert.Equal(HttpStatusCode.Forbidden, response!.StatusCode);
        Assert.Equal(2, gateway.Requests.Count);
    }

    [Fact]
    public async Task An_unrecognised_refusal_falls_back_for_that_read_only_and_is_not_remembered()
    {
        // A Platform deployed without the consumable route: the gateway's catch-all answers 404 with an empty object.
        var gateway = new StubGateway(uri => uri.Contains("/consumable-sets/")
            ? (HttpStatusCode.NotFound, "{}")
            : (HttpStatusCode.OK, Values));
        var routing = new CrmReferenceSetRouting();
        var reader = Reader(gateway, routing);

        using (var first = await reader.ReadAsync("account-type", Token, Tenant))
        {
            Assert.Equal(HttpStatusCode.OK, first!.StatusCode);
        }

        Assert.False(routing.IsKnownNotConsumable("account-type"));

        gateway.Requests.Clear();
        using (await reader.ReadAsync("account-type", Token, Tenant))
        {
        }

        // Not remembered: the next read asks the consumable route again.
        Assert.Equal(Consumable("account-type"), gateway.Requests.First().Uri);
        Assert.Equal(2, gateway.Requests.Count);
    }

    [Fact]
    public async Task Not_published_is_the_final_answer_never_a_fallback()
    {
        var gateway = new StubGateway(_ => (HttpStatusCode.NotFound, NotPublished));
        var routing = new CrmReferenceSetRouting();
        var reader = Reader(gateway, routing);

        using var response = await reader.ReadAsync("account-category", Token, Tenant);

        Assert.Equal(HttpStatusCode.NotFound, response!.StatusCode);
        Assert.Contains("reference_set_not_published", await response.Content.ReadAsStringAsync());
        Assert.Equal(Consumable("account-category"), Assert.Single(gateway.Requests).Uri);
        Assert.False(routing.IsKnownNotConsumable("account-category"));
    }

    [Fact]
    public async Task No_tenant_means_no_request()
    {
        var gateway = new StubGateway(_ => (HttpStatusCode.OK, Values));
        var reader = Reader(gateway, new CrmReferenceSetRouting());

        Assert.Null(await reader.ReadAsync("account-type", Token, null));
        Assert.Null(await reader.ReadAsync("account-type", Token, "  "));
        Assert.Empty(gateway.Requests);
    }

    [Fact]
    public async Task An_unreachable_gateway_is_null_like_the_controllers_own_helpers()
    {
        var reader = new CrmReferenceSetReader(
            new HttpClient(new UnreachableGateway()), Gateway, NullLogger.Instance, new CrmReferenceSetRouting());

        Assert.Null(await reader.ReadAsync("account-type", Token, Tenant));
    }

    [Fact]
    public async Task The_set_code_is_escaped_into_the_path()
    {
        var gateway = new StubGateway(_ => (HttpStatusCode.OK, Values));
        var reader = Reader(gateway, new CrmReferenceSetRouting());

        using var _ = await reader.ReadAsync("a/b c", Token, Tenant);

        Assert.Equal(Consumable("a%2Fb%20c"), Assert.Single(gateway.Requests).Uri);
    }

    private static CrmReferenceSetReader Reader(StubGateway gateway, CrmReferenceSetRouting routing) =>
        new(new HttpClient(gateway), Gateway, NullLogger.Instance, routing);

    internal sealed class StubGateway(Func<string, (HttpStatusCode Status, string Body)> route) : HttpMessageHandler
    {
        public List<(string Uri, string? Authorization, string? Tenant)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!.AbsoluteUri;
            Requests.Add((uri, request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("X-Tenant-Id", out var t) ? t.Single() : null));
            var (status, body) = route(uri);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class UnreachableGateway : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("connection refused");
    }
}
