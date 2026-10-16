using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;

public sealed class GetReviewHistoryImportBatchHandler :
    IRequestHandler<GetReviewHistoryImportBatchQuery, Response<HistoryImportReviewView>>
{
    private readonly IHistoryImportBatchStore _store;
    private readonly IHistoryImportReviewAuditStore _audit;
    private readonly IHistoryImportScopeAuthority _authority;

    public GetReviewHistoryImportBatchHandler(IHistoryImportBatchStore store,
        IHistoryImportReviewAuditStore audit, IHistoryImportScopeAuthority authority)
    { _store = store; _audit = audit; _authority = authority; }

    public async Task<Response<HistoryImportReviewView>> Handle(
        GetReviewHistoryImportBatchQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.TenantId == Guid.Empty || query.ActorId == Guid.Empty ||
            query.BatchId == Guid.Empty || query.SelectedLegalEntityHint == Guid.Empty)
            return Response<HistoryImportReviewView>.Fail("Batch was not found.", 404);
        if (!query.HasReviewPermission)
            return Response<HistoryImportReviewView>.Fail("Review permission is required.", 403);

        var batch = await _store.GetForReviewAsync(query.TenantId, query.BatchId, cancellationToken);
        if (batch is null)
            return Response<HistoryImportReviewView>.Fail("Batch was not found.", 404);
        Guid? selectedLegalEntityId;
        bool? authorized;
        try
        {
            selectedLegalEntityId = await _authority.ResolveSelectedAsync(query.TenantId,
                query.ActorId, query.SelectedLegalEntityHint, cancellationToken);
            if (selectedLegalEntityId is not null &&
                selectedLegalEntityId != query.SelectedLegalEntityHint)
                return Response<HistoryImportReviewView>.Fail("LegalEntity assignment is inconsistent.", 503);
            if (selectedLegalEntityId != batch.LegalEntityId)
                return Response<HistoryImportReviewView>.Fail("Batch was not found.", 404);
            authorized = await _authority.IsAuthorizedAsync(query.TenantId, query.ActorId,
                batch.LegalEntityId, batch.ScopeFrom, batch.ScopeThrough,
                batch.WarehouseScope, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException ||
                                   !cancellationToken.IsCancellationRequested)
        { return Response<HistoryImportReviewView>.Fail("Review scope authority is unavailable.", 503); }
        if (authorized is null)
            return Response<HistoryImportReviewView>.Fail("Review scope authority is unavailable.", 503);
        if (authorized is false)
            return Response<HistoryImportReviewView>.Fail("Batch was not found.", 404);

        IReadOnlyList<HistoryImportReviewFailure> attempts;
        try { attempts = await _audit.ListAsync(query.TenantId, query.BatchId, cancellationToken); }
        catch (Exception ex) when (ex is not OperationCanceledException ||
                                   !cancellationToken.IsCancellationRequested)
        { return Response<HistoryImportReviewView>.Fail("Review audit is unavailable.", 503); }
        return Response<HistoryImportReviewView>.Success(new HistoryImportReviewView(
            HistoryImportResultMapper.Map(batch), attempts));
    }
}
