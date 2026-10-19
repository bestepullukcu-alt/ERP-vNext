using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.InventoryService.Application.Features.Expiry;
using Diten.InventoryService.Infrastructure.Authorization;
using Diten.InventoryService.Tests.TestInfrastructure;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;
using PdfSharp.Pdf.IO;
using Xunit;

namespace Diten.InventoryService.Tests.Expiry;

/// <summary>
/// WP-0176-F5 — the Kanıt Paketi through the real HTTP pipeline (FEFO-BUNDLE 1.2.0; every JSON answer validated against fefo.openapi.yaml):
/// the four preview sections on a seeded period; CSV and PDF exports whose <c>sha256</c> equals an INDEPENDENT SHA-256 of the downloaded
/// file bytes (K3 sabotage (b)); the same idempotency key → the same export, the same bytes, the same hash; the export-log row and the audit
/// row <c>expiry.evidence.exported</c>; the period rules (≤ 366 days); company scope; the PDF prints Turkish characters through its
/// embedded font (K3 sabotage (h)). The class creates lots with past dates nowhere, but posts receipts — it joins
/// <see cref="Trace.LotExpiryCollection"/> like <see cref="ExpiryReadTests"/>, so a foreign sweeper never expires its lots mid-test.
/// </summary>
[Collection(Trace.LotExpiryCollection.Name)]
public sealed class ExpiryEvidenceTests : IClassFixture<ExpiryApiFactory>
{
    private const string BypassReason = "Musteri talebi: uzun raf omurlu lot istendi";
    private const string PolicyReason = "Kalite müdürü kararı: İç denetim sonrası ufuk genişletildi (şirket içi)";

    private readonly ExpiryApiFactory _factory;

    public ExpiryEvidenceTests(ExpiryApiFactory factory) => _factory = factory;

    private DateOnly Today => DateOnly.FromDateTime(_factory.Clock.GetUtcNow().UtcDateTime);

    private static string D(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private string Day(int offset) => D(Today.AddDays(offset)) + "T00:00:00Z";

    private string Now => _factory.Clock.GetUtcNow().AddMinutes(-5).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private sealed record Seeded(ExpiryScenario S, string Exporter, string BypassCase, string ResolvedCase, string SignatureId, string OpenCase);

    /// <summary>
    /// A FEFO bypass (issue from the later lot with a reason → FEFO_BYPASS case), a manual BELOW_MIN_REMAINING case resolved with a
    /// signature, a manual EXPIRED_ON_HAND case left open, and one policy save (approaching 120) with a Turkish reason — all today.
    /// </summary>
    private async Task<Seeded> SeedAsync()
    {
        var s = new ExpiryScenario(_factory);
        var early = await s.LotAsync(Day(40));
        var late = await s.LotAsync(Day(200));
        await s.Lots.PostOkAsync(s.Lots.Receipt("10", early, postingDate: Now));
        await s.Lots.PostOkAsync(s.Lots.Receipt("10", late, postingDate: Now));
        var bypass = s.Lots.Issue("1", late, postingDate: Now);
        bypass["fefoBypassReason"] = BypassReason;
        await s.Lots.PostOkAsync(bypass);
        var bypassCase = Assert.Single(s.Persisted("expiry_exceptions"), d => d["Type"].AsString == "FefoBypass")["_id"].AsString;

        var resolved = await s.OpenAsync(early, "BELOW_MIN_REMAINING");
        var resolvedId = resolved.GetProperty("exceptionId").GetString()!;
        var detail = await s.ResolveSignedAsync(resolvedId, "AcceptedWithReason", ExpiryScenario.ETag(resolved));
        var signatureId = detail.GetProperty("resolutionSignatureId").GetString()!;
        var open = (await s.OpenAsync(late, "EXPIRED_ON_HAND")).GetProperty("exceptionId").GetString()!;

        var (status, saved, _) = await s.PutPolicyAsync(ExpiryScenario.Policy(approaching: 120, reason: PolicyReason), "\"0\"");
        Assert.True(status == 200, saved.ToString());
        return new Seeded(s, s.Mint(s.Admin, [ExpiryPermissions.EvidenceExport]), bypassCase, resolvedId, signatureId, open);
    }

    private static Dictionary<string, object?> Export(string format, string from, string to, string? key = null, string[]? types = null)
    {
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = key ?? ExpiryScenario.Key("ev"), ["from"] = from, ["to"] = to, ["format"] = format };
        if (types is not null) body["types"] = types;
        return body;
    }

