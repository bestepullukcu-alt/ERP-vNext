using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

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

    public DocumentAccessEvaluator(
        IFolderDocumentAccessPolicyRepository folderPolicies,
        IDocumentShareRecordRepository shares,
        IDocumentAccessPrincipalAccessor principalAccessor)
    {
        _folderPolicies = folderPolicies;
        _shares = shares;
        _principalAccessor = principalAccessor;
    }

    public DocumentPrincipal Principal => _principalAccessor.GetPrincipal();

    public async Task<bool> HasFolderActionAsync(Guid collectionInstanceId, DocumentAccessAction action, CancellationToken ct)
    {
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
        HasFolderUploadInternalAsync(collectionInstanceId, ct);

    private async Task<bool> HasFolderUploadInternalAsync(Guid collectionInstanceId, CancellationToken ct)
    {
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
        CancellationToken ct)
    {
        var tokens = Principal.GranteeTokens();
        if (tokens.Count == 0)
        {
            return Task.FromResult(false);
        }

        if (accessPolicy.Source == AccessPolicySource.Explicit)
        {
            return Task.FromResult(HasExplicitGrant(accessPolicy, action, tokens));
        }

        return HasFolderActionAsync(collectionInstanceId, action, ct);
    }

    /// <summary>Whether the current principal may see a document/template owned by <paramref name="ownerCompanyId"/>.
    /// Owner-company principals are in scope; otherwise an explicit share to one of the principal's companies is
    /// required (else 404 non-leakage).</summary>
    public async Task<bool> CanReachItemAsync(SharedItemKind kind, Guid itemId, Guid ownerCompanyId, CancellationToken ct)
    {
        if (Principal.BelongsToCompany(ownerCompanyId))
        {
            return true;
        }

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
