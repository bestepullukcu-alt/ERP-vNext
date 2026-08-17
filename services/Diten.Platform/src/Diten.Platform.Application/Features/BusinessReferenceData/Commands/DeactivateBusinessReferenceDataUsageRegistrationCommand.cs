using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Commands;

public sealed record DeactivateBusinessReferenceDataUsageRegistrationCommand(
    Guid UsageRegistrationId,
    string ActorId,
    string CorrelationId)
    : IRequest<Response<bool>>, IBusinessReferenceDataRequest, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.ReferenceData,
        AuditOperation.Deactivate,
        "BusinessReferenceDataUsageRegistration",
        EntityId: UsageRegistrationId,
        SourceModule: "PSS-012");
}
