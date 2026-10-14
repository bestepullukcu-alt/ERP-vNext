using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Enums;

namespace Diten.InventoryService.Domain.Recall.Rules;

/// <summary>RECALL-BUNDLE v1 §4.60 — the codes this module owns (+ the shared ones it reuses by their INVENTORY name).</summary>
public static class RecallErrorCodes
{
    public const string SettingsInvalid = "RECALL_SETTINGS_INVALID";
    public const string ScopeExclusionReasonRequired = "RECALL_SCOPE_EXCLUSION_REASON_REQUIRED";
    public const string ScopeItemNotMarkable = "RECALL_SCOPE_ITEM_NOT_MARKABLE";
    public const string SignatureRequired = "RECALL_SIGNATURE_REQUIRED";
    public const string OpenQuantityAcceptanceRequired = "RECALL_OPEN_QUANTITY_ACCEPTANCE_REQUIRED";
    public const string EvidenceRangeInvalid = "RECALL_EVIDENCE_RANGE_INVALID";
    public const string UnknownCustomer = "RECALL_UNKNOWN_CUSTOMER";
    public const string CaseNotFound = "RECALL_CASE_NOT_FOUND";
    public const string Conflict = "RECALL_CONFLICT";
    public const string InvalidTransition = "RECALL_INVALID_TRANSITION";
    public const string CaseAlreadyOpen = "RECALL_CASE_ALREADY_OPEN";
    public const string ScopeChanged = "RECALL_SCOPE_CHANGED";
    public const string SodViolation = "RECALL_SOD_VIOLATION";
    public const string SignatureMismatch = "RECALL_SIGNATURE_MISMATCH";
    public const string PendingActionMismatch = "RECALL_PENDING_ACTION_MISMATCH";
    public const string NoChanges = "RECALL_NO_CHANGES";
    public const string ScopeItemExists = "RECALL_SCOPE_ITEM_EXISTS";
    public const string QuarantineRequestExists = "RECALL_QUARANTINE_REQUEST_EXISTS";
    public const string QuarantineRequestNotRetryable = "RECALL_QUARANTINE_REQUEST_NOT_RETRYABLE";
    public const string ReturnAlreadyLinked = "RECALL_RETURN_ALREADY_LINKED";
    public const string DrillAlreadyRunning = "RECALL_DRILL_ALREADY_RUNNING";
    public const string DrillStepOrder = "RECALL_DRILL_STEP_ORDER";
    public const string ReturnNotEligible = "RECALL_RETURN_NOT_ELIGIBLE";
    public const string ReturnNotQuarantined = "RECALL_RETURN_NOT_QUARANTINED";
    public const string SegregationNotHeld = "RECALL_SEGREGATION_NOT_HELD";
    public const string QuantityExceedsShipped = "RECALL_QUANTITY_EXCEEDS_SHIPPED";
    public const string DrillNoCandidate = "RECALL_DRILL_NO_CANDIDATE";

    // shared (INVENTORY names)
    public const string ReasonRequired = "REASON_REQUIRED";
    public const string UnknownLot = "UNKNOWN_LOT";
}

/// <summary>A refused request: contract code, message, HTTP status.</summary>
public sealed record RecallViolation(string Code, string Message, int Status);

/// <summary>Wire names (RECALL-BUNDLE §2): every enum travels by its exact C# name.</summary>
public static class RecallWire
{
    /// <summary>Exact C# name only ("Draft", never "draft" or "0") — an enum on the wire is a closed string set.</summary>
    public static bool TryParseName<T>(string? raw, out T value) where T : struct, Enum
    {
        value = default;
        return raw is not null && Enum.GetNames<T>().Contains(raw, StringComparer.Ordinal) && Enum.TryParse(raw, out value);
    }

    public static string Decimal(decimal value) => value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>KR-G (owner decision 2026-10-13) defaults + the pack §4.14 ranges. Pure: no I/O.</summary>
public static class RecallSettingsRules
{
    public const int MinTargetHours = 1;
    public const int MaxTargetHours = 72;
    public const int MinFrequencyDays = 30;
    public const int MaxFrequencyDays = 730;
    public const int MinTraceDepth = 2;
    public const int MaxTraceDepth = 50;
    public const int MinTraceNodes = 100;
    public const int MaxTraceNodes = 20000;

