using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.WorkAggregation;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Queries;

/// <summary>
/// BL-414 — one task, as a Task Center work item, by id.
///
/// <para>Carries the same claim-derived permission context <c>GetMyWorkItemsQuery</c> carries, and for the same
/// reason: the projected actions are decided server-side for THIS caller, and the browser is authority for
/// nothing. The caller's user id is not carried; the handler resolves it server-side.</para>
/// </summary>
public sealed record GetTaskWorkItemByIdQuery(
    Guid Id,
    bool IsPlatformActor,
    IReadOnlySet<string> GrantedPermissions,
    string CorrelationId)
    : IRequest<Response<WorkItemProjectionDto>>;
