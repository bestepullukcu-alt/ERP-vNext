using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Commands;

public sealed record DisableTenantModuleEntitlementCommand(Guid TenantId, DisableTenantModuleEntitlementRequest Request)
    : IRequest<Response<NoContent>>, ITransactionOwnedAuditCommand;
