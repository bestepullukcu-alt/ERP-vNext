using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web;
using Diten.Web.Controllers.CRM;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-CL-FE-1 — the claims v2 same-origin proxy layer on the REAL <see cref="ClaimsController"/>: permission gates (read
/// for GET, manage for writes; no gateway call when refused), CRM's <c>[code, message]</c> refusals passed through with
/// their status, bodiless 204 kept bodiless, the removed approve door, and the composed lookups (country axis, workflow
/// template preview).
/// </summary>
public sealed class ClaimsV2ProxyTests
{
    private const string Gateway = "http://gateway.test";
    private const string Read = "crm.claim.read";
    private const string Manage = "crm.claim.manage";
    private static readonly Guid TenantId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid ClaimId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    // ============================================================ permission gates

    [Fact]
    public async Task Reads_need_crm_claim_read_and_never_reach_the_gateway_without_it()
    {
        var gateway = new RoutingGateway();
        var controller = ControllerWith(gateway);

        foreach (var call in new Func<Task<IActionResult>>[]
                 {
                     () => controller.V2List(default), () => controller.V2Get(ClaimId, default),
                     () => controller.V2Coverage(default), () => controller.V2Evidence(ClaimId, default),
                     () => controller.V2Usage(default), () => controller.V2LookupCountries(default),
                     () => controller.V2LookupWorkflowTemplate("CLAIM-CORE-MLR", default)
                 })
        {
            Assert.Equal(403, Status(await call()));
        }

        Assert.Empty(gateway.Requests);
    }

    [Fact]
    public async Task Writes_need_crm_claim_manage_a_reader_is_refused()
    {
        var gateway = new RoutingGateway();
        var controller = ControllerWith(gateway, Read);
        var body = JsonDocument.Parse("{}").RootElement;

        foreach (var call in new Func<Task<IActionResult>>[]
                 {
                     () => controller.V2Create(body, default), () => controller.V2Update(ClaimId, body, default),
                     () => controller.V2Archive(ClaimId, default), () => controller.V2NewVersion(ClaimId, default),
                     () => controller.V2SubmitReview(ClaimId, default), () => controller.V2WithdrawReview(ClaimId, default),
                     () => controller.V2LinkEvidence(ClaimId, body, default),
                     () => controller.V2RemoveEvidence(Guid.NewGuid(), body, default),
                     () => controller.V2CloseCountry(ClaimId, body, default),
                     () => controller.V2CreateCountryVersion(ClaimId, body, default)
                 })
        {
            Assert.Equal(403, Status(await call()));
        }

        Assert.Empty(gateway.Requests);
    }

    [Fact]
    public async Task A_reader_reaches_the_crm_route_with_the_query_the_tenant_and_the_same_path()
    {
        var gateway = new RoutingGateway();
        var controller = ControllerWith(gateway, Read);
        controller.ControllerContext.HttpContext.Request.QueryString = new QueryString("?includeCounts=true");

        Assert.Equal(200, Status(await controller.V2List(default)));

        var request = Assert.Single(gateway.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal($"{Gateway}/api/crm/content-composition/claims?includeCounts=true", request.Uri);
        Assert.Equal(TenantId.ToString(), request.Tenant);
    }

    // ============================================================ pass-through

    [Fact]
    public async Task A_crm_409_refusal_is_passed_through_with_its_code_and_message()
    {
        const string refusal = """{"isSuccessful":false,"statusCode":409,"errors":["evidence_required","At least one active evidence link is required before review."]}""";
        var gateway = new RoutingGateway((_, _) => (HttpStatusCode.Conflict, refusal));
        var controller = ControllerWith(gateway, Read, Manage);

        var result = Assert.IsType<ContentResult>(await controller.V2SubmitReview(ClaimId, default));

        Assert.Equal(409, result.StatusCode);
        Assert.Equal(refusal, result.Content);
        Assert.Equal($"{Gateway}/api/crm/content-composition/claims/{ClaimId}/submit-review", gateway.Requests.Single().Uri);
        Assert.Equal(HttpMethod.Post, gateway.Requests.Single().Method);
    }

    [Fact]
    public async Task A_204_stays_bodiless()
    {
        var gateway = new RoutingGateway((_, _) => (HttpStatusCode.NoContent, ""));
        var controller = ControllerWith(gateway, Read, Manage);

        var result = await controller.V2Archive(ClaimId, default);

        Assert.Equal(204, Assert.IsType<StatusCodeResult>(result).StatusCode);
    }

    [Fact]
    public async Task A_client_supplied_tenant_id_is_refused()
    {
        var gateway = new RoutingGateway();
        var controller = ControllerWith(gateway, Read, Manage);

        var result = await controller.V2Create(JsonDocument.Parse("""{"tenantId":"x","claimCode":"C"}""").RootElement, default);

        Assert.Equal(400, Status(result));
        Assert.Empty(gateway.Requests);
    }

    [Fact]
    public void The_direct_approve_door_is_gone()
    {
        var templates = typeof(ClaimsController).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>())
            .Select(a => a.Template ?? string.Empty)
            .ToList();

        Assert.DoesNotContain(templates, t => t.Contains("approve", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(nameof(ClaimsController.V2List), "GET", "api/v2/claims")]
    [InlineData(nameof(ClaimsController.V2Coverage), "GET", "api/v2/claims/coverage")]
    [InlineData(nameof(ClaimsController.V2CloseCountry), "POST", "api/v2/claims/{claimId:guid}/country-closures")]
    [InlineData(nameof(ClaimsController.V2ReopenCountry), "POST", "api/v2/claims/{claimId:guid}/country-closures/{countryCode}/reopen")]
    [InlineData(nameof(ClaimsController.V2UpdateCountryVersion), "PUT", "api/v2/claims/country-versions/{versionId:guid}")]
    [InlineData(nameof(ClaimsController.V2SubmitCountryVersionReview), "POST", "api/v2/claims/country-versions/{versionId:guid}/submit-review")]
    [InlineData(nameof(ClaimsController.V2RemoveEvidence), "POST", "api/v2/claims/evidence/{linkId:guid}/remove")]
    [InlineData(nameof(ClaimsController.V2EvidenceDocumentOptions), "GET", "api/v2/claims/evidence/document-options")]
    [InlineData(nameof(ClaimsController.V2Usage), "GET", "api/v2/claims/usage")]
    [InlineData(nameof(ClaimsController.V2LookupWorkflowHistory), "GET", "api/v2/lookups/workflow-history/{instanceId:guid}")]
    public void V2_routes_mirror_the_crm_verbs(string action, string verb, string template)
    {
        var http = typeof(ClaimsController).GetMethod(action)!.GetCustomAttributes<HttpMethodAttribute>().Single();
        Assert.Contains(verb, http.HttpMethods);
        Assert.Equal(template, http.Template);
    }

    // ============================================================ lookups

    [Fact]
    public async Task Countries_join_country_codes_with_their_content_languages_in_brd_order()
    {
        var gateway = new RoutingGateway((method, uri) => uri switch
        {
            _ when uri.Contains("/COUNTRY_CODES/published-values") => (HttpStatusCode.OK,
                """{"data":{"items":[{"valueCode":"UZ","displayName":"Uzbekistan","sortOrder":30},{"valueCode":"TR","displayName":"Turkey","sortOrder":10},{"valueCode":"XX","displayName":"Old","isActive":false}]}}"""),
            _ when uri.Contains("/country-content-languages/published-values") => (HttpStatusCode.OK,
                """{"data":{"items":[{"valueCode":"TR","attributes":{"Languages":"tr"}},{"valueCode":"UZ","attributes":{"Languages":"uz, ru"}}]}}"""),
            _ => (HttpStatusCode.NotFound, "")
        });
        var controller = ControllerWith(gateway, Read);

        var data = Data(await controller.V2LookupCountries(default));

        Assert.Equal(new[] { "TR", "UZ" }, data.EnumerateArray().Select(c => c.GetProperty("code").GetString()));
        Assert.Equal(new[] { "uz", "ru" }, data[1].GetProperty("languages").EnumerateArray().Select(l => l.GetString()));
        // Global sets are read WITHOUT scope_key (the platform refuses it for a global set).
        Assert.All(gateway.Requests, r => Assert.DoesNotContain("scope_key", r.Uri));
    }

    [Fact]
    public async Task Tenant_scoped_reference_sets_are_read_with_the_jwt_tenant()
    {
        // WP-BRD-TENANT-CRM-SETS step 2 — the consumable-sets route is asked first: no scope_key, the JWT tenant travels
        // in X-Tenant-Id and the Platform scopes the read from the validated token.
        var gateway = new RoutingGateway((_, _) => (HttpStatusCode.OK,
            """{"data":{"items":[{"valueCode":"not-licensed","displayName":"Not licensed"}]}}"""));
        var controller = ControllerWith(gateway, Read);

        var data = Data(await controller.V2LookupClosureReasons(default));

        Assert.Equal("not-licensed", data[0].GetProperty("code").GetString());
        var request = gateway.Requests.Single();
        Assert.Equal($"{Gateway}/api/lookups/reference-data/consumable-sets/claim-country-closure-reason/published-values", request.Uri);
        Assert.Equal(TenantId.ToString(), request.Tenant);
    }

    [Fact]
    public async Task A_claim_set_outside_the_consumable_list_is_read_on_the_old_path_with_the_jwt_tenant()
    {
        var gateway = new RoutingGateway((_, uri) => uri.Contains("/consumable-sets/")
            ? (HttpStatusCode.NotFound, """{"errors":["reference_set_not_tenant_accessible"]}""")
            : (HttpStatusCode.OK, """{"data":{"items":[{"valueCode":"adapted","displayName":"Adapted"}]}}"""));
        var controller = ControllerWith(gateway, Read);

        var data = Data(await controller.V2LookupAdaptationTypes(default));

        Assert.Equal("adapted", data[0].GetProperty("code").GetString());
        Assert.EndsWith($"/claim-adaptation-type/published-values?scope_key={TenantId}", gateway.Requests.Last().Uri);
    }

    [Fact]
    public async Task Country_codes_missing_is_a_refusal_never_a_local_list()
    {
        var controller = ControllerWith(new RoutingGateway((_, _) => (HttpStatusCode.NotFound, "")), Read);
        Assert.Equal(503, Status(await controller.V2LookupCountries(default)));
    }

    [Fact]
    public async Task Workflow_template_preview_names_the_steps_and_the_candidate_positions()
    {
        var definitionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var position = Guid.NewGuid();
        var plan = JsonSerializer.Serialize(new
        {
            stages = new[]
            {
                new
                {
                    code = "mlr", name = "MLR İnceleme",
                    steps = new[] { new { code = "medical", name = "Medikal inceleme", assignment = new { candidatePrincipalIds = new[] { $"position:{position}", "position:unknown" } } } }
                }
            }
        });
        var gateway = new RoutingGateway((_, uri) => uri switch
        {
            _ when uri.EndsWith("/api/v1/workflow/definitions") => (HttpStatusCode.OK,
                $$"""{"data":[{"id":"{{Guid.NewGuid()}}","templateCode":"OTHER"},{"id":"{{definitionId}}","templateCode":"CLAIM-CORE-MLR"}]}"""),
            _ when uri.EndsWith($"/definitions/{definitionId}") => (HttpStatusCode.OK,
                JsonSerializer.Serialize(new { data = new { id = definitionId, activePublishedVersionId = versionId } })),
            _ when uri.EndsWith($"/versions/{versionId}") => (HttpStatusCode.OK,
                JsonSerializer.Serialize(new { data = new { definitionJson = plan } })),
            _ when uri.EndsWith("/lookups/positions") => (HttpStatusCode.OK,
                $$"""{"data":[{"id":"{{position}}","name":"Medikal Müdür"}]}"""),
            _ => (HttpStatusCode.NotFound, "")
        });
        var controller = ControllerWith(gateway, Read);

        var data = Data(await controller.V2LookupWorkflowTemplate("claim-core-mlr", default));

        Assert.True(data.GetProperty("available").GetBoolean());
        var step = data.GetProperty("stages")[0].GetProperty("steps")[0];
        Assert.Equal("Medikal inceleme", step.GetProperty("name").GetString());
        Assert.Equal(new[] { "Medikal Müdür", "unknown" }, step.GetProperty("candidates").EnumerateArray().Select(c => c.GetString()));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "WorkflowPermissionMissing")]
    [InlineData(HttpStatusCode.OK, "WorkflowTemplateMissing")]
    public async Task Workflow_template_preview_says_why_it_is_unavailable(HttpStatusCode listStatus, string reason)
    {
        var controller = ControllerWith(new RoutingGateway((_, _) => (listStatus, """{"data":[]}""")), Read);

        var data = Data(await controller.V2LookupWorkflowTemplate("CLAIM-CORE-MLR", default));

        Assert.False(data.GetProperty("available").GetBoolean());
        Assert.Equal(reason, data.GetProperty("reason").GetString());
    }

    // ============================================================ helpers

    private static int Status(IActionResult result) => result switch
    {
        StatusCodeResult s => s.StatusCode,
        ObjectResult o => o.StatusCode ?? 200,
        ContentResult c => c.StatusCode ?? 200,
        JsonResult j => j.StatusCode ?? 200,
        _ => throw new InvalidOperationException(result.GetType().Name)
    };

    private static JsonElement Data(IActionResult result)
    {
        var value = Assert.IsAssignableFrom<ObjectResult>(result).Value;
        return JsonDocument.Parse(JsonSerializer.Serialize(value)).RootElement.GetProperty("data");
    }

    private static ClaimsController ControllerWith(RoutingGateway gateway, params string[] permissions)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = Gateway }).Build();
        var controller = new ClaimsController(new HttpClient(gateway), configuration, new KeyLocalizer(),
            NullLogger<ClaimsController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
        };
        return controller;
    }

    private sealed class RoutingGateway(Func<HttpMethod, string, (HttpStatusCode Status, string Body)>? route = null)
        : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Uri, string? Tenant)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!.ToString();
            Requests.Add((request.Method, uri,
                request.Headers.TryGetValues("X-Tenant-Id", out var t) ? t.Single() : null));
            var (status, body) = route?.Invoke(request.Method, uri) ?? (HttpStatusCode.OK, """{"data":{"items":[]}}""");
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
