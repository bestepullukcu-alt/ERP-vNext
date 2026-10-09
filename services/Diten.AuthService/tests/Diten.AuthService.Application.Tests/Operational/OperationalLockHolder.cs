using Diten.AuthService.Api.Operational;

namespace Diten.AuthService.Application.Tests.Operational;

/// <summary>
/// WP-AUTH-BL570-01 FIX1 R4 — the test assembly's entry point, used ONLY by
/// <see cref="OperationalSingleInstanceLockTests"/>: <c>dotnet exec &lt;tests.dll&gt; --hold-operational-lock &lt;name&gt; &lt;ms&gt;</c>
/// takes the lock, prints <c>HELD</c> with its session id, holds it, releases it. Nothing else.
/// </summary>
public static class OperationalLockHolder
{
    public const string Flag = "--hold-operational-lock";

    public static int Main(string[] args)
    {
        if (args.Length != 3 || args[0] != Flag) return 2;
        using var held = OperationalSingleInstanceLock.TryAcquire(args[1]);
        Console.WriteLine((held.Acquired ? "HELD " : "REFUSED ") + GetSid(0));
        Console.Out.Flush();
        if (!held.Acquired) return 3;
        Thread.Sleep(int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture));
        return 0;
    }

    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "getsid")]
    public static extern int GetSid(int pid);
}
