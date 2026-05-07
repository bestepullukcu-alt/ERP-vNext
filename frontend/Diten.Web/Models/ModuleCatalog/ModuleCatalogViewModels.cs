using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.ModuleCatalog;

public sealed class ModuleCatalogEditViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string ModuleCode { get; set; } = string.Empty;

    [Required]
    public string ModuleName { get; set; } = string.Empty;

    [Required]
    public string DisplayName { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public string Domain { get; set; } = string.Empty;

    [Required]
    public string Service { get; set; } = string.Empty;

    public string? Category { get; set; }

    [Required]
    public string Status { get; set; } = "Draft";

    [Required]
    [RegularExpression(@"^\d+\.\d+\.\d+$")]
    public string ModuleVersion { get; set; } = "1.0.0";

    public bool IsCoreModule { get; set; }
    public bool IsTenantAssignable { get; set; } = true;

    [Range(0, int.MaxValue)]
    public int? SortOrder { get; set; }
}

public sealed class ModuleCatalogDetailViewModel
{
    public Guid Id { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ModuleVersion { get; set; } = string.Empty;
    public bool IsCoreModule { get; set; }
    public bool IsTenantAssignable { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class ModuleCatalogSavePayload
{
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string Status { get; set; } = "Draft";
    public string ModuleVersion { get; set; } = "1.0.0";
    public bool IsCoreModule { get; set; }
    public bool IsTenantAssignable { get; set; }
    public int? SortOrder { get; set; }
}

public sealed class GatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public bool Succeeded { get; set; }
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = [];
}

public sealed class ModulePageDescriptorViewModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
    public string PageCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string RoutePath { get; set; } = string.Empty;
    public string? RequiredPermission { get; set; }
    public string PageType { get; set; } = "List";
    public string Status { get; set; } = "Draft";
    public int SortOrder { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }
}

public sealed class ModulePageDescriptorSavePayload
{
    public string ModuleCode { get; set; } = string.Empty;
    public string PageCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string RoutePath { get; set; } = string.Empty;
    public string? RequiredPermission { get; set; }
    public string PageType { get; set; } = "List";
    public string Status { get; set; } = "Draft";
    public int? SortOrder { get; set; }
    public string? Description { get; set; }
}
