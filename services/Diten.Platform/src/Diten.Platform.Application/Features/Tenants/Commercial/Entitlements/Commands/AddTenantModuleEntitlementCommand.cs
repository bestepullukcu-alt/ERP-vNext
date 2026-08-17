using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Commands;

public sealed record AddTenantModuleEntitlementCommand(Guid TenantId, TenantModuleEntitlementRequest Request) : IRequest<Response<Guid>>;
