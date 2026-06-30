using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Services;

/// <summary>
/// MOD-0029-FU04 — validates an access target exists in the current tenant (non-leakage) and produces a friendly
/// label. Resource-bearing targets are resolved through existing tenant-scoped read seams; cross-tenant / missing
/// resolves to "not found" so the caller returns 404 without exposing details.
/// </summary>
public sealed class DocumentAccessTargetResolver
{
    private readonly ITemplateVariantRepository _variants;
    private readonly ITemplateDocumentRepository _templateDocuments;
    private readonly IControlledDocumentRepository _controlledDocuments;
    private readonly ITemplateMasterRepository _masters;
    private readonly ICollectionInstanceReferenceReader _folders;
    private readonly ITenantContext _tenantContext;

    public DocumentAccessTargetResolver(
        ITemplateVariantRepository variants,
        ITemplateDocumentRepository templateDocuments,
        IControlledDocumentRepository controlledDocuments,
        ITemplateMasterRepository masters,
        ICollectionInstanceReferenceReader folders,
        ITenantContext tenantContext)
    {
        _variants = variants;
        _templateDocuments = templateDocuments;
        _controlledDocuments = controlledDocuments;
        _masters = masters;
        _folders = folders;
        _tenantContext = tenantContext;
    }

    /// <summary><paramref name="CompanyId"/> is the target's owning company when it carries one (folder / document /
    /// template / master / variant); it lets the edit UI pre-select the company-scoped target picker.</summary>
    public sealed record TargetResolution(bool Exists, string? Label, Guid? CompanyId = null);

    public async Task<TargetResolution> ResolveAsync(DocumentAccessTargetType targetType, string targetId, CancellationToken ct)
    {
        var trimmed = (targetId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return new TargetResolution(false, null);
        }

        switch (targetType)
        {
            case DocumentAccessTargetType.Tenant:
                return new TargetResolution(
                    Guid.TryParse(trimmed, out var tid) && tid == _tenantContext.TenantId,
                    "Tenant");

            case DocumentAccessTargetType.Company:
                // The Company target id IS the company; surface it so the edit UI can pre-select the company picker.
                return new TargetResolution(Guid.TryParse(trimmed, out var companyId), trimmed, companyId == Guid.Empty ? null : companyId);

            case DocumentAccessTargetType.CollectionDefinition:
                // No dedicated same-tenant reference seam in this FU; accept a well-formed id (label is the id).
                return new TargetResolution(Guid.TryParse(trimmed, out _), trimmed);

            case DocumentAccessTargetType.CollectionInstance when Guid.TryParse(trimmed, out var folderId):
            {
                var folder = await _folders.ResolveByIdAsync(folderId, ct);
                return new TargetResolution(folder is not null, folder?.FullPath, folder?.CompanyId);
            }
            case DocumentAccessTargetType.TemplateDocument when Guid.TryParse(trimmed, out var templateId):
            {
                var td = await _templateDocuments.GetByIdAsync(templateId, ct);
                return new TargetResolution(td is not null, td?.Title, td?.CompanyId);
            }
            case DocumentAccessTargetType.ControlledDocument when Guid.TryParse(trimmed, out var docId):
            {
                var cd = await _controlledDocuments.GetByIdAsync(docId, ct);
                return new TargetResolution(cd is not null, cd?.Title, cd?.CompanyId);
            }
            case DocumentAccessTargetType.TemplateMaster when Guid.TryParse(trimmed, out var masterId):
            {
                var m = await _masters.GetByIdAsync(masterId, ct);
                return new TargetResolution(m is not null, m is null ? null : $"{m.MasterCode} — {m.TemplateName}", m?.OwnerCompanyId);
            }
            case DocumentAccessTargetType.TemplateVariant when Guid.TryParse(trimmed, out var variantId):
            {
                var v = await _variants.GetByIdAsync(variantId, ct);
                return new TargetResolution(v is not null, v is null ? null : $"{v.VariantCode} — {v.VariantName}", v?.OwnerCompanyId);
            }
            default:
                return new TargetResolution(false, null);
        }
    }
}
