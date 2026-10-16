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
    [InlineData(Installed, false, false, true, Installed)]
    [InlineData(null, false, false, true, null)]                      // Unix, no variable: none — never the Windows path
    [InlineData(null, false, true, true, DisposableAuthMongoReplicaSet.WindowsDefaultMongod)]
    [InlineData(null, false, true, false, null)]                      // Windows default not installed: none
    public void The_owned_mongod_is_resolved_by_one_rule(string? variable, bool required, bool windows, bool exists,
        string? expected)
    {
        var resolution = DisposableAuthMongoReplicaSet.Resolve(variable, required, windows, _ => exists);
        Assert.Equal(expected, resolution.Executable);
        Assert.Null(resolution.Failure);
    }

    // FIX3 R4 (a): a variable that is GIVEN but names no installed mongod is red — never the silent skip it was.
    [Theory]
    [InlineData("bin/mongod", true)]          // relative
    [InlineData("", true)]                    // FIX4: empty (an unresolved CI variable)
    [InlineData("   ", true)]
    [InlineData(Installed, false)]            // named but missing
    public void A_given_variable_that_names_no_mongod_is_a_failure_not_a_skip(string variable, bool exists)
    {
        var resolution = DisposableAuthMongoReplicaSet.Resolve(variable, false, false, _ => exists);
        Assert.Null(resolution.Executable);
        Assert.StartsWith(DisposableAuthMongoReplicaSet.InvalidVariable, resolution.Failure);
        Assert.Null(OwnedMongo.SkipReasonFor(resolution)); // not skipped: the fixture's start throws it
    }

    // FIX3 R4 (b) / (c): nothing given and nothing found is a named skip — unless DITEN_REQUIRE_OWNED_MONGO=1.
    [Fact]
    public void Nothing_found_skips_by_name_unless_required()
    {
        var optional = DisposableAuthMongoReplicaSet.Resolve(null, false, false, _ => false);
        Assert.StartsWith("OWNED_TEST_MONGO_UNAVAILABLE", OwnedMongo.SkipReasonFor(optional));

        var required = DisposableAuthMongoReplicaSet.Resolve(null, true, false, _ => false);
        Assert.StartsWith(DisposableAuthMongoReplicaSet.RequiredButUnavailable, required.Failure);
        Assert.Null(OwnedMongo.SkipReasonFor(required));
    }
}
