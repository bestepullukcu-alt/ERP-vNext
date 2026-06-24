using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Navigation.Queries;

/// <summary>
/// MOD-0285 — resolves the dynamic navigation tree for a tenant: its entitled modules' navigation-visible,
/// Active page descriptors. <paramref name="TenantId"/> is the middleware-resolved tenant (JWT tenant_id),
/// not a client-supplied value, so the result cannot leak another tenant's entitlements.
/// </summary>
public sealed record GetTenantNavigationMenuQuery(Guid TenantId)
    : IRequest<Response<IReadOnlyList<NavigationModuleGroupDto>>>;
