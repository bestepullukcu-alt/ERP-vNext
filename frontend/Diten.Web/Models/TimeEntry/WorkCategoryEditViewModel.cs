using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.TimeEntry;

/// <summary>MOD-0280-FU01 T2b — the work-category Slim form's fields (pack §4.6, 5 user fields). The code is immutable after
/// create (D10); the page never sends a changed one, and Platform refuses it anyway (WORK_CATEGORY_CODE_IMMUTABLE).</summary>
public sealed class WorkCategoryEditViewModel
{
    public Guid? Id { get; set; }

    [Required]
    public string Code { get; set; } = string.Empty;

    [Required]
    public string LabelText { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool CountsAsWork { get; set; } = true;

    public int? SortOrder { get; set; }
}
