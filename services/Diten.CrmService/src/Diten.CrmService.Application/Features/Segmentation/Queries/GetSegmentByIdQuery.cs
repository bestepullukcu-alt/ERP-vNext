using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Queries;

/// <summary>Segment detail including the embedded criteria tree. A segment belonging to another tenant is a 404, not an
/// empty body: existence itself must not leak.</summary>
public sealed record GetSegmentByIdQuery(Guid SegmentId) : IRequest<Response<SegmentDetailDto>>;
