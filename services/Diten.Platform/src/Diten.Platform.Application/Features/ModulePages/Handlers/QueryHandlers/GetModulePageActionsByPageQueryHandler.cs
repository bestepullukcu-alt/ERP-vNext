using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModulePages.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModulePages.Handlers.QueryHandlers;

public sealed class GetModulePageActionsByPageQueryHandler
    : IRequestHandler<GetModulePageActionsByPageQuery, Response<IReadOnlyList<ModulePageActionDescriptorDto>>>
{
    private readonly IModulePageDescriptorRepository _pageRepository;
    private readonly IModulePageActionDescriptorRepository _repository;

    public GetModulePageActionsByPageQueryHandler(
        IModulePageDescriptorRepository pageRepository,
        IModulePageActionDescriptorRepository repository)
    {
        _pageRepository = pageRepository;
        _repository = repository;
    }

    public async Task<Response<IReadOnlyList<ModulePageActionDescriptorDto>>> Handle(GetModulePageActionsByPageQuery request, CancellationToken ct)
    {
        var page = await _pageRepository.GetByIdAsync(request.PageDescriptorId, ct);
        if (page is null)
        {
            return Response<IReadOnlyList<ModulePageActionDescriptorDto>>.Fail("Module page descriptor not found.", 404);
        }

        var items = await _repository.GetByPageAsync(request.PageDescriptorId, ct);
        return Response<IReadOnlyList<ModulePageActionDescriptorDto>>.Success(items.Select(ModulePageActionDescriptorMapper.ToDto).ToList());
    }
}
