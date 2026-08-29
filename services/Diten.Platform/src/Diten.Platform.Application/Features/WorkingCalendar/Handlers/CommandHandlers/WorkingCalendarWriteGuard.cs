using Diten.Platform.Application.Common;
using Diten.Platform.Domain.Repositories;
using Wc = Diten.Platform.Domain.Entities.WorkingCalendar.WorkingCalendar;

namespace Diten.Platform.Application.Features.WorkingCalendar.Handlers.CommandHandlers;

/// <summary>
/// Shared load-and-authorize step for every mutating handler. It exists so the layer check happens in exactly ONE
/// place: both controllers dispatch the same commands, so a per-handler copy of this rule is how the tenant boundary
/// would eventually end up enforced on one surface but not the other.
/// </summary>
internal static class WorkingCalendarWriteGuard
{
    /// <summary>
    /// Loads a calendar the caller is actually allowed to write. A platform actor may only reach country rows; a
    /// tenant may only reach its own override rows. Anything else is 404 rather than 403, so the existence of another
    /// layer's row is never disclosed.
    /// </summary>
    public static async Task<(Wc? Calendar, string? Error, int Status)> LoadWritableAsync(
        IWorkingCalendarRepository repository, Guid id, bool isPlatformActor, CancellationToken ct)
    {
        var calendar = isPlatformActor
            ? await repository.GetCountryLayerByIdAsync(id, ct)
            : await repository.GetOwnOverrideByIdAsync(id, ct);

        if (calendar is null)
        {
            return (null, "Working calendar not found.", 404);
        }

        var writable = WorkingCalendarValidation.ValidateWritable(calendar);
        if (!writable.Ok)
        {
            return (null, writable.Message, writable.StatusCode);
        }

        return (calendar, null, 200);
    }

    /// <summary>Optimistic concurrency: a mismatched version answers 409 instead of silently clobbering a concurrent edit.</summary>
    public static async Task<Response<NoContent>> ReplaceAsync(
        IWorkingCalendarRepository repository, Wc calendar, int expectedVersion, CancellationToken ct)
    {
        var ok = await repository.ReplaceAsync(calendar, expectedVersion, ct);
        return ok
            ? Response<NoContent>.Success(204)
            : Response<NoContent>.Fail("The calendar changed since it was loaded. Reload and reapply the change.", 409);
    }
}
