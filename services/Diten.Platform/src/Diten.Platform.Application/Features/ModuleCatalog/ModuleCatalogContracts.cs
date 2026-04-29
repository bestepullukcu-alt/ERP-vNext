using System.Globalization;
using System.Text;

namespace Diten.Platform.Application.Features.ModuleCatalog;

public sealed record DomainLandscapeDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedDate,
    DateTimeOffset? ModifiedDate);

public sealed record SuitePlatformDto(
    Guid Id,
    string Code,
    string Name,
    Guid DomainLandscapeId,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedDate,
    DateTimeOffset? ModifiedDate);

public sealed record CapabilityGroupDto(
    Guid Id,
    string Code,
    string Name,
    Guid DomainLandscapeId,
    Guid SuitePlatformId,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedDate,
    DateTimeOffset? ModifiedDate);

public sealed record ModuleDefinitionListItemDto(
    Guid Id,
    string ModuleId,
    string ModuleName,
    Guid DomainLandscapeId,
    Guid SuitePlatformId,
    Guid CapabilityGroupId,
    string DomainLandscapeName,
    string SuitePlatformName,
    string CapabilityGroupName,
    string Status,
    bool IsPlatformCore,
    bool IsTenantAssignable,
    string? SupportModel,
    DateTimeOffset CreatedDate,
    DateTimeOffset? ModifiedDate);

public sealed record ModuleDefinitionDetailDto(
    Guid Id,
    string ModuleId,
    string ModuleName,
    Guid DomainLandscapeId,
    string DomainLandscapeName,
    Guid SuitePlatformId,
    string SuitePlatformName,
    Guid CapabilityGroupId,
    string CapabilityGroupName,
    string? DependencyGate,
    string? DeliveryOutcome,
    string? Placement,
    string? SupportModel,
    string Status,
    bool IsPlatformCore,
    bool IsTenantAssignable,
    DateTimeOffset CreatedDate,
    DateTimeOffset? ModifiedDate);

public sealed record ModuleCatalogSummaryDto(
    int TotalDomains,
    int TotalSuites,
    int TotalCapabilityGroups,
    int TotalModules,
    int TenantAssignableModules,
    int PlatformCoreModules,
    int DeprecatedOrRetiredModules);

public sealed record ModuleCatalogHierarchyDto(
    IReadOnlyList<DomainLandscapeDto> DomainLandscapes,
    IReadOnlyList<SuitePlatformDto> SuitePlatforms,
    IReadOnlyList<CapabilityGroupDto> CapabilityGroups,
    ModuleCatalogSummaryDto Summary);

public sealed record ModuleDefinitionListResultDto(
    IReadOnlyList<ModuleDefinitionListItemDto> Items,
    long TotalCount);

public sealed record ModulePageDefinitionDto(
    Guid Id,
    string ModuleId,
    string PageCode,
    string PageName,
    string? Description,
    string? RoutePath,
    string PageType,
    string? RequiredPermissionKey,
    bool IsNavigationCandidate,
    bool IsActive,
    DateTimeOffset CreatedDate,
    DateTimeOffset? ModifiedDate);

public sealed record ModuleCatalogImportRowDto(
    string? ModuleId,
    string? DomainLandscape,
    string? SuitePlatform,
    string? CapabilityGroup,
    string? ModuleName,
    string? DependencyGate,
    string? DeliveryOutcome,
    string? Placement,
    string? SupportModel,
    bool? IsPlatformCore,
    bool? IsTenantAssignable,
    string? Status);

public sealed record ModuleCatalogImportRowErrorDto(
    int RowNumber,
    string? ModuleId,
    string? ModuleName,
    string ErrorMessage);

public sealed record ModuleCatalogImportResultDto(
    int CreatedCount,
    int UpdatedCount,
    int SkippedCount,
    int FailedCount,
    IReadOnlyList<ModuleCatalogImportRowErrorDto> FailedRows);

public static class ModuleCatalogCodeNormalizer
{
    public static string NormalizeToCode(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var normalized = input.Trim().Normalize(NormalizationForm.FormD);
        var buffer = new List<char>(normalized.Length);
        var previousDash = false;

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                buffer.Add(char.ToUpperInvariant(ch));
                previousDash = false;
                continue;
            }

            if (previousDash || buffer.Count == 0)
            {
                continue;
            }

            buffer.Add('-');
            previousDash = true;
        }

        while (buffer.Count > 0 && buffer[^1] == '-')
        {
            buffer.RemoveAt(buffer.Count - 1);
        }

        return new string(buffer.ToArray());
    }
}
