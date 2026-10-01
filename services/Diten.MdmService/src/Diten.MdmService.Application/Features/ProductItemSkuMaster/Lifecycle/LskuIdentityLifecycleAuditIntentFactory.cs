using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;

public static class LskuIdentityLifecycleAuditIntentFactory
{
    public static LocalAuditIntent CreateSubmit(
        Lsku product,
        int expectedVersion,
        ProductIdentityWorkflowBinding binding)
    {
        var operationKey = binding.StartIdempotencyKey;
        var evidence = EncodeFacts(
            product.TenantId.ToString("D"), product.Id.ToString("D"),
            binding.WorkflowInstanceId.ToString("D"), binding.WorkflowTemplateId.ToString("D"),
            binding.WorkflowTemplateVersionId.ToString("D"), binding.ApprovalTaskId.ToString("D"),
            binding.AssignmentSnapshotId.ToString("D"), binding.StartTransitionLogId.ToString("D"),
            binding.ObjectType, binding.ObjectId.ToString("D"), binding.ObjectRef,
            binding.SubmitterSubjectId.ToString("D"), binding.StartRequestFingerprint,
            binding.SubmittedAtUtc.ToString("O"), binding.DueAtUtc?.ToString("O"));
        return Create(
            product,
            ProductAuditOperation.LskuIdentitySubmitted,
            expectedVersion,
            binding.SubmitterSubjectId,
            operationKey,
            operationKey,
            binding.SubmittedAtUtc,
            evidence);
    }

    public static LocalAuditIntent CreateDecision(
        Lsku product,
        int expectedVersion,
        ProductIdentityWorkflowDecisionEvidence evidence)
    {
        var operation = evidence.Decision == ProductIdentityDecisionKind.Approved
            ? ProductAuditOperation.LskuIdentityApproved
            : ProductAuditOperation.LskuIdentityRejected;
        var operationKey = $"workflow:{evidence.WorkflowInstanceId:D}:{evidence.TransitionSequence}";
        var facts = EncodeFacts(
            product.TenantId.ToString("D"), product.Id.ToString("D"), evidence.Decision,
            evidence.WorkflowInstanceId.ToString("D"), evidence.ApprovalTaskId.ToString("D"),
            evidence.WorkflowTemplateId.ToString("D"), evidence.WorkflowTemplateVersionId.ToString("D"),
            evidence.ObjectType, evidence.ObjectId.ToString("D"), evidence.ObjectRef,
            evidence.DecisionActorSubjectId.ToString("D"), evidence.ReasonCode ?? string.Empty,
            evidence.DecisionAtUtc.ToString("O"),
            evidence.TransitionSequence.ToString(CultureInfo.InvariantCulture),
            evidence.TaskStatus, evidence.InstanceStatus);
        return Create(
            product,
            operation,
            expectedVersion,
            evidence.DecisionActorSubjectId,
            operationKey,
            evidence.WorkflowInstanceId.ToString("D"),
            evidence.DecisionAtUtc,
            facts);
    }

    public static LocalAuditIntent CreateRetire(
        Lsku product,
        int expectedVersion,
        Guid operationId,
        Guid actorId,
        string reasonCode,
        string? comment,
        DateTimeOffset timestampUtc)
    {
        var operationKey = operationId.ToString("D");
        var evidence = EncodeFacts(
            product.TenantId.ToString("D"), product.Id.ToString("D"), actorId.ToString("D"),
            reasonCode, comment);
        return Create(
            product,
            ProductAuditOperation.LskuIdentityRetired,
            expectedVersion,
            actorId,
            operationKey,
            operationKey,
            timestampUtc,
            evidence);
    }

    public static LocalAuditIntent CreateWithdrawal(
        Lsku product,
        int expectedVersion,
        ProductIdentityWorkflowCancellationEvidence cancellation)
    {
        var evidence = EncodeFacts(product.TenantId.ToString("D"), product.Id.ToString("D"),
            cancellation.WorkflowInstanceId.ToString("D"), cancellation.ApprovalTaskId.ToString("D"),
            cancellation.RequesterSubjectId.ToString("D"), cancellation.ReasonCode, cancellation.Comment,
            cancellation.CancelledAtUtc.ToString("O"), cancellation.TransitionSequence,
            cancellation.TransitionLogId.ToString("D"), cancellation.TaskStatus, cancellation.InstanceStatus);
        return Create(product, ProductAuditOperation.LskuIdentityApprovalWithdrawn, expectedVersion,
            cancellation.RequesterSubjectId, cancellation.IdempotencyKey,
            cancellation.WorkflowInstanceId.ToString("D"), cancellation.CancelledAtUtc, evidence);
    }

    public static LocalAuditIntent CreateRetirementOperation(
        Lsku product, int expectedVersion, Guid operationId, Guid actorId,
        ProductAuditOperation operation, string reason, DateTimeOffset timestampUtc)
    {
        if (operation is not (ProductAuditOperation.LskuRetirementRequested
            or ProductAuditOperation.LskuRetirementRejected
            or ProductAuditOperation.LskuIdentityRetired))
            throw new ArgumentException("LSKU retirement audit operation is invalid.");
        var key = operationId.ToString("D");
        return Create(product, operation, expectedVersion, actorId, key, key, timestampUtc,
            EncodeFacts(product.TenantId.ToString("D"), product.Id.ToString("D"), key,
                actorId.ToString("D"), reason));
    }

    private static LocalAuditIntent Create(
        Lsku product,
        ProductAuditOperation operation,
        int expectedVersion,
        Guid actorId,
        string operationKey,
        string causationId,
        DateTimeOffset timestampUtc,
        byte[] evidence)
    {
        if (product.Id == Guid.Empty || product.TenantId == Guid.Empty || expectedVersion < 0
            || actorId == Guid.Empty || string.IsNullOrWhiteSpace(operationKey)
            || timestampUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Product lifecycle audit facts are invalid.");
        }

        var postVersion = checked(expectedVersion + 1);
        var intentId = DeterministicGuid(
            $"{product.TenantId:D}|{product.Id:D}|{operation}|{operationKey}");
        return new LocalAuditIntent
        {
            IntentId = intentId,
            TenantId = product.TenantId,
            AggregateType = AuditAggregateType.Lsku,
            AggregateId = product.Id,
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
            SnapshotReference = $"Lsku/{product.Id:N}/{postVersion}",
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

            var value = fact switch
            {
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => fact.ToString()
            } ?? string.Empty;
            var bytes = Encoding.UTF8.GetBytes(value);
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
