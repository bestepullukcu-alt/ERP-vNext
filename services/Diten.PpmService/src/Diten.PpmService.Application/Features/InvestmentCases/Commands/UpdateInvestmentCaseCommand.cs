using Diten.PpmService.Application.Common;
using MediatR;

namespace Diten.PpmService.Application.Features.InvestmentCases;

public sealed record UpdateInvestmentCaseCommand(Guid Id, string Code, string Title, string? Description, DateOnly? PlannedStartDate, DateOnly? PlannedEndDate, int ExpectedVersion) : IRequest<Response<InvestmentCaseDto>>;
