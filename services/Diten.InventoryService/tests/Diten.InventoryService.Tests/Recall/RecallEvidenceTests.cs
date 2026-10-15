using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Features.Recall.Evidence;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Persistence.Recall;
using Diten.InventoryService.Tests.CentralAudit;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>Shared calls of the Kanıt Paketi tests (RECALL 1.7.0 evidence operations, through the contract-validating client).</summary>
internal static class RecallEvidence
{
    public static readonly string[] Keys = ["recall.evidence.export", "recall.cases.read", "esign.signatures.read"];

    public static Dictionary<string, object?> Body(string format, string? caseId = null, string? runId = null, string? from = null, string? to = null, string? key = null)
    {
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = key ?? RecallScenario.Key("ev"), ["format"] = format };
        if (caseId is not null) body["caseId"] = caseId;
        if (runId is not null) body["runId"] = runId;
        if (from is not null) body["from"] = from;
        if (to is not null) body["to"] = to;
        return body;
    }

    public static Task<(int Status, JsonElement Body, HttpResponseMessage Raw)> ExportAsync(RecallScenario s, string token, Dictionary<string, object?> body)
        => s.SendAsync(HttpMethod.Post, "/api/recall/evidence/exports", token, body, (string)body["idempotencyKey"]!);

    /// <summary>The downloaded bytes, the media type and the status — read raw (the file is not JSON).</summary>
    public static async Task<(int Status, byte[] Bytes, string? Media)> DownloadAsync(RecallScenario s, string token, string exportId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/recall/evidence/exports/{exportId}/file");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await s.Client.SendAsync(request);
        return ((int)response.StatusCode, await response.Content.ReadAsByteArrayAsync(), response.Content.Headers.ContentType?.MediaType);
    }

    public static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public static JsonElement Section(JsonElement preview, string key) => preview.GetProperty("sections").GetProperty(key);

    public static List<List<string>> Rows(JsonElement section, int table = 0)
        => section.GetProperty("tables")[table].GetProperty("rows").EnumerateArray().Select(r => r.EnumerateArray().Select(c => c.GetString()!).ToList()).ToList();
}

/// <summary>
/// WP-0177-R7b — the Kanıt Paketi of a case (pack §4.13, RECALL 1.7.0) on the real host: the seven sections from the records; the PDF / CSV
/// carries the SHA-256 of THE PRODUCED BYTES (D-12) — the downloaded file hashes to the logged value, a replay of the key returns the same file and
/// hash; the export is logged, audited locally and declared to the central audit (BL-501) in its transaction; section 7 reads each signature
/// from MOD-0022 and an unreadable one says so with nothing filled in; another company's case can be neither previewed nor exported.
/// </summary>
public sealed class RecallEvidenceTests : IClassFixture<RecallApiFactory>
{
    private readonly RecallApiFactory _factory;

    public RecallEvidenceTests(RecallApiFactory factory) => _factory = factory;

    private static string Str(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    private async Task<(RecallTraceWorld W, string CaseId, string Token)> ClosedCaseAsync()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        var (caseId, _) = await RecallR3.ApproveAsync(w.S, w.Fg);
        await RecallR3.CloseAsync(w.S, caseId);
        return (w, caseId, w.S.Mint(w.S.Qa1, RecallEvidence.Keys));
    }

