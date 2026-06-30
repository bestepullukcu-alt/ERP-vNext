using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Queries;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.QueryHandlers;

/// <summary>
/// FIX-3 — resolves a tenant's effectively-Active entitled modules (reusing the entitlement projection) and,
/// for each, the permission keys it DECLARES in the descriptor catalog: page <c>RequiredPermission</c> ∪ action
/// <c>PermissionKey</c> (de-duplicated). Descriptors live under the platform scope (Guid.Empty) where
/// self-registration stores them, so they are read inside a platform scope. A module with no descriptors is
/// still returned (with an empty key list) so the AuthService caller knows it is entitled and applies its
/// convention fallback.
/// </summary>
public sealed class GetTenantEntitledModulePermissionsQueryHandler
    : IRequestHandler<GetTenantEntitledModulePermissionsQuery, Response<IReadOnlyList<TenantEntitledModulePermissionsDto>>>
{
    private const string ActiveAccess = "Active";

    private readonly IMediator _mediator;
    private readonly IModulePageDescriptorRepository _pageRepository;
    private readonly IModulePageActionDescriptorRepository _actionRepository;
    private readonly ITenantContext _tenantContext;

    public GetTenantEntitledModulePermissionsQueryHandler(
        IMediator mediator,
        IModulePageDescriptorRepository pageRepository,
        IModulePageActionDescriptorRepository actionRepository,
        ITenantContext tenantContext)
    {
        _mediator = mediator;
        _pageRepository = pageRepository;
        _actionRepository = actionRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<TenantEntitledModulePermissionsDto>>> Handle(
        GetTenantEntitledModulePermissionsQuery request,
        CancellationToken ct)
    {
        // Effective entitlement is evaluated against the tenant (reuse the authoritative projection).
        var entitlements = await _mediator.Send(new GetTenantModuleEntitlementsQuery(request.TenantId), ct);
        if (!entitlements.IsSuccessful || entitlements.Data is null)
        {
            return Response<IReadOnlyList<TenantEntitledModulePermissionsDto>>.Success(
                Array.Empty<TenantEntitledModulePermissionsDto>());
        }

        var activeCodes = entitlements.Data
            .Where(r => string.Equals(r.EffectiveAccess, ActiveAccess, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.ModuleCode)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<TenantEntitledModulePermissionsDto>(activeCodes.Count);

        // Descriptors are stored under the platform scope (Guid.Empty), like the catalog UI / self-registration.
        using (TenantScope.BeginPlatform(_tenantContext, Guid.Empty))
        {
            foreach (var code in activeCodes)
            {
                var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var pages = await _pageRepository.GetByModuleAsync(code, ct);
                foreach (var page in pages)
                {
                    if (!string.IsNullOrWhiteSpace(page.RequiredPermission))
                    {
                        keys.Add(page.RequiredPermission.Trim());
                    }

                    var actions = await _actionRepository.GetByPageAsync(page.Id, ct);
                    foreach (var action in actions)
                    {
                        if (!string.IsNullOrWhiteSpace(action.PermissionKey))
                        {
                            keys.Add(action.PermissionKey.Trim());
                        }
                    }
                }

                result.Add(new TenantEntitledModulePermissionsDto(code, keys.ToList()));
            }
        }

        return Response<IReadOnlyList<TenantEntitledModulePermissionsDto>>.Success(result);
    }
}
