using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

/// <summary>
/// MOD-0288-FU02 — both field repositories. They share this file because they share one invariant and one
/// discipline: every mutation is CONDITIONAL. Nothing here calls ReplaceOne on an id alone.
/// </summary>
public sealed class OrganizationFieldDefinitionRepository
    : TenantRepository<OrganizationFieldDefinition>, IOrganizationFieldDefinitionRepository
{
    public OrganizationFieldDefinitionRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.OrganizationFieldDefinitions)
    {
    }

    public async Task<bool> TryUpdateAsync(
        OrganizationFieldDefinition definition,
        int expectedVersion,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        /*
         * ⚠ THE VERSION IS PART OF THE FILTER, NOT A CHECK BEFORE THE WRITE. A read-then-compare would leave
         * the window this exists to close: between the compare and the replace, another writer lands and the
         * replace overwrites it with no diff to notice. Here the database itself decides, and the loser is
         * told so by ModifiedCount == 0.
         */
        definition.Version = expectedVersion + 1;
        definition.UpdatedAt = DateTimeOffset.UtcNow;

        var result = await Collection.ReplaceOneAsync(
            Builders<OrganizationFieldDefinition>.Filter.And(
                ExecutionFilter,
                Builders<OrganizationFieldDefinition>.Filter.Eq(x => x.Id, definition.Id),
                Builders<OrganizationFieldDefinition>.Filter.Eq(x => x.Version, expectedVersion)),
            definition,
            cancellationToken: ct);

        return result.MatchedCount == 1;
    }

}

