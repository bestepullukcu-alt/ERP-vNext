using System.Text.RegularExpressions;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Models;
using Diten.AuthService.Application.Features.Users.Services;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Repositories;

/// <summary>
/// WP-AUTH-USERS-LIST-QUERY-01 — <see cref="IUserListReader"/> over Mongo. Everything is decided in the database:
/// filter, order, page, counts, and the roles of a whole page (one <c>$lookup</c>). Every query carries the tenant.
///
/// <para><b>The derived status, once.</b> <see cref="StatusExpression"/> is the Mongo form of
/// <c>UserLifecycle.StatusOf</c> (Invited = MustChangePassword ∧ ¬EmailConfirmed ∧ LastLoginAt=null; else Active by
/// <c>IsActive</c>; else Inactive). The status filter, the status order and the summary all evaluate that ONE expression,
/// so they cannot disagree; <c>UserListStatusParityTests</c> holds it equal to <c>StatusOf</c> over every combination.</para>
/// </summary>
public sealed class UserListReader : IUserListReader
{
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<UserRole> _userRoles;
    private readonly IMongoCollection<Role> _roles;

    public UserListReader(IMongoDatabase database)
    {
        _users = database.GetCollection<User>("users");
        _userRoles = database.GetCollection<UserRole>("userRoles");
        _roles = database.GetCollection<Role>("roles");
    }

    // ── the derived status, in aggregation syntax ────────────────────────────────────────────────────────
    // $ifNull folds a MISSING field into the same value as null / false, which is what the entity's defaults mean.
    internal static readonly BsonValue StatusExpression = new BsonDocument("$cond", new BsonArray
    {
        new BsonDocument("$and", new BsonArray
        {
            new BsonDocument("$eq", new BsonArray { new BsonDocument("$ifNull", new BsonArray { "$MustChangePassword", false }), true }),
            new BsonDocument("$eq", new BsonArray { new BsonDocument("$ifNull", new BsonArray { "$EmailConfirmed", false }), false }),
            new BsonDocument("$eq", new BsonArray { new BsonDocument("$ifNull", new BsonArray { "$LastLoginAt", BsonNull.Value }), BsonNull.Value })
        }),
        UserLifecycle.StatusInvited,
        new BsonDocument("$cond", new BsonArray
        {
            new BsonDocument("$eq", new BsonArray { new BsonDocument("$ifNull", new BsonArray { "$IsActive", false }), true }),
            UserLifecycle.StatusActive,
            UserLifecycle.StatusInactive
        })
    });

    // Account kind as its NAME (what the column shows), built from the enum itself — this file only ORDERS accounts, it
    // never names a classification (AccountKindCreationPathsGuardTests). Missing = Unknown: documents older than the
    // field are never rewritten.
    private static readonly BsonValue AccountKindNameExpression = new BsonDocument("$switch", new BsonDocument
    {
        { "branches", new BsonArray(Enum.GetValues<AccountKind>().Where(k => k != AccountKind.Unknown).Select(k =>
            new BsonDocument { { "case", new BsonDocument("$eq", new BsonArray { "$AccountKind", (int)k }) }, { "then", k.ToString() } })) },
        { "default", nameof(AccountKind.Unknown) }
    });

    // CreatedAt is a DateTimeOffset and the service registers no serializer for it, so Mongo holds the driver's default
    // shape: the ARRAY [ticks, offsetMinutes]. Ordering by an array field uses its smallest element (ascending) — the
    // offset, the same for everyone — so a plain "createdAt asc" would not order at all. The order key is the ticks.
    private static readonly BsonValue CreatedAtTicksExpression = new BsonDocument("$cond", new BsonArray
    {
        new BsonDocument("$isArray", "$CreatedAt"),
        new BsonDocument("$arrayElemAt", new BsonArray { "$CreatedAt", 0 }),
        "$CreatedAt"
    });

    private const string SortField = "_sortKey";

    // "en", strength 2: case-insensitive alphabetical order, so "adem" sorts with "Adem" rather than after "Zeynep".
    private static readonly Collation SortCollation = new("en", strength: CollationStrength.Secondary);

    // ── the page ─────────────────────────────────────────────────────────────────────────────────────────

