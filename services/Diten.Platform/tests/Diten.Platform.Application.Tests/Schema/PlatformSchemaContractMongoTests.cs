using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Schema;

/*
 * THE CONTRACT, AGAINST A REAL MONGO — the half that cannot be faked.
 *
 * ⚠ WHY A REAL DATABASE IS NOT OPTIONAL HERE. Mongo does not complain about a missing index. Ask it to sort
 * on a field with no index and it sorts anyway, in memory, until the day the collection is large enough to
 * blow the sort limit in production. So "the manifest declares 218 indexes" proves nothing on its own; the
 * only proof is reading back what Mongo actually built and comparing it property by property. That is
 * contract item 2, and item 5 exists for the same reason at the query level: an index can be present and
 * still not serve the query it was built for (the parallel-array sort failure that killed the MOD-0023
 * transition gate is exactly that, and it is why the Mongo harness exists at all).
 *
 * ⚠ THIS TEST USES A DATABASE OF ITS OWN, AND THAT IS THE ONE LEGITIMATE CASE. Item 3 asserts what does NOT
 * exist in the database, which is only meaningful if nothing else writes to it. Note the name is FIXED, not
 * a fresh Guid: it is dropped and rebuilt in place, so it cannot accumulate one database per run — which is
 * the failure this whole round is fixing, and which
 * TenantArchitecture.ArchitectureTests.MongoTestDatabaseGuardTests would otherwise catch here.
 */
[Collection("platform-schema-contract")]
public sealed class PlatformSchemaContractMongoTests : IAsyncLifetime
{
    private const string ConnectionString = "mongodb://localhost:27017";
    /*
     * ⚠ NAMED UNDER THE HARNESS'S OWNED PREFIX ON PURPOSE. This database is dropped at the end of every test,
     * but "the end" never arrives if mongod dies mid-run — which is the failure this work exists to fix. A
     * name inside MongoResidueSweeper.OwnedPrefix, plus the marker stamped below, means a later run will
     * clean it up instead of leaving it on disk forever.
     */
    private const string DatabaseName = MongoResidueSweeper.OwnedPrefix + "_schema_contract";

    private static readonly Guid RunId = Guid.NewGuid();

    private MongoClient _client = null!;
    private IMongoDatabase _database = null!;

    public async Task InitializeAsync()
    {
        var settings = MongoClientSettings.FromConnectionString(ConnectionString);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        /*
         * ⚠ THE SAME GUID REPRESENTATION PRODUCTION USES, AND IT IS NOT OPTIONAL. MongoIntegrationHarness
         * registers a PROCESS-GLOBAL GuidSerializer(Standard) when it runs, so a client built without this
         * line encodes Guids one way or the other depending on WHICH TEST CLASS RAN FIRST. That is the
         * classic "passes alone, fails in the suite" shape: the query stops matching its own inserted rows
         * and the failure looks like data loss. Pinning it here mirrors
         * Diten.Platform.Infrastructure.DependencyInjection and makes the order irrelevant.
         */
        settings.GuidRepresentation = GuidRepresentation.Standard;

        _client = new MongoClient(settings);

        // Fail loudly when Mongo is unreachable. Skipping is what let the transition-gate bug ship.
        await _client.GetDatabase(DatabaseName).RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        await _client.DropDatabaseAsync(DatabaseName);
        _database = _client.GetDatabase(DatabaseName);
        await MongoResidueSweeper.TouchAsync(_database, RunId, DateTime.UtcNow);
    }

    public Task DisposeAsync() => _client.DropDatabaseAsync(DatabaseName);

    // ── ITEM 2: EVERY DECLARED INDEX EXISTS, WITH ITS FULL PROPERTIES ──────────────────────────────────────

