using System.Globalization;
using System.Text;
using Diten.InventoryService.Application.Features.CountVariances.Handlers.QueryHandlers;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using Diten.InventoryService.Domain.CycleCounting.Rules;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Application.Features.CountEvidenceExports;

/// <summary>
/// MOD-0181 DL-5 (pack §4.5, AC-43) — reads everything one count's evidence file shows, in the caller's tenant + company only: the head (number,
/// warehouse, type, blind, status, planned date, <c>SnapshotAt</c>, close, scope bins), every line (expected, movements since the snapshot, expected at
/// count, counted, variance, state), the decisions (kind, source, who, when, reason) with the ledger <c>TransactionNumber</c> of the correction, the
/// MANUAL <c>COUNT_*</c> movements on the scope bins between <c>SnapshotAt</c> and the close (pack §2.4 — the same two-way rule as the Variance
/// Trace) and the timeline. Codes stay codes here; the renderers word them in the file's language. 0181 only READS the ledger (Q-21 = A).
/// </summary>
public sealed class CountEvidenceDocumentBuilder
{
    private const int Page = CycleCountContract.MaxPageSize;
    private readonly ICycleCountRepository _repository;
    private readonly CountVarianceReads _reads;
    private readonly CycleCountSupport _support;

    public CountEvidenceDocumentBuilder(ICycleCountRepository repository, CountVarianceReads reads, CycleCountSupport support)
    {
        _repository = repository;
        _reads = reads;
        _support = support;
    }

    public async Task<CountEvidenceDocument> BuildAsync(CycleCount count, string language, string requestedLanguage, DateTimeOffset now, CancellationToken ct)
    {
        var lines = new List<CycleCountLine>();
        for (var page = 1; ; page++)
        {
            var batch = await _repository.LinesAsync(count.Id, page, Page, ct);
            lines.AddRange(batch.Items);
            if (batch.Items.Count < Page || lines.Count >= batch.Total) break;
        }

        var events = new List<CycleCountEvent>();
        for (var page = 1; ; page++)
        {
            var batch = await _repository.EventsAsync(count.Id, page, Page, ct);
            events.AddRange(batch.Items);
            if (batch.Items.Count < Page || events.Count >= batch.Total) break;
        }

        var closedAt = events.Where(e => e.Kind == CycleCountEventKind.Closed).Select(e => (DateTimeOffset?)e.At).LastOrDefault();
        var manualTo = closedAt ?? now;
        var manual = count.SnapshotAt is { } from && count.ScopeBinIds.Count > 0
            ? await _reads.ManualMovementsAsync(count.ScopeBinIds, from, manualTo, ct)
            : [];

        var locationIds = count.ScopeBinIds.Concat(lines.Select(l => l.LocationId)).Append(count.WarehouseId)
            .Concat(manual.Select(m => Guid.TryParse(m.MovementType == MovementType.CountGain ? m.ToLocationId : m.FromLocationId, out var id) ? id : Guid.Empty))
            .Where(id => id != Guid.Empty).Distinct().ToList();
        var names = (await _repository.LocationNamesAsync(locationIds, ct)).ToDictionary(n => n.Id);
        string Place(Guid id) => names.TryGetValue(id, out var n) ? Named(n.Code, n.Name) : id.ToString("D");
        string PlaceText(string? id) => Guid.TryParse(id, out var g) ? Place(g) : id ?? "—";

        var header = new CountEvidenceHeader(
            count.Number,
            Place(count.WarehouseId),
            count.CountType.ToString(),
            count.Blind,
            count.Status.ToString(),
            count.PlannedDate,
            count.SnapshotAt,
            closedAt,
            count.ScopeBinIds.Select(Place).ToList(),
            count.SnapshotAt,
            manualTo);

        var rows = lines.OrderBy(l => Place(l.LocationId), StringComparer.Ordinal).ThenBy(l => l.SkuId).ThenBy(l => l.LotId, StringComparer.Ordinal)
            .Select(l => new CountEvidenceLineRow(
                Place(l.LocationId),
                l.SkuId.ToString("D"),
                l.LotId,
                l.Origin.ToString(),
                Q(l.ExpectedQuantity),
                l.MovementsSinceSnapshot is { } since ? Q(since) : null,
                l.ExpectedAtCount is { } atCount ? Q(atCount) : null,
                l.CountedQuantity is { } counted ? Q(counted) : null,
                l.VarianceQuantity is { } variance ? Q(variance) : null,
                l.Status.ToString(),
                l.Decision?.Kind.ToString(),
                l.Decision?.Source.ToString(),
                l.Decision?.By?.ToString("D"),
                l.Decision?.At,
                l.Decision?.Reason,
                l.Adjustment?.Status.ToString(),
                l.Adjustment?.TransactionNumber))
            .ToList();

        var manualRows = manual.OrderBy(m => m.CreatedAt).ThenBy(m => m.TransactionNumber, StringComparer.Ordinal).Select(m =>
        {
            var gain = m.MovementType == MovementType.CountGain;
            return new CountEvidenceManualRow(m.CreatedAt, PlaceText(gain ? m.ToLocationId : m.FromLocationId), m.SkuId.ToString("D"), m.LotId,
                WireNames.ToWire(m.MovementType), InventoryQuantity.ToWire(m.Quantity), m.TransactionNumber, m.SourceModule, m.SourceType,
                m.CreatedBy.ToString("D"), CycleCountDecisionRules.DeclaresCount(CountVarianceReads.Facts(m)));
        }).ToList();

        var timeline = events.OrderBy(e => e.Seq).Select(e => new CountEvidenceEventRow(e.Seq, e.At, e.Kind.ToString(), e.ActorUserId?.ToString("D"))).ToList();
        return new CountEvidenceDocument(language, requestedLanguage, now, _support.LegalEntityId.ToString("D"), header, rows, manualRows, timeline);
    }

