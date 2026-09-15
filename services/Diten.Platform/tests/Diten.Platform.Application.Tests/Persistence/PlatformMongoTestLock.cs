using System.Diagnostics;

namespace Diten.Platform.Application.Tests.Persistence;

/*
 * ⚠ ONE PLATFORM MONGO TEST PROCESS AT A TIME ON THIS MACHINE (BL-395, 2026-09-15).
 *
 * WHAT WENT WRONG. The databases this project uses on the shared mongod have FIXED names, on purpose — DB-010:
 * a name per run piles up until mongod dies. But a fixed name is shared by every PROCESS on the machine, not
 * just by every class in one run, and several paths begin by clearing what they find:
 *   • MongoIntegrationHarness.CreateIsolatedAsync empties every collection of its scoped database on open;
 *   • PlatformSchemaContractMongoTests and WorkflowTransitionGateMongoRepositoryTests drop theirs on open;
 *   • BusinessReferenceDataMongoResidueSweeper drops another run's BRD database once it is ONE MINUTE old.
 * Two worktrees running this project at the same time therefore wipe each other mid-assertion. The victim goes
 * red with rows or collections that "vanished", and green on rerun — which is why it read as flakiness.
 *
 * WHAT THIS DOES. Before a process first touches the shared mongod, it takes an exclusive OS file lock on a
 * FIXED path and keeps it until the process exits. A second process waits instead of colliding. The database
 * names stay fixed; only the machine's time is shared out.
 *
 * MEASURED ON macOS / .NET 8 ON 2026-09-15, NOT ASSUMED:
 *   • FileShare.None on Unix IS flock(2) LOCK_EX. python's fcntl.flock is refused while a .NET process holds
 *     the file, and .NET is refused while python holds it. The kernel releases it when the holder exits or is
 *     SIGKILLed, so a crashed run cannot leave the machine locked and there is no stale-lock cleanup to get wrong.
 *   • A refused second open inside the HOLDING process does not release the lock (flock belongs to the open
 *     file, unlike fcntl record locks, which any close drops). That is what makes RefuseUnlessExclusive safe.
 *   • DOTNET_SYSTEM_IO_DISABLEFILELOCKING=1 makes .NET skip flock altogether: such a process opens the file
 *     while another process holds it. RefuseUnlessExclusive turns that into a failure, never an unlocked run.
 *
 * ⚠ THE PATH IGNORES TMPDIR ON PURPOSE. Agent sandboxes set their own TMPDIR; a lock under Path.GetTempPath()
 * would give two sandboxes two different files and exclude nobody. /tmp is one directory for every process of
 * the user. Windows uses %SystemRoot%\Temp, which no TEMP/TMP variable moves (not measured on Windows).
 *
 * ⚠ NEVER DELETE THE LOCK FILE. flock locks the open file, not its name: delete it while a run holds it and the
 * next process creates a new file, locks that one, and both run. A dead holder needs no cleanup at all.
 */
public static class PlatformMongoTestLock
{
    public const string LockFileName = "diten-platform-itest.lock";

    /// <summary>
    /// How long a process waits before it fails. A run still waiting after half an hour is stuck behind
    /// something a person should look at — and failing says so, where proceeding would corrupt both runs.
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(30);

    /// <summary>How often a waiting process says that it is waiting, and for whom.</summary>
    public static readonly TimeSpan ProgressInterval = TimeSpan.FromSeconds(30);

    /// <summary>The one lock path for every process of this OS user, whatever TMPDIR says.</summary>
    public static string LockPath { get; } = OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp", LockFileName)
        : "/tmp/" + LockFileName;

    private static LockProofRedirect? _redirect;

    /*
     * Lazy<Task>: the first caller starts the one acquisition and every other caller in the process awaits the
     * SAME task. The lock is taken once; and a process that timed out does not queue for another 30 minutes per
     * test — every later caller gets the same failure straight away.
     */
    private static readonly Lazy<Task<MachineWideFileLock>> Acquisition = new(
        () => Task.Run(() => MachineWideFileLock.AcquireAsync(
            _redirect?.LockPath ?? LockPath,
            _redirect?.Timeout ?? Timeout,
            _redirect?.ProgressInterval ?? ProgressInterval,
            Console.Error)),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Await this before the first thing that opens, sweeps, empties or drops a database on the shared mongod.
    /// Nothing releases it in-process: the static task keeps the handle open until the process ends.
    /// </summary>
    public static Task EnsureHeldAsync() => Acquisition.Value;

    /// <summary>
    /// Points this process at a proof lock instead of the real one. Only a process started as the lock-proof
    /// child can do this, so a normal test run has no way to move the path or shorten the wait.
    /// </summary>
    internal static void RedirectForLockProofChild(string lockPath, TimeSpan timeout, TimeSpan progressInterval)
    {
        if (!LockProofChild.IsThisProcess)
        {
            throw new InvalidOperationException(
                "Only the lock-proof child process may redirect the machine-wide Mongo test lock.");
        }

        if (Acquisition.IsValueCreated)
        {
            throw new InvalidOperationException(
                "The machine-wide Mongo test lock was already requested in this process.");
        }

        _redirect = new LockProofRedirect(lockPath, timeout, progressInterval);
    }

    private sealed record LockProofRedirect(string LockPath, TimeSpan Timeout, TimeSpan ProgressInterval);
}

/// <summary>An exclusive, OS-level, cross-process lock on one file, held for as long as this object lives.</summary>
public sealed class MachineWideFileLock
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    // Never read: the open handle IS the lock. The operating system closes it when the process ends.
    private readonly FileStream _handle;

