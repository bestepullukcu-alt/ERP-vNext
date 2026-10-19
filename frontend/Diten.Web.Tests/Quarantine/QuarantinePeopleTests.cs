using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.Web.Controllers;
using Diten.Web.Models.Quarantine;
using Diten.Web.Models.Traceability;
using Diten.Web.Services;
using Diten.Web.Tests.Traceability;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Quarantine;

/// <summary>
/// WP-0174-ITEM — FU-0175-Q5-02: the quarantine pages show people by NAME. Measured: the Web fills <c>UserRef.name</c> of every
/// <c>{ userId }</c> on the page (hold, history, decision) and of the "user:&lt;id&gt;" approver candidates from AuthService's
/// existing <c>GET /api/users/{id}/display-label</c> with the caller's token; an id that does not resolve (404, an unnamed user,
/// AuthService down) keeps no name and the page shows the short id + "unknown user" (QuarantineUi.person), never an error.
/// </summary>
public sealed class QuarantinePeopleTests
{
    private const string Gateway = "http://gateway.test";
    private static readonly string Named = "aaaaaaaa-1111-4111-8111-000000000001";
    private static readonly string Missing = "bbbbbbbb-2222-4222-8222-000000000002";
    private static readonly string Unnamed = "cccccccc-3333-4333-8333-000000000003";
    private static readonly string HoldId = "dddddddd-4444-4444-8444-000000000004";

    private static string Label(string id, string state, string? label)
        => JsonSerializer.Serialize(new { isSuccessful = true, statusCode = 200, data = new { userId = id, displayLabel = label, labelState = state } });

    private static FakeGateway People()
    {
        var gateway = new FakeGateway();
        gateway.On($"/api/users/{Named}/display-label", 200, Label(Named, "Named", "Zehra Yıldız"));
        gateway.On($"/api/users/{Unnamed}/display-label", 200, Label(Unnamed, "Unnamed", null));
        return gateway; // Missing → 404
    }

    [Fact]
    public void Every_user_id_of_a_page_is_collected_once_from_objects_and_user_candidates()
    {
        var hold = JsonNode.Parse($$"""
            { "createdBy": { "userId": "{{Named}}" }, "reviewStartedBy": { "userId": "{{Missing}}" },
              "history": [ { "by": { "userId": "{{Named}}" } }, { "by": { "userId": "not-a-uuid" } } ],
              "approverCandidates": [ "user:{{Unnamed}}", "position:{{Missing}}" ] }
            """);
        Assert.Equal([Named, Missing, Unnamed], QuarantinePeople.UserIds(hold));
    }

    [Fact]
    public async Task Only_a_named_200_label_resolves_and_each_id_is_read_once_with_the_label_path()
    {
        var gateway = People();
        var names = await QuarantinePeople.ResolveAsync([Named, Missing, Unnamed, Named], async (path, ct) =>
        {
            using var response = await new HttpClient(gateway).GetAsync(Gateway + path, ct);
            return ((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));
        }, CancellationToken.None);
        Assert.Equal("Zehra Yıldız", Assert.Single(names).Value);
        Assert.Equal(3, gateway.Requests.Count);
        Assert.All(gateway.Requests, r => Assert.Matches("^/api/users/[0-9a-f-]{36}/display-label$", r.Path));
    }

    [Fact]
    public void A_name_is_written_only_where_the_id_resolved_and_a_service_name_is_kept()
    {
        var page = JsonNode.Parse($$"""
            { "createdBy": { "userId": "{{Named}}" }, "decider": { "userId": "{{Named}}", "name": "Servisin adı" },
              "history": [ { "by": { "userId": "{{Missing}}" } } ] }
            """)!;
        QuarantinePeople.Fill(new Dictionary<string, string> { [Named] = "Zehra Yıldız" }, page);
        Assert.Equal("Zehra Yıldız", (string?)page["createdBy"]!["name"]);
        Assert.Equal("Servisin adı", (string?)page["decider"]!["name"]);
        Assert.Null(page["history"]![0]!["by"]!["name"]);
    }

