using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Services;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;

/// <summary>
/// MOD-0029-FU01 — Layer 2 (MOD-0029 resource AccessPolicy) evaluation. Layer 1 (central RBAC
/// <c>[HasPermission]</c>) is enforced at the controller; this service is the second, authoritative gate
/// (backend always re-checks). Document access inherits from the parent folder unless the document carries an
/// EXPLICIT override (which may only narrow, never widen across tenant/company isolation). Cross-company access
/// requires an explicit share record.
/// </summary>
public sealed class DocumentAccessEvaluator
{
    private readonly IFolderDocumentAccessPolicyRepository _folderPolicies;
    private readonly IDocumentShareRecordRepository _shares;
    private readonly IDocumentAccessPrincipalAccessor _principalAccessor;
    private readonly DocumentAccessResolver? _matrix;
    private readonly AccessMatrixOptions _options;
    private readonly IDocumentMasterRegisterRepository? _masterRegister;

    public DocumentAccessEvaluator(
        IFolderDocumentAccessPolicyRepository folderPolicies,
        IDocumentShareRecordRepository shares,
        IDocumentAccessPrincipalAccessor principalAccessor,
        DocumentAccessResolver? matrix = null,
        IOptions<AccessMatrixOptions>? options = null,
        IDocumentMasterRegisterRepository? masterRegister = null)
    {
        _folderPolicies = folderPolicies;
        _shares = shares;
        _principalAccessor = principalAccessor;
        _matrix = matrix;
        _options = options?.Value ?? new AccessMatrixOptions();
        _masterRegister = masterRegister;
    }

    public DocumentPrincipal Principal => _principalAccessor.GetPrincipal();

    /// <summary>
    /// Lifecycle is an additional authoritative gate, never a replacement for tenant/company/resource access.
    /// Ordinary users may consume only a linked EFFECTIVE register entry. Governance principals may inspect
    /// non-effective and legacy-unlinked documents. Unlinked documents deliberately fail closed for ordinary users.
    /// </summary>
    public async Task<bool> CanConsumeControlledDocumentLifecycleAsync(ControlledDocument document, CancellationToken ct)
    {
        if (Principal.HasMasterRegisterGovernanceAccess)
        {
            return true;
        }

        if (_masterRegister is null)
        {
            return false;
        }

        var entry = await _masterRegister.GetByControlledDocumentIdAsync(document.Id, ct);
        return entry is not null
            && entry.LifecycleStatus == ControlledDocumentLifecycleStatus.Effective;
    }

    public async Task<ControlledDocumentLifecycleVisibility> GetControlledDocumentLifecycleVisibilityAsync(
        ControlledDocument document,
        CancellationToken ct)
    {
        var entry = _masterRegister is null
            ? null
            : await _masterRegister.GetByControlledDocumentIdAsync(document.Id, ct);
        return new ControlledDocumentLifecycleVisibility(
            entry?.LifecycleStatus.ToString(),
            entry?.LifecycleStatus == ControlledDocumentLifecycleStatus.Effective,
            Principal.HasMasterRegisterGovernanceAccess);
    }

    public async Task<bool> HasFolderActionAsync(Guid collectionInstanceId, DocumentAccessAction action, CancellationToken ct)
    {
        if (Principal.HasAdministrativeDocumentAccess)
        {
            return true;
        }

        var matrixDecision = await ResolveMatrixDecisionAsync(
            DocumentAccessTargetType.CollectionInstance,
            collectionInstanceId,
            ToMatrixAction(action),
            ct);
        if (matrixDecision != DocumentAccessDecision.NoDecision)
        {
            return matrixDecision == DocumentAccessDecision.Allow;
        }

        var tokens = Principal.GranteeTokens();
        if (tokens.Count == 0)
        {
            return false;
        }

        var policies = await _folderPolicies.GetByCollectionInstanceAsync(collectionInstanceId, ct);
        foreach (var policy in policies)
        {
            if (tokens.Contains(GranteeToken(policy.TargetType, policy.TargetId)) && Allows(policy.FolderPermissions, action))
            {
                return true;
            }
        }

        return false;
    }

    public Task<bool> HasFolderUploadAsync(Guid collectionInstanceId, CancellationToken ct) =>
        HasFolderCreateDocumentAsync(collectionInstanceId, ct);

    public Task<bool> HasFolderCreateDocumentAsync(Guid collectionInstanceId, CancellationToken ct) =>
        HasFolderMatrixActionAsync(collectionInstanceId, DocumentAccessMatrixAction.CreateDocument, LegacyFolderUploadAsync, ct);

