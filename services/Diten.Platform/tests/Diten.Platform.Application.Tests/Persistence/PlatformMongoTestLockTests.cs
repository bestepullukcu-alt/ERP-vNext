using System.Diagnostics;
using System.Text.RegularExpressions;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

/*
 * BL-395 — THE MACHINE-WIDE MONGO TEST LOCK, PROVED WITH REAL PROCESSES.
 *
 * Each proof starts one or two more `dotnet` processes running THIS assembly through LockProofChild, so what is
 * on trial is the harness and the lock themselves, not a copy of them. The children use a PROOF lock path next
 * to the real one, so these tests never contend with — or release — the lock their own process holds.
 *
 * SABOTAGE, 2026-09-15: delete `await PlatformMongoTestLock.EnsureHeldAsync();` from
 * MongoIntegrationHarness.CreateCoreAsync and both harness proofs go red — the "blocked" child opens a database
 * at once instead of waiting, and the child with file locking switched off runs instead of refusing.
 */
public sealed class PlatformMongoTestLockTests
{
    private static readonly TimeSpan ChildDeadline = TimeSpan.FromSeconds(120);

    [Fact]
    public async Task The_lock_path_is_fixed_and_does_not_follow_TMPDIR()
    {
        var elsewhere = Directory.CreateTempSubdirectory("diten-lockproof-tmpdir-").FullName;
        try
        {
            await using var child = LockProofProcess.Start(
                new Dictionary<string, string?> { ["TMPDIR"] = elsewhere, ["TMP"] = elsewhere, ["TEMP"] = elsewhere },
                "path");

            Assert.True(await child.WaitForExitAsync(ChildDeadline) == LockProofChild.Succeeded, child.Output);

            // The variable really reached the child: its temp directory moved...
            Assert.Contains(child.Lines, line => line.StartsWith($"TEMP_PATH={elsewhere}", StringComparison.Ordinal));

            // ...and the lock did not.
            Assert.Contains($"LOCK_PATH={PlatformMongoTestLock.LockPath}", child.Lines);
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal("/tmp/diten-platform-itest.lock", PlatformMongoTestLock.LockPath);
            }
        }
        finally
        {
            Directory.Delete(elsewhere, recursive: true);
        }
    }

    [Fact]
    public async Task A_second_process_waits_gives_up_while_the_lock_is_held_and_opens_once_the_holder_dies()
    {
        // The children below reach the shared mongod when they get their proof lock, so this process takes the real one.
        await PlatformMongoTestLock.EnsureHeldAsync();
        var lockPath = ProofLockPath();
        try
        {
            await using var holder = LockProofProcess.Start(null, "hold", lockPath);
            Assert.True(await holder.WaitForLineAsync("HOLDING pid=", ChildDeadline), holder.Output);

            // 1. While another process holds it, the harness waits, keeps saying so, gives up — and never opens a database.
            var clock = Stopwatch.StartNew();
            await using (var blocked = LockProofProcess.Start(null, "harness", lockPath, "3"))
            {
                var exit = await blocked.WaitForExitAsync(ChildDeadline);
                clock.Stop();

                Assert.True(exit == LockProofChild.TimedOut, $"expected exit {LockProofChild.TimedOut}, got {exit}:\n{blocked.Output}");
                Assert.DoesNotContain("HARNESS_OPENED", blocked.Output);
                Assert.True(
                    blocked.Lines.Count(line => line.Contains($"waiting for {lockPath}, held by pid={holder.Id} ", StringComparison.Ordinal)) >= 2,
                    "the waiting process must name the lock and its holder, repeatedly:\n" + blocked.Output);
                Assert.Contains("timed out after 3 s", blocked.Output);
                Assert.True(clock.Elapsed >= TimeSpan.FromSeconds(3), $"gave up after {clock.Elapsed}, before its timeout");
            }

            // 2. A waiter that is visibly blocked opens the moment the holder DIES — the kernel releases the lock.
            await using var waiter = LockProofProcess.Start(null, "harness", lockPath, "120");
            Assert.True(await waiter.WaitForLineAsync($"waiting for {lockPath}", ChildDeadline), waiter.Output);
            Assert.DoesNotContain("HARNESS_OPENED", waiter.Output);

            var killedAt = DateTime.UtcNow;
            holder.Kill();

            Assert.True(await waiter.WaitForExitAsync(ChildDeadline) == LockProofChild.Succeeded, waiter.Output);
            var acquired = waiter.Entries.Single(entry => entry.Text.Contains($"acquired {lockPath} after waiting", StringComparison.Ordinal));
            Assert.True(acquired.At >= killedAt, $"acquired at {acquired.At:O}, before the holder was killed at {killedAt:O}");
            Assert.Contains(waiter.Lines, line => line.StartsWith($"HARNESS_OPENED database={MongoIntegrationHarness.SharedDatabaseName} ", StringComparison.Ordinal));
        }
        finally
        {
            DeleteProofFiles(lockPath);
        }
    }

    [Fact]
    public async Task A_process_with_file_locking_switched_off_refuses_instead_of_running_unlocked()
    {
        await PlatformMongoTestLock.EnsureHeldAsync();
        var lockPath = ProofLockPath();
        try
        {
            await using var child = LockProofProcess.Start(
                new Dictionary<string, string?> { ["DOTNET_SYSTEM_IO_DISABLEFILELOCKING"] = "1" },
                "harness", lockPath, "5");

            var exit = await child.WaitForExitAsync(ChildDeadline);

            Assert.True(exit == LockProofChild.NotEnforced, $"expected exit {LockProofChild.NotEnforced}, got {exit}:\n{child.Output}");
            Assert.Contains("DOTNET_SYSTEM_IO_DISABLEFILELOCKING=1", child.Output);
            Assert.DoesNotContain("HARNESS_OPENED", child.Output);
        }
        finally
        {
            DeleteProofFiles(lockPath);
        }
    }

    /*
     * THE WIRING, MEASURED ON THIS PROJECT'S OWN SOURCE. The harness is proved above with processes; the classes
     * that build their own MongoClient against the shared mongod are held to the same rule here. SABOTAGE: delete
     * the EnsureHeldAsync line from WorkflowTransitionGateMongoRepositoryTests and this names that file.
     * Weakness, stated: a text match — a call inside a comment would satisfy it.
     */
    [Fact]
    public void Every_file_that_addresses_the_shared_mongod_takes_the_lock()
    {
        var project = Path.Combine(RepoPaths.Services(), "Diten.Platform", "tests", "Diten.Platform.Application.Tests");
        var sharedMongod = new Regex(@"mongodb://(?:localhost|127\.0\.0\.1):27017");
        var separator = Path.DirectorySeparatorChar;

        var addressing = Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{separator}obj{separator}") && !path.Contains($"{separator}bin{separator}"))
            .Select(path => (Path: Path.GetRelativePath(project, path).Replace('\\', '/'), Body: File.ReadAllText(path)))
            .Where(file => sharedMongod.IsMatch(file.Body))
            .ToArray();

        // A scan that silently finds nothing is green forever; the harness itself must always be in it.
        Assert.Contains(addressing, file => file.Path == "Persistence/MongoIntegrationHarness.cs");

        var bypassing = addressing
            .Where(file => !file.Body.Contains("PlatformMongoTestLock.EnsureHeldAsync()", StringComparison.Ordinal))
            .Select(file => file.Path)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.True(bypassing.Length == 0,
            "these files talk to the shared mongod without the machine-wide lock, so a second test process on this "
            + "machine can empty or drop what they are asserting (BL-395):\n" + string.Join("\n", bypassing));
    }

    private static string ProofLockPath() => Path.Combine(
        Path.GetDirectoryName(PlatformMongoTestLock.LockPath)!,
        $"diten-platform-itest-lockproof-{Guid.NewGuid():N}.lock");

    private static void DeleteProofFiles(string lockPath)
    {
        foreach (var path in new[] { lockPath, MachineWideFileLock.HolderFilePath(lockPath) })
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // A proof file left in /tmp is harmless; the proof's verdict is already recorded.
            }
        }
    }

    private sealed class LockProofProcess : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly List<(DateTime At, string Text)> _entries = new();

        private LockProofProcess(Process process) => _process = process;

        public int Id => _process.Id;

        public IReadOnlyList<(DateTime At, string Text)> Entries
        {
            get
            {
                lock (_entries)
                {
                    return _entries.ToArray();
                }
            }
        }

        public IReadOnlyList<string> Lines => Entries.Select(entry => entry.Text).ToArray();

        public string Output => string.Join('\n', Lines);

        public static LockProofProcess Start(IReadOnlyDictionary<string, string?>? environment, params string[] args)
        {
            var start = new ProcessStartInfo(DotnetHost())
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            start.ArgumentList.Add(typeof(LockProofChild).Assembly.Location);
            start.ArgumentList.Add(LockProofChild.Verb);
            foreach (var argument in args)
            {
                start.ArgumentList.Add(argument);
            }

            start.Environment.Remove("DOTNET_SYSTEM_IO_DISABLEFILELOCKING");
            foreach (var (name, value) in environment ?? new Dictionary<string, string?>())
            {
                start.Environment[name] = value;
            }

            var process = new Process { StartInfo = start };
            var child = new LockProofProcess(process);
            process.OutputDataReceived += (_, e) => child.Append(e.Data);
            process.ErrorDataReceived += (_, e) => child.Append(e.Data);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return child;
        }

        public async Task<bool> WaitForLineAsync(string fragment, TimeSpan deadline)
        {
            var clock = Stopwatch.StartNew();
            while (clock.Elapsed < deadline)
            {
                if (Lines.Any(line => line.Contains(fragment, StringComparison.Ordinal)))
                {
                    return true;
                }

                if (_process.HasExited)
                {
                    await _process.WaitForExitAsync();
                    return Lines.Any(line => line.Contains(fragment, StringComparison.Ordinal));
                }

                await Task.Delay(50);
            }

            return false;
        }

        public async Task<int> WaitForExitAsync(TimeSpan deadline)
        {
            using var cancel = new CancellationTokenSource(deadline);
            try
            {
                await _process.WaitForExitAsync(cancel.Token);
            }
            catch (OperationCanceledException)
            {
                Kill();
                throw new TimeoutException($"lock-proof child did not exit within {deadline}:\n{Output}");
            }

            return _process.ExitCode;
        }

        public void Kill()
        {
            if (!_process.HasExited)
            {
                _process.Kill();
            }
        }

        public async ValueTask DisposeAsync()
        {
            Kill();
            await _process.WaitForExitAsync();
            _process.Dispose();
        }

        private void Append(string? line)
        {
            if (line is null)
            {
                return;
            }

            lock (_entries)
            {
                _entries.Add((DateTime.UtcNow, line));
            }
        }

        private static string DotnetHost()
        {
            var fromCli = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
            if (!string.IsNullOrEmpty(fromCli) && File.Exists(fromCli))
            {
                return fromCli;
            }

            var current = Environment.ProcessPath;
            return current is not null
                   && Path.GetFileNameWithoutExtension(current).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
                ? current
                : "dotnet";
        }
    }
}
