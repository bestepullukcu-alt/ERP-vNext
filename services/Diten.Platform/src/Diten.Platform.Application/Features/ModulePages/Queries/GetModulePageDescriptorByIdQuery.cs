using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ModulePages.Queries;

public sealed record GetModulePageDescriptorByIdQuery(Guid Id) : IRequest<Response<ModulePageDescriptorDto>>;
