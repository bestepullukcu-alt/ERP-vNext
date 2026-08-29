using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Commands;

/// <summary>Closes one hand-written membership row. Soft, like every other close in this FU: the row stays readable so
/// a past selection keeps its explanation.</summary>
public sealed record ArchiveTargetCustomerCommand(
    Guid SegmentId,
    Guid TargetCustomerId,
    int? ExpectedVersion) : IRequest<Response<bool>>;
