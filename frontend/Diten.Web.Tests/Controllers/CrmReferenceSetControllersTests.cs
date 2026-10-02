using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.Web;
using Diten.Web.Controllers.CRM;
using Diten.Web.Views.CRM.TerritoryManagement;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using AccountIndex = Diten.Web.Views.CRM.Accounts.AccountIndex;
using ContactIndex = Diten.Web.Views.CRM.Contacts.ContactIndex;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-BRD-TENANT-CRM-SETS step 2 — the nine CRM Web controllers read their dropdown sets through the shared
/// <c>CrmReferenceSetReader</c>. (1) Behaviour on the REAL controllers: a tenant user WITHOUT the Platform consumer
/// permission (the stub Gateway refuses the old consumer path with 403, as the Platform does) still gets the options,
/// because the consumable-sets route is asked first. (2) A source scan: each of the nine files uses the reader and none of
/// them builds the old consumer URL any more.
/// </summary>
public sealed class CrmReferenceSetControllersTests
{
    private const string Gateway = "http://gateway.test";
    private const string Tenant = "97c50000-0000-0000-0000-000000000001";

    /// <summary>The nine CRM Web controller files that read reference sets (WP §Adım 2).</summary>
    public static readonly TheoryData<string> ReaderFiles = new()
    {
        "AccountsController.cs",
        "ContactsController.cs",
        "ClaimsController.V2.cs",
        "KnowledgeController.cs",
        "KnowledgeController.Claims.cs",
        "KnowledgeConceptsController.cs",
        "SegmentsController.cs",
        "TerritoryManagementController.cs",
        "VisitFrequencyPoliciesController.cs"
    };

    // ============================================================ behaviour: a non-admin gets the options

    [Fact]
    public async Task Accounts_lookups_are_filled_for_a_user_without_the_platform_permission()
    {
        var gateway = NonAdminGateway();
        var controller = WithUser(new AccountsController(new HttpClient(gateway), Configuration(), new KeyLocalizer<SharedResource>(),
            new KeyLocalizer<AccountIndex>(), NullLogger<AccountsController>.Instance), "crm.account.read");

        var json = JsonOf(await controller.Lookups());

        Assert.Equal("v1", json.GetProperty("accountTypes")[0].GetProperty("value").GetString());
        Assert.Equal("v1", json.GetProperty("statuses")[0].GetProperty("value").GetString());
        AssertConsumableOnly(gateway, "account-type", "account-status");
    }

    [Fact]
    public async Task Contacts_lookups_are_filled_for_a_user_without_the_platform_permission()
    {
        var gateway = NonAdminGateway();
        var controller = WithUser(new ContactsController(new HttpClient(gateway), Configuration(), new KeyLocalizer<SharedResource>(),
            new KeyLocalizer<ContactIndex>(), NullLogger<ContactsController>.Instance), "crm.contact.read");

        var json = JsonOf(await controller.Lookups());

        Assert.Equal("Value one", json.GetProperty("contactTypes")[0].GetProperty("text").GetString());
        AssertConsumableOnly(gateway, "contact-type", "contact-status");
    }

    [Fact]
    public async Task Territory_model_scope_lookups_are_filled_for_a_user_without_the_platform_permission()
    {
        var gateway = NonAdminGateway();
        var controller = WithUser(new TerritoryManagementController(new HttpClient(gateway), Configuration(),
            new KeyLocalizer<SharedResource>(), new KeyLocalizer<TerritoryManagementResources>(),
            NullLogger<TerritoryManagementController>.Instance), "crm.territory.model.manage");

        var json = JsonOf(await controller.ModelLookups());

        Assert.True(json.GetProperty("countryReady").GetBoolean());
        Assert.True(json.GetProperty("businessUnitReady").GetBoolean());
        AssertConsumableOnly(gateway, "COUNTRY_CODES", "business-unit");
    }

