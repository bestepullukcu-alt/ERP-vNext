using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;

public sealed class GetOwnDemandHistoryImportBatchHandler :
    IRequestHandler<GetOwnDemandHistoryImportBatchQuery, Response<HistoryImportBatchResult>>
{
    private readonly IHistoryImportBatchStore _store;
    private readonly IHistoryImportScopeAuthority _authority;
    public GetOwnDemandHistoryImportBatchHandler(
        IHistoryImportBatchStore store, IHistoryImportScopeAuthority authority)
    { _store = store; _authority = authority; }

    public async Task<Response<HistoryImportBatchResult>> Handle(
        GetOwnDemandHistoryImportBatchQuery query, CancellationToken cancellationToken)
    {
        if (query.TenantId == Guid.Empty || query.ActorId == Guid.Empty ||
            query.BatchId == Guid.Empty || query.SelectedLegalEntityHint == Guid.Empty)
            return Response<HistoryImportBatchResult>.Fail("Batch was not found.", 404);
        var batch = await _store.GetOwnAsync(query.TenantId, query.ActorId,
            query.BatchId, cancellationToken);
        if (batch is null) return Response<HistoryImportBatchResult>.Fail("Batch was not found.", 404);
        Guid? selectedLegalEntityId;
        bool? authorized;
        try
        {
            selectedLegalEntityId = await _authority.ResolveSelectedAsync(query.TenantId,
                query.ActorId, query.SelectedLegalEntityHint, cancellationToken);
            if (selectedLegalEntityId is not null &&
                selectedLegalEntityId != query.SelectedLegalEntityHint)
                return Response<HistoryImportBatchResult>.Fail("LegalEntity assignment is inconsistent.", 503);
            if (selectedLegalEntityId != batch.LegalEntityId)
                return Response<HistoryImportBatchResult>.Fail("Batch was not found.", 404);
            authorized = await _authority.IsAuthorizedAsync(query.TenantId, query.ActorId,
                batch.LegalEntityId, batch.ScopeFrom, batch.ScopeThrough,
                batch.WarehouseScope, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException ||
                                   !cancellationToken.IsCancellationRequested)
        {
            return Response<HistoryImportBatchResult>.Fail("Import scope authority is unavailable.", 503);
        }
        if (authorized is null)
            return Response<HistoryImportBatchResult>.Fail("Import scope authority is unavailable.", 503);
        if (authorized is false)
            return Response<HistoryImportBatchResult>.Fail("Batch was not found.", 404);
        return Response<HistoryImportBatchResult>.Success(HistoryImportResultMapper.Map(batch));
    }
}
