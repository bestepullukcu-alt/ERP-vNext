using System.Globalization;

namespace Diten.ProcurementService.Application.Features.Contract.Validators;

/// <summary>
/// Ortak Contract validation yardımcıları (MOD-0144 §12). Tarih alanları ISO date string (yyyy-MM-dd); geçersiz →
/// reddedilir. EffectiveTo ≥ EffectiveFrom kuralı burada tekilleştirilir (validator + handler paylaşır).
/// </summary>
internal static class ContractValidationRules
{
    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>ISO tarih (yyyy-MM-dd) parse edilebilir mi.</summary>
    public static bool IsValidDate(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && DateOnly.TryParseExact(value!.Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    public static bool TryParseDate(string? value, out DateOnly parsed)
        => DateOnly.TryParseExact((value ?? string.Empty).Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed);

    /// <summary>EffectiveTo verilmemişse geçerli; verilmişse ikisi de parse edilebilmeli ve to ≥ from olmalı.</summary>
    public static bool IsEffectiveRangeValid(string? effectiveFrom, string? effectiveTo)
    {
        if (string.IsNullOrWhiteSpace(effectiveTo))
        {
            return true;
        }
        return TryParseDate(effectiveFrom, out var from)
               && TryParseDate(effectiveTo, out var to)
               && to >= from;
    }
}
