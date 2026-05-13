using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Queries;

public sealed record GetPlatformAdministratorByIdQuery(Guid Id) : IRequest<Response<PlatformAdministratorDetailDto>>;
