using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web.Controllers;
using Diten.Web.Models.Warehouse;
using Diten.Web.Services;
using Diten.Web.Views.Warehouse.CountVariances;
using Diten.Web.Views.Warehouse.CycleCounts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Diten.Web.Tests.Warehouse;

/// <summary>
/// MOD-0181 DL-4 — the two same-origin cycle-count proxies driven on the REAL controllers with a Gateway handler (pack §11): every write carries
/// [ValidateAntiForgeryToken]; every action without its key is refused 403 and never reaches the Gateway; the page's Accept-Language travels on every
/// Gateway request (reads and writes); the sheet's entry is one keyed, If-Match'ed request on the line; the variance rows carry the four-eyes flag
/// (the caller counted the line or submitted the count) and the count's ETag for "Yeniden say"; service refusals come back as the resx sentence with
/// their status and code kept; a variance query outside the contract is refused before the Gateway.
/// </summary>
public sealed class CycleCountProxyControllerTests
{
    private const string Gateway = "http://gateway.test";
    private static readonly Guid TenantId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid CompanyId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private const string Me = "55555555-0000-0000-0000-000000000001";
    private const string Other = "c4444444-0000-0000-0000-000000000004";
    private const string CountId = "b1111111-0000-0000-0000-000000000001";
    private const string LineId = "b2222222-0000-0000-0000-000000000002";
    private const string WarehouseId = "a2222222-0000-0000-0000-000000000002";
    private const string Bin = "a3333333-0000-0000-0000-000000000003";
    private const string Key = "d5555555-0000-4000-8000-000000000005";

    private static string Line(string countedBy) =>
        $$"""{"lineId":"{{LineId}}","countId":"{{CountId}}","warehouseId":"{{WarehouseId}}","locationId":"{{Bin}}","item":{"itemId":"e1","skuId":"f1","skuLevel":"Lsku"},"stockStatus":"Available","baseUomId":"EA","origin":"Snapshot","serialControlled":false,"expectedAtCount":"10","countedQuantity":"8","countedBy":"{{countedBy}}","varianceQuantity":"-2","status":"AwaitingApproval","version":4}""";

    private static string Count(string submittedBy) =>
        $$"""{"countId":"{{CountId}}","number":"MG-CNT-000001","warehouseId":"{{WarehouseId}}","countType":"Cycle","blind":false,"scopeLocationIds":[],"scopeSkuIds":[],"plannedDate":"2026-11-11","status":"Submitted","scopeBinIds":[],"lineCount":1,"createdBy":"{{Other}}","createdAt":"2026-11-10T08:00:00Z","submittedBy":"{{submittedBy}}","version":7,"contractVersion":"v1"}""";