    public Task<bool> HasFolderCreateTemplateAsync(Guid collectionInstanceId, CancellationToken ct) =>
        HasFolderMatrixActionAsync(collectionInstanceId, DocumentAccessMatrixAction.CreateTemplate, LegacyFolderUploadAsync, ct);

    private async Task<bool> HasFolderMatrixActionAsync(
        Guid collectionInstanceId,
        DocumentAccessMatrixAction action,
        Func<Guid, CancellationToken, Task<bool>> legacyFallback,
        CancellationToken ct)
    {
        if (Principal.HasAdministrativeDocumentAccess)
        {
            return true;
        }

        var matrixDecision = await ResolveMatrixDecisionAsync(
            DocumentAccessTargetType.CollectionInstance,
            collectionInstanceId,
            action,
            ct);
        if (matrixDecision != DocumentAccessDecision.NoDecision)
        {
            return matrixDecision == DocumentAccessDecision.Allow;
        }

        return await legacyFallback(collectionInstanceId, ct);
    }

    private async Task<bool> LegacyFolderUploadAsync(Guid collectionInstanceId, CancellationToken ct)
    {
        if (Principal.HasAdministrativeDocumentAccess)
        {
            return true;
        }

        var tokens = Principal.GranteeTokens();
        if (tokens.Count == 0)
        {
            return false;
        }

        var policies = await _folderPolicies.GetByCollectionInstanceAsync(collectionInstanceId, ct);
        return policies.Any(p =>
            tokens.Contains(GranteeToken(p.TargetType, p.TargetId)) && p.FolderPermissions.CanUploadDocument);
    }

    /// <summary>Effective document access: an EXPLICIT policy is authoritative for that document; an INHERITED
    /// policy falls back to the parent folder permission for the mapped action.</summary>
    public Task<bool> HasDocumentActionAsync(
        DocumentAccessPolicy accessPolicy,
        Guid collectionInstanceId,
        DocumentAccessAction action,
        CancellationToken ct) =>
        HasDocumentActionAsync(accessPolicy, collectionInstanceId, null, action, ct);

    public async Task<bool> HasDocumentActionAsync(
        DocumentAccessPolicy accessPolicy,
        Guid collectionInstanceId,
        Guid? documentId,
        DocumentAccessAction action,
        CancellationToken ct)
    {
        if (Principal.HasAdministrativeDocumentAccess)
        {
            return true;
        }

        var matrixDecision = documentId is { } id && id != Guid.Empty
            ? await ResolveMatrixDecisionAsync(DocumentAccessTargetType.ControlledDocument, id, ToMatrixAction(action), ct)
            : await ResolveMatrixDecisionAsync(DocumentAccessTargetType.CollectionInstance, collectionInstanceId, ToMatrixAction(action), ct);
        if (matrixDecision != DocumentAccessDecision.NoDecision)
        {
            return matrixDecision == DocumentAccessDecision.Allow;
        }

        var tokens = Principal.GranteeTokens();
        if (tokens.Count == 0)
        {
            return false;
        }

        if (accessPolicy.Source == AccessPolicySource.Explicit)
        {
            return HasExplicitGrant(accessPolicy, action, tokens);
        }

        return await HasFolderActionAsync(collectionInstanceId, action, ct);
    }

    public async Task<bool> HasControlledDocumentMatrixActionAsync(
        ControlledDocument document,
        DocumentAccessMatrixAction action,
        DocumentAccessAction? legacyFallbackAction,
        CancellationToken ct)
    {
        if (Principal.HasAdministrativeDocumentAccess)
        {
            return true;
        }

        var matrixDecision = await ResolveMatrixDecisionAsync(
            DocumentAccessTargetType.ControlledDocument,
            document.Id,
            action,
            ct);
        if (matrixDecision != DocumentAccessDecision.NoDecision)
        {
            return matrixDecision == DocumentAccessDecision.Allow;
        }

        return legacyFallbackAction is { } legacy
            ? await HasDocumentActionAsync(document.AccessPolicy, document.CollectionInstanceId, legacy, ct)
            : false;
    }

    public async Task<bool> HasControlledDocumentActionOrOwnerDefaultAsync(
        ControlledDocument document,
        DocumentAccessMatrixAction action,
        DocumentAccessAction? legacyFallbackAction,
        CancellationToken ct)
    {
        if (Principal.HasAdministrativeDocumentAccess)
        {
            return true;
        }

        var matrixDecision = await ResolveMatrixDecisionAsync(
            DocumentAccessTargetType.ControlledDocument,
            document.Id,
            action,
            ct);
        if (matrixDecision != DocumentAccessDecision.NoDecision)
        {
            return matrixDecision == DocumentAccessDecision.Allow;
        }

        return Principal.BelongsToCompany(document.OwnerCompanyId)
            || (legacyFallbackAction is { } legacy && await HasDocumentActionAsync(document.AccessPolicy, document.CollectionInstanceId, legacy, ct));
    }

