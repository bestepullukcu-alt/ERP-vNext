using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementVariantLocalization.Commands;

// MOD-0029-FU18 — variant localization / translation governance commands. Auditable via the central
// AuditBehavior. No command here deletes anything, transitions the parent, or touches variant content.

internal static class VariantLocalizationAudit
{
    public const string Module = "MOD-0029-FU18";
    public static Guid? Correlation(string? c) => Guid.TryParse(c, out var g) ? g : null;
    public static AuditRequestMetadata Meta(AuditOperation op, string entityType, Guid entityId, string correlationId) =>
        new(AuditCategory.DocumentManagement, op, entityType, EntityId: entityId, SourceModule: Module, CorrelationId: Correlation(correlationId));
}

public sealed record UpsertVariantLocalizationProfileCommand(Guid VariantId, VariantLocalizationProfileInput Input, string CorrelationId)
    : IRequest<Response<VariantLocalizationProfileModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() =>
        VariantLocalizationAudit.Meta(AuditOperation.Update, "TemplateVariantLocalizationProfile", VariantId, CorrelationId);
}

public sealed record RequireBilingualReviewCommand(Guid VariantId, string CorrelationId)
    : IRequest<Response<VariantLocalizationProfileModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() =>
        VariantLocalizationAudit.Meta(AuditOperation.Update, "TemplateVariantLocalizationProfile", VariantId, CorrelationId);
}

public sealed record RecordBilingualReviewEvidenceCommand(Guid VariantId, RecordBilingualReviewInput Input, string CorrelationId)
    : IRequest<Response<VariantLocalizationProfileModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() =>
        VariantLocalizationAudit.Meta(AuditOperation.Create, "TemplateVariantReviewEvidence", VariantId, CorrelationId);
}

public sealed record RejectBilingualReviewCommand(Guid VariantId, RejectVariantReviewInput Input, string CorrelationId)
    : IRequest<Response<VariantLocalizationProfileModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() =>
        VariantLocalizationAudit.Meta(AuditOperation.Create, "TemplateVariantReviewEvidence", VariantId, CorrelationId);
}

public sealed record RequireLocalApprovalCommand(Guid VariantId, string CorrelationId)
    : IRequest<Response<VariantLocalizationProfileModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() =>
        VariantLocalizationAudit.Meta(AuditOperation.Update, "TemplateVariantLocalizationProfile", VariantId, CorrelationId);
}

public sealed record RecordLocalApprovalEvidenceCommand(Guid VariantId, RecordLocalApprovalInput Input, string CorrelationId)
    : IRequest<Response<VariantLocalizationProfileModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() =>
        VariantLocalizationAudit.Meta(AuditOperation.Create, "TemplateVariantReviewEvidence", VariantId, CorrelationId);
}

public sealed record RejectLocalApprovalCommand(Guid VariantId, RejectVariantReviewInput Input, string CorrelationId)
    : IRequest<Response<VariantLocalizationProfileModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() =>
        VariantLocalizationAudit.Meta(AuditOperation.Create, "TemplateVariantReviewEvidence", VariantId, CorrelationId);
}

public sealed record AllowTemporaryEnglishMasterCommand(Guid VariantId, AllowTemporaryEnglishMasterInput Input, string CorrelationId)
    : IRequest<Response<VariantLocalizationProfileModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() =>
        VariantLocalizationAudit.Meta(AuditOperation.Update, "TemplateVariantLocalizationProfile", VariantId, CorrelationId);
}

public sealed record RevokeTemporaryEnglishMasterCommand(Guid VariantId, string CorrelationId)
    : IRequest<Response<VariantLocalizationProfileModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() =>
        VariantLocalizationAudit.Meta(AuditOperation.Update, "TemplateVariantLocalizationProfile", VariantId, CorrelationId);
}

/// <summary>Assesses the variant against its parent. Records a verdict; never transitions the parent or the variant's FU03 status.</summary>
public sealed record EvaluateVariantParentChangeCommand(Guid VariantId, string? EvidenceReference, string CorrelationId)
    : IRequest<Response<VariantParentChangeAssessmentModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() =>
        VariantLocalizationAudit.Meta(AuditOperation.Execute, "TemplateVariantParentChangeAssessment", VariantId, CorrelationId);
}
