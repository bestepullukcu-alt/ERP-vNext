using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Commands;

public sealed record RetireBusinessReferenceDataEvidenceFixtureSetCommand(
    string FixtureCode,
    Guid SetId,
    long? ExpectedRowVersion,
    string ActorId,
    string CorrelationId)
    : IRequest<Response<BusinessReferenceDataEvidenceFixtureRetireModel>>, IBusinessReferenceDataRequest, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.ReferenceData,
        AuditOperation.Deactivate,
        "BusinessReferenceDataSet",
        EntityId: SetId,
        SourceModule: "PSS-012",
        Metadata: new Dictionary<string, object?> { ["governanceEvent"] = "fixture_retire", ["fixtureCode"] = FixtureCode });
}