    /*
     * ⚠ WorkflowWorkCenter AND DocumentManagement ARE ON THIS LIST BECAUSE BL-279 PUT INDEXES IN THEM. Until
     * then neither profile appeared here, so every index they declared was checked by nothing: the manifest
     * could name an index Mongo never built and the suite stayed green. That is the same "green for the wrong
     * reason" shape the key/unique/partial comparison below exists to stop, one level up. A profile that
     * declares indexes belongs on this list; adding indexes to a profile that is missing from it is exactly
     * how they go unverified.
     *
     * MUTATION GUARD: delete any declared index from PlatformSchemaManifest.* and the profile that owns it
     * goes red here, naming "<collection>.<index>: NOT BUILT".
     */
    [Theory]
    [InlineData(SchemaProfile.BusinessReferenceData)]
    [InlineData(SchemaProfile.Eventing)]
    [InlineData(SchemaProfile.Notification)]
    [InlineData(SchemaProfile.Organization)]
    [InlineData(SchemaProfile.WorkflowWorkCenter)]
    [InlineData(SchemaProfile.DocumentManagement)]
    public async Task EveryIndexTheManifestDeclaresIsBuiltWithItsFullProperties(SchemaProfile profile)
    {
        await PlatformSchemaManifest.ApplyAsync(_database, new[] { profile });

        var mismatches = new List<string>();

        foreach (var collection in PlatformSchemaManifest.For(profile))
        {
            if (collection.Indexes.Count == 0)
            {
                continue;
            }

            var built = await ListIndexesAsync(collection.Name);

            foreach (var declared in collection.Indexes)
            {
                if (!built.TryGetValue(declared.Name, out var actual))
                {
                    mismatches.Add($"{collection.Name}.{declared.Name}: NOT BUILT (present: {string.Join(", ", built.Keys)})");
                    continue;
                }

                /*
                 * ⚠ COMPARING THE NAME ALONE WOULD BE THE "GREEN FOR THE WRONG REASON" TEST. An index can
                 * carry the right name and the wrong keys, or be unique when the manifest says partial-unique
                 * — and a partial filter that silently disappears turns a live-only uniqueness rule into one
                 * that soft-deleted rows also enforce, which blocks legitimate inserts in production.
                 */
                var actualKey = actual["key"].AsBsonDocument;
                if (actualKey != declared.Key)
                {
                    mismatches.Add($"{collection.Name}.{declared.Name}: keys {actualKey} != declared {declared.Key}");
                }

                var actualUnique = actual.GetValue("unique", BsonBoolean.False).ToBoolean();
                if (actualUnique != declared.Unique)
                {
                    mismatches.Add($"{collection.Name}.{declared.Name}: unique={actualUnique} != declared {declared.Unique}");
                }

                var actualPartial = actual.TryGetValue("partialFilterExpression", out var pfe)
                    ? pfe.AsBsonDocument
                    : null;
                if (!BsonEquals(actualPartial, declared.PartialFilterExpression))
                {
                    mismatches.Add(
                        $"{collection.Name}.{declared.Name}: partialFilterExpression "
                        + $"{actualPartial?.ToString() ?? "<none>"} != declared "
                        + $"{declared.PartialFilterExpression?.ToString() ?? "<none>"}");
                }
            }
        }

        Assert.True(mismatches.Count == 0,
            $"{profile}: what Mongo built does not match what the manifest declares:\n"
            + string.Join("\n", mismatches));
    }

    // ── ITEM 3: NOTHING OUTSIDE THE PROFILE IS CREATED ─────────────────────────────────────────────────────

