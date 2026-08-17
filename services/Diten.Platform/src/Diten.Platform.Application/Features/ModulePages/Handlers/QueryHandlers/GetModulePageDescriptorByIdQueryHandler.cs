using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModulePages.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModulePages.Handlers.QueryHandlers;

public sealed class GetModulePageDescriptorByIdQueryHandler : IRequestHandler<GetModulePageDescriptorByIdQuery, Response<ModulePageDescriptorDto>>
{
    private readonly IModulePageDescriptorRepository _repository;

    public GetModulePageDescriptorByIdQueryHandler(IModulePageDescriptorRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<ModulePageDescriptorDto>> Handle(GetModulePageDescriptorByIdQuery request, CancellationToken ct)
    {
        var descriptor = await _repository.GetByIdAsync(request.Id, ct);
        return descriptor is null
            ? Response<ModulePageDescriptorDto>.Fail("Module page descriptor not found.", 404)
            : Response<ModulePageDescriptorDto>.Success(ModulePageDescriptorMapper.ToDto(descriptor));
    }
}
