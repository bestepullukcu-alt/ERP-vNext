using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;

namespace Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling.Catalog;

public static class CatalogMongoCollections
{
    public const string Architectures = "mg_process_architectures";
    public const string Domains = "mg_process_domains";
    public const string Families = "mg_process_families";
    public const string Definitions = "mg_process_definitions";
    public const string Receipts = "mg_process_modeling_idempotency_receipts";
    public const string AuditIntents = "mg_process_modeling_audit_intents";
    public const string Outbox = "mg_process_modeling_outbox_messages";

    public static IReadOnlySet<string> Business { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        Architectures, Domains, Families, Definitions
    };
}

public sealed class CatalogMongoContext
{
    private readonly IMongoDatabase _database;

    public CatalogMongoContext(IMongoClient client, string databaseName)
    {
        Client = client ?? throw new ArgumentNullException(nameof(client));
        if (string.IsNullOrWhiteSpace(databaseName)) throw new ArgumentException("Database is required.", nameof(databaseName));
        _database = client.GetDatabase(databaseName);
    }

    public IMongoClient Client { get; }
    public IMongoCollection<BsonDocument> Collection(string name) =>
        CatalogMongoCollections.Business.Contains(name) || name is CatalogMongoCollections.Receipts or CatalogMongoCollections.AuditIntents or CatalogMongoCollections.Outbox
            ? _database.GetCollection<BsonDocument>(name)
            : throw new InvalidOperationException("process_modeling_catalog_collection_forbidden");
}

public sealed record CatalogMongoIndex(string Name, string Collection, IReadOnlyList<string> Keys);

public static class CatalogMongoManifest
{
    public static IReadOnlyList<CatalogMongoIndex> Indexes { get; } =
    [
        new("ux_pm_catalog_architecture_code", CatalogMongoCollections.Architectures, ["TenantId", "ArchitectureCode"]),
        new("ux_pm_catalog_domain_code", CatalogMongoCollections.Domains, ["TenantId", "ProcessArchitectureId", "DomainCode"]),
        new("ux_pm_catalog_family_code", CatalogMongoCollections.Families, ["TenantId", "ProcessDomainId", "FamilyCode"]),
        new("ux_pm_catalog_definition_code", CatalogMongoCollections.Definitions, ["TenantId", "ProcessCode"])
    ];
}

public sealed class CatalogMongoIndexInitializer(CatalogMongoContext context)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        foreach (var index in CatalogMongoManifest.Indexes)
        {
            var keys = new BsonDocument(index.Keys.Select(key => new BsonElement(key, 1)));
            var options = new CreateIndexOptions<BsonDocument>
            {
                Name = index.Name,
                Unique = true,
                PartialFilterExpression = new BsonDocument("IsDeleted", false)
            };
            await context.Collection(index.Collection).Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(keys, options), cancellationToken: cancellationToken);
        }
    }
}

public sealed record CatalogMongoMutation(
    Guid TenantId,
    Guid ActorId,
    string Permission,
    string CommandName,
    string IdempotencyKey,
    string PayloadHash,
    Guid AggregateId,
    string Collection,
    BsonDocument BusinessDocument,
    DateTime OccurredAtUtc,
    int? ExpectedVersion = null);

public sealed record CatalogTreeDocuments(
    IReadOnlyList<BsonDocument> Architectures,
    IReadOnlyList<BsonDocument> Domains,
    IReadOnlyList<BsonDocument> Families,
    IReadOnlyList<BsonDocument> Definitions);

public enum CatalogMutationParticipant { Business, Receipt, AuditIntent, Outbox }

