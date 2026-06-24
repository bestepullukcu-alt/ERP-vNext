using System;
using System.Threading;
using System.Threading.Tasks;
using Diten.Platform.Domain.Entities.Organization;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

// DEV-ONLY (MOD-0023): assigns the tenant-97c5 workflow operator (bestepullukcu@gmail.com) to the
// CEO position so that picking a Position as a Start Workflow Instance candidate resolves to a real
// user. Without an active PositionAssignment, a position resolves to zero users and Start fails with
// "At least one assignment candidate is required.".
//
// This is a temporary development convenience until a proper Position Assignments admin screen exists.
// The position id (Platform DB) and the user id (Auth DB) are both runtime-generated, so both are
// resolved at startup rather than hardcoded. Idempotent and GUID-safe (subtype-4 / Standard).
public static class PositionAssignmentSeed
{
    private static readonly Guid Tenant97c5Id = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private const string OperatorEmail = "bestepullukcu@gmail.com";
    private const string AuthDatabaseName = "diten_auth_v3";
    private const string CeoPositionCode = "CEO";

    public static async Task EnsureSeededAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        var positions = database.GetCollection<Position>("positions");
        var assignments = database.GetCollection<PositionAssignment>("position_assignments");

        // 1) Resolve the CEO position for tenant-97c5 (Platform DB).
        var ceo = await positions
            .Find(p => p.TenantId == Tenant97c5Id && p.Code == CeoPositionCode && !p.IsDeleted)
            .FirstOrDefaultAsync(ct);
        if (ceo is null)
        {
            Console.WriteLine($"PositionAssignmentSeed: CEO position not found for tenant {Tenant97c5Id}; skipped.");
            return;
        }

        // 2) Resolve the operator user id from the Auth DB (same Mongo server, different database).
        var userId = await ResolveOperatorUserIdAsync(database, ct);
        if (userId is null)
        {
            Console.WriteLine($"PositionAssignmentSeed: operator '{OperatorEmail}' not found in {AuthDatabaseName}; skipped.");
            return;
        }

        // 3) Idempotent: skip if an assignment for this position + user already exists.
        var alreadyAssigned = await assignments
            .Find(a => a.TenantId == Tenant97c5Id && a.PositionId == ceo.Id && a.UserId == userId.Value && !a.IsDeleted)
            .AnyAsync(ct);
        if (alreadyAssigned)
        {
            return;
        }

        var assignment = new PositionAssignment
        {
            TenantId = Tenant97c5Id,
            PositionId = ceo.Id,
            UserId = userId.Value,
            EffectiveFrom = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            EffectiveTo = null,
            CreatedBy = "system-dev-seed"
        };
        await assignments.InsertOneAsync(assignment, cancellationToken: ct);
        Console.WriteLine(
            $"PositionAssignmentSeed: assigned {OperatorEmail} ({userId}) to CEO position {ceo.Id} for tenant {Tenant97c5Id}.");
    }

    private static async Task<Guid?> ResolveOperatorUserIdAsync(IMongoDatabase database, CancellationToken ct)
    {
        var authUsers = database.Client.GetDatabase(AuthDatabaseName).GetCollection<BsonDocument>("users");
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("Email", OperatorEmail),
            Builders<BsonDocument>.Filter.Eq("TenantId", new BsonBinaryData(Tenant97c5Id, GuidRepresentation.Standard)),
            Builders<BsonDocument>.Filter.Ne("IsDeleted", true));

        var userDoc = await authUsers.Find(filter).FirstOrDefaultAsync(ct);
        if (userDoc is null || !userDoc.TryGetValue("_id", out var idValue) || !idValue.IsBsonBinaryData)
        {
            return null;
        }

        return idValue.AsBsonBinaryData.ToGuid(GuidRepresentation.Standard);
    }
}