    [Fact]
    public async Task The_hold_page_data_carries_names_and_an_unresolved_person_stays_an_id()
    {
        var gateway = People();
        // FU-WEB-CONTRACT-FAKES: a whole HoldDetail and a whole Settings of quarantine.openapi.yaml (the number is KRN-YYYY-NNNN)
        gateway.On($"/api/quarantine/holds/{HoldId}", 200, $$"""
            { "holdId": "{{HoldId}}", "holdNumber": "KRN-2026-0001", "legalEntityId": "eeeeeeee-5555-4555-8555-000000000005",
              "item": { "itemId": "eeeeeeee-6666-4666-8666-000000000006", "skuId": "eeeeeeee-7777-4777-8777-000000000007", "skuLevel": "Lsku" },
              "warehouse": { "id": "eeeeeeee-8888-4888-8888-000000000008" }, "location": { "id": "eeeeeeee-9999-4999-8999-000000000009" },
              "stockStatus": "QUARANTINE", "quantity": 5, "baseUom": { "id": "EA" }, "source": "Manual", "enteredAtUtc": "2026-10-09T10:00:00Z",
              "createdBy": { "userId": "{{Named}}" }, "reviewState": "InReview", "status": "Open", "waitDays": 1, "targetDays": 7, "late": false, "version": 2,
              "attachments": [], "movements": [], "decisions": [],
              "history": [ { "atUtc": "2026-10-09T10:00:00Z", "action": "Quarantined", "by": { "userId": "{{Named}}" } },
                           { "atUtc": "2026-10-09T11:00:00Z", "action": "ReviewStarted", "by": { "userId": "{{Missing}}" } } ] }
            """);
        gateway.On("/api/quarantine/settings", 200, """
            { "waitTargetDays": 7, "fourEyes": false, "approverCandidates": [], "sampleLocations": {}, "lotReleaseRequiresApproval": false, "reconcileAfterSeconds": 300, "version": 1 }
            """);

        var result = await Controller(gateway).HoldData(HoldId, CancellationToken.None);
        var hold = Body(result).GetProperty("hold");
        Assert.Equal("Zehra Yıldız", hold.GetProperty("createdBy").GetProperty("name").GetString());
        Assert.Equal("Zehra Yıldız", hold.GetProperty("history")[0].GetProperty("by").GetProperty("name").GetString());
        Assert.False(hold.GetProperty("history")[1].GetProperty("by").TryGetProperty("name", out _));
        // the label read goes out with the caller's token
        Assert.All(gateway.Requests.Where(r => r.Path.Contains("/display-label", StringComparison.Ordinal)), r => Assert.Equal("Bearer caller-token", r.Authorization));
    }

    [Fact]
    public async Task The_settings_candidates_are_named_through_lookups_users()
    {
        var result = await Controller(People()).Lookups("users", null, null, null, $"{Named},{Missing},not-a-uuid", CancellationToken.None);
        var items = Body(result).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([Named, Missing], items.Select(i => i.GetProperty("id").GetString()));
        Assert.Equal("Zehra Yıldız", items[0].GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, items[1].GetProperty("name").ValueKind);
    }

    [Fact]
    public void The_pages_render_people_through_QuarantineUi_person_with_the_unknown_user_fallback()
    {
        var js = Path.Combine(TraceViewRenderer.WebRoot, "wwwroot", "assets", "js", "Quarantine");
        var ui = File.ReadAllText(Path.Combine(js, "quarantine-ui.js"));
        Assert.Contains("const person = (ref) =>", ui);
        Assert.Contains("t('Quarantine.Common.UnknownUser')", ui);

        var details = File.ReadAllText(Path.Combine(js, "details.js"));
        foreach (var call in new[] { "ui.person(h.by)", "ui.person(decision.decider)", "ui.person(wf.approver)", "ui.person(h.createdBy)" })
            Assert.Contains(call, details);
        Assert.DoesNotMatch(@"\?\.userId\s*\|\|", details); // no raw-id fallback left

        var settings = File.ReadAllText(Path.Combine(js, "settings.js"));
        Assert.Contains("labels.users[id] || ui.unknownPerson(id)", settings);
        Assert.Contains("/Quarantine/lookups/users?ids=", settings);
    }

