using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.WorkAggregation.Queries;

// WC-1 (DCP-004) — the current user's personal work-item projection (read-only).
//
// The caller's UserId is NOT carried here: it is resolved server-side from the tenant/user context in the
// handler. Only the claim-derived permission context travels with the query — IsPlatformActor plus the set of
// granted permission keys the API layer evaluated from the principal's claims — so action eligibility can be
// resolved without the browser being an authority. Permission: platform.work-aggregation.inbox.view.
public sealed record GetMyWorkItemsQuery(
    bool IsPlatformActor,
    IReadOnlySet<string> GrantedPermissions,
    string CorrelationId)
    : IRequest<Response<IReadOnlyList<WorkItemProjectionDto>>>;
