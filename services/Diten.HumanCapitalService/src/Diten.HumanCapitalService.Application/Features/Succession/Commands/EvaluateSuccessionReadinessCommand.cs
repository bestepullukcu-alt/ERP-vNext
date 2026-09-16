using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.Succession.Commands;

public sealed record EvaluateSuccessionReadinessCommand(Guid Id) : IRequest<Response<SuccessionReadinessDto>>;