    public async Task<bool> HasTemplateDocumentMatrixActionAsync(
        TemplateDocument template,
        DocumentAccessMatrixAction action,
        DocumentAccessAction? legacyFallbackAction,
        CancellationToken ct)
    {
        if (Principal.HasAdministrativeDocumentAccess)
        {
            return true;
        }

        var matrixDecision = await ResolveMatrixDecisionAsync(
            DocumentAccessTargetType.TemplateDocument,
            template.Id,
            action,
            ct);
        if (matrixDecision != DocumentAccessDecision.NoDecision)
        {
            return matrixDecision == DocumentAccessDecision.Allow;
        }

        return template.CollectionInstanceId is { } folderId && legacyFallbackAction is { } legacy
            ? await HasDocumentActionAsync(template.AccessPolicy, folderId, legacy, ct)
            : false;
    }

    public async Task<bool> HasTemplateDocumentActionOrOwnerDefaultAsync(
        TemplateDocument template,
        DocumentAccessMatrixAction action,
        DocumentAccessAction? legacyFallbackAction,
        CancellationToken ct)
    {
        if (Principal.HasAdministrativeDocumentAccess)
        {
            return true;
        }

        var matrixDecision = await ResolveMatrixDecisionAsync(
            DocumentAccessTargetType.TemplateDocument,
            template.Id,
            action,
            ct);
        if (matrixDecision != DocumentAccessDecision.NoDecision)
        {
            return matrixDecision == DocumentAccessDecision.Allow;
        }

        return Principal.BelongsToCompany(template.OwnerCompanyId)
            || (template.CollectionInstanceId is { } folderId
                && legacyFallbackAction is { } legacy
                && await HasDocumentActionAsync(template.AccessPolicy, folderId, legacy, ct));
    }

    /// <summary>Whether the current principal may see a document/template owned by <paramref name="ownerCompanyId"/>.
    /// Owner-company principals are in scope; otherwise an explicit share to one of the principal's companies is
    /// required (else 404 non-leakage).</summary>
    public async Task<bool> CanReachItemAsync(SharedItemKind kind, Guid itemId, Guid ownerCompanyId, CancellationToken ct)
    {
        if (Principal.HasAdministrativeDocumentAccess)
        {
            return true;
        }

        if (Principal.BelongsToCompany(ownerCompanyId))
        {
            return true;
        }

        var shares = await _shares.GetByItemAsync(kind, itemId, ct);
        return shares.Any(s => Principal.BelongsToCompany(s.TargetCompanyId));
    }

    /// <summary>Reachability for a controlled document: an explicit item-level matrix decision is authoritative.
    /// Without one, the transitional compatibility surface remains owner company / share / folder-view.</summary>
    public Task<bool> CanReachDocumentAsync(ControlledDocument document, CancellationToken ct) =>
        CanViewControlledDocumentAsync(document, null, ct);

    public async Task<bool> CanReadControlledDocumentAsync(ControlledDocument document, CancellationToken ct) =>
        await CanConsumeControlledDocumentLifecycleAsync(document, ct)
        && await CanViewControlledDocumentAsync(document, null, ct);

    public async Task<bool> CanViewControlledDocumentAsync(
        ControlledDocument document,
        IReadOnlySet<Guid>? sharedItemIds,
        CancellationToken ct)
    {
        if (Principal.HasAdministrativeDocumentAccess)
        {
            return true;
        }

        var matrixDecision = await ResolveMatrixDecisionAsync(
            DocumentAccessTargetType.ControlledDocument,
            document.Id,
            DocumentAccessMatrixAction.View,
            ct);
        if (matrixDecision != DocumentAccessDecision.NoDecision)
        {
            return matrixDecision == DocumentAccessDecision.Allow;
        }

        if (IsCompatibilityClaimlessViewer())
        {
            return true;
        }

        return Principal.BelongsToCompany(document.OwnerCompanyId)
            || (sharedItemIds?.Contains(document.Id) == true)
            || await HasFolderActionAsync(document.CollectionInstanceId, DocumentAccessAction.View, ct)
            || await HasSharedItemAsync(SharedItemKind.ControlledDocument, document.Id, ct);
    }