    private static string Named(string code, string name)
        => string.IsNullOrWhiteSpace(name) || string.Equals(code, name, StringComparison.Ordinal) ? code : $"{code} · {name}";

    private static string Q(decimal value) => InventoryQuantity.ToWire(value);

}

/// <summary>
/// The worded sections of one evidence file — ONE source for the PDF and the CSV, so both carry the same content (Q-18 = A, AC-43). Every label
/// and coded value comes from <see cref="CycleCountEvidenceText"/> in the document's language; quantities, numbers, ids and reasons as stored.
/// </summary>
public static class CountEvidenceSections
{
    public sealed record Section(string Title, string[] Header, IReadOnlyList<string[]> Rows);

    public static string Title(CountEvidenceDocument d) => CycleCountEvidenceText.Of("CountPdf.Title", d.Language);

    /// <summary>The head of the file as label / value pairs.</summary>
    public static IReadOnlyList<(string Label, string Value)> Meta(CountEvidenceDocument d)
    {
        var l = d.Language;
        string P(string key) => CycleCountEvidenceText.Of("CountPdf." + key, l);
        var h = d.Header;
        return
        [
            (P("Count"), h.Number),
            (P("Company"), d.Company),
            (P("Warehouse"), h.Warehouse),
            (P("Type"), CycleCountEvidenceText.Value("CycleCount.Type.", h.CountType, l)),
            (P("Blind"), h.Blind ? P("Yes") : P("No")),
            (P("Status"), CycleCountEvidenceText.Value(CycleCountTextKeys.StatusPrefix, h.Status, l)),
            (P("PlannedDate"), h.PlannedDate),
            (P("SnapshotAt"), Utc(h.SnapshotAt)),
            (P("ClosedAt"), h.ClosedAt is { } closed ? Utc(closed) : P("NotClosed")),
            (P("ScopeBins"), h.ScopeBins.Count == 0 ? "—" : string.Join(", ", h.ScopeBins)),
            (P("ManualWindow"), $"{Utc(h.ManualFrom)} – {Utc(h.ManualTo)}"),
            (P("Generated"), Utc(d.GeneratedAt))
        ];
    }

