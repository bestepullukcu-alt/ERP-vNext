using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Testing;

/// <summary>
/// WP-INFRA-AUTH-ACCOUNT-KIND-01 (C1) — the fixture's pre-flight target verification, extracted as PURE decision
/// functions so a guard test can prove the refusal WITHOUT spending a real mongod process on it.
///
/// <para>PPM CT finding this corrects. The fixture's only target check used to run AFTER
/// <c>WebApplicationFactory.Server</c> had already built the host — by the time a mismatch was detected, the
/// production seeder had already run against whatever database the host actually resolved. These three checks run
/// BEFORE the host is built: <see cref="EnsureEnvironmentTookTheOverride"/> (a self-check that the override loop
/// actually applied), <see cref="EnsureRunnerIsNotTheSharedServer"/> (the ephemeral runner did not somehow bind
/// the shared port), and <see cref="EnsureEffectiveConfigurationTargetsTheIsolatedDatabase"/> (the SAME
/// appsettings.json → appsettings.Development.json → user secrets → environment-variable chain Program.cs will
/// read, resolved without building the host, already lands on the isolated database).</para>
/// </summary>
internal static class AccountKindAcceptanceGuard
{
    /// <summary>(1a) — the environment variable the host will read actually holds what the fixture just set.</summary>
    public static void EnsureEnvironmentTookTheOverride(string variableName, string? expectedValue)
    {
        var actual = Environment.GetEnvironmentVariable(variableName);
        if (!string.Equals(actual, expectedValue, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Refusing to start: environment variable '{variableName}' reads '{actual}', expected '{expectedValue}'.");
        }
    }

    /// <summary>
    /// (1b) — the runner bound a LOOPBACK host on a port that is NOT the shared server's 27017. Runs before any
    /// data operation touches the runner.
    /// </summary>
    public static void EnsureRunnerIsNotTheSharedServer(string runnerConnectionString)
    {
        var server = MongoUrl.Create(runnerConnectionString).Servers.Single();

        var isLoopback = string.Equals(server.Host, "127.0.0.1", StringComparison.Ordinal)
            || string.Equals(server.Host, "localhost", StringComparison.OrdinalIgnoreCase);
        if (!isLoopback)
        {
            throw new InvalidOperationException(
                $"Refusing to start: the ephemeral runner's host '{server.Host}' is not a loopback address ({runnerConnectionString}).");
        }

        if (server.Port == 27017)
        {
            throw new InvalidOperationException(
                $"Refusing to start: the ephemeral runner bound the SHARED port 27017 ({runnerConnectionString}).");
        }
    }

    /// <summary>
    /// (1c) — the SAME configuration chain Program.cs will read (appsettings.json → appsettings.Development.json
    /// → user secrets → environment variables) already resolves <c>MongoDbSettings</c> to the isolated fixture
    /// target, computed WITHOUT building the host. <paramref name="effectiveConfiguration"/> is that pre-read
    /// chain; a mismatch — for instance the DatabaseName resolving to the real shared database instead of the
    /// isolated one — is refused here, before a single line of production seed data is written anywhere.
    /// </summary>
    public static void EnsureEffectiveConfigurationTargetsTheIsolatedDatabase(
        IConfiguration effectiveConfiguration,
        string expectedConnectionString,
        string expectedDatabaseName)
    {
        var connectionString = effectiveConfiguration["MongoDbSettings:ConnectionString"];
        var databaseName = effectiveConfiguration["MongoDbSettings:DatabaseName"];

        if (!string.Equals(connectionString, expectedConnectionString, StringComparison.Ordinal)
            || !string.Equals(databaseName, expectedDatabaseName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Refusing to start: the effective configuration resolves MongoDbSettings to "
                + $"'{connectionString}/{databaseName}', not the isolated fixture target "
                + $"'{expectedConnectionString}/{expectedDatabaseName}'.");
        }
    }
}
