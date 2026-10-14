namespace Diten.CrmService.Application.Features.VisitWorkspace;

/// <summary>
/// WP-VW-W2 (A1) — the reference set the cancel / missed / reschedule reasons come from, and the value attributes the CRM
/// reads. The VALUES are not here: they are reference data (MOD-0048, Platform catalog <c>crm-visit-reference.json</c>),
/// read through the published-values seam. Only the set code and the attribute names are code.
/// </summary>
public static class VisitOutcomeReasons
{
    public const string ReasonSet = "visit-outcome-reason";

    /// <summary>Comma-separated subset of cancel / missed / reschedule.</summary>
    public const string AppliesToAttribute = "applies_to";

    /// <summary>"true" ⇒ a note is required with the reason.</summary>
    public const string RequiresNoteAttribute = "requires_note";

    /// <summary>Per-language label attribute prefix (<c>label_tr</c> …); English is the value's display name (4I rule).</summary>
    public const string LabelAttributePrefix = "label_";

    public const string Cancel = "cancel";
    public const string Missed = "missed";
    public const string Reschedule = "reschedule";

    public static readonly IReadOnlyList<string> AppliesToAll = new[] { Cancel, Missed, Reschedule };

    public static bool IsKnownAppliesTo(string? value)
        => value is not null && AppliesToAll.Contains(value.Trim().ToLowerInvariant(), StringComparer.Ordinal);

    /// <summary>Does the attribute value list <paramref name="appliesTo"/>?</summary>
    public static bool Applies(IReadOnlyDictionary<string, string>? attributes, string appliesTo)
        => attributes is not null
           && attributes.TryGetValue(AppliesToAttribute, out var raw)
           && raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
               .Contains(appliesTo, StringComparer.OrdinalIgnoreCase);

    public static bool RequiresNote(IReadOnlyDictionary<string, string>? attributes)
        => attributes is not null
           && attributes.TryGetValue(RequiresNoteAttribute, out var raw)
           && string.Equals(raw?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
}
