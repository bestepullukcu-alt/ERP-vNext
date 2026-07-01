namespace Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;

/// <summary>
/// MOD-0029-FU01 — resolves the current principal's Layer 2 grantee identities (user / roles / companies) so
/// the <see cref="DocumentAccessEvaluator"/> can match them against folder/document AccessPolicy grants.
/// First version supports user / role / company grantee kinds (extensible to position / group later).
/// </summary>
public interface IDocumentAccessPrincipalAccessor
{
    DocumentPrincipal GetPrincipal();
}

public sealed record DocumentPrincipal(
    Guid UserId,
    IReadOnlyCollection<string> RoleIds,
    IReadOnlyCollection<Guid> CompanyIds,
    bool IsPlatformAdmin = false,
    bool IsTenantAdmin = false)
{
    public static readonly DocumentPrincipal Empty = new(Guid.Empty, [], []);

    public bool HasAdministrativeDocumentAccess => IsPlatformAdmin || IsTenantAdmin;

    /// <summary>Builds the set of typed grantee tokens (<c>user:{id}</c>, <c>role:{id}</c>, <c>company:{id}</c>)
    /// used for AccessPolicy matching.</summary>
    public IReadOnlySet<string> GranteeTokens()
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (UserId != Guid.Empty)
        {
            tokens.Add($"user:{UserId:D}");
        }

        foreach (var role in RoleIds)
        {
            if (!string.IsNullOrWhiteSpace(role))
            {
                tokens.Add($"role:{role.Trim()}");
            }
        }

        foreach (var company in CompanyIds)
        {
            if (company != Guid.Empty)
            {
                tokens.Add($"company:{company:D}");
            }
        }

        return tokens;
    }

    public bool BelongsToCompany(Guid companyId) => companyId != Guid.Empty && CompanyIds.Contains(companyId);
}
