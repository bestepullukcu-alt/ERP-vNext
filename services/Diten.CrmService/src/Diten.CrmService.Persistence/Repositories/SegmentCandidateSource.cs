using System.Text.RegularExpressions;
using Diten.CrmService.Application.Features.Segmentation.Catalog;
using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// MOD-0167 FU02 Phase-1 / Phase-1.5 read side. Reads the Account / Contact / AccountContactLink /
/// AccountAttributeValue collections as PROJECTIONS, so no MOD-0149 / MOD-0150 file, repository or aggregate changes.
/// <para><b>The pushdown is an over-approximation, on purpose.</b> A criteria node the query cannot express safely
/// contributes "true" instead of being guessed at, so the query returns a SUPERSET of the answer and the in-memory
/// evaluator decides exactly. That is the only translation that is both fast and correct; an exact-looking translation
/// that quietly drops a candidate would be a wrong member list nobody could detect.</para>
/// <para>Two things are deliberately never pushed down. <b>DateTimeOffset comparisons</b>, because the driver stores
/// them as a BSON array and a range filter over an array is the parallel-array / instant-versus-date trap this codebase
/// has been bitten by before. And <b>negations</b> (ne / not-in / a NOT group / a Negate flag), because negating an
/// over-approximation is not an over-approximation — it can exclude a real member. Both are answered exactly in memory
/// instead.</para>
/// <para>String equality is pushed as a case-insensitive anchored regex, so the query can never be STRICTER than the
/// evaluator (which compares case-insensitively) and drop a candidate the rule would have accepted.</para>
/// <para>The ceiling is detected by the SAME query: it reads cap + 1 rows, so exceeding the ceiling costs no extra
/// round-trip and the caller can answer 422 without ever holding a partial list.</para>
/// </summary>
public sealed class SegmentCandidateSource : ISegmentCandidateSource
{
    private const string AccountsCollection = "accounts";
    private const string ContactsCollection = "contacts";
    private const string LinksCollection = "account_contact_links";
    private const string AttributesCollection = "account_attribute_values";

    private readonly IMongoDatabase _database;

    public SegmentCandidateSource(IMongoDatabase database) => _database = database;

    public async Task<SegmentCandidateLoad> LoadCandidatesAsync(
        Guid tenantId,
        string subjectType,
        IReadOnlyList<SegmentCriteriaNode> criteria,
        string matchMode,
        int cap,
        CancellationToken cancellationToken)
    {
        var isContact = string.Equals(
            SegmentSubjectTypes.Normalize(subjectType), SegmentSubjectTypes.Contact, StringComparison.Ordinal);

        var filter = new BsonDocument
        {
            { "TenantId", tenantId.ToString() },
            { "IsDeleted", false }
        };

        var pushdown = BuildPushdown(criteria, matchMode, isContact);
        if (pushdown is not null)
        {
            filter = new BsonDocument("$and", new BsonArray { filter, pushdown });
        }

        var collection = _database.GetCollection<BsonDocument>(
            isContact ? ContactsCollection : AccountsCollection);

        var documents = await collection
            .Find(filter)
            .Limit(cap + 1)
            .ToListAsync(cancellationToken);

        if (documents.Count > cap)
        {
            // Over the ceiling: nothing is returned at all. A truncated list would look exactly like a complete one.
            return new SegmentCandidateLoad(Array.Empty<SegmentSubjectSnapshot>(), true, cap);
        }

        return new SegmentCandidateLoad(
            documents.Select(d => ToSnapshot(d, isContact)).ToList(), false, cap);
    }

    public async Task<IReadOnlyList<SegmentSubjectSnapshot>> LoadSubjectsByIdsAsync(
        Guid tenantId,
        string subjectType,
        IReadOnlyCollection<Guid> subjectIds,
        CancellationToken cancellationToken)
    {
        if (subjectIds.Count == 0)
        {
            return Array.Empty<SegmentSubjectSnapshot>();
        }

        var isContact = string.Equals(
            SegmentSubjectTypes.Normalize(subjectType), SegmentSubjectTypes.Contact, StringComparison.Ordinal);

        var filter = new BsonDocument
        {
            { "TenantId", tenantId.ToString() },
            { "IsDeleted", false },
            { "_id", new BsonDocument("$in", new BsonArray(subjectIds.Distinct().Select(id => id.ToString()))) }
        };

        var documents = await _database
            .GetCollection<BsonDocument>(isContact ? ContactsCollection : AccountsCollection)
            .Find(filter)
            .ToListAsync(cancellationToken);

        return documents.Select(d => ToSnapshot(d, isContact)).ToList();
    }