    [Fact]
    public async Task AProfileBuildsItsOwnCollectionsAndNothingElse()
    {
        const SchemaProfile profile = SchemaProfile.BusinessReferenceData;
        await PlatformSchemaManifest.ApplyAsync(_database, new[] { profile });

        var expected = PlatformSchemaManifest.For(profile)
            .Where(c => c.Indexes.Count > 0)   // a collection with no index is not created until first write
            .Select(c => c.Name)
            .ToHashSet(StringComparer.Ordinal);

        var actual = await CollectionNamesAsync();

        var strays = actual.Except(expected).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.True(strays.Length == 0,
            $"asking for {profile} created collections outside it — the profile is not a subset any more:\n"
            + string.Join("\n", strays));

        var missing = expected.Except(actual).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.True(missing.Length == 0, $"{profile} did not create: {string.Join(", ", missing)}");

        // ⚠ THE NUMBER THAT MATTERS. The whole profile is 8 collections against the 82 the old path built.
        Assert.True(actual.Count < 15,
            $"{profile} created {actual.Count} collections — a profile that builds most of the schema is not "
            + "a profile, and this is exactly the regression that brings mongod back down.");
    }

    // ── ITEM 5: THE INDEX BEHAVIOUR REGRESSIONS STILL HOLD ─────────────────────────────────────────────────

    [Fact]
    public async Task TheTransitionLogSortStillRunsUnderTheWorkflowProfileAlone()
    {
        /*
         * ⚠ THIS IS THE FAILURE CLASS THE HARNESS EXISTS FOR. The MOD-0023 transition gate died on
         * "cannot sort with keys that are parallel arrays" — a defect no fake repository can see, because the
         * Mongo query never runs against a fake. Narrowing the schema to a profile must not quietly take that
         * coverage away: if the profile did not build workflow_transition_logs' indexes, this query would
         * still SUCCEED (unindexed) and the test would pass while the protection was gone. So the assertion
         * is not just "the query ran" — it is that the index backing the sort is present and used.
         */
        await PlatformSchemaManifest.ApplyAsync(_database, new[] { SchemaProfile.WorkflowWorkCenter });

        var logs = _database.GetCollection<BsonDocument>(PlatformCollections.WorkflowTransitionLogs);
        var tenantId = Guid.NewGuid();

        await logs.InsertManyAsync(new[]
        {
            new BsonDocument { { "TenantId", new BsonBinaryData(tenantId, GuidRepresentation.Standard) }, { "Sequence", 2 } },
            new BsonDocument { { "TenantId", new BsonBinaryData(tenantId, GuidRepresentation.Standard) }, { "Sequence", 1 } }
        });

        var sorted = await logs
            .Find(Builders<BsonDocument>.Filter.Eq("TenantId", new BsonBinaryData(tenantId, GuidRepresentation.Standard)))
            .Sort(Builders<BsonDocument>.Sort.Ascending("Sequence"))
            .ToListAsync();

        Assert.Equal(new[] { 1, 2 }, sorted.Select(d => d["Sequence"].AsInt32).ToArray());

        var built = await ListIndexesAsync(PlatformCollections.WorkflowTransitionLogs);
        Assert.True(built.Count > 1,
            "workflow_transition_logs came back with only _id — the sort above passed unindexed, which is "
            + "precisely how this test would go green while the protection was gone.");
    }

    // ── BL-279: THE NINE UNINDEXED COLLECTIONS STAY INDEXED ────────────────────────────────────────────────

