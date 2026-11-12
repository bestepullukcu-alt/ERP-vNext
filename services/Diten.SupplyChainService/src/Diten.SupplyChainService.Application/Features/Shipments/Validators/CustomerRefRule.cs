using System.Text.RegularExpressions;
namespace Diten.SupplyChainService.Application.Features.Shipments.Validators;
/// <summary>SHIPMENT-BUNDLE 3.5.0 <c>CustomerRef</c> pattern (MOD-0183 r9 §24.1): a CRM Account.Id as a Guid in "D" format,
/// lower case (<c>\z</c>, so a trailing newline does not pass as JSON Schema's <c>$</c> would not). The service checks the format only and never calls CRM.</summary>
public static partial class CustomerRefRule
{
    [GeneratedRegex("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
    public static bool IsValid(string? value) => value is not null && Pattern().IsMatch(value);
}
