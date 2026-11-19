using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.RecordCodes;

/// <summary>
/// MOD-0040-FU02 §21.3 — what reaches <c>audit_events</c>, through the production door, outbox and processor on a real
/// replica set: Table A for an allocation (field by field), Table B for each refusal the pack says is recorded, nothing for
/// those it says are not, the platform-actor refusal, the absent-tenant refusal, and the outbox keys (AC-39).
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
[Trait("Category", "Mongo")]
public sealed class RecordCodeAuditDeliveryMongoTests : IClassFixture<ReplicaSetClassFixture>
{
    private static readonly Guid TenantA = Guid.Parse("40fc0002-0000-4000-8000-00000000a0a2");
    private static readonly Guid TenantB = Guid.Parse("40fc0002-0000-4000-8000-00000000b0b2");
    private static readonly Guid User = Guid.Parse("40fc0002-0000-4000-8000-0000000000c2");
    private static readonly Guid Myg = Guid.Parse("6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7");
    private const string PayCreate = "finance-treasury.payment-batches.create";

    private readonly DisposableMongoReplicaSet _mongo;

    public RecordCodeAuditDeliveryMongoTests(ReplicaSetClassFixture replicaSet) => _mongo = replicaSet.Mongo;

    private async Task<RecordCodeHttpHost> HostAsync(RecordCodeHostOptions? options = null)
    {
        var host = await RecordCodeHttpHost.StartAsync(_mongo, "diten_record_code_audit", options, TenantA, TenantB);
        host.Mdm.Company(Myg, "MYG");
        return host;
    }

    private static string Token(Guid? tenant = null, params string[] permissions) =>
        RecordCodeHttpHost.TenantUserToken(tenant ?? TenantA, User, permissions.Length == 0 ? [PayCreate] : permissions);