    /// <summary>Reachability for a template: item-level deny closes visibility before folder/share/company fallbacks.</summary>
    public Task<bool> CanReachTemplateAsync(TemplateDocument template, CancellationToken ct) =>
        CanViewTemplateDocumentAsync(template, null, ct);

    public async Task<bool> CanViewTemplateDocumentAsync(
        TemplateDocument template,
        IReadOnlySet<Guid>? sharedItemIds,
        CancellationToken ct)
    {
        if (Principal.HasAdministrativeDocumentAccess)
        {
            return true;
        }

        var matrixDecision = await ResolveMatrixDecisionAsync(
            DocumentAccessTargetType.TemplateDocument,
            template.Id,
            DocumentAccessMatrixAction.View,
            ct);
        if (matrixDecision != DocumentAccessDecision.NoDecision)
        {
            return matrixDecision == DocumentAccessDecision.Allow;
        }

        if (IsCompatibilityClaimlessViewer())
        {
            return true;
        }

        return Principal.BelongsToCompany(template.OwnerCompanyId)
            || (sharedItemIds?.Contains(template.Id) == true)
            || (template.CollectionInstanceId is { } folderId && await HasFolderActionAsync(folderId, DocumentAccessAction.View, ct))
            || await HasSharedItemAsync(SharedItemKind.Template, template.Id, ct);
    }

    /// <summary>Whether the principal may view documents in a folder: owner-company OR a folder-view grant.</summary>
    public async Task<bool> CanViewFolderAsync(Guid collectionInstanceId, Guid companyId, CancellationToken ct)
    {
        if (Principal.HasAdministrativeDocumentAccess)
        {
            return true;
        }

        var matrixDecision = await ResolveMatrixDecisionAsync(
            DocumentAccessTargetType.CollectionInstance,
            collectionInstanceId,
            DocumentAccessMatrixAction.View,
            ct);
        if (matrixDecision != DocumentAccessDecision.NoDecision)
        {
            return matrixDecision == DocumentAccessDecision.Allow;
        }

        if (IsCompatibilityClaimlessViewer())
        {
            return true;
        }

        if (_options.Mode == AccessMatrixEnforcementMode.Compatibility
            && _options.OwnerCompanyTransitionalView
            && Principal.BelongsToCompany(companyId))
        {
            return true;
        }

        return await HasFolderActionAsync(collectionInstanceId, DocumentAccessAction.View, ct);
    }

    // MOD-0029-FU04 — Compatibility (Deny-only) rollout: a principal with NO company claim (the current seed-admin /
    // unwired-claim token shape) is treated as a tenant-wide viewer so existing UX is not broken before company
    // claims are issued. Callers apply admin bypass and explicit-Deny precedence BEFORE this; claimed principals keep
    // the owner-company / share / folder-grant model, so cross-company isolation is preserved for properly-claimed
    // users. Affects VISIBILITY (View/reach) only — action gates (download/edit/upload/…) still require a grant/policy.
    private bool IsCompatibilityClaimlessViewer() =>
        _options.Mode == AccessMatrixEnforcementMode.Compatibility && Principal.CompanyIds.Count == 0;

    /// <summary>MOD-0029-FU04 — read/list visibility for the Instantiate Structures <c>collection-instances</c> grid.
    /// That grid had NO row-level access filtering before the matrix rollout, so in <see cref="AccessMatrixEnforcementMode.Compatibility"/>
    /// (default) it must stay visible unless an EXPLICIT matrix <c>View</c> Deny exists — the one new capability. Tightening
    /// it by owner-company membership / folder grants (as the Controlled-Documents folder gate does) would empty the list
    /// for principals whose token does not yet carry company claims, breaking existing UX. <see cref="AccessMatrixEnforcementMode.Enforce"/>
    /// delegates to the full folder-view gate.</summary>
    public async Task<bool> CanListFolderAsync(Guid collectionInstanceId, Guid companyId, CancellationToken ct)
    {
        if (Principal.HasAdministrativeDocumentAccess)
        {
            return true;
        }

        var matrixDecision = await ResolveMatrixDecisionAsync(
            DocumentAccessTargetType.CollectionInstance,
            collectionInstanceId,
            DocumentAccessMatrixAction.View,
            ct);
        if (matrixDecision != DocumentAccessDecision.NoDecision)
        {
            return matrixDecision == DocumentAccessDecision.Allow;
        }

        // No explicit policy: Compatibility rollout must not tighten this previously-unfiltered list.
        if (_options.Mode == AccessMatrixEnforcementMode.Compatibility)
        {
            return true;
        }

        return await CanViewFolderAsync(collectionInstanceId, companyId, ct);
    }

