using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.SelfService.Commands;

public sealed record CreateSelfServiceReadinessCommand(SelfServiceReadinessCreateRequest Request) : IRequest<Response<Guid>>;
