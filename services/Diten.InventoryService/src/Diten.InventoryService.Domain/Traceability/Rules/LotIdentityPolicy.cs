using System.Text.RegularExpressions;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Domain.Traceability.Rules;

/// <summary>
/// K-G — THE single policy point for lot and serial identity (pack §3, §8 "K-G single policy point"). It alone decides
/// <list type="number">
/// <item>which SKU levels a lot may bind to (today all three: Gsku, Lsku, FinishedGood);</item>
/// <item>how a lot number / serial number is normalised (trim + upper-case) and what shape it must have;</item>
/// <item>the business key: lot = Tenant + SKU (skuId + skuLevel) + LotNumber (owner change of DEC-INV-14,
/// 2026-10-05); serial = Tenant + SKU + serial number (CT-D7).</item>
/// </list>
/// Repositories, indexes and handlers call it; an architecture test fails when another class builds a key. Moving to
/// another scope later (e.g. "Lsku only") happens HERE, together with the data migration of pack §8 steps ①–⑦.
/// </summary>
public static partial class LotIdentityPolicy
{
    public const int MaxLotNumberLength = 40;

    public const int MinSerialLength = 3;

    public const int MaxSerialLength = 24;

    /// <summary>Policy version of the key below (pack §8 step ①: a V2 key is added beside it, never swapped in place).</summary>
    public const int KeyVersion = 1;

    public static IReadOnlyCollection<SkuLevel> AllowedSkuLevels { get; } = [SkuLevel.Gsku, SkuLevel.Lsku, SkuLevel.FinishedGood];

    public static bool MayBindTo(SkuLevel level) => AllowedSkuLevels.Contains(level);

    public static string NormalizeLotNumber(string raw) => raw.Trim().ToUpperInvariant();

    /// <summary>Same shape as the W-UI-02 typed lot (FU01 <c>MovementRules</c>): 1–40 of A–Z 0–9 . / -.</summary>
    public static bool IsValidLotNumber(string? normalized) => normalized is not null && LotNumberShape().IsMatch(normalized);

    public static string LotKey(Guid tenantId, Guid skuId, SkuLevel level, string lotNumber)
        => $"{tenantId:D}|{WireNames.ToWire(level)}|{skuId:D}|{NormalizeLotNumber(lotNumber)}";

    public static string NormalizeSerial(string raw) => raw.Trim().ToUpperInvariant();

    /// <summary>Serial shape of the approved mockup: 3–24 of A–Z 0–9 - (pack §12 UI rules).</summary>
    public static bool IsValidSerial(string? normalized) => normalized is not null && SerialShape().IsMatch(normalized);

    public static string SerialKey(Guid tenantId, Guid skuId, SkuLevel level, string serialNumber)
        => $"{tenantId:D}|{WireNames.ToWire(level)}|{skuId:D}|{NormalizeSerial(serialNumber)}";

    [GeneratedRegex("^[A-Z0-9./-]{1,40}$", RegexOptions.CultureInvariant)]
    private static partial Regex LotNumberShape();

    [GeneratedRegex("^[A-Z0-9-]{3,24}$", RegexOptions.CultureInvariant)]
    private static partial Regex SerialShape();
}
