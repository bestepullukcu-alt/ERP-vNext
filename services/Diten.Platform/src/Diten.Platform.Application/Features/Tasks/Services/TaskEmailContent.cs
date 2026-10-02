using Diten.Platform.Domain.Enums.Tasks;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// BL-454 — the absolute address of a task's page, for the button of a task e-mail. Implemented where the web
/// origin setting lives (<c>AuthServiceOptions.FrontendBaseUrl</c>, the <c>ITimeEntryLinks</c> precedent); the PATH
/// is still <see cref="TaskLinks.Detail"/>'s, so a task has one address whether it is in a notification or a mail.
/// </summary>
public interface ITaskWebLinks
{
    string Detail(Guid taskId);
}

/// <summary>
/// BL-454 — the words a task e-mail needs that are not in the template: a priority is an enum, and "High" in an
/// Arabic e-mail is a defect nobody reports. One language outside the seven reads English.
/// </summary>
public static class TaskEmailContent
{
    public static string PriorityLabel(TaskPriority priority, string? locale)
    {
        var language = (locale ?? string.Empty).Trim().ToLowerInvariant();
        var separator = language.IndexOf('-');
        if (separator > 0)
        {
            language = language[..separator];
        }

        var (low, medium, high) = language switch
        {
            "tr" => ("Düşük", "Orta", "Yüksek"),
            "fr" => ("Basse", "Moyenne", "Haute"),
            "es" => ("Baja", "Media", "Alta"),
            "zh" => ("低", "中", "高"),
            "ar" => ("منخفضة", "متوسطة", "عالية"),
            "ru" => ("Низкий", "Средний", "Высокий"),
            _ => ("Low", "Medium", "High")
        };

        return priority switch
        {
            TaskPriority.Low => low,
            TaskPriority.High => high,
            _ => medium
        };
    }
}
