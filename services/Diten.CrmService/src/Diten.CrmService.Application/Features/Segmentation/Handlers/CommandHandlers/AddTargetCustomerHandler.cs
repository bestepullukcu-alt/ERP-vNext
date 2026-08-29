using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Segmentation.Commands;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Handlers.CommandHandlers;

/// <summary>
/// Adds one hand-written membership row. The referenced subject master is NOT read and NOT mutated — the caller
/// supplies the id, exactly as CampaignTarget does; <c>SubjectDisplayName</c> is captured for display and audit only
/// and is explicitly not a source of truth.
/// <para>Uniqueness is enforced in the handler over (segment, subject type, subject id) among live rows, so switching
/// include to exclude has to be an UPDATE and can never become a contradictory second row.</para>
/// </summary>
public sealed class AddTargetCustomerHandler : IRequestHandler<AddTargetCustomerCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ISegmentRepository _segments;
    private readonly ITargetCustomerRepository _targets;

    public AddTargetCustomerHandler(
        ITenantContext tenant, IActorContext actor, ISegmentRepository segments, ITargetCustomerRepository targets)
    {
        _tenant = tenant;
        _actor = actor;
        _segments = segments;
        _targets = targets;
    }

    public async Task<Response<Guid>> Handle(AddTargetCustomerCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var segment = await _segments.GetByIdAsync(tenantId, request.SegmentId, cancellationToken);
        if (segment is null)
        {
            return Response<Guid>.Fail("Segment not found.", 404);
        }

        if (segment.IsArchived())
        {
            return Response<Guid>.Fail("An archived segment accepts no membership row.", 409);
        }

        var failure = SegmentValidation.ValidateTargetCustomer(
            segment, request.SubjectType, request.SubjectId, request.MembershipMode, request.SelectionReason,
            request.ReasonCodes, request.EffectiveFrom, request.EffectiveTo);
        if (failure is not null)
        {
            return Response<Guid>.Fail(SegmentWriteGuards.ToErrors(failure), failure.StatusCode);
        }

        var subjectType = SegmentSubjectTypes.Normalize(request.SubjectType);
        var existing = await _targets.ListBySegmentAsync(tenantId, segment.Id, cancellationToken);
        if (existing.Any(t => !t.IsArchived()
                              && t.SubjectId == request.SubjectId
                              && string.Equals(t.SubjectType, subjectType, StringComparison.Ordinal)))
        {
            return Response<Guid>.Fail(
                "This subject already has a live membership row in the segment; update it instead of adding a "
                + "second, contradictory one.", 409);
        }

        var entity = new TargetCustomer
        {
            TenantId = tenantId,
            SegmentId = segment.Id,
            SubjectType = subjectType,
            SubjectId = request.SubjectId,
            MembershipMode = SegmentMembershipModes.Normalize(request.MembershipMode),
            SubjectDisplayName = SegmentValidation.Trim(request.SubjectDisplayName),
            SelectionReason = request.SelectionReason.Trim(),
            ReasonCodes = request.ReasonCodes.Select(c => c.Trim().ToLowerInvariant()).Distinct().ToList(),
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Notes = SegmentValidation.Trim(request.Notes),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = _actor.ActorName
        };

        await _targets.InsertAsync(entity, cancellationToken);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
