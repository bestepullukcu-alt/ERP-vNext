using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementCorporateCollectionInstances;

public sealed class CorporateCollectionFolderAccessEvaluator
{
    private readonly IDocumentAccessPolicyRepository _policies;
    private readonly IDocumentAccessPrincipalAccessor _principals;

    public CorporateCollectionFolderAccessEvaluator(
        IDocumentAccessPolicyRepository policies,
        IDocumentAccessPrincipalAccessor principals)
    {
        _policies = policies;
        _principals = principals;
    }

    public async Task<bool> HasExplicitGrantAsync(Guid folderId, DocumentAccessMatrixAction action, CancellationToken ct)
    {
        var principal = _principals.GetPrincipal();
        if (principal.HasAdministrativeDocumentAccess)
        {
            return true;
        }

        var policies = await _policies.GetByTargetsAsync(
            [(DocumentAccessTargetType.CollectionInstance, folderId.ToString("D"))], ct);
        var matching = policies.Where(x =>
            x.Status == DocumentAccessPolicyStatus.Active
            && x.Actions.Contains(action)
            && ((x.PrincipalType == DocumentAccessPrincipalType.User
                    && string.Equals(x.PrincipalId, principal.UserId.ToString("D"), StringComparison.OrdinalIgnoreCase))
                || (x.PrincipalType == DocumentAccessPrincipalType.Role
                    && principal.RoleIds.Contains(x.PrincipalId, StringComparer.OrdinalIgnoreCase))))
            .ToList();

        return matching.Count > 0
            && !matching.Any(x => x.Effect == DocumentAccessEffect.Deny)
            && matching.Any(x => x.Effect == DocumentAccessEffect.Allow);
    }
}
