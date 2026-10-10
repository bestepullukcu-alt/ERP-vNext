using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Application.Features.ItemMaster.Activation;

// MOD-0290-FU04 S2 (§2.6): the approver is the QA Manager POSITION (OD-7) — the engine resolves its holders, the first
// one decides (GP-1b). Its id is configuration (ItemActivationWorkflow:ApproverPositionId), never code.
public sealed record ItemActivationWorkflowStartConfiguration(
    Guid? TemplateId,
    string? TemplateCode,
    Guid ApproverPositionId,
    string ReasonCode,
    bool CommentRequired,
    bool EvidenceRequired,
    TimeSpan? DueAfter);

public sealed record ItemActivationWorkflowStartPlan(
    ItemWorkflowOperation Operation,
    ProductIdentityWorkflowStartRequest TransportRequest);

public sealed class ItemActivationWorkflowStartRequestFactory(
    ItemActivationWorkflowStartConfiguration configuration,
    TimeProvider timeProvider)
{
    // The engine's object type for an item approval (Platform's TrustedWorkflowStartAuthorization lists it with the
    // template code; the flow is ITEM_ACTIVATION).
    public const string ItemObjectType = "Item";
    public const string ConfigurationInvalid = "ITEM_ACTIVATION_WORKFLOW_CONFIGURATION_INVALID";
    public const string StartFactsInvalid = "ITEM_ACTIVATION_WORKFLOW_START_FACTS_INVALID";

    /// <summary>The engine candidate for a position (Platform's WorkflowCandidateResolver form).</summary>
    public static string PositionCandidate(Guid positionId) => "position:" + positionId.ToString("D");

    /// <summary>
    /// One activation operation per (tenant, item, expected version, kind): a repeat of the same request is the same
    /// operation; another person's submit of the same version meets it (OPERATION_CONFLICT), never a second approval.
    /// </summary>
    public static Guid OperationIdFor(Guid tenantId, Guid itemId, int expectedVersion, ItemActivationKind kind) =>
        DerivedId("item-activation-operation-v1", tenantId, itemId, expectedVersion, (int)kind);

    /// <summary>The withdrawal command of one requester for one pending version (a repeat is the same command).</summary>
    public static Guid WithdrawalCommandIdFor(Guid tenantId, Guid itemId, int expectedVersion, Guid requester) =>
        DerivedId("item-activation-withdrawal-v1", tenantId, itemId, expectedVersion, 0, requester.ToString("D"));

    public ItemActivationWorkflowStartPlan Create(Guid tenantId, Item item, ItemActivationKind kind, Guid operationId,
        int expectedVersion, Guid makerSubjectId)
    {
        ArgumentNullException.ThrowIfNull(item);
        var source = kind == ItemActivationKind.Reactivation ? ItemLifecycleStatus.Deactivated : ItemLifecycleStatus.Draft;
        if (tenantId == Guid.Empty || item.Id == Guid.Empty || operationId == Guid.Empty
            || makerSubjectId == Guid.Empty || expectedVersion < 0 || item.Version != expectedVersion
            || item.TenantId != tenantId || item.IsDeleted || item.LifecycleStatus != source
            || kind is not (ItemActivationKind.Activation or ItemActivationKind.Reactivation)
            || string.IsNullOrWhiteSpace(item.CanonicalCode))
            throw new InvalidOperationException(StartFactsInvalid);

        EnsureConfiguration();
        var now = timeProvider.GetUtcNow();
        DateTimeOffset? dueAt = configuration.DueAfter.HasValue ? now.Add(configuration.DueAfter.Value) : null;
        var startKey = $"item-activation:{tenantId:D}:{operationId:D}";
        var fingerprint = Fingerprint(tenantId, operationId, item, kind, expectedVersion, makerSubjectId);
        var request = new ProductIdentityWorkflowStartRequest(
            configuration.TemplateId, configuration.TemplateCode, ItemObjectType, item.Id.ToString("D"),
            item.CanonicalCode, [PositionCandidate(configuration.ApproverPositionId)], configuration.ReasonCode, startKey,
            configuration.CommentRequired, configuration.EvidenceRequired, dueAt);
        var operation = new ItemWorkflowOperation
        {
            Id = operationId,
            TenantId = tenantId,
            OperationId = operationId,
            ItemId = item.Id,
            ActivationKind = kind,
            ExpectedItemVersion = expectedVersion,
            MakerSubjectId = makerSubjectId,
            // 0 = "tracked, never sent" (positive proof).
            LastStartSentAtUtcTicksV1 = 0,
            WorkflowTemplateId = configuration.TemplateId,
            WorkflowTemplateCode = configuration.TemplateCode,
            ApproverPositionId = configuration.ApproverPositionId,
            ReasonCode = configuration.ReasonCode,
            CommentRequired = configuration.CommentRequired,
            EvidenceRequired = configuration.EvidenceRequired,
            DueAtUtcTicksV1 = dueAt?.UtcTicks,
            ObjectType = ItemObjectType,
            ObjectId = item.Id.ToString("D"),
            ObjectRef = item.CanonicalCode,
            StartIdempotencyKey = startKey,
            OperationFingerprint = fingerprint,
            Checkpoint = ItemWorkflowCheckpoint.Prepared,
            RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.None,
            TemporalStorageVersion = ItemWorkflowOperation.CurrentTemporalStorageVersion,
            CreatedAtUtcTicksV1 = now.UtcTicks,
            UpdatedAtUtcTicksV1 = now.UtcTicks
        };
        return new(operation, request);
    }

    public ProductIdentityWorkflowStartRequest Rehydrate(ItemWorkflowOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new(operation.WorkflowTemplateId, operation.WorkflowTemplateCode, operation.ObjectType, operation.ObjectId,
            operation.ObjectRef, [PositionCandidate(operation.ApproverPositionId)], operation.ReasonCode,
            operation.StartIdempotencyKey, operation.CommentRequired, operation.EvidenceRequired,
            operation.DueAtUtcTicksV1.HasValue ? new DateTimeOffset(operation.DueAtUtcTicksV1.Value, TimeSpan.Zero) : null);
    }

    public static bool IsValid(ItemActivationWorkflowStartConfiguration value) =>
        value.TemplateId.HasValue != !string.IsNullOrEmpty(value.TemplateCode)
        && value.TemplateId != Guid.Empty
        && (value.TemplateCode is null || value.TemplateCode.Length <= 128 && value.TemplateCode == value.TemplateCode.Trim()
            && !value.TemplateCode.Any(char.IsControl))
        && value.ApproverPositionId != Guid.Empty
        && !string.IsNullOrWhiteSpace(value.ReasonCode) && value.ReasonCode.Length <= 128
        && value.ReasonCode == value.ReasonCode.Trim() && !value.ReasonCode.Any(char.IsControl)
        && (value.DueAfter is not { } dueAfter || dueAfter >= TimeSpan.FromMinutes(1) && dueAfter <= TimeSpan.FromDays(30));

    private void EnsureConfiguration()
    {
        if (!IsValid(configuration)) throw new InvalidOperationException(ConfigurationInvalid);
    }

    private string Fingerprint(Guid tenantId, Guid operationId, Item item, ItemActivationKind kind, int expectedVersion,
        Guid makerSubjectId)
    {
        var facts = string.Join('\n',
            "contract=item-activation-workflow-start-v1",
            $"tenantId={tenantId:D}",
            $"operationId={operationId:D}",
            $"itemId={item.Id:D}",
            $"kind={((int)kind).ToString(CultureInfo.InvariantCulture)}",
            $"expectedVersion={expectedVersion.ToString(CultureInfo.InvariantCulture)}",
            $"makerSubjectId={makerSubjectId:D}",
            $"templateId={configuration.TemplateId?.ToString("D") ?? string.Empty}",
            $"templateCode={Encode(configuration.TemplateCode)}",
            $"approverPosition={configuration.ApproverPositionId:D}",
            $"reason={Encode(configuration.ReasonCode)}",
            $"commentRequired={configuration.CommentRequired}",
            $"evidenceRequired={configuration.EvidenceRequired}",
            $"objectType={ItemObjectType}",
            $"objectRef={Encode(item.CanonicalCode)}",
            $"dueAfterTicks={configuration.DueAfter?.Ticks.ToString(CultureInfo.InvariantCulture) ?? string.Empty}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(facts))).ToLowerInvariant();
    }

    private static string Encode(string? value) =>
        value is null ? string.Empty : Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private static Guid DerivedId(string contract, Guid tenantId, Guid itemId, int version, int kind, string extra = "")
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', contract, tenantId.ToString("D"),
            itemId.ToString("D"), version.ToString(CultureInfo.InvariantCulture), kind.ToString(CultureInfo.InvariantCulture),
            extra)));
        var bytes = hash[..16];
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50); // a name-based (v5-shaped) id
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }
}
