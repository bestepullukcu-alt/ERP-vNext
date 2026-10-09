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
/// read. A row is an Auth writer when its command line (which carries the executable path) contains
/// <c>Diten.AuthService</c>; this process and the observer child are excluded. The listing must contain pid 1 and this
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

    public static async Task<string> ObserveAsync(CancellationToken ct)
    {
        IReadOnlyList<(int Pid, string CommandLine)> rows;
        int? observer = null;
        if (OperatingSystem.IsMacOS()) (rows, observer) = await ListWithPsAsync(ct);
        else if (OperatingSystem.IsLinux()) rows = ListWithProc();
        else throw new InvalidOperationException(UnsupportedPlatform);
        int[] ports;
        try { ports = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Select(x => x.Port).ToArray(); }
        catch (Exception exception) when (exception is PlatformNotSupportedException or NetworkInformationException)
        { throw new InvalidOperationException(ObservationFailed); }
        return Classify(rows, ports, Environment.ProcessId, observer);
    }

    /// <summary>The decision, apart from how the rows were read (tested on synthetic listings).</summary>
    public static string Classify(IReadOnlyList<(int Pid, string CommandLine)> rows, IReadOnlyCollection<int> listenerPorts,
        int selfPid, int? observerPid)
    {
        if (!rows.Any(row => row.Pid == 1) || !rows.Any(row => row.Pid == selfPid))
            throw new InvalidOperationException(ObservationFailed);
        var writers = rows.Count(row => row.Pid != selfPid && row.Pid != observerPid
                                        && row.CommandLine.Contains(Marker, StringComparison.Ordinal));
        if (writers != 0 || listenerPorts.Contains(AuthPort)) throw new InvalidOperationException(NotQuiescent);
        return EntitlementReconciliationPlan.Hash(Quiescent);
    }

    private static async Task<(IReadOnlyList<(int, string)> Rows, int Observer)> ListWithPsAsync(CancellationToken ct)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo("/bin/ps")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        }};
        foreach (var argument in new[] { "-A", "-ww", "-o", "pid=,uid=,args=" }) process.StartInfo.ArgumentList.Add(argument);
        if (!process.Start()) throw new InvalidOperationException(ObservationFailed);
        var output = process.StandardOutput.ReadToEndAsync(ct);
        var error = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        var text = await output;
        _ = await error;
        if (process.ExitCode != 0) throw new InvalidOperationException(ObservationFailed);
        var rows = new List<(int, string)>();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var match = PsRow.Match(line);
            if (!match.Success) throw new InvalidOperationException(ObservationFailed);
            rows.Add((int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), match.Groups[3].Value));
        }
        return (rows, process.Id);
    }

    private static IReadOnlyList<(int, string)> ListWithProc()
    {
        if (ProcHidesProcesses()) throw new InvalidOperationException(UnsupportedPlatform);
        var rows = new List<(int, string)>();
        foreach (var directory in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(directory), System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var pid)) continue;
            try
            {
                var bytes = File.ReadAllBytes(Path.Combine(directory, "cmdline"));
                var commandLine = bytes.Length > 0
                    ? Encoding.UTF8.GetString(bytes).Replace('\0', ' ')
                    : File.ReadAllText(Path.Combine(directory, "comm")).Trim(); // kernel thread: no command line
                rows.Add((pid, commandLine));
            }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
            {
                // ended between the listing and the read: not running
            }
            catch (UnauthorizedAccessException)
            {
                throw new InvalidOperationException(ObservationFailed); // cannot classify → fail closed
            }
        }
        return rows;
    }

    // /proc mounted with hidepid=1|2|invisible|noaccess hides other users' processes: the listing would be incomplete.
    private static bool ProcHidesProcesses()
    {
        foreach (var line in File.ReadLines("/proc/self/mountinfo"))
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