    public static readonly IReadOnlyList<string> Classes = ["1", "2", "3"];

    /// <summary>The settings of a company that never saved any (version 0): 4 h, 180 days, class targets 100 / 95 / 90 (KR-G).</summary>
    public static RecallSettings Defaults() => new()
    {
        DrillTargetHours = 4,
        DrillFrequencyWarningDays = 180,
        EffectivenessTargetPercent = new Dictionary<string, decimal>(StringComparer.Ordinal) { ["1"] = 100m, ["2"] = 95m, ["3"] = 90m },
        TraceMaxDepth = 20,
        TraceMaxNodes = 5000,
        Version = 0
    };

    public static RecallViolation? Validate(RecallSettings s)
    {
        if (s.DrillTargetHours is < MinTargetHours or > MaxTargetHours)
            return Invalid($"drillTargetHours must be {MinTargetHours}-{MaxTargetHours}.");
        if (s.DrillFrequencyWarningDays is < MinFrequencyDays or > MaxFrequencyDays)
            return Invalid($"drillFrequencyWarningDays must be {MinFrequencyDays}-{MaxFrequencyDays}.");
        if (s.EffectivenessTargetPercent.Count != Classes.Count || Classes.Any(c => !s.EffectivenessTargetPercent.ContainsKey(c)))
            return Invalid("effectivenessTargetPercent must name exactly the classes 1, 2 and 3.");
        if (s.EffectivenessTargetPercent.Values.Any(v => v is < 0m or > 100m || decimal.Round(v, 1) != v))
            return Invalid("every effectivenessTargetPercent value must be 0-100 with at most one decimal.");
        if (s.TraceMaxDepth is < MinTraceDepth or > MaxTraceDepth)
            return Invalid($"traceMaxDepth must be {MinTraceDepth}-{MaxTraceDepth}.");
        return s.TraceMaxNodes is < MinTraceNodes or > MaxTraceNodes ? Invalid($"traceMaxNodes must be {MinTraceNodes}-{MaxTraceNodes}.") : null;
    }

    /// <summary>The changed fields, one entry each (field, old, new) — the history rows of one save.</summary>
    public static IReadOnlyList<(string Field, string Old, string New)> Changes(RecallSettings before, RecallSettings after)
    {
        var changes = new List<(string, string, string)>();

        void Compare(string field, string old, string @new)
        {
            if (!string.Equals(old, @new, StringComparison.Ordinal)) changes.Add((field, old, @new));
        }

        Compare("drillTargetHours", I(before.DrillTargetHours), I(after.DrillTargetHours));
        Compare("drillFrequencyWarningDays", I(before.DrillFrequencyWarningDays), I(after.DrillFrequencyWarningDays));
        foreach (var c in Classes)
        {
            Compare($"effectivenessTargetPercent.{c}", P(before.EffectivenessTargetPercent.GetValueOrDefault(c)), P(after.EffectivenessTargetPercent.GetValueOrDefault(c)));
        }

        Compare("traceMaxDepth", I(before.TraceMaxDepth), I(after.TraceMaxDepth));
        Compare("traceMaxNodes", I(before.TraceMaxNodes), I(after.TraceMaxNodes));
        return changes;
    }

    /// <summary>The effectiveness target of a class (defaults when the settings lack it).</summary>
    public static decimal TargetFor(RecallSettings settings, int @class)
    {
        var key = @class.ToString(CultureInfo.InvariantCulture);
        return settings.EffectivenessTargetPercent.TryGetValue(key, out var v) ? v : Defaults().EffectivenessTargetPercent[key];
    }

    private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Normalised (95 and 95.0 are the same value).</summary>
    private static string P(decimal value) => (value / 1.0000000000000000000000000000m).ToString(CultureInfo.InvariantCulture);

