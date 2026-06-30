using Diten.Platform.Domain.Entities.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;

/// <summary>MOD-0029-FU01 — entity → wire model mappers (UPPER_SNAKE enums, no internal storage key leakage).</summary>
public static class ControlledDocumentMapping
{
    public static ControlledDocumentListItemModel ToListItem(ControlledDocument d) => new(
        d.Id,
        d.DocumentKey,
        d.Title,
        d.DocumentType.ToWire(),
        d.CompanyId,
        d.CollectionInstanceId,
        d.CollectionPath,
        d.CurrentVersionNumber,
        d.CurrentVersionId,
        d.Status.ToWire(),
        d.Controlled,
        d.CreatedAt);

    public static ControlledDocumentDetailModel ToDetail(ControlledDocument d) => new(
        d.Id,
        d.DocumentKey,
        d.Title,
        d.DocumentType.ToWire(),
        d.Description,
        d.Tags,
        d.CompanyId,
        d.OwnerCompanyId,
        d.CollectionInstanceId,
        d.CollectionPath,
        d.CanonicalId,
        d.Controlled,
        d.EffectiveDate,
        d.ReviewDate,
        d.ExpiryDate,
        d.CurrentVersionId,
        d.CurrentVersionNumber,
        d.Status.ToWire(),
        ToAccessModel(d.AccessPolicy),
        d.CopiedFromDocumentId,
        d.CreatedAt,
        d.CreatedBy);

    public static DocumentVersionModel ToVersionModel(ControlledDocumentVersion v) => new(
        v.Id,
        v.DocumentId,
        v.VersionNumber,
        v.VersionStatus.ToWire(),
        v.UploadedBy,
        v.UploadedAt,
        v.ChangeSummary,
        ToContentRefModel(v.FileRef));

    public static DocumentVersionModel ToVersionModel(TemplateVersion v) => new(
        v.Id,
        v.TemplateId,
        v.VersionNumber,
        v.VersionStatus.ToWire(),
        v.UploadedBy,
        v.UploadedAt,
        v.ChangeSummary,
        ToContentRefModel(v.FileRef));

    public static TemplateListItemModel ToListItem(TemplateDocument t) => new(
        t.Id,
        t.TemplateKey,
        t.Title,
        t.CompanyId,
        t.CollectionInstanceId,
        t.CollectionPath,
        t.CurrentVersionNumber,
        t.Status.ToWire(),
        ToFlagsModel(t.TemplateFlags),
        t.CreatedAt);

    public static TemplateDetailModel ToDetail(TemplateDocument t) => new(
        t.Id,
        t.TemplateKey,
        t.Title,
        t.Description,
        t.Tags,
        t.CompanyId,
        t.OwnerCompanyId,
        t.CollectionInstanceId,
        t.CollectionPath,
        t.CanonicalId,
        t.CurrentVersionId,
        t.CurrentVersionNumber,
        t.Status.ToWire(),
        ToFlagsModel(t.TemplateFlags),
        ToAccessModel(t.AccessPolicy),
        t.CopiedFromTemplateId,
        t.CreatedAt,
        t.CreatedBy);

    public static ContentRefModel ToContentRefModel(ContentRef r) =>
        new(r.ContentId, r.FileName, r.MediaType, r.ByteSize, r.Checksum);

    public static TemplateFlagsModel ToFlagsModel(TemplateFlags f) =>
        new(f.Reusable, f.Shareable, f.CopyableOnAdopt, f.ReferenceOnly);

    public static AccessPolicyModel ToAccessModel(DocumentAccessPolicy p) => new(
        p.Source.ToWire(),
        p.Grants.Select(g => new AccessGrantModel(g.Action.ToWire(), g.TargetType.ToWire(), g.TargetId)).ToList());
}
