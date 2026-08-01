using Diten.PpmService.Domain.Entities;

namespace Diten.PpmService.Application.Common;

public sealed record InvestmentCaseDto(Guid Id, string Code, string Title, string? Description, Guid PortfolioId,
    DateOnly? PlannedStartDate, DateOnly? PlannedEndDate, InvestmentCaseLifecycleState LifecycleState, int Version);

public sealed record BenefitCommitmentDto(Guid Id, string Code, string Title, string? Description, Guid InvestmentCaseId,
    string TargetDescription, DateOnly? TargetDate, BenefitCommitmentLifecycleState LifecycleState, int Version);
