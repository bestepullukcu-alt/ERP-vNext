using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Domain.Recall.Entities;

namespace Diten.InventoryService.Application.Features.Recall.Services;

/// <summary>RECALL <c>SignableSubject</c> (ESIGN-BUNDLE §6 provider answer).</summary>
public sealed record RecallSignableSubjectDto(string Display, IReadOnlyList<RecallSubjectLineDto> Summary, string LegalEntityId, IReadOnlyDictionary<string, string?> Fields);

public sealed record RecallSubjectLineDto(string Label, string Value);

/// <summary>
/// What <c>rc-v1</c> needs besides the case row: the scope counts of the case's CURRENT revision (the same <c>RecallScopeRules</c> the scope view
/// uses) and the effectiveness totals per SKU + unit — computed live for a prepared completion, the stored result for a prepared closure
/// (<see cref="RecallSubjectFacts"/>), none otherwise.
/// </summary>
public sealed record RecallCaseSubjectFacts(int ExcludedCount, int IncludedStockCount, int IncludedShipmentCount, RecallEffectivenessResult? Effectiveness);

/// <summary><c>dr-v1</c> input — the drill run of R5 (the entity does not exist yet; the field list and its golden vector do).</summary>
public sealed record RecallDrillRunSubjectFacts(
    Guid RunId,
    string Number,
    string PlanCode,
    string LotId,
    string LotNumber,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    int? DurationMinutes,
    int TargetHours,
    bool? Passed,
    int GapCount,
    string? SnapshotSha256,
    string? ResultNote,
    int Version);

/// <summary>
/// MOD-0177 pack §8.8 (WP-0177-R4) — the signable subjects of 0177 and their canonical hash. ONE place: the subject provider
/// (<c>GET /api/recall/esign-subjects/…</c>) and the signature verification (<see cref="ESignRecallSignature"/>) call the same field functions and
/// <see cref="Hash"/> (CT decision F3-A: 0177 compares the signed <c>subjectHash</c> with this hash of the CURRENT record).
/// <list type="bullet">
/// <item><c>recall.case</c> — <c>rc-v1</c>. <c>submittedSnapshotSha256</c> / <c>approvedSnapshotSha256</c> are the trace seals: signing the scope
/// signs exactly the frozen trace. <c>pendingAction</c> + <c>pendingActionPayloadSha256</c> are the PREPARED decision.</item>
/// <item><c>recall.drill-run</c> — <c>dr-v1</c> (R5 binds it).</item>
/// <item><c>recall.settings-change</c> — <c>rs-v1</c>, the prepared change (<see cref="RecallSettingsChange"/>).</item>
/// </list>
/// A changed field name or order changes every hash: a new list needs a new version (golden vectors in the tests).
/// </summary>
public static class RecallSignableSubjects
{
    public static IReadOnlyDictionary<string, string?> CaseFields(RecallCase c, RecallCaseSubjectFacts facts)
    {
        var fields = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["number"] = c.Number,
            ["status"] = c.Status.ToString(),
            ["class"] = I(c.Class),
            ["level"] = c.Level.ToString(),
            ["legalEntityId"] = c.LegalEntityId?.ToString("D"),
            ["rootLotId"] = c.RootLotId,
            ["rootLotNumber"] = c.RootLotNumber,
            ["skuId"] = c.SkuId.ToString("D"),
            ["direction"] = c.Direction.ToString(),
            ["reason"] = c.Reason,
            ["reasonReference"] = c.ReasonReference,
            ["scopeRevision"] = I(c.ScopeRevision),
            ["submittedSnapshotSha256"] = c.SubmittedScope?.Sha256,
            ["approvedSnapshotSha256"] = c.ApprovedScope?.Sha256,
            ["excludedCount"] = I(facts.ExcludedCount),
            ["includedStockCount"] = I(facts.IncludedStockCount),
            ["includedShipmentCount"] = I(facts.IncludedShipmentCount),
            ["pendingAction"] = c.PendingAction?.Action.ToString(),
            ["pendingActionPayloadSha256"] = c.PendingAction?.PayloadSha256,
            ["closureSummary"] = c.ClosureSummary,
            ["openQuantityAcceptance"] = c.OpenQuantityAcceptance,
            ["version"] = I(c.Version)
        };
        if (facts.Effectiveness is { } e)
        {
            // flattened per SKU + unit (quantities of different SKUs / units are never summed)
            fields["effectiveness.targetPercent"] = Number(e.TargetPercent);
            foreach (var t in e.Totals.OrderBy(t => t.SkuId).ThenBy(t => t.BaseUomId, StringComparer.Ordinal))
            {
                var prefix = $"effectiveness.{t.SkuId:D}.{t.BaseUomId}.";
                fields[prefix + "shipped"] = Number(t.Shipped);
                fields[prefix + "returned"] = Number(t.Returned);
                fields[prefix + "consumed"] = Number(t.Consumed);
                fields[prefix + "open"] = Number(t.Open);
                fields[prefix + "percent"] = Number(t.Percent);
                fields[prefix + "met"] = t.Met ? "true" : "false";
            }
        }

