using System.Globalization;
using System.Text;
using System.Text.Json;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Application.Features.Recall.Services;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Enums;
using Diten.InventoryService.Domain.Recall.Repositories;
using Diten.InventoryService.Domain.Recall.Rules;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Application.Features.Recall.Evidence;

/// <summary>
/// One table of a section: column keys (worded through <see cref="RecallEvidenceText"/>), and string cells already in the pack's language.
/// A table whose first column is <c>field</c> names a summary field per row (its first cell is a word key, worded by every renderer).
/// <see cref="PdfHidden"/> — columns the PDF leaves out (the ids beside a name: the CSV and the screen keep them).
/// </summary>
public sealed record RecallEvidenceTable(string Key, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows, IReadOnlyList<string>? PdfHidden = null)
{
    public bool IsFieldTable => Columns.Count > 0 && Columns[0] == "field";
}

/// <summary>One of the seven sections (pack §4.13, proposal §2.9). <see cref="Applicable"/> false = "uygulanmaz" (a drill or a period).</summary>
public sealed record RecallEvidenceSection(string Key, bool Applicable, IReadOnlyList<RecallEvidenceTable> Tables, string? Note = null);

/// <summary>
/// WP-0177-R7b — the content of ONE evidence pack: what the preview shows and what the PDF / CSV print, from the same object (the mockup's
/// "önizleme dışa aktarılacak içeriğin aynısıdır"). WP-MVP1-FIX-LIVE2: values are worded in <see cref="Language"/> (status, level, direction,
/// node, event, meaning …), people / company / product / warehouse are names read from their sources with the id beside them — a value
/// that cannot be read is the id with "(okunamadı)"; nothing is made up. <see cref="RequestedLanguage"/> differs from <see cref="Language"/>
/// only for a PDF in a language its font does not cover (zh / ar → en, with a note).
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
    IReadOnlyList<RecallEvidenceSection> Sections,
    string? CompanyText = null,
    string? RequestedLanguage = null)
{
    public IReadOnlyList<string> SectionKeys => Sections.Where(s => s.Applicable).Select(s => s.Key).ToList();

    /// <summary>The company as printed: "code · name", or the id with "(okunamadı)".</summary>
    public string Company => string.IsNullOrEmpty(CompanyText) ? LegalEntityId.ToString("D") : CompanyText;

    /// <summary>True when the file is in English because the selected language is not drawable in a PDF.</summary>
    public bool EnglishFallback => RequestedLanguage is { } r && r != Language;
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

    public const int MaxPeriodDays = 366;
}

