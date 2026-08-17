using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Commands;

public sealed record CommitBusinessReferenceDataImportCommand(
    Guid PreviewId,
    string IdempotencyKey,
    string ActorId,
    string CorrelationId)
    : IRequest<Response<BusinessReferenceDataImportCommitResultModel>>, IBusinessReferenceDataRequest, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.ReferenceData,
        AuditOperation.Update,
        "BusinessReferenceDataImportPreview",
        EntityId: PreviewId,
        SourceModule: "PSS-012",
        Metadata: new Dictionary<string, object?> { ["governanceEvent"] = "import_commit" });
}