    private MachineWideFileLock(FileStream handle, string lockPath)
    {
        _handle = handle;
        LockPath = lockPath;
    }

    public string LockPath { get; }

    /// <summary>
    /// Who took the lock last. A separate file because a waiting process cannot read the locked one: on Unix
    /// .NET opens a file for reading under a shared flock, which the holder's exclusive flock refuses.
    /// </summary>
    public static string HolderFilePath(string lockPath) => lockPath + ".holder";

    public static async Task<MachineWideFileLock> AcquireAsync(
        string lockPath,
        TimeSpan timeout,
        TimeSpan progressInterval,
        TextWriter log,
        CancellationToken cancellationToken = default)
    {
        var clock = Stopwatch.StartNew();
        var nextReport = TimeSpan.Zero;
        var contended = false;

        while (true)
        {
            var handle = TryOpenExclusive(lockPath);
            if (handle is not null)
            {
                RefuseUnlessExclusive(lockPath, handle);
                RecordHolder(lockPath);
                if (contended)
                {
                    log.WriteLine($"[PlatformMongoTestLock] acquired {lockPath} after waiting {Describe(clock.Elapsed)}");
                }

                return new MachineWideFileLock(handle, lockPath);
            }

            contended = true;
            if (clock.Elapsed >= timeout)
            {
                throw new MachineWideFileLockTimeoutException(
                    $"[PlatformMongoTestLock] timed out after {Describe(timeout)} waiting for {lockPath}, held by "
                    + $"{DescribeHolder(lockPath)}. Another Platform Mongo test run on this machine still has it: let it "
                    + "finish or stop it. Do not delete the lock file, and do not run these tests without it.");
            }

            if (clock.Elapsed >= nextReport)
            {
                log.WriteLine(
                    $"[PlatformMongoTestLock] waiting for {lockPath}, held by {DescribeHolder(lockPath)}; "
                    + $"waited {Describe(clock.Elapsed)} of {Describe(timeout)}");
                nextReport = clock.Elapsed + progressInterval;
            }

            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    private static FileStream? TryOpenExclusive(string lockPath)
    {
        try
        {
            return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException exception) when (IsHeldElsewhere(exception))
        {
            return null;
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new MachineWideFileLockNotEnforcedException(
                $"[PlatformMongoTestLock] cannot open {lockPath} ({exception.Message}). If another OS user created "
                + "it, that user's run shares this mongod too; the tests refuse to run without the lock.",
                exception);
        }
    }

    /*
     * Measured on macOS: IOException.HResult == 35, the raw EWOULDBLOCK errno, which .NET passes through on Unix.
     * Linux's EWOULDBLOCK is 11. Windows reports ERROR_SHARING_VIOLATION (32) or ERROR_LOCK_VIOLATION (33) inside
     * an HRESULT. Any other IOException is a real problem and propagates.
     */
    private static bool IsHeldElsewhere(IOException exception) =>
        OperatingSystem.IsWindows() ? (exception.HResult & 0xFFFF) is 32 or 33
        : OperatingSystem.IsLinux() ? exception.HResult == 11
        : exception.HResult == 35;

    /*
     * ⚠ THE LOCK PROVES ITSELF BEFORE ANY TEST IS ALLOWED TO RELY ON IT. Opening the same file a second time with
     * FileShare.None must be refused even from this process. When .NET's file locking is switched off
     * (DOTNET_SYSTEM_IO_DISABLEFILELOCKING, the System.IO.DisableFileLocking switch, or a filesystem without
     * flock) the second open succeeds — and then this process excludes nobody and ignores everybody. Failing
     * here is the only honest outcome.
     */
    private static void RefuseUnlessExclusive(string lockPath, FileStream held)
    {
        FileStream second;
        try
        {
            second = new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException exception) when (IsHeldElsewhere(exception))
        {
            return;
        }
        catch
        {
            held.Dispose();
            throw;
        }

        second.Dispose();
        held.Dispose();
        var variable = Environment.GetEnvironmentVariable("DOTNET_SYSTEM_IO_DISABLEFILELOCKING") ?? "(unset)";
        throw new MachineWideFileLockNotEnforcedException(
            $"[PlatformMongoTestLock] {lockPath} opened twice with FileShare.None in one process, so file locking "
            + $"is not in effect here (DOTNET_SYSTEM_IO_DISABLEFILELOCKING={variable}). This process would neither "
            + "wait for another Platform Mongo test run nor keep one out. Unset that variable (or the "
            + "System.IO.DisableFileLocking switch) and run again; these tests do not run unlocked.");
    }

    private static void RecordHolder(string lockPath)
    {
        try
        {
            var command = Environment.CommandLine;
            File.WriteAllText(
                HolderFilePath(lockPath),
                $"pid={Environment.ProcessId} since={DateTime.UtcNow:O} "
                + $"cmd={(command.Length > 240 ? command[..240] + "…" : command)}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Informational only; the lock is the open handle, not this file.
        }
    }

    private static string DescribeHolder(string lockPath)
    {
        try
        {
            var recorded = File.ReadAllText(HolderFilePath(lockPath)).Trim();
            return recorded.Length == 0 ? "an unrecorded process" : recorded;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return "an unrecorded process";
        }
    }

    private static string Describe(TimeSpan span) =>
        span.TotalMinutes >= 1 ? $"{(int)span.TotalMinutes} min {span.Seconds} s" : $"{span.TotalSeconds:0.#} s";
}

public sealed class MachineWideFileLockTimeoutException(string message) : TimeoutException(message);

public sealed class MachineWideFileLockNotEnforcedException(string message, Exception? inner = null)
    : InvalidOperationException(message, inner);
