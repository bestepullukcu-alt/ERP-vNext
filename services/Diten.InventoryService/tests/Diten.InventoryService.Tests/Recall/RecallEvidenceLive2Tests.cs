using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.InventoryService.Application.Features.Recall.Evidence;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Recall.Enums;
using Diten.InventoryService.Persistence.Recall;
using Diten.InventoryService.Tests.CentralAudit;
using Diten.InventoryService.Tests.Expiry;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-MVP1-FIX-LIVE2 — the recall Kanıt Paketi after the live round (case RC-2026-0001): people, company, product, SKU, warehouse are NAMES
/// (the id beside them; unreadable → the id + "(okunamadı)", never invented); every stored code is worded in the asked language (the
/// screen's own keys, shared through the service resx); the seals carry their real names and the one rc-v1 signs; a completed effectiveness
/// check states its result; the PDF is drawn in tr / en / fr / es / ru and, for zh / ar, in English with a note.
/// </summary>
public sealed class RecallEvidenceLive2Tests : IClassFixture<RecallApiFactory>
{
    private const string Ayse = "Ayşe Demir";
    private const string Mehmet = "Mehmet Kaya";
    private readonly RecallApiFactory _factory;

    public RecallEvidenceLive2Tests(RecallApiFactory factory) => _factory = factory;

    private static string Str(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    private async Task<(RecallTraceWorld W, string CaseId, string Token, JsonElement Detail)> ClosedCaseAsync()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        var (caseId, _) = await RecallR3.ApproveAsync(w.S, w.Fg);
        await RecallR3.CloseAsync(w.S, caseId);
        var token = w.S.Mint(w.S.Qa1, RecallEvidence.Keys);
        return (w, caseId, token, await w.S.GetAsync(caseId));
    }

