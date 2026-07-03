using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;

public sealed record CommitQmsBaselineImportCommand(
    string FileName,
    string Format,
    string ContentBase64,
    string SourceBaselineKey,
    string BaselineVersion,
    string? ChangeSummary,
    string CorrelationId) : IRequest<Response<QmsBaselineCommitResult>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Create, "QmsBaseline",
        SourceModule: "MOD-0028", CorrelationId: Guid.TryParse(CorrelationId, out var c) ? c : null,
        Metadata: new Dictionary<string, object?> { ["sourceBaselineKey"] = SourceBaselineKey, ["baselineVersion"] = BaselineVersion });
}