    private async Task<bool> CanReachMatrixTargetAsync(DocumentAccessTargetType targetType, Guid targetId, Guid ownerCompanyId, CancellationToken ct)
    {
        if (Principal.HasAdministrativeDocumentAccess)
        {
            return true;
        }

        var matrixDecision = await ResolveMatrixDecisionAsync(targetType, targetId, DocumentAccessMatrixAction.View, ct);
        if (matrixDecision != DocumentAccessDecision.NoDecision)
        {
            return matrixDecision == DocumentAccessDecision.Allow;
        }

        return await CanReachItemAsync(
            targetType == DocumentAccessTargetType.TemplateDocument ? SharedItemKind.Template : SharedItemKind.ControlledDocument,
            targetId,
            ownerCompanyId,
            ct);
    }

    private async Task<bool> CanReachTemplateMatrixTargetAsync(TemplateDocument template, CancellationToken ct)
    {
        if (Principal.HasAdministrativeDocumentAccess)
        {
            return true;
        }

        var matrixDecision = await ResolveMatrixDecisionAsync(
            DocumentAccessTargetType.TemplateDocument,
            template.Id,
            DocumentAccessMatrixAction.View,
            ct);
        if (matrixDecision != DocumentAccessDecision.NoDecision)
        {
            return matrixDecision == DocumentAccessDecision.Allow;
        }

        return await CanReachItemAsync(SharedItemKind.Template, template.Id, template.OwnerCompanyId, ct);
    }

    private async Task<bool> HasSharedItemAsync(SharedItemKind kind, Guid itemId, CancellationToken ct)
    {
        var shares = await _shares.GetByItemAsync(kind, itemId, ct);
        return shares.Any(s => Principal.BelongsToCompany(s.TargetCompanyId));
    }

    private static bool HasExplicitGrant(DocumentAccessPolicy policy, DocumentAccessAction action, IReadOnlySet<string> tokens) =>
        policy.Grants.Any(g => g.Action == action && tokens.Contains(GranteeToken(g.TargetType, g.TargetId)));

    private static bool Allows(FolderPermissionSet set, DocumentAccessAction action) => action switch
    {
        DocumentAccessAction.View => set.CanViewFolderDocuments,
        DocumentAccessAction.Download => set.CanViewFolderDocuments,
        DocumentAccessAction.Edit => set.CanEditFolderDocuments,
        DocumentAccessAction.Version => set.CanUploadNewVersion,
        DocumentAccessAction.Share => set.CanShareFolderDocuments,
        DocumentAccessAction.ManageAccess => set.CanManageFolderDocumentAccess,
        _ => false
    };

    private static DocumentAccessMatrixAction ToMatrixAction(DocumentAccessAction action) => action switch
    {
        DocumentAccessAction.View => DocumentAccessMatrixAction.View,
        DocumentAccessAction.Download => DocumentAccessMatrixAction.Download,
        DocumentAccessAction.Edit => DocumentAccessMatrixAction.EditMetadata,
        DocumentAccessAction.Version => DocumentAccessMatrixAction.UploadVersion,
        DocumentAccessAction.Share => DocumentAccessMatrixAction.Share,
        DocumentAccessAction.ManageAccess => DocumentAccessMatrixAction.ManageAccess,
        _ => DocumentAccessMatrixAction.View
    };

    private Task<DocumentAccessDecision> ResolveMatrixDecisionAsync(
        DocumentAccessTargetType targetType,
        Guid targetId,
        DocumentAccessMatrixAction action,
        CancellationToken ct) =>
        _matrix is null
            ? Task.FromResult(DocumentAccessDecision.NoDecision)
            : _matrix.ResolveCurrentDecisionAsync(targetType, targetId, action, ct);

    public static string GranteeToken(AccessTargetType targetType, string targetId) => targetType switch
    {
        AccessTargetType.User => $"user:{targetId.Trim()}",
        AccessTargetType.Role => $"role:{targetId.Trim()}",
        AccessTargetType.Company => $"company:{targetId.Trim()}",
        AccessTargetType.Plant => $"plant:{targetId.Trim()}",
        AccessTargetType.BusinessUnit => $"business-unit:{targetId.Trim()}",
        _ => $"user:{targetId.Trim()}"
    };
}

public sealed record ControlledDocumentLifecycleVisibility(
    string? MasterRegisterLifecycleStatus,
    bool IsOfficiallyEffective,
    bool CanViewNonEffective);
