using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Features.BankMaster;
using Diten.MdmService.Application.Features.BankMaster.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Audit;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests.BankMaster;

/// <summary>
/// MOD-0289 DL-1 — the audit intent of every bank / branch write (AC-6, AC-27's delivery part): one intent per write, in the
/// same document write; a failed write leaves nothing; the delivery finds the tenant, sends the bank operation's own name and
/// waits (bounded) for Platform's map; product intents keep their shape and their name.
/// </summary>
public sealed class BankAuditIntentMongoTests
{
    private const string ContractVersion = AuditIntentDeliveryProcessor.RequiredContractVersion;

    private static async Task<IReadOnlySet<Guid>> DiscoveredTenantsAsync()
    {
        await BankHarness.EnsureCutoverAsync();
        var discovery = new AuditIntentTenantPartitionDiscoveryRepository(
            BankHarness.Database, new AuditIntentTemporalMigrationRepository(BankHarness.Database, TimeProvider.System), TimeProvider.System);
        var found = new HashSet<Guid>();
        Guid? after = null;
        do
        {
            var page = await discovery.DiscoverAsync(after, 100);
            found.UnionWith(page.TenantIds);
            after = page.NextTenantId;
        }
        while (after is not null);
        return found;
    }

    private static AuditIntentDeliveryProcessor Processor(BankHarness h, ProbeClient client) =>
        new(new AuditIntentDeliveryRepository(BankHarness.Database, h.Tenant, TimeProvider.System), new Identity(), client);

