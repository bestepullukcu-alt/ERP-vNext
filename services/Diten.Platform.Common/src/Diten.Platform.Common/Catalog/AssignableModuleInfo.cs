namespace Diten.Platform.Common.Catalog;

public sealed record AssignableModuleInfo(
    Guid Id,
    string ModuleCode,
    string ModuleName,
    string DisplayName,
    string? Description,
    string Domain,
    string Service,
    string Status,
    string ModuleVersion,
    bool IsCoreModule,
    bool IsTenantAssignable,
    int SortOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
