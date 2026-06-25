using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;

public sealed class FolderSharePlanner : IFolderSharePlanner
{
    private readonly ICollectionInstanceReferenceReader _reader;
    private readonly ITemplateDocumentRepository _templates;
    private readonly ILegalEntityReferenceValidator _legalEntityValidator;
    private readonly DocumentAccessEvaluator _access;
    private readonly ITenantContext _tenantContext;

    public FolderSharePlanner(
        ICollectionInstanceReferenceReader reader,
        ITemplateDocumentRepository templates,
        ILegalEntityReferenceValidator legalEntityValidator,
        DocumentAccessEvaluator access,
        ITenantContext tenantContext)
    {
        _reader = reader;
        _templates = templates;
        _legalEntityValidator = legalEntityValidator;
        _access = access;
        _tenantContext = tenantContext;
    }

    public async Task<Response<FolderSharePlan>> PlanAsync(
        Guid sourceBranchCollectionInstanceId,
        Guid targetCompanyId,
        bool includeTemplates,
        DocumentShareMode shareMode,
        string correlationId,
        CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);

        var root = await _reader.ResolveByIdAsync(sourceBranchCollectionInstanceId, ct);
        if (root is null)
        {
            return Response<FolderSharePlan>.Fail("Not found.", 404, ControlledDocumentReasonCodes.NotFoundNonLeakage, correlationId);
        }

        if (targetCompanyId == Guid.Empty || targetCompanyId == root.CompanyId)
        {
            return Response<FolderSharePlan>.Fail("Invalid share target.", 400, ControlledDocumentReasonCodes.ValidationFailed, correlationId);
        }

        // Source share authorization (Layer 2 share on the root branch) — owner-company or folder share grant.
        if (!_access.Principal.BelongsToCompany(root.CompanyId)
            && !await _access.HasFolderActionAsync(sourceBranchCollectionInstanceId, DocumentAccessAction.Share, ct))
        {
            return Response<FolderSharePlan>.Fail("Permission denied.", 403, ControlledDocumentReasonCodes.PermissionDenied, correlationId);
        }

        // MOD-0220 fail-closed.
        var targetValidation = await _legalEntityValidator.ValidateAsync(targetCompanyId, ct);
        if (!targetValidation.IsSuccessful)
        {
            return Response<FolderSharePlan>.Fail("Not found.", 404, ControlledDocumentReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var branch = await _reader.GetBranchAsync(sourceBranchCollectionInstanceId, ct);
        var folders = branch
            .Select(n => new FolderShareNode(n.CollectionInstanceId, n.CanonicalId, n.FullPath))
            .ToList();

        var included = new List<TemplateDocument>();
        var skipped = new List<FolderShareSkippedTemplate>();
        var warnings = new List<string>();

        if (includeTemplates)
        {
            foreach (var node in branch)
            {
                var templates = await _templates.GetByCollectionInstanceAsync(node.CollectionInstanceId, ct);
                foreach (var template in templates.Where(t => t.OwnerCompanyId == root.CompanyId))
                {
                    if (!template.TemplateFlags.Shareable)
                    {
                        skipped.Add(new FolderShareSkippedTemplate(template.Id, template.TemplateKey, ControlledDocumentReasonCodes.ValidationFailed, "Template is not shareable."));
                    }
                    else
                    {
                        included.Add(template);
                    }
                }
            }

            if (included.Count == 0 && skipped.Count == 0)
            {
                warnings.Add("No associated templates were found under the selected branch.");
            }
        }

        var plan = new FolderSharePlan(
            Guid.NewGuid(),
            tenantId,
            root.CompanyId,
            targetCompanyId,
            sourceBranchCollectionInstanceId,
            includeTemplates,
            shareMode,
            Blocked: false,
            Errors: [],
            Warnings: warnings,
            Folders: folders,
            IncludedTemplates: included,
            SkippedTemplates: skipped);

        return Response<FolderSharePlan>.Success(plan, correlationId: correlationId);
    }
}
