using Diten.Platform.Application.Features.BusinessReferenceData.Models;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

/// <summary>
/// Single source of truth for the supported Business Reference Data scope types (PSS-012).
/// The stewardship UI (set create + list filter) is driven from <see cref="Options"/>, while
/// internal scope validation in the consumer query path uses the lowercase <see cref="Codes"/>.
/// Keeping both derived from one list prevents the filter/modal drift this consolidates.
/// </summary>
public static class BusinessReferenceDataScopeTypes
{
    public static IReadOnlyList<BusinessReferenceDataScopeTypeModel> Options { get; } =
    [
        new("Global", "Global"),
        new("Tenant", "Tenant"),
        new("Company", "Company"),
        new("Region", "Region")
    ];

    public static IReadOnlyList<string> Codes { get; } =
        Options.Select(option => option.Value.ToLowerInvariant()).ToList();
}
