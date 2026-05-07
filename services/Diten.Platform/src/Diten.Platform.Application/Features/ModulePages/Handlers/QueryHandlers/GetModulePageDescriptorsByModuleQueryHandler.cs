using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModulePages.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModulePages.Handlers.QueryHandlers;

public sealed class GetModulePageDescriptorsByModuleQueryHandler
    : IRequestHandler<GetModulePageDescriptorsByModuleQuery, Response<IReadOnlyList<ModulePageDescriptorListItemDto>>>
{
    private readonly IModulePageDescriptorRepository _repository;

    public GetModulePageDescriptorsByModuleQueryHandler(IModulePageDescriptorRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<IReadOnlyList<ModulePageDescriptorListItemDto>>> Handle(GetModulePageDescriptorsByModuleQuery request, CancellationToken ct)
    {
        var moduleCode = ModulePageDescriptorNormalizer.NormalizeModuleCode(request.ModuleCode);
        var descriptors = await _repository.GetByModuleAsync(moduleCode, ct);
        return Response<IReadOnlyList<ModulePageDescriptorListItemDto>>.Success(descriptors.Select(ModulePageDescriptorMapper.ToListDto).ToList());
    }
}
