using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementMasterRegister.Commands;

// MOD-0029-FU06 — Document Master Register commands (sealed records; handlers delegate to the service). Mutations
// are auditable via the central AuditBehavior. No hard delete in this FU.

internal static class MasterRegisterAudit
{
    public const string Module = "MOD-0029-FU06";
    public static Guid? Correlation(string? correlationId) => Guid.TryParse(correlationId, out var c) ? c : null;
}

public sealed record CreateMasterRegisterEntryCommand(CreateMasterRegisterEntryInput Input, string CorrelationId)
    : IRequest<Response<MasterRegisterDetailModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Create, "DocumentMasterRegisterEntry",
        SourceModule: MasterRegisterAudit.Module, CorrelationId: MasterRegisterAudit.Correlation(CorrelationId));
}

public sealed record UpdateMasterRegisterMetadataCommand(Guid EntryId, UpdateMasterRegisterMetadataInput Input, string CorrelationId)
    : IRequest<Response<MasterRegisterDetailModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Update, "DocumentMasterRegisterEntry",
        EntityId: EntryId, SourceModule: MasterRegisterAudit.Module, CorrelationId: MasterRegisterAudit.Correlation(CorrelationId));
}

public sealed record LinkControlledDocumentToRegisterEntryCommand(Guid EntryId, LinkControlledDocumentInput Input, string CorrelationId)
    : IRequest<Response<MasterRegisterDetailModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Assign, "DocumentMasterRegisterEntry",
        EntityId: EntryId, SourceModule: MasterRegisterAudit.Module, CorrelationId: MasterRegisterAudit.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?> { ["controlledDocumentId"] = Input.ControlledDocumentId });
}