        return fields;
    }

    public static IReadOnlyDictionary<string, string?> DrillRunFields(RecallDrillRunSubjectFacts d) => new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["number"] = d.Number,
        ["planCode"] = d.PlanCode,
        ["lotId"] = d.LotId,
        ["lotNumber"] = d.LotNumber,
        ["startedAtUtc"] = RecallSupport.Wire(d.StartedAtUtc),
        ["endedAtUtc"] = RecallSupport.Wire(d.EndedAtUtc),
        ["durationMinutes"] = d.DurationMinutes is { } m ? I(m) : null,
        ["targetHours"] = I(d.TargetHours),
        ["passed"] = d.Passed is { } p ? (p ? "true" : "false") : null,
        ["gapCount"] = I(d.GapCount),
        ["snapshotSha256"] = d.SnapshotSha256,
        ["resultNote"] = d.ResultNote,
        ["version"] = I(d.Version)
    };

    public static IReadOnlyDictionary<string, string?> SettingsChangeFields(RecallSettingsChange change)
    {
        var fields = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["legalEntityId"] = change.LegalEntityId?.ToString("D"),
            ["settingsVersion"] = I(change.SettingsVersion),
            ["reason"] = change.Reason
        };
        foreach (var c in change.Changes)
        {
            fields["changes." + c.Field] = $"{c.Old}|{c.New}";
        }

        return fields;
    }

    /// <summary>ESIGN-BUNDLE §7 canonical subject — the MOD-0175 / MOD-0176 method (byte-identical to MOD-0022's), module <c>recall</c>.</summary>
    public static string Canonical(string subjectType, string subjectId, IReadOnlyDictionary<string, string?> fields)
    {
        var builder = new StringBuilder();
        builder.Append("{\"module\":").Append(Quote(RecallSignableActions.Module))
            .Append(",\"subjectType\":").Append(Quote(subjectType))
            .Append(",\"subjectId\":").Append(Quote(subjectId))
            .Append(",\"fields\":{");
        var first = true;
        foreach (var pair in fields.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (!first) builder.Append(',');
            first = false;
            builder.Append(Quote(pair.Key)).Append(':').Append(pair.Value is null ? "null" : Quote(pair.Value));
        }

        return builder.Append("}}").ToString();
    }

    /// <summary>SHA-256 (lower-case hex) of <see cref="Canonical"/> — the <c>subjectHash</c> MOD-0022 stores at signing.</summary>
    public static string Hash(string subjectType, string subjectId, IReadOnlyDictionary<string, string?> fields)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(subjectType, subjectId, fields)))).ToLowerInvariant();

    public static string CaseHash(RecallCase c, RecallCaseSubjectFacts facts)
        => Hash(RecallSignableActions.CaseSubjectType, c.Id.ToString("D"), CaseFields(c, facts));

    public static string DrillRunHash(RecallDrillRunSubjectFacts d)
        => Hash(RecallSignableActions.DrillRunSubjectType, d.RunId.ToString("D"), DrillRunFields(d));

    public static string SettingsChangeHash(RecallSettingsChange change)
        => Hash(RecallSignableActions.SettingsChangeSubjectType, change.Id.ToString("D"), SettingsChangeFields(change));

    public static RecallSignableSubjectDto CaseSubject(RecallCase c, RecallCaseSubjectFacts facts)
    {
        var fields = CaseFields(c, facts);
        var decision = fields["pendingAction"] ?? "—";
        var summary = new List<RecallSubjectLineDto>
        {
            new("Vaka / Case", c.Number),
            new("Sınıf / Class", fields["class"]!),
            new("Kök lot / Root lot", c.RootLotNumber),
            new("Kapsam revizyonu / Scope revision", fields["scopeRevision"]!),
            new("Gönderilen iz / Submitted trace", fields["submittedSnapshotSha256"] ?? "—"),
            new("Onaylanan iz / Approved trace", fields["approvedSnapshotSha256"] ?? "—"),
            new("Hariç / Excluded", fields["excludedCount"]!),
            new("Stok · Sevkiyat / Stock · Shipments", $"{fields["includedStockCount"]} · {fields["includedShipmentCount"]}"),
            new("Karar / Decision", decision)
        };
        if (c.ClosureSummary is { } closure) summary.Add(new("Kapanış özeti / Closure summary", closure));
        return new RecallSignableSubjectDto(Cut($"Geri çağırma {c.Number} · Lot {c.RootLotNumber} · {decision}", 300), Lines(summary),
            c.LegalEntityId!.Value.ToString("D"), fields);
    }

    public static RecallSignableSubjectDto SettingsChangeSubject(RecallSettingsChange change)
    {
        var fields = SettingsChangeFields(change);
        var summary = change.Changes.Select(c => new RecallSubjectLineDto(c.Field, $"{c.Old} → {c.New}")).ToList();
        summary.Add(new("Gerekçe / Reason", change.Reason));
        return new RecallSignableSubjectDto(Cut($"Geri çağırma ayarları · sürüm {change.SettingsVersion} · {change.Changes.Count} alan", 300), Lines(summary),
            change.LegalEntityId!.Value.ToString("D"), fields);
    }

    private static List<RecallSubjectLineDto> Lines(List<RecallSubjectLineDto> summary)
        => summary.Take(20).Select(s => s with { Label = Cut(s.Label, 60), Value = Cut(s.Value, 300) }).ToList();

    private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];

    private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Number(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);

    private static string Quote(string value) => "\"" + value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal)
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\t", "\\t", StringComparison.Ordinal) + "\"";
}
