using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Commands;

public sealed record CreateBusinessReferenceDataSetCommand(
    string SetCode,
    string Name,
    string ScopeType,
    string? Description,
    string? Status,
    string? CorrelationId)
    : IRequest<Response<BusinessReferenceDataSetDetailModel>>, IBusinessReferenceDataRequest, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.ReferenceData,
        AuditOperation.Create,
        "BusinessReferenceDataSet",
        SourceModule: "PSS-012",
        Metadata: new Dictionary<string, object?> { ["setCode"] = SetCode });
}