public sealed class CatalogMongoStore(
    CatalogMongoContext context,
    Action<CatalogMutationParticipant>? testOnlyFault = null,
    Func<IClientSessionHandle, int, CancellationToken, Task>? testOnlyCommit = null)
{
    private static readonly TransactionOptions RequiredTransaction = new(
        ReadConcern.Snapshot, ReadPreference.Primary, WriteConcern.WMajority);

    public async Task<BsonDocument?> FindByIdAsync(
        string collection, Guid tenantId, Guid id, CancellationToken cancellationToken = default) =>
        await context.Collection(RequireBusinessCollection(collection))
            .Find(Visible(tenantId) & Builders<BsonDocument>.Filter.Eq("_id", GuidValue(id)))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<CatalogTreeDocuments> ReadTreeAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var filter = Visible(tenantId);
        var bySortOrder = Builders<BsonDocument>.Sort.Ascending("SortOrder");
        var byName = Builders<BsonDocument>.Sort.Ascending("Name");
        return new(
            await context.Collection(CatalogMongoCollections.Architectures).Find(filter).Sort(bySortOrder).ToListAsync(cancellationToken),
            await context.Collection(CatalogMongoCollections.Domains).Find(filter).Sort(bySortOrder).ToListAsync(cancellationToken),
            await context.Collection(CatalogMongoCollections.Families).Find(filter).Sort(bySortOrder).ToListAsync(cancellationToken),
            await context.Collection(CatalogMongoCollections.Definitions).Find(filter).Sort(byName).ToListAsync(cancellationToken));
    }

    public async Task<CatalogMongoMutationResult> MutateAsync(CatalogMongoMutation mutation, CancellationToken cancellationToken = default)
    {
        Validate(mutation);
        var replay = await ReadReceiptAsync(mutation, cancellationToken);
        if (replay is not null) return replay with { Replayed = true };

        using var session = await context.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction(RequiredTransaction);
        try
        {
            var result = await WriteBusinessAsync(session, mutation, cancellationToken);
            await WriteEvidenceAsync(session, mutation, result.Version, cancellationToken);
            await CommitWithRetryAsync(session, cancellationToken);
            return result;
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            await AbortQuietlyAsync(session, cancellationToken);
            var duplicateReplay = await ReadReceiptAsync(mutation, cancellationToken);
            return duplicateReplay ?? throw new CatalogConflictException("process_modeling_catalog_conflict");
        }
        catch (CatalogException)
        {
            await AbortQuietlyAsync(session, cancellationToken);
            throw;
        }
        catch (MongoException exception)
        {
            await AbortQuietlyAsync(session, cancellationToken);
            throw new CatalogUnavailableException("process_modeling_catalog_transaction_unavailable", exception);
        }
        catch
        {
            await AbortQuietlyAsync(session, cancellationToken);
            throw;
        }
    }

    private async Task<CatalogMongoMutationResult> WriteBusinessAsync(IClientSessionHandle session, CatalogMongoMutation mutation, CancellationToken ct)
    {
        var collection = context.Collection(RequireBusinessCollection(mutation.Collection));
        if (mutation.ExpectedVersion is null)
        {
            var document = mutation.BusinessDocument.DeepClone().AsBsonDocument;
            await EnsureActiveParentAsync(session, mutation, document, ct);
            document["_id"] = GuidValue(mutation.AggregateId);
            document["TenantId"] = GuidValue(mutation.TenantId);
            document["IsDeleted"] = false;
            document["Version"] = 0;
            await collection.InsertOneAsync(session, document, cancellationToken: ct);
            testOnlyFault?.Invoke(CatalogMutationParticipant.Business);
            return new(mutation.AggregateId, 0, false);
        }

        var filter = Visible(mutation.TenantId)
                     & Builders<BsonDocument>.Filter.Eq("_id", GuidValue(mutation.AggregateId))
                     & Builders<BsonDocument>.Filter.Eq("Version", mutation.ExpectedVersion.Value)
                     & Builders<BsonDocument>.Filter.Eq("LifecycleState", "Active");
        var values = mutation.BusinessDocument.DeepClone().AsBsonDocument;
        values.Remove("_id"); values.Remove("TenantId"); values.Remove("IsDeleted"); values.Remove("DeletedAtUtc"); values.Remove("Version");
        var update = new BsonDocument("$set", values).Add("$inc", new BsonDocument("Version", 1));
        var outcome = await collection.UpdateOneAsync(session, filter, update, cancellationToken: ct);
        if (outcome.ModifiedCount != 1)
        {
            var visible = await collection.Find(session, Visible(mutation.TenantId) & Builders<BsonDocument>.Filter.Eq("_id", GuidValue(mutation.AggregateId))).AnyAsync(ct);
            if (!visible) throw new CatalogNotFoundException();
            throw new CatalogConflictException("process_modeling_catalog_stale_version");
        }
        testOnlyFault?.Invoke(CatalogMutationParticipant.Business);
        return new(mutation.AggregateId, checked(mutation.ExpectedVersion.Value + 1), false);
    }

    private async Task EnsureActiveParentAsync(IClientSessionHandle session, CatalogMongoMutation mutation, BsonDocument document, CancellationToken ct)
    {
        var parent = mutation.Collection switch
        {
            CatalogMongoCollections.Domains => (CatalogMongoCollections.Architectures, "ProcessArchitectureId"),
            CatalogMongoCollections.Families => (CatalogMongoCollections.Domains, "ProcessDomainId"),
            CatalogMongoCollections.Definitions => (CatalogMongoCollections.Families, "ProcessFamilyId"),
            _ => default
        };
        if (parent == default) return;
        if (!document.TryGetValue(parent.Item2, out var parentId)) throw new ArgumentException("Parent is required.");
        var filter = Visible(mutation.TenantId)
                     & Builders<BsonDocument>.Filter.Eq("_id", parentId)
                     & Builders<BsonDocument>.Filter.Eq("LifecycleState", "Active");
        if (!await context.Collection(parent.Item1).Find(session, filter).AnyAsync(ct)) throw new CatalogNotFoundException();
    }

    private async Task WriteEvidenceAsync(IClientSessionHandle session, CatalogMongoMutation mutation, int version, CancellationToken ct)
    {
        var receiptId = Guid.NewGuid();
        await context.Collection(CatalogMongoCollections.Receipts).InsertOneAsync(session, new BsonDocument
        {
            { "_id", GuidValue(receiptId) }, { "TenantId", GuidValue(mutation.TenantId) }, { "CommandName", mutation.CommandName },
            { "IdempotencyKey", mutation.IdempotencyKey }, { "PayloadHash", mutation.PayloadHash }, { "AggregateId", GuidValue(mutation.AggregateId) },
            { "Version", version }, { "CreatedAtUtc", mutation.OccurredAtUtc }
        }, cancellationToken: ct);
        testOnlyFault?.Invoke(CatalogMutationParticipant.Receipt);
        var auditIntentId = Guid.NewGuid();
        await context.Collection(CatalogMongoCollections.AuditIntents).InsertOneAsync(session, new BsonDocument
        {
            { "_id", GuidValue(auditIntentId) }, { "TenantId", GuidValue(mutation.TenantId) }, { "AggregateId", GuidValue(mutation.AggregateId) },
            { "ActorId", GuidValue(mutation.ActorId) }, { "Permission", mutation.Permission }, { "CommandName", mutation.CommandName },
            { "OccurredAtUtc", mutation.OccurredAtUtc }, { "State", "Pending" }
        }, cancellationToken: ct);
        testOnlyFault?.Invoke(CatalogMutationParticipant.AuditIntent);
        await context.Collection(CatalogMongoCollections.Outbox).InsertOneAsync(session, new BsonDocument
        {
            { "_id", GuidValue(Guid.NewGuid()) }, { "TenantId", GuidValue(mutation.TenantId) }, { "AggregateId", GuidValue(mutation.AggregateId) },
            { "AuditIntentId", GuidValue(auditIntentId) }, { "EventType", mutation.CommandName + ".accepted" },
            { "OccurredAtUtc", mutation.OccurredAtUtc }, { "State", "Pending" }
        }, cancellationToken: ct);
        testOnlyFault?.Invoke(CatalogMutationParticipant.Outbox);
    }

    private async Task<CatalogMongoMutationResult?> ReadReceiptAsync(CatalogMongoMutation mutation, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("TenantId", GuidValue(mutation.TenantId))
                     & Builders<BsonDocument>.Filter.Eq("CommandName", mutation.CommandName)
                     & Builders<BsonDocument>.Filter.Eq("IdempotencyKey", mutation.IdempotencyKey);
        var receipt = await context.Collection(CatalogMongoCollections.Receipts).Find(filter).FirstOrDefaultAsync(ct);
        if (receipt is null) return null;
        if (receipt["PayloadHash"].AsString != mutation.PayloadHash || receipt["AggregateId"].AsGuid != mutation.AggregateId)
            throw new CatalogConflictException("process_modeling_catalog_idempotency_conflict");
        return new(receipt["AggregateId"].AsGuid, receipt["Version"].AsInt32, true);
    }

    private async Task CommitWithRetryAsync(IClientSessionHandle session, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (testOnlyCommit is null) await session.CommitTransactionAsync(ct);
                else await testOnlyCommit(session, attempt + 1, ct);
                return;
            }
            catch (MongoException exception) when (exception.HasErrorLabel("UnknownTransactionCommitResult")) { }
            catch (CatalogUnknownCommitException) { }
        }
        throw new CatalogUnavailableException("process_modeling_catalog_commit_indeterminate");
    }

    private static async Task AbortQuietlyAsync(IClientSessionHandle session, CancellationToken ct)
    {
        if (!session.IsInTransaction) return;
        try { await session.AbortTransactionAsync(ct); } catch (MongoException) { }
    }

    private static FilterDefinition<BsonDocument> Visible(Guid tenantId) =>
        Builders<BsonDocument>.Filter.Eq("TenantId", GuidValue(tenantId)) & Builders<BsonDocument>.Filter.Eq("IsDeleted", false);

    private static string RequireBusinessCollection(string collection) =>
        CatalogMongoCollections.Business.Contains(collection) ? collection : throw new ArgumentException("Invalid catalog collection.", nameof(collection));

    private static BsonBinaryData GuidValue(Guid value) => new(value, GuidRepresentation.Standard);

    private static void Validate(CatalogMongoMutation mutation)
    {
        if (mutation.TenantId == Guid.Empty || mutation.ActorId == Guid.Empty || mutation.AggregateId == Guid.Empty) throw new ArgumentException("Trusted identifiers are required.");
        if (string.IsNullOrWhiteSpace(mutation.Permission) || string.IsNullOrWhiteSpace(mutation.CommandName) || string.IsNullOrWhiteSpace(mutation.IdempotencyKey) || string.IsNullOrWhiteSpace(mutation.PayloadHash)) throw new ArgumentException("Mutation metadata is required.");
        if (mutation.OccurredAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("OccurredAtUtc must be UTC.");
        RequireBusinessCollection(mutation.Collection);
    }
}

