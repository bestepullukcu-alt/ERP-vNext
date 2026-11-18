using Diten.MdmService.Application.Contracts.RecordCodes;
using Diten.MdmService.Application.Features.BankMaster;
using Diten.MdmService.Application.Features.BankMaster.Commands;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using Diten.Shared.Core;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests.BankMaster;

/// <summary>
/// MOD-0289 DL-1 — the bank and branch commands over the PRODUCTION handlers, repositories and unique indexes, on the
/// test-owned replica-set mongod (MdmTestMongo). Every test makes its own tenant; the fake is only the record code service.
/// </summary>
public sealed class BankCommandMongoTests
{
    private static void Failed<T>(Response<T> response, string code, int status)
    {
        Assert.False(response.IsSuccessful);
        Assert.Equal(status, response.StatusCode);
        Assert.Equal([code], response.Errors);
    }

    private static BsonDocument TenantEmpty() => new("TenantId", new BsonBinaryData(Guid.Empty, GuidRepresentation.Standard));

    // ------------------------------------------------------------------------------------------------------- AC-1

    [Fact]
    public async Task Mod0289Dl1_AC01_Create_returns_a_Draft_bank_with_the_service_code_and_the_trusted_tenant()
    {
        var h = new BankHarness();
        var response = await h.CreateBank().Handle(
            new CreateBankCommand(BankHarness.Bank("First Bank", "tr", "abcd tr22", " 12-34 "), h.Actor), default);

        Assert.True(response.IsSuccessful, string.Join(",", response.Errors));
        Assert.Equal(201, response.StatusCode);
        var dto = response.Data!;
        Assert.Equal("Draft", dto.Status);
        Assert.Equal(0, dto.Version);
        Assert.Equal("TR", dto.CountryCode);
        Assert.Equal("ABCDTR22", dto.Bic);
        Assert.Equal("1234", dto.NationalBankCode);

        // The code is the service's, taken with ONE call: group prefix, no company, type BNK, a key made by the server.
        var call = Assert.Single(h.Codes.Calls);
        Assert.Equal(RecordCodePrefixScope.Group, call.Scope);
        Assert.Null(call.LegalEntityId);
        Assert.Equal("BNK", call.RecordType);
        Assert.InRange(call.IdempotencyKey.Length, 1, 200);
        Assert.All(call.IdempotencyKey, c => Assert.InRange(c, '!', '~'));
        Assert.Equal("GMG-BNK-000001", dto.Code);

        // The tenant is written by the server and is not part of the answer.
        Assert.DoesNotContain(typeof(BankDetailDto).GetProperties(), p => p.Name.Contains("Tenant", StringComparison.OrdinalIgnoreCase));
        var stored = await h.StoredBankAsync(dto.Id);
        Assert.Equal(h.TenantId, stored.TenantId);
        Assert.Equal(h.Actor, stored.CreatedBy);
        Assert.Equal(0, stored.DomesticKeyFenceVersion);
        Assert.False(stored.DomesticKeyIncludesBranch);
    }

    [Fact]
    public async Task Mod0289Dl1_AC01_Every_create_attempt_makes_its_own_idempotency_key_and_takes_its_own_code()
    {
        var h = new BankHarness();
        var first = await h.NewBankAsync("A");
        var second = await h.NewBankAsync("B");
        Assert.NotEqual(first.Code, second.Code);
        Assert.Equal(2, h.Codes.Calls.Count);
        Assert.NotEqual(h.Codes.Calls[0].IdempotencyKey, h.Codes.Calls[1].IdempotencyKey);
    }

    [Fact]
    public async Task Mod0289Dl1_AC01_The_store_refuses_an_empty_tenant_and_no_document_carries_one()
    {
        var empty = new Diten.MdmService.Application.Common.TenantContext();
        empty.SetTenant(Guid.Empty);
        Assert.Throws<InvalidOperationException>(() => new BankRepository(BankHarness.Database, empty));
        Assert.Throws<InvalidOperationException>(() => new BankBranchRepository(BankHarness.Database, empty));

        // A document of another tenant than the repository's is refused as well (the tenant is never the caller's choice).
        var h = new BankHarness();
        var stranger = Bank.CreateDraft(Guid.NewGuid(), "X-1", "N", "TR", null, null, false, h.Actor, DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Banks.CreateAsync(stranger));

        // AC-26's part that falls on DL-1: after writes, no bank or branch document has the empty tenant.
        var bank = await h.NewBankAsync();
        await h.NewBranchAsync(bank.Id);
        Assert.Equal(0, await BankHarness.Database.GetCollection<BsonDocument>(BankRepository.CollectionName)
            .CountDocumentsAsync(TenantEmpty()));
        Assert.Equal(0, await BankHarness.Database.GetCollection<BsonDocument>(BankBranchRepository.CollectionName)
            .CountDocumentsAsync(TenantEmpty()));
    }

