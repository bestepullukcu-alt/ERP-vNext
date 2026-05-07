using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants;
using MediatR;

namespace Diten.Platform.Application.Features.ModulePages.Queries;

public sealed record SearchModulePageDescriptorsQuery(ModulePageDescriptorFilterRequest Filter)
    : IRequest<Response<PagedResult<ModulePageDescriptorListItemDto>>>;
