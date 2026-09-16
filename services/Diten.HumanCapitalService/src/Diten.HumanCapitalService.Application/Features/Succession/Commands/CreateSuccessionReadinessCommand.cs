using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.Succession.Commands;

public sealed record CreateSuccessionReadinessCommand(SuccessionReadinessCreateRequest Request) : IRequest<Response<Guid>>;
