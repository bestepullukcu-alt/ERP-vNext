using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Segmentation.Commands;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Handlers.CommandHandlers;

/// <summary>Closes a segment. Soft, always: the row stays readable so every past selection keeps its explanation, and
/// there is no DELETE route in this FU to bypass it. Re-opening an archived segment is refused (409) — a new version
/// is the honest way back.</summary>
public sealed class ArchiveSegmentHandler : IRequestHandler<ArchiveSegmentCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ISegmentRepository _segments;

    public ArchiveSegmentHandler(ITenantContext tenant, IActorContext actor, ISegmentRepository segments)
    {
        _tenant = tenant;
        _actor = actor;
        _segments = segments;
    }

    public async Task<Response<bool>> Handle(ArchiveSegmentCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var segment = await _segments.GetByIdAsync(tenantId, request.SegmentId, cancellationToken);
        if (segment is null)
        {
            return Response<bool>.Fail("Segment not found.", 404);
        }

        if (segment.IsArchived())
        {
            return Response<bool>.Fail("The segment is already archived.", 409);
        }

        var transition = SegmentValidation.ValidateStatusTransition(segment.SegmentStatus, SegmentStatuses.Archived);
        if (transition is not null)
        {
            return Response<bool>.Fail(transition.Message, transition.StatusCode);
        }

        var now = DateTimeOffset.UtcNow;
        var expectedVersion = request.ExpectedVersion ?? segment.Version;

        segment.SegmentStatus = SegmentStatuses.Archived;
        segment.ArchivedAt = now;
        segment.ArchivedBy = _actor.ActorName;
        segment.UpdatedAt = now;
        segment.UpdatedBy = _actor.ActorName;

        var replaced = await _segments.ReplaceAsync(segment, expectedVersion, cancellationToken);
        return replaced
            ? Response<bool>.Success(true)
            : Response<bool>.Fail("The segment changed since it was loaded. Reload and try again.", 409);
    }
}
