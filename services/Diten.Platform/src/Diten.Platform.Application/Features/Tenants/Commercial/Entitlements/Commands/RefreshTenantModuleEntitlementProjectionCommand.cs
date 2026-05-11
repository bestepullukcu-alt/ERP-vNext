using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Commands;

public sealed record RefreshTenantModuleEntitlementProjectionCommand(Guid TenantId) : IRequest<Response<NoContent>>;
