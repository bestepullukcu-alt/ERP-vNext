using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Quotas.Commands;

public sealed record InitializeTenantQuotasCommand(Guid TenantId, InitializeTenantQuotasRequest Request)
    : IRequest<Response<IReadOnlyList<QuotaStatusDto>>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.Quota, Operation: AuditOperation.Create, EntityType: "Quota",
        TargetTenantId: TenantId, SourceModule: "quotas");
}
