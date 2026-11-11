using System.Text;
using System.Text.Json;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Recall.Trace;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// MOD-0177-FU01 test seam: the REAL recall trace source, wrapped only to count the sales-issue reads (AC-26 (f), (g)); no answer is changed.
/// </summary>
public sealed class CountingRecallTraceSource(IRecallTraceSource inner, RecallApiFactory factory) : IRecallTraceSource
{
    public Task<IReadOnlyList<TraceLot>> LotsAsync(IReadOnlyCollection<Guid> lotIds, CancellationToken cancellationToken = default) => inner.LotsAsync(lotIds, cancellationToken);

    public Task<IReadOnlyList<TraceLink>> ParentLinksAsync(IReadOnlyCollection<Guid> childLotIds, CancellationToken cancellationToken = default)
        => inner.ParentLinksAsync(childLotIds, cancellationToken);

    public Task<IReadOnlyList<TraceLink>> ChildLinksAsync(IReadOnlyCollection<Guid> parentLotIds, CancellationToken cancellationToken = default)
        => inner.ChildLinksAsync(parentLotIds, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, TraceTransaction>> FirstInboundAsync(IReadOnlyCollection<Guid> lotIds, CancellationToken cancellationToken = default)
        => inner.FirstInboundAsync(lotIds, cancellationToken);

    public Task<IReadOnlyList<TraceTransaction>> TransactionsOfLotsAsync(IReadOnlyCollection<Guid> lotIds, CancellationToken cancellationToken = default)
        => inner.TransactionsOfLotsAsync(lotIds, cancellationToken);

    public Task<IReadOnlyList<TraceSalesIssue>> SalesIssuesOfLotsAsync(IReadOnlyCollection<string> lotIds, CancellationToken cancellationToken = default)
    {
        factory.CountSalesIssueRead();
        return inner.SalesIssuesOfLotsAsync(lotIds, cancellationToken);
    }

    public Task<IReadOnlyList<TraceBalance>> BalancesAsync(IReadOnlyCollection<Guid> itemIds, IReadOnlyCollection<Guid> lotIds, CancellationToken cancellationToken = default)
        => inner.BalancesAsync(itemIds, lotIds, cancellationToken);

    public Task<IReadOnlyList<TraceTransaction>> TransactionsOfDocumentAsync(string sourceModule, string sourceDocumentId, CancellationToken cancellationToken = default)
        => inner.TransactionsOfDocumentAsync(sourceModule, sourceDocumentId, cancellationToken);

    public Task<IReadOnlyList<TraceTransaction>> OutboundOfSkuAsync(Guid itemId, Guid skuId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => inner.OutboundOfSkuAsync(itemId, skuId, from, to, cancellationToken);

    public Task<IReadOnlyDictionary<string, Guid>> TypedLotLinksAsync(IReadOnlyCollection<string> transactionIds, CancellationToken cancellationToken = default)
        => inner.TypedLotLinksAsync(transactionIds, cancellationToken);

    public Task<long> OtherCompanyActivityAsync(IReadOnlyCollection<Guid> lotIds, CancellationToken cancellationToken = default)
        => inner.OtherCompanyActivityAsync(lotIds, cancellationToken);

    public Task<IReadOnlyList<TraceSerial>> SerialsAsync(IReadOnlyCollection<Guid> lotIds, CancellationToken cancellationToken = default)
        => inner.SerialsAsync(lotIds, cancellationToken);

    public Task<bool?> LotControlledAsync(Guid skuId, SkuLevel level, CancellationToken cancellationToken = default) => inner.LotControlledAsync(skuId, level, cancellationToken);
}

/// <summary>
/// MOD-0177-FU01 AC-26 (CT-3, F-2@807c8dc2; pack §8.10) — the evidence pack: the CRM names are the export's snapshot (stored bytes and hash
/// never change), an unresolved customer is marked with its shipment number and does not refuse the export, keys without a CRM id are marked
/// only next to a CRM customer, and a customer the caller may not read refuses the pack (no file, no log row, no audit row).
/// </summary>
public sealed class RecallCrmEvidenceTests : IClassFixture<RecallApiFactory>
{
    private readonly RecallApiFactory _factory;

    public RecallCrmEvidenceTests(RecallApiFactory factory) => _factory = factory;

    private static List<string> CustomerCells(JsonElement preview) => RecallEvidence.Rows(RecallEvidence.Section(preview, "4_customers")).Select(r => r[0]).ToList();

    /// <summary>A Draft case: A (CRM), C (CRM, sale number <paramref name="numberC"/>), a legacy ship-to and a Manual hand entry.</summary>
    private async Task<(CrmWorld W, string CaseId, Guid C)> MixedAsync(string numberC = "GIS-0042")
    {
        var w = await CrmWorld.CreateAsync(_factory);
        var c = Guid.NewGuid();
        w.Account(c, "C Eczanesi", "ACC-C");
        w.FirstSaleIs(CrmWorld.A);
        var shipC = await w.SellAsync(c);
        var legacy = await w.SellRawAsync(null, shipTo: "CUST-100/ADDR-2");
        var draft = await w.DraftAsync();
        var id = RecallScenario.Id(draft);
        var items = _factory.Database.GetCollection<BsonDocument>("recall_scope_items");
        var legacyItem = w.ScopeDocs(id).Single(d => d["Kind"] == "Shipment" && d["SourceDocumentId"] == legacy);
        items.UpdateOne(new BsonDocument("_id", legacyItem["_id"]), new BsonDocument("$unset", new BsonDocument("CustomerRefSource", "")));
        Assert.Equal(200, (await w.S.AddItemsAsync(id, RecallScenario.ETag(draft), RecallScenario.ShipmentItem(w.Fg, "SHP-M-9", "CUST-300/ADDR-1", 1m))).Status);
        var sale = w.S.Persisted("inventory_transactions").Single(t => t.Contains("SourceDocumentId") && t["SourceDocumentId"] == shipC);
        _factory.Database.GetCollection<BsonDocument>("inventory_transactions").UpdateOne(new BsonDocument("_id", sale["_id"]),
            new BsonDocument("$set", new BsonDocument("TransactionNumber", numberC)));
        return (w, id, c);
    }

    [Fact]
    public async Task The_pack_marks_and_snapshots_names_and_refuses_only_a_forbidden_customer()
    {
        var (w, id, c) = await MixedAsync();
        _factory.Crm.Behaviour[c] = CrmBehaviour.ServiceUnavailable;
        try
        {
            // (a) CSV and PDF are produced with an Unresolved customer; (f) its cell carries the sale number found behind its item
            var reads = _factory.SalesIssueReads;
            var (status, export) = await RecallCrmEvidence.CreateAsync(w, id, "Csv", "tr");
            Assert.True(status == 201, export.ToString());
            Assert.Equal(reads + 1, _factory.SalesIssueReads);
            var csv = Encoding.UTF8.GetString(await RecallCrmEvidence.FileAsync(w, CrmWorld.Str(export, "exportId")));
            Assert.Contains("Example Hastanesi · ACC-2026-000042", csv, StringComparison.Ordinal);
            Assert.Contains("Çözülemedi (GIS-0042)", csv, StringComparison.Ordinal);
            Assert.Contains("CUST-100/ADDR-2 (Eski kayıt)", csv, StringComparison.Ordinal);
            Assert.Contains("CUST-300/ADDR-1 (CRM kimliği yok)", csv, StringComparison.Ordinal);
            Assert.Contains("Müşteri adları", csv, StringComparison.Ordinal);
            Assert.Contains("CRM'den, dışa aktarım anında", csv, StringComparison.Ordinal);
            foreach (var key in new[] { CrmWorld.A, c }) Assert.DoesNotContain(key.ToString("D"), csv, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(201, (await RecallCrmEvidence.CreateAsync(w, id, "Pdf", "tr")).Status);

            // (b) the stored sha256 is the hash of the delivered bytes; (c) a CRM name change leaves the stored file and hash as they were
            var bytes = await RecallCrmEvidence.FileAsync(w, CrmWorld.Str(export, "exportId"));
            Assert.Equal(CrmWorld.Str(export, "sha256"), RecallEvidence.Sha256(bytes));
            w.Account(CrmWorld.A, "Yeni Ad Hastanesi", "ACC-NEW");
            Assert.Equal(bytes, await RecallCrmEvidence.FileAsync(w, CrmWorld.Str(export, "exportId")));
            Assert.Contains("Yeni Ad Hastanesi · ACC-NEW", await RecallCrmEvidence.ExportTextAsync(w, id, "Csv", "tr"), StringComparison.Ordinal);

            // (e) the export log row and the audit row carry no name and no code
            var log = w.S.Persisted("recall_evidence_exports").Select(d => { var copy = d.DeepClone().AsBsonDocument; copy.Remove("Content"); return copy.ToJson(); }).ToList();
            var audit = w.S.Audit("recall.evidence.exported").Select(d => d.ToJson()).ToList();
            foreach (var text in log.Concat(audit))
            {
                foreach (var word in new[] { CrmWorld.NameA, CrmWorld.CodeA, "Yeni Ad Hastanesi", "ACC-NEW" }) Assert.DoesNotContain(word, text, StringComparison.Ordinal);
            }

            // (g) every CRM customer resolved: the customer section reads no sales movement
            _factory.Crm.Behaviour.Clear();
            reads = _factory.SalesIssueReads;
            Assert.Equal(201, (await RecallCrmEvidence.CreateAsync(w, id, "Csv", "tr")).Status);
            Assert.Equal(reads, _factory.SalesIssueReads);
        }
        finally
        {
            _factory.Crm.Behaviour.Clear();
        }
    }

    [Fact]
    public async Task A_case_without_CRM_customers_keeps_the_pre_FU01_customer_and_summary_sections()
    {
        var w = await CrmWorld.CreateAsync(_factory);
        await w.SellRawAsync(null, shipTo: "CUST-100/ADDR-2");
        var id = RecallScenario.Id(await w.DraftAsync());
        _factory.Crm.ResetCounts();
        var preview = await w.GetAsync($"/api/recall/evidence/preview?caseId={id}");
        Assert.Equal(new[] { "CUST-100/ADDR-2", ShipmentBundleDouble.ShipTo(w.FirstShipment) }.Order(StringComparer.Ordinal), CustomerCells(preview));
        Assert.DoesNotContain(RecallEvidence.Rows(RecallEvidence.Section(preview, "1_summary")), r => r[0] == "customerNames");
        Assert.Equal(0, _factory.Crm.Calls);
    }

    [Fact]
    public async Task A_forbidden_customer_refuses_preview_and_export_before_anything_is_written()
    {
        var w = await CrmWorld.CreateAsync(_factory);
        var (c, d) = (Guid.NewGuid(), Guid.NewGuid());
        w.Account(c, "C Eczanesi", "ACC-C");
        w.Account(d, "D Eczanesi", "ACC-D");
        w.FirstSaleIs(CrmWorld.A);
        await w.SellAsync(c);
        await w.SellAsync(d);
        var (id, _) = await w.ActiveAsync();
        // a stored export made while C was readable (to replay later)
        var replayKey = "ex-" + Guid.NewGuid().ToString("N");
        var (s0, stored) = await RecallCrmEvidence.CreateAsync(w, id, "Csv", "tr", replayKey);
        Assert.True(s0 == 201, stored.ToString());

        _factory.Crm.Behaviour[c] = CrmBehaviour.Forbidden;
        try
        {
            var before = (w.Count("recall_evidence_exports"), w.Count("recall_evidence_files"), w.S.Audit("recall.evidence.exported").Count);
            foreach (var format in new[] { "Csv", "Pdf" })
            {
                var (status, body) = await RecallCrmEvidence.CreateAsync(w, id, format, "tr");
                Assert.Equal((403, "RECALL_CUSTOMER_DIRECTORY_FORBIDDEN"), (status, RecallScenario.Code(body)));
                foreach (var key in new[] { CrmWorld.A, c }) Assert.DoesNotContain(key.ToString("D"), body.GetRawText(), StringComparison.OrdinalIgnoreCase);
            }

            _factory.Crm.Behaviour[d] = CrmBehaviour.ServiceUnavailable; // Forbidden + Unresolved is still 403
            Assert.Equal(403, (await RecallCrmEvidence.CreateAsync(w, id, "Csv", "tr")).Status);
            Assert.Equal(before, (w.Count("recall_evidence_exports"), w.Count("recall_evidence_files"), w.S.Audit("recall.evidence.exported").Count));

            // (i) the preview refuses too
            var (sp, preview, _) = await w.S.SendAsync(HttpMethod.Get, $"/api/recall/evidence/preview?caseId={id}", w.Qa);
            Assert.Equal((403, "RECALL_CUSTOMER_DIRECTORY_FORBIDDEN"), (sp, RecallScenario.Code(preview)));
            foreach (var key in new[] { CrmWorld.A, c }) Assert.DoesNotContain(key.ToString("D"), preview.GetRawText(), StringComparison.OrdinalIgnoreCase);

            // the replay of the stored export → 200, the same export, no CRM call; a period pack → 201, no CRM call; an invalid format → 400
            _factory.Crm.ResetCounts();
            var (sr, replayed) = await RecallCrmEvidence.CreateAsync(w, id, "Csv", "tr", replayKey);
            Assert.True(sr == 200, replayed.ToString());
            Assert.Equal((CrmWorld.Str(stored, "exportId"), CrmWorld.Str(stored, "sha256")), (CrmWorld.Str(replayed, "exportId"), CrmWorld.Str(replayed, "sha256")));
            var periodKey = "ex-" + Guid.NewGuid().ToString("N");
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var (period, _, _) = await w.S.SendAsync(HttpMethod.Post, "/api/recall/evidence/exports", w.Qa,
                new Dictionary<string, object?> { ["idempotencyKey"] = periodKey, ["from"] = today, ["to"] = today, ["format"] = "Csv" }, periodKey);
            Assert.Equal(201, period);
            var xmlKey = "ex-" + Guid.NewGuid().ToString("N");
            Assert.Equal(400, (await w.S.SendAsync(HttpMethod.Post, "/api/recall/evidence/exports", w.Qa,
                new Dictionary<string, object?> { ["idempotencyKey"] = xmlKey, ["caseId"] = id, ["format"] = "Xml" }, xmlKey)).Status);
            Assert.Equal(0, _factory.Crm.Calls);
        }
        finally
        {
            _factory.Crm.Behaviour.Clear();
        }
    }
}
