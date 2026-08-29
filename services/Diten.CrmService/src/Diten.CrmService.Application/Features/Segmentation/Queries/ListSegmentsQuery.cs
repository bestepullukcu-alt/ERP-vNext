using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Queries;

/// <summary>Segment grid. Only the filters the contract publishes are supported, so a UI can never offer one that
/// silently does nothing. The criteria tree is projected out of the list rows.</summary>
public sealed record ListSegmentsQuery(
    string? SegmentType,
    string? SegmentStatus,
    string? SubjectType,
    string? BusinessUnitId,
    string? SegmentCode,
    string? Search,
    bool IncludeArchived) : IRequest<Response<SegmentListDto>>;