    private static RecallViolation Invalid(string message) => new(RecallErrorCodes.SettingsInvalid, message, 400);
}

/// <summary>
/// Pack §8.7 — the case state machine, the four-eyes rule (CT-R1) and the reason rule. Pure. The ONLY place a status transition is decided:
/// <code>
/// Draft ──submit──▶ PendingScopeApproval ──approve(+sig)──▶ Active ──startEffectiveness──▶ EffectivenessCheck
///   ▲                      │
///   └────────return────────┘          EffectivenessCheck ──complete(+sig)──▶ (completed) ──close(+sig)──▶ Closed
/// Draft | PendingScopeApproval ──cancel──▶ Cancelled
/// </code>
/// </summary>
public static class RecallCaseRules
{
    public const int MinReasonLength = 10;
    public const int MaxReasonLength = 1000;
    public const int MaxSummaryLength = 2000;
    public const int MaxNoteLength = 1000;
    public const int MaxReferenceLength = 100;
    public static readonly TimeSpan PendingActionLifetime = TimeSpan.FromMinutes(15);

    public static bool IsTerminal(RecallCaseStatus s) => s is RecallCaseStatus.Closed or RecallCaseStatus.Cancelled;

    /// <summary>Approved and not yet closed: the lots of the approved scope are under recall (CT-R7).</summary>
    public static bool IsUnderRecall(RecallCaseStatus s) => s is RecallCaseStatus.Active or RecallCaseStatus.EffectivenessCheck;

    public static bool CanEditScope(RecallCaseStatus s) => s == RecallCaseStatus.Draft;

    public static bool CanSubmit(RecallCaseStatus s) => s == RecallCaseStatus.Draft;

    public static bool CanReturn(RecallCaseStatus s) => s == RecallCaseStatus.PendingScopeApproval;

    public static bool CanApprove(RecallCaseStatus s) => s == RecallCaseStatus.PendingScopeApproval;

    /// <summary>CT-R5 (R3): an Active case with a revision r+1 waiting for the signed approval of its delta.</summary>
    public static bool IsRevisionPending(RecallCase c) => c.Status == RecallCaseStatus.Active && c.PendingRevision is not null;

    /// <summary>approveScope / prepare(ApproveScope): the first approval, or the approval of a pending revision's delta (CT-R5).</summary>
    public static bool CanApproveScope(RecallCase c) => CanApprove(c.Status) || IsRevisionPending(c);

    /// <summary>CT-R5: a revision needs an approved scope, an Active case and no revision already waiting.</summary>
    public static bool CanRevise(RecallCase c) => c.Status == RecallCaseStatus.Active && c.PendingRevision is null && c.ApprovedScope is not null;

    public static bool CanCancel(RecallCaseStatus s) => s is RecallCaseStatus.Draft or RecallCaseStatus.PendingScopeApproval;

    public static bool CanStartEffectiveness(RecallCaseStatus s) => s == RecallCaseStatus.Active;

    public static bool CanCompleteEffectiveness(RecallCase c) => c.Status == RecallCaseStatus.EffectivenessCheck && c.Effectiveness is null;

    public static bool CanClose(RecallCase c) => c.Status == RecallCaseStatus.EffectivenessCheck && c.Effectiveness is not null;

    /// <summary>Records (notification, consumption, segregation) are kept while the recall runs.</summary>
    public static bool AcceptsRecords(RecallCaseStatus s) => IsUnderRecall(s);

    public static bool CanPrepare(RecallCase c, RecallPendingActionKind action) => action switch
    {
        RecallPendingActionKind.ApproveScope => CanApproveScope(c),
        RecallPendingActionKind.CompleteEffectiveness => CanCompleteEffectiveness(c),
        RecallPendingActionKind.Close => CanClose(c),
        _ => false
    };

    /// <summary>
    /// CT-R1 (four-eyes, both ways): the user who submitted the scope may not approve it; the user who completed the effectiveness check
    /// may not close the case. Completion itself has no SoD.
    /// </summary>
    public static RecallViolation? SeparationOfDuties(RecallCase c, RecallPendingActionKind action, Guid actor) => action switch
    {
        RecallPendingActionKind.ApproveScope when c.ScopeSubmittedBy == actor
            => new RecallViolation(RecallErrorCodes.SodViolation, "The user who submitted the scope cannot approve it (four-eyes, CT-R1).", 409),
        RecallPendingActionKind.Close when c.EffectivenessCompletedBy == actor
            => new RecallViolation(RecallErrorCodes.SodViolation, "The user who completed the effectiveness check cannot close the case (four-eyes, CT-R1).", 409),
        _ => null
    };

