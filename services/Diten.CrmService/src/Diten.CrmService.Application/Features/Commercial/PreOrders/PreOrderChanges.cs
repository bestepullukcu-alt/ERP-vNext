using System.Globalization;
using Diten.CrmService.Domain.Entities.Commercial;

namespace Diten.CrmService.Application.Features.Commercial.PreOrders;

/// <summary>
/// WP-CRM-SYNC-S0B B0a (F8-b, CT decision D-1) — MOD-0168 §21 "Before/After" kept in the module's OWN history: the order's
/// event payload (created / updated / signature-cleared) and the settings change history. Invariant text only (the event
/// payload's rule: a code + parameters, the client words it). Never here: the link token, the signature image, e-mail
/// addresses (§21 last row).
/// </summary>
public static class PreOrderChanges
{
    public const string Lines = "lines";
    public const string Depot = "depot";
    public const string Note = "note";
    public const string Date = "date";
    public const string Pharmacy = "pharmacy";
    public const string Visit = "visit";
    public const string Code = "code";
    public const string BinaryRef = "binaryRef";
    public const string SignerName = "signerName";

    public const string OldSuffix = ".old";
    public const string NewSuffix = ".new";

    /// <summary>The editable part of a draft as invariant text: what §21 names (Lines, Depot, Note, Date) and — CT decision
    /// K-3, 2026-11-13 — the pharmacy and the visit the update can also replace. Pharmacy, depot and visit are IDS only
    /// (no name, address or person data): a changed pharmacy clears the signature, so who changed it must be traceable.</summary>
    public sealed record DraftSnapshot(string? Lines, string? Depot, string? Note, string? Date, string? Pharmacy, string? Visit)
    {
        public static DraftSnapshot Of(PreOrder order)
            => new(LinesText(order.Lines), order.Depot?.AccountId.ToString("D"), Empty(order.Note),
                order.RequestedDeliveryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                order.Pharmacy.AccountId == Guid.Empty ? null : order.Pharmacy.AccountId.ToString("D"), order.VisitId?.ToString("D"));
    }

    /// <summary>The <c>updated</c> event's payload: <c>{field}.old</c> / <c>{field}.new</c> for the fields whose value
    /// changed — nothing else; empty when nothing changed.</summary>
    public static Dictionary<string, string?> UpdatedPayload(DraftSnapshot before, DraftSnapshot after)
    {
        var payload = new Dictionary<string, string?>();
        Add(payload, Lines, before.Lines, after.Lines);
        Add(payload, Depot, before.Depot, after.Depot);
        Add(payload, Note, before.Note, after.Note);
        Add(payload, Date, before.Date, after.Date);
        Add(payload, Pharmacy, before.Pharmacy, after.Pharmacy);
        Add(payload, Visit, before.Visit, after.Visit);
        return payload;
    }

    /// <summary>The <c>created</c> event's payload (§21: After — Code, Pharmacy, Depot, Lines).</summary>
    public static Dictionary<string, string?> CreatedPayload(PreOrder order)
        => new()
        {
            [Code] = order.Code,
            [Pharmacy] = order.Pharmacy.AccountId.ToString("D"),
            [Depot] = order.Depot?.AccountId.ToString("D"),
            [Lines] = LinesText(order.Lines)
        };

    /// <summary>The <c>signature-cleared</c> event's payload: which signature was removed (its content id and the name of
    /// the signer — never the image).</summary>
    public static Dictionary<string, string?> SignatureClearedPayload(PreOrderSignature signature)
        => new() { [BinaryRef] = signature.BinaryRef.ToString("D"), [SignerName] = signature.SignerName };

    /// <summary>One line = <c>{SKU code} q={quantity}[ mf={buy}+{free}][ d={discount %}]</c>, joined by "; " in line order;
    /// null for no lines.</summary>
    public static string? LinesText(IReadOnlyList<PreOrderLine> lines)
        => lines.Count == 0
            ? null
            : string.Join("; ", lines.Select(l =>
                string.Create(CultureInfo.InvariantCulture, $"{l.Sku.Code} q={l.Quantity}")
                + (l.Mf is { } mf ? string.Create(CultureInfo.InvariantCulture, $" mf={mf.Buy}+{mf.Free}") : string.Empty)
                + (l.DiscountPct is { } d ? string.Create(CultureInfo.InvariantCulture, $" d={d}") : string.Empty)));

    /// <summary>Every settings field as invariant text, in a fixed order (§21: Before/After — all fields).</summary>
    public static IReadOnlyList<(string Field, string? Value)> SettingsFields(PreOrderSettings s) =>
    [
        ("approvalMode", s.ApprovalMode),
        ("approvalLinkTtlHours", s.ApprovalLinkTtlHours.ToString(CultureInfo.InvariantCulture)),
        ("transmissionRetryMax", s.TransmissionRetryMax.ToString(CultureInfo.InvariantCulture)),
        ("priceTierByCountry", Empty(string.Join(",", s.PriceTierByCountry.OrderBy(t => t.CountryCode, StringComparer.Ordinal).Select(t => $"{t.CountryCode}={t.Tier}")))),
        ("companyPrefixes", Empty(string.Join(",", s.CompanyPrefixes.OrderBy(p => p.LegalEntityId).Select(p => $"{p.LegalEntityId:D}={p.Prefix}"))))
    ];

    /// <summary>The settings fields whose value differs (old → new), in the fixed order.</summary>
    public static List<PreOrderSettingsFieldChange> SettingsDiff(IReadOnlyList<(string Field, string? Value)> before,
        IReadOnlyList<(string Field, string? Value)> after)
        => before.Zip(after)
            .Where(x => !string.Equals(x.First.Value, x.Second.Value, StringComparison.Ordinal))
            .Select(x => new PreOrderSettingsFieldChange { Field = x.First.Field, Old = x.First.Value, New = x.Second.Value })
            .ToList();

    private static void Add(Dictionary<string, string?> payload, string field, string? before, string? after)
    {
        if (!string.Equals(before, after, StringComparison.Ordinal))
        {
            payload[field + OldSuffix] = before;
            payload[field + NewSuffix] = after;
        }
    }

    private static string? Empty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
