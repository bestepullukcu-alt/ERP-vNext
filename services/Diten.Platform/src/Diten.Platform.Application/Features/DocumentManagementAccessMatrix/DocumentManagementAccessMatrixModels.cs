using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementAccessMatrix;

public static class DocumentManagementAccessPermissions
{
    public const string View = "platform.document-management.access.view";
    public const string Manage = "platform.document-management.access.manage";
    public const string Preview = "platform.document-management.access.preview";
    public const string AuditView = "platform.document-management.access.audit.view";
}

public static class AccessMatrixReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string Conflict = "CONFLICT";
    public const string DuplicatePolicy = "DUPLICATE_POLICY";
    public const string NotFoundNonLeakage = "NOT_FOUND_NON_LEAKAGE";
    public const string InvalidTarget = "INVALID_TARGET";
    public const string InvalidPrincipal = "INVALID_PRINCIPAL";
    public const string InvalidAction = "INVALID_ACTION";
    public const string GroupPrincipalUnavailable = "GROUP_PRINCIPAL_UNAVAILABLE";
}

public sealed record DocumentAccessPolicyListFilter(
    string? TargetType,
    string? TargetId,
    string? PrincipalType,
    string? PrincipalId,
    string? Effect,
    string? Action,
    string? Status);

public sealed record DocumentAccessPolicyInput(
    string TargetType,
    string TargetId,
    string PrincipalType,
    string PrincipalId,
    IReadOnlyList<string> Actions,
    string Effect,
    bool InheritFromParent,
    Guid? SourcePolicyId,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    string? Status,
    string? Reason);

public sealed record DocumentAccessPolicyListItemModel(
    Guid Id,
    string TargetType,
    string TargetId,
    string? TargetLabel,
    string PrincipalType,
    string PrincipalId,
    IReadOnlyList<string> Actions,
    string Effect,
    bool InheritFromParent,
    string Status,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    bool IsExpired,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record DocumentAccessPolicyDetailModel(
    Guid Id,
    Guid AccessPolicyId,
    string TargetType,
    string TargetId,
    string? TargetLabel,
    Guid? TargetCompanyId,
    string PrincipalType,
    string PrincipalId,
    IReadOnlyList<string> Actions,
    string Effect,
    bool InheritFromParent,
    Guid? SourcePolicyId,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    bool IsExpired,
    string Status,
    string? Reason,
    string? CorrelationId,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);

public sealed record DocumentAccessPolicyTargetModel(
    string TargetType,
    string TargetId,
    string Label,
    string? Scope = null);

public sealed record DocumentAccessPrincipalModel(
    string PrincipalType,
    string PrincipalId,
    string Label);

/// <summary>One resolved action decision with the policy level (target type) that produced it, for preview explainability.</summary>
public sealed record EffectiveActionModel(
    string Action,
    bool Allowed,
    string Effect,
    string SourceTargetType,
    bool Inherited);

public sealed record EffectiveDocumentAccessModel(
    string TargetType,
    string TargetId,
    string PrincipalType,
    string PrincipalId,
    IReadOnlyList<string> AllowedActions,
    IReadOnlyList<EffectiveActionModel> Decisions,
    string Mode);

public sealed record EffectiveDocumentAccessRequestItem(
    string TargetType,
    string TargetId);

public sealed record EffectiveDocumentAccessBatchInput(
    string PrincipalType,
    string PrincipalId,
    IReadOnlyList<EffectiveDocumentAccessRequestItem> Targets);

public static class AccessMatrixWire
{
    public static string ToWire(this DocumentAccessTargetType v) => v.ToString();
    public static string ToWire(this DocumentAccessPrincipalType v) => v.ToString();
    public static string ToWire(this DocumentAccessMatrixAction v) => v.ToString();
    public static string ToWire(this DocumentAccessEffect v) => v.ToString().ToUpperInvariant();
    public static string ToWire(this DocumentAccessPolicyStatus v) => v.ToString().ToUpperInvariant();

    public static DocumentAccessTargetType? ParseTargetType(string? v) =>
        Enum.TryParse<DocumentAccessTargetType>((v ?? string.Empty).Trim(), true, out var r) ? r : null;

    public static DocumentAccessPrincipalType? ParsePrincipalType(string? v) =>
        Enum.TryParse<DocumentAccessPrincipalType>((v ?? string.Empty).Trim(), true, out var r) ? r : null;

    public static DocumentAccessMatrixAction? ParseAction(string? v) =>
        Enum.TryParse<DocumentAccessMatrixAction>((v ?? string.Empty).Trim(), true, out var r) ? r : null;

    public static DocumentAccessEffect? ParseEffect(string? v) => (v?.Trim().ToUpperInvariant()) switch
    {
        "ALLOW" => DocumentAccessEffect.Allow,
        "DENY" => DocumentAccessEffect.Deny,
        _ => null
    };

    public static DocumentAccessPolicyStatus? ParseStatus(string? v) => (v?.Trim().ToUpperInvariant()) switch
    {
        "ACTIVE" => DocumentAccessPolicyStatus.Active,
        "DISABLED" => DocumentAccessPolicyStatus.Disabled,
        "ARCHIVED" => DocumentAccessPolicyStatus.Archived,
        _ => null
    };

    public static bool IsExpired(DocumentAccessPolicyEntry e, DateTimeOffset now) =>
        e.ValidTo is { } to && to < now;

    public static DocumentAccessPolicyListItemModel ToListItem(DocumentAccessPolicyEntry e, string? targetLabel, DateTimeOffset now) => new(
        e.Id,
        e.TargetType.ToWire(),
        e.TargetId,
        targetLabel,
        e.PrincipalType.ToWire(),
        e.PrincipalId,
        e.Actions.Select(a => a.ToWire()).ToList(),
        e.Effect.ToWire(),
        e.InheritFromParent,
        e.Status.ToWire(),
        e.ValidFrom,
        e.ValidTo,
        IsExpired(e, now),
        e.CreatedAt,
        e.UpdatedAt);

    public static DocumentAccessPolicyDetailModel ToDetail(DocumentAccessPolicyEntry e, string? targetLabel, DateTimeOffset now, Guid? targetCompanyId = null) => new(
        e.Id,
        e.AccessPolicyId == Guid.Empty ? e.Id : e.AccessPolicyId,
        e.TargetType.ToWire(),
        e.TargetId,
        targetLabel,
        targetCompanyId,
        e.PrincipalType.ToWire(),
        e.PrincipalId,
        e.Actions.Select(a => a.ToWire()).ToList(),
        e.Effect.ToWire(),
        e.InheritFromParent,
        e.SourcePolicyId,
        e.ValidFrom,
        e.ValidTo,
        IsExpired(e, now),
        e.Status.ToWire(),
        e.Reason,
        e.CorrelationId,
        e.CreatedAt,
        e.CreatedBy,
        e.UpdatedAt,
        e.UpdatedBy);
}
