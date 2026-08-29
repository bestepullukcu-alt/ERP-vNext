using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Segmentation.Commands;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Handlers.CommandHandlers;

/// <summary>Updates one hand-written membership row, including the include-to-exclude switch, which is exactly why it
/// is an update: the pair (segment, subject) keeps at most one live answer. Segment, subject type and subject id are
/// immutable, and an archived row accepts nothing.</summary>
public sealed class UpdateTargetCustomerHandler : IRequestHandler<UpdateTargetCustomerCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ISegmentRepository _segments;
    private readonly ITargetCustomerRepository _targets;

    public UpdateTargetCustomerHandler(
        ITenantContext tenant, IActorContext actor, ISegmentRepository segments, ITargetCustomerRepository targets)
    {
        _tenant = tenant;
        _actor = actor;
        _segments = segments;
        _targets = targets;
    }

    public async Task<Response<bool>> Handle(
        UpdateTargetCustomerCommand request, CancellationToken cancellationToken)
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

        var target = await _targets.GetByIdAsync(tenantId, request.TargetCustomerId, cancellationToken);
        if (target is null || target.SegmentId != segment.Id)
        {
            return Response<bool>.Fail("Membership row not found.", 404);
        }

        if (target.IsArchived())
        {
            return Response<bool>.Fail("An archived membership row cannot be updated.", 409);
        }

        var failure = SegmentValidation.ValidateTargetCustomer(
            segment, target.SubjectType, target.SubjectId, request.MembershipMode, request.SelectionReason,
            request.ReasonCodes, request.EffectiveFrom, request.EffectiveTo);
        if (failure is not null)
        {
            return Response<bool>.Fail(SegmentWriteGuards.ToErrors(failure), failure.StatusCode);
        }

        var expectedVersion = request.ExpectedVersion ?? target.Version;

        target.MembershipMode = SegmentMembershipModes.Normalize(request.MembershipMode);
        target.SelectionReason = request.SelectionReason.Trim();
        target.ReasonCodes = request.ReasonCodes.Select(c => c.Trim().ToLowerInvariant()).Distinct().ToList();
        target.EffectiveFrom = request.EffectiveFrom;
        target.EffectiveTo = request.EffectiveTo;
        target.SubjectDisplayName = SegmentValidation.Trim(request.SubjectDisplayName);
        target.Notes = SegmentValidation.Trim(request.Notes);
        target.UpdatedAt = DateTimeOffset.UtcNow;
        target.UpdatedBy = _actor.ActorName;

        var replaced = await _targets.ReplaceAsync(target, expectedVersion, cancellationToken);
        return replaced
            ? Response<bool>.Success(true)
            : Response<bool>.Fail("The membership row changed since it was loaded. Reload and try again.", 409);
    }
}
