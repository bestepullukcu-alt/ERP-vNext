using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementAccessProfileTemplates;

/// <summary>MOD-0029-FU05 permission constants (reuses the FU04 access-matrix manage/view keys).</summary>
public static class AccessProfileTemplatePermissions
{
    public const string View = "platform.document-management.access.view";
    public const string Manage = "platform.document-management.access.manage";
}

public static class AccessProfileTemplateReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFoundNonLeakage = "NOT_FOUND_NON_LEAKAGE";
    public const string BaselineNotEffective = "BASELINE_NOT_EFFECTIVE";
    public const string ScopeNotApplicable = "SCOPE_NOT_APPLICABLE";
}

/// <summary>Logical roles referenced by the access-profile templates; mapped to concrete role principal ids by config.</summary>
public enum LogicalTemplateRole
{
    QaDocumentation,
    Gqd,
    LocalQa,
    RecordsOwner,
    Hr,
    Legal,
    CountryManager,
    DepartmentOwner,
    SiteQa,
    SiteHead,
    OwnerFunction,
    AllUsers
}

/// <summary>Generation scope: preview-only per definition node, or runtime-enforced per company instance node.</summary>
public enum AccessProfileTemplateScope
{
    Definition = 0,
    Instance = 1
}

/// <summary>One template rule: a role gets a set of actions with an effect. Resolved to a concrete principal later.</summary>
public sealed record TemplatePolicySpec(
    LogicalTemplateRole Role,
    IReadOnlyList<DocumentAccessMatrixAction> Actions,
    DocumentAccessEffect Effect);

/// <summary>A fully-resolved desired policy targeting one instance/definition node (principal id already resolved).</summary>
public sealed record DesiredAccessPolicy(
    DocumentAccessTargetType TargetType,
    string TargetId,
    string PrincipalId,
    IReadOnlyList<DocumentAccessMatrixAction> Actions,
    DocumentAccessEffect Effect,
    string TemplateKey,
    LogicalTemplateRole Role,
    string? SourceRegisterFolderId,
    Guid? SourceCollectionDefinitionId,
    Guid? SourceCollectionInstanceId);

public sealed record AccessProfileTemplateRequest(
    Guid BaselineReleaseId,
    AccessProfileTemplateScope Scope,
    IReadOnlyList<string>? IncludeProfiles,
    IReadOnlyList<string>? ExcludeProfiles,
    bool ApplyReadOnlyStatusFolderRules,
    bool DryRun);

public sealed record AccessProfileCountModel(string AccessProfile, int NodeCount, bool Known);

public sealed record AccessProfileTemplateSummary(
    Guid BaselineReleaseId,
    string BaselineStatus,
    string Scope,
    bool DryRun,
    int NodesConsidered,
    int PoliciesPlanned,
    int Created,
    int Updated,
    int SkippedManual,
    int SkippedUnchanged,
    IReadOnlyList<AccessProfileCountModel> Profiles,
    IReadOnlyList<string> UnknownProfiles,
    IReadOnlyList<string> MissingPrincipalRoles,
    IReadOnlyList<string> Warnings);
