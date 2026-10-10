using System.Diagnostics;
using Diten.AuthService.Api.Operational;
using Diten.AuthService.Application.Common.Entitlements;
using Diten.AuthService.Application.Tests.ServiceIdentityTokens;
using Row = Diten.AuthService.Api.Operational.UnixProcessObservation.ProcessRow;

namespace Diten.AuthService.Application.Tests.Operational;

/// <summary>
/// WP-AUTH-BL570-01 FIX1 R3 / FIX2 items 3–5 — the reconciliation command's quiescence observation on Unix: the same
/// proof as the Windows one (no other process carrying Diten.AuthService — command line OR image, case-insensitive — and
/// nothing listening on 5056), fail-closed when the listing cannot be trusted. Decision, ps parsing and the Linux /proc
/// reader are tested on synthetic input; the real listing is shown to contain a live child that carries the marker.
/// </summary>
public sealed class UnixProcessObservationTests
{
    private const int Self = 4242;

    private static Row[] Listing(params Row[] extra) =>
        [new(1, "/sbin/launchd", "/sbin/launchd"),
            new(Self, "/usr/local/share/dotnet/dotnet Diten.AuthService.Api.dll --run-product-identity-entitlement-reconciliation", null),
            new(4343, "/bin/ps -A -ww -o pid=,uid=,args=", "/bin/ps"), .. extra];

    [Fact]
    public void A_listing_without_another_auth_process_or_listener_is_quiescent()
    {
        Assert.Equal(EntitlementReconciliationPlan.Hash(UnixProcessObservation.Quiescent),
            UnixProcessObservation.Classify(Listing(new Row(77, "/bin/sleep 5", "/bin/sleep")), [443, 5001], Self));
    }

    [Theory]
    [InlineData("/usr/local/share/dotnet/dotnet /srv/auth/Diten.AuthService.Api.dll", null)]
    [InlineData("/srv/auth/Diten.AuthService.Api --urls http://0.0.0.0:5056", null)]
    // FIX2 item 4: case-insensitive (default APFS is case-insensitive) …
    [InlineData("/usr/local/share/dotnet/dotnet /srv/auth/diten.authservice.api.dll", null)]
    // … and the IMAGE path counts when argv[0] was renamed (`exec -a`).
    [InlineData("renamed-worker --urls http://0.0.0.0:7000", "/srv/auth/Diten.AuthService.Api")]
    public void Another_auth_process_is_not_quiescent(string commandLine, string? image)
    {
        Assert.Equal(UnixProcessObservation.NotQuiescent, Assert.Throws<InvalidOperationException>(
            () => UnixProcessObservation.Classify(Listing(new Row(77, commandLine, image)), [], Self)).Message);
    }

    [Fact]
    public void A_listener_on_the_auth_port_is_not_quiescent()
    {
        Assert.Equal(UnixProcessObservation.NotQuiescent, Assert.Throws<InvalidOperationException>(
            () => UnixProcessObservation.Classify(Listing(), [UnixProcessObservation.AuthPort], Self)).Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(Self)]
    public void A_listing_that_misses_pid_one_or_this_process_is_not_trusted(int missing)
    {
        var rows = Listing().Where(row => row.Pid != missing).ToArray();
        Assert.Equal(UnixProcessObservation.ObservationFailed, Assert.Throws<InvalidOperationException>(
            () => UnixProcessObservation.Classify(rows, [], Self)).Message);
    }

    // FIX2 item 5: a row that cannot be classified (arguments unread: an empty command line, or a bare `dotnet`) refuses
    // the observation — the twin of Windows' "owner unreadable → exit 4".
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("dotnet")]
    [InlineData("/usr/local/share/dotnet/dotnet")]
    public void An_unclassifiable_row_refuses_the_observation(string commandLine)
    {
        Assert.Equal(UnixProcessObservation.ObservationFailed, Assert.Throws<InvalidOperationException>(
            () => UnixProcessObservation.Classify(Listing(new Row(77, commandLine, null)), [], Self)).Message);
    }

    // FIX2 item 3: the macOS ps reader — args and image joined by pid (uid may be negative); any line that does not parse
    // refuses the observation.
    [Fact]
    public void The_ps_reader_joins_args_and_image_and_refuses_a_malformed_line()
    {
        var rows = UnixProcessObservation.ParsePs(
            "    1     0 /sbin/launchd\n 4779    -2 /usr/libexec/dhcp6d\n  900   501 /bin/sh -c sleep 1; : Diten.AuthService.Probe\n",
            "    1 /sbin/launchd\n 4779 /usr/libexec/dhcp6d\n  900 /bin/sh\n");
        Assert.Equal([1, 4779, 900], rows.Select(row => row.Pid));
        Assert.Equal("/bin/sh -c sleep 1; : Diten.AuthService.Probe", rows[2].CommandLine);
        Assert.Equal("/usr/libexec/dhcp6d", rows[1].Image);

        Assert.Equal(UnixProcessObservation.ObservationFailed, Assert.Throws<InvalidOperationException>(
            () => UnixProcessObservation.ParsePs("    1     0 /sbin/launchd\nnot a ps row\n", "    1 /sbin/launchd\n")).Message);
        Assert.Equal(UnixProcessObservation.ObservationFailed, Assert.Throws<InvalidOperationException>(
            () => UnixProcessObservation.ParsePs("    1     0 /sbin/launchd\n", "garbage\n")).Message);
    }

