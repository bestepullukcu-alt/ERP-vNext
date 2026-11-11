using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities.RecordCodes;
using Diten.Platform.Domain.Features.RecordCodes;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.RecordCodes;

/// <summary>
/// MOD-0040-FU02 — the counter, the ledger and the group lock on a real replica set, driven over HTTP through the shipped
/// handler, with the PRODUCTION repositories wrapped by recording test decorators where a race must be staged. The pack's
/// inferences about the server are measured here: an in-transaction write conflict on the same unique key (§8.6 case 2),
/// and <c>ModifiedCount = 0</c> for an already-locked setting (AC-17, sequential).
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
[Trait("Category", "Mongo")]
public sealed class RecordCodeAllocationMongoTests : IClassFixture<ReplicaSetClassFixture>
{
    private static readonly Guid TenantA = Guid.Parse("40fc0002-0000-4000-8000-00000000a0a3");
    private static readonly Guid TenantB = Guid.Parse("40fc0002-0000-4000-8000-00000000b0b3");
    private static readonly Guid User = Guid.Parse("40fc0002-0000-4000-8000-0000000000c3");
    private static readonly Guid Myg = Guid.Parse("6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7");
    private static readonly Guid Gpl = Guid.Parse("6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f8");
    private const string PayCreate = "finance-treasury.payment-batches.create";
    private const string ApdCreate = "finance-treasury.ap-invoices.create";

    private readonly DisposableMongoReplicaSet _mongo;

    public RecordCodeAllocationMongoTests(ReplicaSetClassFixture replicaSet) => _mongo = replicaSet.Mongo;

    private async Task<RecordCodeHttpHost> HostAsync(RecordCodeHostOptions? options = null, string database = "diten_record_code_alloc")
    {
        var host = await RecordCodeHttpHost.StartAsync(_mongo, database, options, TenantA, TenantB);
        host.Mdm.Company(Myg, "MYG");
        host.Mdm.Company(Gpl, "GPL");
        return host;
    }

    private static string Pay(Guid? tenant = null) => RecordCodeHttpHost.TenantUserToken(tenant ?? TenantA, User, PayCreate, ApdCreate);
    private static string Bank(Guid? tenant = null) => RecordCodeHttpHost.TenantUserToken(tenant ?? TenantA, User, "mdm.banks.create", "mdm.bank-accounts.create");

