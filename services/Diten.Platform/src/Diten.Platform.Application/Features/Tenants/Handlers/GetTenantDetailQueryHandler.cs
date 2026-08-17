using AutoMapper;
using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Application.Features.Tenants.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class GetTenantDetailQueryHandler : IRequestHandler<GetTenantDetailQuery, TenantDetailDto?>
{
    private readonly ITenantRegistryRepository _repository;
    private readonly IMapper _mapper;

    public GetTenantDetailQueryHandler(ITenantRegistryRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<TenantDetailDto?> Handle(GetTenantDetailQuery request, CancellationToken cancellationToken)
    {
        var tenant = await _repository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant == null)
        {
            return null;
        }

        return _mapper.Map<TenantDetailDto>(tenant);
    }
}
