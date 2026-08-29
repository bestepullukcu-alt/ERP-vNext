using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Commands;

/// <summary>
/// Adds ONE hand-written membership row. <c>MembershipMode</c> has exactly two legal values (manual-include /
/// manual-exclude): derived membership is never written down, so "did a rule or a person put this here?" is answered by
/// the model rather than by reading rows.
/// <para><c>SelectionReason</c> is required — a manual membership without a reason is not authorable. A dynamic segment
/// refuses manual rows outright (400); a manual exception belongs to a hybrid segment.</para>
/// </summary>
public sealed record AddTargetCustomerCommand(
    Guid SegmentId,
    string SubjectType,
    Guid SubjectId,
    string MembershipMode,
    string SelectionReason,
    IReadOnlyList<string> ReasonCodes,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? SubjectDisplayName,
    string? Notes) : IRequest<Response<Guid>>;
