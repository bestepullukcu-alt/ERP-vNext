using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementElectronicSignature.Commands;

// MOD-0029-FU23 — electronic signature commands. Auditable via the central AuditBehavior.
// No command deletes anything: policy retirement, request cancellation/rejection and signature invalidation are all
// status changes, and a signature record is never rewritten.

internal static class ElectronicSignatureAudit
{
    public const string Module = "MOD-0029-FU23";
    public static Guid? Correlation(string? c) => Guid.TryParse(c, out var g) ? g : null;
    public static AuditRequestMetadata Meta(AuditOperation op, string entityType, Guid entityId, string correlationId) =>
        new(AuditCategory.DocumentManagement, op, entityType, EntityId: entityId, SourceModule: Module, CorrelationId: Correlation(correlationId));
}

// ── signature policies ───────────────────────────────────────────────────────

public sealed record CreateSignaturePolicyCommand(CreateSignaturePolicyInput Input, string CorrelationId)
    : IRequest<Response<SignaturePolicyModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => ElectronicSignatureAudit.Meta(AuditOperation.Create, "DocumentSignaturePolicy", Guid.Empty, CorrelationId);
}

public sealed record ActivateSignaturePolicyCommand(Guid Id, string CorrelationId)
    : IRequest<Response<SignaturePolicyModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => ElectronicSignatureAudit.Meta(AuditOperation.Update, "DocumentSignaturePolicy", Id, CorrelationId);
}

public sealed record RetireSignaturePolicyCommand(Guid Id, string CorrelationId)
    : IRequest<Response<SignaturePolicyModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => ElectronicSignatureAudit.Meta(AuditOperation.Update, "DocumentSignaturePolicy", Id, CorrelationId);
}

// ── signature requests ───────────────────────────────────────────────────────

public sealed record CreateSignatureRequestCommand(CreateSignatureRequestInput Input, string CorrelationId)
    : IRequest<Response<SignatureRequestModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => ElectronicSignatureAudit.Meta(AuditOperation.Create, "DocumentSignatureRequest", Input.SubjectId, CorrelationId);
}

public sealed record CancelSignatureRequestCommand(Guid Id, CancelSignatureRequestInput Input, string CorrelationId)
    : IRequest<Response<SignatureRequestModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => ElectronicSignatureAudit.Meta(AuditOperation.Update, "DocumentSignatureRequest", Id, CorrelationId);
}

public sealed record RejectSignatureRequestCommand(Guid Id, RejectSignatureRequestInput Input, string CorrelationId)
    : IRequest<Response<SignatureRequestModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => ElectronicSignatureAudit.Meta(AuditOperation.Update, "DocumentSignatureRequest", Id, CorrelationId);
}

// ── signing / verification ───────────────────────────────────────────────────

public sealed record SignDocumentSubjectCommand(SignDocumentSubjectInput Input, string CorrelationId)
    : IRequest<Response<SignatureRecordModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => ElectronicSignatureAudit.Meta(AuditOperation.Create, "DocumentSignatureRecord", Input.SubjectId, CorrelationId);
}

public sealed record VerifySignatureCommand(Guid Id, string CorrelationId)
    : IRequest<Response<SignatureVerificationModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => ElectronicSignatureAudit.Meta(AuditOperation.Update, "DocumentSignatureRecord", Id, CorrelationId);
}

public sealed record InvalidateSignatureCommand(Guid Id, InvalidateSignatureInput Input, string CorrelationId)
    : IRequest<Response<SignatureRecordModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => ElectronicSignatureAudit.Meta(AuditOperation.Update, "DocumentSignatureRecord", Id, CorrelationId);
}
