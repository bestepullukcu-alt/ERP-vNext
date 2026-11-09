using System.Diagnostics;
using System.Net;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Application.Features.RecordCodes.Commands;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.RecordCodes;

/// <summary>
/// MOD-0040-FU02 — <c>POST /api/internal/record-codes/allocate</c> over real HTTP (<see cref="RecordCodeHttpHost"/>: the
/// shipped controller, executor and handler, the production pipeline, repositories and audit door on a real replica set,
/// the production MDM reader over a fake MDM). Each test names its AC; every refusal asserts the envelope, the reason code
/// and that no code, counter or ledger row was produced.
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
[Trait("Category", "Mongo")]
public sealed class InternalRecordCodesHttpTests : IClassFixture<ReplicaSetClassFixture>
{
    private static readonly Guid TenantA = Guid.Parse("40fc0002-0000-4000-8000-00000000a0a1");
    private static readonly Guid TenantB = Guid.Parse("40fc0002-0000-4000-8000-00000000b0b1");
    private static readonly Guid User = Guid.Parse("40fc0002-0000-4000-8000-0000000000c1");
    private static readonly Guid Myg = Guid.Parse("6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7");
    private const string PayCreate = "finance-treasury.payment-batches.create";

    private readonly DisposableMongoReplicaSet _mongo;

    public InternalRecordCodesHttpTests(ReplicaSetClassFixture replicaSet) => _mongo = replicaSet.Mongo;

    private static string PayToken(Guid? tenant = null) => RecordCodeHttpHost.TenantUserToken(tenant ?? TenantA, User, PayCreate, "mdm.legal-entities.read");

    private async Task<RecordCodeHttpHost> HostAsync(RecordCodeHostOptions? options = null)
    {
        var host = await RecordCodeHttpHost.StartAsync(_mongo, "diten_record_code_http", options, TenantA, TenantB);
        host.Mdm.Company(Myg, "MYG");
        return host;
    }

    private static async Task AssertNothingIssuedAsync(RecordCodeHttpHost host)
    {
        Assert.Equal(0, await host.CountAsync("record_code_counters"));
        Assert.Equal(0, await host.CountAsync("record_code_allocations"));
    }

    // ── AC-1 ────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC01_without_the_credential_headers_the_answer_is_401_and_nothing_is_issued()
    {
        using var host = await HostAsync();

        var answer = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(PayToken(), RecordCodeHttpHost.Body("PAY", null, Myg, "k-1"),
            configure: request => request.Headers.Remove(Diten.Platform.API.Security.TrustedRecordCodeRequestExecutor.CredentialIdHeader)));

