using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.CompensationBenefits.Commands;

public sealed record DeleteCompensationBenefitsReadinessCommand(Guid Id) : IRequest<Response<bool>>;
