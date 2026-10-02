using System.Net;
using System.Text;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Infrastructure.ReferenceValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// MOD-0149 — proves the MOD-0048/PSS-012 consumer seam sends the tenant `scope_key` (tenant-scoped sets
/// return `scope_key_required` without it) and degrades in a controlled way. No hardcoded/local fallback.
/// <para>WP-BRD-TENANT-CRM-SETS — every read asks the Platform consumable-sets route FIRST (any tenant role may read it);
/// only a set the Platform does not list (404 <c>reference_set_not_tenant_accessible</c>) goes to the consumer path, and
/// that set is remembered so the second read costs one request. 404 <c>reference_set_not_published</c> is SetMissing.</para>
/// </summary>
public sealed class GatewayReferenceDataValidatorTests
{
    private static readonly Guid Tenant = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");

    private const string ConsumablePrefix = "/api/lookups/reference-data/consumable-sets/";
    private const string ConsumerPrefix = "/api/v1/reference-data/sets/";

    private const string NotAccessibleBody =
        """{"data":null,"statusCode":404,"isSuccessful":false,"errors":["reference_set_not_tenant_accessible"],"reason_code":"reference_set_not_tenant_accessible"}""";

    private const string NotPublishedBody =
        """{"data":null,"statusCode":404,"isSuccessful":false,"errors":["reference_set_not_published"],"reason_code":"reference_set_not_published"}""";

    /// <summary>Answers per request (route-aware) and records every request it saw.</summary>
    private sealed class RoutingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, (HttpStatusCode Status, string Body)> _answer;

