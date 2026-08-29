using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.CycleCapacity.Queries;

/// <summary>One capacity by its own id.</summary>
public sealed record GetCycleCapacityByIdQuery(Guid CycleCapacityId) : IRequest<Response<CycleCapacityDetailDto>>;
