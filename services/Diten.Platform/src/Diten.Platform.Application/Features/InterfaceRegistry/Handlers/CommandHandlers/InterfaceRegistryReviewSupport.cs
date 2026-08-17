using Diten.BuildingBlocks.InterfaceRegistry.Abstractions;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.InterfaceRegistry.Auditing;
using Diten.Platform.Domain.Entities.InterfaceRegistry;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.InterfaceRegistry.Handlers.CommandHandlers;

internal static class InterfaceRegistryReviewSupport
{
    public static string ResolveActor(ICurrentUserContext currentUser) =>
        currentUser.IsAuthenticated && !string.IsNullOrWhiteSpace(currentUser.ActorName)
            ? currentUser.ActorName
            : "system";

    public static async Task ConfirmAsync(
        InterfaceDiscoveryDiffItem diffItem,
        IInterfaceRegistryRepository repository,
        IInterfaceRegistryAuditSink auditSink,
        string actor,
        DateTimeOffset now,
        CancellationToken ct)
    {
        diffItem.ReviewStatus = InterfaceRegistryStatuses.Confirmed;
        diffItem.Decision = InterfaceReviewDecision.Confirmed;
        diffItem.ReviewReason = null;
        diffItem.ReviewedAtUtc = now;
        diffItem.ReviewedBy = actor;
        await repository.UpdateDiffItemAsync(diffItem, ct);

        if (diffItem.ChangeType != InterfaceChangeType.Missing)
        {
            var definition = diffItem.IncomingDefinition;
            if (definition.LifecycleStatus != InterfaceLifecycleStatus.Deprecated)
            {
                definition.LifecycleStatus = InterfaceLifecycleStatus.Active;
            }

            var snapshotHash = diffItem.IncomingHash ?? InterfaceManifestHasher.HashSnapshot(definition);
            await repository.UpsertActiveSnapshotAsync(new InterfaceActiveSnapshot
            {
                InterfaceCode = diffItem.InterfaceCode,
                InterfaceVersion = diffItem.InterfaceVersion,
                SnapshotHash = snapshotHash,
                Definition = definition,
                ConfirmedAtUtc = now,
                ConfirmedBy = actor
            }, ct);

            await repository.UpsertDefinitionAsync(InterfaceRegistryMapper.ToDefinition(definition, now, actor), ct);
        }

        await auditSink.EmitAsync("interface_diff.confirmed", BuildAuditMetadata(diffItem, actor), ct);
    }

    public static async Task RejectAsync(
        InterfaceDiscoveryDiffItem diffItem,
        string reason,
        IInterfaceRegistryRepository repository,
        IInterfaceRegistryAuditSink auditSink,
        string actor,
        DateTimeOffset now,
        CancellationToken ct)
    {
        diffItem.ReviewStatus = InterfaceRegistryStatuses.Rejected;
        diffItem.Decision = InterfaceReviewDecision.Rejected;
        diffItem.ReviewReason = reason.Trim();
        diffItem.ReviewedAtUtc = now;
        diffItem.ReviewedBy = actor;
        await repository.UpdateDiffItemAsync(diffItem, ct);
        await auditSink.EmitAsync("interface_diff.rejected", BuildAuditMetadata(diffItem, actor), ct);
    }

    public static async Task UpdateBatchStatusAsync(
        InterfaceDiscoveryBatch batch,
        IReadOnlyList<InterfaceDiscoveryDiffItem> diffItems,
        IInterfaceRegistryRepository repository,
        CancellationToken ct)
    {
        batch.RejectedCount = diffItems.Count(x => x.ReviewStatus == InterfaceRegistryStatuses.Rejected);

        if (diffItems.All(x => x.ReviewStatus == InterfaceRegistryStatuses.Confirmed))
        {
            batch.Status = InterfaceRegistryStatuses.Confirmed;
        }
        else if (diffItems.All(x => x.ReviewStatus == InterfaceRegistryStatuses.Rejected))
        {
            batch.Status = InterfaceRegistryStatuses.Rejected;
        }
        else if (diffItems.Any(x => x.ReviewStatus is InterfaceRegistryStatuses.Confirmed or InterfaceRegistryStatuses.Rejected))
        {
            batch.Status = InterfaceRegistryStatuses.PartiallyConfirmed;
        }
        else
        {
            batch.Status = InterfaceRegistryStatuses.PendingReview;
        }

        await repository.UpdateBatchAsync(batch, ct);
    }

    public static InterfaceReviewBatchResultDto ToBatchResult(
        InterfaceDiscoveryBatch batch,
        IReadOnlyList<InterfaceDiscoveryDiffItem> diffItems) =>
        new(InterfaceRegistryMapper.ToDto(batch), diffItems.Select(InterfaceRegistryMapper.ToDto).ToList());

    private static IReadOnlyDictionary<string, string?> BuildAuditMetadata(InterfaceDiscoveryDiffItem item, string actor) =>
        new Dictionary<string, string?>
        {
            ["diffItemId"] = item.DiffItemId.ToString(),
            ["batchId"] = item.BatchId.ToString(),
            ["interfaceCode"] = item.InterfaceCode,
            ["interfaceVersion"] = item.InterfaceVersion,
            ["changeType"] = item.ChangeType.ToString(),
            ["actor"] = actor
        };
}