public sealed class OrganizationFieldValueRepository
    : TenantRepository<OrganizationFieldValue>, IOrganizationFieldValueRepository
{
    public OrganizationFieldValueRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.OrganizationFieldValues)
    {
    }

    public async Task<bool> TryInsertAsync(OrganizationFieldValue value, CancellationToken ct = default)
    {
        try
        {
            await CreateAsync(value, ct);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            /*
             * ⚠ EXPECTED, NOT EXCEPTIONAL. The unique partial index on (TenantId, OrganizationUnitId,
             * DefinitionId) is the enforcement of "one active value per unit per definition", and a second
             * concurrent writer hitting it is the mechanism WORKING. Swallowed into `false` so the handler can
             * answer 409 instead of a 500 that reads like a server fault.
             */
            return false;
        }
    }

    public async Task<bool> TryUpdateAsync(
        OrganizationFieldValue value,
        int expectedVersion,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(value);

        value.Version = expectedVersion + 1;
        value.UpdatedAt = DateTimeOffset.UtcNow;

        var result = await Collection.ReplaceOneAsync(
            Builders<OrganizationFieldValue>.Filter.And(
                ExecutionFilter,
                Builders<OrganizationFieldValue>.Filter.Eq(x => x.Id, value.Id),
                Builders<OrganizationFieldValue>.Filter.Eq(x => x.Version, expectedVersion)),
            value,
            cancellationToken: ct);

        return result.MatchedCount == 1;
    }

    public async Task<bool> TrySoftDeleteAsync(Guid id, int expectedVersion, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var result = await Collection.UpdateOneAsync(
            Builders<OrganizationFieldValue>.Filter.And(
                ExecutionFilter,
                Builders<OrganizationFieldValue>.Filter.Eq(x => x.Id, id),
                Builders<OrganizationFieldValue>.Filter.Eq(x => x.Version, expectedVersion)),
            Builders<OrganizationFieldValue>.Update
                .Set(x => x.IsDeleted, true)
                .Set(x => x.DeletedAt, now)
                .Set(x => x.UpdatedAt, now)
                .Set(x => x.Version, expectedVersion + 1),
            cancellationToken: ct);

        return result.MatchedCount == 1;
    }

    public Task<OrganizationFieldValue?> GetAsync(
        Guid organizationUnitId,
        Guid definitionId,
        CancellationToken ct = default)
        => Collection
            .Find(Builders<OrganizationFieldValue>.Filter.And(
                ExecutionFilter,
                Builders<OrganizationFieldValue>.Filter.Eq(x => x.OrganizationUnitId, organizationUnitId),
                Builders<OrganizationFieldValue>.Filter.Eq(x => x.DefinitionId, definitionId)))
            .FirstOrDefaultAsync(ct)!;

    public async Task<IReadOnlyList<OrganizationFieldValue>> GetByUnitAsync(
        Guid organizationUnitId,
        CancellationToken ct = default)
        => await Collection
            .Find(Builders<OrganizationFieldValue>.Filter.And(
                ExecutionFilter,
                Builders<OrganizationFieldValue>.Filter.Eq(x => x.OrganizationUnitId, organizationUnitId)))
            .ToListAsync(ct);

    public Task<bool> AnyForDefinitionAsync(Guid definitionId, CancellationToken ct = default)
        => Collection
            .Find(Builders<OrganizationFieldValue>.Filter.And(
                ExecutionFilter,
                Builders<OrganizationFieldValue>.Filter.Eq(x => x.DefinitionId, definitionId)))
            .AnyAsync(ct);

    /// <summary>
    /// Executes an ALREADY-VALIDATED spec. ⚠ This method decides no policy: whether a definition may be
    /// filtered, whether an operator suits its type and how large a page may be were all settled in the
    /// handler. A repository that re-decided any of it would be a second opinion, and two opinions is how one
    /// of them silently stops being consulted.
    /// </summary>
    public async Task<IReadOnlyList<OrganizationFieldValue>> QueryAsync(
        OrganizationFieldValueQuerySpec spec,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var b = Builders<OrganizationFieldValue>.Filter;
        var filters = new List<FilterDefinition<OrganizationFieldValue>> { ExecutionFilter };

        if (spec.OrganizationUnitId.HasValue)
        {
            filters.Add(b.Eq(x => x.OrganizationUnitId, spec.OrganizationUnitId.Value));
        }

        foreach (var clause in spec.Filters)
        {
            var definition = b.Eq(x => x.DefinitionId, clause.DefinitionId);
            filters.Add(b.And(definition, ValueClause(clause)));
        }

        var find = Collection.Find(b.And(filters));

        find = spec.SortDefinitionId.HasValue
            ? find.Sort(spec.SortDescending
                ? Builders<OrganizationFieldValue>.Sort.Descending(x => x.Value)
                : Builders<OrganizationFieldValue>.Sort.Ascending(x => x.Value))
            // A stable order even without a sort: an unordered page is a page that changes under the reader.
            : find.Sort(Builders<OrganizationFieldValue>.Sort.Ascending(x => x.Id));

        return await find.Skip(spec.Skip).Limit(spec.Take).ToListAsync(ct);
    }

    private static FilterDefinition<OrganizationFieldValue> ValueClause(OrganizationFieldValueFilterSpec clause)
    {
        var b = Builders<OrganizationFieldValue>.Filter;

        return clause.Operator switch
        {
            OrganizationFieldFilterOperator.Equals => b.Eq(x => x.Value, clause.Values[0]),
            OrganizationFieldFilterOperator.In => b.In(x => x.Value, clause.Values),

            /*
             * ⚠ PREFIX-ANCHORED, AND THE ANCHOR IS NOT DECORATION. `^` is what lets Mongo walk the
             * (TenantId, DefinitionId, Value) index instead of scanning every value in the collection, and the
             * input is Regex-escaped so a user's "." or "*" is a character rather than a pattern.
             */
            OrganizationFieldFilterOperator.StartsWith => b.Regex(
                x => x.Value,
                new MongoDB.Bson.BsonRegularExpression(
                    "^" + System.Text.RegularExpressions.Regex.Escape(clause.Values[0]), "i")),

            OrganizationFieldFilterOperator.LessThan => b.Lt(x => x.Value, clause.Values[0]),
            OrganizationFieldFilterOperator.LessThanOrEqual => b.Lte(x => x.Value, clause.Values[0]),
            OrganizationFieldFilterOperator.GreaterThan => b.Gt(x => x.Value, clause.Values[0]),
            OrganizationFieldFilterOperator.GreaterThanOrEqual => b.Gte(x => x.Value, clause.Values[0]),

            // Unreachable while the enum stays closed, and loud rather than silent if it ever does not.
            _ => throw new NotSupportedException($"Unsupported filter operator '{clause.Operator}'.")
        };
    }
}