    [Fact]
    public async Task A_closed_case_exports_PDF_and_CSV_whose_downloaded_bytes_hash_to_the_logged_SHA_256_and_a_replay_returns_the_same_file()
    {
        var (w, caseId, token) = await ClosedCaseAsync();
        var outboxBefore = CentralAuditInventoryTests.Outbox(_factory).CountDocuments(x => x.TenantId == w.S.Tenant);

        foreach (var (format, media) in new[] { ("Pdf", "application/pdf"), ("Csv", "text/csv") })
        {
            var key = RecallScenario.Key("ev");
            var (status, export, _) = await RecallEvidence.ExportAsync(w.S, token, RecallEvidence.Body(format, caseId, key: key));
            Assert.True(status == 201, export.ToString());
            Assert.Equal((format, "Case", caseId), (Str(export, "format"), Str(export.GetProperty("subject"), "kind"), Str(export.GetProperty("subject"), "caseId")));
            Assert.True(RecallEvidence.Section(await PreviewAsync(w.S, token, caseId), "1_summary").GetProperty("applicable").GetBoolean());
            Assert.Equal(7, export.GetProperty("sections").GetArrayLength());

            var (fs, bytes, got) = await RecallEvidence.DownloadAsync(w.S, token, Str(export, "exportId"));
            Assert.Equal((200, media), (fs, got));
            Assert.Equal(Str(export, "sha256"), RecallEvidence.Sha256(bytes));            // THE produced bytes (D-12), not the parameters
            Assert.Equal(export.GetProperty("sizeBytes").GetInt64(), bytes.LongLength);
            if (format == "Csv")
            {
                var text = Encoding.UTF8.GetString(bytes);
                Assert.StartsWith("﻿" + RecallEvidenceLabels.Of("title", "en") + ";" + Str(await w.S.GetAsync(caseId), "number"), text, StringComparison.Ordinal);
                Assert.All(RecallEvidenceSections.All, k => Assert.Contains("\r\n" + RecallEvidenceLabels.Of(k, "en") + "\r\n", text, StringComparison.Ordinal));
            }

            // the same key + body → the same export (200), the same bytes and hash — never a re-render
            var (rs, replay, _) = await RecallEvidence.ExportAsync(w.S, token, RecallEvidence.Body(format, caseId, key: key));
            Assert.Equal((200, Str(export, "exportId"), Str(export, "sha256")), (rs, Str(replay, "exportId"), Str(replay, "sha256")));
            var (_, again, _) = await RecallEvidence.DownloadAsync(w.S, token, Str(export, "exportId"));
            Assert.Equal(bytes, again);
            var (os, other, _) = await RecallEvidence.ExportAsync(w.S, token, RecallEvidence.Body(format == "Pdf" ? "Csv" : "Pdf", caseId, key: key));
            Assert.Equal((400, "IDEMPOTENCY_KEY_REUSED"), (os, RecallScenario.Code(other)));
        }

        // the export log (newest first), the local audit, the central audit declared in the command transaction
        var log = await w.S.ExpectAsync(HttpMethod.Get, "/api/recall/evidence/exports", token, null, 200);
        Assert.Equal(2, log.GetProperty("total").GetInt64());
        Assert.Equal(["Csv", "Pdf"], log.GetProperty("items").EnumerateArray().Select(i => Str(i, "format")).ToList());
        Assert.All(log.GetProperty("items").EnumerateArray(), i => Assert.Equal(w.S.Qa1.ToString("D"), Str(i.GetProperty("exportedBy"), "userId")));
        Assert.Equal(2, w.S.Audit("recall.evidence.exported").Count);
        Assert.Equal(2, w.S.Persisted(RecallCollections.EvidenceExports).Count);
        Assert.Equal(2, w.S.Persisted(RecallCollections.EvidenceFiles).Count);
        var central = CentralAuditInventoryTests.Outbox(_factory).Find(x => x.TenantId == w.S.Tenant && x.RequestType == "CreateRecallEvidenceExportCommand").ToList();
        Assert.Equal(2, central.Count);
        Assert.All(central, r => Assert.True(r.WrittenInCommandTransaction));
        Assert.All(central, r => Assert.Equal("RecallEvidenceExport", r.EntityType));
        Assert.Equal(log.GetProperty("items").EnumerateArray().Select(i => Guid.Parse(Str(i, "exportId"))).OrderBy(g => g),
            central.Select(r => r.EntityId!.Value).OrderBy(g => g));
        Assert.Equal(outboxBefore + 2, CentralAuditInventoryTests.Outbox(_factory).CountDocuments(x => x.TenantId == w.S.Tenant));
    }