    [Fact]
    public async Task Claims_v2_reference_lookups_are_filled_for_a_user_without_the_platform_permission()
    {
        var gateway = NonAdminGateway();
        var controller = WithUser(new ClaimsController(new HttpClient(gateway), Configuration(), new KeyLocalizer<SharedResource>(),
            NullLogger<ClaimsController>.Instance), "crm.claim.read");

        var result = Assert.IsAssignableFrom<ObjectResult>(await controller.V2LookupEvidenceTypes(default));

        Assert.Equal("v1", JsonOf(result).GetProperty("data")[0].GetProperty("code").GetString());
        AssertConsumableOnly(gateway, "evidence-type");
    }

    [Fact]
    public async Task Knowledge_reference_axes_pass_the_consumable_answer_through()
    {
        var gateway = NonAdminGateway();
        var controller = WithUser(new KnowledgeController(new HttpClient(gateway), Configuration(), new KeyLocalizer<SharedResource>(),
            NullLogger<KnowledgeController>.Instance), "crm.knowledge.subject.read");

        var result = Assert.IsType<ContentResult>(await controller.ReferenceValues("medical-specialty", default));

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(NonAdminValues, result.Content);
        AssertConsumableOnly(gateway, "medical-specialty");
    }

    [Fact]
    public async Task Knowledge_concepts_moderator_values_pass_the_consumable_answer_through()
    {
        var gateway = NonAdminGateway();
        var controller = WithUser(new KnowledgeConceptsController(new HttpClient(gateway), Configuration(),
            new KeyLocalizer<SharedResource>(), NullLogger<KnowledgeConceptsController>.Instance), "crm.knowledge.concept.read");

        var result = Assert.IsType<ContentResult>(await controller.ReferenceValues("content-moderator-role", default));

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(NonAdminValues, result.Content);
        AssertConsumableOnly(gateway, "content-moderator-role");
    }

    [Fact]
    public async Task Segment_reference_values_pass_the_consumable_answer_through_for_a_declared_set()
    {
        var gateway = NonAdminGateway(uri => uri.EndsWith("/api/crm/segments/attribute-catalog")
            ? (HttpStatusCode.OK, """{"data":{"attributes":[{"valueSource":{"kind":"reference-set","referenceSetCode":"account-type"}}]}}""")
            : null);
        var controller = WithUser(new SegmentsController(new HttpClient(gateway), Configuration(), new KeyLocalizer<SharedResource>(),
            NullLogger<SegmentsController>.Instance), "crm.segment.read");

        var result = Assert.IsType<ContentResult>(await controller.ReferenceValues("account-type", default));

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(NonAdminValues, result.Content);
        Assert.DoesNotContain(gateway.Requests, r => r.Contains("/api/v1/reference-data/"));
    }

    [Fact]
    public async Task Visit_frequency_business_units_pass_the_consumable_answer_through()
    {
        var gateway = NonAdminGateway();
        var controller = WithUser(new VisitFrequencyPoliciesController(new HttpClient(gateway), Configuration(),
            NullLogger<VisitFrequencyPoliciesController>.Instance), "crm.territory.read");

        var result = Assert.IsType<ContentResult>(await controller.BusinessUnits(default));

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(NonAdminValues, result.Content);
        AssertConsumableOnly(gateway, "business-unit");
    }

    [Fact]
    public async Task The_permission_gates_are_unchanged_no_gateway_call_without_them()
    {
        var gateway = NonAdminGateway();

        var knowledge = WithUser(new KnowledgeController(new HttpClient(gateway), Configuration(),
            new KeyLocalizer<SharedResource>(), NullLogger<KnowledgeController>.Instance));
        var concepts = WithUser(new KnowledgeConceptsController(new HttpClient(gateway), Configuration(),
            new KeyLocalizer<SharedResource>(), NullLogger<KnowledgeConceptsController>.Instance));
        var frequency = WithUser(new VisitFrequencyPoliciesController(new HttpClient(gateway), Configuration(),
            NullLogger<VisitFrequencyPoliciesController>.Instance));

        Assert.Equal(403, Assert.IsType<ObjectResult>(await knowledge.ReferenceValues("account-type", default)).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>(await concepts.ReferenceValues("content-moderator-role", default)).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>(await frequency.BusinessUnits(default)).StatusCode);
        Assert.Empty(gateway.Requests);
    }

