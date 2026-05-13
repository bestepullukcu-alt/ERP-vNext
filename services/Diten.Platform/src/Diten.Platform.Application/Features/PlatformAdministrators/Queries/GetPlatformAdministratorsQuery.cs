using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants;
using MediatR;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Queries;

public sealed record GetPlatformAdministratorsQuery(PlatformAdministratorFilterRequest Filter)
    : IRequest<Response<PagedResult<PlatformAdministratorListItemDto>>>;
