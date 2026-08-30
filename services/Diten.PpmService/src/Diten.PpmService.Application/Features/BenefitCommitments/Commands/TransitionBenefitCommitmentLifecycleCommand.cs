using Diten.PpmService.Application.Common;
using Diten.PpmService.Domain.Entities;
using MediatR;

namespace Diten.PpmService.Application.Features.BenefitCommitments;

public sealed record TransitionBenefitCommitmentLifecycleCommand(Guid Id, BenefitCommitmentLifecycleState TargetState, int ExpectedVersion) : IRequest<Response<BenefitCommitmentDto>>;
