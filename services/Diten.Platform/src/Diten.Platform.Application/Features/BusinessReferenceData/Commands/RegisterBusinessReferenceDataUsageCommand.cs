using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Commands;

public sealed record RegisterBusinessReferenceDataUsageCommand(
    string SetCode,
    string ConsumerModule,
    string ConsumerName,
    string? ConsumerEndpoint,
    string? ScopeType,
    string? ScopeKey,
    int? VersionPin,
    DateTimeOffset? AsOfDate,
    string? ResolutionMode,
    string? Criticality,
    string? Notes,
    string ActorId,
    string CorrelationId)
    : IRequest<Response<BusinessReferenceDataUsageRegistrationResultModel>>, IBusinessReferenceDataRequest, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.ReferenceData,
        AuditOperation.Create,
        "BusinessReferenceDataUsageRegistration",
        SourceModule: "PSS-012",
        Metadata: new Dictionary<string, object?> { ["setCode"] = SetCode, ["consumerModule"] = ConsumerModule, ["consumerName"] = ConsumerName });
}
