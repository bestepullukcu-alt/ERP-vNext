using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Queries;

public sealed record GetTenantModuleEntitlementsQuery(Guid TenantId) : IRequest<Response<IReadOnlyList<TenantModuleEntitlementRowDto>>>;
