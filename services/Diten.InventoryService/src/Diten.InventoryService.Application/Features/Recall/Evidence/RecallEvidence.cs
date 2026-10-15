using System.Globalization;
using System.Text;
using System.Text.Json;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Application.Features.Recall.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Enums;
using Diten.InventoryService.Domain.Recall.Repositories;
using Diten.InventoryService.Domain.Recall.Rules;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Application.Features.Recall.Evidence;

/// <summary>One table of a section: column keys (the Web words them in 7 languages; the PDF / CSV in tr | en) and string cells.</summary>
public sealed record RecallEvidenceTable(string Key, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>One of the seven sections (pack §4.13, proposal §2.9). <see cref="Applicable"/> false = "uygulanmaz" (a drill or a period).</summary>
public sealed record RecallEvidenceSection(string Key, bool Applicable, IReadOnlyList<RecallEvidenceTable> Tables, string? Note = null);

/// <summary>
/// WP-0177-R7b — the content of ONE evidence pack: what the preview shows and what the PDF / CSV print, from the same object (the mockup's
/// "önizleme dışa aktarılacak içeriğin aynısıdır"). Values are the records' own (codes, ids, invariant numbers, UTC times); nothing is
/// made up — a value that cannot be read is left empty and said so.
/// </summary>
public sealed record RecallEvidenceDocument(
    RecallEvidenceSubjectKind SubjectKind,
    Guid? CaseId,
    Guid? RunId,
    DateOnly? From,
    DateOnly? To,
    string SubjectLabel,
    Guid LegalEntityId,
    DateTimeOffset GeneratedAtUtc,
    string Language,
    IReadOnlyList<RecallEvidenceSection> Sections)
{
    public IReadOnlyList<string> SectionKeys => Sections.Where(s => s.Applicable).Select(s => s.Key).ToList();
}

/// <summary>The section keys of the contract (RECALL <c>EvidencePreview.sections</c>), in order.</summary>
public static class RecallEvidenceSections
{
    public const string Summary = "1_summary";
    public const string Trace = "2_trace";
    public const string Quarantine = "3_quarantine";
    public const string Customers = "4_customers";
    public const string ReturnsAndEffectiveness = "5_returnsAndEffectiveness";
    public const string Timeline = "6_timeline";
    public const string Signatures = "7_signatures";

    public static readonly IReadOnlyList<string> All = [Summary, Trace, Quarantine, Customers, ReturnsAndEffectiveness, Timeline, Signatures];

    /// <summary>A signature row whose MOD-0022 record could not be read: status <c>Unreadable:{outcome}</c>, signer / meaning / time EMPTY.</summary>
    public const string Unreadable = "Unreadable";

    public const int MaxPeriodDays = 366;
}

/// <summary>
/// The labels of the PDF and the CSV in the reader's language — tr or en (any other → en): the FU-0176-PDF-L10N limit of the 0176 Kanıt Paketi,
/// kept for the recall pack. The screen does not use them (it words the column keys in its 7 languages).
/// </summary>
public static class RecallEvidenceLabels
{
    private static readonly Dictionary<string, (string Tr, string En)> Words = new(StringComparer.Ordinal)
    {
        ["title"] = ("Geri çağırma kanıt paketi", "Recall evidence pack"),
        ["subject"] = ("Konu", "Subject"),
        ["company"] = ("Şirket", "Company"),
        ["generated"] = ("Oluşturma", "Generated"),
        ["page"] = ("Sayfa", "Page"),
        ["notApplicable"] = ("Uygulanmaz", "Not applicable"),
        ["notApplicableDrill"] = ("Uygulanmaz — tatbikat salt-okunur provadır", "Not applicable — a drill is a read-only rehearsal"),
        ["notApplicablePeriod"] = ("Uygulanmaz — dönem raporu yalnız tatbikat listesidir", "Not applicable — a period report lists the drills only"),
        ["notSealed"] = ("Kapsam mühürlenmedi; izleme anlık görüntüsü yok", "The scope is not sealed; no trace snapshot"),
        ["none"] = ("Kayıt yok", "No record"),
        ["kind.Case"] = ("Gerçek vaka", "Recall case"),
        ["kind.DrillRun"] = ("Tatbikat", "Drill"),
        ["kind.Period"] = ("Dönem", "Period"),
        [RecallEvidenceSections.Summary] = ("1 · Özet", "1 · Summary"),
        [RecallEvidenceSections.Trace] = ("2 · İzleme sonucu", "2 · Trace result"),
        [RecallEvidenceSections.Quarantine] = ("3 · Karantina istekleri ve sonuçları", "3 · Quarantine requests and outcomes"),
        [RecallEvidenceSections.Customers] = ("4 · Müşteri ve bilgilendirme", "4 · Customers and notification"),
        [RecallEvidenceSections.ReturnsAndEffectiveness] = ("5 · Geri dönüşler ve etkinlik kontrolü", "5 · Returns and effectiveness check"),
        [RecallEvidenceSections.Timeline] = ("6 · Zaman çizelgesi", "6 · Timeline"),
        [RecallEvidenceSections.Signatures] = ("7 · İmzalar", "7 · Signatures"),
        // table / column keys
        ["field"] = ("Alan", "Field"), ["value"] = ("Değer", "Value"), ["nodeKind"] = ("Düğüm", "Node"), ["lot"] = ("Lot", "Lot"),
        ["place"] = ("Depo / müşteri", "Warehouse / customer"), ["quantity"] = ("Miktar", "Quantity"), ["unit"] = ("Birim", "Unit"),
        ["document"] = ("Belge", "Document"), ["date"] = ("Tarih", "Date"), ["warehouse"] = ("Depo", "Warehouse"), ["location"] = ("Lokasyon", "Location"),
        ["status"] = ("Durum", "Status"), ["reason"] = ("Neden", "Reason"), ["held"] = ("Tutulan", "Held"), ["holds"] = ("Karantina kaydı", "Quarantine record"),
        ["customer"] = ("Müşteri", "Customer"), ["notifiedOn"] = ("Bilgilendirme", "Notified on"), ["channel"] = ("Kanal", "Channel"), ["note"] = ("Not", "Note"),
        ["transaction"] = ("Hareket", "Movement"), ["sku"] = ("SKU", "SKU"), ["shipped"] = ("Sevk", "Shipped"), ["returned"] = ("Geri dönen", "Returned"),
        ["consumed"] = ("Tüketildi", "Consumed"), ["open"] = ("Açık", "Open"), ["percent"] = ("Etkinlik %", "Effectiveness %"), ["target"] = ("Hedef %", "Target %"),
        ["met"] = ("Hedefte", "On target"), ["seq"] = ("Sıra", "No."), ["at"] = ("Zaman", "Time"), ["event"] = ("Olay", "Event"), ["actor"] = ("Kişi", "User"),
        ["text"] = ("Açıklama", "Text"), ["signatureId"] = ("İmza", "Signature"), ["action"] = ("İşlem", "Action"), ["signer"] = ("İmzalayan", "Signer"),
        ["meaning"] = ("Anlam", "Meaning"), ["signedAt"] = ("İmza zamanı", "Signed at"), ["run"] = ("Tatbikat", "Drill"), ["started"] = ("Başlangıç", "Started"),
        ["duration"] = ("Süre (dk)", "Duration (min)"), ["targetHours"] = ("Hedef (sa)", "Target (h)"), ["passed"] = ("Sonuç", "Result"),
        ["returns"] = ("Geri dönüşler", "Returns"), ["effectiveness"] = ("Etkinlik", "Effectiveness"), ["summary"] = ("Özet", "Summary"), ["runs"] = ("Tatbikatlar", "Drills"),
        ["trace"] = ("İzleme", "Trace"), ["requests"] = ("İstekler", "Requests"), ["customers"] = ("Müşteriler", "Customers"), ["timeline"] = ("Olaylar", "Events"),
        ["signatures"] = ("İmzalar", "Signatures"),
        // summary fields
        ["number"] = ("Numara", "Number"), ["class"] = ("Sınıf", "Class"), ["level"] = ("Düzey", "Level"), ["itemId"] = ("Ürün", "Item"),
        ["skuId"] = ("SKU", "SKU"), ["rootLot"] = ("Kök lot", "Root lot"), ["reasonReference"] = ("Neden referansı", "Reason reference"),
        ["openedBy"] = ("Açan", "Opened by"), ["openedAtUtc"] = ("Açılış", "Opened at"), ["direction"] = ("Yön", "Direction"),
        ["scopeRevision"] = ("Kapsam revizyonu", "Scope revision"), ["snapshotSha256"] = ("İzleme mührü (SHA-256)", "Trace seal (SHA-256)"),
        ["scopeApprovedBy"] = ("Kapsamı onaylayan", "Scope approved by"), ["scopeApprovedAtUtc"] = ("Kapsam onayı", "Scope approved at"),
        ["effectivenessCompletedBy"] = ("Etkinliği tamamlayan", "Effectiveness completed by"), ["effectivenessCompletedAtUtc"] = ("Etkinlik tamamlandı", "Effectiveness completed at"),
        ["closedBy"] = ("Kapatan", "Closed by"), ["closedAtUtc"] = ("Kapanış", "Closed at"), ["closureSummary"] = ("Kapanış özeti", "Closure summary"),
        ["openQuantityAcceptance"] = ("Açık miktar kabulü", "Open quantity acceptance"), ["cancelledBy"] = ("İptal eden", "Cancelled by"),
        ["cancelReason"] = ("İptal gerekçesi", "Cancellation reason"), ["planCode"] = ("Plan", "Plan"), ["selectionMode"] = ("Lot seçimi", "Lot selection"),
        ["startedAtUtc"] = ("Başlangıç", "Started at"), ["endedAtUtc"] = ("Bitiş", "Ended at"), ["durationMinutes"] = ("Süre (dk)", "Duration (min)"),
        ["gapCount"] = ("İzleme boşluğu", "Trace gaps"), ["resultNote"] = ("Sonuç notu", "Result note"), ["from"] = ("Başlangıç tarihi", "From"),
        ["to"] = ("Bitiş tarihi", "To"), ["runCount"] = ("Tatbikat sayısı", "Drill count")
    };

    public static string Normal(string? language) => language is not null && language.StartsWith("tr", StringComparison.OrdinalIgnoreCase) ? "tr" : "en";

    public static string Of(string key, string language) => Words.TryGetValue(key, out var w) ? (language == "tr" ? w.Tr : w.En) : key;
}

/// <summary>The CSV of a pack: UTF-8 with BOM, ';' separated; per section its title line, then per table its header and rows (the 0176 F5 form).</summary>
public static class RecallEvidenceCsv
{
    public static byte[] Render(RecallEvidenceDocument d)
    {
        var l = d.Language;
        var sb = new StringBuilder();
        void Line(IEnumerable<string> cells) => sb.Append(string.Join(';', cells.Select(Cell))).Append("\r\n");
        Line([RecallEvidenceLabels.Of("title", l), d.SubjectLabel, RecallEvidenceLabels.Of("kind." + d.SubjectKind, l)]);
        Line([RecallEvidenceLabels.Of("company", l), d.LegalEntityId.ToString("D"), RecallEvidenceLabels.Of("generated", l), RecallSupport.Wire(d.GeneratedAtUtc)]);
        foreach (var section in d.Sections)
        {
            Line([RecallEvidenceLabels.Of(section.Key, l)]);
            if (!section.Applicable)
            {
                Line([RecallEvidenceLabels.Of(section.Note ?? "notApplicable", l)]);
                continue;
            }

            if (section.Note is not null) Line([RecallEvidenceLabels.Of(section.Note, l)]);
            foreach (var table in section.Tables)
            {
                Line(table.Columns.Select(c => RecallEvidenceLabels.Of(c, l)));
                foreach (var row in table.Rows) Line(row);
            }
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }

    private static string Cell(string? value)
    {
        var v = value ?? string.Empty;
        return v.IndexOfAny([';', '"', '\r', '\n']) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
    }
}

/// <summary>
/// WP-0177-R7b — builds the pack (pack §4.13, proposal §2.9) in the CALLER'S COMPANY: every read below is the company-bound recall / drill /
/// ledger / 0175 read; another company's case or run is not found (the controller answers 403). Section 7 reads each signature from MOD-0022
/// NOW (the caller's token); an unreadable one is listed as <see cref="RecallEvidenceSections.Unreadable"/> with empty signer / meaning / time.
/// </summary>
public sealed class RecallEvidenceBuilder
{
    private readonly IRecallRepository _repository;
    private readonly IRecallDrillRepository _drills;
    private readonly IRecallTraceSource _ledger;
    private readonly IRecallQuarantineRequester _quarantine;
    private readonly IRecallESignClient _esign;
    private readonly RecallSubjectFacts _facts;
    private readonly RecallSupport _support;

    public RecallEvidenceBuilder(IRecallRepository repository, IRecallDrillRepository drills, IRecallTraceSource ledger, IRecallQuarantineRequester quarantine,
        IRecallESignClient esign, RecallSubjectFacts facts, RecallSupport support)
    {
        _repository = repository;
        _drills = drills;
        _ledger = ledger;
        _quarantine = quarantine;
        _esign = esign;
        _facts = facts;
        _support = support;
    }

    /// <summary>Exactly one subject: caseId, or runId, or from + to (≤ 366 days). (null, error, status) on a refusal.</summary>
    public async Task<(RecallEvidenceDocument? Document, string? Code, string? Error, int Status)> BuildAsync(
        string? caseId, string? runId, string? from, string? to, string language, CancellationToken ct)
    {
        var lang = RecallEvidenceLabels.Normal(language);
        var given = new[] { caseId, runId, from ?? to }.Count(v => !string.IsNullOrWhiteSpace(v));
        if (given != 1) return (null, Common.InventoryErrorCodes.InvalidRequest, "Give exactly one subject: caseId, runId, or from + to.", 400);
        if (!string.IsNullOrWhiteSpace(caseId))
        {
            if (!RecallSupport.TryParseId(caseId, out var id) || await _repository.FindCaseAsync(id, ct) is not { } c)
                return (null, RecallErrorCodes.CaseNotFound, "Recall case not found.", 404);
            return (await CaseAsync(c, lang, ct), null, null, 200);
        }

        if (!string.IsNullOrWhiteSpace(runId))
        {
            if (!RecallSupport.TryParseId(runId, out var id) || await _drills.FindRunAsync(id, ct) is not { } run)
                return (null, RecallErrorCodes.DrillNotFound, "Drill run not found.", 404);
            return (await RunAsync(run, lang, ct), null, null, 200);
        }

        if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
            || !DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
        {
            return (null, Common.InventoryErrorCodes.InvalidRequest, "from and to are dates (yyyy-MM-dd).", 400);
        }

        if (end < start || end.DayNumber - start.DayNumber + 1 > RecallEvidenceSections.MaxPeriodDays)
            return (null, RecallErrorCodes.EvidenceRangeInvalid, $"The period must be 1-{RecallEvidenceSections.MaxPeriodDays} days.", 400);
        return (await PeriodAsync(start, end, lang, ct), null, null, 200);
    }

    // ── a recall case: the seven sections ────────────────────────────────────────────────────────────────

    private async Task<RecallEvidenceDocument> CaseAsync(RecallCase c, string lang, CancellationToken ct)
    {
        var now = _support.Now();
        var snapshotId = c.ApprovedSnapshotId ?? c.SubmittedSnapshotId;
        var snapshot = snapshotId is { } sid ? await _repository.FindSnapshotAsync(sid, ct) : null;
        var items = await _repository.ScopeItemsAsync(c.Id, c.ApprovedScope?.Revision ?? c.ScopeRevision, ct);
        var events = await _repository.AllEventsAsync(c.Id, ct);

        var summary = new List<IReadOnlyList<string>>
        {
            F("number", c.Number), F("status", c.Status.ToString()), F("class", I(c.Class)), F("level", c.Level.ToString()), F("itemId", c.ItemId.ToString("D")),
            F("skuId", c.SkuId.ToString("D")), F("rootLot", c.RootLotNumber), F("reason", c.Reason), F("reasonReference", c.ReasonReference),
            F("openedBy", c.OwnerUserId.ToString("D")), F("openedAtUtc", RecallSupport.Wire(c.OpenedAtUtc)), F("direction", c.Direction.ToString()),
            F("scopeRevision", I(c.ScopeRevision)), F("snapshotSha256", snapshot?.Sha256),
            F("scopeApprovedBy", c.ScopeApprovedBy?.ToString("D")), F("scopeApprovedAtUtc", RecallSupport.Wire(c.ScopeApprovedAtUtc)),
            F("effectivenessCompletedBy", c.EffectivenessCompletedBy?.ToString("D")), F("effectivenessCompletedAtUtc", RecallSupport.Wire(c.EffectivenessCompletedAtUtc)),
            F("closedBy", c.ClosedBy?.ToString("D")), F("closedAtUtc", RecallSupport.Wire(c.ClosedAtUtc)), F("closureSummary", c.ClosureSummary),
            F("openQuantityAcceptance", c.OpenQuantityAcceptance), F("cancelledBy", c.CancelledBy?.ToString("D")), F("cancelReason", c.CancelReason)
        };

        var requests = await _repository.QuarantineRequestsAsync(c.Id, ct);
        var holds = (await _quarantine.HoldsAsync(requests.SelectMany(r => r.HoldIds).Distinct(StringComparer.Ordinal).ToList(), ct))
            .ToDictionary(h => h.HoldId, StringComparer.Ordinal);
        var quarantine = requests.Select(r => Row(r.LotNumber, r.WarehouseId, r.LocationId, r.Status.ToString(), r.NotNeededReason?.ToString(), D(r.HeldQuantity),
            r.BaseUomId, string.Join(", ", r.HoldIds.Select(h => holds.TryGetValue(h, out var v) ? v.HoldNumber : h)))).ToList();

        var notifications = await _repository.NotificationsAsync(c.Id, ct);
        var superseded = notifications.Where(n => n.SupersedesId is not null).Select(n => n.SupersedesId!.Value).ToHashSet();
        var customers = RecallReads.Shipments(items).Select(s => s.CustomerRef!).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal)
            .Select(customer =>
            {
                var n = notifications.Where(x => x.CustomerRef == customer && !superseded.Contains(x.Id)).OrderBy(x => x.RecordedAtUtc).LastOrDefault();
                return Row(customer, n is null ? null : RecallSupport.WireDate(n.NotifiedOn), n?.Channel.ToString(), n?.Note);
            }).ToList();

        var links = await _repository.ReturnLinksAsync(c.Id, ct);
        var returns = new List<IReadOnlyList<string>>();
        foreach (var link in links)
        {
            var hold = await _quarantine.HoldOfMovementAsync(link.TransactionId, ct);
            returns.Add(Row(link.TransactionNumber ?? link.TransactionId, link.CustomerRef, link.LotNumber, D(link.Quantity), link.BaseUomId, hold?.HoldNumber,
                RecallSupport.Wire(link.PostedAtUtc)));
        }

        var effectiveness = c.Effectiveness ?? await _facts.LiveEffectivenessAsync(c, null, now, ct);
        var effRows = effectiveness.Totals.Select(t => Row(t.SkuId.ToString("D"), t.BaseUomId, D(t.Shipped), D(t.Returned), D(t.Consumed), D(t.Open), D(t.Percent),
            D(effectiveness.TargetPercent), t.Met ? "true" : "false")).ToList();

        var timeline = events.Select((e, i) => Row(I(i + 1), RecallSupport.Wire(e.AtUtc), e.Kind.ToString(), e.ActorUserId?.ToString("D"), e.Text, e.SignatureId)).ToList();

        var signed = new List<(string Action, string Id)>();
        void Add(string action, string? id)
        {
            if (!string.IsNullOrWhiteSpace(id) && signed.All(s => s.Id != id)) signed.Add((action, id));
        }

        foreach (var e in events.Where(e => e.SignatureId is not null))
        {
            Add(e.Kind switch
            {
                RecallCaseEventKind.EffectivenessCompleted => "recall.effectiveness.complete",
                RecallCaseEventKind.Closed => "recall.case.close",
                _ => "recall.scope.approve"
            }, e.SignatureId);
        }

        Add("recall.scope.approve", c.ScopeApprovalSignatureId);
        Add("recall.effectiveness.complete", c.EffectivenessSignatureId);
        Add("recall.case.close", c.ClosureSignatureId);

        var sections = new List<RecallEvidenceSection>
        {
            new(RecallEvidenceSections.Summary, true, [new("summary", ["field", "value"], summary)]),
            await TraceSectionAsync(snapshot, items, ct),
            new(RecallEvidenceSections.Quarantine, true, [new("requests", ["lot", "warehouse", "location", "status", "reason", "held", "unit", "holds"], quarantine)]),
            new(RecallEvidenceSections.Customers, true, [new("customers", ["customer", "notifiedOn", "channel", "note"], customers)]),
            new(RecallEvidenceSections.ReturnsAndEffectiveness, true,
            [
                new("returns", ["transaction", "customer", "lot", "quantity", "unit", "holds", "date"], returns),
                new("effectiveness", ["sku", "unit", "shipped", "returned", "consumed", "open", "percent", "target", "met"], effRows)
            ]),
            new(RecallEvidenceSections.Timeline, true, [new("timeline", ["seq", "at", "event", "actor", "text", "signatureId"], timeline)]),
            await SignaturesAsync(signed, ct)
        };
        return new RecallEvidenceDocument(RecallEvidenceSubjectKind.Case, c.Id, null, null, null, c.Number, c.LegalEntityId ?? Guid.Empty, now, lang, sections);
    }

    // ── a drill run: 1, 2, 7 (3–6 not applicable — a read-only rehearsal) ─────────────────────────────────

    private async Task<RecallEvidenceDocument> RunAsync(RecallDrillRun run, string lang, CancellationToken ct)
    {
        var snapshot = run.SnapshotId is { } sid ? await _repository.FindSnapshotAsync(sid, ct) : null;
        var summary = new List<IReadOnlyList<string>>
        {
            F("number", run.Number), F("planCode", run.PlanCode), F("status", run.Status.ToString()), F("selectionMode", run.SelectionMode.ToString()),
            F("rootLot", run.LotNumber), F("startedAtUtc", RecallSupport.Wire(run.StartedAtUtc)), F("endedAtUtc", RecallSupport.Wire(run.EndedAtUtc)),
            F("durationMinutes", run.DurationMinutes is { } m ? I(m) : null), F("targetHours", I(run.TargetHours)),
            F("passed", run.Passed is { } p ? (p ? "true" : "false") : null), F("gapCount", I(run.GapCount)), F("snapshotSha256", run.SnapshotSha256 ?? snapshot?.Sha256),
            F("resultNote", run.ResultNote)
        };
        var signed = new List<(string Action, string Id)>();
        if (!string.IsNullOrWhiteSpace(run.ResultSignatureId)) signed.Add(("recall.drill.result", run.ResultSignatureId));
        var sections = new List<RecallEvidenceSection>
        {
            new(RecallEvidenceSections.Summary, true, [new("summary", ["field", "value"], summary)]),
            await TraceSectionAsync(snapshot, [], ct),
            NotApplicable(RecallEvidenceSections.Quarantine, "notApplicableDrill"),
            NotApplicable(RecallEvidenceSections.Customers, "notApplicableDrill"),
            NotApplicable(RecallEvidenceSections.ReturnsAndEffectiveness, "notApplicableDrill"),
            NotApplicable(RecallEvidenceSections.Timeline, "notApplicableDrill"),
            await SignaturesAsync(signed, ct)
        };
        return new RecallEvidenceDocument(RecallEvidenceSubjectKind.DrillRun, null, run.Id, null, null, run.Number, run.LegalEntityId ?? Guid.Empty, _support.Now(), lang, sections);
    }

    // ── a period: the company's drill runs in it ─────────────────────────────────────────────────────────

    private async Task<RecallEvidenceDocument> PeriodAsync(DateOnly from, DateOnly to, string lang, CancellationToken ct)
    {
        var runs = await _drills.RunsStartedBetweenAsync(new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), ct);
        var rows = runs.Select(r => Row(r.Number, RecallSupport.Wire(r.StartedAtUtc), r.LotNumber, r.DurationMinutes is { } m ? I(m) : null, I(r.TargetHours),
            r.Passed is { } p ? (p ? "true" : "false") : null, r.Status.ToString())).ToList();
        var label = $"{Day(from)} – {Day(to)}";
        var sections = new List<RecallEvidenceSection>
        {
            new(RecallEvidenceSections.Summary, true,
            [
                new("summary", ["field", "value"], [F("from", Day(from)), F("to", Day(to)), F("runCount", I(runs.Count))]),
                new("runs", ["run", "started", "lot", "duration", "targetHours", "passed", "status"], rows)
            ])
        };
        sections.AddRange(RecallEvidenceSections.All.Skip(1).Select(k => NotApplicable(k, "notApplicablePeriod")));
        return new RecallEvidenceDocument(RecallEvidenceSubjectKind.Period, null, null, from, to, label, _support.LegalEntityId, _support.Now(), lang, sections);
    }

    // ── shared sections ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Section 2 — the nodes of the sealed snapshot (the rule-10 order of its canonical form), the customer of a shipment from the scope, and the
    /// posting date of the node's movement read from the ledger (the canonical form carries no date). No snapshot → the section says so.
    /// </summary>
    private async Task<RecallEvidenceSection> TraceSectionAsync(RecallTraceSnapshot? snapshot, IReadOnlyList<RecallScopeItem> items, CancellationToken ct)
    {
        if (snapshot is null) return new RecallEvidenceSection(RecallEvidenceSections.Trace, true, [new("trace", TraceColumns, [])], "notSealed");
        using var json = JsonDocument.Parse(snapshot.Canonical);
        var nodes = json.RootElement.GetProperty("nodes").EnumerateArray().ToList();
        string? S(JsonElement n, string name) => n.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        var lotIds = nodes.Select(n => S(n, "lotId")).Where(v => Guid.TryParse(v, out _)).Select(v => Guid.Parse(v!)).Distinct().ToList();
        var movements = await _ledger.TransactionsOfLotsAsync(lotIds, ct);
        var customers = items.Where(i => i.CustomerRef is not null).ToDictionary(i => i.NodeKey, i => i.CustomerRef!, StringComparer.Ordinal);
        var rows = nodes.Select(n =>
        {
            var key = S(n, "key") ?? string.Empty;
            var kind = S(n, "kind") ?? string.Empty;
            var gap = S(n, "gap");
            var document = S(n, "documentId");
            var type = S(n, "movementType");
            var lot = S(n, "lotId");
            var date = movements.Where(t => t.LotId == lot && t.SourceDocumentId == document && document is not null && WireNames.ToWire(t.Type) == type)
                .Select(t => (DateTimeOffset?)t.PostingDate).Min();
            var place = customers.TryGetValue(key, out var customer) ? customer : string.Join(" · ", new[] { S(n, "warehouseId"), S(n, "locationId") }.Where(v => v is not null));
            return Row(gap is null ? kind : $"{kind}:{gap}", S(n, "lotNumber"), place, S(n, "quantity"), S(n, "baseUomId"), document, date is { } d ? RecallSupport.Wire(d) : null);
        }).ToList();
        return new RecallEvidenceSection(RecallEvidenceSections.Trace, true, [new("trace", TraceColumns, rows)]);
    }

    private static readonly IReadOnlyList<string> TraceColumns = ["nodeKind", "lot", "place", "quantity", "unit", "document", "date"];

    /// <summary>Section 7 — each signature read from MOD-0022 at generation time; unreadable → the outcome, NOTHING filled in.</summary>
    private async Task<RecallEvidenceSection> SignaturesAsync(IReadOnlyList<(string Action, string Id)> signed, CancellationToken ct)
    {
        var rows = new List<IReadOnlyList<string>>();
        foreach (var (action, id) in signed)
        {
            var read = await _esign.SignatureAsync(id, ct);
            rows.Add(read.IsOk
                ? Row(action, id, read.Value!.SignerUserId, read.Value.MeaningCode, RecallSupport.Wire(read.Value.SignedAtUtc), read.Value.Invalidated ? "Invalidated" : "Ok")
                : Row(action, id, null, null, null, $"{RecallEvidenceSections.Unreadable}:{read.Outcome}"));
        }

        return new RecallEvidenceSection(RecallEvidenceSections.Signatures, true, [new("signatures", ["action", "signatureId", "signer", "meaning", "signedAt", "status"], rows)]);
    }

    private static RecallEvidenceSection NotApplicable(string key, string note) => new(key, false, [], note);

    private static IReadOnlyList<string> F(string field, string? value) => [field, value ?? string.Empty];

    private static IReadOnlyList<string> Row(params string?[] cells) => cells.Select(c => c ?? string.Empty).ToList();

    private static string Day(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string D(decimal value) => TraceRulesDec(value);

    private static string D(decimal? value) => value is { } v ? TraceRulesDec(v) : string.Empty;

    private static string TraceRulesDec(decimal value) => Domain.Recall.Trace.TraceRules.Dec(value);
}