        Assert.Equal((401, "RECORD_CODE_UNAUTHENTICATED"), (answer.Status, answer.Reason));
        Assert.True(answer.IsEnvelope);
        await AssertNothingIssuedAsync(host);
        Assert.Empty(host.Mdm.Requests);
    }

    // ── AC-2 ────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC02_a_tenant_in_the_body_is_400()
    {
        using var host = await HostAsync();

        var answer = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(PayToken(),
            "{\"recordType\":\"PAY\",\"legalEntityId\":\"" + Myg + "\",\"idempotencyKey\":\"k-1\",\"tenantId\":\"" + TenantB + "\"}"));

        Assert.Equal((400, "RECORD_CODE_REQUEST_INVALID"), (answer.Status, answer.Reason));
        await AssertNothingIssuedAsync(host);
    }

    [Fact]
    public async Task AC02_the_tenant_is_the_tokens_a_query_string_tenant_is_ignored_and_MDM_hears_the_token_tenant()
    {
        using var host = await HostAsync();
        var token = PayToken();

        var answer = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(token, RecordCodeHttpHost.Body("PAY", null, Myg, "k-1"),
            path: RecordCodeHttpHost.AllocatePath + "?tenantId=" + TenantB + "&tenant_id=" + TenantB));

        Assert.Equal(200, answer.Status);
        var row = Assert.Single(await host.Collection("record_code_allocations").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync());
        Assert.Equal(TenantA, row["TenantId"].AsGuid);
        var mdm = Assert.Single(host.Mdm.Requests);
        Assert.Equal(TenantA.ToString("D"), mdm.Tenant);
        Assert.Equal("Bearer " + token, mdm.Authorization);
    }

    // ── AC-3 ────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC03_a_company_code_is_issued_in_order_and_a_v0_body_gives_the_same_result()
    {
        using var host = await HostAsync();

        var first = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(PayToken(), RecordCodeHttpHost.Body("PAY", "LegalEntity", Myg, "k-1")));
        var second = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(PayToken(), RecordCodeHttpHost.Body("PAY", null, Myg, "k-2")));

        Assert.Equal((200, "MYG-PAY-000001", 1L), (first.Status, first.Code, first.Sequence!.Value));
        Assert.Equal((200, "MYG-PAY-000002", 2L), (second.Status, second.Code, second.Sequence!.Value));
    }

    // ── AC-8 ────────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("BNK", "Group", true, 422, "RECORD_CODE_SCOPE_INVALID")]          // Group with a legal entity
    [InlineData("PAY", "LegalEntity", false, 422, "RECORD_CODE_SCOPE_INVALID")]   // LegalEntity without one
    [InlineData("PAY", "Platform", false, 422, "RECORD_CODE_SCOPE_INVALID")]      // Platform past the type check (K-11)
    [InlineData("PLN", "Platform", false, 403, "RECORD_CODE_FORBIDDEN")]          // the type check comes first
    [InlineData("PAY", "Tenant", false, 400, "RECORD_CODE_REQUEST_INVALID")]      // undefined scope
    public async Task AC08_an_incompatible_scope_is_refused_before_MDM_and_the_counter(
        string recordType, string scope, bool withLegalEntity, int status, string reason)
    {
        using var host = await HostAsync();
        var token = RecordCodeHttpHost.TenantUserToken(TenantA, User, PayCreate, "mdm.banks.create");
        var body = RecordCodeHttpHost.Body(recordType, scope, withLegalEntity ? Myg : null, "k-1");

        var answer = await RecordCodeHttpHost.ReadAsync(recordType == "BNK"
            ? await host.AllocateAsMdmAsync(token, body)
            : await host.AllocateAsync(token, body));

        Assert.Equal((status, reason), (answer.Status, answer.Reason));
        Assert.Empty(host.Mdm.Requests);
        await AssertNothingIssuedAsync(host);
    }

    // ── AC-12 ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC12_another_tenants_company_is_404_with_one_body_and_no_row_in_either_tenant()
    {
        using var host = await HostAsync();
        var foreign = Guid.NewGuid();
        var inactive = Guid.NewGuid();

        var a = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(PayToken(), RecordCodeHttpHost.Body("PAY", null, foreign, "k-1")));
        var b = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(PayToken(), RecordCodeHttpHost.Body("PAY", null, inactive, "k-2")));

        Assert.Equal((404, "LEGAL_ENTITY_NOT_FOUND"), (a.Status, a.Reason));
        Assert.Equal((b.Status, b.Reason), (a.Status, a.Reason));
        Assert.All(host.Mdm.Requests, request => Assert.Equal(TenantA.ToString("D"), request.Tenant));
        await AssertNothingIssuedAsync(host);
    }

    // ── AC-13 ───────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(500)]
    [InlineData(503)]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(0)]   // connection refused
    [InlineData(-1)]  // a 200 for another legal entity
    public async Task AC13_an_unreachable_or_contradictory_MDM_is_503_and_the_counter_does_not_move(int mdmStatus)
    {
        using var host = await HostAsync();
        var company = Guid.NewGuid();
        switch (mdmStatus)
        {
            case 0: host.Mdm.Throw(company); break;
            case -1:
                host.Mdm.Answer(company, 200, "{\"data\":{\"legalEntityId\":\"" + Guid.NewGuid() + "\",\"code\":\"MYG\",\"lifecycleState\":\"ACTIVE\",\"referenceable\":true}}");
                break;
            default: host.Mdm.Answer(company, mdmStatus); break;
        }

        var answer = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(PayToken(), RecordCodeHttpHost.Body("PAY", null, company, "k-1")));

        Assert.Equal((503, "LEGAL_ENTITY_SOURCE_UNAVAILABLE"), (answer.Status, answer.Reason));
        Assert.Null(answer.Code);
        await AssertNothingIssuedAsync(host);
    }

    [Fact]
    public async Task AC13_an_MDM_403_is_a_permanent_403_LEGAL_ENTITY_READ_PERMISSION_MISSING()
    {
        using var host = await HostAsync();
        var company = Guid.NewGuid();
        host.Mdm.Answer(company, 403);

        var answer = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(PayToken(), RecordCodeHttpHost.Body("PAY", null, company, "k-1")));

        Assert.Equal((403, "LEGAL_ENTITY_READ_PERMISSION_MISSING"), (answer.Status, answer.Reason));
        await AssertNothingIssuedAsync(host);
    }

    // ── AC-23 ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC23_the_contract_examples_answer_as_the_contract_says()
    {
        using var host = await HostAsync();
        var bank = RecordCodeHttpHost.TenantUserToken(TenantA, User, "mdm.banks.create");
        await host.SeedGroupPrefixAsync(TenantA, "GMG");

        var company = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(PayToken(),
            "{ \"recordType\": \"PAY\", \"prefixScope\": \"LegalEntity\", \"legalEntityId\": \"6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7\", \"idempotencyKey\": \"payment-batch:create:9b2f0c4e-1d7a-4a55-9a3e-0c6b8f2d7e11\" }"));
        var v0 = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(PayToken(),
            "{ \"recordType\": \"PAY\", \"legalEntityId\": \"6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7\", \"idempotencyKey\": \"payment-batch:create:9b2f0c4e-1d7a-4a55-9a3e-0c6b8f2d7e11\" }"));
        var group = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsMdmAsync(bank,
            "{ \"recordType\": \"BNK\", \"prefixScope\": \"Group\", \"legalEntityId\": null, \"idempotencyKey\": \"bank:create:2a6d1f3c-7b8e-4c90-8d12-5e4f3a2b1c0d\" }"));
        var bnkCompany = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsMdmAsync(bank,
            "{ \"recordType\": \"BNK\", \"prefixScope\": \"LegalEntity\", \"legalEntityId\": \"6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7\", \"idempotencyKey\": \"k-1\" }"));
        var plan = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(PayToken(),
            "{ \"recordType\": \"PLN\", \"prefixScope\": \"Platform\", \"legalEntityId\": null, \"idempotencyKey\": \"k-2\" }"));
        var platform = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(PayToken(),
            "{ \"recordType\": \"PAY\", \"prefixScope\": \"Platform\", \"legalEntityId\": null, \"idempotencyKey\": \"k-3\" }"));
        var unknownMember = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(PayToken(),
            "{ \"recordType\": \"PAY\", \"legalEntityId\": \"6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7\", \"idempotencyKey\": \"k-9\", \"code\": \"X\" }"));

        Assert.Equal((200, "MYG-PAY-000001"), (company.Status, company.Code));
        Assert.Equal(new[] { "code", "sequence" }, company.DataMembers);
        Assert.Equal((200, "MYG-PAY-000001"), (v0.Status, v0.Code));
        Assert.Equal((200, "GMG-BNK-000001"), (group.Status, group.Code));
        Assert.Equal((422, "RECORD_CODE_SCOPE_INVALID"), (bnkCompany.Status, bnkCompany.Reason));
        Assert.Equal((403, "RECORD_CODE_FORBIDDEN"), (plan.Status, plan.Reason));
        Assert.Equal((422, "RECORD_CODE_SCOPE_INVALID"), (platform.Status, platform.Reason));
        Assert.Equal((400, "RECORD_CODE_REQUEST_INVALID"), (unknownMember.Status, unknownMember.Reason));
        Assert.Contains("\"statusCode\":422", bnkCompany.Raw);
        Assert.Contains("\"data\":null", bnkCompany.Raw);
    }

    // ── AC-26 ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC26_an_MDM_that_does_not_answer_in_3_s_is_503_inside_the_budget()
    {
        using var host = await HostAsync();
        host.Mdm.Delay = TimeSpan.FromSeconds(10);
        var clock = Stopwatch.StartNew();

        var answer = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(PayToken(), RecordCodeHttpHost.Body("PAY", null, Myg, "k-1")));

        clock.Stop();
        Assert.Equal((503, "LEGAL_ENTITY_SOURCE_UNAVAILABLE"), (answer.Status, answer.Reason));
        Assert.InRange(clock.Elapsed, TimeSpan.FromSeconds(2.5), TimeSpan.FromSeconds(5));
        await AssertNothingIssuedAsync(host);
    }

    // ── AC-28 ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC28_a_source_service_in_the_body_is_400_and_one_in_a_header_is_never_read()
    {
        using var host = await HostAsync();

        var body = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(PayToken(),
            "{\"recordType\":\"PAY\",\"legalEntityId\":\"" + Myg + "\",\"idempotencyKey\":\"k-1\",\"sourceService\":\"Diten.MdmService\"}"));
        var header = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(PayToken(), RecordCodeHttpHost.Body("PAY", null, Myg, "k-2"),
            configure: request => request.Headers.TryAddWithoutValidation("X-Source-Service", "Diten.MdmService")));

        Assert.Equal((400, "RECORD_CODE_REQUEST_INVALID"), (body.Status, body.Reason));
        Assert.Equal(200, header.Status);
        Assert.All(await host.DeliveredAuditEventsAsync(), audit => Assert.Equal("Diten.FinanceTreasuryService", audit.SourceService));
    }

    // ── AC-32 ───────────────────────────────────────────────────────────────────────────────────────────────

    public static TheoryData<string> TokensWithoutTheExactPermission => new()
    {
        "absent", "upper-case", "space-joined", "permissions-claim"
    };

    [Theory]
    [MemberData(nameof(TokensWithoutTheExactPermission))]
    public async Task AC32_only_the_exact_permission_claim_opens_the_type(string variant)
    {
        using var host = await HostAsync();
        var token = variant switch
        {
            "absent" => RecordCodeHttpHost.TenantUserToken(TenantA, User, "finance-treasury.payment-batches.read"),
            "upper-case" => RecordCodeHttpHost.TenantUserToken(TenantA, User, "FINANCE-TREASURY.PAYMENT-BATCHES.CREATE"),
            "space-joined" => RecordCodeHttpHost.TenantUserToken(TenantA, User,
                "finance-treasury.payment-batches.read finance-treasury.payment-batches.create"),
            _ => RecordCodeHttpHost.TenantUserTokenWithClaims(TenantA, User, ("permissions", PayCreate))
        };

        var first = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(token, RecordCodeHttpHost.Body("PAY", null, Myg, "k-1")));
        var again = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(token, RecordCodeHttpHost.Body("PAY", null, Myg, "k-1")));

        Assert.Equal((403, "RECORD_CODE_PERMISSION_MISSING"), (first.Status, first.Reason));
        Assert.Equal((403, "RECORD_CODE_PERMISSION_MISSING"), (again.Status, again.Reason));
        await AssertNothingIssuedAsync(host);
    }

    [Theory]
    [InlineData("PAY", "finance-treasury.payment-batches.create")]
    [InlineData("APD", "finance-treasury.ap-invoices.create")]
    [InlineData("ARI", "finance-treasury.receivable-invoices.create")]
    [InlineData("CPS", "finance-treasury.cash-positions.create")]
    [InlineData("CTP", "finance-treasury.cash-position-threshold-policies.create")]
    public async Task AC32_each_type_asks_its_own_permission(string recordType, string permission)
    {
        using var host = await HostAsync();
        var other = recordType == "PAY" ? "finance-treasury.ap-invoices.create" : PayCreate;

        var refused = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(
            RecordCodeHttpHost.TenantUserToken(TenantA, User, other), RecordCodeHttpHost.Body(recordType, null, Myg, "k-1")));
        var allowed = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(
            RecordCodeHttpHost.TenantUserToken(TenantA, User, permission), RecordCodeHttpHost.Body(recordType, null, Myg, "k-2")));

        Assert.Equal((403, "RECORD_CODE_PERMISSION_MISSING"), (refused.Status, refused.Reason));
        Assert.Equal((200, $"MYG-{recordType}-000001"), (allowed.Status, allowed.Code));
    }

    // ── AC-37 / AC-38 ───────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(TenantStatus.Suspended)]
    [InlineData(TenantStatus.Provisioning)]
    [InlineData(TenantStatus.Deactivated)]
    public async Task AC37_a_tenant_that_is_not_Active_gets_no_code_not_even_on_a_replay(TenantStatus status)
    {
        using var host = await HostAsync();
        var tenant = Guid.NewGuid();
        await host.SeedTenantAsync(tenant, status);
        var token = RecordCodeHttpHost.TenantUserToken(tenant, User, PayCreate);

        var first = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(token, RecordCodeHttpHost.Body("PAY", null, Myg, "k-1")));
        var again = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(token, RecordCodeHttpHost.Body("PAY", null, Myg, "k-1")));

        Assert.Equal((403, "RECORD_CODE_TENANT_NOT_ACTIVE"), (first.Status, first.Reason));
        Assert.Equal((403, "RECORD_CODE_TENANT_NOT_ACTIVE"), (again.Status, again.Reason));
        await AssertNothingIssuedAsync(host);
    }

    [Fact]
    public async Task AC38_the_platform_system_tenant_is_an_ordinary_tenant_and_gets_a_code()
    {
        var system = Guid.Parse("00000000-0000-0000-0000-000000000001");
        using var host = await RecordCodeHttpHost.StartAsync(_mongo, "diten_record_code_http", null, system);
        host.Mdm.Company(Myg, "MYG");

        var answer = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(
            RecordCodeHttpHost.TenantUserToken(system, User, PayCreate), RecordCodeHttpHost.Body("PAY", null, Myg, "k-1")));

        Assert.Equal((200, "MYG-PAY-000001"), (answer.Status, answer.Code));
        Assert.Equal(system, (await host.Collection("record_code_counters").Find(FilterDefinition<BsonDocument>.Empty).SingleAsync())["TenantId"].AsGuid);
        Assert.Equal(system, (await host.Collection("record_code_allocations").Find(FilterDefinition<BsonDocument>.Empty).SingleAsync())["TenantId"].AsGuid);
        Assert.Equal(system, Assert.Single(await host.DeliveredAuditEventsAsync()).TenantId);
    }

    // ── AC-45 ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AC45_no_validator_exists_for_either_record_code_command()
    {
        var validators = typeof(AllocateRecordCodeCommand).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .SelectMany(type => type.GetInterfaces())
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>))
            .Select(i => i.GetGenericArguments()[0])
            .ToArray();

        Assert.DoesNotContain(typeof(AllocateRecordCodeCommand), validators);
        Assert.DoesNotContain(typeof(AlignRecordCodeGroupPrefixCommand), validators);
    }

    public static TheoryData<string, string> MalformedBodies => new()
    {
        { "{\"recordType\":\"PAY\",\"legalEntityId\":\"6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7\",\"idempotencyKey\":\"k\",\"extra\":1}", "application/json" },
        { "{\"recordType\":\"pay\",\"legalEntityId\":\"6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7\",\"idempotencyKey\":\"k\"}", "application/json" },
        { "{\"recordType\":\"PAY\",\"legalEntityId\":\"not-a-guid\",\"idempotencyKey\":\"k\"}", "application/json" },
        { "{\"recordType\":\"PAY\",\"legalEntityId\":\"6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7\",\"idempotencyKey\":\"k\"}", "text/plain" },
        { "{\"recordType\":\"PAY\",\"recordType\":\"PAY\",\"legalEntityId\":\"6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7\",\"idempotencyKey\":\"k\"}", "application/json" }
    };

    [Theory]
    [MemberData(nameof(MalformedBodies))]
    public async Task AC45_a_malformed_body_is_a_400_envelope_and_one_Failed_record_at_step_5(string json, string contentType)
    {
        using var host = await HostAsync();

        var answer = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(PayToken(), json,
            configure: request => request.Content!.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType)));

        Assert.Equal((400, "RECORD_CODE_REQUEST_INVALID"), (answer.Status, answer.Reason));
        Assert.True(answer.IsEnvelope);
        Assert.Contains("\"data\":null", answer.Raw);
        var audit = Assert.Single(await host.DeliveredAuditEventsAsync());
        Assert.Equal(AuditOutcome.Failed, audit.Outcome);
        Assert.Equal(5, Convert.ToInt32(audit.Metadata["EvaluationStep"]));
        Assert.False(audit.Metadata.ContainsKey("RecordType"));
        await AssertNothingIssuedAsync(host);
    }

    // ── AC-47 (internal endpoint) ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC47a_an_unavailable_transaction_is_a_503_envelope_and_no_code()
    {
        using var host = await HostAsync(new RecordCodeHostOptions { FaultProbe = new AlwaysUnknownCommit() });

        var answer = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(PayToken(), RecordCodeHttpHost.Body("PAY", null, Myg, "k-1")));

        Assert.Equal((503, "RECORD_CODE_UNAVAILABLE"), (answer.Status, answer.Reason));
        Assert.True(answer.IsEnvelope);
        Assert.Equal(0, await host.CountAsync("record_code_allocations"));
    }

    [Fact]
    public async Task AC47b_a_door_that_cannot_name_the_actor_is_a_503_ProblemDetails_and_nothing_is_issued()
    {
        // No authentication middleware and the executor's own user assignment cannot run for a principal-less request:
        // the door is reached with a principal whose actor type is unknown — simulated by an actor type the door does not know.
        using var host = await HostAsync(new RecordCodeHostOptions
        {
            ConfigureServices = services => services.AddScoped<Diten.Platform.Common.Authorization.ITenantAuthorizationContext, AnonymousAuthorizationContext>()
        });

        var answer = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(PayToken(), RecordCodeHttpHost.Body("PAY", null, Myg, "k-1")));

        Assert.Equal((503, "AUDIT_RECORD_UNAVAILABLE"), (answer.Status, answer.Reason));
        Assert.False(answer.IsEnvelope);
        Assert.Equal(0, await host.CountAsync("record_code_allocations"));
    }

    [Fact]
    public async Task AC47c_an_invalid_intent_is_a_500_ProblemDetails_without_its_inner_sentence()
    {
        using var host = await HostAsync(new RecordCodeHostOptions
        {
            ConfigureServices = services =>
            {
                services.AddScoped<Diten.Platform.Application.Features.Audit.CanonicalTransactionalAuditOutboxWriter>();
                services.AddScoped<Diten.Platform.Application.Contracts.Audit.ITransactionalAuditOutboxWriter, BentSourceServiceDoor>();
            }
        });

        var answer = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(PayToken(), RecordCodeHttpHost.Body("PAY", null, Myg, "k-1")));

        Assert.Equal((500, "AUDIT_INTENT_INVALID"), (answer.Status, answer.Reason));
        Assert.False(answer.IsEnvelope);
        Assert.DoesNotContain("closed set", answer.Raw);
        Assert.Equal(0, await host.CountAsync("record_code_allocations"));
    }

    [Fact]
    public async Task AC47e_a_tenant_predicate_violation_is_a_500_Server_Error_without_its_inner_sentence()
    {
        using var host = await HostAsync(new RecordCodeHostOptions
        {
            ConfigureServices = services =>
            {
                services.AddScoped<Diten.Platform.Infrastructure.Persistence.Repositories.RecordCodeCounterRepository>();
                services.AddScoped<IRecordCodeCounterRepository, EmptyTenantCounters>();
            }
        });

        var response = await host.AllocateAsync(PayToken(), RecordCodeHttpHost.Body("PAY", null, Myg, "k-1"));
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("empty tenant", raw);
        Assert.Equal(0, await host.CountAsync("record_code_allocations"));
    }

    [Fact]
    public async Task AC47d_a_refusal_whose_record_cannot_be_written_keeps_its_own_answer()
    {
        using var host = await HostAsync(new RecordCodeHostOptions
        {
            ConfigureServices = services => services.AddScoped<Diten.Platform.Application.Contracts.Audit.ITransactionalAuditRefusalWriter, ThrowingRefusalWriter>()
        });

        var answer = await RecordCodeHttpHost.ReadAsync(await host.AsMdmGroupAsync(TenantA));

        Assert.Equal((409, "GROUP_PREFIX_MISSING"), (answer.Status, answer.Reason));
        Assert.True(answer.IsEnvelope);
    }

    private sealed class AlwaysUnknownCommit : IPlatformTransactionFaultProbe
    {
        public Task BeforeCommitAsync(IPlatformTransactionSession session, int commitAttempt, CancellationToken cancellationToken) =>
            throw RecordCodeFaults.UnknownCommit();

        public Task AfterCommitAsync(IPlatformTransactionSession session, int commitAttempt, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}

/// <summary>A principal the door cannot name: authenticated, but with no recognised actor type.</summary>
internal sealed class AnonymousAuthorizationContext : Diten.Platform.Common.Authorization.ITenantAuthorizationContext
{
    public Guid TenantId => Guid.Empty;
    public Guid UserId => Guid.Empty;
    public string? ActorType => "unknown_actor";
    public bool IsAuthenticated => true;
    public bool IsPlatformAdmin => false;
    public IReadOnlyList<string> PermissionKeys => [];
    public IReadOnlyList<Guid> RoleIds => [];
    public IReadOnlyList<string> RoleNames => [];
    public IReadOnlyList<Guid> OrgUnitIds => [];
    public IReadOnlyList<Guid> PositionIds => [];
    public Guid? LegalEntityId => null;
    public string? Country => null;
    public IReadOnlyList<Guid> ManagerChain => [];
    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>The production door, handed an intent whose source service was bent outside the closed set.</summary>
internal sealed class BentSourceServiceDoor(Diten.Platform.Application.Features.Audit.CanonicalTransactionalAuditOutboxWriter door)
    : Diten.Platform.Application.Contracts.Audit.ITransactionalAuditOutboxWriter
{
    public Task<bool> TryEnqueueAsync(IPlatformTransactionSession session,
        Diten.Platform.Application.Contracts.Audit.AuditOutboxWriteRequest request, CancellationToken ct = default) =>
        door.TryEnqueueAsync(session, new Diten.Platform.Application.Contracts.Audit.AuditOutboxWriteRequest
        {
            TenantId = request.TenantId, CorrelationId = request.CorrelationId, IdempotencyKey = request.IdempotencyKey,
            RequestType = request.RequestType, Operation = request.Operation, EntityType = request.EntityType,
            EntityId = request.EntityId, Intent = request.Intent! with { SourceService = "Diten.Unknown" }
        }, ct);
}

/// <summary>The production counter store, called for an empty tenant — the predicate violation a coding error would cause.</summary>
internal sealed class EmptyTenantCounters(Diten.Platform.Infrastructure.Persistence.Repositories.RecordCodeCounterRepository inner)
    : IRecordCodeCounterRepository
{
    public Task<long> NextAsync(Guid tenantId, string prefix, string recordType, CancellationToken cancellationToken = default) =>
        inner.NextAsync(Guid.Empty, prefix, recordType, cancellationToken);
}

/// <summary>A refusal writer whose transaction is unavailable.</summary>
internal sealed class ThrowingRefusalWriter : Diten.Platform.Application.Contracts.Audit.ITransactionalAuditRefusalWriter
{
    public Task<bool> TryEnqueueRefusalAsync(IPlatformTransactionSession session,
        Diten.Platform.Application.Contracts.Audit.AuditOutboxWriteRequest request, AuditOutcome outcome, CancellationToken ct = default) =>
        throw new PlatformTransactionUnavailableException("refusal store down (test)");
}

internal static class RecordCodeFaults
{
    /// <summary>A MongoDB error labelled UnknownTransactionCommitResult, as the driver raises it.</summary>
    public static MongoException UnknownCommit()
    {
        var exception = new MongoException("commit result unknown (test)");
        exception.AddErrorLabel("UnknownTransactionCommitResult");
        return exception;
    }

    /// <summary>The E11000 a lost upsert race raises when the server does not retry the upsert itself.</summary>
    public static MongoCommandException DuplicateKey() => new(
        new MongoDB.Driver.Core.Connections.ConnectionId(new MongoDB.Driver.Core.Servers.ServerId(
            new MongoDB.Driver.Core.Clusters.ClusterId(), new System.Net.DnsEndPoint("record-code-test", 1))),
        "E11000 duplicate key error (test)",
        new BsonDocument(),
        new BsonDocument { { "ok", 0 }, { "code", 11000 }, { "errmsg", "E11000 duplicate key error (test)" } });
}

internal static class RecordCodeHostExtensions
{
    public static Task<HttpResponseMessage> AsMdmGroupAsync(this RecordCodeHttpHost host, Guid tenant, string key = "k-g") =>
        host.AllocateAsMdmAsync(
            RecordCodeHttpHost.TenantUserToken(tenant, Guid.NewGuid(), "mdm.banks.create"),
            RecordCodeHttpHost.Body("BNK", "Group", null, key));
}
