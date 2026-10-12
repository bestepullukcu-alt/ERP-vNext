namespace Diten.Platform.Application.Tests.Persistence;

/*
 * BL-482 — RUNS ONE PIECE OF START-OF-RUN HOUSEKEEPING EXACTLY ONCE, AND MAKES EVERY CALLER WAIT FOR IT.
 *
 * The harness used to guard its residue sweep with `Interlocked.Exchange(ref started, 1)`: the first caller ran
 * the sweep, and every other caller returned IMMEDIATELY and went on to stamp and build the schema of the shared
 * database — while the first caller was still deciding to drop that very database. Reproduced 2026-10-01 by ageing
 * the shared database's marker past the one-hour residue window: the sweep dropped `diten_platform_itest` under a
 * class that was building the Meetings indexes ("database is in the process of being dropped"), the profile was
 * memoised as applied, the rest of the run had no unique indexes, and two "the real unique index refuses a
 * duplicate" tests left their duplicates behind — the sticky E11000 of BL-482.
 *
 * "Once" is not enough on its own. The property that matters is "nobody touches a database until the sweep has
 * FINISHED", which is what returning the same Task to every caller gives.
 */
internal sealed class RunOnceGate
{
    private readonly object _lock = new();
    private Task? _task;

    /// <summary>
    /// Starts <paramref name="work"/> on the first call; every call, first or not, gets the SAME task and so
    /// completes only when the work has.
    /// </summary>
    public Task RunAsync(Func<Task> work)
    {
        lock (_lock)
        {
            return _task ??= Task.Run(work);
        }
    }
}
