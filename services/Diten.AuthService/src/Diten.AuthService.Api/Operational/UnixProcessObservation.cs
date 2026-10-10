using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using Diten.AuthService.Application.Common.Entitlements;

namespace Diten.AuthService.Api.Operational;

/// <summary>
/// WP-AUTH-BL570-01 FIX1 R3 — the Unix twin of the reconciliation command's Windows quiescence observation.
///
/// <para><b>What the Windows observation proves</b> (measured from its PowerShell, unchanged): over EVERY process
/// (<c>Win32_Process</c>) other than itself, none is named <c>Diten.AuthService*</c> or has <c>Diten.AuthService</c> in
/// its command line; every such row's owner is readable (exit 4 otherwise — fail closed); and nothing listens on Auth's
/// port 5056. Quiescent → the constant <c>AUTH_WRITERS_ABSENT</c>, hashed; anything else →
/// <c>AUTH_WRITERS_NOT_QUIESCENT</c>. (Windows' limit: without admin, another user's command line is null, so a
/// <c>dotnet.exe</c> host of another user is matched by name only.)</para>
///
/// <para><b>Unix, the same proof.</b> macOS: <c>/bin/ps</c> (absolute path; setuid root on macOS — measured 2026-10-07:
/// it lists every process's command line, root's included, 627 / 627 with no empty row, where .NET's Process API
/// could name none of 199 other-user processes). Linux: <c>/proc/&lt;pid&gt;/cmdline</c> for every process, refused when
/// <c>/proc</c> is mounted with <c>hidepid</c> (other users' processes would be invisible) or a command line cannot be
/// read. A row is an Auth writer when its command line or its image path contains <c>Diten.AuthService</c>, compared
/// case-insensitively (FIX2); only this process is excluded (the observer's own ps arguments never carry the marker —
/// FIX2 removed the observer-pid exclusion, a pid-reuse hole). The listing must contain pid 1 and this
/// process, or it is not trusted. Listeners: .NET's own TCP listener table (measured on macOS: sees a fresh listener).
/// Any other OS → <see cref="UnsupportedPlatform"/>; the command does not run. Nothing here is relaxed relative to
/// Windows; the Linux path is unmeasured on this machine (no Linux) and fails closed.</para>
/// </summary>
public static class UnixProcessObservation
{
    public const string Marker = "Diten.AuthService";
    public const int AuthPort = 5056;
    public const string Quiescent = "AUTH_WRITERS_ABSENT";
    public const string NotQuiescent = "AUTH_WRITERS_NOT_QUIESCENT";
    public const string ObservationFailed = "PROCESS_OBSERVATION_FAILED";
    public const string UnsupportedPlatform = "PROCESS_OBSERVATION_UNSUPPORTED_PLATFORM";

    // uid may be negative on macOS (nobody = -2, measured).
    private static readonly Regex PsRow = new(@"^\s*(\d+)\s+(-?\d+)\s+(.*)$", RegexOptions.CultureInvariant);
    private static readonly Regex PsImageRow = new(@"^\s*(\d+)\s+(.*)$", RegexOptions.CultureInvariant);

    /// <summary>
    /// A listed process: its command line and, when readable, its image (executable) path. <c>FromComm</c>: Linux gave no
    /// command line and the row carries <c>comm</c> — which the kernel cuts to 15 characters (FIX3).
    /// </summary>
    public readonly record struct ProcessRow(int Pid, string CommandLine, string? Image, bool FromComm = false);

    // Linux' comm is at most 15 characters (TASK_COMM_LEN 16 with its NUL): the 17-character marker never fits whole.
    public static readonly string CommMarker = Marker[..15];

    public static async Task<string> ObserveAsync(CancellationToken ct)
    {
        var rows = await ListAsync(ct);
        int[] ports;
        try { ports = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Select(x => x.Port).ToArray(); }
        catch (Exception exception) when (exception is PlatformNotSupportedException or NetworkInformationException)
        { throw new InvalidOperationException(ObservationFailed); }
        return Classify(rows, ports, Environment.ProcessId);
    }

    /// <summary>The listing step alone (BL570 FIX2 item 3: tests prove a live process appears in it).</summary>
    internal static async Task<IReadOnlyList<ProcessRow>> ListAsync(CancellationToken ct)
    {
        if (OperatingSystem.IsMacOS())
            return ParsePs(await RunPsAsync("pid=,uid=,args=", ct), await RunPsAsync("pid=,comm=", ct));
        if (OperatingSystem.IsLinux())
        {
            if (ProcHidesProcesses("/proc/self/mountinfo")) throw new InvalidOperationException(UnsupportedPlatform);
            return ListWithProc("/proc");
        }
        throw new InvalidOperationException(UnsupportedPlatform);
    }

    /// <summary>
    /// The decision, apart from how the rows were read (tested on synthetic listings). Matching is case-INSENSITIVE and
    /// covers the image path as well as the command line (FIX2 item 4: the Windows observation is case-insensitive and
    /// also matches the image name; macOS' default APFS is case-insensitive, and `exec -a` can rename argv[0]).
    /// A row that cannot be classified — an empty command line, or a bare <c>dotnet</c> with no arguments (another
    /// user's arguments unread) — refuses the whole observation (FIX2 item 5, the twin of Windows' "owner unreadable").
    /// </summary>
    public static string Classify(IReadOnlyList<ProcessRow> rows, IReadOnlyCollection<int> listenerPorts, int selfPid)
    {
        if (!rows.Any(row => row.Pid == 1) || !rows.Any(row => row.Pid == selfPid))
            throw new InvalidOperationException(ObservationFailed);
        if (rows.Any(Unclassifiable)) throw new InvalidOperationException(ObservationFailed);
        var writers = rows.Count(row => row.Pid != selfPid
                                        && (row.CommandLine.Contains(Marker, StringComparison.OrdinalIgnoreCase)
                                            || (row.Image?.Contains(Marker, StringComparison.OrdinalIgnoreCase) ?? false)
                                            // FIX3: a comm-only row is matched on what comm can hold.
                                            || row.FromComm && row.CommandLine.Contains(CommMarker, StringComparison.OrdinalIgnoreCase)));
        if (writers != 0 || listenerPorts.Contains(AuthPort)) throw new InvalidOperationException(NotQuiescent);
        return EntitlementReconciliationPlan.Hash(Quiescent);
    }