    public async Task<UserListPage> SearchAsync(Guid tenantId, UserListCriteria criteria, CancellationToken ct)
    {
        var filter = BuildFilter(tenantId, criteria);
        var filteredTotal = await _users.CountDocumentsAsync(filter, cancellationToken: ct);

        if (criteria.Sort.Key == UserListSortKey.Natural)
        {
            // The legacy page/pageSize call keeps the collection's own order.
            var natural = await _users.Find(filter).Skip(criteria.Skip).Limit(criteria.Take).ToListAsync(ct);
            return new UserListPage(natural, filteredTotal);
        }

        var pipeline = _users.Aggregate(new AggregateOptions { Collation = SortCollation }).Match(filter);
        var direction = criteria.Sort.Descending ? -1 : 1;
        var sort = new BsonDocument();
        var computed = criteria.Sort.Key switch
        {
            UserListSortKey.Status => StatusExpression,
            UserListSortKey.AccountKind => AccountKindNameExpression,
            UserListSortKey.CreatedAt => CreatedAtTicksExpression,
            _ => null
        };
        if (computed is not null)
        {
            pipeline = pipeline.AppendStage<User>(new BsonDocument("$addFields", new BsonDocument(SortField, computed)));
            sort.Add(SortField, direction);
        }
        else
        {
            sort.Add(FieldOf(criteria.Sort.Key), direction);
        }

        sort.Add("_id", 1); // a total order: without it a tie between two pages can repeat or skip a row
        var ordered = pipeline
            .AppendStage<User>(new BsonDocument("$sort", sort))
            .Skip(criteria.Skip)
            .Limit(criteria.Take);
        if (computed is not null)
        {
            // The helper field must not reach the deserializer: whether an unknown element is tolerated depends on the
            // convention registry having been filled BEFORE the class map froze (measured: it is not, in a test process).
            ordered = ordered.AppendStage<User>(new BsonDocument("$project", new BsonDocument(SortField, 0)));
        }

        return new UserListPage(await ordered.ToListAsync(ct), filteredTotal);
    }

    private static string FieldOf(UserListSortKey key) => key switch
    {
        UserListSortKey.Email => nameof(User.Email),
        UserListSortKey.FirstName => nameof(User.FirstName),
        UserListSortKey.LastName => nameof(User.LastName),
        UserListSortKey.LastLoginAt => nameof(User.LastLoginAt),
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Not an orderable column.")
    };

    private static FilterDefinition<User> BuildFilter(Guid tenantId, UserListCriteria criteria)
    {
        var f = Builders<User>.Filter;
        var all = new List<FilterDefinition<User>>
        {
            f.Eq(u => u.TenantId, tenantId),
            f.Eq(u => u.IsDeleted, false)
        };

        foreach (var token in (criteria.Search ?? string.Empty)
                     .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Take(UserListRules.MaxSearchTokens))
        {
            var pattern = new BsonRegularExpression(TurkishIAware(Regex.Escape(token)), "i");
            all.Add(f.Or(f.Regex(u => u.Email, pattern), f.Regex(u => u.FirstName, pattern), f.Regex(u => u.LastName, pattern)));
        }

        if (criteria.Statuses.Count > 0)
        {
            all.Add(new BsonDocumentFilterDefinition<User>(new BsonDocument("$expr",
                new BsonDocument("$in", new BsonArray { StatusExpression, new BsonArray(criteria.Statuses) }))));
        }

        if (criteria.AccountKinds.Count > 0)
        {
            var kinds = f.In(u => u.AccountKind, criteria.AccountKinds);
            all.Add(criteria.AccountKinds.Contains(AccountKind.Unknown)
                ? f.Or(kinds, f.Exists(u => u.AccountKind, false)) // pre-field documents read as Unknown
                : kinds);
        }

        if (criteria.RestrictToUserIds is not null)
        {
            all.Add(f.In(u => u.Id, criteria.RestrictToUserIds));
        }

        return f.And(all);
    }

    // The "i" option folds I↔i and Ç↔ç but not the Turkish pair: "YILMAZ" would miss "Yılmaz" (ı) and "ÇELİK" would miss
    // "Çelik". Every i-like letter of the (already escaped) token becomes the class of all four, so a Turkish tenant's
    // upper-case search finds its people. The class holds literals only — it adds no regex meaning to the term.
    private static string TurkishIAware(string escaped)
        => string.Concat(escaped.Select(c => c is 'i' or 'ı' or 'I' or 'İ' ? "[iıIİ]" : c.ToString()));

    // ── the counters ─────────────────────────────────────────────────────────────────────────────────────

