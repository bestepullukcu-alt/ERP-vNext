using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web;
using Diten.Web.Controllers.CRM;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-KP-CH-1 — the chain template "Outputs" panel on the Web layer: the same-origin proxy to the CRM outputs read
/// (read permission, JWT tenant, only <c>includeOtherVersions</c> forwarded, refusals passed through), the panel script
/// (real counts, path / journey lists with links, "N planned visits", the other-versions switch, hidden on a new
/// template, "—" only when the read failed, the old "no source" note gone) and its 7-language texts on the bridge.
/// </summary>
public sealed class ChainTemplateOutputsPanelWebTests
{
    private const string GatewayUrl = "http://gateway.test";
    private const string Read = "crm.knowledge.concept.read";
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
    private static readonly Guid TenantId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid TemplateId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    private static readonly string[] NewKeys =
    [
        "OutputsAfterSave", "OutputsRestricted", "OutputsChainVersion", "OutputsCurrentRelease", "OutputsStageCount",
        "OutputsPathCount", "OutputsPlannedVisits", "OutputsAllVersions", "PathStatusDraft", "PathStatusReview",
        "PathStatusApproved", "PathStatusPublished", "PathStatusInactive", "PathStatusArchived", "JourneyStatus_draft",
        "JourneyStatus_review", "JourneyStatus_approved", "JourneyStatus_published", "JourneyStatus_inactive",
        "JourneyStatus_archived"
    ];

    // ============================================================ proxy

    [Theory]
    [InlineData(false, "false")]
    [InlineData(true, "true")]
    public async Task The_outputs_read_reaches_crm_with_the_jwt_tenant_and_only_the_versions_switch(bool all, string expected)
    {
        var gateway = new Gateway((_, _) => (HttpStatusCode.OK, """{"data":{"pathCount":1}}"""));
        var controller = ControllerWith(gateway, Read);
        controller.ControllerContext.HttpContext.Request.QueryString = new QueryString($"?includeOtherVersions={expected}&tenantId={Guid.NewGuid()}");

        var result = Assert.IsType<ContentResult>(await controller.TemplateOutputs(TemplateId, all, default));

        Assert.Equal(200, result.StatusCode);
        var request = Assert.Single(gateway.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal($"{GatewayUrl}/api/crm/knowledge/concept-chain-templates/{TemplateId}/outputs?includeOtherVersions={expected}", request.Uri);
        Assert.Equal(TenantId.ToString(), request.Tenant);
    }

    [Fact]
    public async Task The_outputs_read_needs_the_concept_read_permission()
    {
        var gateway = new Gateway((_, _) => (HttpStatusCode.OK, "{}"));
        var controller = ControllerWith(gateway, "crm.strategy-template.read");

        var result = await controller.TemplateOutputs(TemplateId, false, default);

        Assert.Equal(403, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Empty(gateway.Requests);
    }

    [Fact]
    public async Task A_crm_refusal_is_passed_through()
    {
        var gateway = new Gateway((_, _) => (HttpStatusCode.NotFound, """{"errors":["Concept chain template not found."]}"""));
        var controller = ControllerWith(gateway, Read);

        var result = Assert.IsType<ContentResult>(await controller.TemplateOutputs(TemplateId, false, default));

        Assert.Equal(404, result.StatusCode);
    }

    // ============================================================ panel script

    [Fact]
    public void The_panel_reads_the_outputs_endpoint_and_drops_the_no_source_note()
    {
        var script = Script();
        Assert.Contains("/outputs?includeOtherVersions=", script);
        Assert.DoesNotContain("OutputsNoSource", script);
        Assert.DoesNotContain("${kv(L.KnowledgePaths || '', dash)}", script);
        Assert.Contains("${outputsHtml()}", script);
    }

    [Fact]
    public void The_panel_shows_counts_lists_links_and_the_versions_switch()
    {
        var html = OutputsHtml();
        Assert.Contains("o.pathCount", html);
        Assert.Contains("o.currentReleaseCount", html);
        Assert.Contains("o.journeyCount", html);
        Assert.Contains("o.plannedVisitCount", html);
        Assert.Contains("/CRM/KnowledgePaths/${encodeURIComponent(p.pathId)}", html);
        Assert.Contains("/CRM/ContentEngagementJourneys", html);
        Assert.Contains("js-outputs-all-versions", html);
        Assert.Contains("pathStatusBadge(p.pathStatus)", html);
        // Restricted → the count stays, the detail is replaced by a note (nothing invented).
        Assert.Contains("o.pathsRestricted ? restrictedNote", html);
        Assert.Contains("o.journeysRestricted ? restrictedNote", html);
        // A visit is a count, never a link / list.
        Assert.DoesNotContain("plannedVisitId", html);
    }

    [Fact]
    public void A_dash_is_shown_only_when_the_outputs_could_not_be_read_and_a_new_template_waits_for_save()
    {
        var html = OutputsHtml();
        Assert.Contains("if (!currentRow?.conceptChainTemplateId) return `<div class=\"form-text\">${esc(L.OutputsAfterSave || '')}</div>`;", html);
        // "—" appears in exactly one branch: the not-ready one (loading shows L.Loading, error shows the dash).
        Assert.Single(Regex.Matches(html, @"\bdash\b"));
        Assert.Contains("outputsState === 'loading'", html);
    }

    [Fact]
    public void Every_server_value_in_the_panel_is_escaped()
    {
        var html = OutputsHtml();
        foreach (var field in new[] { "p.pathCode", "p.pathName", "j.journeyCode", "j.journeyName", "j.languageCode" })
        {
            Assert.Contains($"esc({field})", html);
        }
    }

    // ============================================================ L10n

    [Fact]
    public void The_new_texts_exist_in_seven_languages_without_echo_and_are_on_the_bridge()
    {
        var bridge = File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "KnowledgeConcepts", "_TemplateFormL10n.cshtml"));
        var reference = Resx("en").Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var language in Languages)
        {
            var values = Resx(language);
            Assert.True(reference.SetEquals(values.Keys), $"{language}: key set differs from en");
            Assert.False(values.ContainsKey("OutputsNoSource"), $"{language}: OutputsNoSource left behind");
            foreach (var key in NewKeys)
            {
                Assert.True(values.TryGetValue(key, out var value), $"{language}: missing {key}");
                Assert.False(string.IsNullOrWhiteSpace(value), $"{language}: {key} is empty");
                Assert.NotEqual(key, value);
            }
        }

