using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;

public sealed class GetAcceptedDemandHistoryRowHandler :
    IRequestHandler<GetAcceptedDemandHistoryRowQuery, Response<AcceptedDemandHistoryView>>
{
    private readonly IAcceptedDemandHistoryStore _store;
    private readonly IHistoryImportScopeAuthority _authority;

    public GetAcceptedDemandHistoryRowHandler(IAcceptedDemandHistoryStore store,
        IHistoryImportScopeAuthority authority)
    { _store = store; _authority = authority; }

    public async Task<Response<AcceptedDemandHistoryView>> Handle(
        GetAcceptedDemandHistoryRowQuery query, CancellationToken cancellationToken)
    {
        if (query.TenantId == Guid.Empty || query.ActorId == Guid.Empty || !query.HasReadPermission)
            return Response<AcceptedDemandHistoryView>.Fail("History read permission is required.", 403);
        if (query.AcceptedRowId == Guid.Empty || query.SelectedLegalEntityHint == Guid.Empty)
            return Response<AcceptedDemandHistoryView>.Fail("Accepted row and LegalEntity selection are required.", 400);
        try
        {
            var selected = await _authority.ResolveSelectedAsync(query.TenantId, query.ActorId,
                query.SelectedLegalEntityHint, cancellationToken);
            if (selected is not null && selected != query.SelectedLegalEntityHint)
                return Response<AcceptedDemandHistoryView>.Fail("LegalEntity assignment is inconsistent.", 503);
            if (selected is null)
                return Response<AcceptedDemandHistoryView>.Fail("Accepted row was not found.", 404);
            var found = await _store.GetAsync(query.TenantId, selected.Value,
                query.AcceptedRowId, cancellationToken);
            if (found is null)
                return Response<AcceptedDemandHistoryView>.Fail("Accepted row was not found.", 404);
            var authorized = await _authority.IsAuthorizedAsync(query.TenantId, query.ActorId,
                selected.Value, found.Row.OccurredOn, found.Row.OccurredOn,
                [found.Row.WarehouseId], cancellationToken);
            if (authorized is null)
                return Response<AcceptedDemandHistoryView>.Fail("History scope is unavailable.", 503);
            return authorized.Value ? Response<AcceptedDemandHistoryView>.Success(found) :
                Response<AcceptedDemandHistoryView>.Fail("Accepted row was not found.", 404);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        { return Response<AcceptedDemandHistoryView>.Fail("Accepted history is unavailable.", 503); }
    }
}
