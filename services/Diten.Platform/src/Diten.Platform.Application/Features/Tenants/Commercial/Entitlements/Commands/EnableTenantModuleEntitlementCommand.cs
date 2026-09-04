using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Commands;

public sealed record EnableTenantModuleEntitlementCommand(Guid TenantId, Guid EntitlementId, byte[]? RowVersion)
    : IRequest<Response<NoContent>>, ITransactionOwnedAuditCommand;
