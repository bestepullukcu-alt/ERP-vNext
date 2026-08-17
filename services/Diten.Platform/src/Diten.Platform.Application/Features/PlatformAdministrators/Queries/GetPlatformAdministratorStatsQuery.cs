using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Queries;

public sealed record GetPlatformAdministratorStatsQuery : IRequest<Response<PlatformAdministratorStatsDto>>;
