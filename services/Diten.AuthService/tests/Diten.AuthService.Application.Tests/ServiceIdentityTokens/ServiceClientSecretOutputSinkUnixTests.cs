using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Diten.AuthService.Api.Services.ServiceIdentityTokens;
using Xunit;
using Reasons = Diten.AuthService.Api.Services.ServiceIdentityTokens.ServiceClientSecretOutputSink.Reasons;

namespace Diten.AuthService.Application.Tests.ServiceIdentityTokens;

/// <summary>Runs on Unix only; on Windows it is skipped BY NAME (the Windows path has its own tests).</summary>
public sealed class UnixFactAttribute : FactAttribute
{
    public UnixFactAttribute()
    {
        if (OperatingSystem.IsWindows()) Skip = "UNIX_ONLY: the Windows pipe path is measured by ServiceClientOperationalProvisioningRunnerTests.";
    }
}

/// <summary>FIX3: the theory twin of <see cref="UnixFactAttribute"/>.</summary>
public sealed class UnixTheoryAttribute : TheoryAttribute
{
    public UnixTheoryAttribute()
    {
        if (OperatingSystem.IsWindows()) Skip = "UNIX_ONLY: the Windows pipe path is measured by ServiceClientOperationalProvisioningRunnerTests.";
    }
}

