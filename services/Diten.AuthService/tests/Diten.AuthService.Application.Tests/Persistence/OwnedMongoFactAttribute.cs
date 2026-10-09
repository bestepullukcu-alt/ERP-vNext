using Xunit;

namespace Diten.AuthService.Application.Tests.Persistence;

/// <summary>
/// WP-AUTH-BL570-01 — tests that need an owned disposable mongod. When the ONE resolver
/// (<see cref="DisposableAuthMongoReplicaSet.ResolveExecutable"/>: <c>DITEN_TEST_MONGOD</c>, or the Windows default
/// install on Windows) finds none, the test is skipped BY NAME and counted as skipped — never a silent pass, never a
/// shared Mongo (FIX1 M6, FIX2 item 6). CI (BL-577) sets the variable and asserts zero skips.
/// </summary>
public static class OwnedMongo
{
    public const string Variable = "DITEN_TEST_MONGOD";

    public static string? SkipReason =>
        DisposableAuthMongoReplicaSet.ResolveExecutable() is null
            ? $"OWNED_TEST_MONGO_UNAVAILABLE: no installed mongod ({Variable} unset and no default install)."
            : null;
}

public sealed class OwnedMongoFactAttribute : FactAttribute
{
    public OwnedMongoFactAttribute() => Skip = OwnedMongo.SkipReason;
}

public sealed class OwnedMongoTheoryAttribute : TheoryAttribute
{
    public OwnedMongoTheoryAttribute() => Skip = OwnedMongo.SkipReason;
}
