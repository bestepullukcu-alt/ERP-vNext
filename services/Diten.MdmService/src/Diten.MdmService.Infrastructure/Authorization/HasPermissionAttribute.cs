using Microsoft.AspNetCore.Authorization;

namespace Diten.MdmService.Infrastructure.Authorization;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
        => Policy = $"Permission:{permission}";

    /// <summary>
    /// WP-MDM-BRAND-ACCEPT-01 FIX1 (item 11) — ANY of the listed permissions admits (a read shared by two modules, e.g. the
    /// brand/product contract). The policy name joins them with <see cref="PermissionRequirement.AnyOfSeparator"/>.
    /// </summary>
    public HasPermissionAttribute(string permission, params string[] alternatives)
        => Policy = $"Permission:{string.Join(PermissionRequirement.AnyOfSeparator, new[] { permission }.Concat(alternatives))}";
}
