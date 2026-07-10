using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Quotas.Commands;

public sealed record SyncTenantQuotaLimitsFromSubscriptionCommand(Guid TenantId, SyncTenantQuotaLimitsRequest Request)
    : IRequest<Response<IReadOnlyList<QuotaStatusDto>>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.Quota, Operation: AuditOperation.Update, EntityType: "Quota",
        TargetTenantId: TenantId, SourceModule: "quotas");
}
