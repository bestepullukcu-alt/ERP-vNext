using Diten.PpmService.Application.Common;
using MediatR;

namespace Diten.PpmService.Application.Features.BenefitCommitments;

public sealed record UpdateBenefitCommitmentCommand(Guid Id, string Code, string Title, string? Description, string TargetDescription, DateOnly? TargetDate, int ExpectedVersion) : IRequest<Response<BenefitCommitmentDto>>;
