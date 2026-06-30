using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Queries;

/// <summary>
/// FIX-3 — returns each effectively-Active entitled module of a tenant together with the permission keys it
/// declares in the descriptor catalog. Consumed S2S by AuthService to drive a catalog-authoritative
/// entitlement → role-permission sync (namespace-agnostic), with a convention fallback when a module declares
/// no keys.
/// </summary>
public sealed record GetTenantEntitledModulePermissionsQuery(Guid TenantId)
    : IRequest<Response<IReadOnlyList<TenantEntitledModulePermissionsDto>>>;
