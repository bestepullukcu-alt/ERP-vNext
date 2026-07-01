using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Services;

/// <summary>
/// MOD-0029-FU04 — bridges the existing FU01 <see cref="Domain.Entities.DocumentManagement.FolderDocumentAccessPolicy"/>
/// grants into the new matrix action set so transitional rollout keeps existing folder grants working. Read-only;
/// it never mutates the FU01 policy collection. Mapping (per pack §19):
/// <list type="bullet">
/// <item>CanViewFolderDocuments → View, Download (Download maps to folder view per existing FU01 evaluator)</item>
/// <item>CanUploadDocument → CreateDocument, CreateTemplate</item>
/// <item>CanEditFolderDocuments → EditMetadata</item>
/// <item>CanUploadNewVersion → UploadVersion</item>
/// <item>CanShareFolderDocuments → Share</item>
/// <item>CanManageFolderDocumentAccess → ManageAccess</item>
/// </list>
/// </summary>
public sealed class DocumentAccessCompatibilityAdapter
{
    private readonly IFolderDocumentAccessPolicyRepository _folderPolicies;

    // Per-request (Scoped) memoization: a collection-instances list resolves the same parent/ancestor folders across
    // many rows; folder grants are read-only reference data within a request.
    private readonly Dictionary<Guid, IReadOnlyList<Domain.Entities.DocumentManagement.FolderDocumentAccessPolicy>> _folderPolicyCache = new();

    public DocumentAccessCompatibilityAdapter(IFolderDocumentAccessPolicyRepository folderPolicies)
    {
        _folderPolicies = folderPolicies;
    }

    /// <summary>Union of matrix actions granted to the supplied grantee tokens by existing folder policies on the folder.</summary>
    public async Task<IReadOnlySet<DocumentAccessMatrixAction>> FolderActionsAsync(
        Guid collectionInstanceId,
        IReadOnlySet<string> granteeTokens,
        CancellationToken ct)
    {
        var actions = new HashSet<DocumentAccessMatrixAction>();
        if (granteeTokens.Count == 0)
        {
            return actions;
        }

        if (!_folderPolicyCache.TryGetValue(collectionInstanceId, out var policies))
        {
            policies = await _folderPolicies.GetByCollectionInstanceAsync(collectionInstanceId, ct);
            _folderPolicyCache[collectionInstanceId] = policies;
        }

        foreach (var policy in policies)
        {
            if (!granteeTokens.Contains(DocumentAccessEvaluator.GranteeToken(policy.TargetType, policy.TargetId)))
            {
                continue;
            }

            var set = policy.FolderPermissions;
            if (set.CanViewFolderDocuments) { actions.Add(DocumentAccessMatrixAction.View); actions.Add(DocumentAccessMatrixAction.Download); }
            if (set.CanUploadDocument) { actions.Add(DocumentAccessMatrixAction.CreateDocument); actions.Add(DocumentAccessMatrixAction.CreateTemplate); }
            if (set.CanEditFolderDocuments) { actions.Add(DocumentAccessMatrixAction.EditMetadata); }
            if (set.CanUploadNewVersion) { actions.Add(DocumentAccessMatrixAction.UploadVersion); }
            if (set.CanShareFolderDocuments) { actions.Add(DocumentAccessMatrixAction.Share); }
            if (set.CanManageFolderDocumentAccess) { actions.Add(DocumentAccessMatrixAction.ManageAccess); }
        }

        return actions;
    }
}
