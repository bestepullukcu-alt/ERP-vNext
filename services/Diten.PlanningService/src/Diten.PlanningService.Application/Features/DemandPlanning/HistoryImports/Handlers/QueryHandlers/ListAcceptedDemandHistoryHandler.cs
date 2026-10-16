using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;

public sealed class ListAcceptedDemandHistoryHandler :
    IRequestHandler<ListAcceptedDemandHistoryQuery, Response<IReadOnlyList<AcceptedDemandHistoryView>>>
{
    private readonly IAcceptedDemandHistoryStore _store;
    private readonly IHistoryImportScopeAuthority _authority;

    public ListAcceptedDemandHistoryHandler(IAcceptedDemandHistoryStore store,
        IHistoryImportScopeAuthority authority)
    { _store = store; _authority = authority; }

    public async Task<Response<IReadOnlyList<AcceptedDemandHistoryView>>> Handle(
        ListAcceptedDemandHistoryQuery query, CancellationToken cancellationToken)
    {
        if (query.TenantId == Guid.Empty || query.ActorId == Guid.Empty || !query.HasReadPermission)
            return Response<IReadOnlyList<AcceptedDemandHistoryView>>.Fail(
                "History read permission is required.", 403);
        if (query.SelectedLegalEntityHint == Guid.Empty || query.Skip < 0 || query.Take is < 1 or > 100)
            return Response<IReadOnlyList<AcceptedDemandHistoryView>>.Fail(
                "History list request is invalid.", 400);
        try
        {
            var selected = await _authority.ResolveSelectedAsync(query.TenantId, query.ActorId,
                query.SelectedLegalEntityHint, cancellationToken);
            if (selected is null)
                return Response<IReadOnlyList<AcceptedDemandHistoryView>>.Fail(
                    "Accepted history was not found.", 404);
            if (selected != query.SelectedLegalEntityHint)
                return Response<IReadOnlyList<AcceptedDemandHistoryView>>.Fail(
                    "LegalEntity assignment is inconsistent.", 503);
            var rows = await _store.ListAsync(query.TenantId, selected.Value, query.Skip,
                query.Take, cancellationToken);
            foreach (var group in rows.GroupBy(x => x.Row.WarehouseId))
            {
                var from = group.Min(x => x.Row.OccurredOn);
                var through = group.Max(x => x.Row.OccurredOn);
                var allowed = await _authority.IsAuthorizedAsync(query.TenantId, query.ActorId,
                    selected.Value, from, through, [group.Key], cancellationToken);
                if (allowed is null)
                    return Response<IReadOnlyList<AcceptedDemandHistoryView>>.Fail(
                        "History scope is unavailable.", 503);
                if (!allowed.Value)
                    return Response<IReadOnlyList<AcceptedDemandHistoryView>>.Fail(
                        "Accepted history was not found.", 404);
            }
            return Response<IReadOnlyList<AcceptedDemandHistoryView>>.Success(rows);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        { return Response<IReadOnlyList<AcceptedDemandHistoryView>>.Fail(
            "Accepted history is unavailable.", 503); }
    }
}