    // ------------------------------------------------------------------------------------------------------- AC-2

    [Fact]
    public async Task Mod0289Dl1_AC02_Duplicate_BIC_returns_409_and_the_same_BIC_is_fine_in_another_tenant_or_after_a_delete()
    {
        var h = new BankHarness();
        var first = await h.NewBankAsync("A", bic: "ABCDTR22");

        var duplicate = await h.CreateBank().Handle(new CreateBankCommand(BankHarness.Bank("B", bic: "ABCDTR22"), h.Actor), default);
        Failed(duplicate, BankErrorCodes.BicConflict, 409);

        // Another tenant: the index is per tenant.
        var other = new BankHarness();
        Assert.True((await other.CreateBank().Handle(new CreateBankCommand(BankHarness.Bank("A", bic: "ABCDTR22"), other.Actor), default)).IsSuccessful);

        // A deleted bank no longer holds its BIC (partial: IsDeleted = false).
        Assert.True((await h.DeleteBank().Handle(new DeleteBankCommand(first.Id, h.Actor), default)).IsSuccessful);
        Assert.True((await h.CreateBank().Handle(new CreateBankCommand(BankHarness.Bank("C", bic: "ABCDTR22"), h.Actor), default)).IsSuccessful);
    }

    [Fact]
    public async Task Mod0289Dl1_AC02_Duplicate_country_and_national_code_returns_409_but_another_country_is_fine()
    {
        var h = new BankHarness();
        await h.NewBankAsync("A", "TR", national: "0010");
        Failed(await h.CreateBank().Handle(new CreateBankCommand(BankHarness.Bank("B", "TR", national: "00-10"), h.Actor), default),
            BankErrorCodes.NationalCodeConflict, 409);
        Assert.True((await h.CreateBank().Handle(new CreateBankCommand(BankHarness.Bank("C", "DE", national: "0010"), h.Actor), default)).IsSuccessful);
        // No national code at all is not a value: any number of banks have none.
        await h.NewBankAsync("D", "TR");
        await h.NewBankAsync("E", "TR");
    }

    [Fact]
    public async Task Mod0289Dl1_AC02_Duplicate_branch_code_inside_a_bank_returns_409_but_another_bank_is_fine()
    {
        var h = new BankHarness();
        var bank = await h.NewBankAsync("A");
        var otherBank = await h.NewBankAsync("B");
        var branch = await h.NewBranchAsync(bank.Id, "0 01");
        Assert.Equal("001", branch.BranchCode);
        Failed(await h.CreateBranch().Handle(new CreateBankBranchCommand(bank.Id, BankHarness.Branch("001"), h.Actor), default),
            BankErrorCodes.BranchCodeConflict, 409);
        Assert.True((await h.CreateBranch().Handle(new CreateBankBranchCommand(otherBank.Id, BankHarness.Branch("001"), h.Actor), default)).IsSuccessful);
        // After the branch is deleted its code is free again.
        Assert.True((await h.DeleteBranch().Handle(new DeleteBankBranchCommand(branch.Id, h.Actor), default)).IsSuccessful);
        Assert.True((await h.CreateBranch().Handle(new CreateBankBranchCommand(bank.Id, BankHarness.Branch("001"), h.Actor), default)).IsSuccessful);
    }