    private static async Task<JsonElement> PreviewAsync(RecallScenario s, string token, string caseId, string language)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/recall/evidence/preview?caseId={caseId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.AcceptLanguage.ParseAdd(language);
        using var response = await s.Client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, text);
        var root = JsonDocument.Parse(text).RootElement.Clone();
        return root.TryGetProperty("data", out var data) ? data : root;
    }

    /// <summary>Export in a language, download THE stored bytes, check their SHA-256 against the logged one.</summary>
    private static async Task<byte[]> ExportAsync(RecallScenario s, string token, string caseId, string format, string language)
    {
        var body = RecallEvidence.Body(format, caseId);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/recall/evidence/exports") { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", (string)body["idempotencyKey"]!);
        request.Headers.AcceptLanguage.ParseAdd(language);
        using var response = await s.Client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True((int)response.StatusCode == 201, text);
        var root = JsonDocument.Parse(text).RootElement;
        var export = root.TryGetProperty("data", out var data) ? data : root;
        var (status, bytes, _) = await RecallEvidence.DownloadAsync(s, token, Str(export, "exportId"));
        Assert.Equal(200, status);
        Assert.Equal(Str(export, "sha256"), RecallEvidence.Sha256(bytes)); // still the hash of THE delivered bytes
        return bytes;
    }

    private static Dictionary<string, string> Summary(JsonElement preview)
        => RecallEvidence.Rows(RecallEvidence.Section(preview, "1_summary")).ToDictionary(r => r[0], r => r[1]);

    private static string Flat(string text) => Regex.Replace(text, @"\s", string.Empty);

    // ── values and seals ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Values_names_and_seals_are_worded_in_the_asked_language_and_the_signed_seal_is_the_rc_v1_one()
    {
        var (w, caseId, token, detail) = await ClosedCaseAsync();
        var item = Guid.Parse(Str(detail.GetProperty("item"), "itemId"));
        var sku = Guid.Parse(Str(detail.GetProperty("item"), "skuId"));
        _factory.Products.SetDisplay(item, "GP-0042", "Asetilsalisilik asit 100 mg");
        _factory.Products.SetDisplay(sku, "LS-0042");
        _factory.LegalEntities.RegisterName(w.S.Ledger.LegalEntity, "GM-EU", "Grand Medical EU");

        var tr = await PreviewAsync(w.S, token, caseId, "tr-TR");
        var summary = Summary(tr);
        Assert.Equal(("Kapandı", "İleriye"), (summary["status"], summary["direction"]));                    // finding 3 — not "Closed" / "Forward"
        Assert.Equal(RecallEvidenceText.Of("Recall.Level." + Str(detail, "level"), "tr"), summary["level"]);
        Assert.Equal(("GP-0042 · Asetilsalisilik asit 100 mg", item.ToString("D")), (summary["item"], summary["itemId"]));   // finding 2
        Assert.Equal(("LS-0042", "GM-EU · Grand Medical EU"), (summary["skuCode"], summary["company"]));
        var events = RecallEvidence.Rows(RecallEvidence.Section(tr, "6_timeline")).Select(r => r[2]).ToList();
        Assert.Contains("Vaka açıldı", events);
        Assert.DoesNotContain(events, e => Enum.GetNames<RecallCaseEventKind>().Contains(e));                 // no raw event name
        var nodes = RecallEvidence.Rows(RecallEvidence.Section(tr, "2_trace")).ToList();
        Assert.DoesNotContain(nodes, n => Enum.GetNames<RecallScopeItemKind>().Contains(n[0]));               // "Sevkiyat", not "Shipment"
        Assert.Contains(nodes, n => n[0] == "Sevkiyat");
        Assert.All(nodes.Where(n => n[7].Length > 0), n => Assert.DoesNotMatch("^[0-9a-f-]{36}", n[2]));    // a stock node's place is its code · name
        Assert.Contains(nodes, n => n[2].Contains(w.S.Ledger.WarehouseNode.Code, StringComparison.Ordinal));
        Assert.DoesNotContain(nodes, n => n[2].Contains(w.S.Ledger.WarehouseNode.Code + " · " + w.S.Ledger.WarehouseNode.Code, StringComparison.Ordinal)); // the name once

        // finding 4: the two seals rc-v1 signs are the stored SCOPE seals — the same value in the rc-v1 subject, on the case (the screen's data)
        // and in the pack, under the same name; the trace snapshot digest is a different value with its own name
        var subjectBody = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/esign-subjects/recall.case/{caseId}", w.S.Mint(w.S.Qa1, RecallR3.QaKeys), null, 200);
        var subject = subjectBody.TryGetProperty("data", out var sd) ? sd : subjectBody;
        var fields = subject.GetProperty("fields");
        Assert.Equal(Str(fields, "approvedSnapshotSha256"), summary["approvedScopeSeal"]);
        Assert.Equal(Str(fields, "submittedSnapshotSha256"), summary["submittedScopeSeal"]);
        Assert.Equal(Str(detail.GetProperty("approvedScope"), "sha256"), summary["approvedScopeSeal"]);
        Assert.NotEqual(summary["approvedScopeSeal"], summary["snapshotSha256"]);
        var lines = subject.GetProperty("summary").EnumerateArray().ToDictionary(l => Str(l, "label"), l => Str(l, "value"));
        Assert.Equal(summary["approvedScopeSeal"], lines["Onaylanan kapsam mührü / Approved scope seal"]);
        Assert.Equal(summary["submittedScopeSeal"], lines["Gönderilen kapsam mührü / Submitted scope seal"]);

        // the PDF (tr): the worded values and the signed seal under its name — sabotage (c) / (d) turn this red
        var pdf = Flat(ExpiryEvidenceTests.PdfText.Read(await ExportAsync(w.S, token, caseId, "Pdf", "tr-TR")).Text);
        Assert.Contains(Flat("Onaylanan kapsam mührü (imzalanan)") + summary["approvedScopeSeal"], pdf, StringComparison.Ordinal);
        Assert.Contains(Flat("Gönderilen kapsam mührü (imzalanan)") + summary["submittedScopeSeal"], pdf, StringComparison.Ordinal);
        Assert.Contains(Flat("İz anlık görüntüsü özeti") + summary["snapshotSha256"], pdf, StringComparison.Ordinal);
        Assert.Contains("Kapandı", pdf, StringComparison.Ordinal);
        Assert.Contains("GM-EU·GrandMedicalEU", pdf, StringComparison.Ordinal);
        Assert.DoesNotContain("Closed", pdf, StringComparison.Ordinal);

        // the CSV carries the same names AND keeps the ids beside them
        var csv = Encoding.UTF8.GetString(await ExportAsync(w.S, token, caseId, "Csv", "tr"));
        Assert.Contains("Ürün;GP-0042 · Asetilsalisilik asit 100 mg", csv, StringComparison.Ordinal);
        Assert.Contains($"Ürün kimliği;{item:D}", csv, StringComparison.Ordinal);
        Assert.Contains($";GM-EU · Grand Medical EU;{w.S.Ledger.LegalEntity:D};", csv, StringComparison.Ordinal);
        Assert.Contains(";Kapandı", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unreadable_master_name_is_the_id_with_okunamadi_and_never_invented()
    {
        var (w, caseId, token, detail) = await ClosedCaseAsync();
        var item = Guid.Parse(Str(detail.GetProperty("item"), "itemId"));
        _factory.Products.MarkUnavailable(item);                           // PRODUCT-MASTER down for this product; MDM has no name for the company
        var summary = Summary(await PreviewAsync(w.S, token, caseId, "tr"));
        Assert.Equal($"{item:D} (okunamadı)", summary["item"]);
        Assert.Equal($"{w.S.Ledger.LegalEntity:D} (okunamadı)", summary["company"]);
        Assert.EndsWith(" (okunamadı)", summary["skuCode"], StringComparison.Ordinal); // the stub publishes no SKU code
    }

    // ── people ───────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_person_is_the_signer_at_signing_or_the_audit_name_of_the_same_event_and_never_another_events_name()
    {
        var (w, caseId, token, _) = await ClosedCaseAsync();

        // before any name is known: every person is the id + "(name unreadable)" — nothing invented (sabotage (b))
        var before = Summary(await PreviewAsync(w.S, token, caseId, "en"));
        Assert.Equal($"{w.S.Qa1:D} (name unreadable)", before["openedBy"]);
        Assert.Equal($"{w.S.Qa2:D} (name unreadable)", before["closedBy"]);

        // the names the central audit queue recorded (as the named tokens of the live round would have): one per user, on THEIR rows only;
        // plus a trap — a row of the Opened event's correlation by the OTHER user, named "Yanlış Kişi"
        var outbox = CentralAuditInventoryTests.Outbox(_factory);
        await outbox.UpdateManyAsync(x => x.TenantId == w.S.Tenant && x.ActorId == w.S.Qa1, Builders<Diten.BuildingBlocks.CentralAudit.CentralAuditOutboxRecord>.Update.Set(x => x.ActorDisplayName, Ayse));
        await outbox.UpdateManyAsync(x => x.TenantId == w.S.Tenant && x.ActorId == w.S.Qa2, Builders<Diten.BuildingBlocks.CentralAudit.CentralAuditOutboxRecord>.Update.Set(x => x.ActorDisplayName, Mehmet));
        var opened = _factory.Database.GetCollection<BsonDocument>(RecallCollections.CaseEvents)
            .Find(new BsonDocument { ["CaseId"] = caseId, ["Kind"] = "Opened" }).Single();
        await outbox.InsertOneAsync(new Diten.BuildingBlocks.CentralAudit.CentralAuditOutboxRecord
        {
            Id = Guid.NewGuid(), TenantId = w.S.Tenant, ActorId = w.S.Qa2, ActorDisplayName = "Yanlış Kişi", SourceService = "inventory", SourceModule = "recall",
            Category = "Inventory", Operation = "trap", EntityType = "RecallCase", RequestType = "trap", CorrelationId = Guid.Parse(opened["CorrelationId"].AsString),
            OccurredAtUtc = DateTime.UtcNow, NextAttemptAtUtc = DateTime.UtcNow, CreatedAtUtc = DateTime.UtcNow
        });

        // and MOD-0022 knows the closing signature, signed by Qa2 under the name it recorded then
        var real = _factory.ESign.Sign(w.S.Tenant, w.S.Qa2, caseId, "APPROVE", new string('a', 64), "recall", RecallSignableActions.CaseSubjectType,
            "recall.case.close", DateTimeOffset.UtcNow, w.S.Ledger.LegalEntity, signerName: "Mehmet Kaya (imza anı)");
        await _factory.Database.GetCollection<BsonDocument>(RecallCollections.Cases)
            .UpdateOneAsync(new BsonDocument("_id", caseId), new BsonDocument("$set", new BsonDocument("ClosureSignatureId", real)));

        var preview = await PreviewAsync(w.S, token, caseId, "en");
        var summary = Summary(preview);
        Assert.Equal((Ayse, w.S.Qa1.ToString("D")), (summary["openedBy"], summary["openedById"]));         // sabotage (a): an id instead of the name → red
        Assert.Equal(Mehmet, summary["scopeApprovedBy"]);
        Assert.Equal(Ayse, summary["effectivenessCompletedBy"]);
        Assert.Equal("Mehmet Kaya (imza anı)", summary["closedBy"]);                                         // the signer's name at signing wins
        var timeline = RecallEvidence.Rows(RecallEvidence.Section(preview, "6_timeline"));
        Assert.DoesNotContain(timeline, r => r[3] == "Yanlış Kişi");                                         // never another event's name
        Assert.All(timeline.Where(r => r[4] == w.S.Qa1.ToString("D")), r => Assert.Equal(Ayse, r[3]));
        Assert.All(timeline.Where(r => r[4] == w.S.Qa2.ToString("D")), r => Assert.Equal(Mehmet, r[3]));
        var signature = Assert.Single(RecallEvidence.Rows(RecallEvidence.Section(preview, "7_signatures")), r => r[1] == real);
        Assert.Equal(("Mehmet Kaya (imza anı)", w.S.Qa2.ToString("D")), (signature[2], signature[3]));

        // the PDF prints the names (the ids stay in the CSV)
        var pdf = Flat(ExpiryEvidenceTests.PdfText.Read(await ExportAsync(w.S, token, caseId, "Pdf", "en")).Text);
        Assert.Contains(Flat(Ayse), pdf, StringComparison.Ordinal);
        Assert.Contains(Flat("Mehmet Kaya (imza anı)"), pdf, StringComparison.Ordinal);
        var csv = Encoding.UTF8.GetString(await ExportAsync(w.S, token, caseId, "Csv", "en"));
        Assert.Contains($"Opened by;{Ayse}", csv, StringComparison.Ordinal);
        Assert.Contains($"Opened by (id);{w.S.Qa1:D}", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_new_event_records_the_name_of_the_user_from_the_token()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        var named = TestTokens.Mint(w.S.Tenant, w.S.Ledger.LegalEntity, w.S.Qa1, RecallR3.QaKeys,
            [new Claim("given_name", "Ayşe"), new Claim("family_name", "Demir")]);
        var (status, opened, _) = await w.S.PostOpenAsync(named, RecallScenario.Open(w.Fg));
        Assert.True(status == 201, opened.ToString());
        var stored = _factory.Database.GetCollection<BsonDocument>(RecallCollections.CaseEvents)
            .Find(new BsonDocument { ["CaseId"] = RecallScenario.Id(opened), ["Kind"] = "Opened" }).Single();
        Assert.Equal(Ayse, stored["ActorName"].AsString);
        var summary = Summary(await PreviewAsync(w.S, w.S.Mint(w.S.Qa1, RecallEvidence.Keys), RecallScenario.Id(opened), "tr"));
        Assert.Equal(Ayse, summary["openedBy"]);                                                             // no audit lookup needed
    }

    // ── effectiveness ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_completed_check_without_shipments_states_its_result_instead_of_no_record()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        var lot = await w.S.LotAsync();
        await w.PostAsync(w.Lots.Receipt("5", lot, movementType: "GOODS_RECEIPT_PO"), "MOD-0172", "PO-" + Guid.NewGuid().ToString("N")[..8]);
        var (caseId, _) = await RecallR3.ApproveAsync(w.S, lot);
        await RecallR3.CloseAsync(w.S, caseId);
        var token = w.S.Mint(w.S.Qa1, RecallEvidence.Keys);

        var section = RecallEvidence.Section(await PreviewAsync(w.S, token, caseId, "tr"), "5_returnsAndEffectiveness");
        var tables = section.GetProperty("tables").EnumerateArray().Select(t => Str(t, "key")).ToList();
        Assert.Equal(["returns", "effectivenessCheck"], tables);                                            // no empty per-SKU table
        var check = RecallEvidence.Rows(section, 1).ToDictionary(r => r[0], r => r[1]);
        Assert.Equal("Sevkiyat yok — ölçülecek miktar yok", check["effectiveness"]);                         // sabotage (e)
        Assert.Equal("Etkinlik kontrolü tamamlandı", check["status"]);
        Assert.NotEqual(string.Empty, check["target"]);
        Assert.NotEqual(string.Empty, check["effectivenessCompletedAtUtc"]);
        Assert.NotEqual(string.Empty, check["signatureId"]);
        Assert.NotEqual(string.Empty, check["note"]);

        var pdf = Flat(ExpiryEvidenceTests.PdfText.Read(await ExportAsync(w.S, token, caseId, "Pdf", "tr")).Text);
        var start = pdf.IndexOf(Flat(RecallEvidenceText.Word(RecallEvidenceSections.ReturnsAndEffectiveness, "tr")), StringComparison.Ordinal);
        var end = pdf.IndexOf(Flat(RecallEvidenceText.Word(RecallEvidenceSections.Timeline, "tr")), StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, pdf);
        var five = pdf[start..end];
        // the em dash extracts differently through the minimal test reader (the font has U+2014 — measured); the words around it are compared
        Assert.Contains("Sevkiyatyokölçülecekmiktaryok", Regex.Replace(five, @"[^\p{L}\p{N}]", string.Empty), StringComparison.Ordinal);
        Assert.Contains(Flat("Etkinlik kontrolü sonucu"), five, StringComparison.Ordinal);
    }

    // ── languages ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Every_language_has_a_PDF_tr_en_fr_es_ru_in_their_own_words_zh_and_ar_in_English_with_the_note()
    {
        var (w, caseId, token, _) = await ClosedCaseAsync();
        foreach (var lang in RecallEvidenceText.Languages)
        {
            var text = Flat(ExpiryEvidenceTests.PdfText.Read(await ExportAsync(w.S, token, caseId, "Pdf", lang)).Text);
            if (RecallEvidenceText.PdfLanguages.Contains(lang))
            {
                Assert.Contains(Flat(RecallEvidenceText.Of("RecallPdf.Title", lang)), text, StringComparison.Ordinal);
                Assert.Contains(Flat(RecallEvidenceText.Of("Recall.Status.Closed", lang)), text, StringComparison.Ordinal);
                Assert.DoesNotContain(Flat(RecallEvidenceText.Word("englishFallback", "en")), text, StringComparison.Ordinal);
            }
            else
            {
                Assert.Contains(Flat(RecallEvidenceText.Of("RecallPdf.Title", "en")), text, StringComparison.Ordinal);
                Assert.Contains(Flat(RecallEvidenceText.Word("englishFallback", "en")), text, StringComparison.Ordinal);
                Assert.Contains($"Requestedlanguage:{lang}", text, StringComparison.Ordinal);
            }

            // the CSV is in the selected language, zh / ar included
            var csv = Encoding.UTF8.GetString(await ExportAsync(w.S, token, caseId, "Csv", lang));
            Assert.StartsWith("﻿" + RecallEvidenceText.Of("RecallPdf.Title", lang) + ";", csv, StringComparison.Ordinal);
            Assert.Contains(RecallEvidenceText.Of("Recall.Status.Closed", lang), csv, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Every_stored_code_the_pack_words_has_its_key_in_all_seven_languages()
    {
        var codes = Enum.GetNames<RecallCaseStatus>().Select(v => "Recall.Status." + v)
            .Concat(Enum.GetNames<RecallLevel>().Select(v => "Recall.Level." + v))
            .Concat(Enum.GetNames<RecallTraceDirection>().Select(v => "Recall.Direction." + v))
            .Concat(Enum.GetNames<RecallCaseEventKind>().Select(v => "Recall.Event." + v))
            .Concat(Enum.GetNames<RecallQuarantineRequestStatus>().Select(v => "Recall.ReqStatus." + v))
            .Concat(Enum.GetNames<RecallNotNeededReason>().Select(v => "Recall.NotNeeded." + v))
            .Concat(Enum.GetNames<RecallNotificationChannel>().Select(v => "Recall.Channel." + v))
            .Concat(Enum.GetNames<RecallDrillRunStatus>().Select(v => "Recall.DrillStatus." + v))
            .Concat(Enum.GetNames<RecallDrillSelectionMode>().Select(v => "Recall.Drills.Mode" + v))
            .Concat(Enum.GetNames<RecallScopeItemKind>().Select(v => "Recall.Kind." + v))
            .Concat(["Recall.Class.N", "Recall.Ev.W.meaning.APPROVE", "Recall.Ev.W.nameUnreadable", "Recall.Ev.W.unreadable", "Recall.Ev.W.noShipments"]);
        foreach (var key in codes)
            foreach (var lang in RecallEvidenceText.Languages)
                Assert.False(string.IsNullOrWhiteSpace(RecallEvidenceText.Find(key, lang)), $"{key} [{lang}]");
        Assert.All(RecallSignableActions.All, a => Assert.Equal(7, a.Names.Count));
        Assert.Equal("tr", RecallEvidenceText.Normal("tr-TR"));
        Assert.Equal("zh", RecallEvidenceText.Normal("zh-Hans-CN"));
        Assert.Equal("en", RecallEvidenceText.Normal("de-DE"));
        Assert.Equal("en", RecallEvidenceText.PdfLanguage("ar"));
    }
}
