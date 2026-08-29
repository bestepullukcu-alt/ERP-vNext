using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.WorkingCalendar;

public sealed class WorkingCalendarImportViewModel
{
    [Required] public string CountryCode { get; set; } = string.Empty;
    [Required] public int CalendarYear { get; set; }
    [Required] public Guid? TargetCalendarId { get; set; }
    public bool IncludeNonPublicTypes { get; set; }
    public string? Notes { get; set; }
}
