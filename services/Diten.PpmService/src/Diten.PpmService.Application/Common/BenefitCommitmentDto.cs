using Diten.PpmService.Domain.Entities;

namespace Diten.PpmService.Application.Common;

public sealed record BenefitCommitmentDto(Guid Id, string Code, string Title, string? Description, Guid InvestmentCaseId,
    string TargetDescription, DateOnly? TargetDate, BenefitCommitmentLifecycleState LifecycleState, int Version);
