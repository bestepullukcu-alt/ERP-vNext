using Diten.PpmService.Application.Common;
using MediatR;

namespace Diten.PpmService.Application.Features.InvestmentCases;

public sealed record ListInvestmentCasesQuery : IRequest<Response<IReadOnlyList<InvestmentCaseDto>>>;
