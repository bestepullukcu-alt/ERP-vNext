using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.InventoryService.Application.Features.Expiry.Ports;
using Diten.InventoryService.Domain.Expiry.Entities;
using Diten.InventoryService.Domain.Expiry.Rules;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Application.Features.Expiry.Services;

/// <summary>FEFO-BUNDLE <c>SignableSubject</c> (ESIGN-BUNDLE §6 provider answer).</summary>
public sealed record ExpirySignableSubjectDto(string Display, IReadOnlyList<ExpirySubjectLineDto> Summary, string LegalEntityId, IReadOnlyDictionary<string, string?> Fields);

public sealed record ExpirySubjectLineDto(string Label, string Value);

/// <summary>
/// MOD-0176 pack §8.6 — the signable subjects of 0176 and their canonical hash. ONE place: the subject provider
/// (<c>GET /api/expiry/esign-subjects/…</c>) and the signature verification (<see cref="ESignExpiryResolutionSignature"/>,
/// <see cref="ESignLotDateOverrideSignature"/>) call the same <see cref="ExceptionFields"/> / <see cref="DateOverrideFields"/> and
/// <see cref="Hash"/> (CT decision F3-A: 0176 compares the signed <c>subjectHash</c> with this hash of the CURRENT record).
/// <list type="bullet">
/// <item><c>expiry.exception</c> — field list <c>fx-v1</c>; <c>resolution</c> + <c>resolutionReason</c> are the PREPARED decision
/// (<c>PendingResolution</c>), so the signer signs the decision it intends.</item>
/// <item><c>expiry.date-override</c> — field list <c>do-v1</c>.</item>
/// </list>
/// A changed field name or order changes every hash: a new list needs a new version (golden vectors in the tests).
/// </summary>
public static class ExpirySignableSubjects
{
    public static IReadOnlyDictionary<string, string?> ExceptionFields(FefoExceptionCase c)
    {
        var pending = c.PendingResolution;
        var fields = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["number"] = c.Number,
            ["type"] = ExpiryWire.Code(c.Type),
            ["status"] = c.Status.ToString(),
            ["legalEntityId"] = c.LegalEntityId?.ToString("D"),
            ["itemId"] = c.ItemId.ToString("D"),
            ["skuId"] = c.SkuId.ToString("D"),
            ["skuLevel"] = WireNames.ToWire(c.SkuLevel),
            ["skuCode"] = null, // F1 R-2: the SKU code is not read yet; the key is part of fx-v1 (null until a later list version)
            ["lotId"] = c.LotId,
            ["lotNumber"] = c.LotNumber,
            ["expiryDate"] = Day(c.ExpiryDate),
            ["retestDate"] = Day(c.RetestDate),
            ["warehouseId"] = c.WarehouseId,
            ["quantity"] = c.Quantity is { } q ? Quantity(q) : null,
            ["baseUom"] = c.BaseUomId,
            ["sourceTransactionId"] = c.SourceMovement?.TransactionId,
            ["sourceMovementType"] = c.SourceMovement?.MovementType,
            ["openedAtUtc"] = ExpirySupport.Wire(c.OpenedAtUtc),
            ["resolution"] = (pending?.Resolution ?? c.Resolution)?.ToString(),
            ["resolutionReason"] = pending?.Reason ?? c.ResolutionReason,
            ["version"] = c.Version.ToString(CultureInfo.InvariantCulture)
        };
        foreach (var (key, value) in c.Facts)
        {
            fields["facts." + key] = value;
        }

