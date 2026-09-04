using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.BenefitCommitments;

public sealed record SoftDeleteBenefitCommitmentCommand(Guid Id, int ExpectedVersion) : IRequest<Response<NoContent>>;
