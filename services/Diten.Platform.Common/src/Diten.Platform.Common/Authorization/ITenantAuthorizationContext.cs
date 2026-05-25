namespace Diten.Platform.Common.Authorization;

public interface ITenantAuthorizationContext
{
    Guid TenantId { get; }

    Guid UserId { get; }

    string? ActorType { get; }

    bool IsAuthenticated { get; }

    bool IsPlatformAdmin { get; }

    IReadOnlyList<string> PermissionKeys { get; }

    IReadOnlyList<Guid> RoleIds { get; }

    IReadOnlyList<string> RoleNames { get; }

    IReadOnlyList<Guid> OrgUnitIds { get; }

    IReadOnlyList<Guid> PositionIds { get; }

    Guid? LegalEntityId { get; }

    string? Country { get; }

    IReadOnlyList<Guid> ManagerChain { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);
}
