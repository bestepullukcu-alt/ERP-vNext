using System.Text.Json.Serialization;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems;

public static class ProductAbbreviationWorkItemContract
{
    public const string ProviderCode = "mdm-product-abbreviations";
    public const string ContractVersion = "1.0";
    public const string AllocationObjectType = "productAbbreviationAllocationRequest";
    public const string CorrectionObjectType = "productAbbreviationCorrectionRequest";
    public const string RetirementObjectType = "productAbbreviationRetirementRequest";
    public const string ObjectType = AllocationObjectType;
    public const int MaximumItems = 100;
    public const int OverflowSentinelLimit = MaximumItems + 1;

    public static readonly IReadOnlySet<string> ActionCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        "approve", "reject", "cancel"
    };
}

public sealed record ProductAbbreviationWorkItemEnvelope<T>(
    T? Data,
    int StatusCode,
    bool IsSuccessful,
    IReadOnlyList<string> Errors,
    [property: JsonPropertyName("reason_code")] string? ReasonCode)
{
    public static ProductAbbreviationWorkItemEnvelope<T> Success(T data, int statusCode = 200)
        => new(data, statusCode, true, [], null);

    public static ProductAbbreviationWorkItemEnvelope<T> Fail(int statusCode, string reasonCode)
        => new(default, statusCode, false, [reasonCode], reasonCode);
}

public sealed record ProductAbbreviationWorkItemProjectionResponse(
    string ContractVersion,
    IReadOnlyList<ProductAbbreviationWorkItemProjection> Items);

public sealed record ProductAbbreviationWorkItemActionResponse(
    string ItemId,
    string ProviderCode,
    string ActionCode);

public sealed record ProductAbbreviationWorkItemOperationResult<T>(
    bool IsSuccessful,
    int StatusCode,
    string? ReasonCode,
    T? Data)
{
    public static ProductAbbreviationWorkItemOperationResult<T> Success(T data, int statusCode = 200)
        => new(true, statusCode, null, data);

    public static ProductAbbreviationWorkItemOperationResult<T> Fail(int statusCode, string reasonCode)
        => new(false, statusCode, reasonCode, default);
}

public sealed record ProductAbbreviationWorkItemProjection(
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
    ProductAbbreviationWorkItemLabel Title,
    ProductAbbreviationWorkItemNativeStatus NativeStatus,
    ProductAbbreviationWorkItemSource Source,
    string LifecycleOwner,
    IReadOnlyList<string> WorkItemCapabilities,
    IReadOnlyList<ProductAbbreviationWorkItemAction> Actions,
    ProductAbbreviationWorkItemConcurrency Concurrency,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object? WaitingContext = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object? Escalation = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? DueAt = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PrimaryActionCode = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? OverflowActionCodes = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ProductAbbreviationWorkItemPerson? Assignee = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ProductAbbreviationWorkItemPerson? Requester = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object? Checklist = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object? Subtasks = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ParentTaskItemId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object? Gates = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Priority = null);

public sealed record ProductAbbreviationWorkItemLabel(
    string Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Key = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, string>? Args = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Text = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Locale = null)
{
    public static ProductAbbreviationWorkItemLabel Resource(string key) => new("resource", Key: key);
    public static ProductAbbreviationWorkItemLabel Display(string text) => new("display", Text: text, Locale: "und");
}

public sealed record ProductAbbreviationWorkItemNativeStatus(
    string Code,
    ProductAbbreviationWorkItemLabel Label);

public sealed record ProductAbbreviationWorkItemSource(
    string ProviderCode,
    string ProviderContractVersion,
    string ObjectType,
    string ObjectId,
    string DeepLink);

public sealed record ProductAbbreviationWorkItemAction(
    string Code,
    ProductAbbreviationWorkItemLabel Label,
    string SemanticType,
    bool Enabled,
    string Source,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DisabledReasonCode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ProductAbbreviationWorkItemLabel? DisabledReason,
    bool RequiresConfirmation,
    bool RequiresReason,
    bool RequiresEvidence,
    bool SupportsBulk,
    string RiskLevel);

public sealed record ProductAbbreviationWorkItemConcurrency(string Kind, string Token);

public sealed record ProductAbbreviationWorkItemPerson(
    string Id,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DisplayName = null,
    bool IsCurrentUser = false);
