using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.SensitiveAccess.Queries;

public sealed record GetSensitiveAccessHealthQuery() : IRequest<Response<SensitiveAccessHealthDto>>;
