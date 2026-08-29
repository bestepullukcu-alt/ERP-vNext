using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementIdentifiers.Commands;

// MOD-0029-FU07 — identifier allocation commands. All mutations are auditable via the central AuditBehavior. No hard
// delete: a cancel is a status change only.

internal static class IdentifierAudit
{
    public const string Module = "MOD-0029-FU07";
    public static Guid? Correlation(string? correlationId) => Guid.TryParse(correlationId, out var c) ? c : null;
}

public sealed record AllocateUidCommand(Guid RegisterEntryId, AllocateIdentifierInput Input, string CorrelationId)
    : IRequest<Response<IdentifierAllocationResultModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Assign, "DocumentIdentifierAllocation",
        EntityId: RegisterEntryId, SourceModule: IdentifierAudit.Module, CorrelationId: IdentifierAudit.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?> { ["identifierType"] = "PermanentUid" });
}

public sealed record AllocateCodeCommand(Guid RegisterEntryId, AllocateIdentifierInput Input, string CorrelationId)
    : IRequest<Response<IdentifierAllocationResultModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Assign, "DocumentIdentifierAllocation",
        EntityId: RegisterEntryId, SourceModule: IdentifierAudit.Module, CorrelationId: IdentifierAudit.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?> { ["identifierType"] = "DocumentCode" });
}

public sealed record AllocateIdentifiersCommand(Guid RegisterEntryId, AllocateIdentifierInput Input, string CorrelationId)
    : IRequest<Response<IdentifierAllocationResultModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Assign, "DocumentIdentifierAllocation",
        EntityId: RegisterEntryId, SourceModule: IdentifierAudit.Module, CorrelationId: IdentifierAudit.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?> { ["identifierType"] = "Both" });
}

public sealed record ReserveIdentifierCommand(ReserveIdentifierInput Input, string CorrelationId)
    : IRequest<Response<IdentifierAllocationModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Create, "DocumentIdentifierAllocation",
        SourceModule: IdentifierAudit.Module, CorrelationId: IdentifierAudit.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?> { ["identifierType"] = Input.IdentifierType, ["reserve"] = true });
}

public sealed record CancelIdentifierCommand(Guid AllocationId, CancelIdentifierInput Input, string CorrelationId)
    : IRequest<Response<IdentifierAllocationModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Update, "DocumentIdentifierAllocation",
        EntityId: AllocationId, SourceModule: IdentifierAudit.Module, CorrelationId: IdentifierAudit.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?> { ["cancel"] = true });
}
