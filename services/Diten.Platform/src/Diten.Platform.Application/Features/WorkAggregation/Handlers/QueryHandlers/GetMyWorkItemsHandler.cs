using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.WorkAggregation.Providers;
using Diten.Platform.Application.Features.WorkAggregation.Queries;
using MediatR;

namespace Diten.Platform.Application.Features.WorkAggregation.Handlers.QueryHandlers;

// WC-1 (DCP-004) — aggregates every bound provider's work items for the current user into
// one canonical list. In WC-1 exactly one provider is bound (MOD-0023 approvals); the handler iterates an
// IEnumerable<IWorkItemProvider> so WC-5 adds providers additively without touching this handler.
//
// READ-ONLY: no state is written. UserId is resolved server-side (never from the client payload). A provider
// declaring a contract version this handler cannot map is skipped (charter OD-WC-04), not mis-projected.
public sealed class GetMyWorkItemsHandler
    : IRequestHandler<GetMyWorkItemsQuery, Response<IReadOnlyList<WorkItemProjectionDto>>>
{
    // Provider contract versions this projection generation can map. An unknown version is skipped.
    private static readonly HashSet<string> SupportedProviderContractVersions = ["1.0"];

    private readonly IEnumerable<IWorkItemProvider> _providers;
    private readonly ICurrentUserContext _currentUser;

    public GetMyWorkItemsHandler(IEnumerable<IWorkItemProvider> providers, ICurrentUserContext currentUser)
    {
        _providers = providers;
        _currentUser = currentUser;
    }

    public async Task<Response<IReadOnlyList<WorkItemProjectionDto>>> Handle(
        GetMyWorkItemsQuery request,
        CancellationToken ct)
    {
        var actor = new WorkItemActor(
            UserId: _currentUser.UserId,
            IsPlatformActor: request.IsPlatformActor,
            GrantedPermissions: request.GrantedPermissions);

        var aggregated = new List<WorkItemProjectionDto>();

        foreach (var provider in _providers)
        {
            // Charter OD-WC-04 — contract-version handshake. Skip an unmappable provider rather than emit a
            // silently mis-projected item.
            if (!SupportedProviderContractVersions.Contains(provider.ProviderContractVersion))
            {
                continue;
            }

            var items = await provider.GetWorkItemsAsync(actor, ct);
            aggregated.AddRange(items);
        }

        IReadOnlyList<WorkItemProjectionDto> result = aggregated
            .OrderBy(i => i.DueAt ?? DateTimeOffset.MaxValue)
            .ToList();

        return Response<IReadOnlyList<WorkItemProjectionDto>>.Success(result, correlationId: request.CorrelationId);
    }
}
