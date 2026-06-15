using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Commands;

public sealed record ReplaceBusinessReferenceDataVersionValuesCommand(
    Guid VersionId,
    string ActorId,
    string CorrelationId,
    string? ExpectedConcurrencyToken,
    IReadOnlyList<BusinessReferenceDataVersionValueInputModel> Values)
    : IRequest<Response<BusinessReferenceDataVersionDetailModel>>, IBusinessReferenceDataRequest, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.ReferenceData,
        AuditOperation.Update,
        "BusinessReferenceDataVersion",
        EntityId: VersionId,
        SourceModule: "PSS-012",
        Metadata: new Dictionary<string, object?> { ["governanceEvent"] = "replace_values", ["valueCount"] = Values.Count });
}
