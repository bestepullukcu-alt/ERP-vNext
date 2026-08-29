using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.CyclePeriod.Contract;
using MediatR;

namespace Diten.CrmService.Application.Features.CyclePeriod.Queries;

/// <summary>What this FU is, and — just as importantly — what it deliberately is not.</summary>
public sealed record GetCyclePeriodContractQuery : IRequest<Response<CyclePeriodContractDto>>;
