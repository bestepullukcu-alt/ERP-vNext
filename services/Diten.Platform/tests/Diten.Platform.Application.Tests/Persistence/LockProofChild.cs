using System.Globalization;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;

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
                    // ⚠ BL-482 (M1): its OWN fixed scoped database, never the shared one. This child holds only a
                    // proof lock while the parent's live run is using the shared database under the real one.
                    await using var harness = await MongoIntegrationHarness.CreateIsolatedAsync(ChildScope, SchemaProfile.Meetings);
                    Console.WriteLine($"HARNESS_OPENED database={harness.DatabaseName} pid={Environment.ProcessId}");
                    Console.WriteLine($"HOLDS_REAL_LOCK={PlatformMongoTestLock.HoldsRealLock}");
                    return Succeeded;
                }

                case ["heal-refusal", var lockPath]:
                    return await ProveTheHealRefusesAsync(lockPath);

                default:
                    Console.Error.WriteLine(
                        "usage: lock-proof path | hold <lockPath> | harness <lockPath> <timeoutSeconds> | heal-refusal <lockPath>");
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

    /// <summary>The lock-proof child's own fixed scoped database: diten_platform_itest_lock_proof_child.</summary>
    public const string ChildScope = "lock_proof_child";

    /*
     * BL-482 (M1) — THE HEAL, ASKED TO RUN INSIDE A LOCK-PROOF CHILD, MUST REFUSE. The child builds the exact residue
     * the heal exists for (the unique index dropped, a duplicate of a finished tenant written) in ITS OWN scoped
     * database, asks the real heal to repair it, and reports whether it refused and whether the rows are still
     * there. It always clears what it wrote and rebuilds the index, so the next child starts clean.
     */
    private static async Task<int> ProveTheHealRefusesAsync(string lockPath)
    {
        PlatformMongoTestLock.RedirectForLockProofChild(lockPath, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1));

        await using var harness = await MongoIntegrationHarness.CreateIsolatedAsync(ChildScope, SchemaProfile.Meetings);
        var series = harness.Database.GetCollection<BsonDocument>(PlatformCollections.MeetingSeries);
        try
        {
            await series.Indexes.DropOneAsync("ux_meeting_series_tenant_name");
            var finishedTenant = new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard);
            BsonDocument Row() => new()
            {
                { "_id", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
                { "TenantId", finishedTenant },
                { "Name", "BL-482 lock-proof residue" },
                { "IsDeleted", false }
            };
            await series.InsertManyAsync(new[] { Row(), Row() });

            try
            {
                await MongoIntegrationHarness.ApplyProfileHealingResidueAsync(harness.Database, SchemaProfile.Meetings);
                Console.WriteLine("HEAL_SUCCEEDED_IN_CHILD");
            }
            catch (InvalidOperationException refused) when (refused.Message.Contains("not healed", StringComparison.Ordinal))
            {
                var left = await series.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
                Console.WriteLine($"HEAL_REFUSED rows={left} reason={refused.Message}");
            }

            return Succeeded;
        }
        finally
        {
            await series.DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
            await PlatformSchemaManifest.ApplyAsync(harness.Database, new[] { SchemaProfile.Meetings });
        }
    }
}