    public static IReadOnlyList<Section> Build(CountEvidenceDocument d)
    {
        var l = d.Language;
        string P(string key) => CycleCountEvidenceText.Of("CountPdf." + key, l);
        string V(string prefix, string? code) => code is null ? "—" : CycleCountEvidenceText.Value(prefix, code, l);
        static string D(string? value) => string.IsNullOrEmpty(value) ? "—" : value;

        var decided = d.Lines.Where(r => r.DecisionKind is not null || r.AdjustmentStatus is not null).ToList();
        return
        [
            new Section($"1. {P("Lines")} ({d.Lines.Count})",
                [P("Bin"), P("Sku"), P("Lot"), P("Origin"), P("Expected"), P("MovementsSinceSnapshot"), P("ExpectedAtCount"), P("Counted"), P("Variance"), P("LineStatus")],
                d.Lines.Select(r => new[]
                {
                    r.Bin, r.Sku, D(r.Lot), V("CountPdf.Origin.", r.Origin), r.Expected, D(r.MovementsSinceSnapshot), D(r.ExpectedAtCount), D(r.Counted),
                    D(r.Variance), V(CycleCountTextKeys.LineStatusPrefix, r.Status)
                }).ToList()),
            new Section($"2. {P("Decisions")} ({decided.Count})",
                [P("Bin"), P("Sku"), P("Lot"), P("Decision"), P("Source"), P("By"), P("At"), P("Reason"), P("AdjustmentStatus"), P("LedgerNumber")],
                decided.Select(r => new[]
                {
                    r.Bin, r.Sku, D(r.Lot), V(CycleCountTextKeys.DecisionPrefix, r.DecisionKind), V(CycleCountTextKeys.DecisionSourcePrefix, r.DecisionSource),
                    D(r.DecidedBy), r.DecidedAt is { } at ? Utc(at) : "—", D(r.Reason), V("CycleCount.Adjustment.", r.AdjustmentStatus), D(r.TransactionNumber)
                }).ToList()),
            new Section($"3. {P("Manual")} ({d.Manual.Count})",
                [P("At"), P("Bin"), P("Sku"), P("Lot"), P("MovementType"), P("Quantity"), P("LedgerNumber"), P("DeclaredSource"), P("User"), P("Flag")],
                d.Manual.Select(r => new[]
                {
                    Utc(r.At), r.Bin, r.Sku, D(r.Lot), V("MovementType.", r.MovementType), r.Quantity, r.TransactionNumber,
                    $"{D(r.SourceModule)} / {D(r.SourceType)}", r.CreatedBy, r.SourceMismatch ? P("SourceMismatch") : "—"
                }).ToList()),
            new Section($"4. {P("Timeline")} ({d.Timeline.Count})",
                [P("Seq"), P("At"), P("Event"), P("User")],
                d.Timeline.Select(r => new[] { r.Seq.ToString(CultureInfo.InvariantCulture), Utc(r.At), V("CycleCount.Event.", r.Kind), D(r.Actor) }).ToList())
        ];
    }

    public static string Utc(DateTimeOffset? value)
        => value is { } v ? v.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC" : "—";
}

/// <summary>
/// The Evidence Pack CSV (pack §4.5): the head as label;value rows, then the four sections — each a title row, a header row and its rows — from
/// <see cref="CountEvidenceSections"/>, so it carries exactly what the PDF carries. UTF-8 with BOM, CRLF, ';' separated; a formula-looking cell is
/// neutralised (CSV injection — the MOD-0176 rule). The CSV has no font limit: it is written in the requested language.
/// </summary>
public static class CountEvidenceCsv
{
    public static byte[] Render(CountEvidenceDocument d)
    {
        var text = new StringBuilder();
        void Row(IEnumerable<string?> cells) => text.Append(string.Join(';', cells.Select(Cell))).Append("\r\n");

        Row([CountEvidenceSections.Title(d)]);
        foreach (var (label, value) in CountEvidenceSections.Meta(d)) Row([label, value]);
        foreach (var section in CountEvidenceSections.Build(d))
        {
            text.Append("\r\n");
            Row([section.Title]);
            Row(section.Header);
            if (section.Rows.Count == 0) Row([CycleCountEvidenceText.Of("CountPdf.None", d.Language)]);
            foreach (var r in section.Rows) Row(r);
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text.ToString())];
    }

    private static string Cell(string? value)
    {
        var v = value ?? string.Empty;
        // a negative quantity stays a number; any other leading '-' (and '=', '+', '@') is neutralised
        if (v.Length > 0 && (v[0] is '=' or '+' or '@' || (v[0] == '-' && !decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out _)))) v = "'" + v;
        return v.IndexOfAny([';', '"', '\r', '\n']) >= 0 ? "\"" + v.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : v;
    }
}