    // ── AC-17 / AC-18 — Table A ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC17_an_allocation_writes_one_outbox_row_keyed_by_the_allocation_and_one_Table_A_record()
    {
        using var host = await HostAsync();

        var answer = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Token(), RecordCodeHttpHost.Body("PAY", null, Myg, "client-key-1"),
            configure: request => request.Headers.TryAddWithoutValidation("X-Correlation-Id", "client-corr-1")));

        Assert.Equal(200, answer.Status);
        var ledger = await host.Collection("record_code_allocations").Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
        var allocationId = ledger["_id"].AsGuid;
        var outbox = await host.Collection(AuditCollectionNames.AuditOutbox).Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
        Assert.Equal($"record-code:allocated:{allocationId:N}", outbox["IdempotencyKey"].AsString);

        var audit = Assert.Single(await host.DeliveredAuditEventsAsync());
        Assert.Equal(AuditActorType.TenantUser, audit.ActorType);
        Assert.Equal(User, audit.ActorId);
        Assert.Equal("Diten.FinanceTreasuryService", audit.SourceService);
        Assert.Equal(TenantA, audit.TenantId);
        Assert.Equal(TenantA, audit.TargetTenantId);
        Assert.Equal(AuditCategory.PlatformConfiguration, audit.Category);
        Assert.Equal("RecordCodeAllocation", audit.EntityType);
        Assert.Equal(allocationId, audit.EntityId);
        Assert.Equal(AuditOperation.Create, audit.Operation);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal("AllocateRecordCodeCommand", audit.RequestType);
        Assert.Equal("MOD-0040-FU02", audit.SourceModule);
        Assert.Null(audit.BeforeState);
        Assert.Equal(new[] { "Code", "LegalEntityId", "Prefix", "PrefixScope", "RecordType", "Sequence" },
            audit.AfterState!.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray());
        Assert.Equal("MYG-PAY-000001", audit.AfterState["Code"]);
        Assert.Equal(1L, Convert.ToInt64(audit.AfterState["Sequence"]));
        Assert.Equal("PAY", audit.AfterState["RecordType"]);
        Assert.Equal("LegalEntity", audit.AfterState["PrefixScope"]);
        Assert.Equal("MYG", audit.AfterState["Prefix"]);
        Assert.Equal(Myg.ToString("D"), audit.AfterState["LegalEntityId"]);
        Assert.Equal("platform.record-code.allocated", audit.Metadata["EventName"]);
        Assert.Equal("client-key-1", audit.Metadata["IdempotencyKey"]);
        Assert.Equal(false, audit.Metadata["GroupPrefixLockedNow"]);
        var designed = new[] { "EventName", "IdempotencyKey", "GroupPrefixLockedNow", "ClientCorrelation" };
        Assert.All(audit.Metadata.Keys.Where(key => !designed.Contains(key)),
            key => Assert.StartsWith("AuditOutbox", key)); // the outbox mapper's own bookkeeping keys only

        // A replay writes nothing new.
        var replay = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Token(), RecordCodeHttpHost.Body("PAY", null, Myg, "client-key-1")));
        Assert.Equal((200, "MYG-PAY-000001"), (replay.Status, replay.Code));
        Assert.Equal(1, await host.CountAsync(AuditCollectionNames.AuditOutbox));
    }

    [Fact]
    public async Task AC17_an_outbox_write_lost_to_the_connection_aborts_the_allocation()
    {
        using var host = await HostAsync(new RecordCodeHostOptions
        {
            ConfigureServices = services => services.AddScoped<ITransactionalAuditOutboxStore, BrokenOutboxStore>()
        });

        var answer = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Token(), RecordCodeHttpHost.Body("PAY", null, Myg, "k-1")));

        Assert.Equal((503, "RECORD_CODE_UNAVAILABLE"), (answer.Status, answer.Reason));
        Assert.Null(answer.Code);
        Assert.Equal(0, await host.CountAsync("record_code_allocations"));
    }

    // ── AC-19 — Table B ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC19_a_not_found_company_is_one_Denied_record_with_the_body_keys_and_no_code()
    {
        using var host = await HostAsync();

        await host.AllocateAsync(Token(), RecordCodeHttpHost.Body("PAY", null, Guid.Parse("00000000-0000-4000-8000-0000000000ff"), "k-404"));

        var audit = Assert.Single(await host.DeliveredAuditEventsAsync());
        Assert.Equal(AuditOutcome.Denied, audit.Outcome);
        Assert.Equal(AuditActorType.TenantUser, audit.ActorType);
        Assert.Equal(User, audit.ActorId);
        Assert.Equal("Diten.FinanceTreasuryService", audit.SourceService);
        Assert.Equal(TenantA, audit.TenantId);
        Assert.Null(audit.EntityId);
        Assert.Null(audit.BeforeState);
        Assert.Null(audit.AfterState);
        Assert.Equal("platform.record-code.allocation-refused", audit.Metadata["EventName"]);
        Assert.Equal("LEGAL_ENTITY_NOT_FOUND", audit.Metadata["ReasonCode"]);
        Assert.Equal(11, Convert.ToInt32(audit.Metadata["EvaluationStep"]));
        Assert.Equal("k-404", audit.Metadata["IdempotencyKey"]);
        Assert.Equal("PAY", audit.Metadata["RecordType"]);
        Assert.Equal("LegalEntity", audit.Metadata["PrefixScope"]);
        Assert.True(audit.Metadata.ContainsKey("LegalEntityId"));
        Assert.False(audit.Metadata.ContainsKey("Code"));
        Assert.False(audit.Metadata.ContainsKey("Sequence"));
        Assert.False(audit.Metadata.ContainsKey("Prefix"));
        var outbox = await host.Collection(AuditCollectionNames.AuditOutbox).Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
        Assert.StartsWith("record-code:refused:", outbox["IdempotencyKey"].AsString);
    }

    [Fact]
    public async Task AC19_a_tenant_that_is_not_Active_is_Denied_at_step_4_without_any_body_key()
    {
        using var host = await HostAsync();
        var suspended = Guid.NewGuid();
        await host.SeedTenantAsync(suspended, TenantStatus.Suspended);

        await host.AllocateAsync(Token(suspended), RecordCodeHttpHost.Body("PAY", null, Myg, "k-4"));

        var audit = Assert.Single(await host.DeliveredAuditEventsAsync());
        Assert.Equal(AuditOutcome.Denied, audit.Outcome);
        Assert.Equal(suspended, audit.TenantId);
        Assert.Equal("RECORD_CODE_TENANT_NOT_ACTIVE", audit.Metadata["ReasonCode"]);
        Assert.Equal(4, Convert.ToInt32(audit.Metadata["EvaluationStep"]));
        foreach (var key in new[] { "IdempotencyKey", "RecordType", "PrefixScope", "LegalEntityId", "Prefix" })
        {
            Assert.False(audit.Metadata.ContainsKey(key), key);
        }
    }

    [Fact]
    public async Task AC37_an_absent_tenants_refusal_is_filed_under_the_platform_audit_tenant_and_names_the_token_tenant()
    {
        using var host = await HostAsync();
        var absent = Guid.NewGuid();

        var answer = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Token(absent), RecordCodeHttpHost.Body("PAY", null, Myg, "k-4")));

        Assert.Equal((403, "RECORD_CODE_TENANT_NOT_ACTIVE"), (answer.Status, answer.Reason));
        var audit = Assert.Single(await host.DeliveredAuditEventsAsync());
        Assert.Equal(AuditTenantIds.PlatformSystemTenantId, audit.TenantId);
        Assert.Equal(absent, audit.TargetTenantId);
        Assert.Equal(AuditOutcome.Denied, audit.Outcome);
    }

    public static TheoryData<string, string, AuditOutcome, int> RecordedRefusals => new()
    {
        { "unknown-type", "RECORD_TYPE_UNKNOWN", AuditOutcome.Failed, 6 },
        { "type-not-allowed", "RECORD_CODE_FORBIDDEN", AuditOutcome.Denied, 7 },
        { "permission", "RECORD_CODE_PERMISSION_MISSING", AuditOutcome.Denied, 8 },
        { "scope", "RECORD_CODE_SCOPE_INVALID", AuditOutcome.Failed, 10 },
        { "short-code", "LEGAL_ENTITY_SHORT_CODE_MISSING", AuditOutcome.Failed, 11 },
        { "mdm-403", "LEGAL_ENTITY_READ_PERMISSION_MISSING", AuditOutcome.Denied, 11 },
        { "mdm-down", "LEGAL_ENTITY_SOURCE_UNAVAILABLE", AuditOutcome.Failed, 11 },
        { "not-eligible", "LEGAL_ENTITY_NOT_ELIGIBLE", AuditOutcome.Failed, 11 },
        { "group-missing", "GROUP_PREFIX_MISSING", AuditOutcome.Failed, 11 },
        { "body", "RECORD_CODE_REQUEST_INVALID", AuditOutcome.Failed, 5 }
    };

    [Theory]
    [MemberData(nameof(RecordedRefusals))]
    public async Task AC19_each_recorded_refusal_writes_one_row_with_its_outcome_and_step(
        string refusal, string reason, AuditOutcome outcome, int step)
    {
        using var host = await HostAsync();
        var company = Guid.NewGuid();
        HttpResponseMessage response;
        switch (refusal)
        {
            case "unknown-type":
                response = await host.AllocateAsync(Token(), RecordCodeHttpHost.Body("CTR", null, Myg, "k")); break;
            case "type-not-allowed":
                response = await host.AllocateAsync(Token(), RecordCodeHttpHost.Body("BNK", "Group", null, "k")); break;
            case "permission":
                response = await host.AllocateAsync(Token(TenantA, "finance-treasury.payment-batches.read"), RecordCodeHttpHost.Body("PAY", null, Myg, "k")); break;
            case "scope":
                response = await host.AllocateAsync(Token(), RecordCodeHttpHost.Body("PAY", "Group", null, "k")); break;
            case "short-code":
                host.Mdm.Company(company, "GMG-CH");
                response = await host.AllocateAsync(Token(), RecordCodeHttpHost.Body("PAY", null, company, "k")); break;
            case "mdm-403":
                host.Mdm.Answer(company, 403);
                response = await host.AllocateAsync(Token(), RecordCodeHttpHost.Body("PAY", null, company, "k")); break;
            case "mdm-down":
                host.Mdm.Answer(company, 500);
                response = await host.AllocateAsync(Token(), RecordCodeHttpHost.Body("PAY", null, company, "k")); break;
            case "not-eligible":
                host.Mdm.Company(company, "MYX", "\"REJECTED\"");
                response = await host.AllocateAsync(Token(), RecordCodeHttpHost.Body("PAY", null, company, "k")); break;
            case "group-missing":
                response = await host.AsMdmGroupAsync(TenantA); break;
            default:
                response = await host.AllocateAsync(Token(), "{\"recordType\":\"PAY\"}"); break;
        }

        var answer = await RecordCodeHttpHost.ReadAsync(response);
        Assert.Equal(reason, answer.Reason);
        var audit = Assert.Single(await host.DeliveredAuditEventsAsync());
        Assert.Equal(outcome, audit.Outcome);
        Assert.Equal(reason, audit.Metadata["ReasonCode"]);
        Assert.Equal(step, Convert.ToInt32(audit.Metadata["EvaluationStep"]));
        Assert.Equal(step >= 6, audit.Metadata.ContainsKey("RecordType"));
        Assert.Equal(0, await host.CountAsync("record_code_allocations"));
    }

    [Fact]
    public async Task AC19_an_idempotency_key_reused_with_another_body_is_one_Failed_record_at_step_9()
    {
        using var host = await HostAsync();
        await host.AllocateAsync(Token(), RecordCodeHttpHost.Body("PAY", null, Myg, "k-9"));

        var answer = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Token(TenantA, PayCreate, "finance-treasury.ap-invoices.create"),
            RecordCodeHttpHost.Body("APD", null, Myg, "k-9")));

        Assert.Equal((400, "IDEMPOTENCY_KEY_REUSED"), (answer.Status, answer.Reason));
        var refused = Assert.Single(await host.DeliveredAuditEventsAsync(), audit => audit.Outcome != AuditOutcome.Succeeded);
        Assert.Equal(AuditOutcome.Failed, refused.Outcome);
        Assert.Equal(9, Convert.ToInt32(refused.Metadata["EvaluationStep"]));
    }

    [Fact]
    public async Task AC19_refusals_before_a_named_tenant_and_person_are_not_written()
    {
        using var host = await HostAsync();

        await host.AllocateAsync(null, RecordCodeHttpHost.Body("PAY", null, Myg, "k"));                                 // 401 token
        await host.AllocateAsync(Token(), RecordCodeHttpHost.Body("PAY", null, Myg, "k"), secret: "wrong");              // 401 credential
        await host.AllocateAsync(Token(), RecordCodeHttpHost.Body("PAY", null, Myg, "k"),
            configure: request => request.Headers.TryAddWithoutValidation("X-Tenant-Id", TenantA.ToString()));         // 403 X-Tenant-Id
        await host.AllocateAsync(RecordCodeHttpHost.Token("tenant_user", null, User), RecordCodeHttpHost.Body("PAY", null, Myg, "k")); // 403 no tenant
        await host.AllocateAsync(Token(), new string('x', 5000));                                                     // 413

        Assert.Equal(0, await host.CountAsync(AuditCollectionNames.AuditOutbox));
    }

    // ── AC-29 ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC29_a_refusal_is_written_apart_from_the_business_data_and_answers_with_no_data()
    {
        using var host = await HostAsync();

        var answer = await RecordCodeHttpHost.ReadAsync(await host.AsMdmGroupAsync(TenantA));

        Assert.Equal(409, answer.Status);
        Assert.Contains("\"data\":null", answer.Raw);
        Assert.Equal(0, await host.CountAsync("record_code_allocations"));
        Assert.Equal(0, await host.CountAsync("record_code_counters"));
        Assert.Equal(0, await host.CountAsync("record_code_group_prefixes"));
        Assert.Equal(1, await host.CountAsync(AuditCollectionNames.AuditOutbox));
    }

    // ── AC-39 (a), (b), (d) ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC39a_two_tenants_with_the_same_client_key_both_get_codes_and_two_outbox_rows()
    {
        using var host = await HostAsync();

        var a = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Token(TenantA), RecordCodeHttpHost.Body("PAY", null, Myg, "same-key")));
        var b = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Token(TenantB), RecordCodeHttpHost.Body("PAY", null, Myg, "same-key")));

        Assert.Equal((200, 200), (a.Status, b.Status));
        Assert.Equal(2, await host.CountAsync(AuditCollectionNames.AuditOutbox));
    }

    [Fact]
    public async Task AC39b_a_key_first_refused_then_successful_writes_both_records()
    {
        using var host = await HostAsync();
        var company = Guid.NewGuid();
        host.Mdm.Company(company, "GMG-CH");

        var refused = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Token(), RecordCodeHttpHost.Body("PAY", null, company, "retry-key")));
        host.Mdm.Company(company, "GMH");
        var allocated = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Token(), RecordCodeHttpHost.Body("PAY", null, company, "retry-key")));

        Assert.Equal(409, refused.Status);
        Assert.Equal((200, "GMH-PAY-000001"), (allocated.Status, allocated.Code));
        var events = await host.DeliveredAuditEventsAsync();
        Assert.Equal(new[] { AuditOutcome.Failed, AuditOutcome.Succeeded }, events.Select(e => e.Outcome).OrderBy(o => o == AuditOutcome.Succeeded).ToArray());
    }

    [Fact]
    public async Task AC39d_an_outbox_that_answers_false_inside_the_transaction_is_503_and_issues_nothing()
    {
        using var host = await HostAsync(new RecordCodeHostOptions
        {
            ConfigureServices = services => services.AddScoped<ITransactionalAuditOutboxStore, FalseOutboxStore>()
        });

        var answer = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Token(), RecordCodeHttpHost.Body("PAY", null, Myg, "k-1")));

        Assert.Equal((503, "RECORD_CODE_UNAVAILABLE"), (answer.Status, answer.Reason));
        Assert.Null(answer.Code);
        Assert.Equal(0, await host.CountAsync("record_code_allocations"));
    }

    // ── AC-44 — the platform-actor refusal (below the limit) ────────────────────────────────────────────────

    public static TheoryData<string, string?, bool> PlatformActors => new()
    {
        { "platform_admin", "40fc0002-0000-4000-8000-00000000a0a2", true },
        { "partner_admin", "40fc0002-0000-4000-8000-00000000a0a2", true },
        { "platform_admin", null, false },
        { "platform_admin", "00000000-0000-0000-0000-000000000000", false },
        { "partner_admin", "not-a-guid", false }
    };

    [Theory]
    [MemberData(nameof(PlatformActors))]
    public async Task AC44_a_platform_actor_on_the_internal_endpoint_is_403_and_one_Denied_platform_record(
        string actorType, string? tenantClaim, bool namesATarget)
    {
        using var host = await HostAsync();

        var answer = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(
            RecordCodeHttpHost.Token(actorType, tenantClaim, User, PayCreate), RecordCodeHttpHost.Body("PAY", null, Myg, "k")));

        Assert.Equal((403, "RECORD_CODE_FORBIDDEN"), (answer.Status, answer.Reason));
        var audit = Assert.Single(await host.DeliveredAuditEventsAsync());
        Assert.Equal(AuditOutcome.Denied, audit.Outcome);
        Assert.Equal(AuditTenantIds.PlatformSystemTenantId, audit.TenantId);
        Assert.Equal(namesATarget ? Guid.Parse(tenantClaim!) : null, audit.TargetTenantId);
        Assert.Equal(actorType == "platform_admin" ? AuditActorType.PlatformAdministrator : AuditActorType.PartnerAdministrator, audit.ActorType);
        Assert.Equal("RECORD_CODE_FORBIDDEN", audit.Metadata["ReasonCode"]);
        Assert.Equal(2, Convert.ToInt32(audit.Metadata["EvaluationStep"]));
        Assert.False(audit.Metadata.ContainsKey("RecordType"));
        Assert.Equal(0, await host.CountAsync("record_code_allocations"));
    }

    private sealed class BrokenOutboxStore : ITransactionalAuditOutboxStore
    {
        public Task<bool> TryInsertAsync(IPlatformTransactionSession session, AuditOutboxWriteRequest request, CancellationToken ct = default) =>
            throw new MongoException("outbox connection lost (test)");
    }

    private sealed class FalseOutboxStore : ITransactionalAuditOutboxStore
    {
        public Task<bool> TryInsertAsync(IPlatformTransactionSession session, AuditOutboxWriteRequest request, CancellationToken ct = default) =>
            Task.FromResult(false);
    }
}
