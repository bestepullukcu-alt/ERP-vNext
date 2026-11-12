using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Persistence.CycleCounting;
using Diten.InventoryService.Tests.Expiry;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using Xunit;
using static Diten.InventoryService.Tests.CycleCounting.CycleCountScenario;

namespace Diten.InventoryService.Tests.CycleCounting;

/// <summary>
/// MOD-0181 DL-5 — the Evidence Pack (pack §4.5, §21; AC-43, AC-44), end to end on the real host with every answer checked against the DL-5 draft
/// rows (<see cref="CountEvidenceContractHandler"/>):
/// <list type="bullet">
/// <item>AC-43 content: the head, every line (expected, movements since the snapshot, expected at count, counted, variance), the decisions (who / when /
/// source — a tolerance approval's label is the TRANSLATION of the stored code), the ledger numbers, the manual <c>COUNT_*</c> section (SnapshotAt →
/// close, the two-way rule) and the timeline — the PDF carries every CSV cell;</item>
/// <item>AC-43 language: <c>tr</c> → both files Turkish; <c>zh</c> → an English PDF with the note and the requested code
/// (<c>language = en</c>, <c>requestedLanguage = zh</c>) and a Chinese CSV; an unsupported language → en; 7 service resx files with one key set;</item>
/// <item>AC-43 storage: insert-only record + bytes, the recorded SHA-256 = the downloaded file's; the same key replays (no second file);</item>
/// <item>AC-44: another tenant / company → 404 everywhere; each endpoint needs its own <c>cycle-count.evidence.*</c> key; the central record.</item>
/// </list>
/// </summary>
public sealed class CycleCountEvidenceTests(CycleCountApiFactory factory) : IClassFixture<CycleCountApiFactory>
{
    private const string EvidenceRead = "cycle-count.evidence.read";
    private const string EvidenceExport = "cycle-count.evidence.export";

    private readonly HttpClient _http = factory.CreateDefaultClient(new CountEvidenceContractHandler());

    // ── the count every content test exports ─────────────────────────────────────────────────────────────

    private sealed record Closed(CycleCountScenario S, string CountId, string Number, string ApprovedLineTn, string ManualTn, string Exporter);

    /// <summary>
    /// Bin A 10 counted 12 (+2, over the tolerance → approved by the approver, posted), bin B 5 counted 4 (−1 = 20 %, inside 1 / 50 % → approved
    /// by the tolerance, posted) and a manual COUNT_GAIN (MOD-0173 / MANUAL_MOVEMENT) on bin B after the submit — the count closes after it.
    /// </summary>
    private async Task<Closed> ClosedCountAsync()
    {
        var s = new CycleCountScenario(factory);
        await s.ToleranceAsync("1", "50");
        await s.ReceiveAsync(10, s.BinA);
        await s.ReceiveAsync(5, s.BinB);
        var released = await s.ReleasedAsync(await s.CreatedAsync(s.Form(scope: [s.BinA, s.BinB])));
        var countId = Id(released);
        var lines = await s.LinesAsync(countId);
        await s.CountedAsync(countId, LineOf(lines, s.BinA), 12);
        await s.CountedAsync(countId, LineOf(lines, s.BinB), 4);
        await s.SubmittedAsync(countId);

        var manual = s.Inventory.Movement("COUNT_GAIN", "1");
        manual["toWarehouseId"] = s.Inventory.Warehouse;
        manual["toLocationId"] = s.BinB.ToString("D");
        manual["sourceModule"] = "MOD-0173";
        manual["sourceType"] = "MANUAL_MOVEMENT";
        var manualTn = (await s.Inventory.PostExpectingAsync(manual, 201)).GetProperty("transactionNumber").GetString()!;

        var awaiting = LineOf(await s.LinesAsync(countId, s.ApproverToken), s.BinA);
        Assert.Equal("AwaitingApproval", awaiting.GetProperty("status").GetString());
        var approved = await Expect(s.ApproveAsync(awaiting), 200);
        Assert.Equal("Posted", approved.GetProperty("adjustment").GetProperty("status").GetString());
        var current = await s.CurrentAsync(countId);
        Assert.Equal("Closed", current.GetProperty("status").GetString());
        var exporter = s.TokenFor(Guid.NewGuid(), Read, EvidenceRead, EvidenceExport);
        return new Closed(s, countId, current.GetProperty("number").GetString()!,
            approved.GetProperty("adjustment").GetProperty("transactionNumber").GetString()!, manualTn, exporter);
    }

