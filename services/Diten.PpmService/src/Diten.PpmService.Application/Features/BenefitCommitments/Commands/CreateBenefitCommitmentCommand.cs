using Diten.PpmService.Application.Common;
using MediatR;

namespace Diten.PpmService.Application.Features.BenefitCommitments;

public sealed record CreateBenefitCommitmentCommand(string Code, string Title, string? Description, Guid InvestmentCaseId, string TargetDescription, DateOnly? TargetDate) : IRequest<Response<BenefitCommitmentDto>>;