    // ── antiforgery and permissions ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(typeof(CycleCountsController), 8)]
    [InlineData(typeof(CountVariancesController), 5)]
    [InlineData(typeof(CountEvidenceExportsController), 1)] // DL-5: the export is its one write
    public void Every_write_carries_the_antiforgery_check_and_every_action_one_verb(Type controller, int writes)
    {
        var actions = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any()).ToList();
        Assert.True(actions.Count >= 6, $"{controller.Name}: {actions.Count} actions");
        var posts = actions.Where(m => m.GetCustomAttribute<HttpPostAttribute>() is not null).ToList();
        Assert.Equal(writes, posts.Count);
        Assert.All(posts, m => Assert.True(m.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() is not null, $"{controller.Name}.{m.Name} lacks [ValidateAntiForgeryToken]"));
        Assert.All(actions, m => Assert.Single(m.GetCustomAttributes<HttpMethodAttribute>()));
        Assert.DoesNotContain(actions, m => m.GetCustomAttribute<HttpDeleteAttribute>() is not null); // no "Sil" — G-0181-01
    }

    [Fact]
    public async Task Every_action_without_its_key_is_refused_and_never_reaches_the_gateway()
    {
        var gateway = new FakeGateway();
        var write = new CycleCountWriteRequest
        {
            IdempotencyKey = Key, IfMatch = "W/\"3\"", WarehouseId = WarehouseId, CountType = "Cycle", ScopeLocationIds = [Bin], PlannedDate = "2026-11-12",
            CountedQuantity = "1", LocationId = Bin, ItemId = Bin, SkuId = Bin, SkuLevel = "Lsku", Reason = "r", Decision = "Reject", LineIds = [LineId],
            Items = [new CycleCountBulkItem { Id = CountId, Version = 1 }]
        };
        string[] read = [CycleCountPermissionKeys.CountsRead];
        string[] variances = [CycleCountPermissionKeys.VariancesRead];
        var results = new[]
        {
            await Counts(gateway, [], ("start", "0"), ("length", "25")).List(CancellationToken.None),
            await Counts(gateway, []).Dashboard(null, CancellationToken.None),
            await Counts(gateway, []).Detail(CountId, CancellationToken.None),
            await Counts(gateway, []).Lines(CountId, 1, CancellationToken.None),
            await Counts(gateway, read).Create(write, CancellationToken.None),
            await Counts(gateway, read).Edit(CountId, write, CancellationToken.None),
            await Counts(gateway, read).Release(CountId, write, CancellationToken.None),
            await Counts(gateway, read).Cancel(CountId, write, CancellationToken.None),
            await Counts(gateway, read).Submit(CountId, write, CancellationToken.None),
            await Counts(gateway, read).BulkDelete(write, CancellationToken.None),
            await Counts(gateway, read).Record(CountId, LineId, write, CancellationToken.None),
            await Counts(gateway, read).AddLine(CountId, write, CancellationToken.None),
            await Variances(gateway, read, ("start", "0"), ("length", "25")).List(CancellationToken.None),
            await Variances(gateway, read).Detail(LineId, CancellationToken.None),
            await Variances(gateway, variances).Approve(LineId, write, CancellationToken.None),
            await Variances(gateway, variances).Reject(LineId, write, CancellationToken.None),
            await Variances(gateway, variances).Retry(LineId, write, CancellationToken.None),
            await Variances(gateway, variances).Resolve(LineId, write, CancellationToken.None),
            await Variances(gateway, variances).Recount(CountId, write, CancellationToken.None)
        };

        Assert.All(results, r => Assert.Equal(403, Assert.IsType<JsonResult>(r).StatusCode));
        Assert.All(results, r => Assert.Equal("CycleCount.Error.NoPermission", Body(r).GetProperty("error").GetProperty("message").GetString()));
        Assert.Empty(gateway.Requests);
    }

    // ── DL-5 Evidence Pack (AC-43, AC-44) ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Every_evidence_action_without_its_key_is_refused_and_never_reaches_the_gateway()
    {
        var gateway = new FakeGateway();
        var write = new CountEvidenceWriteRequest { IdempotencyKey = Key, CountId = CountId, Format = "Pdf" };
        var results = new[]
        {
            await Evidence(gateway, [CycleCountPermissionKeys.EvidenceExport], null, ("start", "0"), ("length", "25")).List(CancellationToken.None),
            await Evidence(gateway, [CycleCountPermissionKeys.EvidenceExport], null).Export(CancellationToken.None),
            await Evidence(gateway, [CycleCountPermissionKeys.EvidenceExport], null).Download(CountId, CancellationToken.None),
            await Evidence(gateway, [CycleCountPermissionKeys.EvidenceRead], null).Create(write, CancellationToken.None),
            await Evidence(gateway, [CycleCountPermissionKeys.EvidenceRead], null).Counts(null, CancellationToken.None)
        };
        Assert.All(results, r => Assert.Equal(403, Assert.IsType<JsonResult>(r).StatusCode));
        Assert.All(results, r => Assert.Equal("CycleCount.Error.NoPermission", Body(r).GetProperty("error").GetProperty("message").GetString()));
        Assert.Empty(gateway.Requests);
    }

    /// <summary>AC-43 (r6 SB2/F-3): the export carries the PAGE's language — a tr page sends Accept-Language: tr — and only the draft's two members.</summary>
    [Fact]
    public async Task An_export_from_a_Turkish_page_asks_the_service_in_Turkish_with_its_key_and_only_the_format()
    {
        var gateway = new FakeGateway();
        gateway.On($"/api/warehouse/cycle-counts/{CountId}/evidence-exports", 201,
            $$"""{"exportId":"{{LineId}}","countId":"{{CountId}}","countNumber":"MG-CNT-000001","format":"Pdf","language":"tr","requestedLanguage":"tr","requestedBy":"{{Me}}","requestedAt":"2026-11-12T10:00:00.000Z","fileName":"count-evidence-MG-CNT-000001-tr-20261112100000.pdf","sha256":"{{new string('a', 64)}}","sizeBytes":10,"status":"Ready","contractVersion":"v1"}""");

        var result = await Evidence(gateway, [CycleCountPermissionKeys.EvidenceExport], "tr")
            .Create(new CountEvidenceWriteRequest { IdempotencyKey = Key, CountId = CountId.ToUpperInvariant(), Format = "Pdf" }, CancellationToken.None);

        var call = Assert.Single(gateway.Requests);
        Assert.Equal("POST", call.Method);
        Assert.Equal("tr", call.Language);
        Assert.Equal(Key, call.IdempotencyKey);
        using var sent = JsonDocument.Parse(call.Body);
        Assert.Equal(["format", "idempotencyKey"], sent.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal("Pdf", sent.RootElement.GetProperty("format").GetString());
        Assert.Equal("CountEvidence.Saved.Created", Body(result).GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("Xlsx", CountId)]
    [InlineData("Pdf", "not-a-uuid")]
    public async Task An_export_outside_the_draft_is_refused_before_the_gateway(string format, string countId)
    {
        var gateway = new FakeGateway();
        var result = await Evidence(gateway, [CycleCountPermissionKeys.EvidenceExport], "en")
            .Create(new CountEvidenceWriteRequest { IdempotencyKey = Key, CountId = countId, Format = format }, CancellationToken.None);
        Assert.Equal(400, Assert.IsType<JsonResult>(result).StatusCode);
        Assert.Empty(gateway.Requests);
    }

    [Fact]
    public async Task A_download_passes_the_stored_bytes_their_name_and_SHA256_and_a_refusal_comes_back_as_its_sentence()
    {
        var bytes = "%PDF-1.7 evidence"u8.ToArray();
        var sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        var gateway = new FakeGateway();
        gateway.OnFile($"/api/warehouse/count-evidence-exports/{LineId}/file", bytes, "application/pdf", "count-evidence-MG-CNT-000001-en.pdf", sha);
        gateway.On($"/api/warehouse/count-evidence-exports/{CountId}/file", 404,
            """{"error":{"code":"COUNT_EVIDENCE_NOT_FOUND","message":"x","correlationId":"c-5"},"contractVersion":"v1"}""");

        var controller = Evidence(gateway, [CycleCountPermissionKeys.EvidenceRead], "en");
        var file = Assert.IsType<FileContentResult>(await controller.Download(LineId, CancellationToken.None));
        Assert.Equal(bytes, file.FileContents);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("count-evidence-MG-CNT-000001-en.pdf", file.FileDownloadName);
        Assert.Equal(sha, controller.Response.Headers["X-Content-SHA256"].ToString());

        var refused = await Evidence(gateway, [CycleCountPermissionKeys.EvidenceRead], "en").Download(CountId, CancellationToken.None);
        Assert.Equal(404, Assert.IsType<JsonResult>(refused).StatusCode);
        Assert.Equal("CycleCount.Error.COUNT_EVIDENCE_NOT_FOUND", Body(refused).GetProperty("error").GetProperty("message").GetString());
    }

    // ── language ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_pages_language_travels_on_reads_and_writes()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/warehouse/cycle-counts", 200, """{"items":[],"total":0,"page":1,"pageSize":25,"contractVersion":"v1"}""");
        gateway.On($"/api/warehouse/cycle-counts/{CountId}/submit", 409, """{"error":{"code":"COUNT_LINES_UNCOUNTED","message":"x","correlationId":"c-9"},"contractVersion":"v1"}""");

        await Counts(gateway, [CycleCountPermissionKeys.CountsRead], language: "ar", ("start", "0"), ("length", "25")).List(CancellationToken.None);
        var refused = await Counts(gateway, [CycleCountPermissionKeys.CountsRecord], language: "fr-FR")
            .Submit(CountId, new CycleCountWriteRequest { IdempotencyKey = Key, IfMatch = "W/\"7\"" }, CancellationToken.None);

        Assert.Equal(["ar", "fr-FR"], gateway.Requests.Select(r => r.Language));
        Assert.Equal(TenantId.ToString(), gateway.Requests[0].Tenant);
        Assert.Equal(409, Assert.IsType<JsonResult>(refused).StatusCode);
        var error = Body(refused).GetProperty("error");
        Assert.Equal("COUNT_LINES_UNCOUNTED", error.GetProperty("code").GetString());
        Assert.Equal("CycleCount.Error.COUNT_LINES_UNCOUNTED", error.GetProperty("message").GetString());
        Assert.Equal("c-9", error.GetProperty("correlationId").GetString());
    }

    // ── the sheet ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_entry_is_one_keyed_If_Match_request_on_the_line_with_the_quantity_as_a_decimal_string()
    {
        var gateway = new FakeGateway();
        gateway.On($"/api/warehouse/cycle-counts/{CountId}/lines/{LineId}/count", 200, Line(Me));

        var result = await Counts(gateway, [CycleCountPermissionKeys.CountsRecord])
            .Record(CountId, LineId, new CycleCountWriteRequest { IdempotencyKey = Key, IfMatch = "W/\"3\"", CountedQuantity = " 4,5 ", ScanRef = " SC-1 " }, CancellationToken.None);

        var call = Assert.Single(gateway.Requests);
        Assert.Equal("POST", call.Method);
        Assert.Equal(Key, call.IdempotencyKey);
        Assert.Equal("W/\"3\"", call.IfMatch);
        using var sent = JsonDocument.Parse(call.Body);
        Assert.Equal("4.5", sent.RootElement.GetProperty("countedQuantity").GetString());
        Assert.Equal("SC-1", sent.RootElement.GetProperty("scanRef").GetString());
        Assert.Equal("CycleCount.Saved.Counted", Body(result).GetProperty("message").GetString());
    }

    [Theory]
    [InlineData(null, "W/\"3\"", "1")]
    [InlineData(Key, null, "1")]
    [InlineData(Key, "W/\"3\"", " ")]
    public async Task An_entry_without_its_key_ETag_or_quantity_is_refused_before_the_gateway(string? key, string? ifMatch, string quantity)
    {
        var gateway = new FakeGateway();
        var result = await Counts(gateway, [CycleCountPermissionKeys.CountsRecord])
            .Record(CountId, LineId, new CycleCountWriteRequest { IdempotencyKey = key, IfMatch = ifMatch, CountedQuantity = quantity }, CancellationToken.None);
        Assert.Equal(400, Assert.IsType<JsonResult>(result).StatusCode);
        Assert.Empty(gateway.Requests);
    }

    // ── the variance trace ───────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(Me, Other, true)]       // I counted it
    [InlineData(Other, Me, true)]       // I submitted the count
    [InlineData(Other, Other, false)]   // someone else did both
    public async Task A_variance_row_carries_the_four_eyes_flag_and_the_counts_ETag(string countedBy, string submittedBy, bool self)
    {
        var gateway = new FakeGateway();
        gateway.On("/api/warehouse/count-variances", 200,
            $$"""{"items":[{"source":"Count","countNumber":"MG-CNT-000001","line":{{Line(countedBy)}}}],"total":1,"page":1,"pageSize":25,"contractVersion":"v1"}""");
        gateway.On($"/api/warehouse/cycle-counts/{CountId}", 200, Count(submittedBy), etag: "W/\"7\"");

        var json = Body(await Variances(gateway, [CycleCountPermissionKeys.VariancesRead], ("start", "0"), ("length", "25"), ("source", "count")).List(CancellationToken.None));

        var row = json.GetProperty("data").GetProperty("items")[0];
        Assert.Equal(self, row.GetProperty("selfApproval").GetBoolean());
        Assert.Equal("W/\"7\"", row.GetProperty("countEtag").GetString());
        Assert.Equal("Submitted", row.GetProperty("countStatus").GetString());
        Assert.Equal("W/\"4\"", row.GetProperty("line").GetProperty("etag").GetString());
        Assert.StartsWith("/api/warehouse/count-variances?page=1&pageSize=25&source=count", gateway.Requests[0].PathAndQuery, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("source", "everything")]
    [InlineData("status", "Deleted")]
    [InlineData("from", "11/01/2026")]
    [InlineData("warehouseId", "not-a-uuid")]
    public async Task A_variance_query_outside_the_contract_is_refused_before_the_gateway(string key, string value)
    {
        var gateway = new FakeGateway();
        var result = await Variances(gateway, [CycleCountPermissionKeys.VariancesRead], ("start", "0"), ("length", "25"), (key, value)).List(CancellationToken.None);
        Assert.Equal(400, Assert.IsType<JsonResult>(result).StatusCode);
        Assert.Empty(gateway.Requests);
    }

    [Fact]
    public async Task A_self_approval_refusal_comes_back_as_its_sentence_with_403()
    {
        var gateway = new FakeGateway();
        gateway.On($"/api/warehouse/count-variances/{LineId}/approve", 403,
            """{"error":{"code":"COUNT_SELF_APPROVAL_FORBIDDEN","message":"x","correlationId":"c-3"},"contractVersion":"v1"}""");

        var result = await Variances(gateway, [CycleCountPermissionKeys.VariancesApprove])
            .Approve(LineId, new CycleCountWriteRequest { IdempotencyKey = Key, IfMatch = "W/\"4\"" }, CancellationToken.None);

        Assert.Equal(403, Assert.IsType<JsonResult>(result).StatusCode);
        Assert.Equal("CycleCount.Error.COUNT_SELF_APPROVAL_FORBIDDEN", Body(result).GetProperty("error").GetProperty("message").GetString());
        var call = Assert.Single(gateway.Requests);
        Assert.Equal(Key, call.IdempotencyKey);
        Assert.Equal("W/\"4\"", call.IfMatch);
    }

    [Fact]
    public async Task A_recount_goes_to_the_count_with_its_ETag_and_only_the_line_ids_and_reason()
    {
        var gateway = new FakeGateway();
        gateway.On($"/api/warehouse/cycle-counts/{CountId}/recount", 201, Count(Other).Replace("MG-CNT-000001", "MG-CNT-000002"));

        var result = await Variances(gateway, [CycleCountPermissionKeys.VariancesRecount])
            .Recount(CountId, new CycleCountWriteRequest { IdempotencyKey = Key, IfMatch = "W/\"7\"", LineIds = [LineId.ToUpperInvariant()], Reason = " shelf moved ", Blind = true },
                CancellationToken.None);

        var call = Assert.Single(gateway.Requests);
        Assert.Equal("W/\"7\"", call.IfMatch);
        using var sent = JsonDocument.Parse(call.Body);
        Assert.Equal([LineId], sent.RootElement.GetProperty("lineIds").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("shelf moved", sent.RootElement.GetProperty("reason").GetString());
        Assert.False(sent.RootElement.TryGetProperty("blind", out _));       // the recount never sends blind / scope (§8.5)
        Assert.False(sent.RootElement.TryGetProperty("scopeLocationIds", out _));
        Assert.Equal("MG-CNT-000002", Body(result).GetProperty("number").GetString());
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────

    private static JsonElement Body(IActionResult result) =>
        JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsType<JsonResult>(result).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))).RootElement;

    private static CycleCountsController Counts(FakeGateway gateway, string[] permissions, params (string Key, string Value)[] query)
        => Counts(gateway, permissions, null, query);

    private static CycleCountsController Counts(FakeGateway gateway, string[] permissions, string? language, params (string Key, string Value)[] query)
    {
        var controller = new CycleCountsController(new HttpClient(gateway), Configuration(), new Permissions(permissions),
            new KeyLocalizer<CycleCountsIndex>(), NullLogger<CycleCountsController>.Instance);
        controller.ControllerContext = Context(query, language);
        return controller;
    }

    private static CountVariancesController Variances(FakeGateway gateway, string[] permissions, params (string Key, string Value)[] query)
    {
        var controller = new CountVariancesController(new HttpClient(gateway), Configuration(), new Permissions(permissions),
            new KeyLocalizer<CountVariancesIndex>(), NullLogger<CountVariancesController>.Instance);
        controller.ControllerContext = Context(query, null);
        return controller;
    }

    private static CountEvidenceExportsController Evidence(FakeGateway gateway, string[] permissions, string? language, params (string Key, string Value)[] query)
    {
        var controller = new CountEvidenceExportsController(new HttpClient(gateway), Configuration(), new Permissions(permissions),
            new KeyLocalizer<Diten.Web.Views.Warehouse.CountEvidenceExports.CountEvidenceExportsIndex>(), NullLogger<CountEvidenceExportsController>.Instance);
        controller.ControllerContext = Context(query, language);
        return controller;
    }

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = Gateway }).Build();

    private static ControllerContext Context((string Key, string Value)[] query, string? language)
    {
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()), new("sub", Me), new("legal_entity_id", CompanyId.ToString()) };
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        if (language is not null) http.Request.Headers.AcceptLanguage = language;
        http.Request.Query = new QueryCollection(query.GroupBy(p => p.Key).ToDictionary(g => g.Key, g => new StringValues(g.Select(p => p.Value).ToArray())));
        return new ControllerContext { HttpContext = http };
    }

    private sealed class Permissions(string[] keys) : IPermissionSnapshot
    {
        public bool Has(string permissionKey) => keys.Contains(permissionKey, StringComparer.OrdinalIgnoreCase);
        public IReadOnlyCollection<string> Keys => keys;
    }

    /// <summary>Answers with the KEY, so a test can assert which resx sentence was chosen.</summary>
    private sealed class KeyLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class FakeGateway : HttpMessageHandler
    {
        private List<(string Prefix, int Status, string Body, string? ETag)> Routes { get; } = [];

        public List<(string Method, string PathAndQuery, string? Tenant, string? IdempotencyKey, string? IfMatch, string Body, string? Language)> Requests { get; } = [];

        public void On(string prefix, int status, string body, string? etag = null) => Routes.Add((prefix, status, body, etag));

        private List<(string Prefix, byte[] Bytes, string ContentType, string FileName, string Sha256)> Files { get; } = [];

        /// <summary>DL-5: a binary answer (the Evidence Pack file) with its media type, download name and X-Content-SHA256.</summary>
        public void OnFile(string prefix, byte[] bytes, string contentType, string fileName, string sha256) => Files.Add((prefix, bytes, contentType, fileName, sha256));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var pathAndQuery = request.RequestUri!.PathAndQuery;
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            // the master-name reads (product-master, locations, users) are not the subject here: no route = 404, names fall back to ids
            if (pathAndQuery.StartsWith("/api/warehouse", StringComparison.Ordinal))
            {
                lock (Requests)
                {
                    Requests.Add((request.Method.Method, pathAndQuery,
                        request.Headers.TryGetValues("X-Tenant-Id", out var tenant) ? tenant.Single() : null,
                        request.Headers.TryGetValues("Idempotency-Key", out var key) ? key.Single() : null,
                        request.Headers.TryGetValues("If-Match", out var ifMatch) ? ifMatch.Single() : null,
                        body,
                        request.Headers.TryGetValues("Accept-Language", out var lang) ? string.Join(",", lang) : null));
                }
            }

            var file = Files.FirstOrDefault(f => pathAndQuery.StartsWith(f.Prefix, StringComparison.Ordinal));
            if (file.Prefix is not null)
            {
                var content = new ByteArrayContent(file.Bytes);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
                content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment") { FileName = file.FileName };
                var ok = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
                ok.Headers.TryAddWithoutValidation("X-Content-SHA256", file.Sha256);
                return ok;
            }

            var route = Routes.Where(r => pathAndQuery.StartsWith(r.Prefix, StringComparison.Ordinal)).OrderByDescending(r => r.Prefix.Length).FirstOrDefault();
            if (route.Prefix is null) return new HttpResponseMessage(HttpStatusCode.NotFound);
            var response = new HttpResponseMessage((HttpStatusCode)route.Status) { Content = new StringContent(route.Body, Encoding.UTF8, "application/json") };
            if (route.ETag is not null) response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue(route.ETag[2..], isWeak: true);
            return response;
        }
    }
}
