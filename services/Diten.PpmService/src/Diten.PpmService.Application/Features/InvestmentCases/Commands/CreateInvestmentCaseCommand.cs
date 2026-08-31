using Diten.PpmService.Application.Common;
using MediatR;

namespace Diten.PpmService.Application.Features.InvestmentCases;

public sealed record CreateInvestmentCaseCommand(string Code, string Title, string? Description, Guid PortfolioId, DateOnly? PlannedStartDate, DateOnly? PlannedEndDate) : IRequest<Response<InvestmentCaseDto>>;
