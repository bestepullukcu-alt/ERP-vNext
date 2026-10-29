using System.Globalization;
using System.Text.RegularExpressions;

namespace Diten.InventoryService.Domain.Warehouse.Rules;

/// <summary>
/// RCS-001 format of the MOD-0178 temporary record codes (pack §8.7, §22; owner decision Q-07 B): <c>{COMPANY}-{TYPE}-{000001}</c>, ASCII.
/// The counter is per (tenant, company prefix, type) and never resets; past <c>999999</c> it simply grows to seven digits
/// (<c>GMG-PUT-1000000</c>, F-12). The company prefix is the MDM legal-entity <c>Code</c> (M-2) and must be 2–5 capital letters A–Z.
/// </summary>
public static partial class WarehouseRecordCodeRules
{
    /// <summary>Putaway task (S1).</summary>
    public const string PutawayTask = "PUT";

    /// <summary>Pick task (S2).</summary>
    public const string PickTask = "PCK";

    /// <summary>Outbound shipment (S2).</summary>
    public const string OutboundShipment = "OUT";

    /// <summary>Package / handling unit (S2).</summary>
    public const string Package = "PKG";

    public static readonly IReadOnlyList<string> TypePrefixes = [PutawayTask, PickTask, OutboundShipment, Package];

    /// <summary>M-2: <c>^[A-Z]{2,5}$</c> — otherwise no new record is opened (422 <c>WMS_COMPANY_CODE_INVALID</c>).</summary>
    public static bool IsValidCompanyCode(string? code) => code is not null && CompanyCode().IsMatch(code);

    public static bool IsKnownTypePrefix(string? type) => type is not null && TypePrefixes.Contains(type, StringComparer.Ordinal);

    /// <summary>The number of counter value <paramref name="sequence"/> (≥ 1): at least six digits, more when the counter outgrows them.</summary>
    public static string Format(string companyCodePrefix, string typePrefix, long sequence)
    {
        if (!IsValidCompanyCode(companyCodePrefix)) throw new ArgumentException("The company prefix must be 2–5 capital letters A–Z.", nameof(companyCodePrefix));
        if (!IsKnownTypePrefix(typePrefix)) throw new ArgumentException($"Unknown record type prefix '{typePrefix}'.", nameof(typePrefix));
        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1L);
        return string.Create(CultureInfo.InvariantCulture, $"{companyCodePrefix}-{typePrefix}-{sequence:D6}");
    }

    [GeneratedRegex("^[A-Z]{2,5}$", RegexOptions.CultureInvariant)]
    private static partial Regex CompanyCode();
}
