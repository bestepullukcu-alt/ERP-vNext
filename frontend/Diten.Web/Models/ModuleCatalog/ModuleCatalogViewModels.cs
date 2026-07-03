using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.ModuleCatalog;

public sealed class ModuleCatalogEditViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(80, MinimumLength = 2)]
    [RegularExpression(@"^[A-Z0-9]+(-[A-Z0-9]+)*$")]
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

    [Required]
    public string Status { get; set; } = "Draft";

    [Required]
    [RegularExpression(@"^\d+\.\d+\.\d+$")]
    public string ModuleVersion { get; set; } = "1.0.0";

    public bool IsCoreModule { get; set; }
    public bool IsTenantAssignable { get; set; } = true;

    [Range(0, int.MaxValue)]
    public int? SortOrder { get; set; }

    // FIX-MODULE-ICON — optional boxicons class (e.g. "bx-cog"). Operator-owned SOFT field.
    [StringLength(60)]
    [RegularExpression(@"^bxs?-[a-z0-9-]+$")]
    public string? Icon { get; set; }

    // FEAT-CATALOG-BASELINE-BADGE — HARD/code-owned; read-only in the form (shown as an info row, never editable).
    public bool IsBaseline { get; set; }
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
    public string Status { get; set; } = string.Empty;
    public string ModuleVersion { get; set; } = string.Empty;
    public bool IsCoreModule { get; set; }
    public bool IsTenantAssignable { get; set; }
    public int SortOrder { get; set; }
    public string Origin { get; set; } = "Manual";
    public string? Icon { get; set; } // FIX-MODULE-ICON
    public bool IsBaseline { get; set; } // FEAT-CATALOG-BASELINE-BADGE
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
    public string Status { get; set; } = "Draft";
    public string ModuleVersion { get; set; } = "1.0.0";
    public bool IsCoreModule { get; set; }
    public bool IsTenantAssignable { get; set; }
    public int? SortOrder { get; set; }
    public string? Icon { get; set; } // FIX-MODULE-ICON
}

public sealed class GatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
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
    public string? ParentPageCode { get; set; }
    public bool IsNavigationVisible { get; set; } = true;
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
    public string? ParentPageCode { get; set; }
    public bool? IsNavigationVisible { get; set; }
    public string PageType { get; set; } = "List";
    public string Status { get; set; } = "Draft";
    public int? SortOrder { get; set; }
    public string? Description { get; set; }
}

public sealed class ModulePageActionDescriptorViewModel
{
    public Guid Id { get; set; }
    public Guid PageDescriptorId { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
    public string PageCode { get; set; } = string.Empty;
    public string ActionCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PermissionKey { get; set; } = string.Empty;
    public string ActionType { get; set; } = "Toolbar";
    public int SortOrder { get; set; }
    public bool IsDangerous { get; set; }
    public bool IsToolbarAction { get; set; }
    public bool IsRowAction { get; set; }
    public string Status { get; set; } = "Draft";
    public string? Description { get; set; }
}
