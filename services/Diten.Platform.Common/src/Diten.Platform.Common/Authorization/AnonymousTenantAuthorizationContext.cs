namespace Diten.Platform.Common.Authorization;

public sealed class AnonymousTenantAuthorizationContext : ITenantAuthorizationContext
{
    private static readonly IReadOnlyList<Guid> EmptyGuidList = Array.Empty<Guid>();
    private static readonly IReadOnlyList<string> EmptyStringList = Array.Empty<string>();

    public Guid TenantId => Guid.Empty;

    public Guid UserId => Guid.Empty;

    public string? ActorType => null;

    public bool IsAuthenticated => false;

    public bool IsPlatformAdmin => false;

    public IReadOnlyList<string> PermissionKeys => EmptyStringList;

    public IReadOnlyList<Guid> RoleIds => EmptyGuidList;

    public IReadOnlyList<string> RoleNames => EmptyStringList;

    public IReadOnlyList<Guid> OrgUnitIds => EmptyGuidList;

    public IReadOnlyList<Guid> PositionIds => EmptyGuidList;

    public Guid? LegalEntityId => null;

    public string? Country => null;

    public IReadOnlyList<Guid> ManagerChain => EmptyGuidList;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return cancellationToken.IsCancellationRequested
            ? Task.FromCanceled(cancellationToken)
            : Task.CompletedTask;
    }
}
