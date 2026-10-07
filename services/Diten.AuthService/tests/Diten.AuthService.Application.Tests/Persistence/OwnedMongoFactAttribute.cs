using Xunit;

namespace Diten.AuthService.Application.Tests.Persistence;

/// <summary>
/// WP-AUTH-BL570-01 — a test that needs the owned disposable mongod (<see cref="DisposableAuthMongoReplicaSet"/>). Without
/// <c>DITEN_TEST_MONGOD</c> it is skipped BY NAME and counted as skipped — never a silent pass, never a shared Mongo.
/// </summary>
public sealed class OwnedMongoFactAttribute : FactAttribute
{
    public const string Variable = "DITEN_TEST_MONGOD";

    public OwnedMongoFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable)))
            Skip = $"OWNED_TEST_MONGO_UNAVAILABLE: {Variable} is not set (the path of an installed mongod for a disposable child).";
    }
}