    [Fact]
    public async Task Mod0289Dl1_AC02_Concurrent_creates_with_the_same_BIC_let_exactly_one_in_on_a_real_replica_set()
    {
        var h = new BankHarness();
        await ((BankRepository)h.Banks).WhenIndexesReadyAsync();
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
            h.CreateBank().Handle(new CreateBankCommand(BankHarness.Bank("Racer " + i, bic: "RACETR22"), h.Actor), default)));

        Assert.Single(results, r => r.IsSuccessful);
        Assert.All(results.Where(r => !r.IsSuccessful), r => Failed(r, BankErrorCodes.BicConflict, 409));
        Assert.Equal(1, await h.BankCollection.CountDocumentsAsync(x => x.TenantId == h.TenantId));
    }

    [Fact]
    public async Task Mod0289Dl1_AC02_A_branch_cannot_be_attached_to_another_bank_and_its_BIC_must_fit_its_bank()
    {
        var h = new BankHarness();
        var bank = await h.NewBankAsync("A", bic: "ABCDTR22");
        var other = await h.NewBankAsync("B");

        Failed(await h.CreateBranch().Handle(
                new CreateBankBranchCommand(bank.Id, BankHarness.Branch() with { BankId = other.Id }, h.Actor), default),
            BankErrorCodes.BranchBankMismatch, 400);
        Failed(await h.CreateBranch().Handle(
                new CreateBankBranchCommand(bank.Id, BankHarness.Branch(bic: "ZZZZTR22123"), h.Actor), default),
            BankErrorCodes.ValidationFailed, 400);
        var branch = await h.NewBranchAsync(bank.Id, "001", "ABCDTR22123");
        Assert.Equal("ABCDTR22123", branch.Bic);

        // Update: the branch cannot be moved to another bank either.
        Failed(await h.UpdateBranch().Handle(new UpdateBankBranchCommand(
                branch.Id, BankHarness.Branch() with { BankId = other.Id }, branch.Version, h.Actor), default),
            BankErrorCodes.BranchBankMismatch, 400);
        Assert.Equal(bank.Id, (await h.StoredBranchAsync(branch.Id)).BankId);
        // A bank that is not the tenant's is not found.
        Failed(await h.CreateBranch().Handle(new CreateBankBranchCommand(Guid.NewGuid(), BankHarness.Branch(), h.Actor), default),
            BankErrorCodes.NotFound, 404);
    }

    [Fact]
    public async Task Mod0289Dl1_AC02_The_unique_indexes_are_exactly_the_four_of_the_pack()
    {
        var h = new BankHarness();
        await ((BankRepository)h.Banks).WhenIndexesReadyAsync();
        await ((BankBranchRepository)h.Branches).WhenIndexesReadyAsync();
        var banks = await (await BankHarness.Database.GetCollection<BsonDocument>(BankRepository.CollectionName).Indexes.ListAsync()).ToListAsync();
        var branches = await (await BankHarness.Database.GetCollection<BsonDocument>(BankBranchRepository.CollectionName).Indexes.ListAsync()).ToListAsync();

        var code = banks.Single(i => i["name"] == "ux_mdm_bank_code");
        Assert.True(code["unique"].ToBoolean());
        // A deleted bank's code stays taken: NO partial filter on this one (RCS-001 §6).
        Assert.False(code.Contains("partialFilterExpression"));
        Assert.Equal(["TenantId", "Code"], code["key"].AsBsonDocument.Names);

        var bic = banks.Single(i => i["name"] == "ux_mdm_bank_bic");
        Assert.True(bic["unique"].ToBoolean());
        Assert.Equal(["TenantId", "Bic"], bic["key"].AsBsonDocument.Names);
        Assert.Contains("IsDeleted", bic["partialFilterExpression"].AsBsonDocument.Names);
        Assert.Contains("Bic", bic["partialFilterExpression"].AsBsonDocument.Names);

        var national = banks.Single(i => i["name"] == "ux_mdm_bank_national_code");
        Assert.True(national["unique"].ToBoolean());
        Assert.Equal(["TenantId", "CountryCode", "NationalBankCode"], national["key"].AsBsonDocument.Names);
        Assert.Contains("NationalBankCode", national["partialFilterExpression"].AsBsonDocument.Names);

        var branch = branches.Single(i => i["name"] == "ux_mdm_bank_branch_code");
        Assert.True(branch["unique"].ToBoolean());
        Assert.Equal(["TenantId", "BankId", "BranchCode"], branch["key"].AsBsonDocument.Names);
        Assert.Contains("IsDeleted", branch["partialFilterExpression"].AsBsonDocument.Names);
    }

    // ------------------------------------------------------------------------------------------------------- AC-3

    [Fact]
    public async Task Mod0289Dl1_AC03_Another_tenants_bank_and_branch_answer_404_to_every_read_and_write()
    {
        var owner = new BankHarness();
        var bank = await owner.NewBankAsync("Owned", bic: "OWNRTR22");
        var branch = await owner.NewBranchAsync(bank.Id);
        var foreign = new BankHarness();

        Failed(await foreign.GetBank().Handle(new Features.BankMaster.Queries.GetBankByIdQuery(bank.Id), default), BankErrorCodes.NotFound, 404);
        Failed(await foreign.GetBranch().Handle(new Features.BankMaster.Queries.GetBankBranchByIdQuery(branch.Id), default), BankErrorCodes.BranchNotFound, 404);
        Failed(await foreign.ListBranches().Handle(new Features.BankMaster.Queries.GetBankBranchListQuery { BankId = bank.Id }, default), BankErrorCodes.NotFound, 404);
        Failed(await foreign.UpdateBank().Handle(new UpdateBankCommand(bank.Id, BankHarness.Bank("Hijack"), bank.Version, foreign.Actor), default), BankErrorCodes.NotFound, 404);
        Failed(await foreign.ActivateBank().Handle(new ActivateBankCommand(bank.Id, bank.Version, foreign.Actor), default), BankErrorCodes.NotFound, 404);
        Failed(await foreign.DeactivateBank().Handle(new DeactivateBankCommand(bank.Id, new DeactivateRequest { ReasonCode = "X" }, bank.Version, foreign.Actor), default), BankErrorCodes.NotFound, 404);
        Failed(await foreign.DeleteBank().Handle(new DeleteBankCommand(bank.Id, foreign.Actor), default), BankErrorCodes.NotFound, 404);
        Failed(await foreign.BulkDeleteBank().Handle(new BulkDeleteBankCommand(new BulkDeleteBanksRequest { Ids = [bank.Id] }, foreign.Actor), default), BankErrorCodes.NotFound, 404);
        Failed(await foreign.CreateBranch().Handle(new CreateBankBranchCommand(bank.Id, BankHarness.Branch("002"), foreign.Actor), default), BankErrorCodes.NotFound, 404);
        Failed(await foreign.UpdateBranch().Handle(new UpdateBankBranchCommand(branch.Id, BankHarness.Branch("009"), branch.Version, foreign.Actor), default), BankErrorCodes.BranchNotFound, 404);
        Failed(await foreign.ActivateBranch().Handle(new ActivateBankBranchCommand(branch.Id, branch.Version, foreign.Actor), default), BankErrorCodes.BranchNotFound, 404);
        Failed(await foreign.DeleteBranch().Handle(new DeleteBankBranchCommand(branch.Id, foreign.Actor), default), BankErrorCodes.BranchNotFound, 404);

        var list = await foreign.List().Handle(new Features.BankMaster.Queries.GetBankListQuery(), default);
        Assert.Empty(list.Data!.Items);

        // Nothing of the owner's moved.
        var stored = await owner.StoredBankAsync(bank.Id);
        Assert.Equal((bank.Version, "Owned", false), (stored.Version, stored.Name, stored.IsDeleted));
        Assert.Equal(branch.Version, (await owner.StoredBranchAsync(branch.Id)).Version);
    }

    // ------------------------------------------------------------------------------------------------------- AC-4

    [Fact]
    public async Task Mod0289Dl1_AC04_Deleting_a_Draft_is_a_soft_delete_with_UTC_time_and_the_code_stays_taken()
    {
        var h = new BankHarness();
        var bank = await h.NewBankAsync("Gone", bic: "GONETR22");
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        var deleted = await h.DeleteBank().Handle(new DeleteBankCommand(bank.Id, h.Actor), default);
        Assert.True(deleted.IsSuccessful);
        Assert.Equal(204, deleted.StatusCode);

        var stored = await h.StoredBankAsync(bank.Id); // the document is still there — soft
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(TimeSpan.Zero, stored.DeletedAt!.Value.Offset);
        Assert.True(stored.DeletedAt >= before);
        Assert.Equal(h.Actor, stored.UpdatedBy);

        Failed(await h.GetBank().Handle(new Features.BankMaster.Queries.GetBankByIdQuery(bank.Id), default), BankErrorCodes.NotFound, 404);
        Assert.DoesNotContain((await h.List().Handle(new Features.BankMaster.Queries.GetBankListQuery(), default)).Data!.Items, i => i.Id == bank.Id);
        Failed(await h.DeleteBank().Handle(new DeleteBankCommand(bank.Id, h.Actor), default), BankErrorCodes.NotFound, 404);

        // The code is never reused: the unique code index has no IsDeleted filter, so even a document made by hand with the
        // deleted bank's code is refused — the store's answer is the code conflict, the handler's is a 503 (not a made-up code).
        var reused = Bank.CreateDraft(h.TenantId, bank.Code, "Reuse", "TR", null, null, false, h.Actor, DateTimeOffset.UtcNow);
        reused.AuditIntents.Add(BankAuditIntentFactory.Create(AuditAggregateType.Bank, h.TenantId, reused.Id,
            MasterDataAuditOperation.BankCreated, -1, 0, Guid.NewGuid(), Guid.NewGuid(), h.Actor, DateTimeOffset.UtcNow, "x"));
        Assert.Equal(BankStoreOutcome.CodeConflict, await h.Banks.CreateAsync(reused));
        h.Codes.Answer = () => RecordCodeAllocationResult.Allocated(bank.Code, 99);
        Failed(await h.CreateBank().Handle(new CreateBankCommand(BankHarness.Bank("Again"), h.Actor), default),
            BankErrorCodes.RecordCodeUnavailable, 503);
    }

    [Fact]
    public async Task Mod0289Dl1_AC04_A_bank_that_was_ever_activated_is_not_deleted()
    {
        var h = new BankHarness();
        var active = await h.NewBankAsync("Active", activate: true);
        Failed(await h.DeleteBank().Handle(new DeleteBankCommand(active.Id, h.Actor), default), BankErrorCodes.StateInvalid, 409);
        var deactivated = (await h.DeactivateBank().Handle(
            new DeactivateBankCommand(active.Id, new DeactivateRequest { ReasonCode = "MERGED" }, active.Version, h.Actor), default)).Data!;
        Failed(await h.DeleteBank().Handle(new DeleteBankCommand(active.Id, h.Actor), default), BankErrorCodes.StateInvalid, 409);
        var stored = await h.StoredBankAsync(active.Id);
        Assert.False(stored.IsDeleted);
        Assert.Equal(deactivated.Version, stored.Version);
    }

    [Fact]
    public async Task Mod0289Dl1_AC04_Bulk_delete_is_all_or_none_and_leaves_one_intent_per_id()
    {
        var h = new BankHarness();
        var draftA = await h.NewBankAsync("A");
        var draftB = await h.NewBankAsync("B");
        var active = await h.NewBankAsync("C", activate: true);

        // One id that cannot be deleted: the whole request is 409, nothing is deleted.
        Failed(await h.BulkDeleteBank().Handle(new BulkDeleteBankCommand(
            new BulkDeleteBanksRequest { Ids = [draftA.Id, draftB.Id, active.Id] }, h.Actor), default), BankErrorCodes.StateInvalid, 409);
        // One id that does not exist: 404, nothing is deleted.
        Failed(await h.BulkDeleteBank().Handle(new BulkDeleteBankCommand(
            new BulkDeleteBanksRequest { Ids = [draftA.Id, Guid.NewGuid()] }, h.Actor), default), BankErrorCodes.NotFound, 404);
        foreach (var id in new[] { draftA.Id, draftB.Id, active.Id })
        {
            var stored = await h.StoredBankAsync(id);
            Assert.False(stored.IsDeleted);
        }

        var done = await h.BulkDeleteBank().Handle(new BulkDeleteBankCommand(
            new BulkDeleteBanksRequest { Ids = [draftA.Id, draftB.Id] }, h.Actor), default);
        Assert.True(done.IsSuccessful, string.Join(",", done.Errors));
        foreach (var id in new[] { draftA.Id, draftB.Id })
        {
            var stored = await h.StoredBankAsync(id);
            Assert.True(stored.IsDeleted);
            Assert.NotNull(stored.DeletedAt);
            Assert.Equal([MasterDataAuditOperation.BankCreated, MasterDataAuditOperation.BankDeleted],
                stored.AuditIntents.Select(i => i.MasterDataOperation!.Value));
        }

        Assert.False((await h.StoredBankAsync(active.Id)).IsDeleted);
    }

    // ------------------------------------------------------------------------------------------------------- AC-5

    [Fact]
    public async Task Mod0289Dl1_AC05_Status_moves_are_only_those_of_the_table_and_an_Inactive_bank_is_not_edited()
    {
        var h = new BankHarness();
        var bank = await h.NewBankAsync("Flow", "TR", "FLOWTR22");
        // Draft: Deactivate is not a move.
        Failed(await h.DeactivateBank().Handle(new DeactivateBankCommand(bank.Id, new DeactivateRequest { ReasonCode = "R" }, bank.Version, h.Actor), default),
            BankErrorCodes.StateInvalid, 409);

        var active = (await h.ActivateBank().Handle(new ActivateBankCommand(bank.Id, bank.Version, h.Actor), default)).Data!;
        Assert.Equal("Active", active.Status);
        Assert.NotNull(active.ActivatedAt);
        Assert.Equal(h.Actor, active.ActivatedBy);
        // Active: Activate again is not a move.
        Failed(await h.ActivateBank().Handle(new ActivateBankCommand(bank.Id, active.Version, h.Actor), default), BankErrorCodes.StateInvalid, 409);

        // Active: the limited edit — the name moves, the identity fields do not.
        var renamed = await h.UpdateBank().Handle(new UpdateBankCommand(bank.Id, BankHarness.Bank("Renamed", "TR", "FLOWTR22"), active.Version, h.Actor), default);
        Assert.True(renamed.IsSuccessful, string.Join(",", renamed.Errors));
        Assert.Equal("Renamed", renamed.Data!.Name);
        Failed(await h.UpdateBank().Handle(new UpdateBankCommand(bank.Id, BankHarness.Bank("Renamed", "DE"), renamed.Data.Version, h.Actor), default),
            BankErrorCodes.StateInvalid, 409);
        Failed(await h.UpdateBank().Handle(new UpdateBankCommand(bank.Id, BankHarness.Bank("Renamed", "TR", "OTHRTR22"), renamed.Data.Version, h.Actor), default),
            BankErrorCodes.StateInvalid, 409);

        var inactive = (await h.DeactivateBank().Handle(
            new DeactivateBankCommand(bank.Id, new DeactivateRequest { ReasonCode = "CLOSED" }, renamed.Data.Version, h.Actor), default)).Data!;
        Assert.Equal("Inactive", inactive.Status);
        Assert.Equal("CLOSED", inactive.DeactivationReasonCode);
        Failed(await h.UpdateBank().Handle(new UpdateBankCommand(bank.Id, BankHarness.Bank("Again"), inactive.Version, h.Actor), default),
            BankErrorCodes.StateInvalid, 409);
        Failed(await h.DeactivateBank().Handle(new DeactivateBankCommand(bank.Id, new DeactivateRequest { ReasonCode = "R" }, inactive.Version, h.Actor), default),
            BankErrorCodes.StateInvalid, 409);

        // Inactive → Active again; no four-eyes rule on a bank (the actor created it).
        Assert.Equal("Active", (await h.ActivateBank().Handle(new ActivateBankCommand(bank.Id, inactive.Version, h.Actor), default)).Data!.Status);
    }

    [Fact]
    public async Task Mod0289Dl1_AC05_An_Inactive_bank_takes_no_new_branch_and_deactivating_it_leaves_branches_untouched()
    {
        var h = new BankHarness();
        var bank = await h.NewBankAsync("Parent", activate: true);
        var branch = await h.NewBranchAsync(bank.Id, "001", activate: true);
        var draftBranch = await h.NewBranchAsync(bank.Id, "002");
        var rawBefore = await BankHarness.Database.GetCollection<BsonDocument>(BankBranchRepository.CollectionName)
            .Find(new BsonDocument("TenantId", new BsonBinaryData(h.TenantId, GuidRepresentation.Standard))).ToListAsync();

        var inactive = (await h.DeactivateBank().Handle(
            new DeactivateBankCommand(bank.Id, new DeactivateRequest { ReasonCode = "CLOSED" }, bank.Version, h.Actor), default)).Data!;
        Assert.Equal("Inactive", inactive.Status);

        // Status is not derived: the branch documents are byte-for-byte what they were (same versions, same status).
        var rawAfter = await BankHarness.Database.GetCollection<BsonDocument>(BankBranchRepository.CollectionName)
            .Find(new BsonDocument("TenantId", new BsonBinaryData(h.TenantId, GuidRepresentation.Standard))).ToListAsync();
        Assert.Equal(rawBefore.Count, rawAfter.Count);
        Assert.All(rawBefore, before => Assert.Equal(before, rawAfter.Single(a => a["_id"] == before["_id"])));
        Assert.Equal("Active", (await h.StoredBranchAsync(branch.Id)).Status.ToString());
        Assert.Equal("Draft", (await h.StoredBranchAsync(draftBranch.Id)).Status.ToString());

        // A branch of an Inactive bank is refused: 422 MDM_BANK_INACTIVE.
        Failed(await h.CreateBranch().Handle(new CreateBankBranchCommand(bank.Id, BankHarness.Branch("003"), h.Actor), default),
            BankErrorCodes.Inactive, 422);
    }

    [Fact]
    public async Task Mod0289Dl1_AC05_A_branch_follows_the_same_table()
    {
        var h = new BankHarness();
        var bank = await h.NewBankAsync("Parent");
        var branch = await h.NewBranchAsync(bank.Id, "001");
        Failed(await h.DeactivateBranch().Handle(new DeactivateBankBranchCommand(branch.Id, new DeactivateRequest { ReasonCode = "R" }, branch.Version, h.Actor), default),
            BankErrorCodes.StateInvalid, 409);
        var active = (await h.ActivateBranch().Handle(new ActivateBankBranchCommand(branch.Id, branch.Version, h.Actor), default)).Data!;
        Assert.Equal("Active", active.Status);
        Failed(await h.ActivateBranch().Handle(new ActivateBankBranchCommand(branch.Id, active.Version, h.Actor), default), BankErrorCodes.StateInvalid, 409);
        // Active: name and city only; the code does not move.
        var edited = await h.UpdateBranch().Handle(new UpdateBankBranchCommand(
            branch.Id, BankHarness.Branch("001", "Renamed", city: "Izmir"), active.Version, h.Actor), default);
        Assert.True(edited.IsSuccessful, string.Join(",", edited.Errors));
        Assert.Equal(("Renamed", "Izmir"), (edited.Data!.Name, edited.Data.City));
        Failed(await h.UpdateBranch().Handle(new UpdateBankBranchCommand(
            branch.Id, BankHarness.Branch("009", "Renamed", city: "Izmir"), edited.Data.Version, h.Actor), default), BankErrorCodes.StateInvalid, 409);
        Failed(await h.DeleteBranch().Handle(new DeleteBankBranchCommand(branch.Id, h.Actor), default), BankErrorCodes.StateInvalid, 409);
        var inactive = (await h.DeactivateBranch().Handle(new DeactivateBankBranchCommand(
            branch.Id, new DeactivateRequest { ReasonCode = "CLOSED" }, edited.Data.Version, h.Actor), default)).Data!;
        Failed(await h.UpdateBranch().Handle(new UpdateBankBranchCommand(branch.Id, BankHarness.Branch("001", "X"), inactive.Version, h.Actor), default),
            BankErrorCodes.StateInvalid, 409);
        Assert.Equal("Active", (await h.ActivateBranch().Handle(new ActivateBankBranchCommand(branch.Id, inactive.Version, h.Actor), default)).Data!.Status);
    }

    // ------------------------------------------------------------------------------------------------------- AC-9

    [Fact]
    public async Task Mod0289Dl1_AC09_A_stale_If_Match_is_409_and_nothing_is_overwritten()
    {
        var h = new BankHarness();
        var bank = await h.NewBankAsync("Original");
        var moved = (await h.UpdateBank().Handle(new UpdateBankCommand(bank.Id, BankHarness.Bank("Second"), bank.Version, h.Actor), default)).Data!;
        Assert.Equal(1, moved.Version);

        // The old version again — for an update, an activation and a deactivation.
        Failed(await h.UpdateBank().Handle(new UpdateBankCommand(bank.Id, BankHarness.Bank("Stale write"), bank.Version, h.Actor), default),
            BankErrorCodes.VersionConflict, 409);
        Failed(await h.ActivateBank().Handle(new ActivateBankCommand(bank.Id, bank.Version, h.Actor), default), BankErrorCodes.VersionConflict, 409);
        Failed(await h.DeactivateBank().Handle(new DeactivateBankCommand(bank.Id, new DeactivateRequest { ReasonCode = "R" }, bank.Version, h.Actor), default),
            BankErrorCodes.VersionConflict, 409);
        var stored = await h.StoredBankAsync(bank.Id);
        Assert.Equal((1, "Second", BankLifecycleStatus.Draft), (stored.Version, stored.Name, stored.Status));
        Assert.Equal(2, stored.AuditIntents.Count); // created + the one update; the refused ones left nothing
    }

    [Fact]
    public async Task Mod0289Dl1_AC09_The_store_itself_refuses_a_stale_version_whatever_the_caller_read()
    {
        var h = new BankHarness();
        var bank = await h.NewBankAsync("Original");
        // Two callers read version 0. The second one's write — by the store, which the handler's own check does not guard —
        // must lose to the first: the version is in the write's own filter, not only in the caller's earlier read.
        var first = await h.StoredBankAsync(bank.Id);
        var second = await h.StoredBankAsync(bank.Id);
        first.Name = "First wins";
        second.Name = "Second loses";
        Assert.Equal(BankStoreOutcome.Written, await h.Banks.UpdateAsync(first, 0, Intent(h, first, 0)));
        Assert.Equal(BankStoreOutcome.Stale, await h.Banks.UpdateAsync(second, 0, Intent(h, second, 0)));
        Assert.Equal("First wins", (await h.StoredBankAsync(bank.Id)).Name);
    }

    private static LocalAuditIntent Intent(BankHarness h, Bank bank, int expected) => BankAuditIntentFactory.Create(
        AuditAggregateType.Bank, h.TenantId, bank.Id, MasterDataAuditOperation.BankUpdated, expected, expected + 1,
        Guid.NewGuid(), Guid.NewGuid(), h.Actor, DateTimeOffset.UtcNow, "changed=Name");

    [Fact]
    public async Task Mod0289Dl1_AC09_Two_concurrent_activations_let_exactly_one_in()
    {
        var h = new BankHarness();
        var bank = await h.NewBankAsync("Race");
        await ((BankRepository)h.Banks).WhenIndexesReadyAsync();
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            h.ActivateBank().Handle(new ActivateBankCommand(bank.Id, bank.Version, h.Actor), default)));

        Assert.Single(results, r => r.IsSuccessful);
        Assert.All(results.Where(r => !r.IsSuccessful), r => Assert.Contains(r.Errors.Single(),
            new[] { BankErrorCodes.VersionConflict, BankErrorCodes.StateInvalid }));
        Assert.Equal(409, results.Where(r => !r.IsSuccessful).Select(r => r.StatusCode).Distinct().Single());
        var stored = await h.StoredBankAsync(bank.Id);
        Assert.Equal(1, stored.Version);
        Assert.Equal([MasterDataAuditOperation.BankCreated, MasterDataAuditOperation.BankActivated],
            stored.AuditIntents.Select(i => i.MasterDataOperation!.Value));
    }

    [Fact]
    public async Task Mod0289Dl1_AC09_Two_concurrent_updates_from_one_version_let_exactly_one_in()
    {
        var h = new BankHarness();
        var bank = await h.NewBankAsync("Race");
        await ((BankRepository)h.Banks).WhenIndexesReadyAsync();
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(i =>
            h.UpdateBank().Handle(new UpdateBankCommand(bank.Id, BankHarness.Bank("Writer " + i), bank.Version, h.Actor), default)));
        Assert.Single(results, r => r.IsSuccessful);
        Assert.Equal(1, (await h.StoredBankAsync(bank.Id)).Version);
    }

    // ------------------------------------------------------------------------------------------------------- AC-60

    [Fact]
    public async Task Mod0289Dl1_AC60_When_the_code_service_does_not_answer_cleanly_Create_is_503_and_nothing_is_written()
    {
        var h = new BankHarness();
        h.Codes.Answer = () => RecordCodeAllocationResult.Unavailable("RECORD_CODE_SCOPE_INVALID");
        Failed(await h.CreateBank().Handle(new CreateBankCommand(BankHarness.Bank("No code"), h.Actor), default),
            BankErrorCodes.RecordCodeUnavailable, 503);
        // A "code" that is not a usable value counts as no code.
        foreach (var bad in new[] { "", "  ", new string('X', 65), "BAD CODE" })
        {
            h.Codes.Answer = () => RecordCodeAllocationResult.Allocated(bad, 1);
            Failed(await h.CreateBank().Handle(new CreateBankCommand(BankHarness.Bank("Bad code"), h.Actor), default),
                BankErrorCodes.RecordCodeUnavailable, 503);
        }

        // No record and no audit intent: the collection holds nothing of this tenant.
        Assert.Equal(0, await h.BankCollection.CountDocumentsAsync(x => x.TenantId == h.TenantId));
    }

    [Fact]
    public async Task Mod0289Dl1_AC60_A_port_that_is_not_bound_fails_closed_no_code_means_no_bank()
    {
        var h = new BankHarness();
        var response = await h.CreateBank((IRecordCodeAllocator?)null).Handle(
            new CreateBankCommand(BankHarness.Bank("Unbound"), h.Actor), default);
        Failed(response, BankErrorCodes.RecordCodeUnavailable, 503);
        Assert.Equal(0, await h.BankCollection.CountDocumentsAsync(x => x.TenantId == h.TenantId));
    }

    [Fact]
    public async Task Mod0289Dl1_AC60_A_missing_permission_is_403_and_an_unknown_company_is_422_neither_writes_anything()
    {
        var h = new BankHarness();
        h.Codes.Answer = () => RecordCodeAllocationResult.PermissionMissing("RECORD_CODE_PERMISSION_MISSING");
        Failed(await h.CreateBank().Handle(new CreateBankCommand(BankHarness.Bank("A"), h.Actor), default),
            "RECORD_CODE_PERMISSION_MISSING", 403);
        h.Codes.Answer = () => RecordCodeAllocationResult.PermissionMissing("LEGAL_ENTITY_READ_PERMISSION_MISSING");
        Failed(await h.CreateBank().Handle(new CreateBankCommand(BankHarness.Bank("B"), h.Actor), default),
            "LEGAL_ENTITY_READ_PERMISSION_MISSING", 403);
        h.Codes.Answer = () => RecordCodeAllocationResult.LegalEntityUnknown();
        Failed(await h.CreateBank().Handle(new CreateBankCommand(BankHarness.Bank("C"), h.Actor), default),
            BankErrorCodes.LegalEntityUnknown, 422);

        // Each of the three asked once and was not retried.
        Assert.Equal(3, h.Codes.Calls.Count);
        Assert.Equal(0, await h.BankCollection.CountDocumentsAsync(x => x.TenantId == h.TenantId));
    }

    [Fact]
    public async Task Mod0289Dl1_AC60_The_call_carries_the_group_scope_and_no_company_for_every_bank()
    {
        var h = new BankHarness();
        await h.NewBankAsync("One");
        await h.NewBankAsync("Two");
        Assert.All(h.Codes.Calls, call =>
        {
            Assert.Equal(RecordCodePrefixScope.Group, call.Scope);
            Assert.Null(call.LegalEntityId);
            Assert.Equal("BNK", call.RecordType);
        });
    }
}