/// <summary>The CSV of a pack: UTF-8 with BOM, ';' separated; per section its title line, then per table its header and rows (the 0176 F5 form).</summary>
public static class RecallEvidenceCsv
{
    public static byte[] Render(RecallEvidenceDocument d)
    {
        var l = d.Language;
        var sb = new StringBuilder();
        void Line(IEnumerable<string> cells) => sb.Append(string.Join(';', cells.Select(Cell))).Append("\r\n");
        Line([RecallEvidenceText.Of("RecallPdf.Title", l), d.SubjectLabel, RecallEvidenceText.Of("RecallPdf.Kind." + d.SubjectKind, l)]);
        Line([RecallEvidenceText.Word("company", l), d.Company, d.LegalEntityId.ToString("D"), RecallEvidenceText.Of("RecallPdf.Generated", l),
            RecallSupport.Wire(d.GeneratedAtUtc)]);
        foreach (var section in d.Sections)
        {
            Line([RecallEvidenceText.Word(section.Key, l)]);
            if (!section.Applicable)
            {
                Line([RecallEvidenceText.Word(section.Note ?? "notApplicable", l)]);
                continue;
            }

            if (section.Note is not null) Line([RecallEvidenceText.Word(section.Note, l)]);
            foreach (var table in section.Tables)
            {
                Line(table.Columns.Select(c => RecallEvidenceText.Word(c, l)));
                // a field row names its field: the word in the pack's language (as the PDF); every other cell is already worded
                foreach (var row in table.Rows) Line(table.IsFieldTable && row.Count > 0 ? [RecallEvidenceText.Word(row[0], l), .. row.Skip(1)] : row);
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
/// NOW (the caller's token); an unreadable one is listed with its outcome and empty signer / meaning / time.
/// <para>WP-MVP1-FIX-LIVE2 — names (CT-accepted path): a SIGNER is MOD-0022's <c>signerNameAtSign</c>; the actor of an EVENT is the name the
/// event recorded (<c>ActorName</c>, new events) or else the central audit queue's <c>ActorDisplayName</c> for the event's correlation id AND
/// actor; a summary person (opened / approved / completed / closed / cancelled by) is the signer of the step's signature or else the actor of
/// the step's event — only when the ids match. The company (MDM), product / SKU (PRODUCT-MASTER) and warehouse / location (MOD-0173 master)
/// are read with the caller's token; every unreadable one is its id + "(okunamadı)".</para>
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
    private readonly IRecallAuditNames _auditNames;
    private readonly IProductMasterClient _products;
    private readonly ILegalEntityMasterClient _companies;
    private readonly ILocationRepository _locations;

    public RecallEvidenceBuilder(IRecallRepository repository, IRecallDrillRepository drills, IRecallTraceSource ledger, IRecallQuarantineRequester quarantine,
        IRecallESignClient esign, RecallSubjectFacts facts, RecallSupport support, IRecallAuditNames auditNames, IProductMasterClient products,
        ILegalEntityMasterClient companies, ILocationRepository locations)
    {
        _repository = repository;
        _drills = drills;
        _ledger = ledger;
        _quarantine = quarantine;
        _esign = esign;
        _facts = facts;
        _support = support;
        _auditNames = auditNames;
        _products = products;
        _companies = companies;
        _locations = locations;
    }

    /// <summary>
    /// Exactly one subject: caseId, or runId, or from + to (≤ 366 days). (null, error, status) on a refusal. <paramref name="forPdf"/>: the
    /// content is worded in <see cref="RecallEvidenceText.PdfLanguage"/> of the selected language (zh / ar → en) and remembers the selection.
    /// </summary>
    public async Task<(RecallEvidenceDocument? Document, string? Code, string? Error, int Status)> BuildAsync(
        string? caseId, string? runId, string? from, string? to, string language, CancellationToken ct, bool forPdf = false)
    {
        var requested = RecallEvidenceText.Normal(language);
        var lang = forPdf ? RecallEvidenceText.PdfLanguage(requested) : requested;
        var given = new[] { caseId, runId, from ?? to }.Count(v => !string.IsNullOrWhiteSpace(v));
        if (given != 1) return (null, Common.InventoryErrorCodes.InvalidRequest, "Give exactly one subject: caseId, runId, or from + to.", 400);
        RecallEvidenceDocument document;
        if (!string.IsNullOrWhiteSpace(caseId))
        {
            if (!RecallSupport.TryParseId(caseId, out var id) || await _repository.FindCaseAsync(id, ct) is not { } c)
                return (null, RecallErrorCodes.CaseNotFound, "Recall case not found.", 404);
            document = await CaseAsync(c, lang, ct);
        }
        else if (!string.IsNullOrWhiteSpace(runId))
        {
            if (!RecallSupport.TryParseId(runId, out var id) || await _drills.FindRunAsync(id, ct) is not { } run)
                return (null, RecallErrorCodes.DrillNotFound, "Drill run not found.", 404);
            document = await RunAsync(run, lang, ct);
        }
        else
        {
            if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
                || !DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
            {
                return (null, Common.InventoryErrorCodes.InvalidRequest, "from and to are dates (yyyy-MM-dd).", 400);
            }

            if (end < start || end.DayNumber - start.DayNumber + 1 > RecallEvidenceSections.MaxPeriodDays)
                return (null, RecallErrorCodes.EvidenceRangeInvalid, $"The period must be 1-{RecallEvidenceSections.MaxPeriodDays} days.", 400);
            document = await PeriodAsync(start, end, lang, ct);
        }

        return (document with { RequestedLanguage = requested }, null, null, 200);
    }

    // ── a recall case: the seven sections ────────────────────────────────────────────────────────────────

    private async Task<RecallEvidenceDocument> CaseAsync(RecallCase c, string lang, CancellationToken ct)
    {
        var now = _support.Now();
        var snapshotId = c.ApprovedSnapshotId ?? c.SubmittedSnapshotId;
        var snapshot = snapshotId is { } sid ? await _repository.FindSnapshotAsync(sid, ct) : null;
        var items = await _repository.ScopeItemsAsync(c.Id, c.ApprovedScope?.Revision ?? c.ScopeRevision, ct);
        var events = await _repository.AllEventsAsync(c.Id, ct);

        // ── signatures first: their signer names serve the summary too ──
        var signed = new List<(string Action, string Id)>();
        void Add(string action, string? id)
        {
            if (!string.IsNullOrWhiteSpace(id) && signed.All(s => s.Id != id)) signed.Add((action, id));
        }

        foreach (var e in events.Where(e => e.SignatureId is not null))
        {
            Add(e.Kind switch
            {
                RecallCaseEventKind.EffectivenessCompleted => RecallSignableActions.EffectivenessComplete,
                RecallCaseEventKind.Closed => RecallSignableActions.CaseClose,
                _ => RecallSignableActions.ScopeApprove
            }, e.SignatureId);
        }

        Add(RecallSignableActions.ScopeApprove, c.ScopeApprovalSignatureId);
        Add(RecallSignableActions.EffectivenessComplete, c.EffectivenessSignatureId);
        Add(RecallSignableActions.CaseClose, c.ClosureSignatureId);
        var reads = await ReadSignaturesAsync(signed, ct);

        var audit = await _auditNames.NamesAsync(events.Where(e => e.ActorName is null && e.ActorUserId is not null)
            .Select(e => (e.CorrelationId, e.ActorUserId!.Value)).Distinct().ToList(), ct);
        string? EventName(RecallCaseEvent e) => e.ActorName ?? (e.ActorUserId is { } a && audit.TryGetValue((e.CorrelationId, a), out var n) ? n : null);

        // a summary person: the signer of the step's signature, else the actor of the step's event — only when the ids are the same person
        string Person(Guid? user, string? signatureId, RecallCaseEventKind kind)
        {
            if (user is null) return string.Empty;
            var id = user.Value.ToString("D");
            if (signatureId is not null && reads.TryGetValue(signatureId, out var s) && s.IsOk && s.Value!.SignerUserId == id
                && !string.IsNullOrWhiteSpace(s.Value.SignerNameAtSign))
            {
                return s.Value.SignerNameAtSign!;
            }

            var name = events.Where(e => e.Kind == kind && e.ActorUserId == user).Select(EventName).FirstOrDefault(n => n is not null);
            return name ?? NameUnreadable(id, lang);
        }

        var item = await ItemTextAsync(c.ItemId, lang, ct);
        var sku = await SkuCodeAsync(c.SkuId, c.SkuLevel, lang, ct);
        var company = await CompanyTextAsync(c.LegalEntityId, lang, ct);
        var summary = new List<IReadOnlyList<string>>
        {
            F("number", c.Number), F("status", V("Recall.Status.", c.Status.ToString(), lang)), F("class", RecallEvidenceText.Format("Recall.Class.N", lang, c.Class)),
            F("level", V("Recall.Level.", c.Level.ToString(), lang)), F("item", item), F("itemId", c.ItemId.ToString("D")), F("skuCode", sku),
            F("skuId", c.SkuId.ToString("D")), F("rootLot", c.RootLotNumber), F("reason", c.Reason), F("reasonReference", c.ReasonReference),
            F("company", company), F("companyId", c.LegalEntityId?.ToString("D")),
            F("openedBy", Person(c.OwnerUserId, null, RecallCaseEventKind.Opened)), F("openedById", c.OwnerUserId.ToString("D")),
            F("openedAtUtc", RecallSupport.Wire(c.OpenedAtUtc)), F("direction", V("Recall.Direction.", c.Direction.ToString(), lang)),
            F("scopeRevision", I(c.ScopeRevision)),
            // the two seals rc-v1 signs (submittedSnapshotSha256 / approvedSnapshotSha256 = the rs-scope-v1 scope seals), then the trace digest
            F("submittedScopeSeal", c.SubmittedScope?.Sha256), F("approvedScopeSeal", c.ApprovedScope?.Sha256), F("snapshotSha256", snapshot?.Sha256),
            F("scopeApprovedBy", Person(c.ScopeApprovedBy, c.ScopeApprovalSignatureId, RecallCaseEventKind.ScopeApproved)),
            F("scopeApprovedById", c.ScopeApprovedBy?.ToString("D")), F("scopeApprovedAtUtc", RecallSupport.Wire(c.ScopeApprovedAtUtc)),
            F("effectivenessCompletedBy", Person(c.EffectivenessCompletedBy, c.EffectivenessSignatureId, RecallCaseEventKind.EffectivenessCompleted)),
            F("effectivenessCompletedById", c.EffectivenessCompletedBy?.ToString("D")),
            F("effectivenessCompletedAtUtc", RecallSupport.Wire(c.EffectivenessCompletedAtUtc)),
            F("closedBy", Person(c.ClosedBy, c.ClosureSignatureId, RecallCaseEventKind.Closed)), F("closedById", c.ClosedBy?.ToString("D")),
            F("closedAtUtc", RecallSupport.Wire(c.ClosedAtUtc)), F("closureSummary", c.ClosureSummary), F("openQuantityAcceptance", c.OpenQuantityAcceptance),
            F("cancelledBy", Person(c.CancelledBy, null, RecallCaseEventKind.Cancelled)), F("cancelledById", c.CancelledBy?.ToString("D")),
            F("cancelReason", c.CancelReason)
        };

        var requests = await _repository.QuarantineRequestsAsync(c.Id, ct);
        var holds = (await _quarantine.HoldsAsync(requests.SelectMany(r => r.HoldIds).Distinct(StringComparer.Ordinal).ToList(), ct))
            .ToDictionary(h => h.HoldId, StringComparer.Ordinal);
        var places = await PlacesAsync(requests.SelectMany(r => new[] { r.WarehouseId, r.LocationId }).Concat(SnapshotPlaceIds(snapshot)), ct);
        var quarantine = requests.Select(r => Row(r.LotNumber, Place(places, r.WarehouseId, lang), Place(places, r.LocationId, lang),
            V("Recall.ReqStatus.", r.Status.ToString(), lang), r.NotNeededReason is { } why ? V("Recall.NotNeeded.", why.ToString(), lang) : null, D(r.HeldQuantity),
            r.BaseUomId, string.Join(", ", r.HoldIds.Select(h => holds.TryGetValue(h, out var v) ? v.HoldNumber : h)), r.WarehouseId, r.LocationId)).ToList();

        var notifications = await _repository.NotificationsAsync(c.Id, ct);
        var superseded = notifications.Where(n => n.SupersedesId is not null).Select(n => n.SupersedesId!.Value).ToHashSet();
        var customers = RecallReads.Shipments(items).Select(s => s.CustomerRef!).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal)
            .Select(customer =>
            {
                var n = notifications.Where(x => x.CustomerRef == customer && !superseded.Contains(x.Id)).OrderBy(x => x.RecordedAtUtc).LastOrDefault();
                return Row(customer, n is null ? null : RecallSupport.WireDate(n.NotifiedOn), n is null ? null : V("Recall.Channel.", n.Channel.ToString(), lang), n?.Note);
            }).ToList();

        var links = await _repository.ReturnLinksAsync(c.Id, ct);
        var returns = new List<IReadOnlyList<string>>();
        foreach (var link in links)
        {
            var hold = await _quarantine.HoldOfMovementAsync(link.TransactionId, ct);
            returns.Add(Row(link.TransactionNumber ?? link.TransactionId, link.CustomerRef, link.LotNumber, D(link.Quantity), link.BaseUomId, hold?.HoldNumber,
                RecallSupport.Wire(link.PostedAtUtc)));
        }

        var effectivenessTables = await EffectivenessTablesAsync(c, reads,
            Person(c.EffectivenessCompletedBy, c.EffectivenessSignatureId, RecallCaseEventKind.EffectivenessCompleted), lang, now, ct);

        var timeline = events.Select((e, i) => Row(I(i + 1), RecallSupport.Wire(e.AtUtc), V("Recall.Event.", e.Kind.ToString(), lang),
            e.ActorUserId is { } a ? EventName(e) ?? NameUnreadable(a.ToString("D"), lang) : null, e.ActorUserId?.ToString("D"), e.Text, e.SignatureId)).ToList();

        var sections = new List<RecallEvidenceSection>
        {
            new(RecallEvidenceSections.Summary, true, [new("summary", ["field", "value"], summary)]),
            await TraceSectionAsync(snapshot, items, places, lang, ct),
            new(RecallEvidenceSections.Quarantine, true,
                [new("requests", ["lot", "warehouse", "location", "status", "reason", "held", "unit", "holds", "warehouseId", "locationId"], quarantine,
                    ["warehouseId", "locationId"])]),
            new(RecallEvidenceSections.Customers, true, [new("customers", ["customer", "notifiedOn", "channel", "note"], customers)]),
            new(RecallEvidenceSections.ReturnsAndEffectiveness, true,
                [new("returns", ["transaction", "customer", "lot", "quantity", "unit", "holds", "date"], returns), .. effectivenessTables]),
            new(RecallEvidenceSections.Timeline, true,
                [new("timeline", ["seq", "at", "event", "actor", "actorId", "text", "signatureId"], timeline, ["actorId"])]),
            SignaturesSection(signed, reads, lang)
        };
        return new RecallEvidenceDocument(RecallEvidenceSubjectKind.Case, c.Id, null, null, null, c.Number, c.LegalEntityId ?? Guid.Empty, now, lang, sections,
            company);
    }

    /// <summary>
    /// Section 5's effectiveness: the STORED result of a completed check (target, who / when, the signature, the note — and per SKU / unit, or
    /// "no shipments — no quantity to measure" when nothing was shipped), or, before completion, the current figures marked as such.
    /// </summary>
    private async Task<IReadOnlyList<RecallEvidenceTable>> EffectivenessTablesAsync(RecallCase c,
        IReadOnlyDictionary<string, RecallESignCall<RecallESignSignature>> reads, string completedBy, string lang, DateTimeOffset now, CancellationToken ct)
    {
        var completed = c.EffectivenessCompletedAtUtc is not null && c.Effectiveness is not null;
        var effectiveness = c.Effectiveness ?? await _facts.LiveEffectivenessAsync(c, null, now, ct);
        var check = new List<IReadOnlyList<string>>
        {
            F("status", completed ? V("Recall.Event.", nameof(RecallCaseEventKind.EffectivenessCompleted), lang) : RecallEvidenceText.Word("notCompleted", lang)),
            F("target", D(effectiveness.TargetPercent))
        };
        if (completed)
        {
            check.Add(F("effectivenessCompletedBy", completedBy));
            check.Add(F("effectivenessCompletedAtUtc", RecallSupport.Wire(c.EffectivenessCompletedAtUtc)));
            check.Add(F("signatureId", SignatureText(c.EffectivenessSignatureId, reads)));
            check.Add(F("note", c.EffectivenessNote));
        }

        if (effectiveness.Totals.Count == 0) check.Add(F("effectiveness", RecallEvidenceText.Word("noShipments", lang)));
        var tables = new List<RecallEvidenceTable> { new("effectivenessCheck", ["field", "value"], check) };
        if (effectiveness.Totals.Count > 0)
        {
            var codes = new Dictionary<Guid, string>();
            foreach (var skuId in effectiveness.Totals.Select(t => t.SkuId).Distinct()) codes[skuId] = await SkuCodeAsync(skuId, c.SkuLevel, lang, ct);
            tables.Add(new("effectiveness", ["sku", "unit", "shipped", "returned", "consumed", "open", "percent", "target", "met", "skuId"],
                effectiveness.Totals.Select(t => Row(codes[t.SkuId], t.BaseUomId, D(t.Shipped), D(t.Returned), D(t.Consumed), D(t.Open), D(t.Percent),
                    D(effectiveness.TargetPercent), RecallEvidenceText.Word(t.Met ? "yes" : "no", lang), t.SkuId.ToString("D"))).ToList(), ["skuId"]));
        }

        return tables;
    }

    // ── a drill run: 1, 2, 7 (3–6 not applicable — a read-only rehearsal) ─────────────────────────────────

    private async Task<RecallEvidenceDocument> RunAsync(RecallDrillRun run, string lang, CancellationToken ct)
    {
        var snapshot = run.SnapshotId is { } sid ? await _repository.FindSnapshotAsync(sid, ct) : null;
        var summary = new List<IReadOnlyList<string>>
        {
            F("number", run.Number), F("planCode", run.PlanCode), F("status", V("Recall.DrillStatus.", run.Status.ToString(), lang)),
            F("selectionMode", V("Recall.Drills.Mode", run.SelectionMode.ToString(), lang)),
            F("rootLot", run.LotNumber), F("startedAtUtc", RecallSupport.Wire(run.StartedAtUtc)), F("endedAtUtc", RecallSupport.Wire(run.EndedAtUtc)),
            F("durationMinutes", run.DurationMinutes is { } m ? I(m) : null), F("targetHours", I(run.TargetHours)),
            F("passed", Passed(run.Passed, lang)), F("gapCount", I(run.GapCount)), F("snapshotSha256", run.SnapshotSha256 ?? snapshot?.Sha256),
            F("resultNote", run.ResultNote)
        };
        var signed = new List<(string Action, string Id)>();
        if (!string.IsNullOrWhiteSpace(run.ResultSignatureId)) signed.Add((RecallSignableActions.DrillResult, run.ResultSignatureId));
        var reads = await ReadSignaturesAsync(signed, ct);
        var company = await CompanyTextAsync(run.LegalEntityId, lang, ct);
        var sections = new List<RecallEvidenceSection>
        {
            new(RecallEvidenceSections.Summary, true, [new("summary", ["field", "value"], summary)]),
            await TraceSectionAsync(snapshot, [], await PlacesAsync(SnapshotPlaceIds(snapshot), ct), lang, ct),
            NotApplicable(RecallEvidenceSections.Quarantine, "notApplicableDrill"),
            NotApplicable(RecallEvidenceSections.Customers, "notApplicableDrill"),
            NotApplicable(RecallEvidenceSections.ReturnsAndEffectiveness, "notApplicableDrill"),
            NotApplicable(RecallEvidenceSections.Timeline, "notApplicableDrill"),
            SignaturesSection(signed, reads, lang)
        };
        return new RecallEvidenceDocument(RecallEvidenceSubjectKind.DrillRun, null, run.Id, null, null, run.Number, run.LegalEntityId ?? Guid.Empty, _support.Now(),
            lang, sections, company);
    }

    // ── a period: the company's drill runs in it ─────────────────────────────────────────────────────────

    private async Task<RecallEvidenceDocument> PeriodAsync(DateOnly from, DateOnly to, string lang, CancellationToken ct)
    {
        var runs = await _drills.RunsStartedBetweenAsync(new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), ct);
        var rows = runs.Select(r => Row(r.Number, RecallSupport.Wire(r.StartedAtUtc), r.LotNumber, r.DurationMinutes is { } m ? I(m) : null, I(r.TargetHours),
            Passed(r.Passed, lang), V("Recall.DrillStatus.", r.Status.ToString(), lang))).ToList();
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
        return new RecallEvidenceDocument(RecallEvidenceSubjectKind.Period, null, null, from, to, label, _support.LegalEntityId, _support.Now(), lang, sections,
            await CompanyTextAsync(_support.LegalEntityId, lang, ct));
    }

    // ── shared sections ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Section 2 — the nodes of the sealed snapshot (the rule-10 order of its canonical form), the customer of a shipment from the scope, and the
    /// posting date of the node's movement read from the ledger (the canonical form carries no date). No snapshot → the section says so.
    /// The node kind (and a gap's reason) is worded; a stock node's place is its warehouse / location "code · name" (ids in hidden columns).
    /// </summary>
    private async Task<RecallEvidenceSection> TraceSectionAsync(RecallTraceSnapshot? snapshot, IReadOnlyList<RecallScopeItem> items,
        IReadOnlyDictionary<string, Location> places, string lang, CancellationToken ct)
    {
        if (snapshot is null) return new RecallEvidenceSection(RecallEvidenceSections.Trace, true, [new("trace", TraceColumns, [], TraceHidden)], "notSealed");
        using var json = JsonDocument.Parse(snapshot.Canonical);
        var nodes = json.RootElement.GetProperty("nodes").EnumerateArray().ToList();
        var lotIds = nodes.Select(n => S(n, "lotId")).Where(v => Guid.TryParse(v, out _)).Select(v => Guid.Parse(v!)).Distinct().ToList();
        var movements = await _ledger.TransactionsOfLotsAsync(lotIds, ct);
        var customers = items.Where(i => i.CustomerRef is not null).ToDictionary(i => i.NodeKey, i => i.CustomerRef!, StringComparer.Ordinal);
        var rows = nodes.Select(n =>
        {
            var key = S(n, "key") ?? string.Empty;
            var kind = V("Recall.Kind.", S(n, "kind") ?? string.Empty, lang);
            var gap = S(n, "gap");
            var document = S(n, "documentId");
            var type = S(n, "movementType");
            var lot = S(n, "lotId");
            var date = movements.Where(t => t.LotId == lot && t.SourceDocumentId == document && document is not null && WireNames.ToWire(t.Type) == type)
                .Select(t => (DateTimeOffset?)t.PostingDate).Min();
            var (warehouse, location) = (S(n, "warehouseId"), S(n, "locationId"));
            var place = customers.TryGetValue(key, out var customer)
                ? customer
                : string.Join(" · ", new[] { Place(places, warehouse, lang), Place(places, location, lang) }.Where(v => !string.IsNullOrEmpty(v)));
            return Row(gap is null ? kind : $"{kind}: {V("Recall.Gap.", gap, lang)}", S(n, "lotNumber"), place, S(n, "quantity"), S(n, "baseUomId"), document,
                date is { } d ? RecallSupport.Wire(d) : null, warehouse, location);
        }).ToList();
        return new RecallEvidenceSection(RecallEvidenceSections.Trace, true, [new("trace", TraceColumns, rows, TraceHidden)]);
    }

    private static readonly IReadOnlyList<string> TraceColumns = ["nodeKind", "lot", "place", "quantity", "unit", "document", "date", "warehouseId", "locationId"];

    private static readonly IReadOnlyList<string> TraceHidden = ["warehouseId", "locationId"];

    private static string? S(JsonElement n, string name) => n.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static IReadOnlyList<string?> SnapshotPlaceIds(RecallTraceSnapshot? snapshot)
    {
        if (snapshot is null) return [];
        using var json = JsonDocument.Parse(snapshot.Canonical);
        return json.RootElement.GetProperty("nodes").EnumerateArray().SelectMany(n => new[] { S(n, "warehouseId"), S(n, "locationId") }).ToList();
    }

    /// <summary>Each signature read ONCE from MOD-0022 at generation time (the caller's token).</summary>
    private async Task<IReadOnlyDictionary<string, RecallESignCall<RecallESignSignature>>> ReadSignaturesAsync(IReadOnlyList<(string Action, string Id)> signed,
        CancellationToken ct)
    {
        var reads = new Dictionary<string, RecallESignCall<RecallESignSignature>>(StringComparer.Ordinal);
        foreach (var (_, id) in signed) reads[id] = await _esign.SignatureAsync(id, ct);
        return reads;
    }

    /// <summary>
    /// Section 7 — the action's name (its MOD-0022 registration names), the signer's name at signing (id beside it), the meaning, the time,
    /// the state; unreadable → the outcome, NOTHING filled in.
    /// </summary>
    private static RecallEvidenceSection SignaturesSection(IReadOnlyList<(string Action, string Id)> signed,
        IReadOnlyDictionary<string, RecallESignCall<RecallESignSignature>> reads, string lang)
    {
        var rows = new List<IReadOnlyList<string>>();
        foreach (var (action, id) in signed)
        {
            var read = reads[id];
            var name = RecallSignableActions.All.FirstOrDefault(a => a.Code == action)?.Names.GetValueOrDefault(lang) ?? action;
            if (!read.IsOk)
            {
                rows.Add(Row(name, id, null, null, null, null, RecallEvidenceText.Format("Recall.Ev.W.sigUnreadable", lang, read.Outcome)));
                continue;
            }

            var s = read.Value!;
            var signer = !string.IsNullOrWhiteSpace(s.SignerNameAtSign) ? s.SignerNameAtSign : s.SignerUserId is { } u ? NameUnreadable(u, lang) : null;
            rows.Add(Row(name, id, signer, s.SignerUserId, Meaning(s, lang), RecallSupport.Wire(s.SignedAtUtc),
                RecallEvidenceText.Word(s.Invalidated ? "sigInvalidated" : "sigOk", lang)));
        }

        return new RecallEvidenceSection(RecallEvidenceSections.Signatures, true,
            [new("signatures", ["action", "signatureId", "signer", "signerId", "meaning", "signedAt", "status"], rows, ["signerId"])]);
    }

    /// <summary>A signature as a summary value: its id and, when MOD-0022 answered, the signer's name.</summary>
    private static string? SignatureText(string? id, IReadOnlyDictionary<string, RecallESignCall<RecallESignSignature>> reads)
        => id is null ? null
            : reads.TryGetValue(id, out var r) && r.IsOk && !string.IsNullOrWhiteSpace(r.Value!.SignerNameAtSign) ? $"{id} · {r.Value.SignerNameAtSign}"
            : id;

    /// <summary>The meaning: a known code worded (<c>Recall.Ev.W.meaning.{code}</c>), else the text MOD-0022 recorded, else the code.</summary>
    private static string? Meaning(RecallESignSignature s, string lang)
        => s.MeaningCode is { } code && RecallEvidenceText.Find("Recall.Ev.W.meaning." + code, lang) is { } word ? word
            : !string.IsNullOrWhiteSpace(s.MeaningText) ? s.MeaningText
            : s.MeaningCode;

    // ── names read from their masters (the caller's token); unreadable → the id + "(okunamadı)" ─────────

    private async Task<string> CompanyTextAsync(Guid? legalEntityId, string lang, CancellationToken ct)
    {
        if (legalEntityId is not { } id || id == Guid.Empty) return string.Empty;
        try
        {
            if (await _companies.GetLegalEntityNameAsync(id, ct) is { } name) return $"{name.Code} · {name.Name}";
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // fall through: unreadable
        }

        return Unreadable(id.ToString("D"), lang);
    }

    private async Task<string> ItemTextAsync(Guid itemId, string lang, CancellationToken ct)
    {
        try
        {
            if (await _products.GetProductAsync(itemId, ct) is { } p && (!string.IsNullOrWhiteSpace(p.CanonicalCode) || !string.IsNullOrWhiteSpace(p.Name)))
                return string.Join(" · ", new[] { p.CanonicalCode, p.Name }.Where(v => !string.IsNullOrWhiteSpace(v)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // fall through: unreadable
        }

        return Unreadable(itemId.ToString("D"), lang);
    }

    private async Task<string> SkuCodeAsync(Guid skuId, SkuLevel level, string lang, CancellationToken ct)
    {
        try
        {
            if (await _products.GetSkuAsync(skuId, level, ct) is { CanonicalCode: { Length: > 0 } code }) return code;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // fall through: unreadable
        }

        return Unreadable(skuId.ToString("D"), lang);
    }

    /// <summary>The company's own warehouses / locations by id (the company-bound master read).</summary>
    private async Task<IReadOnlyDictionary<string, Location>> PlacesAsync(IEnumerable<string?> ids, CancellationToken ct)
    {
        var guids = ids.Where(v => Guid.TryParse(v, out _)).Select(v => Guid.Parse(v!)).Distinct().ToList();
        if (guids.Count == 0) return new Dictionary<string, Location>();
        return (await _locations.FindManyAsync(guids, ct)).ToDictionary(l => l.Id.ToString("D"), StringComparer.OrdinalIgnoreCase);
    }

    private static string Place(IReadOnlyDictionary<string, Location> places, string? id, string lang)
        => string.IsNullOrWhiteSpace(id) ? string.Empty
            // "code · name" — once when the master names a place by its code
            : places.TryGetValue(id, out var l) ? string.Join(" · ", new[] { l.Code, l.Name }.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.Ordinal))
            : Unreadable(id, lang);

    private static string Unreadable(string id, string lang) => RecallEvidenceText.Format("Recall.Ev.W.unreadable", lang, id);

    private static string NameUnreadable(string id, string lang) => RecallEvidenceText.Format("Recall.Ev.W.nameUnreadable", lang, id);

    /// <summary>A stored code worded in the pack's language — the screen's own key (<paramref name="prefix"/> + value); unknown → the code.</summary>
    private static string V(string prefix, string value, string lang) => RecallEvidenceText.Find(prefix + value, lang) ?? value;

    private static string? Passed(bool? passed, string lang) => passed is { } p ? RecallEvidenceText.Word(p ? "passedTrue" : "passedFalse", lang) : null;

    private static RecallEvidenceSection NotApplicable(string key, string note) => new(key, false, [], note);

    private static IReadOnlyList<string> F(string field, string? value) => [field, value ?? string.Empty];

    private static IReadOnlyList<string> Row(params string?[] cells) => cells.Select(c => c ?? string.Empty).ToList();

    private static string Day(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string D(decimal value) => TraceRulesDec(value);

    private static string D(decimal? value) => value is { } v ? TraceRulesDec(v) : string.Empty;

    private static string TraceRulesDec(decimal value) => Domain.Recall.Trace.TraceRules.Dec(value);
}
