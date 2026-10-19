using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster;
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
        Assert.True(MdmTestSerializers.RegistrationFailure is null, MdmTestSerializers.RegistrationFailure?.Message);
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
            // FIX1 M3: every type argument of every driver entry point that maps a document type.
            foreach (Match match in Regex.Matches(code,
                         @"\b(?:GetCollection|IMongoCollection|RepositoryBase|Deserialize|Project|Find|Builders|Aggregate|Unwind|Lookup|Group|As|OfType)<([\w\s,<>?]+?)>(?=[\s.(,;>)\]])"))
                foreach (var argument in Regex.Matches(match.Groups[1].Value, @"\w+").Select(token => token.Value))
                    used.Add(argument);
        }
        used.ExceptWith(["BsonDocument", "TEntity", "T", "TDocument", "TProjection", "TResult", "TNewResult", "BsonValue",
            "Guid", "string", "int", "long", "bool", "object", "List", "IReadOnlyList", "IEnumerable", "Dictionary"]);
        var known = MdmDocumentClassMaps.CoveredTypes.Concat(MdmDocumentClassMaps.StrictByDesign)
            .Select(type => type.Name).ToHashSet(StringComparer.Ordinal);
        // A Persistence-private projection class may carry its own [BsonIgnoreExtraElements] (e.g. the admission census).
        known.UnionWith(typeof(MdmDocumentClassMaps).Assembly.GetTypes()
            .Where(type => type.GetCustomAttributes(typeof(MongoDB.Bson.Serialization.Attributes.BsonIgnoreExtraElementsAttribute), false).Length > 0)
            .Select(type => type.Name));

        Assert.True(used.Count >= 25, $"the scan found only {used.Count} stored types — it is blind");
        var missing = used.Where(name => !known.Contains(name)).ToArray();
        Assert.True(missing.Length == 0,
            "Stored types neither covered, strict by design nor attributed: " + string.Join(", ", missing));
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
        try
        {
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
        finally
        {
            // FIX1 M6: the shared test database keeps none of this test's documents (test-data cleanup only).
            foreach (var (collection, id) in new[]
                     {
                         ("mdm_product_definition_revisions", revision.Id), ("mdm_gskus", gsku.Id), ("mdm_lskus", lsku.Id),
                         ("mdm_finished_goods", finishedGood.Id), ("mdm_code_reservations", reservation.Id)
                     })
                await database.GetCollection<BsonDocument>(collection).DeleteOneAsync(
                    new BsonDocument("_id", new BsonBinaryData(id, GuidRepresentation.Standard)));
        }
    }

    // ---------------------------------------------------------------------------------------------- FIX1 R2 / R3

    [Fact]
    public void The_strict_by_design_documents_are_named_and_never_get_the_flag()
    {
        // R2 / R3 (CT decisions): the scope rollout (leases, fence, abort, snapshot), the scope policy (periods) and the
        // whole-document-replaced LegalEntity stay strict — named, not forgotten.
        Assert.Equal(["LegalEntity", "ProductLegalEntityScopePolicy", "ProductLegalEntityScopeRolloutState"],
            MdmDocumentClassMaps.StrictByDesignRoots.Select(type => type.Name).Order(StringComparer.Ordinal));
        Assert.Contains(MdmDocumentClassMaps.StrictByDesign, type => type.Name == "ProductLegalEntityScopeWriterLease");
        Assert.Contains(MdmDocumentClassMaps.StrictByDesign, type => type.Name == "ProductLegalEntityScopeActivationFence");
        Assert.Contains(MdmDocumentClassMaps.StrictByDesign, type => type.Name == "ProductLegalEntityScopePeriod");
        Assert.Empty(MdmDocumentClassMaps.StrictByDesign.Intersect(MdmDocumentClassMaps.CoveredTypes));
        Assert.All(MdmDocumentClassMaps.StrictByDesign, type =>
            Assert.False(BsonClassMap.LookupClassMap(type).IgnoreExtraElements, type.Name));
    }

    [Fact]
    public async Task A_rollout_written_by_a_newer_binary_is_never_read_leniently_and_refuses_the_write()
    {
        // R2: a lease / fence field this binary does not know must not be dropped silently — the guard fails closed.
        // Measured on this branch (cut before SCOPE-INT-01): the store's read refuses the document, and the write fence
        // answers its coded 503. The product READ's coded 503 (instead of the raw refusal) is BL-567, which arrives with
        // the takeover merge (SCOPE-INT-01) — CT re-measures it there.
        // BL-595 FIX2: the shared helpers follow this class's replica set in-process (never the environment).
        var ownedRedirect = MdmTestMongo.UseOwnedRedirect(_fixture.ReplicaConnectionString);
        try
        {
            await using var scope = await ProductScopeMongoScope.CreateAsync();
            var rollouts = scope.RolloutRepository();
            Assert.True((await rollouts.CreateAsync(ProductLegalEntityScopeRolloutState.CreatePreparation(
                scope.TenantId, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow))).Succeeded);
            await scope.Database.GetCollection<BsonDocument>("mdm_product_legal_entity_scope_rollout_states").UpdateOneAsync(
                new BsonDocument("TenantId", new BsonBinaryData(scope.TenantId, GuidRepresentation.Standard)),
                new BsonDocument("$set", new BsonDocument("FutureFenceField", "held")));
            var tenant = new TenantContext();
            tenant.SetTenant(scope.TenantId);

            var refusal = await Assert.ThrowsAsync<FormatException>(() => rollouts.GetAsync());
            var write = await new ProductLegalEntityScopeWriteFenceCoordinator(rollouts,
                    new ProductLegalEntityScopeOperationalReadinessRepository(scope.Database, tenant), new Actor(), tenant,
                    TimeProvider.System)
                .EnterAsync(new ReserveCanonicalCodeCommand(new ProductItemSkuMasterModels.ReserveGlobalProductCodeRequest()), default);

            Assert.Contains("FutureFenceField", refusal.Message, StringComparison.Ordinal);
            Assert.False(write.IsAllowed);
            Assert.Equal(503, write.StatusCode);
            Assert.Equal("PRODUCT_LEGAL_ENTITY_SCOPE_ROLLOUT_INVALID", write.FailureCode);
        }
        finally
        {
            ownedRedirect.Dispose();
        }
    }

    [Fact]
    public void A_policy_or_legal_entity_written_by_a_newer_binary_fails_to_read_on_purpose()
    {
        // R2 (policy) and R3 (LegalEntity, replaced whole: a read that succeeded would let the next replace erase the field).
        foreach (var type in new[] { typeof(ProductLegalEntityScopePolicy), typeof(LegalEntity) })
        {
            var document = Activator.CreateInstance(type, nonPublic: true)!.ToBsonDocument(type);
            document["FutureField"] = 1;
            Assert.ThrowsAny<Exception>(() => BsonSerializer.Deserialize(document, type));
        }
    }

    [Fact]
    public void Every_type_written_whole_is_strict_or_a_named_pre_existing_risk()
    {
        // R3 (CT decision): a type written by whole-document replace, or whose array is $set whole, must not read
        // "lenient" — the next whole write would erase what it skipped. Measured on the Persistence source.
        var source = PersistenceSources();
        var replaced = new SortedSet<string>(StringComparer.Ordinal);
        var replacedViaBase = source.Values.Any(code => Regex.IsMatch(code, @"\bCollection\.ReplaceOneAsync\("));
        foreach (var (file, code) in source)
        {
            foreach (Match call in Regex.Matches(code, @"\b(\w+)\.(?:ReplaceOneAsync|FindOneAndReplaceAsync)\("))
            {
                if (call.Groups[1].Value == "Collection")
                {
                    if (Path.GetFileName(file) == "RepositoryBase.cs")
                        foreach (Match sub in Regex.Matches(string.Join("\n", source.Values), @"RepositoryBase<(\w+)>"))
                            if (sub.Groups[1].Value != "TEntity") replaced.Add(sub.Groups[1].Value);
                    foreach (Match own in Regex.Matches(code, @"RepositoryBase<(\w+)>"))
                        if (own.Groups[1].Value != "TEntity") replaced.Add(own.Groups[1].Value);
                    continue;
                }
                var declaration = Regex.Match(code, $@"IMongoCollection<(\w+)>\s+{Regex.Escape(call.Groups[1].Value)}\b");
                replaced.Add(declaration.Success ? declaration.Groups[1].Value : "UNRESOLVED:" + call.Groups[1].Value);
            }
            foreach (Match model in Regex.Matches(code, @"ReplaceOneModel<(\w+)>")) replaced.Add(model.Groups[1].Value);
        }
        // Whole-array $set: a set property whose stored type is a list of an MDM class.
        var setProperties = source.Values.SelectMany(code => Regex.Matches(code, @"\.Set\(\s*\w+\s*=>\s*\w+\.(\w+)\s*,")
            .Select(match => match.Groups[1].Value)).ToHashSet(StringComparer.Ordinal);
        var arrayElements = typeof(EntityBase).Assembly.GetTypes()
            .SelectMany(owner => owner.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(property => setProperties.Contains(property.Name) && property.PropertyType.IsGenericType
                               && property.PropertyType.GetGenericArguments()[0].Assembly == typeof(EntityBase).Assembly
                               && property.PropertyType.GetGenericArguments()[0].IsClass)
            .Select(property => property.PropertyType.GetGenericArguments()[0].Name)
            .ToHashSet(StringComparer.Ordinal);
        var strict = MdmDocumentClassMaps.StrictByDesign.Select(type => type.Name).ToHashSet(StringComparer.Ordinal);

        Assert.True(replacedViaBase && replaced.Contains("LegalEntity"), "the scan is blind: LegalEntity's replace not found");
        var all = replaced.Concat(arrayElements).Distinct().ToArray();
        var offenders = all
            .Where(name => !strict.Contains(name) && !KnownPreExistingLenientWholeWrites.Contains(name))
            .ToArray();
        Assert.True(offenders.Length == 0, "Written whole but read leniently: " + string.Join(", ", offenders));
    }

    // Brand / Product: their explicit maps ignored extra elements BEFORE this work, and ProductRepository replaces whole —
    // a pre-existing risk, NOT changed here (CT backlog; 🟡 in the report).
    // Item: FU04 S2 moved the item update from the whole-document ReplaceAsync to a field-level $set (S1 carry-over b), so
    // Item itself is no longer here — a whole write of an Item is red again. What remains is narrower and named: an edit
    // replaces the item's two lists of classes whole when it writes them — its unit conversions (ItemUomConversion) and its
    // storage selections (ReferenceCatalogSelection) — so a sub-field a later binary added to an ELEMENT of those lists is
    // lost on an older binary's edit of that item (FU04 S2 report, 🟡).
    private static readonly HashSet<string> KnownPreExistingLenientWholeWrites =
        ["Brand", "Product", "BrandProductExternalReference", "ItemUomConversion", "ReferenceCatalogSelection"];

    // ---------------------------------------------------------------------------------------------- FIX1 M2 / M4

    [Fact]
    public void MDM_registers_no_convention_that_could_change_how_a_document_is_mapped()
    {
        var root = RepoRoot();
        var source = Path.Combine(root, "services", "Diten.MdmService", "src");
        var callers = Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => ProductLegalEntityScopeWriteAdmissionContractTests.WithoutComments(File.ReadAllText(path))
                .Contains("ConventionRegistry", StringComparison.Ordinal))
            .ToArray();
        Assert.Empty(callers);
    }

    [Fact]
    public void The_walk_meets_no_struct_interface_abstract_or_open_generic_document_member()
    {
        // M4: such a member would be skipped by the walk and silently stay strict — none exists today; a new one is named.
        var domain = typeof(EntityBase).Assembly;
        var persistence = typeof(MdmDocumentClassMaps).Assembly;
        var known = MdmDocumentClassMaps.CoveredTypes.Concat(MdmDocumentClassMaps.StrictByDesign).ToHashSet();
        var unwalkable = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var type in known)
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        foreach (var candidate in Unwrap(property.PropertyType))
        {
            if (candidate.Assembly != domain && candidate.Assembly != persistence) continue;
            if (candidate.IsEnum) continue;
            if (candidate.IsValueType || candidate.IsInterface || candidate.IsAbstract || candidate.IsGenericTypeDefinition
                || !known.Contains(candidate))
                unwalkable.Add($"{type.Name}.{property.Name}: {candidate.Name}");
        }
        Assert.True(unwalkable.Count == 0, "Members the walk cannot map: " + string.Join(", ", unwalkable));
        Assert.All(known, type => Assert.True(type.Assembly == domain || type.Assembly == persistence, type.FullName));
    }

    [Fact]
    public void A_stored_type_that_is_gone_is_a_named_error_not_a_type_initialization_failure()
    {
        var nested = typeof(MdmDocumentClassMaps).GetMethod("NestedDocument", BindingFlags.NonPublic | BindingFlags.Static)!;

        var refused = Assert.Throws<TargetInvocationException>(() =>
            nested.Invoke(null, [typeof(CodeReservationRepository), "NoSuchDocument"]));

        Assert.IsType<InvalidOperationException>(refused.InnerException);
        Assert.Contains("BL-576", refused.InnerException!.Message, StringComparison.Ordinal);
    }

    private static IEnumerable<Type> Unwrap(Type type)
    {
        if (type.IsArray) { foreach (var inner in Unwrap(type.GetElementType()!)) yield return inner; yield break; }
        if (type.IsGenericType && type.Assembly != typeof(EntityBase).Assembly)
        {
            foreach (var argument in type.GetGenericArguments())
            foreach (var inner in Unwrap(argument)) yield return inner;
            yield break;
        }
        yield return type;
    }

    private static string RepoRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        return root?.FullName ?? throw new InvalidOperationException("repo root not found");
    }

    private static Dictionary<string, string> PersistenceSources()
    {
        var persistence = Path.Combine(RepoRoot(), "services", "Diten.MdmService", "src", "Diten.MdmService.Persistence");
        return Directory.EnumerateFiles(persistence, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToDictionary(path => path, path => ProductLegalEntityScopeWriteAdmissionContractTests.WithoutComments(File.ReadAllText(path)));
    }

    private static T Unreachable<T>() where T : class
    {
        var proxy = DispatchProxy.Create<T, FirstGskuIdentityWorkflowProcessorTests.RepositoryProxy>();
        ((FirstGskuIdentityWorkflowProcessorTests.RepositoryProxy)(object)proxy).Handler =
            (method, _) => throw new NotSupportedException("not reached: " + method.Name);
        return proxy;
    }

    private static IProductLegalEntityScopePolicyRepository NoPolicies()
    {
        var proxy = DispatchProxy.Create<IProductLegalEntityScopePolicyRepository, FirstGskuIdentityWorkflowProcessorTests.RepositoryProxy>();
        ((FirstGskuIdentityWorkflowProcessorTests.RepositoryProxy)(object)proxy).Handler = (method, _) => method.Name switch
        {
            "GetConfiguredGlobalProductIdsAsync" => Task.FromResult<IReadOnlyList<Guid>>([]),
            "GetByGlobalProductIdAsync" or "GetByCreationCommandIdAsync" => Task.FromResult<ProductLegalEntityScopePolicy?>(null),
            _ => throw new NotSupportedException(method.Name)
        };
        return proxy;
    }

    private sealed class Actor : Contracts.IProductIdentityActorContext
    {
        public string ActorId { get; } = Guid.NewGuid().ToString("D");
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
        Assert.All(client.Settings.Servers, server => Assert.NotEqual(MdmTestMongo.SharedPort, server.Port));
        return client.GetDatabase(ProductLegalEntityScopeMongoCollection.DatabaseName);
    }

    private sealed class ReadBeforeRegistered
    {
        public string Name { get; set; } = string.Empty;
    }
}