        return fields;
    }

    public static IReadOnlyDictionary<string, string?> DateOverrideFields(LotDateOverride o) => new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["overrideId"] = o.Id.ToString("D"),
        ["legalEntityId"] = o.LegalEntityId?.ToString("D"),
        ["itemId"] = o.ItemId.ToString("D"),
        ["skuId"] = o.SkuId.ToString("D"),
        ["skuLevel"] = WireNames.ToWire(o.SkuLevel),
        ["lotNumber"] = o.LotNumber,
        ["manufacturingDate"] = Day(o.ManufacturingDate),
        ["suggestedExpiry"] = Day(o.SuggestedExpiry),
        ["enteredExpiry"] = Day(o.EnteredExpiry),
        ["suggestedRetest"] = Day(o.SuggestedRetest),
        ["enteredRetest"] = Day(o.EnteredRetest),
        ["reason"] = o.Reason,
        ["shelfLifeDays"] = o.ShelfLifeDays?.ToString(CultureInfo.InvariantCulture),
        ["retestDays"] = o.RetestDays?.ToString(CultureInfo.InvariantCulture)
    };

    /// <summary>ESIGN-BUNDLE §7 canonical subject — the MOD-0175 <c>QuarantineSignableSubjectBuilder.Canonical</c> method (byte-identical to MOD-0022's).</summary>
    public static string Canonical(string subjectType, string subjectId, IReadOnlyDictionary<string, string?> fields)
    {
        var builder = new StringBuilder();
        builder.Append("{\"module\":").Append(Quote(ExpirySignableActions.Module))
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

    public static string ExceptionHash(FefoExceptionCase c)
        => Hash(ExpirySignableActions.ExceptionSubjectType, c.Id.ToString("D"), ExceptionFields(c));

    public static string DateOverrideHash(LotDateOverride o)
        => Hash(ExpirySignableActions.DateOverrideSubjectType, o.Id.ToString("D"), DateOverrideFields(o));

    public static ExpirySignableSubjectDto ExceptionSubject(FefoExceptionCase c)
    {
        var fields = ExceptionFields(c);
        var decision = fields["resolution"] ?? "—";
        var summary = new List<ExpirySubjectLineDto>
        {
            new("İstisna / Exception", c.Number),
            new("Tür / Type", fields["type"]!),
            new("Lot", c.LotNumber),
            new("SKT / Expiry", fields["expiryDate"] ?? "—"),
            new("Depo / Warehouse", c.WarehouseId ?? "—"),
            new("Miktar / Quantity", fields["quantity"] is { } q ? $"{q} {c.BaseUomId}".Trim() : "—"),
            new("Karar / Decision", decision),
            new("Gerekçe / Reason", fields["resolutionReason"] ?? "—")
        };
        return new ExpirySignableSubjectDto(Cut($"FEFO istisnası {c.Number} · Lot {c.LotNumber} · {decision}", 300), Lines(summary),
            c.LegalEntityId!.Value.ToString("D"), fields);
    }

    public static ExpirySignableSubjectDto DateOverrideSubject(LotDateOverride o)
    {
        var fields = DateOverrideFields(o);
        var summary = new List<ExpirySubjectLineDto>
        {
            new("Lot", o.LotNumber),
            new("Üretim / Manufacturing", fields["manufacturingDate"]!),
            new("Önerilen SKT / Suggested expiry", fields["suggestedExpiry"] ?? "—"),
            new("Girilen SKT / Entered expiry", fields["enteredExpiry"] ?? "—"),
            new("Önerilen retest / Suggested retest", fields["suggestedRetest"] ?? "—"),
            new("Girilen retest / Entered retest", fields["enteredRetest"] ?? "—"),
            new("Gerekçe / Reason", o.Reason)
        };
        return new ExpirySignableSubjectDto(Cut($"Lot tarih sapması · Lot {o.LotNumber} · SKT {fields["enteredExpiry"] ?? "—"}", 300), Lines(summary),
            o.LegalEntityId!.Value.ToString("D"), fields);
    }

    private static List<ExpirySubjectLineDto> Lines(List<ExpirySubjectLineDto> summary)
        => summary.Take(20).Select(s => s with { Label = Cut(s.Label, 60), Value = Cut(s.Value, 300) }).ToList();

    private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];

    private static string? Day(DateTimeOffset? value) => value?.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Quantity(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);

    private static string Quote(string value) => "\"" + value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal)
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\t", "\\t", StringComparison.Ordinal) + "\"";
}
