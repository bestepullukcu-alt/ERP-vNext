using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Commands;

public sealed record UpdateTenantModuleEntitlementExpiryCommand(Guid TenantId, Guid EntitlementId, UpdateTenantModuleEntitlementExpiryRequest Request) : IRequest<Response<NoContent>>;
