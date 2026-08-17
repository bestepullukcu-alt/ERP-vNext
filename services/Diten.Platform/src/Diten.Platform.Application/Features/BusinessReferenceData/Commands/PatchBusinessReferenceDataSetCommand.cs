using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Commands;

public sealed record PatchBusinessReferenceDataSetCommand(
    Guid SetId,
    long RowVersion,
    string? Name,
    string? Description,
    string? Status,
    string? SetCode,
    string? ScopeType,
    string? CorrelationId)
    : IRequest<Response<BusinessReferenceDataSetDetailModel>>, IBusinessReferenceDataRequest, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.ReferenceData,
        AuditOperation.Update,
        "BusinessReferenceDataSet",
        EntityId: SetId,
        SourceModule: "PSS-012");
}
