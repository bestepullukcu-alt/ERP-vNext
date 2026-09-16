using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.SensitiveAccess;

public enum SensitiveAccessRequestedAction
{
    Read = 0,
    Manage = 1,
    Review = 2
}

public enum SensitiveAccessDecision
{
    Denied = 0,
    Allowed = 1,
    Deferred = 2
}

public sealed class SensitiveAccessDecisionRequest
{
    public SensitiveAccessRequestedAction RequestedAction { get; init; } = SensitiveAccessRequestedAction.Read;
    public string SourcePolicyVersion { get; init; } = "v1";
}

public sealed record SensitiveAccessDecisionDto(
    Guid EmployeeProjectionId,
    EmployeeVisibilityClassification VisibilityClassification,
    SensitiveAccessRequestedAction RequestedAction,
    SensitiveAccessDecision Decision,
    bool IsAllowed,
    string ReasonCode,
    bool DataScopeEvaluated,
    string DataScopeReasonCode,
    string AuditMode,
    string RuntimeOwnerKey,
    string SourcePolicyVersion);

public sealed record SensitiveAccessHealthDto(string OwnerKey, string Status);

public sealed record SensitiveAccessAuditStatusDto(string AuditMode, string Status, string FollowUp);

public sealed record SensitiveAccessPolicyValidationRequest(string SourcePolicyVersion);

public sealed record SensitiveAccessPolicyValidationDto(string SourcePolicyVersion, bool IsCurrent, string Status);