    /// <summary>The signable action of a prepared decision (registered with MOD-0022 in R4, module <c>recall</c>).</summary>
    public static string ActionCode(RecallPendingActionKind action) => action switch
    {
        RecallPendingActionKind.ApproveScope => "recall.scope.approve",
        RecallPendingActionKind.CompleteEffectiveness => "recall.effectiveness.complete",
        RecallPendingActionKind.Close => "recall.case.close",
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
    };

    /// <summary>Reason 10–1000 after trim (mockup "en az 10 karakter").</summary>
    public static RecallViolation? Reason(string? reason, string member = "reason", int max = MaxReasonLength)
        => reason?.Trim() is { Length: >= MinReasonLength } r && r.Length <= max
            ? null
            : new RecallViolation(RecallErrorCodes.ReasonRequired, $"{member} must be {MinReasonLength}-{max} characters.", 400);
}

/// <summary>
/// Pack §8.3 / §4.3 — the scope items: node keys, the subtree exclusion (CT-R6) and the canonical form whose SHA-256 is sealed at submit
/// and re-computed at approval (<c>RECALL_SCOPE_CHANGED</c> when it differs). Pure.
/// </summary>
public static class RecallScopeRules
{
    /// <summary>The canonical form version of the scope seal (R2 extends the items from the trace; the form stays).</summary>
    public const string CanonicalVersion = "rs-scope-v1";

    public const int MaxItemsPerRequest = 200;

    /// <summary>Stable key of a node: kind + ids (R1 hand entry and R2 trace produce the same key for the same node).</summary>
    public static string NodeKey(RecallScopeItemKind kind, string lotId, string? warehouseId = null, string? locationId = null,
        string? stockStatus = null, string? sourceDocumentId = null) => kind switch
    {
        RecallScopeItemKind.Lot => $"Lot:{lotId}",
        RecallScopeItemKind.Stock => $"Stock:{lotId}:{warehouseId}:{locationId ?? "-"}:{stockStatus ?? "-"}",
        RecallScopeItemKind.Shipment => $"Shipment:{lotId}:{sourceDocumentId}",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    /// <summary>CT-R6: an item is in the scope when it and every ancestor are included.</summary>
    public static bool EffectivelyIncluded(RecallScopeItem item, IReadOnlyDictionary<string, RecallScopeItem> byKey)
    {
        var current = item;
        var guard = 0;
        while (current is not null && guard++ <= byKey.Count)
        {
            if (!current.Included) return false;
            current = current.ParentKey is { } parent && byKey.TryGetValue(parent, out var p) ? p : null;
        }

        return true;
    }

    /// <summary>The items whose effective inclusion is false (the item or an ancestor excluded).</summary>
    public static IReadOnlyList<RecallScopeItem> Excluded(IReadOnlyList<RecallScopeItem> items)
    {
        var byKey = items.ToDictionary(i => i.NodeKey, StringComparer.Ordinal);
        return items.Where(i => !EffectivelyIncluded(i, byKey)).ToList();
    }

    /// <summary>
    /// Canonical JSON of the scope (items ordered by node key ordinally; every member written, nulls as null; decimals invariant;
    /// <c>included</c> = the EFFECTIVE inclusion) — the bytes whose SHA-256 is the seal.
    /// </summary>
    public static string Canonical(IReadOnlyList<RecallScopeItem> items, int revision)
    {
        var byKey = items.ToDictionary(i => i.NodeKey, StringComparer.Ordinal);
        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteString("form", CanonicalVersion);
            w.WriteNumber("revision", revision);
            w.WriteStartArray("items");
            foreach (var i in items.OrderBy(i => i.NodeKey, StringComparer.Ordinal))
            {
                w.WriteStartObject();
                w.WriteString("nodeKey", i.NodeKey);
                Str(w, "parentKey", i.ParentKey);
                w.WriteString("kind", i.Kind.ToString());
                w.WriteBoolean("isRoot", i.IsRoot);
                w.WriteString("lotId", i.LotId);
                w.WriteString("skuId", i.SkuId.ToString("D"));
                Str(w, "warehouseId", i.WarehouseId);
                Str(w, "locationId", i.LocationId);
                Str(w, "stockStatus", i.StockStatus?.ToString());
                Str(w, "sourceDocumentId", i.SourceDocumentId);
                Str(w, "customerRef", i.CustomerRef);
                Str(w, "quantity", i.Quantity is { } q ? RecallWire.Decimal(q / 1.0000000000000000000000000000m) : null);
                Str(w, "baseUomId", i.BaseUomId);
                w.WriteBoolean("included", EffectivelyIncluded(i, byKey));
                Str(w, "exclusionReason", i.Included ? null : i.ExclusionReason);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    public static string Sha256(IReadOnlyList<RecallScopeItem> items, int revision)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(items, revision)))).ToLowerInvariant();