    private static Task<AuditIntentDeliveryBatchResult> RunAsync(BankHarness h, ProbeClient client) =>
        Processor(h, client).ProcessTenantAsync(h.TenantId, 20, "bank-test", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5), 3);

    private static TrustedSourceAuditIntentDeliveryResult Accept(TrustedSourceAuditIntentEnvelope envelope) =>
        TrustedSourceAuditIntentDeliveryResult.Accepted(new TrustedSourceAuditIntentAcceptanceReceipt(
            "ack-" + envelope.IntentId.ToString("N"),
            AuditIntentContract.BuildCentralIdempotencyKey(envelope.TenantId, envelope.IntentId, envelope.ContractVersion),
            envelope.ContractVersion, DateTimeOffset.UtcNow, false));

    // ------------------------------------------------------------------------------------------------------- AC-6

    [Fact]
    public async Task Mod0289Dl1_AC06_Every_bank_command_leaves_exactly_one_intent_in_the_same_write()
    {
        var h = new BankHarness();
        var bank = await h.NewBankAsync("Audited", "TR", "AUDITR22", "77");
        var updated = (await h.UpdateBank().Handle(new UpdateBankCommand(bank.Id, BankHarness.Bank("Audited 2", "TR", "AUDITR22", "77"), bank.Version, h.Actor), default)).Data!;
        var active = (await h.ActivateBank().Handle(new ActivateBankCommand(bank.Id, updated.Version, h.Actor), default)).Data!;
        await h.DeactivateBank().Handle(new DeactivateBankCommand(bank.Id, new DeactivateRequest { ReasonCode = "CLOSED" }, active.Version, h.Actor), default);
        var gone = await h.NewBankAsync("To delete");
        await h.DeleteBank().Handle(new DeleteBankCommand(gone.Id, h.Actor), default);

        var stored = await h.StoredBankAsync(bank.Id);
        Assert.Equal(3, stored.Version);
        Assert.Equal([MasterDataAuditOperation.BankCreated, MasterDataAuditOperation.BankUpdated,
                MasterDataAuditOperation.BankActivated, MasterDataAuditOperation.BankDeactivated],
            stored.AuditIntents.Select(i => i.MasterDataOperation!.Value));
        Assert.Equal([-1, 0, 1, 2], stored.AuditIntents.Select(i => i.PreVersion));
        Assert.Equal([0, 1, 2, 3], stored.AuditIntents.Select(i => i.PostVersion));
        Assert.Equal([MasterDataAuditOperation.BankCreated, MasterDataAuditOperation.BankDeleted],
            (await h.StoredBankAsync(gone.Id)).AuditIntents.Select(i => i.MasterDataOperation!.Value));

        foreach (var intent in stored.AuditIntents.Concat((await h.StoredBankAsync(gone.Id)).AuditIntents))
        {
            Assert.Equal(AuditAggregateType.Bank, intent.AggregateType);
            Assert.Equal(h.TenantId, intent.TenantId);
            Assert.Equal(h.Actor.ToString("D"), intent.ActorId);
            Assert.Equal(h.Correlation.ToString("D"), intent.CorrelationId);
            Assert.Equal(AuditIntentDeliveryState.Pending, intent.DeliveryState);
            Assert.Equal(AuditIntentTemporalStorage.CurrentVersion, intent.TemporalStorageVersion);
            Assert.Equal(64, intent.EvidenceHash.Length);
            Assert.All(intent.EvidenceHash, c => Assert.True(Uri.IsHexDigit(c)));
            Assert.Equal(intent.PostVersion + 1L, intent.Sequence);
            // The product operation stays at its default: the intent travels under the master data operation, never under one
            // of the product family's.
            Assert.Equal(default(ProductAuditOperation), intent.Operation);
            // Nothing of the bank's own values in what leaves for the central log.
            Assert.DoesNotContain("Audited", intent.SnapshotReference ?? "");
        }

        var all = stored.AuditIntents.Concat((await h.StoredBankAsync(gone.Id)).AuditIntents).ToList();
        Assert.Equal(6, all.Count); // one per write: 4 on the first bank, 2 on the second
        Assert.Equal(6, all.Select(i => i.IdempotencyKey).Distinct().Count());
        Assert.Equal(6, all.Select(i => i.IntentId).Distinct().Count());
    }

    [Fact]
    public async Task Mod0289Dl1_AC06_Every_branch_command_leaves_exactly_one_intent_in_the_same_write()
    {
        var h = new BankHarness();
        var bank = await h.NewBankAsync("Parent");
        var branch = await h.NewBranchAsync(bank.Id, "001");
        var edited = (await h.UpdateBranch().Handle(new UpdateBankBranchCommand(branch.Id, BankHarness.Branch("001", "Renamed"), branch.Version, h.Actor), default)).Data!;
        var active = (await h.ActivateBranch().Handle(new ActivateBankBranchCommand(branch.Id, edited.Version, h.Actor), default)).Data!;
        await h.DeactivateBranch().Handle(new DeactivateBankBranchCommand(branch.Id, new DeactivateRequest { ReasonCode = "CLOSED" }, active.Version, h.Actor), default);
        var gone = await h.NewBranchAsync(bank.Id, "002");
        await h.DeleteBranch().Handle(new DeleteBankBranchCommand(gone.Id, h.Actor), default);

        Assert.Equal([MasterDataAuditOperation.BankBranchCreated, MasterDataAuditOperation.BankBranchUpdated,
                MasterDataAuditOperation.BankBranchActivated, MasterDataAuditOperation.BankBranchDeactivated],
            (await h.StoredBranchAsync(branch.Id)).AuditIntents.Select(i => i.MasterDataOperation!.Value));
        Assert.Equal([MasterDataAuditOperation.BankBranchCreated, MasterDataAuditOperation.BankBranchDeleted],
            (await h.StoredBranchAsync(gone.Id)).AuditIntents.Select(i => i.MasterDataOperation!.Value));
        Assert.All((await h.StoredBranchAsync(branch.Id)).AuditIntents, i => Assert.Equal(AuditAggregateType.BankBranch, i.AggregateType));
    }

    [Fact]
    public async Task Mod0289Dl1_AC06_A_store_that_cannot_write_the_intent_answers_503_and_the_business_data_is_not_written()
    {
        var appName = "mod0289-fp-" + Guid.NewGuid().ToString("N");
        var failing = new BankHarness(database: BankHarness.Open(appName));
        // Prepared first: the write below must reach the store, not stop at the index preparation.
        await ((BankRepository)failing.Banks).WhenIndexesReadyAsync();
        var existing = await failing.NewBankAsync("Existing", activate: false);

        // CREATE: the insert is refused by the server.
        await using (await FailPoint.ArmAsync(appName, "insert"))
        {
            var created = await failing.CreateBank().Handle(new CreateBankCommand(BankHarness.Bank("Never written"), failing.Actor), default);
            Assert.False(created.IsSuccessful);
            Assert.Equal(503, created.StatusCode);
            Assert.Equal([BankErrorCodes.AuditUnavailable], created.Errors);
        }

        Assert.Equal(1, await failing.BankCollection.CountDocumentsAsync(x => x.TenantId == failing.TenantId));
        Assert.Equal(0, await failing.BankCollection.CountDocumentsAsync(x => x.TenantId == failing.TenantId && x.Name == "Never written"));

        // UPDATE and DELETE: the conditional write is refused — the business fields did not move, no intent was added.
        await using (await FailPoint.ArmAsync(appName, "update"))
        {
            var updated = await failing.UpdateBank().Handle(
                new UpdateBankCommand(existing.Id, BankHarness.Bank("Never renamed"), existing.Version, failing.Actor), default);
            Assert.Equal((false, 503, BankErrorCodes.AuditUnavailable), (updated.IsSuccessful, updated.StatusCode, updated.Errors.Single()));
        }

        await using (await FailPoint.ArmAsync(appName, "update"))
        {
            var deleted = await failing.DeleteBank().Handle(new DeleteBankCommand(existing.Id, failing.Actor), default);
            Assert.Equal((false, 503, BankErrorCodes.AuditUnavailable), (deleted.IsSuccessful, deleted.StatusCode, deleted.Errors.Single()));
        }

        var stored = await failing.StoredBankAsync(existing.Id);
        Assert.Equal(("Existing", 0, false), (stored.Name, stored.Version, stored.IsDeleted));
        Assert.Single(stored.AuditIntents);
    }

    [Fact]
    public async Task Mod0289Dl1_AC06_The_store_refuses_a_write_without_its_intent_before_touching_anything()
    {
        var h = new BankHarness();
        var bare = Bank.CreateDraft(h.TenantId, "GMG-BNK-NOINTENT", "No intent", "TR", null, null, false, h.Actor, DateTimeOffset.UtcNow);
        Assert.Equal(BankStoreOutcome.AuditIntentUnavailable, await h.Banks.CreateAsync(bare));
        Assert.Equal(0, await h.BankCollection.CountDocumentsAsync(x => x.TenantId == h.TenantId));

        // An intent that belongs to another tenant, another aggregate, or carries no master data operation is not one.
        var bank = await h.NewBankAsync("Real");
        var stored = await h.StoredBankAsync(bank.Id);
        stored.Name = "Renamed";
        LocalAuditIntent Good() => BankAuditIntentFactory.Create(AuditAggregateType.Bank, h.TenantId, bank.Id,
            MasterDataAuditOperation.BankUpdated, 0, 1, Guid.NewGuid(), Guid.NewGuid(), h.Actor, DateTimeOffset.UtcNow, "changed=Name");
        var foreignTenant = Good(); foreignTenant.TenantId = Guid.NewGuid();
        var otherAggregate = Good(); otherAggregate.AggregateId = Guid.NewGuid();
        var noOperation = Good(); noOperation.MasterDataOperation = null;
        var legacyShape = Good(); legacyShape.TemporalStorageVersion = null;
        foreach (var bad in new[] { foreignTenant, otherAggregate, noOperation, legacyShape })
            Assert.Equal(BankStoreOutcome.AuditIntentUnavailable, await h.Banks.UpdateAsync(stored, 0, bad));
        Assert.Equal(BankStoreOutcome.AuditIntentUnavailable, await h.Banks.UpdateAsync(stored, 0, null!));
        Assert.Equal("Real", (await h.StoredBankAsync(bank.Id)).Name);
        Assert.Equal(BankStoreOutcome.Written, await h.Banks.UpdateAsync(stored, 0, Good()));
    }

    [Fact]
    public async Task Mod0289Dl1_AC06_A_product_intent_keeps_its_stored_shape_and_a_bank_intent_stores_its_operation()
    {
        // New product intent: no MasterDataOperation element (the Domain project cannot carry a Bson attribute, so the
        // driver's ShouldSerialize<Member> rule does it).
        var product = new LocalAuditIntent { Operation = ProductAuditOperation.GlobalProductDraftCreated, AggregateType = AuditAggregateType.GlobalProduct };
        Assert.False(product.ToBsonDocument().Contains("MasterDataOperation"));
        Assert.True(product.ToBsonDocument().Contains("Operation"));

        // A bank intent stores it.
        var h = new BankHarness();
        var bank = await h.NewBankAsync("Raw");
        var raw = await BankHarness.Database.GetCollection<BsonDocument>(BankRepository.CollectionName)
            .Find(new BsonDocument("_id", new BsonBinaryData(bank.Id, GuidRepresentation.Standard))).SingleAsync();
        var intent = raw["AuditIntents"].AsBsonArray.Single().AsBsonDocument;
        Assert.Equal((int)MasterDataAuditOperation.BankCreated, intent["MasterDataOperation"].AsInt32);

        // An intent written the OLD way (no such element) reads back as a product intent with no master data operation.
        var legacy = new BsonDocument(product.ToBsonDocument());
        Assert.Null(BsonSerializer.Deserialize<LocalAuditIntent>(legacy).MasterDataOperation);
        Assert.Equal(ProductAuditOperation.GlobalProductDraftCreated, BsonSerializer.Deserialize<LocalAuditIntent>(legacy).Operation);
    }

    // ----------------------------------------------------------------------------------------- AC-6 / AC-27 delivery

    [Fact]
    public async Task Mod0289Dl1_AC06_A_tenant_whose_only_intents_are_in_the_bank_collections_is_found_by_the_delivery_discovery()
    {
        var banksOnly = new BankHarness();
        var branchesOnly = new BankHarness();
        await banksOnly.NewBankAsync("Banks only");
        // The branch-only tenant: its bank's intent is delivered away first, so only the branch collection holds a pending one.
        var parent = await branchesOnly.NewBankAsync("Parent");
        await branchesOnly.NewBranchAsync(parent.Id);
        var drain = new ProbeClient(Accept);
        await RunAsync(branchesOnly, drain);
        await BankHarness.EnsureCutoverAsync();
        // Both of that tenant's intents were delivered; a fresh branch makes ONLY mdm_bank_branches hold a pending intent.
        await branchesOnly.NewBranchAsync(parent.Id, "002");
        Assert.Equal(0, await BankHarness.Database.GetCollection<BsonDocument>(BankRepository.CollectionName).CountDocumentsAsync(
            new BsonDocument { { "TenantId", new BsonBinaryData(branchesOnly.TenantId, GuidRepresentation.Standard) }, { "AuditIntents.DeliveryState", 1 } }));

        var found = await DiscoveredTenantsAsync();
        Assert.Contains(banksOnly.TenantId, found);
        Assert.Contains(branchesOnly.TenantId, found);
    }

    [Fact]
    public async Task Mod0289Dl1_AC06_Delivery_sends_the_bank_operations_own_name_and_compacts_the_acknowledged_intent()
    {
        var h = new BankHarness();
        await BankHarness.EnsureCutoverAsync();
        var bank = await h.NewBankAsync("Delivered");
        var branch = await h.NewBranchAsync(bank.Id);
        var client = new ProbeClient(Accept);

        var result = await RunAsync(h, client);

        Assert.Equal((2, 2, 0), (result.Accepted, result.Claimed, result.DeadLettered));
        var bankEnvelope = Assert.Single(client.Envelopes, e => e.AggregateType == "Bank");
        var branchEnvelope = Assert.Single(client.Envelopes, e => e.AggregateType == "BankBranch");
        Assert.Equal("BankCreated", bankEnvelope.Operation);
        Assert.Equal("BankBranchCreated", branchEnvelope.Operation);
        Assert.Equal((bank.Id, h.TenantId, ContractVersion), (bankEnvelope.AggregateId, bankEnvelope.TenantId, bankEnvelope.ContractVersion));
        Assert.Equal(branch.Id, branchEnvelope.AggregateId);
        // Delivered and compacted: the intent is gone from the document, its receipt is there, the business version untouched.
        var stored = await h.StoredBankAsync(bank.Id);
        Assert.Empty(stored.AuditIntents);
        Assert.Single(stored.AuditIntentReceipts);
        Assert.Equal(0, stored.Version);
        Assert.Empty((await h.StoredBranchAsync(branch.Id)).AuditIntents);
    }

    [Fact]
    public async Task Mod0289Dl1_AC27_A_created_bank_reaches_the_trusted_source_endpoint_as_one_central_record()
    {
        // What Platform will log for "a bank was created": the envelope the trusted-source endpoint receives — the tenant,
        // the actor, the aggregate, the operation, the evidence — and nothing of the bank's values. (The dev-environment
        // proof with the real Platform is the acceptance gate's: it needs Platform's P-12 mapping.)
        var h = new BankHarness();
        await BankHarness.EnsureCutoverAsync();
        var bank = await h.NewBankAsync("Central", "TR", "CENTTR22", "5555");
        var client = new ProbeClient(Accept);
        await RunAsync(h, client);

        var envelope = Assert.Single(client.Envelopes);
        Assert.Equal(("Diten.MDM", "Bank", "BankCreated"), (envelope.SourceService, envelope.AggregateType, envelope.Operation));
        Assert.Equal((h.TenantId, bank.Id, h.Actor.ToString("D")), (envelope.TenantId, envelope.AggregateId, envelope.ActorId));
        Assert.Equal(h.Correlation, envelope.CorrelationId);
        Assert.Equal(64, envelope.EvidenceHash.Length);
        var text = System.Text.Json.JsonSerializer.Serialize(envelope);
        foreach (var value in new[] { "Central", "CENTTR22", "5555", bank.Code }) Assert.DoesNotContain(value, text);
    }

    [Fact]
    public async Task Mod0289Dl1_AC06_A_bank_intent_the_platform_does_not_map_yet_waits_it_is_not_dead_lettered()
    {
        var h = new BankHarness();
        await BankHarness.EnsureCutoverAsync();
        var bank = await h.NewBankAsync("Waiting");
        var client = new ProbeClient(_ => TrustedSourceAuditIntentDeliveryResult.Terminal("AUDIT_SOURCE_INTENT_CONFLICT")
            with { CentralReasonCode = AuditIntentDeliveryProcessor.MappingUnsupported });

        var result = await RunAsync(h, client);

        Assert.Equal((1, 1, 0), (result.RetryScheduled, result.AwaitingCentralMapping, result.DeadLettered));
        // The waiting name is the BANK operation's, not a product name and not "0".
        Assert.Equal(["BankCreated"], result.AwaitingCentralMappingOperationNames);
        Assert.Equal("BankCreated", Assert.Single(client.Envelopes).Operation);
        var intent = Assert.Single((await h.StoredBankAsync(bank.Id)).AuditIntents);
        Assert.Equal(AuditIntentDeliveryState.Pending, intent.DeliveryState);
        Assert.Equal(AuditIntentDeliveryProcessor.MappingUnsupported, intent.LastError);
        Assert.NotNull(intent.NextRetryAt);
    }

    [Fact]
    public async Task Mod0289Dl1_AC06_The_wait_set_holds_every_bank_operation_the_deployed_platform_does_not_map()
    {
        var expected = Enum.GetValues<MasterDataAuditOperation>().Select(o => o.ToString())
            .Where(name => !CentralMappingRollout.MappedByDeployedPlatform.Contains(name)).Order().ToArray();
        Assert.Equal(expected, CentralMappingRollout.AwaitingCentralMappingMasterData.Select(o => o.ToString()).Order());
        Assert.Equal(Enum.GetValues<MasterDataAuditOperation>().Length, CentralMappingRollout.AwaitingCentralMappingMasterData.Count);
        // The product set is the product family's own: no bank operation is in it and no product operation in the bank set.
        Assert.Empty(CentralMappingRollout.AwaitingCentralMapping.Select(o => o.ToString())
            .Intersect(CentralMappingRollout.AwaitingCentralMappingMasterData.Select(o => o.ToString())));
    }

    [Fact]
    public async Task Mod0289Dl1_AC06_A_bank_intent_whose_refusal_timed_out_is_retried_and_any_other_refusal_is_final()
    {
        var timedOut = new BankHarness();
        var refused = new BankHarness();
        await BankHarness.EnsureCutoverAsync();
        await timedOut.NewBankAsync("Slow");
        await refused.NewBankAsync("Refused");

        var retried = await RunAsync(timedOut, new ProbeClient(_ => TrustedSourceAuditIntentDeliveryResult.Terminal("AUDIT_SOURCE_INTENT_CONFLICT")
            with { CentralReasonTimedOut = true }));
        Assert.Equal((1, 0), (retried.RetryScheduled, retried.DeadLettered));

        var dead = await RunAsync(refused, new ProbeClient(_ => TrustedSourceAuditIntentDeliveryResult.Terminal("AUDIT_SOURCE_INTENT_CONFLICT")
            with { CentralReasonCode = "SOMETHING_ELSE" }));
        Assert.Equal((0, 1), (dead.RetryScheduled, dead.DeadLettered));
    }

    [Fact]
    public async Task Mod0289Dl1_AC06_A_product_intent_still_goes_under_its_own_name_and_its_stored_shape_has_no_bank_element()
    {
        var tenantId = Guid.NewGuid();
        var h = new BankHarness(tenantId);
        await BankHarness.EnsureCutoverAsync();
        // A product-family aggregate with its intent, written through the typed document: no MasterDataOperation element.
        var entry = new ProductAbbreviationRegisterEntry
        {
            TenantId = tenantId, Version = 1, NormalizedAbbreviation = "TST", GlobalProductId = Guid.NewGuid()
        };
        var intent = new LocalAuditIntent
        {
            TenantId = tenantId, AggregateId = entry.Id, AggregateType = AuditAggregateType.ProductAbbreviation,
            IntentId = Guid.NewGuid(), Operation = ProductAuditOperation.ProductAbbreviationAllocationRequested,
            ContractVersion = ContractVersion, TimestampUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            ActorId = Guid.NewGuid().ToString("D"), CorrelationId = Guid.NewGuid().ToString("D"),
            CommandId = Guid.NewGuid().ToString("D"), CausationId = Guid.NewGuid().ToString("D"),
            IdempotencyKey = Guid.NewGuid().ToString("D"), EvidenceHash = new string('A', 64),
            PreVersion = 0, PostVersion = 1, Sequence = 1
        };
        AuditIntentTemporalStorage.ApplyCurrentVersion(intent);
        entry.AuditIntents.Add(intent);
        await BankHarness.Database.GetCollection<ProductAbbreviationRegisterEntry>("mdm_product_abbreviation_register").InsertOneAsync(entry);
        var rawIntent = (await BankHarness.Database.GetCollection<BsonDocument>("mdm_product_abbreviation_register")
            .Find(new BsonDocument("_id", new BsonBinaryData(entry.Id, GuidRepresentation.Standard))).SingleAsync())["AuditIntents"].AsBsonArray.Single().AsBsonDocument;
        // The stored shape of a product intent is what it always was: no master data element at all.
        Assert.False(rawIntent.Contains("MasterDataOperation"));
        Assert.Equal((int)ProductAuditOperation.ProductAbbreviationAllocationRequested, rawIntent["Operation"].AsInt32);
        var client = new ProbeClient(Accept);

        var result = await RunAsync(h, client);

        Assert.Equal(1, result.Accepted);
        var envelope = Assert.Single(client.Envelopes);
        Assert.Equal("ProductAbbreviation", envelope.AggregateType);
        Assert.Equal(nameof(ProductAuditOperation.ProductAbbreviationAllocationRequested), envelope.Operation);
    }

    // ------------------------------------------------------------------------------------------------- the test seams

    private sealed class Identity : ITrustedSourceAuditServiceIdentityProvider
    {
        public Task<TrustedSourceAuditServiceIdentity> GetAsync(Guid tenantId, string audience, bool forceRefresh, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TrustedSourceAuditServiceIdentity("token", DateTimeOffset.UtcNow.AddMinutes(5)));
    }

    private sealed class ProbeClient(Func<TrustedSourceAuditIntentEnvelope, TrustedSourceAuditIntentDeliveryResult> answer) : ITrustedSourceAuditIntentClient
    {
        public List<TrustedSourceAuditIntentEnvelope> Envelopes { get; } = [];

        public Task<TrustedSourceAuditIntentDeliveryResult> AcceptAsync(
            TrustedSourceAuditIntentEnvelope envelope, TrustedSourceAuditServiceIdentity identity, CancellationToken cancellationToken = default)
        {
            Envelopes.Add(envelope);
            return Task.FromResult(answer(envelope));
        }
    }

    /// <summary>A server-side fault for ONE client (matched by its application name) — never another test's writes.</summary>
    private sealed class FailPoint : IAsyncDisposable
    {
        private FailPoint() { }

        public static async Task<FailPoint> ArmAsync(string appName, params string[] commands)
        {
            await Admin(new BsonDocument
            {
                { "configureFailPoint", "failCommand" },
                { "mode", new BsonDocument("times", 1) },
                // errorCode 2 (BadValue) is not a retryable write error: the driver does not quietly try again.
                { "data", new BsonDocument { { "failCommands", new BsonArray(commands) }, { "errorCode", 2 }, { "appName", appName } } }
            });
            return new FailPoint();
        }

        public async ValueTask DisposeAsync() =>
            await Admin(new BsonDocument { { "configureFailPoint", "failCommand" }, { "mode", "off" } });

        private static Task Admin(BsonDocument command) =>
            BankHarness.Database.Client.GetDatabase("admin").RunCommandAsync<BsonDocument>(command);
    }
}
