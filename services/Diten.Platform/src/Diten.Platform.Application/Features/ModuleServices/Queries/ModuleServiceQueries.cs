using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleServices.Queries;

public sealed record GetModuleServiceByIdQuery(Guid Id) : IRequest<Response<ModuleServiceDto>>;

public sealed record GetModuleServicesQuery(ModuleServiceFilterRequest Filter)
    : IRequest<Response<PagedResult<ModuleServiceDto>>>;
