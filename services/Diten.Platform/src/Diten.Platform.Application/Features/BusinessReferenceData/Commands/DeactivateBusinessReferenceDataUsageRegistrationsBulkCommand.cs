using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Commands;

public sealed record DeactivateBusinessReferenceDataUsageRegistrationsBulkCommand(
    IReadOnlyList<Guid> UsageRegistrationIds,
    string ActorId,
    string CorrelationId)
    : IRequest<Response<int>>, IBusinessReferenceDataRequest, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.ReferenceData,
        AuditOperation.Deactivate,
        "BusinessReferenceDataUsageRegistration",
        SourceModule: "PSS-012",
        Metadata: new Dictionary<string, object?> { ["count"] = UsageRegistrationIds?.Count ?? 0 });
}