    // ============================================================ source scan: the nine files use the reader

    [Theory]
    [MemberData(nameof(ReaderFiles))]
    public void Each_crm_controller_reads_reference_sets_through_the_shared_reader(string file)
    {
        var text = File.ReadAllText(Path.Combine(CrmControllersDirectory(), file));

        Assert.Contains("_referenceSets.ReadAsync(", text);
    }

    [Theory]
    [MemberData(nameof(ReaderFiles))]
    public void No_crm_controller_builds_the_old_consumer_url_itself(string file)
    {
        var text = File.ReadAllText(Path.Combine(CrmControllersDirectory(), file));

        Assert.DoesNotContain("reference-data/sets", text);
        Assert.False(Regex.IsMatch(text, @"[?&]scope_key="), $"{file} still builds a scope_key query itself.");
    }

    [Fact]
    public void Every_crm_controller_class_that_reads_reference_sets_owns_one_reader()
    {
        foreach (var file in new[]
                 {
                     "AccountsController.cs", "ContactsController.cs", "ClaimsController.cs", "KnowledgeController.cs",
                     "KnowledgeConceptsController.cs", "SegmentsController.cs", "TerritoryManagementController.cs",
                     "VisitFrequencyPoliciesController.cs"
                 })
        {
            var text = File.ReadAllText(Path.Combine(CrmControllersDirectory(), file));
            Assert.True(Regex.Matches(text, @"new CrmReferenceSetReader\(").Count == 1, $"{file} must construct exactly one reader.");
        }
    }

    [Fact]
    public void No_other_crm_controller_reads_the_consumer_path_around_the_reader()
    {
        var offenders = Directory.EnumerateFiles(CrmControllersDirectory(), "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("/api/v1/reference-data/", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    // ============================================================ helpers

    private const string NonAdminValues =
        """{"isSuccessful":true,"data":{"setCode":"x","versionNumber":1,"items":[{"code":"v1","label":"Value one","isActive":true,"sortOrder":1}]}}""";

    /// <summary>The Platform as a non-admin tenant user sees it: consumable-sets route answers, the consumer path is 403.</summary>
    private static RecordingGateway NonAdminGateway(Func<string, (HttpStatusCode, string)?>? other = null) => new(uri =>
        other?.Invoke(uri)
        ?? (uri.Contains("/api/lookups/reference-data/consumable-sets/")
            ? (HttpStatusCode.OK, NonAdminValues)
            : uri.Contains("/api/v1/reference-data/")
                ? (HttpStatusCode.Forbidden, """{"errors":["forbidden"]}""")
                : (HttpStatusCode.NotFound, "{}")));

    private static void AssertConsumableOnly(RecordingGateway gateway, params string[] sets)
    {
        Assert.Equal(
            sets.Select(s => $"{Gateway}/api/lookups/reference-data/consumable-sets/{s}/published-values"),
            gateway.Requests);
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = Gateway }).Build();

    private static T WithUser<T>(T controller, params string[] permissions) where T : Controller
    {
        var claims = new List<Claim> { new("tenantId", Tenant) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
        };
        return controller;
    }

    private static JsonElement JsonOf(IActionResult result)
    {
        var value = result switch
        {
            JsonResult json => json.Value,
            ObjectResult obj => obj.Value,
            _ => throw new InvalidOperationException(result.GetType().Name)
        };
        return JsonDocument.Parse(JsonSerializer.Serialize(value)).RootElement;
    }

    private static string CrmControllersDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
        {
            current = current.Parent;
        }

        Assert.NotNull(current);
        return Path.Combine(current!.FullName, "frontend", "Diten.Web", "Controllers", "CRM");
    }

    private sealed class RecordingGateway(Func<string, (HttpStatusCode Status, string Body)> route) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!.AbsoluteUri;
            if (!uri.EndsWith("/api/crm/segments/attribute-catalog", StringComparison.Ordinal))
            {
                Requests.Add(uri);
            }

            var (status, body) = route(uri);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class KeyLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