public sealed record CatalogMongoMutationResult(Guid AggregateId, int Version, bool Replayed);

public sealed class MongoCatalogStore(CatalogMongoStore store) : ICatalogStore
{
    public async Task<CatalogResponse<CatalogMutationResult>> MutateAsync(
        CatalogMutation mutation, CatalogCommandContext commandContext, CancellationToken cancellationToken)
    {
        try
        {
            var (collection, commandName) = Describe(mutation.Kind);
            var business = BusinessDocument(mutation);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(mutation))));
            var result = await store.MutateAsync(new CatalogMongoMutation(
                commandContext.TenantId, commandContext.SubjectId, commandContext.Permission, commandName,
                commandContext.IdempotencyKey, hash, mutation.EntityId, collection, business,
                DateTime.UtcNow, mutation.ExpectedVersion), cancellationToken);
            return CatalogResponse<CatalogMutationResult>.Success(new(result.AggregateId, result.Version), mutation.ExpectedVersion is null ? 201 : 200);
        }
        catch (CatalogNotFoundException) { return CatalogResponse<CatalogMutationResult>.Fail(CatalogErrors.NotFound, 404); }
        catch (CatalogConflictException) { return CatalogResponse<CatalogMutationResult>.Fail(CatalogErrors.Conflict, 409); }
        catch (CatalogUnavailableException) { return CatalogResponse<CatalogMutationResult>.Fail(CatalogErrors.Unavailable, 503); }
        catch (MongoException) { return CatalogResponse<CatalogMutationResult>.Fail(CatalogErrors.Unavailable, 503); }
    }

    public async Task<CatalogResponse<CatalogTreeDto>> GetTreeAsync(CatalogQueryContext context, CancellationToken cancellationToken)
    {
        try
        {
            var documents = await store.ReadTreeAsync(context.TenantId, cancellationToken);
            var definitions = documents.Definitions.Select(Definition).ToLookup(x => x.ProcessFamilyId);
            var families = documents.Families.Select(x => Family(x, definitions[x["_id"].AsGuid].ToArray())).ToLookup(x => x.ProcessDomainId);
            var domains = documents.Domains.Select(x => Domain(x, families[x["_id"].AsGuid].ToArray())).ToLookup(x => x.ProcessArchitectureId);
            var architectures = documents.Architectures.Select(x => Architecture(x, domains[x["_id"].AsGuid].ToArray())).ToArray();
            return CatalogResponse<CatalogTreeDto>.Success(new(architectures));
        }
        catch (MongoException) { return CatalogResponse<CatalogTreeDto>.Fail(CatalogErrors.Unavailable, 503); }
    }

    public async Task<CatalogResponse<ProcessDefinitionDto>> GetDefinitionAsync(Guid id, CatalogQueryContext context, CancellationToken cancellationToken)
    {
        try
        {
            var document = await store.FindByIdAsync(CatalogMongoCollections.Definitions, context.TenantId, id, cancellationToken);
            return document is null
                ? CatalogResponse<ProcessDefinitionDto>.Fail(CatalogErrors.NotFound, 404)
                : CatalogResponse<ProcessDefinitionDto>.Success(Definition(document));
        }
        catch (MongoException) { return CatalogResponse<ProcessDefinitionDto>.Fail(CatalogErrors.Unavailable, 503); }
    }

    private static (string Collection, string Command) Describe(CatalogMutationKind kind) => kind switch
    {
        CatalogMutationKind.CreateArchitecture => (CatalogMongoCollections.Architectures, "CreateProcessArchitecture"),
        CatalogMutationKind.UpdateArchitecture => (CatalogMongoCollections.Architectures, "UpdateProcessArchitecture"),
        CatalogMutationKind.ArchiveArchitecture => (CatalogMongoCollections.Architectures, "ArchiveProcessArchitecture"),
        CatalogMutationKind.CreateDomain => (CatalogMongoCollections.Domains, "CreateProcessDomain"),
        CatalogMutationKind.UpdateDomain => (CatalogMongoCollections.Domains, "UpdateProcessDomain"),
        CatalogMutationKind.ArchiveDomain => (CatalogMongoCollections.Domains, "ArchiveProcessDomain"),
        CatalogMutationKind.CreateFamily => (CatalogMongoCollections.Families, "CreateProcessFamily"),
        CatalogMutationKind.UpdateFamily => (CatalogMongoCollections.Families, "UpdateProcessFamily"),
        CatalogMutationKind.ArchiveFamily => (CatalogMongoCollections.Families, "ArchiveProcessFamily"),
        CatalogMutationKind.CreateDefinition => (CatalogMongoCollections.Definitions, "CreateProcessDefinition"),
        CatalogMutationKind.UpdateDefinition => (CatalogMongoCollections.Definitions, "UpdateProcessDefinition"),
        CatalogMutationKind.ArchiveDefinition => (CatalogMongoCollections.Definitions, "ArchiveProcessDefinition"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static BsonDocument BusinessDocument(CatalogMutation mutation)
    {
        var document = new BsonDocument { { "UpdatedAtUtc", DateTime.UtcNow } };
        if (mutation.Kind.ToString().StartsWith("Create", StringComparison.Ordinal))
        {
            document["CreatedAtUtc"] = DateTime.UtcNow;
            document["LifecycleState"] = "Active";
        }
        if (mutation.Kind.ToString().StartsWith("Archive", StringComparison.Ordinal)) document["LifecycleState"] = "Archived";
        if (mutation.Name is not null) document["Name"] = mutation.Name;
        if (mutation.Description is not null) document["Description"] = mutation.Description;
        if (mutation.Purpose is not null) document["Purpose"] = mutation.Purpose;
        if (mutation.SortOrder.HasValue) document["SortOrder"] = mutation.SortOrder.Value;
        if (mutation.ParentId.HasValue)
        {
            var parentField = mutation.Kind switch
            {
                CatalogMutationKind.CreateDomain => "ProcessArchitectureId",
                CatalogMutationKind.CreateFamily => "ProcessDomainId",
                CatalogMutationKind.CreateDefinition => "ProcessFamilyId",
                _ => null
            };
            if (parentField is not null) document[parentField] = new BsonBinaryData(mutation.ParentId.Value, GuidRepresentation.Standard);
        }
        if (mutation.Code is not null)
        {
            var codeField = mutation.Kind switch
            {
                CatalogMutationKind.CreateArchitecture => "ArchitectureCode",
                CatalogMutationKind.CreateDomain => "DomainCode",
                CatalogMutationKind.CreateFamily => "FamilyCode",
                CatalogMutationKind.CreateDefinition => "ProcessCode",
                _ => null
            };
            if (codeField is not null) document[codeField] = mutation.Code;
        }
        return document;
    }

    private static ProcessDefinitionDto Definition(BsonDocument x) => new(x["_id"].AsGuid, x["ProcessFamilyId"].AsGuid, x["ProcessCode"].AsString, x["Name"].AsString, Optional(x, "Purpose"), Optional(x, "Description"), x["LifecycleState"].AsString, x["Version"].AsInt32);
    private static ProcessFamilyDto Family(BsonDocument x, IReadOnlyList<ProcessDefinitionDto> children) => new(x["_id"].AsGuid, x["ProcessDomainId"].AsGuid, x["FamilyCode"].AsString, x["Name"].AsString, Optional(x, "Description"), x["SortOrder"].AsInt32, x["LifecycleState"].AsString, x["Version"].AsInt32, children);
    private static ProcessDomainDto Domain(BsonDocument x, IReadOnlyList<ProcessFamilyDto> children) => new(x["_id"].AsGuid, x["ProcessArchitectureId"].AsGuid, x["DomainCode"].AsString, x["Name"].AsString, Optional(x, "Description"), x["SortOrder"].AsInt32, x["LifecycleState"].AsString, x["Version"].AsInt32, children);
    private static ProcessArchitectureDto Architecture(BsonDocument x, IReadOnlyList<ProcessDomainDto> children) => new(x["_id"].AsGuid, x["ArchitectureCode"].AsString, x["Name"].AsString, Optional(x, "Description"), x["SortOrder"].AsInt32, x["LifecycleState"].AsString, x["Version"].AsInt32, children);
    private static string? Optional(BsonDocument x, string name) => x.TryGetValue(name, out var value) && !value.IsBsonNull ? value.AsString : null;
}

public abstract class CatalogException(string code, Exception? inner = null) : Exception(code, inner);
public sealed class CatalogNotFoundException() : CatalogException("process_modeling_catalog_not_found");
public sealed class CatalogConflictException(string code) : CatalogException(code);
public sealed class CatalogUnavailableException(string code, Exception? inner = null) : CatalogException(code, inner);
public sealed class CatalogUnknownCommitException() : Exception("process_modeling_catalog_commit_indeterminate");
