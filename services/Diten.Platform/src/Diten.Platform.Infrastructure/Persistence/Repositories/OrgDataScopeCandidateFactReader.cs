using Diten.Platform.Application.Authorization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class OrgDataScopeCandidateFactReader : IOrgDataScopeCandidateFactReader
{
    private readonly IMongoCollection<PositionAssignment> _assignments;
    private readonly IMongoCollection<Position> _positions;
    private readonly IMongoCollection<OrganizationUnit> _organizationUnits;
    private readonly ITenantContext _tenantContext;

    public OrgDataScopeCandidateFactReader(IPlatformDbContext dbContext, ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _assignments = dbContext.GetCollection<PositionAssignment>(PlatformCollections.PositionAssignments);
        _positions = dbContext.GetCollection<Position>(PlatformCollections.Positions);
        _organizationUnits = dbContext.GetCollection<OrganizationUnit>(PlatformCollections.OrganizationUnits);
    }

    public async Task<IReadOnlyList<Guid>> ResolveLegalEntityIdsAsync(
        Guid tenantId,
        Guid userId,
        DateTimeOffset effectiveAtUtc,
        int maxCandidates,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty || _tenantContext.TenantId != tenantId)
        {
            throw new OrgDataScopeCandidateContractException("Candidate fact tenant or subject is invalid.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCandidates);
        var boundedLimit = checked(maxCandidates + 1);

        var assignmentFilter = Builders<PositionAssignment>.Filter.And(
            Builders<PositionAssignment>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<PositionAssignment>.Filter.Eq(x => x.IsDeleted, false),
            Builders<PositionAssignment>.Filter.Eq(x => x.IsCancelled, false),
            Builders<PositionAssignment>.Filter.Eq(x => x.UserId, userId),
            EffectiveAtFilter(effectiveAtUtc));

        var positionIds = await _assignments.Aggregate()
            .Match(assignmentFilter)
            .Group(x => x.PositionId, group => group.Key)
            .Limit(boundedLimit)
            .ToListAsync(cancellationToken);
        EnsureBound(positionIds.Count, maxCandidates, "Active position");
        if (positionIds.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        var positionFilter = Builders<Position>.Filter.And(
            Builders<Position>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<Position>.Filter.Eq(x => x.IsDeleted, false),
            Builders<Position>.Filter.Eq(x => x.IsArchived, false),
            Builders<Position>.Filter.In(x => x.Id, positionIds));
        var organizationUnitIds = (await _positions.Find(positionFilter)
                .Project(x => x.OrganizationUnitId)
                .Limit(boundedLimit)
                .ToListAsync(cancellationToken))
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        EnsureBound(organizationUnitIds.Length, maxCandidates, "Organization Unit");
        if (organizationUnitIds.Length == 0)
        {
            return Array.Empty<Guid>();
        }

        var organizationUnitFilter = Builders<OrganizationUnit>.Filter.And(
            Builders<OrganizationUnit>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<OrganizationUnit>.Filter.Eq(x => x.IsDeleted, false),
            Builders<OrganizationUnit>.Filter.Eq(x => x.IsArchived, false),
            Builders<OrganizationUnit>.Filter.Ne(x => x.LegalEntityId, Guid.Empty),
            Builders<OrganizationUnit>.Filter.In(x => x.Id, organizationUnitIds));
        var legalEntityIds = (await _organizationUnits.Find(organizationUnitFilter)
                .Project(x => x.LegalEntityId)
                .Limit(boundedLimit)
                .ToListAsync(cancellationToken))
            .Distinct()
            .ToArray();
        EnsureBound(legalEntityIds.Length, maxCandidates, "Legal Entity");
        return legalEntityIds;
    }

    private static FilterDefinition<PositionAssignment> EffectiveAtFilter(DateTimeOffset effectiveAtUtc)
    {
        var utcTicks = effectiveAtUtc.UtcDateTime.Ticks;
        var fromUtcTicks = UtcTicksExpression("$EffectiveFrom");
        var toUtcTicks = UtcTicksExpression("$EffectiveTo");
        var expression = new BsonDocument("$expr", new BsonDocument("$and", new BsonArray
        {
            new BsonDocument("$lte", new BsonArray { fromUtcTicks, utcTicks }),
            new BsonDocument("$or", new BsonArray
            {
                new BsonDocument("$eq", new BsonArray { "$EffectiveTo", BsonNull.Value }),
                new BsonDocument("$gt", new BsonArray { toUtcTicks, utcTicks })
            })
        }));
        return new BsonDocumentFilterDefinition<PositionAssignment>(expression);
    }

    private static BsonDocument UtcTicksExpression(string field) =>
        new("$subtract", new BsonArray
        {
            new BsonDocument("$arrayElemAt", new BsonArray { field, 0 }),
            new BsonDocument("$multiply", new BsonArray
            {
                new BsonDocument("$arrayElemAt", new BsonArray { field, 1 }),
                TimeSpan.TicksPerMinute
            })
        });

    private static void EnsureBound(int count, int maxCandidates, string label)
    {
        if (count > maxCandidates)
        {
            throw new OrgDataScopeCandidateContractException($"{label} candidate bound exceeded.");
        }
    }
}