        Assert.All(NewKeys, key => Assert.Contains($"\"{key}\"", bridge));
        Assert.DoesNotContain("\"OutputsNoSource\"", bridge);
        Assert.Equal("{0} planlı ziyaret", Resx("tr")["OutputsPlannedVisits"]);
        Assert.Equal("Diğer sürümleri de göster", Resx("tr")["OutputsAllVersions"]);
    }

    [Fact]
    public void Every_outputs_key_the_script_reads_is_on_the_bridge()
    {
        var bridge = File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "KnowledgeConcepts", "_TemplateFormL10n.cshtml"));
        var used = Regex.Matches(OutputsHtml(), @"\bL\.([A-Za-z_]+)").Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.NotEmpty(used);
        Assert.All(used, key => Assert.Contains($"\"{key}\"", bridge));
    }

    // ============================================================ helpers

    private static string Script() => File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM",
        "KnowledgeConcepts", "template-form.js"));

    /// <summary>The WP-KP-CH-1 block of the script (from the loader to the end of outputsHtml).</summary>
    private static string OutputsHtml()
    {
        var script = Script();
        var start = script.IndexOf("const loadOutputs = async", StringComparison.Ordinal);
        var end = script.IndexOf("const renderConnections = ()", StringComparison.Ordinal);
        Assert.True(start > 0 && end > start, "outputs block not found");
        return script[start..end];
    }

    private static Dictionary<string, string> Resx(string language)
    {
        var path = Path.Combine(WebRoot(), "Resources", "Views", "CRM", "KnowledgeConcepts", $"KnowledgeConceptsIndex.{language}.resx");
        return XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);
    }

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }

    private static KnowledgeConceptsController ControllerWith(Gateway gateway, params string[] permissions)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();
        var controller = new KnowledgeConceptsController(new HttpClient(gateway), configuration, new KeyLocalizer(),
            NullLogger<KnowledgeConceptsController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
        };
        return controller;
    }

    private sealed class Gateway(Func<HttpMethod, string, (HttpStatusCode Status, string Body)> route) : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Uri, string? Tenant)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!.ToString();
            Requests.Add((request.Method, uri, request.Headers.TryGetValues("X-Tenant-Id", out var t) ? t.Single() : null));
            var (status, body) = route(request.Method, uri);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
