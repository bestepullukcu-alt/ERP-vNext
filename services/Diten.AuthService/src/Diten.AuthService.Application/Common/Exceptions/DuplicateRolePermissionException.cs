namespace Diten.AuthService.Application.Common.Exceptions;

/// <summary>
/// WP-ROLES-CLOSE-01 — the role already holds this permission: the unique (role, permission, tenant) index refused the
/// insert. Thrown by <c>RolePermissionRepository.AssignAsync</c> instead of the driver's own exception, so a caller in
/// the Application layer can answer it (the person path answers 409 <c>ROLE_PERMISSION_ALREADY_GRANTED</c>) without
/// knowing the store. It is what a double click, or two administrators at once, look like from the second request.
/// </summary>
public sealed class DuplicateRolePermissionException : Exception
{
    public DuplicateRolePermissionException(Guid roleId, Guid permissionId, Exception inner)
        : base($"Role {roleId} already holds permission {permissionId}.", inner)
    {
        RoleId = roleId;
        PermissionId = permissionId;
    }

    public Guid RoleId { get; }
    public Guid PermissionId { get; }
}
