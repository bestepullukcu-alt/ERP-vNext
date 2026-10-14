using System.Runtime.InteropServices;

namespace Diten.AuthService.Application.Tests.ServiceIdentityTokens;

/// <summary>
/// WP-AUTH-BL570-01 FIX4 — tests whose claim only holds on some Unix hosts are skipped BY NAME elsewhere (xUnit 2 has no
/// runtime skip): CI runs the Auth tests on Ubuntu (BL-590), where uid and gid are often equal and containers run as root.
/// </summary>
internal static class UnixIds
{
    public static uint EffectiveUid => OperatingSystem.IsWindows() ? uint.MaxValue : GetEffectiveUid();
    public static uint EffectiveGid => OperatingSystem.IsWindows() ? uint.MaxValue : GetEffectiveGid();

    [DllImport("libc", EntryPoint = "geteuid")]
    private static extern uint GetEffectiveUid();
    [DllImport("libc", EntryPoint = "getegid")]
    private static extern uint GetEffectiveGid();
}

/// <summary>macOS only (the claim reads a field Linux' check never uses).</summary>
public sealed class MacOsFactAttribute : FactAttribute
{
    public MacOsFactAttribute()
    {
        if (!OperatingSystem.IsMacOS()) Skip = "MACOS_ONLY: Linux decides anonymity by /proc/self/fd (tested on its own).";
    }
}

/// <summary>Unix, and the effective uid differs from the effective gid (a uid offset moved onto the gid must read wrong).</summary>
public sealed class UidDiffersFromGidFactAttribute : FactAttribute
{
    public UidDiffersFromGidFactAttribute()
    {
        if (OperatingSystem.IsWindows()) Skip = "UNIX_ONLY";
        else if (UnixIds.EffectiveUid == UnixIds.EffectiveGid)
            Skip = $"UID_EQUALS_GID: euid {UnixIds.EffectiveUid} = egid — a uid offset moved onto the gid reads the same value here.";
    }
}

/// <summary>Unix, and not root (root reads a mode-000 file: CAP_DAC_OVERRIDE).</summary>
public sealed class NotRootUnixFactAttribute : FactAttribute
{
    public NotRootUnixFactAttribute()
    {
        if (OperatingSystem.IsWindows()) Skip = "UNIX_ONLY";
        else if (UnixIds.EffectiveUid == 0) Skip = "RUNS_AS_ROOT: root reads a mode-000 file (CAP_DAC_OVERRIDE); the refusal is not observable.";
    }
}
