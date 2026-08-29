namespace Diten.Platform.Application.Features.WorkingCalendar.Services;

public interface IWorkingCalendarLegalEntityValidator
{
    Task<WorkingCalendarLegalEntityValidationResult> ValidateAsync(
        Guid legalEntityId,
        CancellationToken ct = default);
}

public sealed record WorkingCalendarLegalEntityValidationResult(
    bool IsReferenceable,
    bool DependencyUnavailable)
{
    public static readonly WorkingCalendarLegalEntityValidationResult Valid = new(true, false);
    public static readonly WorkingCalendarLegalEntityValidationResult NotReferenceable = new(false, false);
    public static readonly WorkingCalendarLegalEntityValidationResult Unavailable = new(false, true);
}
