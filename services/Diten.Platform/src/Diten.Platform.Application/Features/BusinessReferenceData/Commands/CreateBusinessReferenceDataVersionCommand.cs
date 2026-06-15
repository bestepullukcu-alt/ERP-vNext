using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Commands;

public sealed record CreateBusinessReferenceDataVersionCommand(
    Guid SetId,
    Guid? SourceVersionId,
    string? CorrelationId)
    : IRequest<Response<BusinessReferenceDataVersionDetailModel>>, IBusinessReferenceDataRequest, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.ReferenceData,
        AuditOperation.Create,
        "BusinessReferenceDataVersion",
        EntityId: SetId,
        SourceModule: "PSS-012");
}