    private static Task<JsonElement> PreviewAsync(RecallScenario s, string token, string caseId)
        => s.ExpectAsync(HttpMethod.Get, $"/api/recall/evidence/preview?caseId={caseId}", token, null, 200);

    [Fact]
    public async Task The_preview_fills_the_seven_sections_from_the_records_and_reads_every_signature_from_MOD_0022()
    {
        var (w, caseId, token) = await ClosedCaseAsync();
        var preview = await PreviewAsync(w.S, token, caseId);
        var keys = preview.GetProperty("sections").EnumerateObject().Select(p => p.Name).ToList();
        Assert.Equal(["1_summary", "2_trace", "3_quarantine", "4_customers", "5_returnsAndEffectiveness", "6_timeline", "7_signatures"], keys);
        Assert.All(keys, k => Assert.True(RecallEvidence.Section(preview, k).GetProperty("applicable").GetBoolean(), k));
        Assert.All(keys, k => Assert.Contains(RecallEvidence.Section(preview, k).GetProperty("tables").EnumerateArray(), t => t.GetProperty("rows").GetArrayLength() > 0));

        var summary = RecallEvidence.Rows(RecallEvidence.Section(preview, "1_summary")).ToDictionary(r => r[0], r => r[1]);
        var detail = await w.S.GetAsync(caseId);
        Assert.Equal((Str(detail, "number"), "Closed"), (summary["number"], summary["status"]));
        Assert.Contains(RecallEvidence.Rows(RecallEvidence.Section(preview, "2_trace")), r => (r[0], r[2], r[3]) == ("Shipment", ShipmentBundleDouble.ShipTo(w.Shipment), "4"));
        Assert.Contains(RecallEvidence.Rows(RecallEvidence.Section(preview, "4_customers")), r => r.Contains(ShipmentBundleDouble.ShipTo(w.Shipment)));

        // section 7: the test signature gate issues ids MOD-0022 does not know → every row "Unreadable:<outcome>", signer / meaning / time EMPTY
        var signatures = RecallEvidence.Rows(RecallEvidence.Section(preview, "7_signatures"));
        Assert.True(signatures.Count >= 3, string.Join(" | ", signatures.Select(r => string.Join(",", r))));
        Assert.All(signatures, r => Assert.StartsWith("Unreadable:", r[5]));
        Assert.All(signatures, r => Assert.Equal(("", "", ""), (r[2], r[3], r[4])));

        // a signature MOD-0022 does know is read as recorded (signer, meaning, time) — never copied from the case
        var signedAt = new DateTimeOffset(2026, 10, 14, 9, 30, 0, TimeSpan.Zero);
        var real = _factory.ESign.Sign(w.S.Tenant, w.S.Qa2, caseId, "APPROVE", new string('a', 64), "recall", RecallSignableActions.CaseSubjectType,
            "recall.case.close", signedAt, w.S.Ledger.LegalEntity);
        await _factory.Database.GetCollection<BsonDocument>(RecallCollections.Cases)
            .UpdateOneAsync(new BsonDocument("_id", caseId), new BsonDocument("$set", new BsonDocument("ClosureSignatureId", real)));
        var reread = RecallEvidence.Rows(RecallEvidence.Section(await PreviewAsync(w.S, token, caseId), "7_signatures"));
        var row = Assert.Single(reread, r => r[1] == real);
        Assert.Equal((w.S.Qa2.ToString("D"), "APPROVE", "Ok"), (row[2], row[3], row[5]));
        Assert.StartsWith("2026-10-14T09:30:00", row[4]);

        // MOD-0022 itself down → that row too is unreadable, still nothing invented
        _factory.ESign.Down = true;
        try
        {
            var down = RecallEvidence.Rows(RecallEvidence.Section(await PreviewAsync(w.S, token, caseId), "7_signatures"));
            var unreadable = Assert.Single(down, r => r[1] == real);
            Assert.Equal(("", "", ""), (unreadable[2], unreadable[3], unreadable[4]));
            Assert.StartsWith("Unreadable:", unreadable[5]);
        }
        finally
        {
            _factory.ESign.Down = false;
        }
    }

