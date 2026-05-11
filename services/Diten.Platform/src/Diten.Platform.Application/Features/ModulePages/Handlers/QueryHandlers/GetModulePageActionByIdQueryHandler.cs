using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModulePages.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModulePages.Handlers.QueryHandlers;

public sealed class GetModulePageActionByIdQueryHandler
    : IRequestHandler<GetModulePageActionByIdQuery, Response<ModulePageActionDescriptorDto>>
{
    private readonly IModulePageActionDescriptorRepository _repository;

    public GetModulePageActionByIdQueryHandler(IModulePageActionDescriptorRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<ModulePageActionDescriptorDto>> Handle(GetModulePageActionByIdQuery request, CancellationToken ct)
    {
        var descriptor = await _repository.GetByIdAsync(request.Id, ct);
        return descriptor is null
            ? Response<ModulePageActionDescriptorDto>.Fail("Module page action descriptor not found.", 404)
            : Response<ModulePageActionDescriptorDto>.Success(ModulePageActionDescriptorMapper.ToDto(descriptor));
    }
}
