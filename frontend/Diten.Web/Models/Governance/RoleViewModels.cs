using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.Governance;

// FE-C (MOD-0018-FU9) — tenant Roles screen view models. Mirrors the golden-reference Slim
// contract; backed by AuthService /api/roles via the gateway.
public sealed class RoleEditViewModel
{
    // WP-ROLES-CLOSE-01 — AuthService's limits (RoleFieldLimits) and refusal codes (RoleErrorCodes), restated here
    // because this project cannot reference that one; RoleErrorCodeContractTests (AuthService tests) reads this file
    // and holds both equal. The form's `maxlength` reads these two constants — there is no third copy.
    public const int NameMaxLength = 50;
    public const int DisplayNameMaxLength = 100;

    public const string NameRequiredCode = "ROLE_NAME_REQUIRED";
    public const string NameTooLongCode = "ROLE_NAME_TOO_LONG";
    public const string DisplayNameRequiredCode = "ROLE_DISPLAY_NAME_REQUIRED";
    public const string DisplayNameTooLongCode = "ROLE_DISPLAY_NAME_TOO_LONG";

    public Guid? Id { get; set; }

    // No [Required] here on purpose: a bare attribute answers with the framework's English sentence. The form's own
    // rules are ValidationCodes() below — the same codes AuthService's validator uses, said in the reader's language.
    public string? Name { get; set; }

    public string? DisplayName { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Every rule the posted form breaks, as the stable codes the screen already has a sentence for in seven
    /// languages. All of them, not the first: a form with two mistakes says both.
    /// </summary>
    public IReadOnlyList<string> ValidationCodes()
    {
        var codes = new List<string>();
        var name = Name?.Trim() ?? string.Empty;
        var displayName = DisplayName?.Trim() ?? string.Empty;

        if (name.Length == 0) codes.Add(NameRequiredCode);
        else if (name.Length > NameMaxLength) codes.Add(NameTooLongCode);

        if (displayName.Length == 0) codes.Add(DisplayNameRequiredCode);
        else if (displayName.Length > DisplayNameMaxLength) codes.Add(DisplayNameTooLongCode);

        return codes;
    }
}

public sealed class RoleDetailViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public int PermissionCount { get; set; }
}

// AuthService CreateRoleRequest contract: { name, displayName, description }.
public sealed class RoleCreatePayload
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

// AuthService UpdateRoleRequest contract: { displayName, description } (name is immutable).
public sealed class RoleUpdatePayload
{
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public sealed class GovernanceGatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