    private static void Str(Utf8JsonWriter w, string name, string? value)
    {
        if (value is null) w.WriteNull(name);
        else w.WriteString(name, value);
    }
}

/// <summary>
/// Pack §8.6 — effectiveness per (customer, SKU, base unit): shipped − returned − consumed = open (never below 0); percent =
/// (returned + consumed) ÷ shipped × 100, one decimal; met = percent ≥ the class target (KR-G). Different SKUs / units are never summed.
/// R1: shipped = the included shipment items of the approved scope, returned = 0 (return links are R3), consumed = declarations.
/// </summary>
public static class RecallEffectivenessRule
{
    public sealed record Input(string CustomerRef, Guid SkuId, string BaseUomId, decimal Shipped, decimal Returned, decimal Consumed, bool Notified);

    public static RecallEffectivenessResult Compute(int @class, decimal target, IEnumerable<Input> inputs, DateTimeOffset now)
    {
        var rows = inputs
            .GroupBy(i => (i.CustomerRef, i.SkuId, i.BaseUomId))
            .OrderBy(g => g.Key.CustomerRef, StringComparer.Ordinal).ThenBy(g => g.Key.SkuId).ThenBy(g => g.Key.BaseUomId, StringComparer.Ordinal)
            .Select(g => Row(g.Key.CustomerRef, g.Key.SkuId, g.Key.BaseUomId, g.Sum(x => x.Shipped), g.Sum(x => x.Returned), g.Sum(x => x.Consumed),
                g.Any(x => x.Notified), target))
            .ToList();
        var totals = rows
            .GroupBy(r => (r.SkuId, r.BaseUomId))
            .OrderBy(g => g.Key.SkuId).ThenBy(g => g.Key.BaseUomId, StringComparer.Ordinal)
            .Select(g => Row(null, g.Key.SkuId, g.Key.BaseUomId, g.Sum(r => r.Shipped), g.Sum(r => r.Returned), g.Sum(r => r.Consumed),
                g.All(r => r.Notified), target))
            .ToList();
        return new RecallEffectivenessResult { Class = @class, TargetPercent = target, Rows = rows, Totals = totals, ComputedAtUtc = now };
    }

    public static bool HasOpenQuantity(RecallEffectivenessResult result) => result.Totals.Any(t => t.Open > 0m);

    private static RecallEffectivenessRow Row(string? customer, Guid sku, string uom, decimal shipped, decimal returned, decimal consumed, bool notified, decimal target)
    {
        var open = Math.Max(0m, shipped - returned - consumed);
        var percent = shipped == 0m ? 100m : Math.Round((returned + consumed) / shipped * 100m, 1, MidpointRounding.AwayFromZero);
        return new RecallEffectivenessRow
        {
            CustomerRef = customer, SkuId = sku, BaseUomId = uom, Shipped = shipped, Returned = returned, Consumed = consumed, Open = open,
            Percent = percent, Met = percent >= target, Notified = notified
        };
    }
}
