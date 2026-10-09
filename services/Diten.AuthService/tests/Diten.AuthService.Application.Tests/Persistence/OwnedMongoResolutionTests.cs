namespace Diten.AuthService.Application.Tests.Persistence;

/// <summary>
/// WP-AUTH-BL570-01 FIX2 item 6 — the ONE owned-mongod resolver every fixture and [OwnedMongoFact] use: the variable
/// first; the Windows default install only on Windows (never a relative "C:/…" path on Linux); otherwise none, and the
/// tests are skipped by name.
/// </summary>
public sealed class OwnedMongoResolutionTests
{
    private const string Installed = "/opt/homebrew/bin/mongod";

    [Theory]
    [InlineData(Installed, false, true, Installed)]
    [InlineData(null, false, true, null)]                      // Unix, no variable: none — never the Windows path
    [InlineData(null, true, true, DisposableAuthMongoReplicaSet.WindowsDefaultMongod)]
    [InlineData(null, true, false, null)]                      // Windows default not installed: none
    [InlineData("bin/mongod", false, true, null)]              // relative: refused
    [InlineData(Installed, false, false, null)]                // named but missing: none
    public void The_owned_mongod_is_resolved_by_one_rule(string? variable, bool windows, bool exists, string? expected)
    {
        Assert.Equal(expected, DisposableAuthMongoReplicaSet.Resolve(variable, windows, _ => exists));
    }
}
