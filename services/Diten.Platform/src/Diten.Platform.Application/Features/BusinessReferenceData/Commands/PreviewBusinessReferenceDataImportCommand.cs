using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Commands;

public sealed record PreviewBusinessReferenceDataImportCommand(
    Guid TargetDraftVersionId,
    string FileName,
    string Format,
    string ContentBase64,
    string ActorId,
    string CorrelationId)
    : IRequest<Response<BusinessReferenceDataImportPreviewModel>>, IBusinessReferenceDataRequest, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.ReferenceData,
        AuditOperation.Execute,
        "BusinessReferenceDataImportPreview",
        EntityId: TargetDraftVersionId,
        SourceModule: "PSS-012",
        Metadata: new Dictionary<string, object?> { ["governanceEvent"] = "import_preview", ["format"] = Format, ["fileName"] = FileName });
}
