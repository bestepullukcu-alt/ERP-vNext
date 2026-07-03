namespace Diten.AuthService.Application.Common.Interfaces;

/// <summary>
/// FEAT-AUDIT-RBAC — the id of the authenticated user making the current request (the "actor" for audit),
/// read from the principal's NameIdentifier/sub claim. Null when unauthenticated or outside a request scope.
/// UX/audit-only: backend authorization ([HasPermission]) remains the enforcement boundary.
/// </summary>
public interface ICurrentUserAccessor
{
    Guid? UserId { get; }
}
