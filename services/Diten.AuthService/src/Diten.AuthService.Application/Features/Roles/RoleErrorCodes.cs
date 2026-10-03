using Diten.AuthService.Application.Common;

namespace Diten.AuthService.Application.Features.Roles;

/// <summary>
/// WP-ROLES-CLOSE-01 — the stable refusal codes of the Roles, Role Permissions and User Roles screens, in one place.
/// AuthService is a headless API with no reliable UI culture: it keeps the English sentence in <c>errors</c> (logs,
/// other consumers) and adds the code in <c>errorCodes</c>; each screen maps the code to a resx key in the reader's
/// language (seven tenant languages). <c>RoleErrorCodeContractTests</c> holds this list, the three screens' maps and
/// the 21 resx files equal. Renaming a value here is a breaking contract change.
/// </summary>
public static class RoleErrorCodes
{
    /// <summary>The request reached a person path without a person (no actor id on the token).</summary>
    public const string ActorRequired = "ROLE_ACTOR_REQUIRED";
    public const string NotFound = "ROLE_NOT_FOUND";
    public const string NameTaken = "ROLE_NAME_TAKEN";
    public const string SystemNotDeletable = "ROLE_SYSTEM_NOT_DELETABLE";
    /// <summary>A system role's display name and description are the template's; the screen offers no Edit for it.</summary>
    public const string SystemNotEditable = "ROLE_SYSTEM_NOT_EDITABLE";

    // CreateRoleCommandValidator (FluentValidation ErrorCode).
    public const string NameRequired = "ROLE_NAME_REQUIRED";
    public const string NameTooLong = "ROLE_NAME_TOO_LONG";
    public const string DisplayNameRequired = "ROLE_DISPLAY_NAME_REQUIRED";
    public const string DisplayNameTooLong = "ROLE_DISPLAY_NAME_TOO_LONG";
    public const string DescriptionTooLong = "ROLE_DESCRIPTION_TOO_LONG";

    /// <summary>A platform-administration permission cannot be granted to a tenant role.</summary>
    public const string PermissionNotTenantAssignable = "ROLE_PERMISSION_NOT_TENANT_ASSIGNABLE";
    /// <summary>The role already holds the permission (also the answer to a double click).</summary>
    public const string PermissionAlreadyGranted = "ROLE_PERMISSION_ALREADY_GRANTED";
    /// <summary>A System/Module grant is provisioning-managed and cannot be removed by hand.</summary>
    public const string PermissionGrantManaged = "ROLE_PERMISSION_GRANT_MANAGED";

    /// <summary>User Roles screen: the user the role is being assigned to does not exist in this tenant.</summary>
    public const string UserRoleUserNotFound = "USER_ROLE_USER_NOT_FOUND";

    /// <summary>A refusal that carries its code next to the English sentence.</summary>
    public static Response<T> Refuse<T>(string code, string error, int statusCode) =>
        Response<T>.Fail(error, [new ResponseError(code)], statusCode);
}
