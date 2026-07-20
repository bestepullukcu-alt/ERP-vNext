using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Quotas.Commands;

public sealed record ReleaseQuotaCommand(ReleaseQuotaRequest Request)
    : IRequest<Response<QuotaMutationDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.Quota, Operation: AuditOperation.Execute, EntityType: "Quota",
        SourceModule: "quotas");
}
