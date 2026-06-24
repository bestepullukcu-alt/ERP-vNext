using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleDomains.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleDomains.Handlers.QueryHandlers;

public sealed class GetModuleDomainByIdQueryHandler
    : IRequestHandler<GetModuleDomainByIdQuery, Response<ModuleDomainDto>>
{
    private readonly IModuleDomainRepository _repository;

    public GetModuleDomainByIdQueryHandler(IModuleDomainRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<ModuleDomainDto>> Handle(GetModuleDomainByIdQuery request, CancellationToken ct)
    {
        var item = await _repository.GetByIdAsync(request.Id, ct);
        return item is null
            ? Response<ModuleDomainDto>.Fail("Module domain not found.", 404)
            : Response<ModuleDomainDto>.Success(ModuleDomainMapper.ToDto(item));
    }
}