    private static bool Unclassifiable(ProcessRow row)
    {
        var tokens = row.CommandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length == 0
               || (tokens.Length == 1 && Path.GetFileName(tokens[0]).Equals("dotnet", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<string> RunPsAsync(string columns, CancellationToken ct)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo("/bin/ps")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        }};
        foreach (var argument in new[] { "-A", "-ww", "-o", columns }) process.StartInfo.ArgumentList.Add(argument);
        if (!process.Start()) throw new InvalidOperationException(ObservationFailed);
        var output = process.StandardOutput.ReadToEndAsync(ct);
        var error = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        var text = await output;
        _ = await error;
        if (process.ExitCode != 0) throw new InvalidOperationException(ObservationFailed);
        return text;
    }

    /// <summary>
    /// macOS: <c>ps -o pid=,uid=,args=</c> and <c>ps -o pid=,comm=</c> (comm = the image's full path; two calls because
    /// both columns may hold spaces). Any line that does not parse refuses the observation. A pid only in one listing
    /// (started / ended between the two calls) keeps what was read.
    /// </summary>
    internal static IReadOnlyList<ProcessRow> ParsePs(string argsListing, string imageListing)
    {
        var images = new Dictionary<int, string>();
        foreach (var line in imageListing.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var imageMatch = PsImageRow.Match(line);
            if (!imageMatch.Success) throw new InvalidOperationException(ObservationFailed);
            images[int.Parse(imageMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)] = imageMatch.Groups[2].Value;
        }
        var rows = new List<ProcessRow>();
        foreach (var line in argsListing.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var match = PsRow.Match(line);
            if (!match.Success) throw new InvalidOperationException(ObservationFailed);
            var pid = int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            rows.Add(new ProcessRow(pid, match.Groups[3].Value, images.GetValueOrDefault(pid)));
        }
        return rows;
    }

    /// <summary>
    /// Linux: every <c>&lt;root&gt;/&lt;pid&gt;/cmdline</c> (kernel threads: <c>comm</c>), and the image from <c>exe</c> when readable
    /// (another user's is not — the command line still is). A process that ENDED mid-read is skipped — only when it is
    /// really gone (FIX3 R2 / FIX4: the read said ENOENT / ESRCH); ANY other I/O error (EIO,
    /// a short read the kernel refused…) refuses the observation, as an unreadable command line does — before FIX3 every
    /// IOException was "ended", which opened the fail-closed rule. ⚠ PID namespaces: only this namespace's processes are
    /// listed — an Auth in another container is caught by its 5056 listener alone. Unmeasured on this machine (no Linux).
    /// </summary>
    internal static IReadOnlyList<ProcessRow> ListWithProc(string root)
    {
        var rows = new List<ProcessRow>();
        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            if (!int.TryParse(Path.GetFileName(directory), System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var pid)) continue;
            try
            {
                var bytes = File.ReadAllBytes(Path.Combine(directory, "cmdline"));
                var fromComm = bytes.Length == 0;
                var commandLine = !fromComm
                    ? Encoding.UTF8.GetString(bytes).Replace('\0', ' ').Trim()
                    : File.ReadAllText(Path.Combine(directory, "comm")).Trim(); // kernel thread: no command line
                string? image = null;
                try { image = new FileInfo(Path.Combine(directory, "exe")).LinkTarget; }
                catch (Exception exception) when (exception is UnauthorizedAccessException or IOException) { }
                rows.Add(new ProcessRow(pid, commandLine, image, fromComm));
            }
            catch (UnauthorizedAccessException)
            {
                throw new InvalidOperationException(ObservationFailed); // cannot classify → fail closed
            }
            catch (IOException exception) when (Ended(exception))
            {
                // ended between the listing and the read (gone, ENOENT, ESRCH): not running
            }
            catch (IOException)
            {
                throw new InvalidOperationException(ObservationFailed); // FIX3 R2: any other I/O error → fail closed
            }
        }
        return rows;
    }

    private const int NoSuchFile = 2;    // ENOENT
    private const int NoSuchProcess = 3; // ESRCH

    // FIX3 R2: "ended" only when it really is — the read said ENOENT / ESRCH.
    // FIX4: no "the directory is gone" branch — Directory.Exists answers false on ANY stat failure, so "gone" was "stat
    // failed"; the read's own errno says whether the process ended.
    internal static bool Ended(IOException exception) =>
        exception is FileNotFoundException or DirectoryNotFoundException
        || (exception.HResult & 0xFFFF) is NoSuchFile or NoSuchProcess;

    // /proc mounted with hidepid=1|2|invisible|noaccess hides other users' processes: the listing would be incomplete.
    internal static bool ProcHidesProcesses(string mountInfo)
    {
        foreach (var line in File.ReadLines(mountInfo))
        {
            var fields = line.Split(' ');
            if (fields.Length < 5 || fields[4] != "/proc") continue;
            var options = string.Join(',', fields.Skip(5));
            var hidden = Regex.Match(options, @"hidepid=([^,\s]+)");
            if (hidden.Success && hidden.Groups[1].Value is not ("0" or "off")) return true;
        }
        return false;
    }
}
