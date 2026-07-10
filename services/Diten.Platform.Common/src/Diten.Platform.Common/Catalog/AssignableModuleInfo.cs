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
    DateTimeOffset? UpdatedAt,
    string? Icon = null, // FIX-MODULE-ICON — module default sidebar icon (boxicons class); null → nav fallback.
    // FEAT-BASELINE-MODULES — HARD (code-owned via manifest): a baseline module is entitlement-free (every tenant
    // auto-has it). Threaded through so the Add-Module picker can exclude baseline modules from the grantable list —
    // they are never manually entitled. Defaults false so legacy positional callers stay non-baseline.
    bool IsBaseline = false);
