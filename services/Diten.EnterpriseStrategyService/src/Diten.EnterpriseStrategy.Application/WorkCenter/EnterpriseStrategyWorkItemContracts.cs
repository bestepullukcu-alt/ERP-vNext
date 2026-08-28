using System.Text.Json;
using System.Text.Json.Serialization;

namespace Diten.Application.WorkCenter;

public static class EnterpriseStrategyWorkItemContract
{
    public const string ProviderCode = "enterprise-strategy";
    public const string ContractVersion = "1.0";
    public const string ProjectionRoute = "api/v1/work-items/projection";
    public const string TenantSafetyNotReady = "TENANT_SAFETY_NOT_READY";
}

public sealed record EnterpriseStrategyWorkItemEnvelope(
    [property: JsonPropertyName("data")] EnterpriseStrategyWorkItemData? Data,
    [property: JsonPropertyName("statusCode")] int StatusCode,
    [property: JsonPropertyName("isSuccessful")] bool IsSuccessful,
    [property: JsonPropertyName("errors")] IReadOnlyList<string> Errors,
    [property: JsonPropertyName("reason_code")] string? ReasonCode)
{
    public static EnterpriseStrategyWorkItemEnvelope TenantSafetyUnavailable()
        => new(null, 503, false, [EnterpriseStrategyWorkItemContract.TenantSafetyNotReady],
            EnterpriseStrategyWorkItemContract.TenantSafetyNotReady);
}

public sealed record EnterpriseStrategyWorkItemData(
    [property: JsonPropertyName("contractVersion")] string ContractVersion,
    [property: JsonPropertyName("items")] IReadOnlyList<EnterpriseStrategyWorkItemProjection> Items);

// Wire-shape preparation only. No instance is emitted before a tenant-safe source is separately authorized.
public sealed record EnterpriseStrategyWorkItemProjection(
    string FixtureKind,
    string Id,
    string WorkIntent,
    string AssignmentMode,
    string OwnershipState,
    string AdmissionState,
    string NormalizedStatus,
    string TaskLifecycle,
    string ExecutionState,
    string TimerState,
    string SystemState,
    string ActionDepth,
    JsonElement Title,
    JsonElement NativeStatus,
    JsonElement Source,
    string LifecycleOwner,
    IReadOnlyList<string> WorkItemCapabilities,
    IReadOnlyList<JsonElement> Actions,
    JsonElement Concurrency,
    JsonElement? WaitingContext = null,
    JsonElement? Escalation = null,
    DateTimeOffset? DueAt = null,
    string? PrimaryActionCode = null,
    IReadOnlyList<string>? OverflowActionCodes = null,
    JsonElement? Assignee = null,
    JsonElement? Requester = null,
    JsonElement? Checklist = null,
    JsonElement? Subtasks = null,
    string? ParentTaskItemId = null,
    JsonElement? Gates = null,
    string? Priority = null,
    IReadOnlyList<JsonElement>? Dependencies = null,
    JsonElement? BlockedState = null,
    IReadOnlyList<JsonElement>? Activity = null,
    DateTimeOffset? PlannedDate = null,
    JsonElement? TaskType = null,
    JsonElement? Pool = null,
    string? SlaState = null,
    JsonElement? BusinessContext = null,
    DateTimeOffset? ClosedAt = null,
    JsonElement? Summary = null,
    DateTimeOffset? StartAt = null,
    decimal? EstimateHours = null,
    decimal? SpentHours = null,
    IReadOnlyList<string>? Tags = null,
    JsonElement? Personal = null,
    IReadOnlyList<JsonElement>? Watchers = null,
    bool? DelegationAllowed = null,
    JsonElement? Notifications = null,
    int? ReminderLeadDays = null);
