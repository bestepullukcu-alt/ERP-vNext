using System.Security.Claims;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using Microsoft.AspNetCore.Http;

namespace Diten.Platform.Infrastructure.Services.DocumentManagement;

/// <summary>
/// MOD-0029-FU01 — resolves the current principal's Layer 2 grantee identities (user / roles / companies) from
/// the JWT claims for AccessPolicy evaluation. First version supports user / role / company grantee kinds.
/// </summary>
public sealed class DocumentAccessPrincipalAccessor : IDocumentAccessPrincipalAccessor
{
    private static readonly string[] RoleClaimTypes = [ClaimTypes.Role, "role", "roles"];
    private static readonly string[] CompanyClaimTypes =
        ["companyId", "company_id", "legalEntityId", "legal_entity_id", "companyIds", "company_ids"];

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICurrentUserContext _currentUser;

    public DocumentAccessPrincipalAccessor(IHttpContextAccessor httpContextAccessor, ICurrentUserContext currentUser)
    {
        _httpContextAccessor = httpContextAccessor;
        _currentUser = currentUser;
    }

    public DocumentPrincipal GetPrincipal()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user is null)
        {
            return DocumentPrincipal.Empty;
        }

        var roles = RoleClaimTypes
            .SelectMany(t => user.FindAll(t))
            .Select(c => c.Value.Trim())
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var companies = CompanyClaimTypes
            .SelectMany(t => user.FindAll(t))
            .SelectMany(c => c.Value.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(v => Guid.TryParse(v, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .Distinct()
            .ToList();

        var actorType = user.FindFirst("actor_type")?.Value?.Trim();
        var isPlatformAdmin = string.Equals(actorType, "platform_admin", StringComparison.OrdinalIgnoreCase);
        // "Admin" is the tenant administrator role provisioned for every tenant (RoleProvisioningService creates
        // "Admin" + "Viewer"); recognize it alongside the explicit tenant_admin/TenantAdmin names so tenant admins
        // get Layer-2 administrative document access without a separately-named role.
        var isTenantAdmin = string.Equals(actorType, "tenant_admin", StringComparison.OrdinalIgnoreCase)
            || roles.Any(r => string.Equals(r, "tenant_admin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(r, "TenantAdmin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase));

        return new DocumentPrincipal(_currentUser.UserId, roles, companies, isPlatformAdmin, isTenantAdmin);
    }
}