    public async Task<IReadOnlyList<SegmentLinkProjection>> LoadLinksAsync(
        Guid tenantId,
        string subjectType,
        IReadOnlyCollection<Guid> subjectIds,
        CancellationToken cancellationToken)
    {
        if (subjectIds.Count == 0)
        {
            return Array.Empty<SegmentLinkProjection>();
        }

        var isContact = string.Equals(
            SegmentSubjectTypes.Normalize(subjectType), SegmentSubjectTypes.Contact, StringComparison.Ordinal);

        var ids = new BsonArray(subjectIds.Distinct().Select(id => id.ToString()));
        var filter = new BsonDocument
        {
            { "TenantId", tenantId.ToString() },
            { "IsDeleted", false },
            { "Status", "active" },
            { isContact ? "ContactId" : "AccountId", new BsonDocument("$in", ids) }
        };

        var links = await _database.GetCollection<BsonDocument>(LinksCollection)
            .Find(filter)
            .ToListAsync(cancellationToken);

        var accountIds = links
            .Select(l => l.GetValue("AccountId", BsonNull.Value).ToString())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct()
            .ToList();

        // ONE further bulk read for the linked account type. Still bulk, still no per-candidate query.
        var accountTypes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (accountIds.Count > 0)
        {
            var accounts = await _database.GetCollection<BsonDocument>(AccountsCollection)
                .Find(new BsonDocument
                {
                    { "TenantId", tenantId.ToString() },
                    { "IsDeleted", false },
                    { "_id", new BsonDocument("$in", new BsonArray(accountIds)) }
                })
                .Project(Builders<BsonDocument>.Projection.Include("AccountType"))
                .ToListAsync(cancellationToken);

            foreach (var account in accounts)
            {
                accountTypes[account["_id"].ToString()!] = ReadString(account, "AccountType");
            }
        }

        return links
            .Select(l =>
            {
                var accountId = l.GetValue("AccountId", BsonNull.Value).ToString();
                return new SegmentLinkProjection(
                    ParseGuid(l.GetValue("ContactId", BsonNull.Value)),
                    ParseGuid(l.GetValue("AccountId", BsonNull.Value)),
                    ReadString(l, "RoleCode") ?? string.Empty,
                    l.GetValue("IsPrimary", false).ToBoolean(),
                    accountId is null ? null : accountTypes.GetValueOrDefault(accountId));
            })
            .ToList();
    }