    /// <summary>FU-0175-Q5-01 on the page: "Notu düzenle" exists only for quarantine.holds.review (UAS-001) and saves with the page ETag.</summary>
    [Fact]
    public void The_note_editor_is_drawn_only_for_the_review_key_and_patches_with_the_page_ETag()
    {
        var view = File.ReadAllText(Path.Combine(TraceViewRenderer.WebRoot, "Views", "Quarantine", "Details.cshtml"));
        foreach (var id in new[] { "qr-d-note-edit", "qr-d-note-editor" })
        {
            var at = view.IndexOf($"id=\"{id}\"", StringComparison.Ordinal);
            Assert.True(at > 0, id);
            var guard = view.LastIndexOf("@if (Perms.Has(\"quarantine.holds.review\"))", at, StringComparison.Ordinal);
            Assert.True(guard > 0 && view.IndexOf('}', guard) > at, $"{id} is outside the quarantine.holds.review guard");
        }

        var details = File.ReadAllText(Path.Combine(TraceViewRenderer.WebRoot, "wwwroot", "assets", "js", "Quarantine", "details.js"));
        Assert.Contains("ui.write('PATCH', ", details);
        Assert.Contains("{ analysisNote: note }, state.etag)", details);
        Assert.Contains("show(ui.byId('qr-d-note-edit'), ui.can('STARTREVIEW') && open && !noteLocked);", details); // FUB2: + the decision lock
    }

    [Theory]
    [InlineData("Item")]
    [InlineData("Lsku")]
    public void The_lot_form_accepts_every_TRACE_1_3_0_level(string level)
    {
        var sku = Guid.NewGuid();
        Assert.True(LotFormRules.TryParseSku($"{level}:{sku}", out var parsed, out var skuId));
        Assert.Equal(level, parsed);
        Assert.Equal(sku.ToString("D"), skuId);
        Assert.False(LotFormRules.TryParseSku($"Pallet:{sku}", out _, out _));
    }

    // ── plumbing ─────────────────────────────────────────────────────────────────────────────────────────────

    private static JsonElement Body(IActionResult result) => result switch
    {
        ContentResult content => JsonDocument.Parse(content.Content!).RootElement,
        JsonResult json => JsonDocument.Parse(JsonSerializer.Serialize(json.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))).RootElement,
        _ => throw new Xunit.Sdk.XunitException("unexpected result " + result.GetType().Name)
    };

    private static QuarantineController Controller(FakeGateway gateway)
    {
        var controller = new QuarantineController(new Factory(gateway), new HttpClient(gateway),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = Gateway }).Build(),
            new Permissions(["quarantine.holds.read", "quarantine.holds.review"]), new KeyLocalizer(), NullLogger<QuarantineController>.Instance);
        var claims = new List<Claim> { new("tenantId", Guid.NewGuid().ToString()), new("sub", Guid.NewGuid().ToString()), new("legal_entity_id", Guid.NewGuid().ToString()) };
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        http.Request.Headers.Cookie = "access_token=caller-token";
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class Permissions(string[] keys) : IPermissionSnapshot
    {
        public bool Has(string permissionKey) => keys.Contains(permissionKey, StringComparer.OrdinalIgnoreCase);
        public IReadOnlyCollection<string> Keys => keys;
    }

    private sealed class KeyLocalizer : IStringLocalizer<Diten.Web.Views.Quarantine.Quarantine>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class FakeGateway : HttpMessageHandler
    {
        private readonly List<(string Path, int Status, string Body)> _routes = [];

        public List<(string Path, string? Authorization)> Requests { get; } = [];

        public void On(string path, int status, string body) { Contracts.ContractRoutes.Require(path, status, body); _routes.Add((path, status, body)); } // FU-WEB-CONTRACT-FAKES

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            lock (Requests) Requests.Add((path, request.Headers.Authorization?.ToString()));
            var route = _routes.FirstOrDefault(r => r.Path == path);
            return Task.FromResult(route.Path is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}", Encoding.UTF8, "application/json") }
                : new HttpResponseMessage((HttpStatusCode)route.Status) { Content = new StringContent(route.Body, Encoding.UTF8, "application/json") });
        }
    }
}