    // ── AC-43: content and language ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC43_a_Turkish_export_carries_every_section_in_Turkish_and_the_PDF_carries_every_CSV_cell()
    {
        var c = await ClosedCountAsync();
        var csvExport = await Expect(ExportAsync(c, "Csv", "tr-TR,tr;q=0.9,en;q=0.8"), 201);
        var pdfExport = await Expect(ExportAsync(c, "Pdf", "tr"), 201);
        Assert.Equal(("tr", "tr"), (Str(csvExport, "language"), Str(csvExport, "requestedLanguage")));
        Assert.Equal(("tr", "tr"), (Str(pdfExport, "language"), Str(pdfExport, "requestedLanguage")));

        var csv = Encoding.UTF8.GetString((await FileAsync(c.S, Str(csvExport, "exportId"), c.Exporter)).Bytes);
        var pdf = ExpiryEvidenceTests.PdfText.Read((await FileAsync(c.S, Str(pdfExport, "exportId"), c.Exporter)).Bytes).Text;

        // the head, the four sections, the quantities, the decisions, the ledger numbers, the manual movement and the timeline — in Turkish
        var approver = c.S.Approver.ToString("D");
        foreach (var expected in new[]
                 {
                     "Sayım kanıt paketi", c.Number, "Anlık görüntü zamanı", "Kapanış", "Kapsamdaki raf gözleri",
                     "Sayım satırları (2)", "Kararlar ve defter düzeltmeleri (2)", "Kapsam raf gözlerinde elle COUNT hareketleri (1)", "Zaman çizelgesi",
                     "Sayım anında beklenen", "Anlık görüntüden beri hareket", "Sayılan", "Fark",
                     // the ledger's wire quantities: bin A expected 10, counted 12, variance +2; bin B expected 5, counted 4, variance −1
                     ";10.000;", ";12.000;", ";2.000;", ";5.000;", ";4.000;", ";-1.000;",
                     "tolerans içinde", "kullanıcı kararı", approver, c.ApprovedLineTn, c.ManualTn, "MOD-0173 / MANUAL_MOVEMENT", "Sayım fazlası",
                     "Kapandı", "Fark onaylandı"
                 })
        {
            Assert.Contains(expected, csv, StringComparison.Ordinal);
        }

        // the PDF draws the same document: every non-empty CSV cell is in its text
        var flatPdf = Flat(pdf);
        var cells = csv.Split(["\r\n"], StringSplitOptions.RemoveEmptyEntries).Skip(1).SelectMany(l => l.Split(';'))
            .Select(x => x.Trim('"')).Where(x => x.Length > 0).Distinct().ToList();
        Assert.True(cells.Count > 80, $"only {cells.Count} CSV cells");
        var missing = cells.Where(x => !flatPdf.Contains(Flat(x), StringComparison.Ordinal)).ToList();
        Assert.True(missing.Count == 0, "the PDF lacks: " + string.Join(" | ", missing.Take(10)));
        Assert.Contains("Sayımkanıtpaketi", flatPdf, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AC43_the_tolerance_label_is_the_translation_of_the_stored_code_not_a_stored_text()
    {
        var c = await ClosedCountAsync();
        var tr = await CsvAsync(c, "tr");
        var en = await CsvAsync(c, "en");
        Assert.Contains("tolerans içinde", tr, StringComparison.Ordinal);
        Assert.DoesNotContain("tolerans içinde", en, StringComparison.Ordinal);
        Assert.Contains("within tolerance", en, StringComparison.Ordinal);
        // stored as a code: the line's decision source on the wire and in the store
        var line = LineOf(await c.S.LinesAsync(c.CountId, c.S.ApproverToken), c.S.BinB);
        Assert.Equal("Tolerance", line.GetProperty("decision").GetProperty("source").GetString());
        Assert.All(c.S.Stored(CycleCountCollections.EvidenceExports), e => Assert.DoesNotContain("tolerans", e.ToJson(), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AC43_a_Chinese_request_gives_an_English_PDF_with_the_note_and_a_Chinese_CSV_an_unsupported_one_English()
    {
        var c = await ClosedCountAsync();
        var pdfExport = await Expect(ExportAsync(c, "Pdf", "zh-CN"), 201);
        Assert.Equal(("en", "zh"), (Str(pdfExport, "language"), Str(pdfExport, "requestedLanguage")));
        Assert.EndsWith("-en-" + Regex.Match(Str(pdfExport, "fileName"), @"-(\d{14})\.pdf$").Groups[1].Value + ".pdf", Str(pdfExport, "fileName"), StringComparison.Ordinal);
        var pdf = Flat(ExpiryEvidenceTests.PdfText.Read((await FileAsync(c.S, Str(pdfExport, "exportId"), c.Exporter)).Bytes).Text);
        Assert.Contains(Flat(CycleCountEvidenceText.Of("CountPdf.EnglishFallback", "en")), pdf, StringComparison.Ordinal);
        Assert.Contains(Flat("Requested language: zh"), pdf, StringComparison.Ordinal);
        Assert.Contains("Cyclecountevidence", pdf, StringComparison.Ordinal);

        var csvExport = await Expect(ExportAsync(c, "Csv", "zh"), 201);
        Assert.Equal(("zh", "zh"), (Str(csvExport, "language"), Str(csvExport, "requestedLanguage")));
        Assert.Contains("盘点证据包", Encoding.UTF8.GetString((await FileAsync(c.S, Str(csvExport, "exportId"), c.Exporter)).Bytes), StringComparison.Ordinal);

        var arabic = await Expect(ExportAsync(c, "Pdf", "ar"), 201);
        Assert.Equal(("en", "ar"), (Str(arabic, "language"), Str(arabic, "requestedLanguage")));

        foreach (var unsupported in new[] { "de-DE", "", "x-klingon" })
        {
            var e = await Expect(ExportAsync(c, "Csv", unsupported), 201);
            Assert.Equal(("en", "en"), (Str(e, "language"), Str(e, "requestedLanguage")));
        }
    }

    [Fact]
    public async Task AC43_the_record_is_insert_only_its_sha256_is_the_downloaded_files_and_the_same_key_replays()
    {
        var c = await ClosedCountAsync();
        var key = NewKey();
        var first = await Expect(ExportAsync(c, "Pdf", "en", key), 201);
        var replay = await Expect(ExportAsync(c, "Pdf", "en", key), 200);
        Assert.Equal(Str(first, "exportId"), Str(replay, "exportId"));
        Assert.Equal(Str(first, "sha256"), Str(replay, "sha256"));
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await ErrorCode(ExportAsync(c, "Csv", "en", key), 400));
        Assert.Single(c.S.Stored(CycleCountCollections.EvidenceExports));
        Assert.Single(c.S.Stored(CycleCountCollections.EvidenceFiles));

        var (bytes, header) = await FileAsync(c.S, Str(first, "exportId"), c.Exporter);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Assert.Equal(Str(first, "sha256"), hash);
        Assert.Equal(hash, header);
        Assert.Equal(bytes.LongLength, first.GetProperty("sizeBytes").GetInt64());
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(bytes, 0, 4), StringComparison.Ordinal);
        var stored = Assert.Single(c.S.Stored(CycleCountCollections.EvidenceExports));
        Assert.Equal(hash, stored["Sha256"].AsString);
        Assert.Equal("Ready", stored["Status"].AsString);
        Assert.Equal(1, stored["Version"].AsInt32);

        var second = await Expect(ExportAsync(c, "Pdf", "en"), 201);
        Assert.NotEqual(Str(first, "exportId"), Str(second, "exportId"));
        var log = await Expect(c.S.GetAsync($"/api/warehouse/count-evidence-exports?countId={c.CountId}", c.Exporter), 200);
        Assert.Equal(2, log.GetProperty("total").GetInt64());
        Assert.Equal(Str(second, "exportId"), Str(log.GetProperty("items")[0], "exportId")); // newest first
        Assert.Single(c.S.CentralRows("CreateCountEvidenceExportCommand").Where(r => r.ToJson().Contains(Str(first, "exportId"), StringComparison.Ordinal)));
        Assert.Equal(2, c.S.CentralRows("CreateCountEvidenceExportCommand").Count);
    }

    // ── state, body and paging rules ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Only_a_submitted_or_closed_count_has_evidence_and_the_body_and_query_are_the_drafts()
    {
        var s = new CycleCountScenario(factory);
        var exporter = s.TokenFor(Guid.NewGuid(), Read, EvidenceRead, EvidenceExport);
        var draft = await s.CreatedAsync();
        Assert.Equal("COUNT_INVALID_STATE", await ErrorCode(SendAsync(s, HttpMethod.Post, $"/api/warehouse/cycle-counts/{Id(draft)}/evidence-exports",
            new { idempotencyKey = NewKey(), format = "Pdf" }, exporter, "en"), 409));
        Assert.Equal("COUNT_NOT_FOUND", await ErrorCode(SendAsync(s, HttpMethod.Post, $"/api/warehouse/cycle-counts/{Guid.NewGuid()}/evidence-exports",
            new { idempotencyKey = NewKey(), format = "Pdf" }, exporter, "en"), 404));

        var (countId, _) = await s.SubmittedLineAsync(3, 3);
        var path = $"/api/warehouse/cycle-counts/{countId}/evidence-exports";
        Assert.Equal("INVALID_REQUEST", await ErrorCode(SendAsync(s, HttpMethod.Post, path, new { idempotencyKey = NewKey(), format = "Xlsx" }, exporter, "en"), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(SendAsync(s, HttpMethod.Post, path, new { idempotencyKey = NewKey(), format = "Pdf", language = "tr" }, exporter, "en"), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(SendAsync(s, HttpMethod.Post, path, new { format = "Pdf" }, exporter, "en", key: null), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.GetAsync("/api/warehouse/count-evidence-exports?countId=nope", exporter), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.GetAsync("/api/warehouse/count-evidence-exports?pageSize=500", exporter), 400));
        Assert.Equal("COUNT_EVIDENCE_NOT_FOUND", await ErrorCode(SendAsync(s, HttpMethod.Get, $"/api/warehouse/count-evidence-exports/{Guid.NewGuid()}/file", null, exporter, "en"), 404));
        var created = await Expect(SendAsync(s, HttpMethod.Post, path, new { idempotencyKey = NewKey(), format = "Csv" }, exporter, "en"), 201);
        Assert.Equal("Csv", Str(created, "format"));
        Assert.Empty(s.Stored(CycleCountCollections.EvidenceExports).Where(e => e["Status"].AsString != "Ready"));
    }

    // ── AC-44: scope and keys ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC44_another_tenant_or_company_sees_404_on_every_evidence_endpoint_and_lists_nothing()
    {
        var c = await ClosedCountAsync();
        var export = await Expect(ExportAsync(c, "Csv", "en"), 201);
        var otherTenant = Guid.NewGuid();
        factory.TrackCountTenant(otherTenant);
        var otherCompany = Guid.NewGuid();
        factory.LegalEntities.RegisterName(otherCompany, "OTH", "Other company");
        foreach (var token in new[]
                 {
                     TestTokens.Mint(otherTenant, c.S.LegalEntity, c.S.User, [Read, EvidenceRead, EvidenceExport]),
                     TestTokens.Mint(c.S.Tenant, otherCompany, c.S.User, [Read, EvidenceRead, EvidenceExport])
                 })
        {
            Assert.Equal("COUNT_NOT_FOUND", await ErrorCode(SendAsync(c.S, HttpMethod.Post, $"/api/warehouse/cycle-counts/{c.CountId}/evidence-exports",
                new { idempotencyKey = NewKey(), format = "Pdf" }, token, "en"), 404));
            Assert.Equal("COUNT_EVIDENCE_NOT_FOUND", await ErrorCode(SendAsync(c.S, HttpMethod.Get,
                $"/api/warehouse/count-evidence-exports/{Str(export, "exportId")}/file", null, token, "en"), 404));
            var log = await Expect(c.S.GetAsync("/api/warehouse/count-evidence-exports", token), 200);
            Assert.Equal(0, log.GetProperty("total").GetInt64());
        }

        Assert.Single(c.S.Stored(CycleCountCollections.EvidenceExports));
    }

    [Fact]
    public async Task AC44_each_evidence_endpoint_needs_its_own_key()
    {
        var c = await ClosedCountAsync();
        var export = await Expect(ExportAsync(c, "Pdf", "en"), 201);
        var readOnly = c.S.TokenFor(Guid.NewGuid(), Read, EvidenceRead);
        var exportOnly = c.S.TokenFor(Guid.NewGuid(), Read, EvidenceExport);
        Assert.Equal(403, (int)(await SendAsync(c.S, HttpMethod.Post, $"/api/warehouse/cycle-counts/{c.CountId}/evidence-exports",
            new { idempotencyKey = NewKey(), format = "Pdf" }, readOnly, "en")).StatusCode);
        Assert.Equal(403, (int)(await SendAsync(c.S, HttpMethod.Get, "/api/warehouse/count-evidence-exports", null, exportOnly, "en")).StatusCode);
        Assert.Equal(403, (int)(await SendAsync(c.S, HttpMethod.Get, $"/api/warehouse/count-evidence-exports/{Str(export, "exportId")}/file", null, exportOnly,
            "en")).StatusCode);
        Assert.Equal(200, (int)(await SendAsync(c.S, HttpMethod.Get, $"/api/warehouse/count-evidence-exports/{Str(export, "exportId")}/file", null, readOnly,
            "en")).StatusCode);
    }

    // ── the service text family (pack §4.5; the Web parity guard measures the shared keys) ───────────────

    [Fact]
    public void AC43_the_service_text_family_has_one_key_set_in_seven_embedded_languages()
    {
        var folder = Path.Combine(RepoPaths.Root, "services", "Diten.InventoryService", "src", "Diten.InventoryService.Application", "Features", "CycleCount");
        var families = CycleCountEvidenceText.Languages.ToDictionary(l => l,
            l => XDocument.Load(Path.Combine(folder, $"CycleCountEvidenceText_{l}.resx")).Root!.Elements("data")
                .ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")!.Value, StringComparer.Ordinal));
        var en = families["en"];
        Assert.True(en.Count > 80, $"only {en.Count} keys");
        foreach (var (lang, rows) in families)
        {
            Assert.Equal(en.Keys.Order(StringComparer.Ordinal), rows.Keys.Order(StringComparer.Ordinal));
            Assert.All(rows, kv => Assert.False(string.IsNullOrWhiteSpace(kv.Value), $"{lang}: {kv.Key}"));
            // embedded under the base name the reader uses — every key resolves, the file's own value
            Assert.All(rows, kv => Assert.Equal(kv.Value, CycleCountEvidenceText.Find(kv.Key, lang)));
            if (lang != "en") Assert.NotEqual(en["CountPdf.Title"], rows["CountPdf.Title"]);
        }

        Assert.Contains($"{CycleCountEvidenceText.ResourceBase}tr.resources", typeof(CycleCountEvidenceText).Assembly.GetManifestResourceNames());
        Assert.Equal(["en", "tr", "fr", "es", "ru"], CycleCountEvidenceText.PdfLanguages);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────

    private Task<HttpResponseMessage> ExportAsync(Closed c, string format, string language, string? key = null)
    {
        var k = key ?? NewKey();
        return SendAsync(c.S, HttpMethod.Post, $"/api/warehouse/cycle-counts/{c.CountId}/evidence-exports", new { idempotencyKey = k, format }, c.Exporter, language, k);
    }

    private async Task<string> CsvAsync(Closed c, string language)
    {
        var export = await Expect(ExportAsync(c, "Csv", language), 201);
        return Encoding.UTF8.GetString((await FileAsync(c.S, Str(export, "exportId"), c.Exporter)).Bytes);
    }

    private async Task<(byte[] Bytes, string Sha256Header)> FileAsync(CycleCountScenario s, string exportId, string token)
    {
        using var response = await SendAsync(s, HttpMethod.Get, $"/api/warehouse/count-evidence-exports/{exportId}/file", null, token, "en");
        Assert.Equal(200, (int)response.StatusCode);
        return (await response.Content.ReadAsByteArrayAsync(), response.Headers.GetValues("X-Content-SHA256").Single());
    }

    private Task<HttpResponseMessage> SendAsync(CycleCountScenario s, HttpMethod method, string path, object? body, string token, string language,
        string? key = "auto")
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
            var bodyKey = JsonSerializer.SerializeToElement(body).TryGetProperty("idempotencyKey", out var k) ? k.GetString() : null;
            var header = key == "auto" ? bodyKey : key;
            if (header is not null) request.Headers.TryAddWithoutValidation("Idempotency-Key", header);
        }

        if (language.Length > 0) request.Headers.TryAddWithoutValidation("Accept-Language", language);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _ = s;
        return _http.SendAsync(request);
    }

    private static string Str(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    /// <summary>
    /// Whitespace-free, and without the two dashes: MigraDoc writes an all-ANSI run with a WinAnsi font, whose "–" / "—" (0x96 / 0x97) the test's
    /// minimal PDF reader decodes as Latin-1 control characters — a reader limit, not a content difference.
    /// </summary>
    private static string Flat(string text) => Regex.Replace(text, @"[\s–—]", string.Empty);
}