    [Fact]
    public async Task TheQueriesBL279SizedRunOnAnIndexAndNotACollectionScan()
    {
        /*
         * ⚠ THIS IS THE MUTATION GUARD FOR BL-279, AND IT HAD TO BE WRITTEN AT THE PLAN LEVEL. The obvious
         * place to guard "these collections have indexes" is item 2 above — but item 2 iterates over what the
         * manifest DECLARES and checks Mongo built it. Delete a declaration and the loop simply stops looking
         * at it: nothing is declared, nothing is missing, GREEN. That is precisely the hole these nine
         * collections fell through in the first place, so re-using item 2 as their guard would re-open it.
         *
         * So the assertion here is the one Mongo cannot fake: run each repository's REAL filter and sort
         * through explain, and demand the winning plan be an IXSCAN on the named index. Delete any index
         * below from PlatformSchemaManifest.* and the plan degrades to COLLSCAN and this goes red naming the
         * collection — which is the failure mode the whole round exists to stop, because a missing index
         * raises no error in Mongo: the query just quietly scans.
         *
         * ⚠ THE ROWS ARE NOT DECORATION. An empty collection lets the planner pick anything, so each shape
         * gets a handful of documents that match the filter's shape; the plan is then the one production gets.
         */
        await PlatformSchemaManifest.ApplyAsync(
            _database,
            new[]
            {
                SchemaProfile.WorkflowWorkCenter, SchemaProfile.DocumentManagement, SchemaProfile.Notification,
                SchemaProfile.BusinessReferenceData
            });

        var tenant = new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard);
        var task = new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard);
        var listVersion = new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard);
        var baseline = new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard);
        var instance = new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard);

        await SeedAsync(PlatformCollections.TaskComments, i => new BsonDocument
            { { "TenantId", tenant }, { "IsDeleted", false }, { "TaskItemId", task }, { "Text", $"c{i}" } });
        await SeedAsync(PlatformCollections.TaskTransitions, i => new BsonDocument
            { { "TenantId", tenant }, { "IsDeleted", false }, { "TaskItemId", task }, { "Kind", i } });
        await SeedAsync(PlatformCollections.TaskTypes, i => new BsonDocument
            { { "TenantId", tenant }, { "IsDeleted", false }, { "Code", $"T{i:D3}" }, { "IsActive", true }, { "DeletedAt", BsonNull.Value } });
        await SeedAsync(PlatformCollections.DocumentReferenceEntries, i => new BsonDocument
            { { "TenantId", tenant }, { "DeletedAt", BsonNull.Value }, { "ListVersionId", listVersion },
              { "DocumentCode", $"DOC-{i:D3}" }, { "DocumentUid", $"UID-{i:D3}" }, { "Title", $"t{i}" } });
        await SeedAsync(PlatformCollections.DocumentReferenceListVersions, i => new BsonDocument
            { { "TenantId", tenant }, { "IsDeleted", false }, { "ContentHash", $"h{i:D3}" }, { "WithdrawnAt", BsonNull.Value } });
        await SeedAsync(PlatformCollections.DocumentManagementCollectionProvisioningEvidence, i => new BsonDocument
            { { "TenantId", tenant }, { "IsDeleted", false }, { "BaselineReleaseId", baseline },
              { "CollectionInstanceId", i == 0 ? instance : new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) } });
        await SeedAsync(PlatformCollections.DocumentManagementCollectionDeviations, i => new BsonDocument
            { { "TenantId", tenant }, { "IsDeleted", false }, { "BaselineReleaseId", baseline }, { "Status", 0 } });
        await SeedAsync(PlatformCollections.NotificationEventDefinitions, i => new BsonDocument
            { { "IsDeleted", false }, { "EventCode", $"e.{i:D3}" }, { "Status", 1 } });

        /*
         * ⚠ THE TYPES HERE ARE THE ONES THE ENTITY SERIALIZES TO, NOT THE ONES THAT READ NATURALLY. TenantId
         * is a plain Guid on TenantScopedEntity and lands as UUID binary; BusinessReferenceDataVersionId
         * carries [BsonRepresentation(BsonType.String)] and lands as a string. Seed the version id as a
         * binary Guid instead and the filters below stop matching their own rows — the collection reads as
         * empty, the planner picks whatever it likes, and this test goes green while proving nothing.
         */
        var brdVersion = Guid.NewGuid().ToString();
        await SeedAsync(PlatformCollections.BusinessReferenceDataValidationResults, i => new BsonDocument
            { { "TenantId", tenant }, { "IsDeleted", false }, { "BusinessReferenceDataVersionId", brdVersion },
              { "RuleId", $"RULE-{i:D3}" }, { "Message", $"m{i}" } });

        var failures = new List<string>();

        // Each row is one repository read, written exactly as the repository writes it.
        await ExpectIndexScanAsync(failures, PlatformCollections.TaskComments, "ix_task_comments_tenant_task",
            "TaskCommentRepository.ListByTaskIdAsync",
            new BsonDocument { { "TenantId", tenant }, { "IsDeleted", false }, { "TaskItemId", task } });

        await ExpectIndexScanAsync(failures, PlatformCollections.TaskTransitions, "ix_task_transitions_tenant_task",
            "TaskTransitionRepository.ListByTaskIdAsync",
            new BsonDocument { { "TenantId", tenant }, { "IsDeleted", false }, { "TaskItemId", task } });

        await ExpectIndexScanAsync(failures, PlatformCollections.TaskTypes, "ux_task_types_tenant_code_active",
            "TaskTypeRepository.GetByCodeAsync",
            new BsonDocument { { "TenantId", tenant }, { "IsDeleted", false }, { "Code", "T000" } });

        /*
         * ⚠ THIS ONE ALSO PINS AN INDEX THAT WAS DELIBERATELY *NOT* ADDED. The sibling TaskFieldDefinition
         * carries a second {TenantId, IsActive, SortOrder} index, and symmetry argued for one here — but the
         * unique index alone already serves ListActive with no blocking SORT, so BL-279 rejected it as a write
         * cost with no read benefit. Asserting "no SORT stage" is what makes that a measured decision instead
         * of an opinion: if it ever stops holding, this says so rather than the index quietly being missed.
         */
        await ExpectIndexScanAsync(failures, PlatformCollections.TaskTypes, "ux_task_types_tenant_code_active",
            "TaskTypeRepository.ListActiveAsync (sorted, and with NO second index)",
            new BsonDocument { { "TenantId", tenant }, { "IsDeleted", false }, { "IsActive", true }, { "DeletedAt", BsonNull.Value } },
            sort: new BsonDocument("Code", 1),
            forbidBlockingSort: true);

        await ExpectIndexScanAsync(failures, PlatformCollections.DocumentReferenceEntries,
            "ix_document_reference_entries_tenant_version_code",
            "DocumentReferenceListRepository.SearchAsync",
            new BsonDocument { { "TenantId", tenant }, { "DeletedAt", BsonNull.Value }, { "ListVersionId", listVersion } },
            sort: new BsonDocument("DocumentCode", 1),
            forbidBlockingSort: true);

        await ExpectIndexScanAsync(failures, PlatformCollections.DocumentReferenceEntries,
            "ix_document_reference_entries_tenant_version_uid",
            "DocumentReferenceListRepository.GetEntriesByUidsAsync",
            new BsonDocument
            {
                { "TenantId", tenant }, { "DeletedAt", BsonNull.Value }, { "ListVersionId", listVersion },
                { "DocumentUid", new BsonDocument("$in", new BsonArray { "UID-000" }) }
            });

        await ExpectIndexScanAsync(failures, PlatformCollections.DocumentReferenceListVersions,
            "ix_document_reference_list_versions_tenant_hash",
            "DocumentReferenceListRepository.FindLiveVersionByHashAsync",
            new BsonDocument { { "TenantId", tenant }, { "IsDeleted", false }, { "ContentHash", "h000" }, { "WithdrawnAt", BsonNull.Value } });

        await ExpectIndexScanAsync(failures, PlatformCollections.DocumentManagementCollectionProvisioningEvidence,
            "ux_dm_collection_provisioning_evidence_tenant_instance_active",
            "ProvisioningEvidenceRepository.GetByCollectionInstanceAsync",
            new BsonDocument { { "TenantId", tenant }, { "IsDeleted", false }, { "CollectionInstanceId", instance } });

        await ExpectIndexScanAsync(failures, PlatformCollections.DocumentManagementCollectionProvisioningEvidence,
            "ix_dm_collection_provisioning_evidence_tenant_baseline",
            "ProvisioningEvidenceRepository.GetByBaselineAsync",
            new BsonDocument { { "TenantId", tenant }, { "IsDeleted", false }, { "BaselineReleaseId", baseline } });

        await ExpectIndexScanAsync(failures, PlatformCollections.DocumentManagementCollectionDeviations,
            "ix_dm_collection_deviations_tenant_baseline_status",
            "DocumentCollectionDeviationRepository.GetOpenByBaselineAsync",
            new BsonDocument { { "TenantId", tenant }, { "IsDeleted", false }, { "BaselineReleaseId", baseline }, { "Status", 0 } });

        await ExpectIndexScanAsync(failures, PlatformCollections.NotificationEventDefinitions,
            "ux_notification_event_definitions_event_code_active",
            "NotificationEventDefinitionRepository.GetByEventCodeAsync",
            new BsonDocument { { "IsDeleted", false }, { "EventCode", "e.000" } });

        /*
         * ── BL-298: THE INDEX THE INDEX BUDGET FINALLY PAID FOR ───────────────────────────────────────────
         *
         * business_reference_data_validation_results was the ONE collection BL-279 measured, sized, and could
         * not spend — the profile sat at its 18-index ceiling, and raising a ceiling to fit a change is the
         * move SchemaProfileBudget's header exists to stop. The GSKU owners raised it to 19 on 2026-08-28 and
         * the index went in. Both of its call sites are pinned below, and the SECOND one matters most, for a
         * reason no reader would guess from the manifest.
         */
        await ExpectIndexScanAsync(failures, PlatformCollections.BusinessReferenceDataValidationResults,
            "ix_business_reference_data_validation_results_tenant_version_rule",
            "BusinessReferenceDataStewardshipRepository.GetValidationResultsByVersionAsync (sorted)",
            new BsonDocument { { "TenantId", tenant }, { "BusinessReferenceDataVersionId", brdVersion }, { "IsDeleted", false } },
            sort: new BsonDocument("RuleId", 1),
            forbidBlockingSort: true);

        /*
         * ⚠ THIS ONE PINS THE ABSENCE OF A PARTIAL FILTER, AND NOTHING ELSE CAN. Every other index in the
         * BusinessReferenceData profile carries PartialFilterExpression IsDeleted=false, and the read above
         * does filter on IsDeleted=false — so the next person to look at this manifest will see an index that
         * breaks the house pattern and "fix" it. Measured, that fix costs half the win: the read is served
         * identically either way (25 examined, no SORT), but ReplaceValidationResultsAsync deletes on
         * {TenantId, VersionId} with NO IsDeleted predicate, so Mongo cannot prove the delete is a subset of
         * the partial filter and refuses the index — straight back to a scan of the whole collection.
         *
         * MUTATION GUARD: add PartialFilterExpression to that index and this goes red naming the delete.
         */
        await ExpectIndexScanAsync(failures, PlatformCollections.BusinessReferenceDataValidationResults,
            "ix_business_reference_data_validation_results_tenant_version_rule",
            "BusinessReferenceDataStewardshipRepository.ReplaceValidationResultsAsync (the DeleteMany leg, "
            + "which carries no IsDeleted predicate — a partial filter on the index would strand it)",
            new BsonDocument { { "TenantId", tenant }, { "BusinessReferenceDataVersionId", brdVersion } },
            shape: QueryShape.Delete);

        Assert.True(failures.Count == 0,
            "BL-279 sized these indexes from the repositories that read them, and Mongo is no longer using "
            + "them. A missing index does not raise an error here — the query silently scans the whole "
            + "collection in production:\n" + string.Join("\n", failures));
    }

    /// <summary>Eight documents is enough for the planner to prefer an index and cheap enough to be free.</summary>
    private async Task SeedAsync(string collectionName, Func<int, BsonDocument> factory)
    {
        var documents = Enumerable.Range(0, 8).Select(i =>
        {
            var d = factory(i);
            d["_id"] = new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard);
            return d;
        }).ToList();
        await _database.GetCollection<BsonDocument>(collectionName).InsertManyAsync(documents);
    }

    /*
     * ⚠ A DELETE IS PLANNED SEPARATELY FROM THE FIND THAT LOOKS IDENTICAL, AND THE DIFFERENCE IS THE WHOLE
     * POINT FOR ONE INDEX HERE. Explaining `find {TenantId, VersionId}` and calling that "the delete leg"
     * would be a lie in exactly the case that matters: a partial index is refused for a delete whose filter
     * cannot be proved a subset of the partial expression, and the equivalent find would happily report an
     * IXSCAN. So the delete is explained AS a delete.
     */
    private enum QueryShape { Find, Delete }

    private async Task ExpectIndexScanAsync(
        List<string> failures,
        string collectionName,
        string expectedIndex,
        string callSite,
        BsonDocument filter,
        BsonDocument? sort = null,
        bool forbidBlockingSort = false,
        QueryShape shape = QueryShape.Find)
    {
        BsonDocument command;
        if (shape == QueryShape.Delete)
        {
            command = new BsonDocument
            {
                { "delete", collectionName },
                { "deletes", new BsonArray { new BsonDocument { { "q", filter }, { "limit", 0 } } } }
            };
        }
        else
        {
            var find = new BsonDocument { { "find", collectionName }, { "filter", filter } };
            if (sort is not null)
            {
                find["sort"] = sort;
            }

            command = find;
        }

        var explained = await _database.RunCommandAsync<BsonDocument>(
            new BsonDocument { { "explain", command }, { "verbosity", "queryPlanner" } });

        var stages = new List<string>();
        var indexes = new List<string>();
        for (var node = explained["queryPlanner"]["winningPlan"].AsBsonDocument; node is not null;)
        {
            stages.Add(node.GetValue("stage", "?").AsString);
            if (node.TryGetValue("indexName", out var name))
            {
                indexes.Add(name.AsString);
            }

            node = node.TryGetValue("inputStage", out var next) ? next.AsBsonDocument
                : node.TryGetValue("queryPlan", out var qp) ? qp.AsBsonDocument
                : null;
        }

        var plan = string.Join("->", stages);
        if (!indexes.Contains(expectedIndex))
        {
            failures.Add($"{collectionName}: {callSite} planned as [{plan}] — expected an IXSCAN on "
                + $"'{expectedIndex}'. {(stages.Contains("COLLSCAN") ? "It is scanning the whole collection." : "")}");
        }

        if (forbidBlockingSort && stages.Contains("SORT"))
        {
            failures.Add($"{collectionName}: {callSite} planned as [{plan}] — the index no longer serves the "
                + "sort, so Mongo is ordering in memory and will blow the 32MB sort limit as the data grows.");
        }
    }

    // ── THE TWO DATA JOBS MUST SURVIVE THE SPLIT ───────────────────────────────────────────────────────────

    [Fact]
    public async Task TheProductionPathStillBuildsEverythingAndRunsBothDataJobs()
    {
        /*
         * ⚠ ONE TEST, THREE ASSERTIONS, AND THAT IS DELIBERATE. This is the only place that pays for the FULL
         * 82-collection schema, and each rebuild of it is roughly six hundred files on disk — the very cost
         * this round exists to remove. Splitting it into "builds everything" and "runs the data jobs" would
         * double that for no extra coverage, so the expensive setup is shared.
         *
         * (a) EnsureIndexesAsync still builds the WHOLE manifest. The union test in PlatformSchemaManifestTests
         *     proves the LIST is complete; only this proves the list is actually applied.
         * (b) + (c) Both startup DATA jobs still run. This is the easiest thing in the round to break in
         *     silence: EnsureIndexesAsync used to do three jobs under a name that promised one, and splitting
         *     the declarative part out is exactly how a startup repair gets lost — it moves to a new file,
         *     nothing calls the new file, and nothing fails. The repair just stops happening, forever.
         *
         * MUTATION GUARD: delete either call from PlatformSchemaMigrations.RunAsync and this goes red naming
         * the job; narrow EnsureIndexesAsync to a subset and it goes red naming the missing collections.
         */
        var deletedTenantId = Guid.NewGuid();

        await _database.GetCollection<Tenant>(PlatformCollections.Tenants).InsertOneAsync(new Tenant
        {
            Id = deletedTenantId,
            Name = "Gone",
            DisplayName = "Gone",
            Domain = "gone.example.com",
            Code = "GONE-" + deletedTenantId.ToString("N")[..6],
            Slug = "gone-" + deletedTenantId.ToString("N")[..6],
            IsDeleted = true
        });

        var domains = _database.GetCollection<TenantDomain>(PlatformCollections.TenantDomains);
        await domains.InsertOneAsync(new TenantDomain
        {
            Id = Guid.NewGuid(),
            TenantId = deletedTenantId,
            DomainName = "gone.example.com",
            Type = DomainType.Custom,
            IsDeleted = false,
            Status = TenantDomainStatus.Active
        });

        var catalog = _database.GetCollection<BsonDocument>(PlatformCollections.ModuleCatalog);
        var catalogId = Guid.NewGuid();
        await catalog.InsertOneAsync(new BsonDocument
        {
            { "_id", new BsonBinaryData(catalogId, GuidRepresentation.Standard) },
            { "ModuleCode", "MUT-" + catalogId.ToString("N")[..6] },
            { "Category", "retired-value" }
        });

        await MongoDbIndexConfigurations.EnsureIndexesAsync(_database);

        // (a) the whole manifest, not a slice
        var actual = await CollectionNamesAsync();
        var missing = PlatformSchemaManifest.All
            .Where(c => c.Indexes.Count > 0)
            .Select(c => c.Name)
            .Where(n => !actual.Contains(n))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.True(missing.Length == 0,
            "the production path no longer builds the whole manifest — these collections were never "
            + "created:\n" + string.Join("\n", missing));

        // (b) the tenant-domain data repair
        var domain = await domains.Find(Builders<TenantDomain>.Filter.Eq(x => x.TenantId, deletedTenantId))
            .FirstAsync();
        Assert.True(domain.IsDeleted,
            "SoftDeleteDomainsForDeletedTenantsAsync no longer runs on the production path — a deleted "
            + "tenant's domain stayed live.");

        // (c) the retired-field data migration
        var catalogDocument = await catalog
            .Find(Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(catalogId, GuidRepresentation.Standard)))
            .FirstAsync();
        Assert.False(catalogDocument.Contains("Category"),
            "UnsetRetiredModuleCatalogCategoryAsync no longer runs on the production path — the retired "
            + "Category field survived startup.");
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The collections in the database, minus the harness ownership marker — that one is bookkeeping about
    /// the database, not part of the schema, and item 3 asks what the SCHEMA created.
    /// </summary>
    private async Task<HashSet<string>> CollectionNamesAsync()
        => (await _database.ListCollectionNames().ToListAsync())
            .Where(n => !string.Equals(n, MongoResidueSweeper.MarkerCollection, StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

    private async Task<Dictionary<string, BsonDocument>> ListIndexesAsync(string collectionName)
    {
        var cursor = await _database.GetCollection<BsonDocument>(collectionName).Indexes.ListAsync();
        var list = await cursor.ToListAsync();
        return list.ToDictionary(d => d["name"].AsString, d => d, StringComparer.Ordinal);
    }

    private static bool BsonEquals(BsonDocument? left, BsonDocument? right)
        => (left is null && right is null) || (left is not null && right is not null && left.Equals(right));
}
