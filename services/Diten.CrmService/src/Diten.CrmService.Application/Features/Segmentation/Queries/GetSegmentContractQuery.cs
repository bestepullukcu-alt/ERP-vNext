using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Segmentation.Contract;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Queries;

/// <summary>The capability + vocabulary contract, so a UI hardcodes nothing and no consumer assumes a capability this
/// FU does not have.</summary>
public sealed record GetSegmentContractQuery : IRequest<Response<SegmentContractDto>>;
