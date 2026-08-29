using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Commands;

/// <summary>
/// Updates a segment. <c>SubjectType</c> is absent on purpose: it is immutable, so a segment can never silently start
/// answering a different question. <c>SegmentCode</c> is absent too — the stable business key is not renamed; the name
/// is what changes.
/// <para><c>CriteriaProvided</c> distinguishes "the caller sent a criteria tree" from "the caller sent none": an
/// omitted tree leaves the existing one untouched, which is what makes editing the metadata of an ACTIVE (frozen)
/// segment possible without tripping the freeze guard.</para>
/// </summary>
public sealed record UpdateSegmentCommand(
    Guid SegmentId,
    string SegmentName,
    string SegmentType,
    string SegmentStatus,
    string MatchMode,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? BusinessUnitId,
    string? Description,
    string? Notes,
    bool CriteriaProvided,
    IReadOnlyList<SegmentCriteriaNodeInput>? Criteria,
    int? ExpectedVersion) : IRequest<Response<bool>>;
