using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Territory.Models;

/// <summary>
/// MOD-0151 FU02A: normalizes + fail-closed validates the Business Unit scopes on a create/update command. Rules
/// (pack §9 / §20): scopeType must be <c>business-unit</c> (brand/product rejected), scopeCode required, duplicates
/// collapsed, and every code must be a MOD-0048 published <c>business-unit</c> value (no hardcoded fallback — an
/// unpublished set fails closed). Returns the persisted value objects or a controlled error message.
/// </summary>
public static class TerritoryBusinessScopeResolver
{
    public static async Task<(List<TerritoryBusinessScope> Scopes, string? Error)> ResolveAsync(
        IReadOnlyList<TerritoryBusinessScopeInput>? input,
        ITerritoryReferenceValidator references,
        CancellationToken cancellationToken)
    {
        var result = new List<TerritoryBusinessScope>();
        if (input is null || input.Count == 0)
        {
            return (result, null);
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in input)
        {
            var scopeType = raw.ScopeType?.Trim();
            var scopeCode = raw.ScopeCode?.Trim();

            if (string.IsNullOrWhiteSpace(scopeCode))
            {
                return (result, "BusinessScopes: scopeCode is required.");
            }

            // FU02A supports ONLY business-unit scopes; brand-group / product-portfolio are later FUs.
            if (!string.Equals(scopeType, TerritoryReferenceSets.BusinessUnitScopeType, StringComparison.OrdinalIgnoreCase))
            {
                return (result, $"BusinessScopes: only scopeType '{TerritoryReferenceSets.BusinessUnitScopeType}' is supported (got '{raw.ScopeType}').");
            }

            if (!seen.Add(scopeCode))
            {
                continue; // duplicate scopeCode — collapse silently
            }

            var check = await references.ValidateValueAsync(TerritoryReferenceSets.BusinessUnitValueSet, scopeCode, cancellationToken);
            if (check != ReferenceValidationStatus.Valid)
            {
                return (result, ReferenceError(TerritoryReferenceSets.BusinessUnitValueSet, check));
            }

            result.Add(new TerritoryBusinessScope
            {
                ScopeType = TerritoryReferenceSets.BusinessUnitScopeType,
                ScopeCode = scopeCode
            });
        }

        return (result, null);
    }

    private static string ReferenceError(string setCode, ReferenceValidationStatus status) => status switch
    {
        ReferenceValidationStatus.SetMissing => $"Required reference set '{setCode}' is not published yet (MOD-0048 authoring pending).",
        _ => $"'{setCode}' does not contain the required published value."
    };
}
