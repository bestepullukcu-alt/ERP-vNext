using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.PlatformAdministrators;

public sealed class PlatformAdministratorEditViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    public string ActorType { get; set; } = "PlatformAdmin";

    public Guid? PartnerId { get; set; }

    public string? AllowedTenantIdsRaw { get; set; }

    [Required]
    public List<string> Roles { get; set; } = ["ReadOnly"];

    public string Status { get; set; } = "Active";

    public int Version { get; set; }

}

public sealed class PlatformAdministratorDetailViewModel
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public Guid? PartnerId { get; set; }
    public List<Guid> AllowedTenantIds { get; set; } = [];
    public string Status { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = [];
    public DateTimeOffset? LastLoginAtUtc { get; set; }
    public string InvitationStatus { get; set; } = string.Empty;
    public DateTimeOffset? InvitedAtUtc { get; set; }
    public DateTimeOffset? InviteExpiresAtUtc { get; set; }
    public string? LastStatusReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? UpdatedBy { get; set; }
    public int Version { get; set; }
}

public sealed class PlatformAdministratorSavePayload
{
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ActorType { get; set; } = "PlatformAdmin";
    public Guid? PartnerId { get; set; }
    public List<Guid> AllowedTenantIds { get; set; } = [];
    public List<string> Roles { get; set; } = [];
    public string Status { get; set; } = "Active";
    public int Version { get; set; }
    public bool SendEmail { get; set; } = true;
    public bool RequirePasswordChange { get; set; } = true;
}

public sealed class GatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}

public sealed class PlatformAdministratorInviteResultViewModel
{
    public Guid Id { get; set; }
    public string? SetupUrl { get; set; }
    public bool EmailSent { get; set; }
}