    // FIX2 item 3: the Linux /proc reader on a synthetic tree — command lines (NUL-separated), a kernel thread (comm), the
    // image link, a pid directory without a readable command line that vanished (skipped), non-pid entries ignored.
    [Fact]
    public void The_proc_reader_reads_command_lines_images_and_kernel_threads()
    {
        var root = Path.Combine(Path.GetTempPath(), "diten-fake-proc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "1"));
        File.WriteAllBytes(Path.Combine(root, "1", "cmdline"), "/sbin/init\0splash\0"u8.ToArray());
        Directory.CreateDirectory(Path.Combine(root, "2"));
        File.WriteAllBytes(Path.Combine(root, "2", "cmdline"), []);
        File.WriteAllText(Path.Combine(root, "2", "comm"), "kthreadd\n");
        Directory.CreateDirectory(Path.Combine(root, "77"));
        File.WriteAllBytes(Path.Combine(root, "77", "cmdline"), "renamed\0--urls\0http://0.0.0.0:7000\0"u8.ToArray());
        File.CreateSymbolicLink(Path.Combine(root, "77", "exe"), "/srv/auth/Diten.AuthService.Api");
        Directory.CreateDirectory(Path.Combine(root, "88")); // ended mid-read: no cmdline any more
        Directory.CreateDirectory(Path.Combine(root, "self"));

        var rows = UnixProcessObservation.ListWithProc(root).OrderBy(row => row.Pid).ToArray();

        Assert.Equal([1, 2, 77], rows.Select(row => row.Pid));
        Assert.Equal("/sbin/init splash", rows[0].CommandLine);
        Assert.Equal("kthreadd", rows[1].CommandLine);
        Assert.Equal("/srv/auth/Diten.AuthService.Api", rows[2].Image);
    }

    [Theory]
    [InlineData("22 26 0:21 / /proc rw,nosuid,nodev,noexec,relatime shared:13 - proc proc rw,hidepid=2", true)]
    [InlineData("22 26 0:21 / /proc rw,nosuid,nodev,noexec,relatime shared:13 - proc proc rw,hidepid=invisible", true)]
    [InlineData("22 26 0:21 / /proc rw,nosuid,nodev,noexec,relatime shared:13 - proc proc rw,hidepid=0", false)]
    [InlineData("22 26 0:21 / /proc rw,nosuid,nodev,noexec,relatime shared:13 - proc proc rw", false)]
    public void A_proc_mounted_with_hidepid_is_detected(string mountLine, bool hides)
    {
        var path = Path.Combine(Path.GetTempPath(), "diten-mountinfo-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(path, "25 1 8:1 / / rw,relatime shared:1 - ext4 /dev/sda1 rw\n" + mountLine + "\n");
        Assert.Equal(hides, UnixProcessObservation.ProcHidesProcesses(path));
    }

    // FIX2 item 3 / FIX3 R3(a): the REAL listing step contains a live child, with its pid and its marker-carrying command
    // line (the shell keeps the marker as $0; `; :` stops sh from exec-ing away). FIX3: the child lives until its stdin
    // closes (the OperationalLockHolder way — never a timer that may run out under load), and the decision is asserted on
    // exactly {pid 1, this process, the child} from that real listing — nothing else on the machine (a test runner, a dev
    // Auth, a bare `dotnet`) can make it pass or fail. Every wait bounded; the child is ended by closing its stdin, and
    // only if it does not end is it killed — by its own pid, never by name.
    [UnixFact]
    public async Task The_real_listing_contains_a_live_marker_process_and_it_alone_makes_the_decision_not_quiescent()
    {
        var bound = TimeSpan.FromSeconds(30);
        using var child = Process.Start(new ProcessStartInfo("/bin/sh")
        {
            ArgumentList = { "-c", "read _; :", "Diten.AuthService.ObservationProbe" },
            UseShellExecute = false,
            RedirectStandardInput = true
        })!;
        try
        {
            using var budget = new CancellationTokenSource(bound);
            var rows = await UnixProcessObservation.ListAsync(budget.Token).WaitAsync(bound);
            var probe = Assert.Single(rows, row => row.Pid == child.Id);
            Assert.Contains("Diten.AuthService.ObservationProbe", probe.CommandLine, StringComparison.Ordinal);
            var self = Environment.ProcessId;
            var basis = rows.Where(row => row.Pid == 1 || row.Pid == self).ToArray();
            Assert.Equal(2, basis.Length);

            Assert.Equal(UnixProcessObservation.NotQuiescent, Assert.Throws<InvalidOperationException>(
                () => UnixProcessObservation.Classify([.. basis, probe], [], self)).Message);
            Assert.Equal(EntitlementReconciliationPlan.Hash(UnixProcessObservation.Quiescent),
                UnixProcessObservation.Classify(basis, [], self));
        }
        finally
        {
            child.StandardInput.Close();
            try { await child.WaitForExitAsync().WaitAsync(bound); }
            catch (TimeoutException)
            {
                child.Kill(); // this test's own child, by its pid
                await child.WaitForExitAsync().WaitAsync(bound);
            }
        }
    }

    // FIX3 R2: a /proc row is skipped only when its process really ended — its directory gone, or ENOENT / ESRCH; any
    // other I/O error refuses the observation (before FIX3 every IOException was "ended": fail-closed turned open).
    [Fact]
    public void Only_a_process_that_really_ended_is_skipped_by_the_proc_reader()
    {
        var existing = Path.GetTempPath();
        var gone = Path.Combine(Path.GetTempPath(), "diten-gone-" + Guid.NewGuid().ToString("N"));
        Assert.True(UnixProcessObservation.Ended(gone, new IOException("eio", 5)));
        Assert.True(UnixProcessObservation.Ended(existing, new IOException("esrch", 3)));
        Assert.True(UnixProcessObservation.Ended(existing, new FileNotFoundException("enoent")));
        Assert.False(UnixProcessObservation.Ended(existing, new IOException("eio", 5)));
        Assert.False(UnixProcessObservation.Ended(existing, new IOException("unknown")));
    }

    // FIX3 R2 on a synthetic tree: the directory exists and its command line fails with another I/O error (a symbolic
    // link loop — ELOOP, not ENOENT / ESRCH): refused, never skipped.
    [UnixFact]
    public void A_proc_entry_that_exists_but_cannot_be_read_refuses_the_observation()
    {
        var root = Path.Combine(Path.GetTempPath(), "diten-fake-proc-eio-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "1"));
        File.WriteAllBytes(Path.Combine(root, "1", "cmdline"), "/sbin/init\0"u8.ToArray());
        Directory.CreateDirectory(Path.Combine(root, "99"));
        File.CreateSymbolicLink(Path.Combine(root, "99", "cmdline"), Path.Combine(root, "99", "cmdline"));

        Assert.Equal(UnixProcessObservation.ObservationFailed, Assert.Throws<InvalidOperationException>(
            () => UnixProcessObservation.ListWithProc(root)).Message);
    }

    // FIX3 (CT sabotage C7 survived: the UnauthorizedAccessException branch had no test): a /proc entry whose command line
    // this account may not read (another user's, hidepid half-applied…) refuses the observation — never skipped.
    [UnixFact]
    public void A_proc_entry_whose_command_line_cannot_be_read_by_this_account_refuses_the_observation()
    {
        Assert.False(Environment.UserName == "root", "Run as a regular user: root reads a mode-000 file.");
        var root = Path.Combine(Path.GetTempPath(), "diten-fake-proc-eacces-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "1"));
        File.WriteAllBytes(Path.Combine(root, "1", "cmdline"), "/sbin/init\0"u8.ToArray());
        Directory.CreateDirectory(Path.Combine(root, "98"));
        var locked = Path.Combine(root, "98", "cmdline");
        File.WriteAllBytes(locked, "/srv/auth/Diten.AuthService.Api\0"u8.ToArray());
        File.SetUnixFileMode(locked, UnixFileMode.None);
        try
        {
            Assert.Equal(UnixProcessObservation.ObservationFailed, Assert.Throws<InvalidOperationException>(
                () => UnixProcessObservation.ListWithProc(root)).Message);
        }
        finally
        {
            File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    // FIX3: Linux' comm is cut to 15 characters — a comm-only row is matched on the 15 the kernel keeps.
    [Fact]
    public void A_comm_only_row_is_matched_on_what_comm_can_hold()
    {
        Assert.Equal(15, UnixProcessObservation.CommMarker.Length);
        Assert.Equal(UnixProcessObservation.NotQuiescent, Assert.Throws<InvalidOperationException>(
            () => UnixProcessObservation.Classify(Listing(new Row(77, "Diten.AuthServi", null, FromComm: true)), [], Self)).Message);
        Assert.Equal(EntitlementReconciliationPlan.Hash(UnixProcessObservation.Quiescent),
            UnixProcessObservation.Classify(Listing(new Row(77, "Diten.AuthServi", null)), [], Self));
    }
}