    // ── AC-4 ────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC04_fifty_concurrent_allocations_from_no_counter_give_fifty_distinct_codes_and_no_500()
    {
        using var host = await HostAsync();

        var answers = await Task.WhenAll(Enumerable.Range(1, 50).Select(async i =>
            await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("PAY", null, Myg, $"k-{i}")))));

        Assert.All(answers, answer => Assert.Equal(200, answer.Status));
        Assert.Equal(50, answers.Select(a => a.Code).Distinct().Count());
        Assert.Equal(Enumerable.Range(1, 50).Select(i => (long)i), answers.Select(a => a.Sequence!.Value).OrderBy(s => s));
        var counter = await host.Collection("record_code_counters").Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
        Assert.Equal(50L, counter["LastNumber"].ToInt64());
    }

    [Fact]
    public async Task AC04_the_first_creation_race_on_the_counter_is_recovered_not_a_500()
    {
        using var host = await HostAsync();
        var counters = new RecordCodeCounterRepository(new PlatformDbContext(_mongo.Client, host.Database), Tenant(TenantA));

        var numbers = await Task.WhenAll(Enumerable.Range(1, 20).Select(_ => Task.Run(() => counters.NextAsync(TenantA, "RCE", "PAY"))));

        Assert.Equal(Enumerable.Range(1, 20).Select(i => (long)i), numbers.OrderBy(n => n));
    }

    [Fact]
    public async Task AC04_a_lost_upsert_race_E11000_is_retried_onto_the_winners_counter_without_a_second_upsert()
    {
        // The server retries an upsert whose predicate is the unique key itself (MongoDB 4.2+), so the 20-way race above
        // seldom reaches the client's own E11000 recovery; here the loser's E11000 is staged in front of the real collection.
        using var host = await HostAsync();
        var context = new PlatformDbContext(_mongo.Client, host.Database);
        var real = context.GetCollection<RecordCodeCounter>(PlatformCollections.RecordCodeCounters);
        var winner = await new RecordCodeCounterRepository(context, Tenant(TenantA)).NextAsync(TenantA, "RCE", "PAY");
        var upserts = new List<bool>();
        var collection = new Mock<IMongoCollection<RecordCodeCounter>>();
        collection
            .Setup(c => c.FindOneAndUpdateAsync(
                It.IsAny<FilterDefinition<RecordCodeCounter>>(),
                It.IsAny<UpdateDefinition<RecordCodeCounter>>(),
                It.IsAny<FindOneAndUpdateOptions<RecordCodeCounter, RecordCodeCounter>>(),
                It.IsAny<CancellationToken>()))
            .Returns((FilterDefinition<RecordCodeCounter> filter, UpdateDefinition<RecordCodeCounter> update,
                FindOneAndUpdateOptions<RecordCodeCounter, RecordCodeCounter> options, CancellationToken cancellationToken) =>
            {
                upserts.Add(options.IsUpsert);
                return upserts.Count == 1
                    ? throw RecordCodeFaults.DuplicateKey()
                    : real.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
            });
        var dbContext = new Mock<IPlatformDbContext>();
        dbContext.Setup(x => x.GetCollection<RecordCodeCounter>(PlatformCollections.RecordCodeCounters)).Returns(collection.Object);

        var loser = await new RecordCodeCounterRepository(dbContext.Object, Tenant(TenantA)).NextAsync(TenantA, "RCE", "PAY");

        Assert.Equal((1L, 2L), (winner, loser));
        Assert.Equal(new[] { true, false }, upserts);
    }

    // ── AC-5 — replay equality and the four races ───────────────────────────────────────────────────────────

    [Fact]
    public async Task AC05_a_replay_returns_the_same_code_without_counter_MDM_ledger_or_audit_and_another_body_is_400()
    {
        using var host = await HostAsync();
        var first = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("PAY", null, Myg, "k")));
        var mdmCalls = host.Mdm.Requests.Count;

        var v0Then = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("PAY", "LegalEntity", Myg, "k")));
        var upper = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(),
            "{\"recordType\":\"PAY\",\"legalEntityId\":\"" + Myg.ToString("D").ToUpperInvariant() + "\",\"idempotencyKey\":\"k\"}"));
        var otherType = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("APD", null, Myg, "k")));
        var otherScope = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("PAY", "Group", null, "k")));
        var otherCompany = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("PAY", null, Gpl, "k")));

        Assert.Equal((200, "MYG-PAY-000001"), (v0Then.Status, v0Then.Code));
        Assert.Equal((200, "MYG-PAY-000001"), (upper.Status, upper.Code));
        Assert.Equal(first.Sequence, v0Then.Sequence);
        Assert.Equal((400, "IDEMPOTENCY_KEY_REUSED"), (otherType.Status, otherType.Reason));
        Assert.Equal((400, "IDEMPOTENCY_KEY_REUSED"), (otherScope.Status, otherScope.Reason));
        Assert.Equal((400, "IDEMPOTENCY_KEY_REUSED"), (otherCompany.Status, otherCompany.Reason));
        Assert.Equal(mdmCalls, host.Mdm.Requests.Count);
        Assert.Equal(1L, (await host.Collection("record_code_counters").Find(FilterDefinition<BsonDocument>.Empty).SingleAsync())["LastNumber"].ToInt64());
        Assert.Equal(1, await host.CountAsync("record_code_allocations"));
        Assert.Equal(1, await host.Collection("audit_outbox").CountDocumentsAsync(new BsonDocument("IdempotencyKey", new BsonRegularExpression("^record-code:allocated:"))));
    }

    [Fact]
    public async Task AC05_case1_a_replay_after_the_winner_committed_is_found_by_the_first_read_and_never_inserts()
    {
        var recorder = new RecordingAllocations();
        using var host = await HostAsync(Recording(recorder));
        await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("PAY", null, Myg, "k"));
        recorder.Calls.Clear();

        var answer = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("PAY", null, Myg, "k")));

        Assert.Equal((200, "MYG-PAY-000001"), (answer.Status, answer.Code));
        Assert.Contains(recorder.Calls, call => call.Name == "Find" && call.Found);
        Assert.DoesNotContain(recorder.Calls, call => call.Name == "Insert");
    }

    /// <summary>
    /// Case 2 — the in-transaction overlap. The winner is held before its commit (fault probe); the second request enters its
    /// body and writes the same unique key. MEASURED: the server answers that write with a write conflict (the pack's
    /// inference); the recorder then releases the winner, waits for its commit and rethrows; the body is re-run, the
    /// session re-read finds the winner and nothing is inserted on the retry.
    /// </summary>
    [Fact]
    public async Task AC05_case2_an_in_transaction_overlap_is_a_write_conflict_and_the_retry_answers_the_winner()
    {
        var recorder = new RecordingAllocations();
        var probe = new HoldFirstCommit();
        recorder.OnInsertFailure = async exception =>
        {
            recorder.FirstFailure ??= exception;
            probe.Release();
            await probe.Committed.Task.WaitAsync(TimeSpan.FromSeconds(20));
        };
        using var host = await HostAsync(Recording(recorder, probe));

        var winner = Task.Run(async () => await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("PAY", null, Myg, "race"))));
        await probe.Holding.Task.WaitAsync(TimeSpan.FromSeconds(20));
        var second = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("PAY", null, Myg, "race")));
        var first = await winner;

        Assert.True(recorder.FirstFailure is not null, "No write conflict was raised — the inference (§8.6 case 2) did not hold.");
        Assert.True(IsWriteConflict(recorder.FirstFailure!), "The overlap raised " + recorder.FirstFailure!.GetType().Name + ": " + recorder.FirstFailure.Message);
        Assert.Equal(200, first.Status);
        Assert.Equal((200, first.Code), (second.Status, second.Code));
        Assert.Equal(1, await host.CountAsync("record_code_allocations"));
        Assert.Equal(1, recorder.Calls.Count(call => call.Name == "Insert" && call.Succeeded));
        var lastAttemptCalls = recorder.Calls.SkipWhile(call => !(call.Name == "Insert" && !call.Succeeded)).Skip(1).ToArray();
        Assert.Contains(lastAttemptCalls, call => call.Name == "FindInSession" && call.Found);
        Assert.DoesNotContain(lastAttemptCalls, call => call.Name == "Insert");
    }

    [Fact]
    public async Task AC05_case3_a_late_arrival_is_answered_by_the_body_re_read_with_no_insert_and_no_E11000()
    {
        var recorder = new RecordingAllocations();
        using var host = await HostAsync(Recording(recorder));
        var hold = new TaskCompletionSource();
        var arrived = new TaskCompletionSource();
        host.Mdm.Gate = async () =>
        {
            if (arrived.TrySetResult())
            {
                await hold.Task;
            }
        };

        var late = Task.Run(async () => await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("PAY", null, Myg, "late"))));
        await arrived.Task.WaitAsync(TimeSpan.FromSeconds(20));                       // the late request passed step 9 and waits at step 11
        var winner = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("PAY", null, Myg, "late")));
        recorder.Calls.Clear();
        hold.SetResult();
        var answer = await late;

        Assert.Equal((200, winner.Code), (answer.Status, answer.Code));
        Assert.Contains(recorder.Calls, call => call.Name == "FindInSession" && call.Found);
        Assert.DoesNotContain(recorder.Calls, call => call.Name == "Insert");
        Assert.Null(recorder.FirstFailure);
        Assert.Equal(1, await host.CountAsync("record_code_allocations"));
    }

    [Fact]
    public async Task AC05_case4_when_the_re_read_misses_the_winner_the_ledger_E11000_is_translated_and_the_winner_answered()
    {
        var recorder = new RecordingAllocations { SkipNextSessionRead = false };
        using var host = await HostAsync(Recording(recorder));
        var hold = new TaskCompletionSource();
        var arrived = new TaskCompletionSource();
        host.Mdm.Gate = async () =>
        {
            if (arrived.TrySetResult())
            {
                await hold.Task;
            }
        };

        var late = Task.Run(async () => await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("PAY", null, Myg, "e11000"))));
        await arrived.Task.WaitAsync(TimeSpan.FromSeconds(20));
        var winner = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("PAY", null, Myg, "e11000")));
        recorder.SkipNextSessionRead = true;
        hold.SetResult();
        var answer = await late;

        var failure = recorder.FirstFailure;
        Assert.True(failure is RecordCodeIdempotencyConflictException,
            "Expected the translated E11000, got " + (failure?.GetType().Name ?? "nothing") + ": " + failure?.Message);
        var inner = Assert.IsType<MongoWriteException>(failure!.InnerException);
        Assert.Equal(ServerErrorCategory.DuplicateKey, inner.WriteError.Category);
        Assert.Equal((200, winner.Code), (answer.Status, answer.Code));
        Assert.Equal(1, await host.CountAsync("record_code_allocations"));
    }

    [Fact]
    public async Task AC05_ten_concurrent_requests_with_one_key_leave_one_row_and_at_most_nine_gaps()
    {
        using var host = await HostAsync();

        var answers = await Task.WhenAll(Enumerable.Range(1, 10).Select(async _ =>
            await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("PAY", null, Myg, "one-key")))));
        foreach (var unavailable in answers.Where(a => a.Status == 503))
        {
            var again = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("PAY", null, Myg, "one-key")));
            Assert.Equal(200, again.Status);
        }

        var issued = answers.Where(a => a.Status == 200).Select(a => a.Code).Distinct().ToArray();
        Assert.Single(issued);
        Assert.Equal(1, await host.CountAsync("record_code_allocations"));
        Assert.InRange((await host.Collection("record_code_counters").Find(FilterDefinition<BsonDocument>.Empty).SingleAsync())["LastNumber"].ToInt64(), 1, 10);
    }

    // ── AC-6 / AC-7 ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC06_counters_are_per_tenant_prefix_and_type_and_a_dropped_allocation_leaves_a_gap()
    {
        using var host = await HostAsync();

        var mygPay = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("PAY", null, Myg, "a")));
        var gplPay = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("PAY", null, Gpl, "b")));
        var mygApd = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("APD", null, Myg, "c")));
        var bMygPay = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(TenantB), RecordCodeHttpHost.Body("PAY", null, Myg, "d")));

        Assert.Equal(new[] { "MYG-PAY-000001", "GPL-PAY-000001", "MYG-APD-000001", "MYG-PAY-000001" },
            new[] { mygPay.Code, gplPay.Code, mygApd.Code, bMygPay.Code });
        Assert.Equal(4, await host.CountAsync("record_code_counters"));
    }

    [Fact]
    public async Task AC06_a_number_taken_by_a_failed_allocation_is_never_given_again()
    {
        var failing = new FailOnce();
        using var host = await HostAsync(new RecordCodeHostOptions { FaultProbe = failing });
        failing.Armed = true;

        var dropped = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("PAY", null, Myg, "x")));
        var next = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("PAY", null, Myg, "y")));

        Assert.Equal(503, dropped.Status);
        Assert.Equal((200, "MYG-PAY-000002", 2L), (next.Status, next.Code, next.Sequence!.Value));
        Assert.DoesNotContain(typeof(IRecordCodeCounterRepository).GetMethods(), m => m.Name != nameof(IRecordCodeCounterRepository.NextAsync));
    }

    [Fact]
    public async Task AC07_after_999999_the_counter_grows_to_seven_digits()
    {
        using var host = await HostAsync();
        await host.Database.GetCollection<RecordCodeCounter>("record_code_counters").InsertOneAsync(new RecordCodeCounter
        {
            TenantId = TenantA, Prefix = "MYG", RecordType = "PAY", LastNumber = 999998
        });

        var a = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("PAY", null, Myg, "a")));
        var b = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("PAY", null, Myg, "b")));

        Assert.Equal(("MYG-PAY-999999", 999999L), (a.Code, a.Sequence!.Value));
        Assert.Equal(("MYG-PAY-1000000", 1000000L), (b.Code, b.Sequence!.Value));
    }

    // ── AC-10 / AC-17 (sequential lock) / AC-22 (lock) ──────────────────────────────────────────────────────

    [Fact]
    public async Task AC10_a_group_code_uses_the_aligned_prefix_and_without_a_setting_is_409_with_no_counter()
    {
        using var host = await HostAsync();
        await host.SeedGroupPrefixAsync(TenantA, "GMG");

        var bank = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsMdmAsync(Bank(), RecordCodeHttpHost.Body("BNK", "Group", null, "b1")));
        var account = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsMdmAsync(Bank(), RecordCodeHttpHost.Body("BAC", "Group", null, "b2")));
        var missing = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsMdmAsync(Bank(TenantB), RecordCodeHttpHost.Body("BNK", "Group", null, "b3")));

        Assert.Equal(("GMG-BNK-000001", "GMG-BAC-000001"), (bank.Code, account.Code));
        Assert.Equal((409, "GROUP_PREFIX_MISSING"), (missing.Status, missing.Reason));
        Assert.Equal(0, await host.Collection("record_code_counters").CountDocumentsAsync(new BsonDocument("TenantId", new BsonBinaryData(TenantB, GuidRepresentation.Standard))));
    }

    /// <summary>
    /// AC-17 (r11, sequential): the first Group allocation locks and says so; the next one — after the first one's 200 —
    /// says it did not, and the lock time is the single value the first wrote. Measures the inference that an already
    /// locked row is not counted in ModifiedCount.
    /// </summary>
    [Fact]
    public async Task AC17_the_first_group_allocation_locks_and_the_next_one_sequentially_does_not()
    {
        using var host = await HostAsync();
        await host.SeedGroupPrefixAsync(TenantA, "GMG");
        Assert.Equal(BsonNull.Value, (await GroupSettingAsync(host))["LockedAtUtc"]);

        var first = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsMdmAsync(Bank(), RecordCodeHttpHost.Body("BNK", "Group", null, "g1")));
        var lockedAfterFirst = (await GroupSettingAsync(host))["LockedAtUtc"];
        var second = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsMdmAsync(Bank(), RecordCodeHttpHost.Body("BNK", "Group", null, "g2")));
        var lockedAfterSecond = (await GroupSettingAsync(host))["LockedAtUtc"];

        Assert.Equal((200, 200), (first.Status, second.Status));
        Assert.NotEqual(BsonNull.Value, lockedAfterFirst);
        Assert.Equal(lockedAfterFirst, lockedAfterSecond);
        var events = (await host.DeliveredAuditEventsAsync()).OrderBy(e => Convert.ToInt64(e.AfterState!["Sequence"])).ToArray();
        Assert.Equal(true, events[0].Metadata["GroupPrefixLockedNow"]);
        Assert.Equal(false, events[1].Metadata["GroupPrefixLockedNow"]);
    }

    [Fact]
    public async Task AC22_the_group_lock_is_written_in_the_allocations_transaction()
    {
        var failing = new FailOnce();
        using var host = await HostAsync(new RecordCodeHostOptions { FaultProbe = failing });
        await host.SeedGroupPrefixAsync(TenantA, "GMG");
        failing.Armed = true;

        var dropped = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsMdmAsync(Bank(), RecordCodeHttpHost.Body("BNK", "Group", null, "g1")));

        Assert.Equal(503, dropped.Status);
        Assert.Equal(BsonNull.Value, (await GroupSettingAsync(host))["LockedAtUtc"]);
    }

    // ── AC-46 — the lock filter does not match (two hosts on one database) ─────────────────────────────────

    [Fact]
    public async Task AC46_a_setting_re_aligned_after_step_11_by_another_process_is_409_mismatch_and_no_code()
    {
        var holding = new GroupPrefixHold();
        using var a = await HostAsync(new RecordCodeHostOptions
        {
            OrgPrefix = "GMG",
            ConfigureServices = services => services.AddScoped<IRecordCodeGroupPrefixRepository>(provider =>
                holding.Wrap(ActivatorUtilities.CreateInstance<RecordCodeGroupPrefixRepository>(provider)))
        }, "diten_record_code_two_hosts");
        using var b = new RecordCodeHttpHost(_mongo.Client, a.Database, new RecordCodeHostOptions { OrgPrefix = "GMX" });
        await a.SeedGroupPrefixAsync(TenantA, "GMG");
        var admin = RecordCodeHttpHost.Token("platform_admin", "00000000-0000-0000-0000-000000000001", Guid.NewGuid(),
            "platform.record-code-group-prefixes.update");

        var allocation = Task.Run(async () => await RecordCodeHttpHost.ReadAsync(
            await a.AllocateAsMdmAsync(Bank(), RecordCodeHttpHost.Body("BNK", "Group", null, "g1"))));
        await holding.AfterRead.Task.WaitAsync(TimeSpan.FromSeconds(20));
        var aligned = await b.SendAsync(HttpMethod.Put, $"/api/admin/tenants/{TenantA}/record-code-group-prefix", admin);
        holding.Release.SetResult();
        var answer = await allocation;

        Assert.Equal(System.Net.HttpStatusCode.OK, aligned.StatusCode);
        Assert.Equal((409, "GROUP_PREFIX_MISMATCH"), (answer.Status, answer.Reason));
        Assert.True(answer.IsEnvelope);
        Assert.Equal(0, await a.CountAsync("record_code_allocations"));
        var setting = await GroupSettingAsync(a);
        Assert.Equal(("GMX", BsonNull.Value), (setting["GroupPrefix"].AsString, setting["LockedAtUtc"]));
        Assert.Equal(1L, (await a.Collection("record_code_counters").Find(new BsonDocument("Prefix", "GMG")).SingleAsync())["LastNumber"].ToInt64());
        var refusal = Assert.Single(await a.DeliveredAuditEventsAsync(), e => e.RequestType == "AllocateRecordCodeCommand");
        Assert.Equal(("GROUP_PREFIX_MISMATCH", "GMG", 13), ((string)refusal.Metadata["ReasonCode"]!, (string)refusal.Metadata["Prefix"]!,
            Convert.ToInt32(refusal.Metadata["EvaluationStep"])));

        var aAgain = await RecordCodeHttpHost.ReadAsync(await a.AllocateAsMdmAsync(Bank(), RecordCodeHttpHost.Body("BNK", "Group", null, "g2")));
        var bAllocates = await RecordCodeHttpHost.ReadAsync(await b.AllocateAsMdmAsync(Bank(), RecordCodeHttpHost.Body("BNK", "Group", null, "g3")));
        Assert.Equal((409, "GROUP_PREFIX_MISMATCH"), (aAgain.Status, aAgain.Reason));
        Assert.Equal((200, "GMX-BNK-000001"), (bAllocates.Status, bAllocates.Code));
    }

    // ── AC-20 / AC-24 ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AC20_no_store_can_change_remove_or_reissue_a_code()
    {
        var forbidden = new[] { "Update", "Replace", "Delete", "Remove", "Reset", "Decrement", "Set" };
        Assert.DoesNotContain(typeof(IRecordCodeAllocationRepository).GetMethods(), m => forbidden.Any(f => m.Name.StartsWith(f, StringComparison.Ordinal)));
        Assert.Equal(new[] { nameof(IRecordCodeCounterRepository.NextAsync) }, typeof(IRecordCodeCounterRepository).GetMethods().Select(m => m.Name).ToArray());
        Assert.Equal(new[] { "AlignIfUnlockedAsync", "GetAsync", "LockAsync" },
            typeof(IRecordCodeGroupPrefixRepository).GetMethods().Select(m => m.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task AC24_the_three_collections_carry_their_named_tenant_first_indexes()
    {
        var database = await ReplicaSetClassFixture.FreshDatabaseAsync(_mongo, "diten_record_code_schema");
        await PlatformSchemaManifest.ApplyAsync(database, new[] { SchemaProfile.RecordCodes });

        async Task<Dictionary<string, BsonDocument>> Indexes(string name) =>
            (await (await database.GetCollection<BsonDocument>(name).Indexes.ListAsync()).ToListAsync())
            .ToDictionary(index => index["name"].AsString);

        var counters = await Indexes("record_code_counters");
        var allocations = await Indexes("record_code_allocations");
        var prefixes = await Indexes("record_code_group_prefixes");
        Assert.True(counters["ux_record_code_counters_tenant_prefix_type"]["unique"].AsBoolean);
        Assert.True(allocations["ux_record_code_allocations_tenant_caller_key"]["unique"].AsBoolean);
        Assert.True(allocations["ux_record_code_allocations_tenant_code"]["unique"].AsBoolean);
        Assert.Contains("ix_record_code_allocations_tenant_type_prefix_seq", allocations.Keys);
        Assert.Contains("ix_record_code_allocations_tenant_le_time", allocations.Keys);
        Assert.Contains("ix_record_code_allocations_tenant_prefix_le", allocations.Keys);
        Assert.True(prefixes["ux_record_code_group_prefixes_tenant"]["unique"].AsBoolean);
        Assert.All(counters.Values.Concat(allocations.Values).Concat(prefixes.Values).Where(i => i["name"] != "_id_"),
            index => Assert.Equal("TenantId", index["key"].AsBsonDocument.Names.First()));
        Assert.All(PlatformSchemaManifest.For(new[] { SchemaProfile.RecordCodes }), c => Assert.Equal(SchemaProfile.RecordCodes, c.Profile));
    }

    // ── AC-39 (c) — commit result unknown ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC39c_an_unknown_commit_result_is_renewed_on_the_same_session_and_answers_200_once()
    {
        var probe = new UnknownAfterFirstCommit();
        using var host = await HostAsync(new RecordCodeHostOptions { FaultProbe = probe });

        var answer = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("PAY", null, Myg, "u")));

        Assert.Equal((200, "MYG-PAY-000001"), (answer.Status, answer.Code));
        Assert.Equal(1, await host.CountAsync("record_code_allocations"));
        Assert.Equal(1, await host.CountAsync("audit_outbox"));
    }

    [Fact]
    public async Task AC39c_three_unknown_commits_are_503_and_the_same_key_then_is_a_first_write()
    {
        var probe = new UnknownBeforeCommit { Times = 3 };
        using var host = await HostAsync(new RecordCodeHostOptions { FaultProbe = probe });

        var lost = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("PAY", null, Myg, "u")));
        var retried = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("PAY", null, Myg, "u")));

        Assert.Equal((503, "RECORD_CODE_UNAVAILABLE"), (lost.Status, lost.Reason));
        Assert.Equal((200, "MYG-PAY-000002"), (retried.Status, retried.Code));
        Assert.Equal(1, await host.CountAsync("record_code_allocations"));
    }

    // ── AC-42 — the interim short-code usage lock ───────────────────────────────────────────────────────────

    [Fact]
    public async Task AC42_a_short_code_changed_after_use_or_moved_to_another_company_issues_nothing_until_restored()
    {
        using var host = await HostAsync();
        var x = Guid.NewGuid();
        var y = Guid.NewGuid();
        host.Mdm.Company(x, "MYQ");
        await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("PAY", null, x, "x1"));

        host.Mdm.Company(x, "MYZ");
        var changed = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("PAY", null, x, "x2")));
        host.Mdm.Company(y, "MYQ");
        var moved = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("PAY", null, y, "y1")));
        host.Mdm.Company(x, "MYQ");
        var restored = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("PAY", null, x, "x3")));

        Assert.Equal((409, "LEGAL_ENTITY_SHORT_CODE_CHANGED"), (changed.Status, changed.Reason));
        Assert.Equal((409, "LEGAL_ENTITY_SHORT_CODE_CHANGED"), (moved.Status, moved.Reason));
        Assert.Equal((200, "MYQ-PAY-000002"), (restored.Status, restored.Code));
        Assert.Equal(0, await host.Collection("record_code_counters").CountDocumentsAsync(new BsonDocument("Prefix", "MYZ")));
        var refusals = (await host.DeliveredAuditEventsAsync()).Where(e => e.Outcome != Diten.Platform.Domain.Enums.AuditOutcome.Succeeded).ToArray();
        Assert.Equal(2, refusals.Length);
        Assert.All(refusals, r => Assert.Equal(Diten.Platform.Domain.Enums.AuditOutcome.Failed, r.Outcome));
    }

    [Fact]
    public async Task AC42_group_rows_do_not_count_for_the_usage_lock()
    {
        using var host = await HostAsync();
        await host.SeedGroupPrefixAsync(TenantA, "GMG");
        var company = Guid.NewGuid();
        host.Mdm.Company(company, "GMG");
        await host.AllocateAsMdmAsync(Bank(), RecordCodeHttpHost.Body("BAC", "Group", null, "g"));

        var answer = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(), RecordCodeHttpHost.Body("PAY", null, company, "c")));

        Assert.Equal((200, "GMG-PAY-000001"), (answer.Status, answer.Code));
    }

    // ── harness ─────────────────────────────────────────────────────────────────────────────────────────────

    private static Task<BsonDocument> GroupSettingAsync(RecordCodeHttpHost host) =>
        host.Collection("record_code_group_prefixes").Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();

    private static Diten.Platform.Common.Tenancy.TenantContext Tenant(Guid tenantId)
    {
        var context = new Diten.Platform.Common.Tenancy.TenantContext();
        context.SetTenant(tenantId);
        return context;
    }

    private static bool IsWriteConflict(Exception exception) =>
        exception is MongoCommandException { Code: 112 }
        || (exception is MongoException mongo && mongo.HasErrorLabel("TransientTransactionError"))
        || (exception is MongoWriteException write && write.WriteError?.Code == 112);

    private static RecordCodeHostOptions Recording(RecordingAllocations recorder, IPlatformTransactionFaultProbe? probe = null) => new()
    {
        FaultProbe = probe,
        ConfigureServices = services => services.AddScoped<IRecordCodeAllocationRepository>(provider =>
            recorder.Wrap(ActivatorUtilities.CreateInstance<RecordCodeAllocationRepository>(provider)))
    };

    /// <summary>
    /// The production ledger store, wrapped: every call, its result and the exception it threw are recorded. It can skip ONE
    /// session re-read (case 4) and run a hook on an insert failure before rethrowing it (case 2).
    /// </summary>
    internal sealed class RecordingAllocations
    {
        public System.Collections.Concurrent.ConcurrentQueue<(string Name, bool Found, bool Succeeded)> Calls { get; private set; } = new();
        public Exception? FirstFailure { get; set; }
        public bool SkipNextSessionRead { get; set; }
        public Func<Exception, Task>? OnInsertFailure { get; set; }

        public IRecordCodeAllocationRepository Wrap(IRecordCodeAllocationRepository inner) => new Decorator(this, inner);

        private sealed class Decorator(RecordingAllocations recorder, IRecordCodeAllocationRepository inner) : IRecordCodeAllocationRepository
        {
            public async Task InsertAsync(IPlatformTransactionSession session, Guid tenantId, RecordCodeAllocation allocation, CancellationToken ct = default)
            {
                try
                {
                    await inner.InsertAsync(session, tenantId, allocation, ct);
                    recorder.Calls.Enqueue(("Insert", false, true));
                }
                catch (Exception exception)
                {
                    recorder.Calls.Enqueue(("Insert", false, false));
                    recorder.FirstFailure ??= exception;
                    if (recorder.OnInsertFailure is { } hook)
                    {
                        recorder.OnInsertFailure = null;
                        await hook(exception);
                    }

                    throw;
                }
            }

            public async Task<RecordCodeAllocation?> FindByIdempotencyKeyAsync(Guid tenantId, string callerService, string key, CancellationToken ct = default)
            {
                var found = await inner.FindByIdempotencyKeyAsync(tenantId, callerService, key, ct);
                recorder.Calls.Enqueue(("Find", found is not null, true));
                return found;
            }

            public async Task<RecordCodeAllocation?> FindByIdempotencyKeyAsync(IPlatformTransactionSession session, Guid tenantId, string callerService, string key, CancellationToken ct = default)
            {
                if (recorder.SkipNextSessionRead)
                {
                    recorder.SkipNextSessionRead = false;
                    recorder.Calls.Enqueue(("FindInSession(skipped)", false, true));
                    return null;
                }

                var found = await inner.FindByIdempotencyKeyAsync(session, tenantId, callerService, key, ct);
                recorder.Calls.Enqueue(("FindInSession", found is not null, true));
                return found;
            }

            public Task<string?> FindLatestPrefixForLegalEntityAsync(Guid tenantId, Guid legalEntityId, CancellationToken ct = default) =>
                inner.FindLatestPrefixForLegalEntityAsync(tenantId, legalEntityId, ct);

            public Task<bool> ExistsPrefixForOtherLegalEntityAsync(Guid tenantId, string prefix, Guid legalEntityId, CancellationToken ct = default) =>
                inner.ExistsPrefixForOtherLegalEntityAsync(tenantId, prefix, legalEntityId, ct);
        }
    }

    /// <summary>Holds the FIRST commit until released; tells when it is holding and when it committed.</summary>
    private sealed class HoldFirstCommit : IPlatformTransactionFaultProbe
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _first;
        public TaskCompletionSource Holding { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Committed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Guid _held;

        public void Release() => _release.TrySetResult();

        public async Task BeforeCommitAsync(IPlatformTransactionSession session, int commitAttempt, CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref _first, 1) == 0)
            {
                _held = session.TransactionId;
                Holding.TrySetResult();
                await _release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
        }

        public Task AfterCommitAsync(IPlatformTransactionSession session, int commitAttempt, CancellationToken cancellationToken)
        {
            if (session.TransactionId == _held)
            {
                Committed.TrySetResult();
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FailOnce : IPlatformTransactionFaultProbe
    {
        public bool Armed { get; set; }

        /// <summary>Fails every commit attempt of ONE transaction (the executor renews the commit up to three times).</summary>
        public Task BeforeCommitAsync(IPlatformTransactionSession session, int commitAttempt, CancellationToken cancellationToken)
        {
            if (Armed)
            {
                Armed = commitAttempt < 3;
                throw RecordCodeFaults.UnknownCommit();
            }

            return Task.CompletedTask;
        }

        public Task AfterCommitAsync(IPlatformTransactionSession session, int commitAttempt, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class UnknownAfterFirstCommit : IPlatformTransactionFaultProbe
    {
        private int _thrown;

        public Task BeforeCommitAsync(IPlatformTransactionSession session, int commitAttempt, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AfterCommitAsync(IPlatformTransactionSession session, int commitAttempt, CancellationToken cancellationToken) =>
            Interlocked.Exchange(ref _thrown, 1) == 0 ? throw RecordCodeFaults.UnknownCommit() : Task.CompletedTask;
    }

    private sealed class UnknownBeforeCommit : IPlatformTransactionFaultProbe
    {
        public int Times { get; set; }

        public Task BeforeCommitAsync(IPlatformTransactionSession session, int commitAttempt, CancellationToken cancellationToken) =>
            Interlocked.Decrement(ref _remaining) >= 0 ? throw RecordCodeFaults.UnknownCommit() : Task.CompletedTask;

        private int _remaining = 3;

        public Task AfterCommitAsync(IPlatformTransactionSession session, int commitAttempt, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>The production group-prefix store, wrapped: the sessionless read (step 11) is held until released.</summary>
    private sealed class GroupPrefixHold
    {
        public TaskCompletionSource AfterRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IRecordCodeGroupPrefixRepository Wrap(IRecordCodeGroupPrefixRepository inner) => new Decorator(this, inner);

        private sealed class Decorator(GroupPrefixHold hold, IRecordCodeGroupPrefixRepository inner) : IRecordCodeGroupPrefixRepository
        {
            public async Task<RecordCodeGroupPrefix?> GetAsync(Guid tenantId, CancellationToken ct = default)
            {
                var value = await inner.GetAsync(tenantId, ct);
                if (hold.AfterRead.TrySetResult())
                {
                    await hold.Release.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
                }

                return value;
            }

            public Task<RecordCodeGroupPrefix?> GetAsync(IPlatformTransactionSession session, Guid tenantId, CancellationToken ct = default) =>
                inner.GetAsync(session, tenantId, ct);

            public Task<RecordCodeGroupPrefixAlignment> AlignIfUnlockedAsync(IPlatformTransactionSession session, Guid tenantId, string groupPrefix,
                string alignedBy, DateTime alignedAtUtc, CancellationToken ct = default) =>
                inner.AlignIfUnlockedAsync(session, tenantId, groupPrefix, alignedBy, alignedAtUtc, ct);

            public Task<RecordCodeGroupPrefixLockResult> LockAsync(IPlatformTransactionSession session, Guid tenantId, string prefix,
                DateTime lockedAtUtc, CancellationToken ct = default) =>
                inner.LockAsync(session, tenantId, prefix, lockedAtUtc, ct);
        }
    }
}