    [Fact]
    public async Task Another_company_cannot_preview_export_or_download_the_case_evidence_and_the_requests_are_checked()
    {
        var (w, caseId, token) = await ClosedCaseAsync();
        var (_, export, _) = await RecallEvidence.ExportAsync(w.S, token, RecallEvidence.Body("Pdf", caseId));
        var exportId = Str(export, "exportId");

        var other = w.S.Mint(w.S.Qa1, RecallEvidence.Keys, Guid.NewGuid());
        var (ps, preview, _) = await w.S.SendAsync(HttpMethod.Get, $"/api/recall/evidence/preview?caseId={caseId}", other);
        Assert.Equal((403, "CROSS_LE_FORBIDDEN"), (ps, RecallScenario.Code(preview)));
        Assert.DoesNotContain(caseId, preview.ToString(), StringComparison.Ordinal);
        var (es, refused, _) = await RecallEvidence.ExportAsync(w.S, other, RecallEvidence.Body("Pdf", caseId));
        Assert.Equal((403, "CROSS_LE_FORBIDDEN"), (es, RecallScenario.Code(refused)));
        Assert.Equal(403, (await RecallEvidence.DownloadAsync(w.S, other, exportId)).Status);
        Assert.Equal(0, (await w.S.ExpectAsync(HttpMethod.Get, "/api/recall/evidence/exports", other, null, 200)).GetProperty("total").GetInt64());
        Assert.Single(w.S.Persisted(RecallCollections.EvidenceExports));               // the refused export wrote nothing

        // the key: without recall.evidence.export → 403 on each operation
        var reader = w.S.Mint(w.S.Qa1, ["recall.cases.read"]);
        Assert.Equal(403, (await w.S.SendAsync(HttpMethod.Get, $"/api/recall/evidence/preview?caseId={caseId}", reader)).Status);
        Assert.Equal(403, (await RecallEvidence.ExportAsync(w.S, reader, RecallEvidence.Body("Pdf", caseId))).Status);
        Assert.Equal(403, (await RecallEvidence.DownloadAsync(w.S, reader, exportId)).Status);

        // the request: exactly one subject; a period of at most 366 days, end ≥ start; an unknown file → 404
        Assert.Equal(400, (await w.S.SendAsync(HttpMethod.Get, "/api/recall/evidence/preview", token)).Status);
        Assert.Equal(400, (await w.S.SendAsync(HttpMethod.Get, $"/api/recall/evidence/preview?caseId={caseId}&from=2026-01-01&to=2026-02-01", token)).Status);
        var (rs, range, _) = await w.S.SendAsync(HttpMethod.Get, "/api/recall/evidence/preview?from=2026-01-01&to=2027-06-01", token);
        Assert.Equal((400, "RECALL_EVIDENCE_RANGE_INVALID"), (rs, RecallScenario.Code(range)));
        var (bs, backwards, _) = await w.S.SendAsync(HttpMethod.Get, "/api/recall/evidence/preview?from=2026-03-01&to=2026-02-01", token);
        Assert.Equal((400, "RECALL_EVIDENCE_RANGE_INVALID"), (bs, RecallScenario.Code(backwards)));
        var (fs, format, _) = await RecallEvidence.ExportAsync(w.S, token, RecallEvidence.Body("Docx", caseId));
        Assert.Equal(400, fs);
        Assert.Equal(404, (await RecallEvidence.DownloadAsync(w.S, token, Guid.NewGuid().ToString("D"))).Status);
    }
}

