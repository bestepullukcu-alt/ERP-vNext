// WP-INFRA-AUTH-ACCEPTANCE-HOST-01 (T11) — the seed-failure DETECTION mechanism, tested directly against
// AccountKindAcceptance.AuthTestHost (in-process, no out-of-process host/W2 needed for this part): does the
// fixture's own read-back check actually catch a silently-swallowed production DataSeeder failure, and is it
// reported as AccountKindSeedFailedException (-> seed-failed/4 at the W2 wire level, verified separately) rather
// than being confused with a genuine mongod/pre-flight failure (-> mongo-start-failed/2)?
//
// No production Auth code is touched or modified by these tests. The "silent seeder failure" is simulated by
// directly soft-deleting a permission the production seeder just wrote, via the SAME repository interface
// production code uses (IPermissionRepository.DeleteAsync) — not a raw collection mutation, and not a filesystem
// mutation, so the mechanical rule's ban on inducing failures via filesystem mutation does not apply here.

using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.AuthService.AccountKindAcceptanceHost.Tests;

[Collection("SevenStateSupervisor")]
public sealed class SeedFailureDetectionTests
{
    // ── T11(a) — a genuinely corrupted catalog is detected as seed-failed, not mongo-start-failed ───────────
    [Fact]
    public async Task Corrupted_catalog_key_after_production_seeding_is_detected_as_AccountKindSeedFailedException()
    {
        var dir = Directory.CreateTempSubdirectory("dak-t11-").FullName;
        try
        {
            var ex = await Record.ExceptionAsync(() => AccountKindAcceptance.AuthTestHost.StartWithMongoDataDirectoryAsync(
                dir,
                beforeSeedHookForTesting: async host =>
                {
                    // Runs AFTER the production DataSeeder has already run (the host is fully built), BEFORE
                    // this fixture's own SeedAsync (and its read-back check) executes — simulating exactly the
                    // moment a silently-swallowed production seeding error would leave the catalog incomplete.
                    using var scope = host.Factory.Services.CreateScope();
                    var permissions = scope.ServiceProvider.GetRequiredService<IPermissionRepository>();
                    var manageKey = await permissions.GetByKeyAsync(ExplicitGrantOnlyPermissions.UsersAccountKindManage, CancellationToken.None);
                    Assert.NotNull(manageKey); // sanity: the production seeder DID write it — we are about to undo that
                    await permissions.DeleteAsync(manageKey!.Id, CancellationToken.None);
                }));

            Assert.NotNull(ex);
            // AccountKindSeedFailedException is internal — asserting the concrete type (visible via
            // InternalsVisibleTo) is the whole point: this must NOT be confused with any other failure type.
            Assert.IsType<Diten.AuthService.Application.Tests.Testing.AccountKindSeedFailedException>(ex);
            Assert.Contains("account-kind.manage", ex!.Message);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* own temp dir, best effort */ }
        }
    }

    // ── F3 CORRECTION — this test measures mongod's own inability to start against an unusable data directory
    // (ENOENT on a nonexistent parent path), NOT the C1 §1(c) effective-config isolated-target guard
    // (EnsureEffectiveConfigurationTargetsTheIsolatedDatabase). Renamed to say exactly that. CT's own finding:
    // "ölçtüğü şey mongod'un başlayamaması." Honest gap: the §1(c) guard's comparison runs against the SAME
    // override dictionary InitializeCoreAsync itself just set (by design — see C1/C3 in the class remarks
    // above), so nothing a CALLING test does can make effective-config disagree with the runner's real target
    // without either modifying production-adjacent test internals (InitializeCoreAsync's own override
    // construction) or a filesystem mutation — both out of this test's reach. NO SEPARATE TEST currently proves
    // the §1(c) guard's comparison itself fires on a genuine mismatch; only that a bad mongod data directory is
    // refused before seeding starts (which IS proven, below).
    [Fact]
    public async Task BadMongoDataDirectory_FailsBeforeSeedingStarts_NeverReachesTheSeedHook()
    {
        var hookCalled = false;
        var ex = await Record.ExceptionAsync(() => AccountKindAcceptance.AuthTestHost.StartWithMongoDataDirectoryAsync(
            "/nonexistent-on-purpose-not-a-real-isolated-root/mongo",
            beforeSeedHookForTesting: _ => { hookCalled = true; return Task.CompletedTask; }));

        Assert.NotNull(ex);
        Assert.False(hookCalled, "seeding must never even start when mongod itself could not start");
        Assert.IsNotType<Diten.AuthService.Application.Tests.Testing.AccountKindSeedFailedException>(ex);
    }
}
