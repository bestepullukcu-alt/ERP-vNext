using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleServices.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleServices.Handlers.QueryHandlers;

public sealed class GetModuleServiceByIdQueryHandler
    : IRequestHandler<GetModuleServiceByIdQuery, Response<ModuleServiceDto>>
{
    private readonly IModuleServiceRepository _repository;

    public GetModuleServiceByIdQueryHandler(IModuleServiceRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<ModuleServiceDto>> Handle(GetModuleServiceByIdQuery request, CancellationToken ct)
    {
        var item = await _repository.GetByIdAsync(request.Id, ct);
        return item is null
            ? Response<ModuleServiceDto>.Fail("Module service not found.", 404)
            : Response<ModuleServiceDto>.Success(ModuleServiceMapper.ToDto(item));
    }
}
