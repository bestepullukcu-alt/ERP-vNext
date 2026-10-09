using Diten.AuthService.Api.Operational;

namespace Diten.AuthService.Application.Tests.Operational;

/// <summary>
/// WP-AUTH-BL570-01 FIX1 R4 / FIX2 item 7 — the test assembly's entry point, used ONLY by
/// <see cref="OperationalSingleInstanceLockTests"/>: <c>dotnet exec &lt;tests.dll&gt; --hold-operational-lock &lt;name&gt;</c>
/// starts a NEW Unix session itself (<c>setsid()</c> — no python3 or other helper needed), takes the lock, prints
/// <c>HELD &lt;sid&gt;</c>, and holds it until its stdin is closed (no fixed timer: a slow machine cannot make it let go
/// early). Nothing else.
/// </summary>
public static class OperationalLockHolder
{
    public const string Flag = "--hold-operational-lock";

    public static int Main(string[] args)
    {
        if (args.Length != 2 || args[0] != Flag) return 2;
        if (SetSid() < 0) { Console.WriteLine("SETSID_FAILED"); return 4; }
        using var held = OperationalSingleInstanceLock.TryAcquire(args[1]);
        Console.WriteLine((held.Acquired ? "HELD " : "REFUSED ") + GetSid(0));
        Console.Out.Flush();
        if (!held.Acquired) return 3;
        _ = Console.In.ReadToEnd(); // until the test closes stdin
        return 0;
    }

    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "getsid")]
    public static extern int GetSid(int pid);

    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "setsid")]
    private static extern int SetSid();
}
