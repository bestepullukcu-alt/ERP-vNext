using Diten.PpmService.Application.Common;
using MediatR;

namespace Diten.PpmService.Application.Features.InvestmentCases;

public sealed record GetInvestmentCaseByIdQuery(Guid Id) : IRequest<Response<InvestmentCaseDto>>;
