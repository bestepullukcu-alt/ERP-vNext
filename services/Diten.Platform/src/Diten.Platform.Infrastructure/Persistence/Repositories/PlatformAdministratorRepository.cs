using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class PlatformAdministratorRepository
    : GlobalRepository<PlatformAdministrator>, IPlatformAdministratorRepository
{
    public PlatformAdministratorRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.PlatformAdministrators)
    {
    }

    public async Task<bool> ExistsByEmailAsync(string normalizedEmail, Guid? excludeId = null, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<PlatformAdministrator>>
        {
            ExecutionFilter,
            Builders<PlatformAdministrator>.Filter.Eq(x => x.NormalizedEmail, normalizedEmail)
        };

        if (excludeId.HasValue)
        {
            filters.Add(Builders<PlatformAdministrator>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        return await Collection.Find(Builders<PlatformAdministrator>.Filter.And(filters)).AnyAsync(ct);
    }

    public async Task<bool> ExistsByUserNameAsync(string normalizedUserName, Guid? excludeId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(normalizedUserName))
        {
            return false;
        }

        var filters = new List<FilterDefinition<PlatformAdministrator>>
        {
            ExecutionFilter,
            Builders<PlatformAdministrator>.Filter.Eq(x => x.NormalizedUserName, normalizedUserName)
        };

        if (excludeId.HasValue)
        {
            filters.Add(Builders<PlatformAdministrator>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        return await Collection.Find(Builders<PlatformAdministrator>.Filter.And(filters)).AnyAsync(ct);
    }

    public async Task<bool> UpdateAsync(PlatformAdministrator administrator, int expectedVersion, CancellationToken ct = default)
    {
        administrator.UpdatedAt = DateTimeOffset.UtcNow;
        administrator.Version = expectedVersion + 1;

        var filter = Builders<PlatformAdministrator>.Filter.And(
            ExecutionFilter,
            Builders<PlatformAdministrator>.Filter.Eq(x => x.Id, administrator.Id),
            Builders<PlatformAdministrator>.Filter.Eq(x => x.Version, expectedVersion));

        var result = await Collection.ReplaceOneAsync(filter, administrator, cancellationToken: ct);
        return result.ModifiedCount == 1;
    }

    public async Task<bool> SoftDeleteAsync(Guid id, int expectedVersion, string actorName, CancellationToken ct = default)
    {
        var filter = Builders<PlatformAdministrator>.Filter.And(
            ExecutionFilter,
            Builders<PlatformAdministrator>.Filter.Eq(x => x.Id, id),
            Builders<PlatformAdministrator>.Filter.Eq(x => x.Version, expectedVersion));

        var update = Builders<PlatformAdministrator>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
            .Set(x => x.UpdatedBy, actorName)
            .Inc(x => x.Version, 1);

        var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        return result.ModifiedCount == 1;
    }

    public async Task<(IReadOnlyList<PlatformAdministrator> Items, long TotalCount)> QueryAsync(
        PlatformAdministratorQuery query,
        CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<PlatformAdministrator>> { ExecutionFilter };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var escaped = System.Text.RegularExpressions.Regex.Escape(query.Search.Trim());
            var regex = new BsonRegularExpression(escaped, "i");
            filters.Add(Builders<PlatformAdministrator>.Filter.Or(
                Builders<PlatformAdministrator>.Filter.Regex(x => x.Email, regex),
                Builders<PlatformAdministrator>.Filter.Regex(x => x.UserName, regex),
                Builders<PlatformAdministrator>.Filter.Regex(x => x.DisplayName, regex)));
        }

        if (query.Statuses is { Count: > 0 })
        {
            filters.Add(Builders<PlatformAdministrator>.Filter.In(x => x.Status, query.Statuses));
        }

        if (query.ActorTypes is { Count: > 0 })
        {
            filters.Add(Builders<PlatformAdministrator>.Filter.In(x => x.ActorType, query.ActorTypes));
        }

        if (query.Roles is { Count: > 0 })
        {
            filters.Add(Builders<PlatformAdministrator>.Filter.AnyIn(x => x.Roles, query.Roles));
        }

        if (query.InvitationStatuses is { Count: > 0 })
        {
            filters.Add(Builders<PlatformAdministrator>.Filter.In(x => x.InvitationStatus, query.InvitationStatuses));
        }

        if (query.PartnerId.HasValue)
        {
            filters.Add(Builders<PlatformAdministrator>.Filter.Eq(x => x.PartnerId, query.PartnerId.Value));
        }

        var filter = Builders<PlatformAdministrator>.Filter.And(filters);
        var totalCount = await Collection.CountDocumentsAsync(filter, cancellationToken: ct);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var items = await Collection.Find(filter)
            .Sort(BuildSort(query.Sort))
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<PlatformAdministratorStatsSnapshot> GetStatsAsync(CancellationToken ct = default)
    {
        var total = await Collection.CountDocumentsAsync(ExecutionFilter, cancellationToken: ct);
        var active = await CountStatusAsync(AdministratorStatus.Active, ct);
        var suspended = await CountStatusAsync(AdministratorStatus.Suspended, ct);
        var disabled = await CountStatusAsync(AdministratorStatus.Disabled, ct);
        var pending = await Collection.CountDocumentsAsync(
            Builders<PlatformAdministrator>.Filter.And(
                ExecutionFilter,
                Builders<PlatformAdministrator>.Filter.Eq(x => x.InvitationStatus, AdministratorInvitationStatus.PendingInvitation)),
            cancellationToken: ct);

        return new PlatformAdministratorStatsSnapshot(total, active, suspended, disabled, pending);
    }

    private Task<long> CountStatusAsync(AdministratorStatus status, CancellationToken ct) =>
        Collection.CountDocumentsAsync(
            Builders<PlatformAdministrator>.Filter.And(
                ExecutionFilter,
                Builders<PlatformAdministrator>.Filter.Eq(x => x.Status, status)),
            cancellationToken: ct);

    public Task<PlatformAdministrator?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return Task.FromResult<PlatformAdministrator?>(null);
        }

        var email = normalizedEmail.Trim().ToLowerInvariant();
        var emailRegex = new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(email)}$", "i");
        var filter = Builders<PlatformAdministrator>.Filter.And(
            ExecutionFilter,
            Builders<PlatformAdministrator>.Filter.Or(
                Builders<PlatformAdministrator>.Filter.Eq(x => x.NormalizedEmail, email),
                Builders<PlatformAdministrator>.Filter.Regex(x => x.NormalizedEmail, emailRegex),
                Builders<PlatformAdministrator>.Filter.Regex(x => x.Email, emailRegex)));

        return Collection.Find(filter).FirstOrDefaultAsync(ct)!;
    }

    public Task<long> CountActiveSuperAdminsAsync(Guid? excludeId = null, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<PlatformAdministrator>>
        {
            ExecutionFilter,
            Builders<PlatformAdministrator>.Filter.Eq(x => x.Status, AdministratorStatus.Active),
            Builders<PlatformAdministrator>.Filter.AnyEq(x => x.Roles, AdministratorRole.SuperAdmin)
        };

        if (excludeId.HasValue)
        {
            filters.Add(Builders<PlatformAdministrator>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        return Collection.CountDocumentsAsync(
            Builders<PlatformAdministrator>.Filter.And(filters),
            cancellationToken: ct);
    }

    private static SortDefinition<PlatformAdministrator> BuildSort(string? sort)
    {
        var normalized = string.IsNullOrWhiteSpace(sort) ? "displayName" : sort.Trim();
        var descending = normalized.StartsWith("-", StringComparison.Ordinal);
        var field = descending ? normalized[1..] : normalized;

        if (field.Contains(':', StringComparison.Ordinal))
        {
            var parts = field.Split(':', 2, StringSplitOptions.TrimEntries);
            field = parts[0];
            descending = parts.Length > 1 && string.Equals(parts[1], "desc", StringComparison.OrdinalIgnoreCase);
        }

        return field.ToLowerInvariant() switch
        {
            "email" => descending ? Builders<PlatformAdministrator>.Sort.Descending(x => x.Email) : Builders<PlatformAdministrator>.Sort.Ascending(x => x.Email),
            "username" => descending ? Builders<PlatformAdministrator>.Sort.Descending(x => x.UserName) : Builders<PlatformAdministrator>.Sort.Ascending(x => x.UserName),
            "displayname" => descending ? Builders<PlatformAdministrator>.Sort.Descending(x => x.DisplayName) : Builders<PlatformAdministrator>.Sort.Ascending(x => x.DisplayName),
            "actortype" => descending ? Builders<PlatformAdministrator>.Sort.Descending(x => x.ActorType) : Builders<PlatformAdministrator>.Sort.Ascending(x => x.ActorType),
            "status" => descending ? Builders<PlatformAdministrator>.Sort.Descending(x => x.Status) : Builders<PlatformAdministrator>.Sort.Ascending(x => x.Status),
            "invitationstatus" => descending ? Builders<PlatformAdministrator>.Sort.Descending(x => x.InvitationStatus) : Builders<PlatformAdministrator>.Sort.Ascending(x => x.InvitationStatus),
            "lastloginatutc" => TimestampSortPolicy.NewestFirstOnly(descending, Builders<PlatformAdministrator>.Sort.Descending(x => x.LastLoginAtUtc), "lastLoginAtUtc"),
            "createdat" => TimestampSortPolicy.NewestFirstOnly(descending, Builders<PlatformAdministrator>.Sort.Descending(x => x.CreatedAt), "createdAt"),
            "updatedat" => TimestampSortPolicy.NewestFirstOnly(descending, Builders<PlatformAdministrator>.Sort.Descending(x => x.UpdatedAt), "updatedAt"),
            _ => descending ? Builders<PlatformAdministrator>.Sort.Descending(x => x.DisplayName) : Builders<PlatformAdministrator>.Sort.Ascending(x => x.DisplayName)
        };
    }
}
