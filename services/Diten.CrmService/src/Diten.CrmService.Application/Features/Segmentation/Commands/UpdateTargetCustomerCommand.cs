using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Commands;

/// <summary>Updates one hand-written membership row. Switching between include and exclude is an UPDATE of this row —
/// never a second row — so the pair (segment, subject) always has at most one live answer. <c>SegmentId</c>,
/// <c>SubjectType</c> and <c>SubjectId</c> are immutable and therefore absent.</summary>
public sealed record UpdateTargetCustomerCommand(
    Guid SegmentId,
    Guid TargetCustomerId,
    string MembershipMode,
    string SelectionReason,
    IReadOnlyList<string> ReasonCodes,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? SubjectDisplayName,
    string? Notes,
    int? ExpectedVersion) : IRequest<Response<bool>>;
