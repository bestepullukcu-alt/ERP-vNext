using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.S2S;

internal static class Mod0018S2STestDatabase
{
    internal const string Name = "diten_authservice_mod0018_s2s_tests";

    private static readonly string[] OwnedCollections =
    [
        "servicePrincipals",
        "serviceCredentialDescriptors",
        "s2sReplayReceipts",
        "s2sPermissionCatalogManifests",
        "s2sPermissionCatalogOperations",
        "s2sPermissionCatalogPermissionOwners",
        "s2sPermissionCatalogRegistrationAudits",
        "permissions",
        "roles",
        "rolePermissions",
        "roleAssignmentVersions",
        "entitlementStates",
        "explicitRoleGrantProvisioningReceipts",
        "explicitRoleGrantProvisioningAudits",
        "auth_role_assignment_versions"
    ];

    internal static string RequireUri()
    {
        var uri = Environment.GetEnvironmentVariable("MONGO_TEST_URI")
                  ?? throw new InvalidOperationException("MONGO_TEST_URI must point to a disposable local MongoDB on a dynamic port >= 27022.");
        var url = new MongoUrl(uri);
        if (!url.Servers.Any() ||
            url.Servers.Any(server =>
                server.Port < 27022 ||
                !string.Equals(server.Host, "127.0.0.1", StringComparison.Ordinal) &&
                !string.Equals(server.Host, "localhost", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("MOD-0018 S2S Mongo evidence requires a disposable loopback endpoint on a dynamic port >= 27022.");
        return uri;
    }

    internal static MongoClient CreateClient()
    {
        var settings = MongoClientSettings.FromConnectionString(RequireUri());
#pragma warning disable CS0618
        settings.GuidRepresentation = MongoDB.Bson.GuidRepresentation.Standard;
#pragma warning restore CS0618
        return new MongoClient(settings);
    }

    internal static async Task<IMongoDatabase> ResetAsync(IMongoClient client)
    {
        var database = client.GetDatabase(Name);
        foreach (var collectionName in OwnedCollections)
            await database.GetCollection<BsonDocument>(collectionName)
                .DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
        return database;
    }
}
