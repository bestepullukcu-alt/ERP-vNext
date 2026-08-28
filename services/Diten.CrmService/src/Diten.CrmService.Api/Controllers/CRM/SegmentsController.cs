using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.Segmentation;
using Diten.CrmService.Application.Features.Segmentation.Commands;
using Diten.CrmService.Application.Features.Segmentation.Queries;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.Segmentation.SegmentPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0167 FU02 — Segment authoring with an EMBEDDED criteria tree, plus the manual TargetCustomer sub-resource and
/// the two read-only membership endpoints.
/// <para>There is <b>no DELETE and no PATCH</b> anywhere in this controller: closing anything is archive, because a
/// deleted segment would take every past explanation of "why was this person selected?" with it.</para>
/// <para>Membership resolution is a POST that <b>writes nothing</b> — it is a POST only because it carries options in a
/// body. Its own permission (<c>crm.segment.resolve</c>) is separate from read on purpose: seeing the DEFINITION of a
/// segment must not be enough to see the IDENTITY of its members. Under the documented DEV-ONLY fallback that split, and
/// the activate separation of duty, both collapse — a deliberate gap closed by F-RBAC.</para>
/// </summary>
[Authorize]
public sealed class SegmentsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public SegmentsController(IMediator mediator) => _mediator = mediator;

    // ---------------- segments ----------------

    [HttpGet("api/crm/segments")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> List(
        [FromQuery] string? segmentType,
        [FromQuery] string? segmentStatus,
        [FromQuery] string? subjectType,
        [FromQuery] string? businessUnitId,
        [FromQuery] string? segmentCode,
        [FromQuery] string? search,
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListSegmentsQuery(
                segmentType, segmentStatus, subjectType, businessUnitId, segmentCode, search, includeArchived),
            cancellationToken));

    [HttpGet("api/crm/segments/{segmentId:guid}")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Get(Guid segmentId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetSegmentByIdQuery(segmentId), cancellationToken));

    [HttpPost("api/crm/segments")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSegmentRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateSegmentCommand(
                request.SegmentCode, request.SegmentName, request.SegmentType, request.SubjectType,
                request.MatchMode, request.EffectiveFrom, request.EffectiveTo, request.BusinessUnitId,
                request.Description, request.Notes, MapCriteria(request.Criteria)),
            cancellationToken));

    [HttpPut("api/crm/segments/{segmentId:guid}")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Update(
        Guid segmentId, [FromBody] UpdateSegmentRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateSegmentCommand(
                segmentId, request.SegmentName, request.SegmentType, request.SegmentStatus, request.MatchMode,
                request.EffectiveFrom, request.EffectiveTo, request.BusinessUnitId, request.Description,
                request.Notes,
                // An omitted Criteria array means "leave the rule alone"; an empty one means "the rule has no nodes".
                CriteriaProvided: request.Criteria is not null,
                MapCriteria(request.Criteria),
                request.ExpectedVersion),
            cancellationToken));

    [HttpPost("api/crm/segments/{segmentId:guid}/activate")]
    // Canonical crm.segment.activate (F-RBAC); under the documented DEV-ONLY fallback it collapses onto manage, so the
    // author-is-not-activator separation of duty cannot be enforced in dev.
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Activate(
        Guid segmentId, [FromQuery] int? expectedVersion, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ActivateSegmentCommand(segmentId, expectedVersion), cancellationToken));

    [HttpPost("api/crm/segments/{segmentId:guid}/archive")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Archive(
        Guid segmentId, [FromQuery] int? expectedVersion, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchiveSegmentCommand(segmentId, expectedVersion), cancellationToken));

    [HttpPost("api/crm/segments/{segmentId:guid}/new-version")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> NewVersion(Guid segmentId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateSegmentVersionCommand(segmentId), cancellationToken));

    // ---------------- membership (READ-ONLY; persists nothing) ----------------

    /// <summary>Resolves the members. A POST that writes NOTHING — it carries options in a body, and every collection
    /// is byte-identical afterwards.</summary>
    [HttpPost("api/crm/segments/{segmentId:guid}/resolve")]
    // Canonical crm.segment.resolve: member identity is PII, so reading a definition must not imply reading members.
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Resolve(
        Guid segmentId,
        [FromBody] ResolveSegmentMembershipRequest? request,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ResolveSegmentMembershipQuery(
                segmentId, request?.EffectiveAt, request?.Limit, request?.Offset,
                request?.IncludeExcluded ?? false),
            cancellationToken));

    [HttpPost("api/crm/segments/{segmentId:guid}/membership/evaluate")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Evaluate(
        Guid segmentId,
        [FromBody] EvaluateSegmentMembershipRequest request,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new EvaluateSegmentMembershipQuery(
                segmentId, request.SubjectType, request.SubjectId, request.EffectiveAt),
            cancellationToken));

    // ---------------- manual membership rows (sub-resource of a segment) ----------------

    [HttpGet("api/crm/segments/{segmentId:guid}/targets")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> ListTargets(
        Guid segmentId,
        [FromQuery] string? membershipMode,
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListTargetCustomersQuery(segmentId, membershipMode, includeArchived), cancellationToken));

    [HttpPost("api/crm/segments/{segmentId:guid}/targets")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> AddTarget(
        Guid segmentId, [FromBody] AddTargetCustomerRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new AddTargetCustomerCommand(
                segmentId, request.SubjectType, request.SubjectId, request.MembershipMode,
                request.SelectionReason, request.ReasonCodes, request.EffectiveFrom, request.EffectiveTo,
                request.SubjectDisplayName, request.Notes),
            cancellationToken));

    [HttpPut("api/crm/segments/{segmentId:guid}/targets/{targetId:guid}")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> UpdateTarget(
        Guid segmentId, Guid targetId, [FromBody] UpdateTargetCustomerRequest request,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateTargetCustomerCommand(
                segmentId, targetId, request.MembershipMode, request.SelectionReason, request.ReasonCodes,
                request.EffectiveFrom, request.EffectiveTo, request.SubjectDisplayName, request.Notes,
                request.ExpectedVersion),
            cancellationToken));

    [HttpPost("api/crm/segments/{segmentId:guid}/targets/{targetId:guid}/archive")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> ArchiveTarget(
        Guid segmentId, Guid targetId, [FromQuery] int? expectedVersion, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchiveTargetCustomerCommand(segmentId, targetId, expectedVersion), cancellationToken));

    // ---------------- the reverse question ----------------

    [HttpGet("api/crm/subjects/{subjectType}/{subjectId:guid}/segments")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> ListSubjectSegments(
        string subjectType,
        Guid subjectId,
        [FromQuery] DateTimeOffset? effectiveAt,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ListSubjectSegmentsQuery(subjectType, subjectId, effectiveAt), cancellationToken));

    private static List<SegmentCriteriaNodeInput>? MapCriteria(List<SegmentCriteriaNodeRequest>? criteria)
        => criteria?.Select(c => c.ToInput()).ToList();
}
