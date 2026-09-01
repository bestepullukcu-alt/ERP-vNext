using FluentValidation;
using FluentValidation.Results;

namespace Diten.Platform.Infrastructure.Persistence;

/// <summary>
/// The one place that decides what happens when a client asks to sort a list OLDEST-FIRST on a timestamp.
///
/// <para><b>The defect (BL-030).</b> No <c>DateTimeOffsetSerializer</c> is registered, so the Mongo driver stores
/// every <see cref="DateTimeOffset"/> as a BSON ARRAY — <c>[localTicks, offsetMinutes]</c>. MongoDB compares an
/// array by its EXTREMUM, and picks a different extremum per direction:</para>
/// <list type="bullet">
///   <item>ASCENDING compares the SMALLEST element, which is <c>offsetMinutes</c> (-300..+180) — never the ticks
///   (~6.4e17). Ascending therefore orders rows BY TIME ZONE, not by time.</item>
///   <item>DESCENDING compares the LARGEST element, which IS the ticks — but they are LOCAL WALL-CLOCK ticks, so
///   descending orders by wall-clock reading and still ignores the offset.</item>
/// </list>
///
/// <para><b>⚠ MEASURED 2026-08-28 against the real dev database, not reasoned about.</b> On
/// <c>task_items.DueAt</c> (22 rows at offset 0, 144 at +180) ascending was non-monotonic from row 4, and
/// descending from row 14 — it returned <c>2026-09-29 21:00Z</c> ahead of <c>2026-09-30 00:00Z</c> in a
/// DESCENDING list. So descending is NOT a correct fallback in general; it is correct only while every row in the
/// collection shares one offset.</para>
///
/// <para><b>Why this class refuses instead of silently flipping direction.</b> Returning descending rows to a
/// client that asked for ascending is giving the caller something other than what it asked for, without saying
/// so — the exact failure class BL-030 exists to close. A 400 with a stable reason code is loud, is visible the
/// first time anyone clicks the column, and cannot be mistaken for data.</para>
///
/// <para><b>Where this applies, and where it deliberately does not.</b> Only to sorts a CLIENT chooses. Sorts the
/// server fixes for itself — outbox drains, retry queues, history trails — are left ascending on purpose: those
/// fields are stamped exclusively from <c>DateTimeOffset.UtcNow</c>, so their offset element is invariably 0, the
/// array comparison degenerates to a ticks comparison, and the order is correct. That invariant is what
/// <c>DateTimeOffsetSortGuardTests</c> pins; it is not a hope.</para>
/// </summary>
public static class TimestampSortPolicy
{
    /// <summary>
    /// Curated, therefore passed through verbatim by <c>ValidationReasonCode.From</c> and mapped in the frontend
    /// resx bridge. Renaming it silently unmaps every translation — see BL-040.
    /// </summary>
    public const string OldestFirstUnsupportedCode = "SORT_TIMESTAMP_OLDEST_FIRST_UNSUPPORTED";

    /// <summary>
    /// Returns <paramref name="descendingSort"/>, or throws when the caller asked for oldest-first.
    /// </summary>
    /// <param name="descending">Whether the caller asked for newest-first.</param>
    /// <param name="descendingSort">The newest-first sort for the requested field.</param>
    /// <param name="field">The client-facing field name, echoed back so the client knows which column it was.</param>
    public static T NewestFirstOnly<T>(bool descending, T descendingSort, string field)
    {
        if (descending)
        {
            return descendingSort;
        }

        throw new ValidationException(
        [
            new ValidationFailure(
                field,
                $"Sorting '{field}' oldest-first is not supported: timestamps are stored with their UTC offset, "
                + "and the database orders that representation by time zone rather than by time (BL-030). "
                + "Request newest-first instead.")
            {
                ErrorCode = OldestFirstUnsupportedCode
            }
        ]);
    }
}
