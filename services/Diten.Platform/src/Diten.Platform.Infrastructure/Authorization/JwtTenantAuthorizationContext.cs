using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Diten.Platform.Common.Authorization;
using Microsoft.AspNetCore.Http;

namespace Diten.Platform.Infrastructure.Authorization;

public sealed class JwtTenantAuthorizationContext : ITenantAuthorizationContext
{
    private static readonly IReadOnlyList<Guid> EmptyGuidList = Array.Empty<Guid>();
    private static readonly IReadOnlyList<string> EmptyStringList = Array.Empty<string>();

    private readonly IDataScopeResolver dataScopeResolver;
    private readonly object initializationLock = new();
    private Task? initializationTask;

    public JwtTenantAuthorizationContext(
        IHttpContextAccessor httpContextAccessor,
        IDataScopeResolver dataScopeResolver)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        this.dataScopeResolver = dataScopeResolver ?? throw new ArgumentNullException(nameof(dataScopeResolver));

        var user = httpContextAccessor?.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            TenantId = Guid.Empty;
            UserId = Guid.Empty;
            PermissionKeys = EmptyStringList;
            RoleIds = EmptyGuidList;
            RoleNames = EmptyStringList;
            OrgUnitIds = EmptyGuidList;
            PositionIds = EmptyGuidList;
            ManagerChain = EmptyGuidList;
            return;
        }

        IsAuthenticated = true;
        TenantId = ParseGuidClaim(user, "tenant_id");
        UserId = ParseGuidClaim(user, JwtRegisteredClaimNames.Sub, ClaimTypes.NameIdentifier);
        ActorType = FindClaimValue(user, "actor_type");
        IsPlatformAdmin = string.Equals(ActorType, "platform_admin", StringComparison.OrdinalIgnoreCase);
        PermissionKeys = FindClaimValues(user, "permission");
        RoleIds = EmptyGuidList;
        RoleNames = FindClaimValues(user, ClaimTypes.Role);
        OrgUnitIds = EmptyGuidList;
        PositionIds = EmptyGuidList;
        ManagerChain = EmptyGuidList;
    }

    public Guid TenantId { get; }

    public Guid UserId { get; }

    public string? ActorType { get; }

    public bool IsAuthenticated { get; }

    public bool IsPlatformAdmin { get; }

    public IReadOnlyList<string> PermissionKeys { get; }

    public IReadOnlyList<Guid> RoleIds { get; }

    public IReadOnlyList<string> RoleNames { get; }

    public IReadOnlyList<Guid> OrgUnitIds { get; private set; } = EmptyGuidList;

    public IReadOnlyList<Guid> PositionIds { get; private set; } = EmptyGuidList;

    public Guid? LegalEntityId { get; private set; }

    public string? Country { get; private set; }

    public IReadOnlyList<Guid> ManagerChain { get; private set; } = EmptyGuidList;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (initializationTask != null)
        {
            return initializationTask;
        }

        lock (initializationLock)
        {
            initializationTask ??= InitializeCoreAsync(cancellationToken);
            return initializationTask;
        }
    }

    private async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        if (!IsAuthenticated)
        {
            return;
        }

        try
        {
            var scopes = await dataScopeResolver.ResolveAsync(
                TenantId,
                UserId,
                moduleCode: string.Empty,
                featureCode: null,
                cancellationToken);

            HydrateDataScopes(scopes);
        }
        catch
        {
            OrgUnitIds = EmptyGuidList;
            PositionIds = EmptyGuidList;
            LegalEntityId = null;
            Country = null;
            ManagerChain = EmptyGuidList;
        }
    }

    private void HydrateDataScopes(IReadOnlyList<EntitlementDataScope>? scopes)
    {
        if (scopes is null || scopes.Count == 0)
        {
            return;
        }

        OrgUnitIds = FindScopeIds(scopes, EntitlementDataScopeKind.OrgUnit);
        PositionIds = FindScopeIds(scopes, EntitlementDataScopeKind.Position);
        var legalEntityIds = FindScopeIds(scopes, EntitlementDataScopeKind.LegalEntity);
        LegalEntityId = legalEntityIds.Count == 0 ? null : legalEntityIds[0];
        Country = scopes
            .Where(scope => scope.Kind == EntitlementDataScopeKind.Country)
            .Select(scope => FirstNonEmpty(scope.Value, scope.ScopeCode))
            .FirstOrDefault(value => value is not null);
        ManagerChain = FindScopeIds(scopes, EntitlementDataScopeKind.ManagerChain);
    }

    private static IReadOnlyList<Guid> FindScopeIds(
        IReadOnlyList<EntitlementDataScope> scopes,
        EntitlementDataScopeKind kind)
    {
        var ids = scopes
            .Where(scope => scope.Kind == kind)
            .Select(FindScopeId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToArray();

        return ids.Length == 0 ? EmptyGuidList : ids;
    }

    private static Guid? FindScopeId(EntitlementDataScope scope)
    {
        if (scope.ScopeId.HasValue)
        {
            return scope.ScopeId.Value;
        }

        var value = FirstNonEmpty(scope.Value, scope.ScopeCode);
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }

    private static Guid ParseGuidClaim(ClaimsPrincipal user, params string[] claimTypes)
    {
        var value = FindClaimValue(user, claimTypes);
        return Guid.TryParse(value, out var parsed) ? parsed : Guid.Empty;
    }

    private static string? FindClaimValue(ClaimsPrincipal user, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = user.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static IReadOnlyList<string> FindClaimValues(ClaimsPrincipal user, string claimType)
    {
        var values = user.Claims
            .Where(claim => string.Equals(claim.Type, claimType, StringComparison.OrdinalIgnoreCase))
            .Select(claim => claim.Value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();

        return values.Length == 0 ? EmptyStringList : values;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}