    public async Task<IReadOnlyList<SegmentAccountAttributeProjection>> LoadAccountAttributesAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken cancellationToken)
    {
        if (accountIds.Count == 0)
        {
            return Array.Empty<SegmentAccountAttributeProjection>();
        }

        var filter = new BsonDocument
        {
            { "TenantId", tenantId.ToString() },
            { "IsDeleted", false },
            {
                "AccountId",
                new BsonDocument("$in", new BsonArray(accountIds.Distinct().Select(id => id.ToString())))
            }
        };

        var rows = await _database.GetCollection<BsonDocument>(AttributesCollection)
            .Find(filter)
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new SegmentAccountAttributeProjection(
                ParseGuid(r.GetValue("AccountId", BsonNull.Value)),
                ReadString(r, "AttributeCode") ?? string.Empty,
                ReadString(r, "Value")))
            .ToList();
    }

    // -----------------------------------------------------------------------------------------------------------
    // Pushdown translation
    // -----------------------------------------------------------------------------------------------------------

    /// <summary>Translates the tree into a native filter, or null meaning "no safe narrowing" (logically true).</summary>
    private static BsonDocument? BuildPushdown(
        IReadOnlyList<SegmentCriteriaNode> criteria, string matchMode, bool isContact)
    {
        if (criteria.Count == 0)
        {
            return null;
        }

        // Guid.Empty stands for "child of the implicit root": a Dictionary cannot take a null key.
        var childrenByParent = criteria.GroupBy(n => n.ParentNodeId ?? Guid.Empty)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<SegmentCriteriaNode>)g.OrderBy(n => n.SortOrder).ToList());

        if (!childrenByParent.TryGetValue(Guid.Empty, out var roots) || roots.Count == 0)
        {
            return null;
        }

        var isAny = string.Equals(
            SegmentMatchModes.Normalize(matchMode), SegmentMatchModes.Any, StringComparison.Ordinal);

        return Combine(roots.Select(n => BuildNode(n, childrenByParent, isContact)).ToList(), isAny);
    }

    private static BsonDocument? BuildNode(
        SegmentCriteriaNode node,
        IReadOnlyDictionary<Guid, IReadOnlyList<SegmentCriteriaNode>> childrenByParent,
        bool isContact)
    {
        // A node-level NOT inverts an over-approximation, which is no longer an over-approximation. Answer it exactly
        // in memory instead of narrowing the query on a guess.
        if (node.Negate)
        {
            return null;
        }

        if (!node.IsGroup())
        {
            return BuildPredicate(node, isContact);
        }

        var op = SegmentGroupOperators.Normalize(node.GroupOperator);
        if (string.Equals(op, SegmentGroupOperators.Not, StringComparison.Ordinal))
        {
            return null;
        }

        var children = childrenByParent.TryGetValue(node.NodeId, out var kids)
            ? kids
            : Array.Empty<SegmentCriteriaNode>();

        if (children.Count == 0)
        {
            return null;
        }

        return Combine(
            children.Select(c => BuildNode(c, childrenByParent, isContact)).ToList(),
            string.Equals(op, SegmentGroupOperators.Or, StringComparison.Ordinal));
    }

    /// <summary>AND drops the unknown parts (still a superset); OR collapses entirely as soon as ONE branch is unknown,
    /// because an OR is only as narrow as its widest branch.</summary>
    private static BsonDocument? Combine(IReadOnlyList<BsonDocument?> parts, bool isOr)
    {
        if (isOr)
        {
            return parts.Any(p => p is null)
                ? null
                : new BsonDocument("$or", new BsonArray(parts.Select(p => p!)));
        }

        var known = parts.Where(p => p is not null).Select(p => p!).ToList();
        return known.Count switch
        {
            0 => null,
            1 => known[0],
            _ => new BsonDocument("$and", new BsonArray(known))
        };
    }

    private static BsonDocument? BuildPredicate(SegmentCriteriaNode node, bool isContact)
    {
        var field = MapField(node.AttributeCode, isContact);
        if (field is null)
        {
            return null;
        }

        var definition = SegmentAttributeCatalog.Find(node.AttributeCode);
        if (definition is null)
        {
            return null;
        }

        // Dates never enter a Mongo filter: DateTimeOffset is a BSON array here.
        if (string.Equals(definition.ValueType, SegmentValueTypes.Date, StringComparison.Ordinal))
        {
            return null;
        }

        var op = SegmentOperators.Normalize(node.Operator);
        var values = node.Values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).ToList();
        if (values.Count == 0)
        {
            return null;
        }

        return op switch
        {
            SegmentOperators.Eq => new BsonDocument(field, ValueMatcher(values[0], definition.ValueType)),
            SegmentOperators.In => new BsonDocument(field,
                new BsonDocument("$in",
                    new BsonArray(values.Select(v => ValueMatcher(v, definition.ValueType))))),
            SegmentOperators.Contains => new BsonDocument(field,
                new BsonRegularExpression(Regex.Escape(values[0]), "i")),

            // ne / not-in / is-null / is-not-null are negations over data that may legitimately be missing; they are
            // resolved exactly in memory rather than approximated here.
            _ => null
        };
    }

    /// <summary>Case-insensitive for strings so the query is never STRICTER than the evaluator; exact for a Guid.</summary>
    private static BsonValue ValueMatcher(string value, string valueType)
        => string.Equals(valueType, SegmentValueTypes.String, StringComparison.Ordinal)
            ? new BsonRegularExpression($"^{Regex.Escape(value)}$", "i")
            : new BsonString(value);

    private static string? MapField(string? attributeCode, bool isContact) =>
        (attributeCode ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            SegmentAttributeCatalog.AccountType when !isContact => "AccountType",
            SegmentAttributeCatalog.AccountCategory when !isContact => "AccountCategory",
            SegmentAttributeCatalog.AccountStatus when !isContact => "Status",
            SegmentAttributeCatalog.AccountCountry when !isContact => "CountryRef",
            SegmentAttributeCatalog.AccountCity when !isContact => "CityRef",
            SegmentAttributeCatalog.AccountDistrict when !isContact => "DistrictRef",
            SegmentAttributeCatalog.AccountParentAccount when !isContact => "ParentAccountId",

            SegmentAttributeCatalog.ContactType when isContact => "ContactType",
            SegmentAttributeCatalog.ContactStatus when isContact => "Status",
            SegmentAttributeCatalog.ContactGender when isContact => "Gender",
            SegmentAttributeCatalog.ContactSpecialty when isContact => "Specialty",
            SegmentAttributeCatalog.ContactProfessionalTitle when isContact => "ProfessionalTitle",
            SegmentAttributeCatalog.ContactDepartment when isContact => "Department",
            SegmentAttributeCatalog.ContactCountry when isContact => "CountryRef",
            SegmentAttributeCatalog.ContactCity when isContact => "CityRef",
            SegmentAttributeCatalog.ContactDistrict when isContact => "DistrictRef",
            SegmentAttributeCatalog.ContactPreferredLanguage when isContact => "PreferredLanguage",

            // account.attribute lives in another collection; the join is answered in Phase 2, not guessed at here.
            _ => null
        };

    // -----------------------------------------------------------------------------------------------------------
    // Projection
    // -----------------------------------------------------------------------------------------------------------

    private static SegmentSubjectSnapshot ToSnapshot(BsonDocument document, bool isContact) => new(
        ParseGuid(document.GetValue("_id", BsonNull.Value)),
        isContact ? SegmentSubjectTypes.Contact : SegmentSubjectTypes.Account,
        ReadDisplayName(document, isContact),
        ReadString(document, isContact ? "ContactType" : "AccountType"),
        isContact ? null : ReadString(document, "AccountCategory"),
        ReadString(document, "Status"),
        ReadString(document, "CountryRef"),
        ReadString(document, "CityRef"),
        ReadString(document, "DistrictRef"),
        isContact ? null : ReadNullableGuid(document, "ParentAccountId"),
        ReadDateTimeOffset(document, "CreatedAt"),
        isContact ? ReadString(document, "Specialty") : null,
        isContact ? ReadString(document, "ProfessionalTitle") : null,
        isContact ? ReadString(document, "Department") : null,
        isContact ? ReadString(document, "Gender") : null,
        isContact ? ReadString(document, "PreferredLanguage") : null);

    /// <summary>The readable label for one candidate, taken from the SAME document the pushdown already returned — no
    /// extra query, no extra round-trip. A contact keeps its stored DisplayName and only falls back to first + last
    /// name when that field is empty; an account uses its name.</summary>
    private static string? ReadDisplayName(BsonDocument document, bool isContact)
    {
        if (!isContact)
        {
            return ReadString(document, "AccountName");
        }

        var display = ReadString(document, "DisplayName");
        if (!string.IsNullOrWhiteSpace(display))
        {
            return display;
        }

        var composed = string.Join(' ',
            new[] { ReadString(document, "FirstName"), ReadString(document, "LastName") }
                .Where(part => !string.IsNullOrWhiteSpace(part)));

        return string.IsNullOrWhiteSpace(composed) ? null : composed;
    }

    private static string? ReadString(BsonDocument document, string name)
    {
        if (!document.TryGetValue(name, out var value) || value.IsBsonNull)
        {
            return null;
        }

        var text = value.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static Guid ParseGuid(BsonValue value)
        => value.IsBsonNull ? Guid.Empty : Guid.TryParse(value.ToString(), out var id) ? id : Guid.Empty;

    private static Guid? ReadNullableGuid(BsonDocument document, string name)
    {
        if (!document.TryGetValue(name, out var value) || value.IsBsonNull)
        {
            return null;
        }

        return Guid.TryParse(value.ToString(), out var id) ? id : null;
    }

    /// <summary>DateTimeOffset is stored by the driver as a two-element BSON array (ticks, offset minutes). It is read
    /// back here rather than compared in the query, which is exactly why no date criterion is ever pushed down.</summary>
    private static DateTimeOffset ReadDateTimeOffset(BsonDocument document, string name)
    {
        if (!document.TryGetValue(name, out var value) || value.IsBsonNull)
        {
            return default;
        }

        if (value.IsBsonArray && value.AsBsonArray.Count == 2)
        {
            var ticks = value.AsBsonArray[0].ToInt64();
            var offsetMinutes = value.AsBsonArray[1].ToInt32();
            return new DateTimeOffset(ticks, TimeSpan.FromMinutes(offsetMinutes));
        }

        if (value.IsValidDateTime)
        {
            return new DateTimeOffset(value.ToUniversalTime(), TimeSpan.Zero);
        }

        return DateTimeOffset.TryParse(value.ToString(), out var parsed) ? parsed : default;
    }
}
