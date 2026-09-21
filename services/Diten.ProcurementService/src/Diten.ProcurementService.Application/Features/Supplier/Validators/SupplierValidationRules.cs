namespace Diten.ProcurementService.Application.Features.Supplier.Validators;

/// <summary>Ortak validation yardımcıları (contact.type enum — MOD-0140 §4/§12).</summary>
internal static class SupplierValidationRules
{
    private static readonly HashSet<string> ContactTypes =
        new(StringComparer.OrdinalIgnoreCase) { "primary", "billing", "quality", "logistics" };

    public static bool IsValidContactType(string? type)
        => !string.IsNullOrWhiteSpace(type) && ContactTypes.Contains(type);
}
