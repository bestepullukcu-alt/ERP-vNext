using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Queries;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.QueryHandlers;

/// <summary>
/// FIX-3 — resolves a tenant's effectively accessible modules (reusing the authoritative entitlement projection) and,
/// for each, the permission keys it DECLARES in the descriptor catalog: page <c>RequiredPermission</c> ∪ action
/// <c>PermissionKey</c> (de-duplicated). Descriptors live under the platform scope (Guid.Empty) where
/// self-registration stores them, so they are read inside a platform scope. A module with no descriptors is
/// still returned (with an empty key list) so the AuthService caller knows it is entitled and applies its
/// convention fallback.
/// </summary>
public sealed class GetTenantEntitledModulePermissionsQueryHandler
    : IRequestHandler<GetTenantEntitledModulePermissionsQuery, Response<IReadOnlyList<TenantEntitledModulePermissionsDto>>>
{
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
        if (!entitlements.IsSuccessful)
        {
            return ForwardFailure(entitlements);
        }

        if (entitlements.Data is null)
        {
            return ProjectionUnavailable(entitlements.CorrelationId);
        }

        if (entitlements.Data.Any(row => row.TenantId != request.TenantId))
        {
            return ProjectionUnavailable(entitlements.CorrelationId);
        }

        var candidateCodes = entitlements.Data
            .Select(r => r.ModuleCode)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var entitledCodes = new List<string>(candidateCodes.Count);
        foreach (var code in candidateCodes)
        {
            var effectiveAccess = await _mediator.Send(
                new GetTenantModuleEffectiveAccessQuery(request.TenantId, code),
                ct);
            if (!effectiveAccess.IsSuccessful)
            {
                return ForwardFailure(effectiveAccess);
            }

            if (effectiveAccess.Data is null
                || effectiveAccess.Data.TenantId != request.TenantId
                || !string.Equals(effectiveAccess.Data.ModuleCode, code, StringComparison.OrdinalIgnoreCase))
            {
                return ProjectionUnavailable(effectiveAccess.CorrelationId);
            }

            if (effectiveAccess.Data.HasAccess)
            {
                entitledCodes.Add(code);
            }
        }

        var result = new List<TenantEntitledModulePermissionsDto>(entitledCodes.Count);

        // Descriptors are stored under the platform scope (Guid.Empty), like the catalog UI / self-registration.
        using (TenantScope.BeginPlatform(_tenantContext, Guid.Empty))
        {
            foreach (var code in entitledCodes)
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

    private static Response<IReadOnlyList<TenantEntitledModulePermissionsDto>> ForwardFailure<T>(Response<T> response) =>
        Response<IReadOnlyList<TenantEntitledModulePermissionsDto>>.Fail(
            response.Errors,
            response.StatusCode,
            response.ReasonCode,
            response.CorrelationId);

    private static Response<IReadOnlyList<TenantEntitledModulePermissionsDto>> ProjectionUnavailable(string? correlationId) =>
        Response<IReadOnlyList<TenantEntitledModulePermissionsDto>>.Fail(
            "Tenant entitlement projection is unavailable.",
            503,
            "tenant_entitlement_projection_unavailable",
            correlationId);
}
