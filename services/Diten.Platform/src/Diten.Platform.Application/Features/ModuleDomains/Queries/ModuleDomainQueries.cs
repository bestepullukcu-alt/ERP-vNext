using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleDomains.Queries;

public sealed record GetModuleDomainByIdQuery(Guid Id) : IRequest<Response<ModuleDomainDto>>;

public sealed record GetModuleDomainsQuery(ModuleDomainFilterRequest Filter)
    : IRequest<Response<PagedResult<ModuleDomainDto>>>;
