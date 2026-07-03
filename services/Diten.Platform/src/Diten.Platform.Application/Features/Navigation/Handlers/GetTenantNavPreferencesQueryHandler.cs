using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Navigation;
using Diten.Platform.Application.Features.Navigation.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Navigation.Handlers;

public sealed class GetTenantNavPreferencesQueryHandler
    : IRequestHandler<GetTenantNavPreferencesQuery, Response<TenantNavPreferencesDto>>
{
    private readonly ITenantNavPreferenceRepository _moduleRepository;
    private readonly ITenantNavDomainPreferenceRepository _domainRepository;

    public GetTenantNavPreferencesQueryHandler(
        ITenantNavPreferenceRepository moduleRepository,
        ITenantNavDomainPreferenceRepository domainRepository)
    {
        _moduleRepository = moduleRepository;
        _domainRepository = domainRepository;
    }

    public async Task<Response<TenantNavPreferencesDto>> Handle(GetTenantNavPreferencesQuery request, CancellationToken ct)
    {
        var modules = (await _moduleRepository.GetByTenantAsync(request.TenantId, ct))
            .Select(p => new TenantNavPreferenceDto(p.ModuleCode, p.SortOrder, p.IsHidden, p.DisplayNameOverride))
            .ToList();

        var domains = (await _domainRepository.GetByTenantAsync(request.TenantId, ct))
            .Select(p => new TenantNavDomainPreferenceDto(p.DomainCode, p.SortOrder, p.DisplayNameOverride))
            .ToList();

        return Response<TenantNavPreferencesDto>.Success(new TenantNavPreferencesDto(modules, domains));
    }
}
