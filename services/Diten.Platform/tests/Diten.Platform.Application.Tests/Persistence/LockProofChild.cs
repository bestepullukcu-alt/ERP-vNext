using System.Globalization;
using Diten.Platform.Infrastructure.Persistence.Schema;

namespace Diten.Platform.Application.Tests.Persistence;

/*
 * THE TEST ASSEMBLY'S ENTRY POINT, AND WHY IT HAS ONE OF ITS OWN (BL-395).
 *
 * vstest never calls Main: it loads this assembly as a library. Microsoft.NET.Test.Sdk used to generate an
 * empty Main here; the csproj now sets GenerateProgramFile=false and this one takes its place, so a normal test
 * run behaves exactly as before.
 *
 * It exists so PlatformMongoTestLockTests can prove the lock with REAL SEPARATE PROCESSES running the real code.
 * A lock that only ever meets itself inside one process proves nothing about the other worktree:
 *
 *     dotnet Diten.Platform.Application.Tests.dll lock-proof path
 *     dotnet Diten.Platform.Application.Tests.dll lock-proof hold <lockPath>
 *     dotnet Diten.Platform.Application.Tests.dll lock-proof harness <lockPath> <timeoutSeconds>
 */
internal static class TestAssemblyEntryPoint
{
    public static Task<int> Main(string[] args)
        => args is [LockProofChild.Verb, ..] ? LockProofChild.RunAsync(args[1..]) : Task.FromResult(0);
}

internal static class LockProofChild
{
    public const string Verb = "lock-proof";

    public const int Succeeded = 0;
    public const int Failed = 1;
    public const int Usage = 2;
    public const int TimedOut = 3;
    public const int NotEnforced = 4;

    /// <summary>True only in a process started as <c>… lock-proof …</c>; never inside vstest's testhost.</summary>
    internal static bool IsThisProcess => Environment.GetCommandLineArgs() is [_, Verb, ..];

    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            switch (args)
            {
                case ["path"]:
                    Console.WriteLine($"LOCK_PATH={PlatformMongoTestLock.LockPath}");
                    Console.WriteLine($"TEMP_PATH={Path.GetTempPath()}");
                    return Succeeded;

                case ["hold", var lockPath]:
                {
                    // The same primitive the harness uses, on the proof's own path.
                    var held = await MachineWideFileLock.AcquireAsync(
                        lockPath, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1), Console.Error);
                    Console.WriteLine($"HOLDING pid={Environment.ProcessId}");

                    // Until the parent closes stdin — or kills this process, which is the case that matters.
                    await Console.In.ReadToEndAsync();
                    GC.KeepAlive(held);
                    return Succeeded;
                }

                case ["harness", var lockPath, var timeoutSeconds]:
                {
                    PlatformMongoTestLock.RedirectForLockProofChild(
                        lockPath,
                        TimeSpan.FromSeconds(int.Parse(timeoutSeconds, CultureInfo.InvariantCulture)),
                        TimeSpan.FromSeconds(1));

                    // The real harness entry point — whatever it does before touching Mongo is what is on trial.
                    await using var harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.Meetings);
                    Console.WriteLine($"HARNESS_OPENED database={harness.DatabaseName} pid={Environment.ProcessId}");
                    return Succeeded;
                }

                default:
                    Console.Error.WriteLine(
                        "usage: lock-proof path | hold <lockPath> | harness <lockPath> <timeoutSeconds>");
                    return Usage;
            }
        }
        catch (MachineWideFileLockTimeoutException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return TimedOut;
        }
        catch (MachineWideFileLockNotEnforcedException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return NotEnforced;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return Failed;
        }
    }
}