/// <summary>
/// WP-AUTH-BL570-01 (+FIX1) — the secret channel on Unix keeps the Windows rule: the secret goes ONLY to an inherited,
/// verified OS pipe, once; never a path, never stdout / stderr / a log. Every refusal is asserted with its exact type and
/// reason (FIX1 M4). All descriptors are disposable test objects; the "secret" is random test data.
/// </summary>
[Collection(ConsoleCaptureCollection.Name)]
public sealed class ServiceClientSecretOutputSinkUnixTests
{
    [UnixFact]
    public async Task A_real_anonymous_pipe_receives_the_secret_once_and_it_appears_nowhere_else()
    {
        Assert.True(UnixPipeHandleInspector.IsSupported, "This Unix platform must pass the inspector's self-check.");
        using var pipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        var handle = PipeHandles.ForSink(pipe);
        var secret = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var (stdout, stderr) = await CaptureConsoleAsync(async () =>
        {
            await using var sink = new ServiceClientSecretOutputSink();
            await sink.PreflightAsync(handle, CancellationToken.None);
            await sink.DeliverOnceAsync(Guid.NewGuid(), "fixture-client", secret, CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() => sink.DeliverOnceAsync(Guid.NewGuid(), "fixture-client", secret, CancellationToken.None));
        });

        using var reader = new StreamReader(pipe);
        var line = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));
        using var payload = JsonDocument.Parse(line!);
        Assert.True(payload.RootElement.GetProperty("Secret").GetString() == secret, "The OS pipe must carry the secret.");
        Assert.False(stdout.Contains(secret, StringComparison.Ordinal) || stderr.Contains(secret, StringComparison.Ordinal),
            "The secret must not reach stdout or stderr.");
    }

    // CT sabotage K1: a SECOND delivery while the first is still writing is refused — the channel writes the secret at
    // most once. The first write is held inside the kernel (the pipe filled first), so a sink without its own
    // one-delivery guard would let the second through too (it would block on the full pipe). Every wait is BOUNDED and
    // the reader is always drained in `finally`: a regression shows as a red test (a timeout), never as a hang
    // (BL570 FIX1 — a first version of this test hung under that sabotage and held the machine lock).
    [UnixFact]
    public async Task A_second_delivery_while_the_first_is_still_writing_is_refused_and_one_envelope_arrives()
    {
        var bound = TimeSpan.FromSeconds(10);
        using var pipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        var handle = PipeHandles.ForSink(pipe);
        await using var sink = new ServiceClientSecretOutputSink();
        await sink.PreflightAsync(handle, CancellationToken.None);

        var filler = Dup(int.Parse(handle));
        var fillerOpen = true;
        var filling = Task.Run(() =>
        {
            var chunk = Encoding.ASCII.GetBytes(new string('.', 4096));
            for (var written = 0; written < 1 << 20; written += chunk.Length) Write(filler, chunk, chunk.Length);
        });
        Task? first = null;
        Task? second = null;
        using var drained = new MemoryStream();
        try
        {
            var waited = 0;
            while (Writable(filler) && waited++ < 500) await Task.Delay(10); // the pipe is full: the next write blocks
            Assert.False(Writable(filler), "The pipe must be full before the deliveries start.");

            first = Task.Run(() => sink.DeliverOnceAsync(Guid.NewGuid(), "fixture-client", "secret-one", CancellationToken.None));
            await Task.Delay(200);
            Assert.False(first.IsCompleted, "The first delivery must still be writing.");
            second = Task.Run(() => sink.DeliverOnceAsync(Guid.NewGuid(), "fixture-client", "secret-two", CancellationToken.None));
            // With the guard: refused at once. Without it: blocked on the full pipe → TimeoutException → red.
            await Assert.ThrowsAsync<InvalidOperationException>(() => second.WaitAsync(bound));
        }
        finally
        {
            // Always drain, so no write stays blocked after the test (pass or fail).
            var buffer = new byte[65536];
            var drain = Task.Run(async () =>
            {
                int read;
                while ((read = await pipe.ReadAsync(buffer)) > 0) drained.Write(buffer, 0, read);
            });
            await Settle(filling, bound);
            if (first is not null) await Settle(first, bound);
            if (second is not null) await Settle(second, bound);
            if (fillerOpen) { Close(filler); fillerOpen = false; }
            await Settle(drain, bound);
        }
        var text = Encoding.UTF8.GetString(drained.ToArray());
        Assert.Equal(1, CountOf(text, "\"Secret\":"));
        Assert.Contains("secret-one", text);
        Assert.DoesNotContain("secret-two", text);
    }

    private static async Task Settle(Task task, TimeSpan bound)
    {
        try { await task.WaitAsync(bound); }
        catch (Exception exception) when (exception is not TimeoutException) { /* its outcome is asserted elsewhere */ }
    }

    // Each kind is first shown not to be a FIFO, then refused for exactly that reason (FIX1 M4).
    [UnixFact]
    public async Task A_regular_file_dev_null_a_terminal_or_a_socket_is_refused_as_not_a_fifo_and_nothing_is_written()
    {
        var path = Path.Combine(Path.GetTempPath(), "diten-unix-sink-negative-" + Guid.NewGuid().ToString("N"));
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
        using var devNull = new FileStream("/dev/null", FileMode.Open, FileAccess.Write);
        var terminal = PosixOpenPt(2 | (OperatingSystem.IsMacOS() ? 0x20000 : 0x100)); // O_RDWR | O_NOCTTY
        var sockets = new int[2];
        Assert.True(terminal > 2, "A pseudo-terminal is needed for the terminal case.");
        Assert.Equal(1, IsATty(terminal));
        Assert.Equal(0, SocketPair(1, 1, 0, sockets));
        try
        {
            foreach (var descriptor in new[]
                     {
                         (int)file.SafeFileHandle.DangerousGetHandle(), (int)devNull.SafeFileHandle.DangerousGetHandle(),
                         terminal, sockets[0]
                     })
            {
                Assert.NotEqual(UnixPipeHandleInspector.TypeFifo, UnixPipeHandleInspector.Stat(descriptor)?.Type);
                await AssertRefusedAsync(descriptor, Reasons.NotAFifo);
            }
            Assert.Equal(0, file.Length);
        }
        finally
        {
            Close(terminal);
            Close(sockets[0]);
            Close(sockets[1]);
        }
    }

    // FIX1 R1 / CT sabotage K3: the access mode is exactly O_WRONLY. A named FIFO opened read-write (`exec 3<>fifo`) is
    // its own reader — poll never reports a hang-up and the secret would sit in the FIFO's buffer; a pipe's READ end is
    // not a channel either.
    [UnixFact]
    public async Task A_read_write_named_fifo_or_a_read_only_pipe_end_is_refused_as_not_write_only()
    {
        var path = Path.Combine(Path.GetTempPath(), "diten-unix-sink-fifo-" + Guid.NewGuid().ToString("N"));
        Assert.Equal(0, MkFifo(path, Convert.ToInt32("600", 8)));
        var readWrite = Open(path, 2); // O_RDWR: does not block, and makes this process the reader
        var ends = new int[2];
        Assert.Equal(0, Pipe(ends));
        try
        {
            Assert.True(readWrite > 2);
            Assert.Equal(UnixPipeHandleInspector.TypeFifo, UnixPipeHandleInspector.Stat(readWrite)?.Type);
            await AssertRefusedAsync(readWrite, Reasons.NotWriteOnly);
            Assert.Equal(UnixPipeHandleInspector.TypeFifo, UnixPipeHandleInspector.Stat(ends[0])?.Type);
            await AssertRefusedAsync(ends[0], Reasons.NotWriteOnly);
        }
        finally
        {
            Close(readWrite);
            Close(ends[0]);
            Close(ends[1]);
            Unlink(path);
        }
    }

    // FIX2 item 8: a NAMED FIFO is reachable by path, so only a private one of this user is a channel: 0600 is accepted,
    // a FIFO others can open (0666, 0640) is refused as FIFO_TOO_PERMISSIVE. Anonymous pipes need no check.
    // FIX3: the mode is SET after mkfifo (chmod) — mkfifo's mode passes through the umask (0666 became 0600 under a 077
    // umask, and the refusal case silently turned into an accepted one).
    [UnixFact]
    public async Task Only_a_private_named_fifo_of_this_user_is_a_channel()
    {
        foreach (var (mode, accepted) in new[] { ("600", true), ("666", false), ("640", false) })
        {
            var path = Path.Combine(Path.GetTempPath(), "diten-unix-sink-named-" + Guid.NewGuid().ToString("N"));
            Assert.Equal(0, MkFifo(path, Convert.ToInt32(mode, 8)));
            Assert.Equal(0, Chmod(path, Convert.ToInt32(mode, 8)));
            var reader = Open(path, OperatingSystem.IsMacOS() ? 0x4 : 0x800); // O_RDONLY | O_NONBLOCK: a reader exists
            var writer = Open(path, 1);                                          // O_WRONLY (does not block: reader open)
            try
            {
                Assert.True(reader > 2 && writer > 2);
                var identity = UnixPipeHandleInspector.Stat(writer)!.Value;
                Assert.False(UnixPipeHandleInspector.IsAnonymousPipe(writer, identity));
                var handle = Dup(writer);
                await using var sink = new ServiceClientSecretOutputSink();
                if (accepted)
                {
                    await sink.PreflightAsync(handle.ToString(System.Globalization.CultureInfo.InvariantCulture), CancellationToken.None);
                }
                else
                {
                    var refused = await Assert.ThrowsAsync<SecretChannelRejectedException>(
                        () => sink.PreflightAsync(handle.ToString(System.Globalization.CultureInfo.InvariantCulture), CancellationToken.None));
                    Assert.Equal(Reasons.FifoTooPermissive, refused.Reason);
                    Close(handle); // refused before the sink opened it: still ours
                }
            }
            finally
            {
                Close(writer);
                Close(reader);
                Unlink(path);
            }
        }
    }

    /*
     * FIX3 R1 (measured 2026-10-09 on macOS 26: a named FIFO whose path is removed after it was opened reads st_nlink 0,
     * like an anonymous pipe). The attack: a 0666 FIFO in a shared directory, its reader held by the attacker, the
     * operator's shell opens the write end, the attacker removes the path — "nlink 0" alone made it "anonymous" and the
     * owner / mode checks were skipped. It keeps its mode, its filesystem's device and its owner: refused.
     */
    [UnixFact]
    public async Task A_named_fifo_whose_path_was_removed_is_not_taken_for_an_anonymous_pipe()
    {
        var path = Path.Combine(Path.GetTempPath(), "diten-unix-sink-unlinked-" + Guid.NewGuid().ToString("N"));
        Assert.Equal(0, MkFifo(path, Convert.ToInt32("666", 8)));
        Assert.Equal(0, Chmod(path, Convert.ToInt32("666", 8)));
        var reader = Open(path, OperatingSystem.IsMacOS() ? 0x4 : 0x800); // O_RDONLY | O_NONBLOCK: the attacker's reader
        var writer = Open(path, 1);                                          // O_WRONLY: the operator's end
        var unlinked = false;
        try
        {
            Assert.True(reader > 2 && writer > 2);
            Assert.Equal(0, Unlink(path));
            unlinked = true;
            var identity = UnixPipeHandleInspector.Stat(writer)!.Value;
            if (OperatingSystem.IsMacOS()) Assert.Equal(0UL, identity.Links); // the measured fact this test is about
            Assert.False(UnixPipeHandleInspector.IsAnonymousPipe(writer, identity));
            var handle = Dup(writer);
            await using var sink = new ServiceClientSecretOutputSink();
            var refused = await Assert.ThrowsAsync<SecretChannelRejectedException>(
                () => sink.PreflightAsync(handle.ToString(System.Globalization.CultureInfo.InvariantCulture), CancellationToken.None));
            Assert.Equal(Reasons.FifoTooPermissive, refused.Reason);
            Close(handle);
        }
        finally
        {
            Close(writer);
            Close(reader);
            if (!unlinked) Unlink(path);
        }
    }

    // FIX3 (CT sabotage C3 / C4 survived: the real FIFO test always had uid = euid, and 0666 → 0644 under the umask hit
    // the "others" bit anyway). The decision on SYNTHETIC identities — owner and each single group bit tried on its own.
    [UnixTheory]
    [InlineData("600", true, null)]
    [InlineData("600", false, Reasons.FifoNotOwned)]
    [InlineData("640", true, Reasons.FifoTooPermissive)]   // group read
    [InlineData("620", true, Reasons.FifoTooPermissive)]   // group write
    [InlineData("610", true, Reasons.FifoTooPermissive)]   // group execute
    [InlineData("604", true, Reasons.FifoTooPermissive)]   // others read
    [InlineData("602", true, Reasons.FifoTooPermissive)]   // others write
    [InlineData("601", true, Reasons.FifoTooPermissive)]   // others execute (FIX4)
    public void A_named_fifo_is_a_channel_only_when_owned_by_this_user_and_closed_to_group_and_others(
        string mode, bool owned, string? expected)
    {
        var ends = new int[2];
        Assert.Equal(0, Pipe(ends));
        try
        {
            var mine = UnixPipeHandleInspector.Stat(ends[1])!.Value; // carries this process' euid
            var identity = mine with
            {
                Permissions = Convert.ToInt32(mode, 8),
                Uid = owned ? mine.Uid : mine.Uid + 1,
                Links = 1,
                Device = mine.Device + 1
            };
            Assert.Equal(expected, ServiceClientSecretOutputSink.NamedFifoRefusal(identity));
        }
        finally
        {
            Close(ends[0]);
            Close(ends[1]);
        }
    }

    // FIX3 R1: a real pipe() of this process is anonymous; the same pipe seen as another account's, or with other mode
    // bits, is not (the signature the proof recorded must match in full).
    [UnixFact]
    public void Anonymous_means_exactly_like_a_fresh_pipe_of_this_process()
    {
        var ends = new int[2];
        Assert.Equal(0, Pipe(ends));
        try
        {
            var identity = UnixPipeHandleInspector.Stat(ends[1])!.Value;
            Assert.True(UnixPipeHandleInspector.IsAnonymousPipe(ends[1], identity));
            Assert.True(UnixPipeHandleInspector.IsOwnedByThisUser(identity));
            Assert.False(UnixPipeHandleInspector.IsAnonymousPipe(ends[1], identity with { Uid = identity.Uid + 1 }));
            Assert.False(UnixPipeHandleInspector.IsOwnedByThisUser(identity with { Uid = identity.Uid + 1 }));
            Assert.False(UnixPipeHandleInspector.IsAnonymousPipe(ends[1], identity with { Permissions = 0x1B6 }));
            Assert.False(UnixPipeHandleInspector.IsAnonymousPipe(ends[1], identity with { Device = identity.Device + 1 }));
        }
        finally
        {
            Close(ends[0]);
            Close(ends[1]);
        }
    }

    // FIX1 M1: a descriptor that would not survive exec (FD_CLOEXEC set — the runtime's own pipes are such) is not one a
    // launcher handed over: refused.
    [UnixFact]
    public async Task A_close_on_exec_pipe_is_refused_as_not_inherited()
    {
        using var pipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
        var descriptor = int.Parse(pipe.GetClientHandleAsString());
        Assert.False(UnixPipeHandleInspector.IsInherited(descriptor));
        Assert.True(UnixPipeHandleInspector.IsWriteOnly(descriptor));
        await AssertRefusedAsync(descriptor, Reasons.NotInherited, leaveOpen: true);
    }

    // The twin of the Windows GetStdHandle comparison, by IDENTITY: under `dotnet test` stdout and stderr are pipes, so
    // a duplicate of them passes a plain "is it a pipe" test and would carry the secret to whatever reads stdout.
    [UnixFact]
    public async Task A_duplicate_of_stdout_or_stderr_is_refused_even_though_it_is_a_pipe()
    {
        Assert.Equal(UnixPipeHandleInspector.TypeFifo, UnixPipeHandleInspector.Stat(1)?.Type);
        foreach (var standard in new[] { 1, 2 })
        {
            var copy = Dup(standard);
            Assert.True(copy > 2);
            try
            {
                Assert.Equal(UnixPipeHandleInspector.TypeFifo, UnixPipeHandleInspector.Stat(copy)?.Type);
                await AssertRefusedAsync(copy, Reasons.SameAsStandardStream, leaveOpen: true);
            }
            finally
            {
                Close(copy);
            }
        }
    }

    // CT sabotage K4: 0 / 1 / 2 are refused BY NUMBER — not by the identity check (which would also catch 1 and 2 here).
    [UnixFact]
    public async Task Descriptors_zero_one_and_two_are_refused_by_number()
    {
        foreach (var descriptor in new[] { 0, 1, 2 }) await AssertRefusedAsync(descriptor, Reasons.StandardDescriptor, leaveOpen: true);
    }

    // FIX1 R2(b): the stat layout is PROVEN before it is trusted. A layout that reads the wrong offsets — the mode, the
    // inode, or another OS's whole table — is refused, so the platform would be unsupported (no secret), never misread.
    [UnixFact]
    public void A_wrong_stat_layout_fails_the_proof()
    {
        var real = UnixPipeHandleInspector.Candidate();
        Assert.NotNull(real);
        Assert.NotNull(UnixPipeHandleInspector.Prove(real!));
        Assert.Null(UnixPipeHandleInspector.Prove(real! with { ModeOffset = real.ModeOffset + 2 }));
        Assert.Null(UnixPipeHandleInspector.Prove(real! with { InodeOffset = real.InodeOffset + 8 }));
        var otherOs = OperatingSystem.IsMacOS()
            ? new UnixPipeHandleInspector.Layout(0, 8, 24, 4, 8, MacOs: false) // Linux x64's table on macOS
            : new UnixPipeHandleInspector.Layout(0, 4, 4, 2, 8, MacOs: true);  // macOS's table on Linux
        Assert.Null(UnixPipeHandleInspector.Prove(otherOs));
    }

    // FIX3 / FIX4: the uid field is proven — a uid offset moved onto the gid (+4 on every row) fails the proof. Only where
    // euid ≠ egid: Ubuntu's 1000 / 1000, .NET's `app` 1654 / 1654 and root 0 / 0 read the same value there — skipped BY NAME
    // (UID_EQUALS_GID), never a false red on CI.
    [UidDiffersFromGidFact]
    public void A_uid_offset_moved_onto_the_gid_fails_the_proof()
    {
        var real = UnixPipeHandleInspector.Candidate()!;
        Assert.Null(UnixPipeHandleInspector.Prove(real with { UidOffset = real.UidOffset + 4 }));
    }

    // FIX4: the link count decides anonymity on macOS only (Linux reads /proc/self/fd) — a link count read from the mode
    // fails macOS' proof; Linux' twin is the pure link rule below.
    [MacOsFact]
    public void A_link_count_read_from_another_field_fails_the_proof_on_macOS()
    {
        var real = UnixPipeHandleInspector.Candidate()!;
        Assert.Null(UnixPipeHandleInspector.Prove(real with { LinksOffset = real.ModeOffset }));
    }

    // FIX4: Linux' anonymity rule alone, on synthetic link targets (runs everywhere): only `pipe:[…]` is anonymous.
    [Theory]
    [InlineData("pipe:[123456]", true)]
    [InlineData("/tmp/diten-fifo", false)]
    [InlineData("/tmp/diten-fifo (deleted)", false)]
    [InlineData("socket:[42]", false)]
    [InlineData("anon_inode:[eventfd]", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void On_Linux_only_a_pipe_link_is_an_anonymous_pipe(string? linkTarget, bool anonymous)
    {
        Assert.Equal(anonymous, UnixPipeHandleInspector.IsPipeLink(linkTarget));
    }

    // FIX3: the proof records what an anonymous pipe looks like here (macOS: mode 0660 on device 0 — measured 2026-10-09).
    [UnixFact]
    public void The_proof_records_the_anonymous_pipe_signature()
    {
        var real = UnixPipeHandleInspector.Candidate()!;
        var proven = UnixPipeHandleInspector.Prove(real)!;
        Assert.NotNull(proven.AnonymousPermissions);
        Assert.NotNull(proven.AnonymousDevice);
        if (OperatingSystem.IsMacOS())
        {
            Assert.Equal(Convert.ToInt32("660", 8), proven.AnonymousPermissions);
            Assert.Equal(0L, proven.AnonymousDevice);
        }
    }

    private static async Task AssertRefusedAsync(int descriptor, string reason, bool leaveOpen = false)
    {
        await using var sink = new ServiceClientSecretOutputSink();
        var refused = await Assert.ThrowsAsync<SecretChannelRejectedException>(
            () => sink.PreflightAsync(descriptor.ToString(System.Globalization.CultureInfo.InvariantCulture), CancellationToken.None));
        Assert.Equal(reason, refused.Reason);
        Assert.Equal("The inherited one-shot secret pipe is invalid or unavailable.", refused.Message);
        // Refused at preflight: there is no channel, so no secret can be delivered anywhere.
        await Assert.ThrowsAsync<InvalidOperationException>(() => sink.DeliverOnceAsync(Guid.NewGuid(), "fixture-client", "never-written", CancellationToken.None));
        _ = leaveOpen;
    }

    private static int CountOf(string text, string value)
    {
        var count = 0;
        for (var at = text.IndexOf(value, StringComparison.Ordinal); at >= 0; at = text.IndexOf(value, at + value.Length, StringComparison.Ordinal)) count++;
        return count;
    }

    private static bool Writable(int descriptor)
    {
        var entry = new PollDescriptor { Descriptor = descriptor, Events = 0x4 };
        return (OperatingSystem.IsMacOS() ? PollMac(ref entry, 1, 0) : PollLinux(ref entry, 1, 0)) == 1 && (entry.ReturnedEvents & 0x4) != 0;
    }

    private static async Task<(string Stdout, string Stderr)> CaptureConsoleAsync(Func<Task> action)
    {
        var (out0, err0) = (Console.Out, Console.Error);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        Console.SetOut(stdout);
        Console.SetError(stderr);
        try { await action(); }
        finally
        {
            Console.SetOut(out0);
            Console.SetError(err0);
        }
        return (stdout.ToString(), stderr.ToString());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PollDescriptor
    {
        public int Descriptor;
        public short Events;
        public short ReturnedEvents;
    }

    [DllImport("libc", EntryPoint = "dup", SetLastError = true)]
    private static extern int Dup(int descriptor);
    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    private static extern int Close(int descriptor);
    [DllImport("libc", EntryPoint = "write", SetLastError = true)]
    private static extern nint Write(int descriptor, byte[] buffer, nint count);
    [DllImport("libc", EntryPoint = "pipe", SetLastError = true)]
    private static extern int Pipe(int[] descriptors);
    [DllImport("libc", EntryPoint = "mkfifo", SetLastError = true)]
    private static extern int MkFifo(string path, int mode);
    [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
    private static extern int Chmod(string path, int mode);
    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int Open(string path, int flags);
    [DllImport("libc", EntryPoint = "unlink", SetLastError = true)]
    private static extern int Unlink(string path);
    [DllImport("libc", EntryPoint = "posix_openpt", SetLastError = true)]
    private static extern int PosixOpenPt(int flags);
    [DllImport("libc", EntryPoint = "isatty", SetLastError = true)]
    private static extern int IsATty(int descriptor);
    [DllImport("libc", EntryPoint = "socketpair", SetLastError = true)]
    private static extern int SocketPair(int domain, int type, int protocol, int[] descriptors);
    [DllImport("libc", EntryPoint = "poll", SetLastError = true)]
    private static extern int PollMac(ref PollDescriptor descriptors, uint count, int timeout);
    [DllImport("libc", EntryPoint = "poll", SetLastError = true)]
    private static extern int PollLinux(ref PollDescriptor descriptors, nuint count, int timeout);
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ConsoleCaptureCollection
{
    public const string Name = "Console capture (process-wide Console.Out)";
}
