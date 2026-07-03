using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Navigation.Commands;
using Diten.Platform.Application.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Navigation.Handlers;

/// <summary>
/// FEAT-TENANT-NAV-PREFS — replaces the tenant's full preference set. Each incoming row is accepted ONLY if the
/// tenant is entitled to that module (so a tenant can never hide/rename a module it does not have, nor inject an
/// unknown module). Duplicate module codes collapse to the last value; the persisted set then replaces the prior one.
/// </summary>
public sealed class ReplaceTenantNavPreferencesCommandHandler
    : IRequestHandler<ReplaceTenantNavPreferencesCommand, Response<NoContent>>
{
    private readonly ITenantNavPreferenceRepository _repository;
    private readonly ITenantNavDomainPreferenceRepository _domainRepository;
    private readonly ITenantModuleAccessService _accessService;

    public ReplaceTenantNavPreferencesCommandHandler(
        ITenantNavPreferenceRepository repository,
        ITenantNavDomainPreferenceRepository domainRepository,
        ITenantModuleAccessService accessService)
    {
        _repository = repository;
        _domainRepository = domainRepository;
        _accessService = accessService;
    }

    public async Task<Response<NoContent>> Handle(ReplaceTenantNavPreferencesCommand request, CancellationToken ct)
    {
        // ── Modules: dedupe by ModuleCode (last wins), drop blanks, accept ONLY entitled modules. ──
        var byCode = new Dictionary<string, TenantNavPreferenceDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in request.Modules ?? Array.Empty<TenantNavPreferenceDto>())
        {
            if (item is not null && !string.IsNullOrWhiteSpace(item.ModuleCode))
            {
                byCode[item.ModuleCode.Trim()] = item;
            }
        }

        var acceptedModules = new List<TenantNavPreference>();
        foreach (var (moduleCode, dto) in byCode)
        {
            // Authoritative boundary: only entitled modules may carry a preference.
            if (!await _accessService.HasAccessAsync(request.TenantId, moduleCode, ct))
            {
                continue;
            }

            acceptedModules.Add(new TenantNavPreference
            {
                TenantId = request.TenantId,
                ModuleCode = moduleCode,
                SortOrder = dto.SortOrder,
                IsHidden = dto.IsHidden,
                DisplayNameOverride = string.IsNullOrWhiteSpace(dto.DisplayNameOverride) ? null : dto.DisplayNameOverride.Trim()
            });
        }

        await _repository.ReplaceForTenantAsync(request.TenantId, acceptedModules, ct);

        // ── Domains: dedupe by DomainCode (last wins), drop blanks. Orphan domains (no entitled module) are
        //    harmless: the menu query only applies a domain pref to a domain that actually renders. ──
        var byDomain = new Dictionary<string, TenantNavDomainPreferenceDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in request.Domains ?? Array.Empty<TenantNavDomainPreferenceDto>())
        {
            if (item is not null && !string.IsNullOrWhiteSpace(item.DomainCode))
            {
                byDomain[item.DomainCode.Trim()] = item;
            }
        }

        var acceptedDomains = byDomain.Values.Select(dto => new TenantNavDomainPreference
        {
            TenantId = request.TenantId,
            DomainCode = dto.DomainCode.Trim(),
            SortOrder = dto.SortOrder,
            DisplayNameOverride = string.IsNullOrWhiteSpace(dto.DisplayNameOverride) ? null : dto.DisplayNameOverride.Trim()
        }).ToList();

        await _domainRepository.ReplaceForTenantAsync(request.TenantId, acceptedDomains, ct);

        return Response<NoContent>.Success(204);
    }
}
