using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.CyclePeriod.Queries;

public sealed record GetCyclePeriodByIdQuery(Guid CyclePeriodId) : IRequest<Response<CyclePeriodDetailDto>>;