    // Three queries, none loads users: the live role ids (small), the distinct user ids holding one of them, and ONE
    // $group over the tenant's users. "Holds a role" mirrors what the list shows: an assignment to a live, same-tenant role.
    public async Task<UserListSummary> GetSummaryAsync(Guid tenantId, CancellationToken ct)
    {
        var liveRoleIds = await _roles
            .Find(r => r.TenantId == tenantId && r.IsDeleted == false)
            .Project(r => r.Id)
            .ToListAsync(ct);

        var withRole = liveRoleIds.Count == 0
            ? new List<Guid>()
            : await _userRoles.Distinct(ur => ur.UserId,
                    Builders<UserRole>.Filter.And(
                        Builders<UserRole>.Filter.Eq(ur => ur.TenantId, tenantId),
                        Builders<UserRole>.Filter.Eq(ur => ur.IsDeleted, false),
                        Builders<UserRole>.Filter.In(ur => ur.RoleId, liveRoleIds)),
                    cancellationToken: ct)
                .ToListAsync(ct);

        BsonDocument CountWhere(BsonValue condition)
            => new("$sum", new BsonDocument("$cond", new BsonArray { condition, 1, 0 }));
        BsonDocument IsStatus(string status)
            => new("$eq", new BsonArray { StatusExpression, status });

        var group = new BsonDocument("$group", new BsonDocument
        {
            { "_id", BsonNull.Value },
            { "total", new BsonDocument("$sum", 1) },
            { "active", CountWhere(IsStatus(UserLifecycle.StatusActive)) },
            { "passive", CountWhere(IsStatus(UserLifecycle.StatusInactive)) },
            { "invited", CountWhere(IsStatus(UserLifecycle.StatusInvited)) },
            { "noRole", CountWhere(new BsonDocument("$not", new BsonArray
                {
                    new BsonDocument("$in", new BsonArray { "$_id", new BsonArray(withRole.Select(id => new BsonBinaryData(id, GuidRepresentation.Standard))) })
                })) }
        });

        var row = await _users.Aggregate()
            .Match(Builders<User>.Filter.And(
                Builders<User>.Filter.Eq(u => u.TenantId, tenantId),
                Builders<User>.Filter.Eq(u => u.IsDeleted, false)))
            .AppendStage<BsonDocument>(group)
            .FirstOrDefaultAsync(ct);

        return row is null
            ? new UserListSummary(0, 0, 0, 0, 0)
            : new UserListSummary(row["total"].ToInt64(), row["active"].ToInt64(), row["passive"].ToInt64(), row["invited"].ToInt64(), row["noRole"].ToInt64());
    }

    // ── roles ────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyCollection<Guid>> GetUserIdsHoldingAnyRoleAsync(Guid tenantId, IReadOnlyCollection<Guid> roleIds, CancellationToken ct)
    {
        if (roleIds.Count == 0) return [];

        var holders = await _userRoles.Distinct(ur => ur.UserId,
                Builders<UserRole>.Filter.And(
                    Builders<UserRole>.Filter.Eq(ur => ur.TenantId, tenantId),
                    Builders<UserRole>.Filter.Eq(ur => ur.IsDeleted, false),
                    Builders<UserRole>.Filter.In(ur => ur.RoleId, roleIds)),
                cancellationToken: ct)
            .ToListAsync(ct);
        return holders;
    }

    [BsonIgnoreExtraElements]
    private sealed class UserRoleNameRow
    {
        public Guid UserId { get; set; }
        public string RoleName { get; set; } = string.Empty;
    }

    // ONE aggregate for the whole page: the user's active assignments, joined to the live same-tenant roles.
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetRoleNamesForUsersAsync(Guid tenantId, IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0) return new Dictionary<Guid, IReadOnlyList<string>>();

        var rows = await _userRoles.Aggregate()
            .Match(Builders<UserRole>.Filter.And(
                Builders<UserRole>.Filter.Eq(ur => ur.TenantId, tenantId),
                Builders<UserRole>.Filter.Eq(ur => ur.IsDeleted, false),
                Builders<UserRole>.Filter.In(ur => ur.UserId, userIds)))
            .AppendStage<BsonDocument>(new BsonDocument("$lookup", new BsonDocument
            {
                { "from", "roles" }, { "localField", nameof(UserRole.RoleId) }, { "foreignField", "_id" }, { "as", "role" }
            }))
            .AppendStage<BsonDocument>(new BsonDocument("$unwind", "$role"))
            .AppendStage<BsonDocument>(new BsonDocument("$match", new BsonDocument
            {
                { "role.TenantId", new BsonBinaryData(tenantId, GuidRepresentation.Standard) },
                { "role.IsDeleted", false }
            }))
            .AppendStage<UserRoleNameRow>(new BsonDocument("$project", new BsonDocument
            {
                { "_id", 0 }, { nameof(UserRole.UserId), 1 }, { "RoleName", "$role.Name" }
            }))
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.UserId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(r => r.RoleName).Distinct().OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList());
    }
}
