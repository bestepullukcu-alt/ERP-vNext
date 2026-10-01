using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;

public static class FirstGskuIdentityLifecycleAuditIntentFactory
{
    public static LocalAuditIntent CreateRevisionSubmit(
        ProductDefinitionRevision revision, int expectedVersion, FirstGskuIdentityWorkflowBinding binding) =>
        CreateSubmit(revision, AuditAggregateType.ProductDefinitionRevision,
            ProductAuditOperation.ProductDefinitionRevisionIdentitySubmitted,
            revision.Id, expectedVersion, binding);

    public static LocalAuditIntent CreateGskuSubmit(
        Gsku gsku, int expectedVersion, FirstGskuIdentityWorkflowBinding binding) =>
        CreateSubmit(gsku, AuditAggregateType.Gsku, ProductAuditOperation.GskuIdentitySubmitted,
            gsku.Id, expectedVersion, binding);

    public static LocalAuditIntent CreateRevisionDecision(
        ProductDefinitionRevision revision, int expectedVersion,
        FirstGskuIdentityWorkflowBinding binding, ProductIdentityWorkflowDecisionEvidence evidence) =>
        CreateDecision(revision, AuditAggregateType.ProductDefinitionRevision,
            evidence.Decision == ProductIdentityDecisionKind.Approved
                ? ProductAuditOperation.ProductDefinitionRevisionIdentityApproved
                : ProductAuditOperation.ProductDefinitionRevisionIdentityRejected,
            revision.Id, expectedVersion, binding, evidence);

    public static LocalAuditIntent CreateGskuDecision(
        Gsku gsku, int expectedVersion,
        FirstGskuIdentityWorkflowBinding binding, ProductIdentityWorkflowDecisionEvidence evidence) =>
        CreateDecision(gsku, AuditAggregateType.Gsku,
            evidence.Decision == ProductIdentityDecisionKind.Approved
                ? ProductAuditOperation.GskuIdentityApproved
                : ProductAuditOperation.GskuIdentityRejected,
            gsku.Id, expectedVersion, binding, evidence);

    private static LocalAuditIntent CreateSubmit(
        EntityBase aggregate,
        AuditAggregateType aggregateType,
        ProductAuditOperation operation,
        Guid aggregateId,
        int expectedVersion,
        FirstGskuIdentityWorkflowBinding binding)
    {
        var evidence = EncodeFacts(
            aggregate.TenantId.ToString("D"), aggregateId.ToString("D"),
            binding.ProductDefinitionRevisionId.ToString("D"), binding.GskuId.ToString("D"),
            binding.WorkflowInstanceId.ToString("D"), binding.WorkflowTemplateId.ToString("D"),
            binding.WorkflowTemplateVersionId.ToString("D"), binding.ApprovalTaskId.ToString("D"),
            binding.AssignmentSnapshotId.ToString("D"), binding.StartTransitionLogId.ToString("D"),
            binding.ObjectType, binding.ObjectRef, binding.SubmitterSubjectId.ToString("D"),
            binding.StartRequestFingerprint, binding.SubmittedAtUtc.ToString("O"),
            binding.DueAtUtc?.ToString("O"));
        return Create(aggregate, aggregateType, operation, aggregateId, expectedVersion,
            binding.SubmitterSubjectId, binding.StartIdempotencyKey, binding.StartIdempotencyKey,
            binding.SubmittedAtUtc, evidence);
    }

    private static LocalAuditIntent CreateDecision(
        EntityBase aggregate,
        AuditAggregateType aggregateType,
        ProductAuditOperation operation,
        Guid aggregateId,
        int expectedVersion,
        FirstGskuIdentityWorkflowBinding binding,
        ProductIdentityWorkflowDecisionEvidence evidence)
    {
        var operationKey = $"workflow:{evidence.WorkflowInstanceId:D}:{evidence.TransitionSequence}";
        var facts = EncodeFacts(
            aggregate.TenantId.ToString("D"), aggregateId.ToString("D"),
            binding.ProductDefinitionRevisionId.ToString("D"), binding.GskuId.ToString("D"),
            evidence.Decision, evidence.WorkflowInstanceId.ToString("D"),
            evidence.ApprovalTaskId.ToString("D"), evidence.WorkflowTemplateId.ToString("D"),
            evidence.WorkflowTemplateVersionId.ToString("D"), evidence.ObjectType,
            evidence.ObjectId.ToString("D"), evidence.ObjectRef,
            evidence.DecisionActorSubjectId.ToString("D"), evidence.ReasonCode ?? string.Empty,
            evidence.DecisionAtUtc.ToString("O"),
            evidence.TransitionSequence.ToString(CultureInfo.InvariantCulture),
            evidence.TaskStatus, evidence.InstanceStatus);
        return Create(aggregate, aggregateType, operation, aggregateId, expectedVersion,
            evidence.DecisionActorSubjectId, operationKey, evidence.WorkflowInstanceId.ToString("D"),
            evidence.DecisionAtUtc, facts);
    }

    private static LocalAuditIntent Create(
        EntityBase aggregate,
        AuditAggregateType aggregateType,
        ProductAuditOperation operation,
        Guid aggregateId,
        int expectedVersion,
        Guid actorId,
        string operationKey,
        string causationId,
        DateTimeOffset timestampUtc,
        byte[] evidence)
    {
        if (aggregateId == Guid.Empty || aggregate.TenantId == Guid.Empty || expectedVersion < 0
            || actorId == Guid.Empty || string.IsNullOrWhiteSpace(operationKey)
            || timestampUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("First GSKU identity lifecycle audit facts are invalid.");
        }
        var postVersion = checked(expectedVersion + 1);
        return new LocalAuditIntent
        {
            IntentId = DeterministicGuid($"{aggregate.TenantId:D}|{aggregateType}|{aggregateId:D}|{operation}|{operationKey}"),
            TenantId = aggregate.TenantId,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            PreVersion = expectedVersion,
            PostVersion = postVersion,
            Operation = operation,
            ActorId = actorId.ToString("D"),
            CorrelationId = operationKey,
            CausationId = causationId,
            CommandId = operationKey,
            Sequence = postVersion,
            TimestampUtc = timestampUtc,
            TimestampUtcTicksV1 = timestampUtc.UtcTicks,
            TemporalStorageVersion = AuditIntentTemporalStorage.CurrentVersion,
            EvidenceHash = Convert.ToHexString(SHA256.HashData(evidence)),
            SnapshotReference = $"{aggregateType}/{aggregateId:N}/{postVersion}",
            DeliveryState = AuditIntentDeliveryState.Pending,
            IdempotencyKey = operationKey
        };
    }

    private static byte[] EncodeFacts(params object?[] facts)
    {
        using var stream = new MemoryStream();
        Span<byte> lengthBytes = stackalloc byte[sizeof(int)];
        foreach (var fact in facts)
        {
            if (fact is null)
            {
                BinaryPrimitives.WriteInt32BigEndian(lengthBytes, -1);
                stream.Write(lengthBytes);
                continue;
            }
            var value = fact is IFormattable formattable
                ? formattable.ToString(null, CultureInfo.InvariantCulture)
                : fact.ToString();
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            BinaryPrimitives.WriteInt32BigEndian(lengthBytes, bytes.Length);
            stream.Write(lengthBytes);
            stream.Write(bytes);
        }
        return stream.ToArray();
    }

    private static Guid DeterministicGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        guidBytes[7] = (byte)((guidBytes[7] & 0x0F) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        return new Guid(guidBytes);
    }
}
