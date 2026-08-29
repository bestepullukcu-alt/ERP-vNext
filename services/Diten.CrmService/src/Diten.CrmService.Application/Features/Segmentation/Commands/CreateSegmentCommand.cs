using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Commands;

/// <summary>Creates a segment. It is always born <c>draft</c> at business version 1 with its own lineage id, and it is
/// never born active: putting a rule live is a separate endpoint and a separate permission (SoD). There is no TenantId
/// here — it is resolved server-side from the claim.</summary>
public sealed record CreateSegmentCommand(
    string SegmentCode,
    string SegmentName,
    string SegmentType,
    string SubjectType,
    string MatchMode,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? BusinessUnitId,
    string? Description,
    string? Notes,
    IReadOnlyList<SegmentCriteriaNodeInput>? Criteria) : IRequest<Response<Guid>>;
