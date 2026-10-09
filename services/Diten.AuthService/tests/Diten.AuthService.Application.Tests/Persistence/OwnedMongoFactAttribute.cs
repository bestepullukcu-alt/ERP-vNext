using Xunit;

namespace Diten.AuthService.Application.Tests.Persistence;

/// <summary>
/// WP-AUTH-BL570-01 — a test that needs the owned disposable mongod (<see cref="DisposableAuthMongoReplicaSet"/>). When
/// the fixture's own resolution finds no installed mongod (<c>DITEN_TEST_MONGOD</c>, or the Windows default path) the
/// test is skipped BY NAME and counted as skipped — never a silent pass, never a shared Mongo (FIX1 M6).
/// </summary>
public sealed class OwnedMongoFactAttribute : FactAttribute
{
    public const string Variable = "DITEN_TEST_MONGOD";

    public OwnedMongoFactAttribute()
    {
        var executable = DisposableAuthMongoReplicaSet.ResolveExecutable();
        if (!Path.IsPathFullyQualified(executable) || !File.Exists(executable))
            Skip = $"OWNED_TEST_MONGO_UNAVAILABLE: no installed mongod ({Variable} unset and no default install).";
    }
}
