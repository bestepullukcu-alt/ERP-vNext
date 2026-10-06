// WP-INFRA-AUTH-ACCEPTANCE-HOST-01 — F11 (Aşama F) + G5 (Aşama G): AccountKindAcceptanceGuard.
// EnsureEffectiveConfigurationTargetsTheIsolatedDatabase is a pure, already-parameterized function — it compares
// whatever IConfiguration it is GIVEN against whatever expectedConnectionString/expectedDatabaseName it is GIVEN,
// with no dependency on AuthTestHost at all. These tests call the guard DIRECTLY with fabricated in-memory
// configurations — no AuthTestHost, no mongod, no factory, no write of any kind, no MongoClient at all.
//
// G5 — the comparison is `connectionString differs || databaseName differs`. Stage F made host and database wrong
// at the SAME time, so deleting either half of that `||` stayed green. Each half now has its own case where ONLY
// that half differs: a port-only and a host-only connection-string difference (same database), and a database-only
// difference (same connection string). The matching case stays as the sanity companion. Sabotage (temporary, on
// AccountKindAcceptanceGuard.cs, restored byte-identical): deleting the connection-string half turns the port and
// host cases red; deleting the database half turns the database case red.

using Microsoft.Extensions.Configuration;
using Xunit;

namespace Diten.AuthService.AccountKindAcceptanceHost.Tests;

public sealed class EffectiveTargetGuardTests
{
    private const string ExpectedConnectionString = "mongodb://127.0.0.1:57123";
    private const string ExpectedDatabaseName = "diten_auth_itest_account_kind";

    private static IConfiguration Effective(string connectionString, string databaseName) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MongoDbSettings:ConnectionString"] = connectionString,
                ["MongoDbSettings:DatabaseName"] = databaseName,
            })
            .Build();

    private static Exception? Check(IConfiguration effective) =>
        Record.Exception(() =>
            Diten.AuthService.Application.Tests.Testing.AccountKindAcceptanceGuard.EnsureEffectiveConfigurationTargetsTheIsolatedDatabase(
                effective, ExpectedConnectionString, ExpectedDatabaseName));

    [Fact]
    public void PortOnlyDifference_SameHostSameDatabase_IsRefused()
    {
        var ex = Check(Effective("mongodb://127.0.0.1:41999", ExpectedDatabaseName));

        Assert.IsType<InvalidOperationException>(ex);
        Assert.Contains("Refusing to start", ex!.Message);
    }

    [Fact]
    public void HostOnlyDifference_SamePortSameDatabase_IsRefused()
    {
        var ex = Check(Effective("mongodb://198.51.100.7:57123", ExpectedDatabaseName));

        Assert.IsType<InvalidOperationException>(ex);
        Assert.Contains("Refusing to start", ex!.Message);
    }

    [Fact]
    public void DatabaseOnlyDifference_SameConnectionString_IsRefused()
    {
        var ex = Check(Effective(ExpectedConnectionString, "diten_auth_v3"));

        Assert.IsType<InvalidOperationException>(ex);
        Assert.Contains("Refusing to start", ex!.Message);
    }

    [Fact]
    public void MatchingTarget_IsAccepted_NoExceptionThrown()
    {
        // Sanity companion: the SAME values on both sides must NOT throw — confirms the refusals above are about
        // genuine inequality, not the method always throwing regardless of input.
        Assert.Null(Check(Effective(ExpectedConnectionString, ExpectedDatabaseName)));
    }
}
