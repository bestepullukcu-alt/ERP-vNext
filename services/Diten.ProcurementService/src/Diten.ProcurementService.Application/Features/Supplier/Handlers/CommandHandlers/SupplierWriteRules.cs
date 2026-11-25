using Diten.ProcurementService.Application.Features.Supplier.Store;
using Diten.ProcurementService.Domain.Entities;

namespace Diten.ProcurementService.Application.Features.Supplier.Handlers.CommandHandlers;

/// <summary>Normalization and comparison rules the supplier write handlers share (one place, no drift).</summary>
internal static class SupplierWriteRules
{
    public static List<SupplierContact> MapContacts(List<SupplierContactInput>? contacts)
        => (contacts ?? new List<SupplierContactInput>())
            .Select(c => new SupplierContact { Type = c.Type, Email = c.Email, Phone = c.Phone, Name = c.Name })
            .ToList();

    public static string? NormalizeCountry(string? country)
        => string.IsNullOrWhiteSpace(country) ? null : country.Trim().ToLowerInvariant();

    public static string? NormalizeTaxId(string? taxId)
        => string.IsNullOrWhiteSpace(taxId) ? null : taxId.Trim();

    public static string GenerateSupplierId()
        => "SUP-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    /// <summary>True when the body would change a common field (name, country, tax number, contacts — K-1, §14).</summary>
    public static bool ChangesCommonFields(SupplierSnapshot current, string name, string? country, string? taxId, List<SupplierContactInput>? contacts)
    {
        if (!string.Equals(name.Trim(), current.Name, StringComparison.Ordinal)
            || !string.Equals(NormalizeCountry(country), current.Country, StringComparison.Ordinal)
            || !string.Equals(NormalizeTaxId(taxId), current.TaxId, StringComparison.Ordinal))
        {
            return true;
        }

        var next = MapContacts(contacts);
        return next.Count != current.Contacts.Count
            || next.Where((c, i) => !SameContact(c, current.Contacts[i])).Any();
    }

    private static bool SameContact(SupplierContact a, SupplierContact b)
        => string.Equals(a.Type, b.Type, StringComparison.Ordinal)
           && string.Equals(a.Email, b.Email, StringComparison.Ordinal)
           && string.Equals(a.Phone, b.Phone, StringComparison.Ordinal)
           && string.Equals(a.Name, b.Name, StringComparison.Ordinal);
}
