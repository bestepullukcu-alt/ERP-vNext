using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.CycleCapacity.Contract;
using MediatR;

namespace Diten.CrmService.Application.Features.CycleCapacity.Queries;

/// <summary>What this FU is, and — just as importantly — what it is not.</summary>
public sealed record GetCycleCapacityContractQuery : IRequest<Response<CycleCapacityContractDto>>;