        public RoutingHandler(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)> answer) => _answer = answer;

        public List<HttpRequestMessage> Requests { get; } = new();

        public List<string> Uris => Requests.Select(r => r.RequestUri!.PathAndQuery).ToList();

        public Uri? LastUri => Requests.LastOrDefault()?.RequestUri;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var (status, body) = _answer(request);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private static (GatewayReferenceDataValidator v, RoutingHandler h) Build(
        Guid? tenantId,
        Func<HttpRequestMessage, (HttpStatusCode, string)> answer,
        ConsumableReferenceSetRouting? routing = null,
        IDictionary<string, string?>? settings = null,
        IHttpContextAccessor? accessor = null)
    {
        var handler = new RoutingHandler(answer);
        var client = new HttpClient(handler);
        var values = new Dictionary<string, string?> { ["Gateway:BaseUrl"] = "http://localhost:5000" };
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            values[key] = value;
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var tenantContext = new TenantContext();
        if (tenantId is { } t) tenantContext.SetTenant(t);

        var validator = new GatewayReferenceDataValidator(
            client, config, accessor ?? new HttpContextAccessor(), tenantContext,
            routing ?? new ConsumableReferenceSetRouting(),
            NullLogger<GatewayReferenceDataValidator>.Instance);
        return (validator, handler);
    }

    /// <summary>The same answer on every route (the pre-WP tests: the answer does not depend on the route).</summary>
    private static (GatewayReferenceDataValidator v, RoutingHandler h) Build(
        Guid? tenantId, HttpStatusCode status = HttpStatusCode.OK, string body = "[]")
        => Build(tenantId, _ => (status, body));

    /// <summary>Consumable route refuses as "not on the list"; the consumer path answers <paramref name="body"/>.</summary>
    private static Func<HttpRequestMessage, (HttpStatusCode, string)> NotListed(string body)
        => request => request.RequestUri!.AbsolutePath.StartsWith(ConsumablePrefix, StringComparison.Ordinal)
            ? (HttpStatusCode.NotFound, NotAccessibleBody)
            : (HttpStatusCode.OK, body);

    // ---- unchanged contract (the answer is the same on both routes) -----------------------------------------------

    [Fact]
    public async Task Without_Tenant_Returns_SetMissing_And_Does_Not_Call()
    {
        var (v, h) = Build(tenantId: null);

        var result = await v.ValidateAsync("account-type", "organization", default);

        Assert.Equal(ReferenceValidationStatus.SetMissing, result.Status);
        Assert.Null(h.LastUri); // no call attempted without a scope_key
    }

    [Fact]
    public async Task Unknown_Value_Returns_InvalidValue()
    {
        var (v, _) = Build(Tenant, HttpStatusCode.OK, """[{"valueCode":"hospital"}]""");

        var result = await v.ValidateAsync("account-type", "not-a-real-type", default);

        Assert.Equal(ReferenceValidationStatus.InvalidValue, result.Status);
    }

    [Fact]
    public async Task Parses_Canonical_Data_Items_Envelope()
    {
        // MOD-0048/PSS-012 canonical shape: Response<BusinessReferenceDataPublishedValuesModel>
        const string body = """
        {"data":{"setCode":"account-type","versionNumber":1,"publishedAt":"2026-07-16T00:00:00Z",
          "items":[{"valueCode":"organization","displayName":"Organization","isActive":true,"sortOrder":10},
                   {"valueCode":"hospital","displayName":"Hospital","isActive":true,"sortOrder":20}]},
         "isSuccessful":true,"statusCode":200}
        """;
        var (v, _) = Build(Tenant, HttpStatusCode.OK, body);

        Assert.Equal(ReferenceValidationStatus.Valid, (await v.ValidateAsync("account-type", "organization", default)).Status);
        Assert.Equal(ReferenceValidationStatus.Valid, (await v.ValidateAsync("account-type", "hospital", default)).Status);
        Assert.Equal(ReferenceValidationStatus.InvalidValue, (await v.ValidateAsync("account-type", "clinic", default)).Status);
    }

    [Fact]
    public async Task Parses_The_Platform_Published_Values_Item_Shape()
    {
        // What the Platform actually serialises: items[{code, label, description, isActive, sortOrder, attributes}].
        const string body = """
        {"data":{"setCode":"account-type","versionNumber":3,"publishedAt":null,
          "items":[{"code":"hospital","label":"Hospital","description":null,"isActive":true,"sortOrder":10,"attributes":null}]},
         "statusCode":200,"isSuccessful":true,"errors":[]}
        """;
        var (v, _) = Build(Tenant, HttpStatusCode.OK, body);

        Assert.Equal(ReferenceValidationStatus.Valid, (await v.ValidateAsync("account-type", "HOSPITAL", default)).Status);
    }

    [Fact]
    public async Task Deprecated_Value_Is_Not_Selectable()
    {
        const string body = """
        {"data":{"setCode":"account-status","items":[
            {"valueCode":"active","isActive":true},
            {"valueCode":"archived","isActive":false},
            {"valueCode":"legacy","isDeprecated":true}]}}
        """;
        var (v, _) = Build(Tenant, HttpStatusCode.OK, body);

        Assert.Equal(ReferenceValidationStatus.Valid, (await v.ValidateAsync("account-status", "active", default)).Status);
        Assert.Equal(ReferenceValidationStatus.InvalidValue, (await v.ValidateAsync("account-status", "archived", default)).Status);
        Assert.Equal(ReferenceValidationStatus.InvalidValue, (await v.ValidateAsync("account-status", "legacy", default)).Status);
    }

    [Fact]
    public async Task Unpublished_Set_Returns_SetMissing()
    {
        var (v, _) = Build(Tenant, HttpStatusCode.NotFound, """{"detail":"no_published_version"}""");

        var result = await v.ValidateAsync("account-status", "active", default);

        Assert.Equal(ReferenceValidationStatus.SetMissing, result.Status);
    }

    // ---- WP-BRD-TENANT-CRM-SETS: consumable-sets first --------------------------------------------------------------

    [Fact]
    public async Task Consumable_Route_Is_Asked_First_Without_A_Scope_Key_And_Its_Answer_Is_Used()
    {
        var (v, h) = Build(Tenant, request => request.RequestUri!.AbsolutePath.StartsWith(ConsumablePrefix, StringComparison.Ordinal)
            ? (HttpStatusCode.OK, """{"data":{"items":[{"code":"organization","isActive":true}]}}""")
            : (HttpStatusCode.Forbidden, """{"errors":["Permission denied."]}"""));

        var result = await v.ValidateAsync("account-type", "organization", default);

        Assert.Equal(ReferenceValidationStatus.Valid, result.Status);
        var uri = Assert.Single(h.Uris);
        Assert.Equal("/api/lookups/reference-data/consumable-sets/account-type/published-values", uri);
        Assert.DoesNotContain("scope_key", uri);
    }

    [Fact]
    public async Task Consumable_Route_Forwards_The_Callers_Token_And_Tenant()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = "Bearer caller-token";
        httpContext.Request.Headers["X-Tenant-Id"] = Tenant.ToString();
        var (v, h) = Build(Tenant, _ => (HttpStatusCode.OK, """[{"code":"organization"}]"""),
            accessor: new HttpContextAccessor { HttpContext = httpContext });

        await v.ValidateAsync("account-type", "organization", default);

        var request = Assert.Single(h.Requests);
        Assert.Equal("Bearer caller-token", request.Headers.GetValues("Authorization").Single());
        Assert.Equal(Tenant.ToString(), request.Headers.GetValues("X-Tenant-Id").Single());
    }

    [Fact]
    public async Task Not_Published_On_The_Consumable_Route_Is_SetMissing_Without_A_Fallback()
    {
        var (v, h) = Build(Tenant, _ => (HttpStatusCode.NotFound, NotPublishedBody));

        var result = await v.ValidateAsync("contact-status", "active", default);

        Assert.Equal(ReferenceValidationStatus.SetMissing, result.Status);
        Assert.Single(h.Requests);
    }

    [Fact]
    public async Task A_Set_The_Platform_Does_Not_List_Falls_Back_To_The_Consumer_Path_With_The_Scope_Key()
    {
        var (v, h) = Build(Tenant, NotListed("""[{"valueCode":"organization"}]"""));

        var result = await v.ValidateAsync("account-type", "organization", default);

        Assert.Equal(ReferenceValidationStatus.Valid, result.Status);
        Assert.Equal(2, h.Requests.Count);
        Assert.StartsWith(ConsumablePrefix, h.Uris[0]);
        Assert.Contains("/api/v1/reference-data/sets/account-type/published-values", h.Uris[1]);
        Assert.Contains($"scope_key={Tenant}", h.Uris[1]);
    }

    [Fact]
    public async Task A_Set_The_Platform_Does_Not_List_Is_Remembered_So_The_Second_Read_Is_One_Request()
    {
        var routing = new ConsumableReferenceSetRouting();
        var (v, h) = Build(Tenant, NotListed("""[{"valueCode":"organization"}]"""), routing);

        await v.ValidateAsync("unlisted-set", "organization", default);
        var afterFirst = h.Requests.Count;
        await v.ValidateAsync("UNLISTED-SET", "organization", default);

        Assert.Equal(2, afterFirst);
        Assert.Equal(3, h.Requests.Count);
        Assert.StartsWith(ConsumerPrefix, h.Uris[2]);
        Assert.True(routing.IsKnownNotConsumable("unlisted-set"));
    }

    [Fact]
    public async Task The_Memory_Is_Shared_By_Every_Validator_Of_The_Process()
    {
        var routing = new ConsumableReferenceSetRouting();
        var (first, _) = Build(Tenant, NotListed("[]"), routing);
        var (second, h) = Build(Tenant, NotListed("""[{"valueCode":"x"}]"""), routing);

        await first.ValidateAsync("unlisted-set", "x", default);
        var result = await second.ValidateAsync("unlisted-set", "x", default);

        Assert.Equal(ReferenceValidationStatus.Valid, result.Status);
        var uri = Assert.Single(h.Uris);
        Assert.StartsWith(ConsumerPrefix, uri);
    }

    [Fact]
    public async Task An_Unrecognised_Consumable_Refusal_Falls_Back_For_That_Read_Only()
    {
        // A Platform without the route answers the gateway's empty 404 — the consumer path still serves the read, but the
        // set is NOT remembered: the next read asks the consumable route again.
        var routing = new ConsumableReferenceSetRouting();
        var (v, h) = Build(Tenant, request => request.RequestUri!.AbsolutePath.StartsWith(ConsumablePrefix, StringComparison.Ordinal)
            ? (HttpStatusCode.NotFound, "{}")
            : (HttpStatusCode.OK, """[{"valueCode":"organization"}]"""), routing);

        Assert.Equal(ReferenceValidationStatus.Valid, (await v.ValidateAsync("account-type", "organization", default)).Status);
        await v.ValidateAsync("account-type", "organization", default);

        Assert.Equal(4, h.Requests.Count);
        Assert.StartsWith(ConsumablePrefix, h.Uris[2]);
        Assert.False(routing.IsKnownNotConsumable("account-type"));
    }

    [Fact]
    public async Task The_Fallback_Keeps_The_Global_Scope_Retry()
    {
        var (v, h) = Build(Tenant, request =>
        {
            var path = request.RequestUri!.PathAndQuery;
            if (path.StartsWith(ConsumablePrefix, StringComparison.Ordinal)) return (HttpStatusCode.NotFound, NotAccessibleBody);
            return path.Contains("scope_key=", StringComparison.Ordinal)
                ? (HttpStatusCode.BadRequest, """{"errors":["scope_key_not_allowed_for_global"]}""")
                : (HttpStatusCode.OK, """[{"valueCode":"TR"}]""");
        });

        var result = await v.ValidateAsync("SOME_GLOBAL", "tr", default);

        Assert.Equal(ReferenceValidationStatus.Valid, result.Status);
        Assert.Equal(3, h.Requests.Count);
        Assert.DoesNotContain("scope_key", h.Uris[2]);
    }

    [Fact]
    public async Task The_Consumable_Path_Template_Is_Configurable()
    {
        var (v, h) = Build(Tenant, _ => (HttpStatusCode.OK, """[{"code":"x"}]"""),
            settings: new Dictionary<string, string?>
            {
                ["ReferenceData:ConsumableSetsPathTemplate"] = "/custom/{setCode}/values"
            });

        await v.ValidateAsync("account-type", "x", default);

        Assert.Equal("/custom/account-type/values", Assert.Single(h.Uris));
    }

    [Fact]
    public async Task Value_Attributes_Come_From_The_Consumable_Route()
    {
        var (v, h) = Build(Tenant, request => request.RequestUri!.AbsolutePath.StartsWith(ConsumablePrefix, StringComparison.Ordinal)
            ? (HttpStatusCode.OK, """{"data":{"items":[{"code":"UZ","attributes":{"Languages":"uz,ru"}}]}}""")
            : (HttpStatusCode.Forbidden, "{}"));

        var attributes = await v.GetValueAttributesAsync("country-content-languages", "uz", default);

        Assert.NotNull(attributes);
        Assert.Equal("uz,ru", attributes!["Languages"]);
        Assert.Single(h.Requests);
    }

    [Fact]
    public async Task Value_Attributes_Of_An_Unpublished_Set_Are_None_And_Of_An_Unlisted_Set_Come_From_The_Fallback()
    {
        var (unpublished, _) = Build(Tenant, _ => (HttpStatusCode.NotFound, NotPublishedBody));
        var (unlisted, h) = Build(Tenant, NotListed("""[{"valueCode":"parent","attributes":{"direction":"down"}}]"""));

        Assert.Null(await unpublished.GetValueAttributesAsync("account-relationship-type", "parent", default));
        var attributes = await unlisted.GetValueAttributesAsync("account-relationship-type", "parent", default);

        Assert.Equal("down", attributes!["direction"]);
        Assert.Equal(2, h.Requests.Count);
    }

    [Fact]
    public async Task Catalog_Reads_The_Consumable_Route_And_Reports_Not_Published_Without_A_Fallback()
    {
        var (published, h1) = Build(Tenant, _ => (HttpStatusCode.OK,
            """{"data":{"items":[{"code":"tr","label":"Türkiye","isActive":true},{"code":"az","label":"Azerbaijan","isActive":true}]}}"""));
        var (unpublished, h2) = Build(Tenant, _ => (HttpStatusCode.NotFound, NotPublishedBody));

        var snapshot = await published.GetPublishedValuesAsync("COUNTRY_CODES", default);
        var missing = await unpublished.GetPublishedValuesAsync("business-unit", default);

        Assert.True(snapshot.IsPublished);
        Assert.Equal(new[] { "tr", "az" }, snapshot.Values.Select(x => x.ValueCode));
        Assert.Equal("Türkiye", snapshot.Values[0].DisplayName);
        Assert.StartsWith(ConsumablePrefix, Assert.Single(h1.Uris));
        Assert.False(missing.IsPublished);
        Assert.Single(h2.Requests);
    }

    [Fact]
    public async Task Catalog_Of_An_Unlisted_Set_Falls_Back_To_The_Consumer_Path()
    {
        var (v, h) = Build(Tenant, NotListed("""[{"valueCode":"x"}]"""));

        var snapshot = await v.GetPublishedValuesAsync("unlisted-set", default);

        Assert.True(snapshot.IsPublished);
        Assert.Equal(2, h.Requests.Count);
        Assert.Contains($"scope_key={Tenant}", h.Uris[1]);
    }
}