    private static async Task<(int Status, JsonElement Body)> PostExportAsync(ExpiryScenario s, string token, Dictionary<string, object?> body, string? language = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/expiry/evidence/exports") { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", (string)body["idempotencyKey"]!);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (language is not null) request.Headers.AcceptLanguage.ParseAdd(language);
        using var response = await s.Client.SendAsync(request);
        return ((int)response.StatusCode, JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone());
    }

    private static async Task<(int Status, byte[] Bytes, string? Type)> FileAsync(ExpiryScenario s, string token, string exportId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/expiry/evidence/exports/{exportId}/file");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await s.Client.SendAsync(request);
        return ((int)response.StatusCode, await response.Content.ReadAsByteArrayAsync(), response.Content.Headers.ContentType?.MediaType);
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    [Fact]
    public async Task The_preview_shows_the_four_sections_of_the_period()
    {
        var w = await SeedAsync();
        var preview = await w.S.ExpectAsync(HttpMethod.Get, $"/api/expiry/evidence/preview?from={D(Today.AddDays(-1))}&to={D(Today)}", w.Exporter, null, 200);

        Assert.Equal(D(Today), preview.GetProperty("period").GetProperty("to").GetString());
        var summary = preview.GetProperty("summary");
        Assert.Equal(1, summary.GetProperty("fefoIssues").GetInt32());
        Assert.Equal(1, summary.GetProperty("fefoBypasses").GetInt32());
        Assert.Equal(2, summary.GetProperty("openExceptions").GetInt32()); // the bypass case + the EXPIRED_ON_HAND case

        var exceptions = preview.GetProperty("exceptions").EnumerateArray().ToList();
        Assert.Equal(3, exceptions.Count);
        var resolved = Assert.Single(exceptions, e => e.GetProperty("exceptionId").GetString() == w.ResolvedCase);
        Assert.Equal("AcceptedWithReason", resolved.GetProperty("resolution").GetString());
        Assert.Equal(w.SignatureId, resolved.GetProperty("resolutionSignatureId").GetString());
        Assert.Equal(ExpiryScenario.Reason, resolved.GetProperty("resolutionReason").GetString());

        var policy = preview.GetProperty("policyChanges").EnumerateArray().ToList();
        var horizon = Assert.Single(policy, p => p.GetProperty("field").GetString() == "approachingDays");
        Assert.Equal("120", horizon.GetProperty("newValue").GetString());
        Assert.Equal(PolicyReason, horizon.GetProperty("reason").GetString());

        Assert.Equal(w.BypassCase, Assert.Single(preview.GetProperty("fefoBypasses").EnumerateArray()).GetProperty("exceptionId").GetString());

        // the types filter narrows the exception section only; the bypass section is its own
        var onlyOnHand = await w.S.ExpectAsync(HttpMethod.Get,
            $"/api/expiry/evidence/preview?from={D(Today)}&to={D(Today)}&types=EXPIRED_ON_HAND", w.Exporter, null, 200);
        Assert.Equal(w.OpenCase, Assert.Single(onlyOnHand.GetProperty("exceptions").EnumerateArray()).GetProperty("exceptionId").GetString());
        Assert.Single(onlyOnHand.GetProperty("fefoBypasses").EnumerateArray());

        // presets: d30 / q / ytd all end today and contain today's cases
        foreach (var preset in new[] { "d30", "q", "ytd" })
        {
            var p = await w.S.ExpectAsync(HttpMethod.Get, $"/api/expiry/evidence/preview?preset={preset}", w.Exporter, null, 200);
            Assert.Equal(D(Today), p.GetProperty("period").GetProperty("to").GetString());
            Assert.Equal(3, p.GetProperty("exceptions").GetArrayLength());
        }
    }

    [Fact]
    public async Task The_period_rules_hold_and_the_company_sees_only_its_own()
    {
        var w = await SeedAsync();
        string[] refused =
        [
            $"from={D(Today.AddDays(-366))}&to={D(Today)}",            // 367 days
            $"from={D(Today)}&to={D(Today.AddDays(1))}",               // ends tomorrow
            $"from={D(Today)}&to={D(Today.AddDays(-1))}",              // to before from
            $"preset=q&from={D(Today)}",                               // preset + dates
            "preset=week",
            $"from={D(Today)}",                                        // custom without to
            $"from={D(Today)}&to={D(Today)}&warehouseIds=not-a-uuid",
            $"from={D(Today)}&to={D(Today)}&types=SOMETHING"
        ];
        foreach (var query in refused)
        {
            var (status, body, _) = await w.S.SendAsync(HttpMethod.Get, "/api/expiry/evidence/preview?" + query, w.Exporter);
            Assert.True(status == 400, $"{query}: {status} {body}");
        }

        Assert.Equal(200, (await w.S.SendAsync(HttpMethod.Get, $"/api/expiry/evidence/preview?from={D(Today.AddDays(-365))}&to={D(Today)}", w.Exporter)).Status); // 366
        var (badFormat, _) = await PostExportAsync(w.S, w.Exporter, Export("Xml", D(Today), D(Today)));
        Assert.Equal(400, badFormat);

        // another company of the tenant: an empty period, and its file is 403; another tenant / unknown id → 404
        var otherCompany = w.S.Mint(w.S.Admin, [ExpiryPermissions.EvidenceExport], Guid.NewGuid());
        var foreign = await w.S.ExpectAsync(HttpMethod.Get, $"/api/expiry/evidence/preview?from={D(Today)}&to={D(Today)}", otherCompany, null, 200);
        Assert.Equal(0, foreign.GetProperty("exceptions").GetArrayLength());
        Assert.Equal(0, foreign.GetProperty("policyChanges").GetArrayLength());
        Assert.Equal(0, foreign.GetProperty("summary").GetProperty("fefoIssues").GetInt32());

        var (created, export) = await PostExportAsync(w.S, w.Exporter, Export("Csv", D(Today), D(Today)));
        Assert.Equal(201, created);
        var exportId = export.GetProperty("exportId").GetString()!;
        Assert.Equal(403, (await FileAsync(w.S, otherCompany, exportId)).Status);
        var unknown = await w.S.SendAsync(HttpMethod.Get, $"/api/expiry/evidence/exports/{Guid.NewGuid()}/file", w.Exporter);
        Assert.Equal(404, unknown.Status);
        Assert.Equal("EXPIRY_EVIDENCE_EXPORT_NOT_FOUND", ExpiryScenario.Code(unknown.Body));
        Assert.Equal(0, (await w.S.ExpectAsync(HttpMethod.Get, "/api/expiry/evidence/exports", otherCompany, null, 200)).GetProperty("total").GetInt32());

        // the key: expiry.evidence.export for every evidence operation
        var reader = w.S.Mint(w.S.Admin, [ExpiryPermissions.PoliciesRead, ExpiryPermissions.ExceptionsRead, ExpiryPermissions.DashboardRead]);
        Assert.Equal(403, (await w.S.SendAsync(HttpMethod.Get, $"/api/expiry/evidence/preview?from={D(Today)}&to={D(Today)}", reader)).Status);
        Assert.Equal(403, (await w.S.SendAsync(HttpMethod.Get, "/api/expiry/evidence/exports", reader)).Status);
        Assert.Equal(403, (await PostExportAsync(w.S, reader, Export("Csv", D(Today), D(Today)))).Status);
        Assert.Equal(403, (await FileAsync(w.S, reader, exportId)).Status);
    }

    [Fact]
    public async Task A_CSV_export_hashes_the_file_bytes_and_a_replay_answers_the_same_file()
    {
        var w = await SeedAsync();
        var body = Export("Csv", D(Today), D(Today));
        var (status, export) = await PostExportAsync(w.S, w.Exporter, body);
        Assert.Equal(201, status);
        var exportId = export.GetProperty("exportId").GetString()!;
        Assert.Equal("Csv", export.GetProperty("format").GetString());

        var (fileStatus, bytes, type) = await FileAsync(w.S, w.Exporter, exportId);
        Assert.Equal(200, fileStatus);
        Assert.Equal("text/csv", type);
        // K3 sabotage (b): the hash of the parameters instead of the file → this independent SHA-256 of the downloaded bytes differs
        Assert.Equal(Sha256(bytes), export.GetProperty("sha256").GetString());
        Assert.Matches("^[0-9a-f]{64}$", export.GetProperty("sha256").GetString()!);

        var text = Encoding.UTF8.GetString(bytes);
        // the nine columns where they were; WP-MVP1-FIX-LIVE2 appends the names / worded values after them
        Assert.StartsWith("﻿istisna_no;tur;urun;lot;depo;acilis;durum;cozum;imza;urun_adi;sku_kodu;depo_adi;tur_adi;durum_adi;cozum_adi;imzalayan;imzalayan_kimlik\r\n",
            text, StringComparison.Ordinal);
        var lines = text.TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length - 1);
        Assert.Equal(lines.Length - 1, export.GetProperty("rowCount").GetInt32());
        Assert.Contains(lines, l => l.Contains(w.SignatureId, StringComparison.Ordinal) && l.Contains(";AcceptedWithReason;", StringComparison.Ordinal));

        // the same key + body → 200, the same export, the same bytes, the same hash; another body → 400
        var (replayStatus, replay) = await PostExportAsync(w.S, w.Exporter, body);
        Assert.Equal(200, replayStatus);
        Assert.Equal(exportId, replay.GetProperty("exportId").GetString());
        Assert.Equal(export.GetProperty("sha256").GetString(), replay.GetProperty("sha256").GetString());
        Assert.Equal(bytes, (await FileAsync(w.S, w.Exporter, exportId)).Bytes);
        var other = Export("Pdf", D(Today), D(Today), (string)body["idempotencyKey"]!);
        var (reusedStatus, reused) = await PostExportAsync(w.S, w.Exporter, other);
        Assert.Equal(400, reusedStatus);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", ExpiryScenario.Code(reused));

        // one export-log row (newest first) and one audit row carrying the same hash
        var log = await w.S.ExpectAsync(HttpMethod.Get, "/api/expiry/evidence/exports", w.Exporter, null, 200);
        Assert.Equal(1, log.GetProperty("total").GetInt32());
        Assert.Equal(exportId, log.GetProperty("items")[0].GetProperty("exportId").GetString());
        var audit = Assert.Single(w.S.Audit("expiry.evidence.exported"));
        Assert.Equal(export.GetProperty("sha256").GetString(), audit["Detail"]["sha256"].AsString);
        Assert.Equal(export.GetProperty("fileName").GetString(), audit["Detail"]["fileName"].AsString);
    }

    [Fact]
    public async Task A_PDF_export_embeds_its_font_prints_Turkish_and_hashes_the_file_bytes()
    {
        var w = await SeedAsync();
        var (status, export) = await PostExportAsync(w.S, w.Exporter, Export("Pdf", D(Today.AddDays(-1)), D(Today)), "tr-TR");
        Assert.Equal(201, status);
        var (fileStatus, bytes, type) = await FileAsync(w.S, w.Exporter, export.GetProperty("exportId").GetString()!);
        Assert.Equal(200, fileStatus);
        Assert.Equal("application/pdf", type);
        Assert.Equal(Sha256(bytes), export.GetProperty("sha256").GetString());
        Assert.Equal(3, export.GetProperty("rowCount").GetInt32());

        var pdf = PdfText.Read(bytes);
        // PDFsharp spaces words by glyph positioning, not by space glyphs: the extracted text is compared without whitespace
        var text = Regex.Replace(pdf.Text, @"\s", string.Empty);
        // K3 sabotage (h): without the embedded font the Turkish letters do not survive (or the render fails)
        Assert.True(pdf.EmbeddedFonts > 0, "the PDF embeds no font file");
        foreach (var word in new[] { "Kanıt Paketi", "Şirket", "Değiştiren", "İmza kimliği", "Dönem içi istisnalar ve çözümleri","Gerekçeyle kabul", "İç denetim", "genişletildi" })
        {
            Assert.Contains(word.Replace(" ", string.Empty, StringComparison.Ordinal), text, StringComparison.Ordinal);
        }

        Assert.Contains(w.SignatureId, text, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"Sayfa1/\d+"), text);

        // the reader's language: English labels when asked
        var (_, english) = await PostExportAsync(w.S, w.Exporter, Export("Pdf", D(Today), D(Today)), "en-US");
        var englishText = PdfText.Read((await FileAsync(w.S, w.Exporter, english.GetProperty("exportId").GetString()!)).Bytes).Text;
        Assert.Contains("EvidencePack", Regex.Replace(englishText, @"\s", string.Empty), StringComparison.Ordinal);
    }

    // ── WP-MVP1-FIX-LIVE2 (FU-0176-EV-NAMES, CT decision "yazı tipi A") ─────────────────────────────────────

    private static string Letters(string text) => Regex.Replace(text, @"[^\p{L}\p{N}]", string.Empty);

    private static string Flat(string text) => Regex.Replace(text, @"\s", string.Empty);

    private async Task<string> ExportTextAsync(Seeded w, string token, string format, string language)
    {
        var (status, export) = await PostExportAsync(w.S, token, Export(format, D(Today.AddDays(-1)), D(Today)), language);
        Assert.True(status == 201, export.ToString());
        var (fileStatus, bytes, _) = await FileAsync(w.S, token, export.GetProperty("exportId").GetString()!);
        Assert.Equal(200, fileStatus);
        Assert.Equal(Sha256(bytes), export.GetProperty("sha256").GetString());   // still the hash of THE delivered bytes
        return format == "Pdf" ? PdfText.Read(bytes).Text : Encoding.UTF8.GetString(bytes);
    }

    [Fact]
    public async Task The_FEFO_pack_prints_names_beside_the_ids_and_never_invents_an_unreadable_one()
    {
        var w = await SeedAsync();
        // the caller reads the signatures with their own token (MOD-0022 esign.signatures.read), as on the Kanıt Paketi page
        var token = w.S.Mint(w.S.Admin, [ExpiryPermissions.EvidenceExport, "esign.signatures.read"]);

        // nothing known yet: company / product / SKU / signer / policy user are the id + "(okunamadı)" / "(ad okunamadı)"
        var bare = Flat(await ExportTextAsync(w, token, "Pdf", "tr"));
        Assert.Contains(Flat($"{w.S.Ledger.LegalEntity:D} (okunamadı)"), bare, StringComparison.Ordinal);
        Assert.Contains(Flat($"{w.S.Admin:D} (ad okunamadı)"), bare, StringComparison.Ordinal);       // sabotage (b): an invented name → red
        Assert.Contains(Flat($"{w.S.Ledger.Item:D} (okunamadı)"), bare, StringComparison.Ordinal);

        // what the masters / MOD-0022 / the central audit queue recorded
        _factory.LegalEntities.RegisterName(w.S.Ledger.LegalEntity, "GM-EU", "Grand Medical EU");
        _factory.Products.SetDisplay(w.S.Ledger.Item, "GP-0099", "Parasetamol 500 mg");
        _factory.Products.SetDisplay(w.S.Ledger.Sku, "LS-0099");
        var signature = _factory.ESign.Sign(w.S.Tenant, w.S.Qa, w.ResolvedCase, "APPROVE", new string('c', 64), "expiry", "expiry.exception",
            "expiry.exception.resolve", DateTimeOffset.UtcNow, w.S.Ledger.LegalEntity, signerName: "Ayşe Demir");
        await _factory.Database.GetCollection<MongoDB.Bson.BsonDocument>("expiry_exceptions").UpdateOneAsync(
            new MongoDB.Bson.BsonDocument("_id", w.ResolvedCase), new MongoDB.Bson.BsonDocument("$set", new MongoDB.Bson.BsonDocument("ResolutionSignatureId", signature)));
        var outbox = Diten.InventoryService.Tests.CentralAudit.CentralAuditInventoryTests.Outbox(_factory);
        await outbox.UpdateManyAsync(MongoDB.Driver.Builders<Diten.BuildingBlocks.CentralAudit.CentralAuditOutboxRecord>.Filter.Where(x => x.TenantId == w.S.Tenant && x.ActorId == w.S.Admin),
            MongoDB.Driver.Builders<Diten.BuildingBlocks.CentralAudit.CentralAuditOutboxRecord>.Update.Set(x => x.ActorDisplayName, "Zeynep Aksoy"));

        var pdf = Flat(await ExportTextAsync(w, token, "Pdf", "tr"));
        Assert.Contains("GM-EU·GrandMedicalEU", pdf, StringComparison.Ordinal);                               // header / footer company
        Assert.Contains("GP-0099·Parasetamol500mg", pdf, StringComparison.Ordinal);                           // product
        Assert.Contains("AyşeDemir·" + signature, pdf, StringComparison.Ordinal);                             // signer at signing + the id
        Assert.Contains("ZeynepAksoy", pdf, StringComparison.Ordinal);                                        // the policy change's user (audit queue)
        Assert.Contains(Flat(w.S.Ledger.WarehouseNode.Code + " · " + w.S.Ledger.WarehouseNode.Name), pdf, StringComparison.Ordinal);
        Assert.Contains("Gerekçeylekabul", pdf, StringComparison.Ordinal);                                    // values worded
        Assert.Contains(Flat(ExpiryEvidenceText.Of("Expiry.Policy.Field.approachingDays", "tr")), pdf, StringComparison.Ordinal);
        Assert.DoesNotContain(Flat($"{w.S.Admin:D} (ad okunamadı)"), pdf, StringComparison.Ordinal);

        // the CSV keeps its nine columns (ids, codes) and carries the names / worded values after them, in the asked language
        var csv = await ExportTextAsync(w, token, "Csv", "en");
        var line = Assert.Single(csv.Split("\r\n"), l => l.Contains(signature, StringComparison.Ordinal));
        var cells = line.Split(';');
        Assert.Equal(17, cells.Length);
        Assert.Equal(("BELOW_MIN_REMAINING", w.S.Ledger.Item.ToString("D"), "AcceptedWithReason", signature), (cells[1], cells[2], cells[7], cells[8]));
        Assert.Equal(("GP-0099 · Parasetamol 500 mg", "LS-0099"), (cells[9], cells[10]));
        Assert.Equal((ExpiryEvidenceText.Of("Expiry.Resolution.AcceptedWithReason", "en"), "Ayşe Demir", w.S.Qa.ToString("D")), (cells[14], cells[15], cells[16]));
        Assert.Equal(ExpiryEvidenceText.Of("Expiry.Type.BELOW_MIN_REMAINING", "en"), cells[12]);

        // FU-0176-EV-NAMES: read paths only — no Expiry entity carries a name of its own
        foreach (var type in new[] { typeof(Diten.InventoryService.Domain.Expiry.Entities.FefoExceptionCase), typeof(Diten.InventoryService.Domain.Expiry.Entities.ExpiryPolicyHistoryEntry) })
            Assert.DoesNotContain(type.GetProperties(), p => p.Name.EndsWith("Name", StringComparison.Ordinal) || p.Name.Contains("NameAt", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Every_language_has_a_FEFO_PDF_and_zh_or_ar_is_English_with_the_note()
    {
        var w = await SeedAsync();
        foreach (var lang in ExpiryEvidenceText.Languages)
        {
            // letters and digits only: the minimal test reader extracts the title's em dash differently (the font has U+2014 — measured)
            var pdf = Letters(await ExportTextAsync(w, w.Exporter, "Pdf", lang));
            if (ExpiryEvidenceText.PdfLanguages.Contains(lang))
            {
                Assert.Contains(Letters(ExpiryEvidenceText.Of("ExpiryPdf.Title", lang)), pdf, StringComparison.Ordinal);
                Assert.Contains(Letters(ExpiryEvidenceText.Of("Expiry.Status.Open", lang)), pdf, StringComparison.Ordinal);
                Assert.DoesNotContain(Letters(ExpiryEvidenceText.Of("ExpiryPdf.EnglishFallback", "en")), pdf, StringComparison.Ordinal);
            }
            else
            {
                Assert.Contains(Letters(ExpiryEvidenceText.Of("ExpiryPdf.Title", "en")), pdf, StringComparison.Ordinal);
                Assert.Contains(Letters(ExpiryEvidenceText.Of("ExpiryPdf.EnglishFallback", "en")), pdf, StringComparison.Ordinal);
                Assert.Contains($"Requestedlanguage{lang}", pdf, StringComparison.Ordinal);
            }

            var csv = await ExportTextAsync(w, w.Exporter, "Csv", lang);
            Assert.Contains(";" + ExpiryEvidenceText.Of("Expiry.Status.Open", lang) + ";", csv, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A minimal, independent PDF text reader for the test: the content stream's Tj / TJ strings of each Tf font, mapped through that
    /// font's ToUnicode CMap (bfchar + bfrange) — what a PDF viewer's "copy text" gives. Counts the embedded font files (FontFile2 / FontFile3).
    /// </summary>
    internal static class PdfText
    {
        public sealed record Result(string Text, int EmbeddedFonts);

        public static Result Read(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes);
            var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
            var text = new StringBuilder();
            var embedded = new HashSet<string>(StringComparer.Ordinal);
            foreach (var page in document.Pages)
            {
                var fonts = new Dictionary<string, Font>(StringComparer.Ordinal);
                var resources = page.Resources.Elements.GetDictionary("/Font");
                if (resources is not null)
                {
                    foreach (var name in resources.Elements.Keys)
                    {
                        var font = resources.Elements.GetDictionary(name)!;
                        // PDFsharp writes an all-ANSI run with a single-byte (WinAnsi) TrueType font and any other run with a Type0 / Identity-H font
                        fonts[name] = new Font(font.Elements.GetName("/Subtype") == "/Type0", CMap(font));
                        var owners = (font.Elements.GetArray("/DescendantFonts")?.Elements.Select(e => (e as PdfReference)?.Value as PdfDictionary) ?? []).Append(font);
                        foreach (var owner in owners)
                        {
                            var descriptor = owner?.Elements.GetDictionary("/FontDescriptor");
                            if (descriptor is not null && (descriptor.Elements.ContainsKey("/FontFile2") || descriptor.Elements.ContainsKey("/FontFile3")))
                                embedded.Add(descriptor.Elements.GetName("/FontName"));
                        }
                    }
                }

                var current = new Font(false, []);
                Walk(ContentReader.ReadContent(page), fonts, ref current, text);
                text.Append('\n');
            }

            return new Result(text.ToString(), embedded.Count);
        }

        private sealed record Font(bool TwoByte, Dictionary<int, string> Map);

        private static void Walk(CSequence sequence, Dictionary<string, Font> fonts, ref Font current, StringBuilder text)
        {
            foreach (var item in sequence)
            {
                if (item is not COperator op) continue;
                switch (op.OpCode.Name)
                {
                    case "Tf" when op.Operands.Count > 0 && op.Operands[0] is CName name:
                        current = fonts.GetValueOrDefault(name.Name) ?? new Font(false, []);
                        break;
                    case "Tj" or "'" or "\"":
                        foreach (var s in op.Operands.OfType<CString>()) Decode(s, current, text);
                        break;
                    case "TJ":
                        foreach (var a in op.Operands.OfType<CArray>())
                            foreach (var s in a.OfType<CString>()) Decode(s, current, text);
                        break;
                    case "ET":
                        text.Append(' ');
                        break;
                }
            }
        }

        private static void Decode(CString s, Font font, StringBuilder text)
        {
            var raw = s.Value;
            if (!font.TwoByte)
            {
                // a simple font: one byte per character (WinAnsi; ASCII and Latin-1 agree on everything PDFsharp puts here)
                foreach (var c in raw) text.Append(font.Map.GetValueOrDefault(c, c.ToString()));
                return;
            }

            for (var i = 0; i + 1 < raw.Length; i += 2)
            {
                var glyph = (raw[i] << 8) | raw[i + 1];
                text.Append(font.Map.GetValueOrDefault(glyph, "�"));
            }
        }

        private static Dictionary<int, string> CMap(PdfDictionary font)
        {
            var map = new Dictionary<int, string>();
            var toUnicode = (font.Elements["/ToUnicode"] as PdfReference)?.Value as PdfDictionary;
            if (toUnicode?.Stream is null) return map;
            var cmap = Encoding.ASCII.GetString(toUnicode.Stream.UnfilteredValue);
            foreach (Match block in Regex.Matches(cmap, @"beginbfchar(?<body>.*?)endbfchar", RegexOptions.Singleline))
                foreach (Match m in Regex.Matches(block.Groups["body"].Value, @"<(?<src>[0-9A-Fa-f]+)>\s*<(?<dst>[0-9A-Fa-f]+)>"))
                    map[Convert.ToInt32(m.Groups["src"].Value, 16)] = Utf16(m.Groups["dst"].Value);
            foreach (Match block in Regex.Matches(cmap, @"beginbfrange(?<body>.*?)endbfrange", RegexOptions.Singleline))
                foreach (Match m in Regex.Matches(block.Groups["body"].Value, @"<(?<lo>[0-9A-Fa-f]+)>\s*<(?<hi>[0-9A-Fa-f]+)>\s*<(?<dst>[0-9A-Fa-f]+)>"))
                {
                    var lo = Convert.ToInt32(m.Groups["lo"].Value, 16);
                    var hi = Convert.ToInt32(m.Groups["hi"].Value, 16);
                    var dst = Convert.ToInt32(m.Groups["dst"].Value, 16);
                    for (var g = lo; g <= hi; g++) map[g] = char.ConvertFromUtf32(dst + g - lo);
                }

            return map;
        }

        private static string Utf16(string hex)
        {
            var units = Enumerable.Range(0, hex.Length / 4).Select(i => (char)Convert.ToInt32(hex.Substring(i * 4, 4), 16)).ToArray();
            return new string(units);
        }
    }
}
