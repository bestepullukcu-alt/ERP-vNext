using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Repositories;

internal sealed class AuthCommonUuidCompatibilityGuard
{
    internal const string FailureCode = "AUTH_UUID_MIGRATION_REQUIRED";

    private readonly IMongoDatabase _legacyProbeDatabase;

    internal AuthCommonUuidCompatibilityGuard(IMongoDatabase database)
    {
        var probeSettings = database.Client.Settings.Clone();
#pragma warning disable CS0618
        probeSettings.GuidRepresentation = GuidRepresentation.CSharpLegacy;
#pragma warning restore CS0618
        _legacyProbeDatabase = new MongoClient(probeSettings).GetDatabase(database.DatabaseNamespace.DatabaseName);
    }

    internal async Task EnsureRoleAssignmentVersionCompatibleAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await RequireNoNonStandardMatchAsync(
            "auth_role_assignment_versions",
            "_id",
            tenantId,
            cancellationToken);
    }

    internal async Task EnsureAuthorizationDocumentsCompatibleAsync(
        Guid tenantId,
        Guid roleId,
        Guid permissionId,
        CancellationToken cancellationToken)
    {
        await RequireNoNonStandardMatchAsync("roles", "_id", roleId, cancellationToken);
        await RequireNoNonStandardMatchAsync("roles", "TenantId", tenantId, cancellationToken);
        await RequireNoNonStandardMatchAsync("permissions", "_id", permissionId, cancellationToken);
        await RequireNoNonStandardMatchAsync("rolePermissions", "TenantId", tenantId, cancellationToken);
        await RequireNoNonStandardMatchAsync("rolePermissions", "RoleId", roleId, cancellationToken);
        await RequireNoNonStandardMatchAsync("rolePermissions", "PermissionId", permissionId, cancellationToken);
    }

    private async Task RequireNoNonStandardMatchAsync(
        string collectionName,
        string fieldName,
        Guid expected,
        CancellationToken cancellationToken)
    {
        var collection = _legacyProbeDatabase.GetCollection<BsonDocument>(collectionName);
        var filter = new BsonDocument(fieldName, new BsonBinaryData(expected, GuidRepresentation.CSharpLegacy));
        using var cursor = await collection.FindAsync(filter, cancellationToken: cancellationToken);
        foreach (var document in await cursor.ToListAsync(cancellationToken))
        {
            if (!document.TryGetValue(fieldName, out var value) || !value.IsBsonBinaryData) continue;
            var binary = value.AsBsonBinaryData;
            if (binary.SubType == BsonBinarySubType.UuidStandard) continue;
            if (binary.SubType == BsonBinarySubType.UuidLegacy)
                throw new AuthUuidMigrationRequiredException(collectionName, fieldName);
        }
    }

}

public sealed class AuthUuidMigrationRequiredException(string collectionName, string fieldName)
    : InvalidOperationException($"{AuthCommonUuidCompatibilityGuard.FailureCode}: {collectionName}.{fieldName} requires offline UUID migration to BSON subtype 4")
{
    public string FailureCode => AuthCommonUuidCompatibilityGuard.FailureCode;
}
