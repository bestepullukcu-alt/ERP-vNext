using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Commands;

public sealed record RemoveTenantManualModuleOverrideCommand(Guid TenantId, Guid EntitlementId, RemoveTenantManualModuleOverrideRequest Request) : IRequest<Response<NoContent>>;