/// <summary>
/// WP-0177-R7b — the Kanıt Paketi of a drill run and of a period (pack §4.13): a run carries its summary, its trace and its result signature
/// (3–6 not applicable, said so); a period lists the company's runs of those days and NOTHING of another company of the tenant.
/// </summary>
public sealed class RecallDrillEvidenceTests : IClassFixture<RecallDrillApiFactory>
{
    private readonly RecallDrillApiFactory _factory;

    public RecallDrillEvidenceTests(RecallDrillApiFactory factory) => _factory = factory;

    [Fact]
    public async Task A_signed_drill_run_and_a_period_export_from_their_own_company_only()
    {
        var w = await DrillWorld.CreateAsync(_factory);
        var token = w.S.Mint(w.S.Qa1, [.. RecallEvidence.Keys, "recall.drills.read"]);
        var finished = await w.FinishedRunAsync();
        var runId = DrillWorld.RunId(finished);
        var prepared = await w.PrepareOkAsync(finished);
        var signature = await w.SignAsync(runId);
        var (rs, result, _) = await w.ResultAsync(runId, RecallScenario.ETag(prepared), signature);
        Assert.True(rs == 200, result.ToString());

        var run = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/evidence/preview?runId={runId}", token, null, 200);
        Assert.Equal(("DrillRun", runId), (run.GetProperty("subject").GetProperty("kind").GetString(), run.GetProperty("subject").GetProperty("runId").GetString()));
        Assert.True(RecallEvidence.Section(run, "2_trace").GetProperty("applicable").GetBoolean());
        foreach (var k in new[] { "3_quarantine", "4_customers", "5_returnsAndEffectiveness", "6_timeline" })
        {
            Assert.False(RecallEvidence.Section(run, k).GetProperty("applicable").GetBoolean(), k);
            Assert.Equal("notApplicableDrill", RecallEvidence.Section(run, k).GetProperty("note").GetString());
        }

        var sign = Assert.Single(RecallEvidence.Rows(RecallEvidence.Section(run, "7_signatures")));
        Assert.Equal((signature, w.S.Qa1.ToString("D"), "APPROVE", "Ok"), (sign[1], sign[2], sign[3], sign[5]));

        var (es, export, _) = await RecallEvidence.ExportAsync(w.S, token, RecallEvidence.Body("Pdf", runId: runId));
        Assert.True(es == 201, export.ToString());
        var (_, bytes, _) = await RecallEvidence.DownloadAsync(w.S, token, export.GetProperty("exportId").GetString()!);
        Assert.Equal(export.GetProperty("sha256").GetString(), RecallEvidence.Sha256(bytes));

        // a period: this company's runs of those days; a run of ANOTHER company of the tenant in the same days stays out (company scope)
        var runs = _factory.Database.GetCollection<BsonDocument>(RecallCollections.DrillRuns);
        var mine = runs.Find(new BsonDocument("_id", runId)).Single();
        var clone = mine.DeepClone().AsBsonDocument;
        clone["_id"] = mine["_id"].IsString ? Guid.NewGuid().ToString("D") : new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard);
        clone["LegalEntityId"] = mine["LegalEntityId"].IsString ? Guid.NewGuid().ToString("D") : new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard);
        clone["Number"] = "RC-OTHER-COMPANY";
        await runs.InsertOneAsync(clone);
        var day = _factory.Clock.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd");
        var period = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/evidence/preview?from={day}&to={day}", token, null, 200);
        var summary = RecallEvidence.Section(period, "1_summary");
        var listed = RecallEvidence.Rows(summary, summary.GetProperty("tables").GetArrayLength() - 1);
        Assert.Contains(listed, r => r.Contains(finished.GetProperty("number").GetString()!));
        Assert.DoesNotContain(listed, r => r.Contains("RC-OTHER-COMPANY"));
        Assert.All(new[] { "2_trace", "3_quarantine", "4_customers", "5_returnsAndEffectiveness", "6_timeline", "7_signatures" },
            k => Assert.Equal("notApplicablePeriod", RecallEvidence.Section(period, k).GetProperty("note").GetString()));
    }
}
