using Diten.PpmService.Domain.Entities;

namespace Diten.PpmService.Application.Common;

public sealed record InvestmentCaseDto(Guid Id, string Code, string Title, string? Description, Guid PortfolioId,
    DateOnly? PlannedStartDate, DateOnly? PlannedEndDate, InvestmentCaseLifecycleState LifecycleState, int Version);
