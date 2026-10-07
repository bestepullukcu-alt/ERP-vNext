using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Tests.Audit;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Persistence.Configurations;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// WP-MDM-BL576-01 — every document MDM stores (and every MDM class it embeds) ignores extra elements, so a binary rolled
/// back past a slice that added a field still reads what the newer binary wrote; the maps change nothing else.
/// </summary>
[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class MdmDocumentClassMapsTests : IClassFixture<AuditIntentTemporalMongoFixture>
{
    private readonly AuditIntentTemporalMongoFixture _fixture;

    public MdmDocumentClassMapsTests(AuditIntentTemporalMongoFixture fixture) => _fixture = fixture;

    public static TheoryData<string> Covered()
    {
        var data = new TheoryData<string>();
        foreach (var type in MdmDocumentClassMaps.CoveredTypes) data.Add(type.FullName!);
        return data;
    }

    private static Type TypeOf(string fullName) => MdmDocumentClassMaps.CoveredTypes.Single(type => type.FullName == fullName);

    [Fact]
    public void The_module_initializer_registered_every_map_before_the_first_test()
    {
        // BL-569: the assembly's own initializer ran (not a host built by some earlier test), and every covered map —
        // the persisted roots and all they embed — ignores extra elements.
        Assert.True(MdmTestSerializers.Ran);
        Assert.Null(MdmTestSerializers.RegistrationFailure);
        Assert.All(MdmDocumentClassMaps.CoveredTypes, type =>
            Assert.True(BsonClassMap.LookupClassMap(type).IgnoreExtraElements, type.Name));
        Assert.True(MdmDocumentClassMaps.CoveredTypes.Count > MdmDocumentClassMaps.PersistedRoots.Count,
            "the walk found no embedded types — it is blind");
    }

    [Theory]
    [MemberData(nameof(Covered))]
    public void A_document_with_a_field_from_a_later_version_still_reads(string typeName)
    {
        var type = TypeOf(typeName);
        var document = Sample(type).ToBsonDocument(type);
        document["FutureField"] = 1;
        document["FutureObject"] = new BsonDocument("Nested", "x");

        var read = BsonSerializer.Deserialize(document, type);

        Assert.IsType(type, read);
    }

    [Theory]
    [MemberData(nameof(Covered))]
    public void The_map_is_the_drivers_own_automap_with_only_the_flag_added(string typeName)
    {
        // "Var olan eşleme DEĞİŞMEZ": member names, element names, member types, the id, the creator — the same as a
        // fresh, unregistered auto-map of the type. Brand / Product (and the GP lifecycle requests) keep their own
        // explicit maps, untouched by this registry: they are compared against those maps' own registration.
        var type = TypeOf(typeName);
        var registered = BsonClassMap.LookupClassMap(type);
        if (ExplicitlyMapped.Contains(type)) return;
        var fresh = new BsonClassMap(type);
        fresh.AutoMap();
        fresh.Freeze();

        Assert.Equal(Shape(fresh), Shape(registered));
        Assert.False(fresh.IgnoreExtraElements);
        Assert.True(registered.IgnoreExtraElements);
    }

    [Fact]
    public void Every_document_type_the_repositories_store_is_covered()
    {
        // The guard: a new collection / raw deserialization / projection of a type this registry does not cover turns
        // this red — the next unmapped document cannot slip in.
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        var persistence = Path.Combine(root!.FullName, "services", "Diten.MdmService", "src", "Diten.MdmService.Persistence");
        var used = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(persistence, "*.cs", SearchOption.AllDirectories)
                     .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            var code = ProductLegalEntityScopeWriteAdmissionContractTests.WithoutComments(File.ReadAllText(path));
            foreach (Match match in Regex.Matches(code,
                         @"(?:GetCollection|IMongoCollection|RepositoryBase|Deserialize|Project|Find)<(\w+)>"))
                used.Add(match.Groups[1].Value);
        }
        used.ExceptWith(["BsonDocument", "TEntity", "T", "TDocument", "TProjection", "BsonValue"]);
        var covered = MdmDocumentClassMaps.CoveredTypes.Select(type => type.Name).ToHashSet(StringComparer.Ordinal);

        Assert.True(used.Count >= 25, $"the scan found only {used.Count} stored types — it is blind");
        var missing = used.Where(name => !covered.Contains(name)).ToArray();
        Assert.True(missing.Length == 0, "Stored types without a forward-compatible map: " + string.Join(", ", missing));
    }

    [Fact]
    public void A_type_read_before_its_map_is_registered_stops_the_start_by_name()
    {
        // The "repository first, map second" order (BL-569): a raw read auto-maps the type WITHOUT the flag and freezes
        // it; registering afterwards cannot change it, and the check names it instead of letting a rollback fail later.
        _ = BsonSerializer.Deserialize<ReadBeforeRegistered>(new BsonDocument("Name", "x"));

        var refused = Assert.Throws<InvalidOperationException>(() =>
            MdmDocumentClassMaps.Verify([typeof(ReadBeforeRegistered)]));

        Assert.Contains(nameof(ReadBeforeRegistered), refused.Message, StringComparison.Ordinal);
        Assert.Contains("BL-576", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Real_documents_with_a_later_field_set_in_the_store_still_read_through_their_repositories()
    {
        // The rollback itself, end to end: a newer binary $set a field on the stored GSKU, LSKU, Finished Good, revision
        // and code reservation — and inside an embedded audit intent; this binary's repositories still read them.
        var database = Database();
        var tenant = new TenantContext();
        tenant.SetTenant(Guid.NewGuid());
        var intent = new LocalAuditIntent { IntentId = Guid.NewGuid(), TenantId = tenant.TenantId };
        var revision = new ProductDefinitionRevision { Id = Guid.NewGuid(), TenantId = tenant.TenantId, RevisionIdentifier = "REV-1", CreationCommandId = "c-" + Guid.NewGuid().ToString("N") };
        var gsku = new Gsku { Id = Guid.NewGuid(), TenantId = tenant.TenantId, ProductDefinitionRevisionId = revision.Id, CanonicalCode = "GS-" + Guid.NewGuid().ToString("N")[..10], CreationCommandId = Guid.NewGuid().ToString("N"), AuditIntents = [intent] };
        var lsku = new Lsku { Id = Guid.NewGuid(), TenantId = tenant.TenantId, GskuId = gsku.Id, CanonicalCode = "LS-" + Guid.NewGuid().ToString("N")[..10], CreationCommandId = Guid.NewGuid().ToString("N"), MarketCode = "M" + Guid.NewGuid().ToString("N")[..6] };
        var finishedGood = new FinishedGood { Id = Guid.NewGuid(), TenantId = tenant.TenantId, GskuId = gsku.Id, CanonicalCode = "FG-" + Guid.NewGuid().ToString("N")[..10], CreationCommandId = Guid.NewGuid().ToString("N") };
        var reservation = new CodeReservation { Id = Guid.NewGuid(), TenantId = tenant.TenantId };
        await database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions").InsertOneAsync(revision);
        await database.GetCollection<Gsku>("mdm_gskus").InsertOneAsync(gsku);
        await database.GetCollection<Lsku>("mdm_lskus").InsertOneAsync(lsku);
        await database.GetCollection<FinishedGood>("mdm_finished_goods").InsertOneAsync(finishedGood);
        await database.GetCollection<CodeReservation>("mdm_code_reservations").InsertOneAsync(reservation);
        foreach (var (collection, id) in new[]
                 {
                     ("mdm_product_definition_revisions", revision.Id), ("mdm_gskus", gsku.Id), ("mdm_lskus", lsku.Id),
                     ("mdm_finished_goods", finishedGood.Id), ("mdm_code_reservations", reservation.Id)
                 })
        {
            var set = new BsonDocument("FutureField", 1);
            if (collection == "mdm_gskus") set["AuditIntents.0.FutureField"] = "later";
            var updated = await database.GetCollection<BsonDocument>(collection).UpdateOneAsync(
                new BsonDocument("_id", new BsonBinaryData(id, GuidRepresentation.Standard)), new BsonDocument("$set", set));
            Assert.Equal(1, updated.ModifiedCount);
        }

        Assert.NotNull(await new ProductDefinitionRevisionRepository(database, tenant).GetByIdAsync(revision.Id));
        var readGsku = await new GskuRepository(database, tenant).GetByIdAsync(gsku.Id);
        Assert.Equal(intent.IntentId, Assert.Single(readGsku!.AuditIntents).IntentId);
        Assert.NotNull(await new LskuRepository(database, tenant).GetByIdAsync(lsku.Id));
        Assert.NotNull(await new FinishedGoodRepository(database, tenant).GetByIdAsync(finishedGood.Id));
        Assert.NotNull(await new CodeReservationRepository(database, tenant).GetByIdAsync(reservation.Id));
    }

    // ---------------------------------------------------------------------------------------------- plumbing

    private static readonly HashSet<Type> ExplicitlyMapped =
    [
        typeof(Brand), typeof(Product), typeof(BrandProductExternalReference), typeof(BrandHistoryEntry),
        typeof(BrandAuditSnapshot), typeof(ExternalReferenceReservation), typeof(GlobalProductCorrectionOperation),
        typeof(GlobalProductRetirementRequestOperation), typeof(GlobalProductIdentityWorkflowOperation)
    ];

    private static string[] Shape(BsonClassMap map) =>
    [
        $"id={map.IdMemberMap?.MemberName}:{map.IdMemberMap?.ElementName}",
        $"discriminator={map.Discriminator}:{map.DiscriminatorIsRequired}:{map.IsRootClass}",
        $"creators={string.Join(";", map.CreatorMaps.Select(creator => string.Join(",", creator.ElementNames ?? [])))}",
        .. map.AllMemberMaps.Select(member =>
            $"{member.MemberName}->{member.ElementName}:{member.MemberType.FullName}:{member.IsRequired}:{member.IgnoreIfNull}:{member.IgnoreIfDefault}")
    ];

    private static object Sample(Type type)
    {
        if (type.GetConstructor(Type.EmptyTypes) is { } parameterless) return parameterless.Invoke([]);
        // A positional record: its own constructor, every argument the default of its type (strings empty).
        var constructor = type.GetConstructors().OrderByDescending(item => item.GetParameters().Length).First();
        return constructor.Invoke(constructor.GetParameters()
            .Select(parameter => parameter.ParameterType == typeof(string) ? string.Empty
                : parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType)
                : parameter.ParameterType.IsGenericType ? Activator.CreateInstance(parameter.ParameterType)
                : null)
            .ToArray());
    }

    private IMongoDatabase Database()
    {
        var settings = MongoClientSettings.FromConnectionString(_fixture.ReplicaConnectionString);
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        var client = new MongoClient(settings);
        Assert.All(client.Settings.Servers, server => Assert.NotEqual(27017, server.Port));
        return client.GetDatabase(ProductLegalEntityScopeMongoCollection.DatabaseName);
    }

    private sealed class ReadBeforeRegistered
    {
        public string Name { get; set; } = string.Empty;
    }
}
