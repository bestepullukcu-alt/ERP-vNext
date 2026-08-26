using Diten.PvgService.Domain.MeddraCoding;

namespace Diten.PvgService.Application.MeddraCoding;

public sealed class InMemoryMeddraCodingApplicationService
{
    private readonly Dictionary<string, StoredCodingWorkItem> _items = new(StringComparer.Ordinal);
    private int _nextWorkItemNumber = 1;

    public int StoredItemCount => _items.Count;

    public MeddraCodingOperationResult CreateCodingWorkItem(CreateMeddraCodingWorkItemCommand command)
    {
        var guardResult = MeddraCodingContractGuard.Evaluate(command);
        if (!guardResult.IsAllowed)
        {
            return MeddraCodingOperationResult.FromSafeResult(guardResult);
        }

        var reference = CreateWorkItemReference();
        var draft = new MeddraCodingAssignmentDraft(
            reference,
            command.SourceTermReference,
            ProposedTerm: null,
            MeddraCodingReviewStatus.Draft);

        _items[reference] = new StoredCodingWorkItem(
            command.ServerTenantContext.TenantContextReference,
            draft);

        return MeddraCodingOperationResult.Allowed(
            MeddraCodingOperation.CreateWorkItem,
            new[] { ToMetadataRecord(draft) });
    }

    public MeddraCodingOperationResult ProposeCodedTerm(ProposeMeddraCodedTermCommand command)
    {
        var guardResult = MeddraCodingContractGuard.Evaluate(command);
        if (!guardResult.IsAllowed)
        {
            return MeddraCodingOperationResult.FromSafeResult(guardResult);
        }

        if (!TryGetSameTenantItem(command.CodingWorkItemReference, command.ServerTenantContext, out var storedItem))
        {
            return MeddraCodingOperationResult.Blocked(MeddraCodingOperation.ProposeCodedTerm, MeddraCodingReasonCode.NotFound);
        }

        var updatedDraft = storedItem.Draft with
        {
            ProposedTerm = command.ProposedTerm,
            ReviewStatus = MeddraCodingReviewStatus.Proposed
        };

        _items[command.CodingWorkItemReference] = storedItem with { Draft = updatedDraft };

        return MeddraCodingOperationResult.Allowed(
            MeddraCodingOperation.ProposeCodedTerm,
            new[] { ToMetadataRecord(updatedDraft) });
    }

    public MeddraCodingOperationResult MarkCodingReviewed(MarkMeddraCodingReviewedCommand command)
    {
        var guardResult = MeddraCodingContractGuard.Evaluate(command);
        if (!guardResult.IsAllowed)
        {
            return MeddraCodingOperationResult.FromSafeResult(guardResult);
        }

        if (!TryGetSameTenantItem(command.CodingWorkItemReference, command.ServerTenantContext, out var storedItem))
        {
            return MeddraCodingOperationResult.Blocked(MeddraCodingOperation.MarkReviewed, MeddraCodingReasonCode.NotFound);
        }

        if (storedItem.Draft.ProposedTerm is null)
        {
            return MeddraCodingOperationResult.Blocked(MeddraCodingOperation.MarkReviewed, MeddraCodingReasonCode.InvalidRequest);
        }

        var updatedDraft = storedItem.Draft with
        {
            ReviewStatus = MeddraCodingReviewStatus.Reviewed
        };

        _items[command.CodingWorkItemReference] = storedItem with { Draft = updatedDraft };

        return MeddraCodingOperationResult.Allowed(
            MeddraCodingOperation.MarkReviewed,
            new[] { ToMetadataRecord(updatedDraft) });
    }

    public MeddraCodingOperationResult GetByIdMetadata(GetMeddraCodingMetadataByIdQuery query)
    {
        var guardResult = MeddraCodingContractGuard.Evaluate(query);
        if (!guardResult.IsAllowed)
        {
            return MeddraCodingOperationResult.FromSafeResult(guardResult);
        }

        if (!TryGetSameTenantItem(query.CodingWorkItemReference, query.ServerTenantContext, out var storedItem))
        {
            return MeddraCodingOperationResult.Blocked(MeddraCodingOperation.GetById, MeddraCodingReasonCode.NotFound);
        }

        return MeddraCodingOperationResult.Allowed(
            MeddraCodingOperation.GetById,
            new[] { ToMetadataRecord(storedItem.Draft) });
    }

    public MeddraCodingOperationResult ListMetadata(GetMeddraCodingMetadataListQuery query)
    {
        var guardResult = MeddraCodingContractGuard.Evaluate(query);
        if (!guardResult.IsAllowed)
        {
            return MeddraCodingOperationResult.FromSafeResult(guardResult);
        }

        var records = _items
            .Values
            .Where(item => string.Equals(
                item.TenantContextReference,
                query.ServerTenantContext.TenantContextReference,
                StringComparison.Ordinal))
            .Select(item => ToMetadataRecord(item.Draft))
            .ToArray();

        return MeddraCodingOperationResult.Allowed(MeddraCodingOperation.List, records);
    }

    private bool TryGetSameTenantItem(
        string codingWorkItemReference,
        PvgServerTenantContext serverTenantContext,
        out StoredCodingWorkItem storedItem)
    {
        if (_items.TryGetValue(codingWorkItemReference, out storedItem!) &&
            string.Equals(storedItem.TenantContextReference, serverTenantContext.TenantContextReference, StringComparison.Ordinal))
        {
            return true;
        }

        storedItem = default!;
        return false;
    }

    private string CreateWorkItemReference() => $"coding-work-item-{_nextWorkItemNumber++}";

    private static MeddraCodingMetadataRecord ToMetadataRecord(MeddraCodingAssignmentDraft draft) =>
        new(
            draft.CodingWorkItemReference,
            draft.ReviewStatus,
            draft.ProposedTerm is not null);

    private sealed record StoredCodingWorkItem(
        string TenantContextReference,
        MeddraCodingAssignmentDraft Draft);
}
