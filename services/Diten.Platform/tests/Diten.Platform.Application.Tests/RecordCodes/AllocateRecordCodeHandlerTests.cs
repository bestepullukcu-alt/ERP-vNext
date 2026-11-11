using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.RecordCodes;
using Diten.Platform.Application.Features.DocumentManagementIdentifiers;
using Diten.Platform.Application.Features.RecordCodes;
using Diten.Platform.Application.Features.RecordCodes.Commands;
using Diten.Platform.Application.Features.RecordCodes.Handlers.CommandHandlers;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.RecordCodes;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Enums.RecordCodes;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.RecordCodes;

/// <summary>
/// MOD-0040-FU02 §8.5 — the handler's decisions with fake stores and a fake MDM reader: the order of steps 4–13, the
/// field-by-field replay equality (AC-5), scope (AC-8), the group prefix's one source (AC-10, AC-35), the reader's outcomes
/// (AC-13), the interim usage lock (AC-42) and what each refusal record carries (AC-19, Table B).
/// </summary>
public sealed class AllocateRecordCodeHandlerTests
{
    private static readonly Guid Tenant = Guid.Parse("40fc0002-0000-4000-8000-00000000a0a9");
    private static readonly Guid Company = Guid.Parse("6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7");
    private const string Finance = "Diten.FinanceTreasuryService";

    private readonly Mock<ITenantRegistryRepository> _tenants = new();
    private readonly Mock<IRecordCodeCounterRepository> _counters = new();
    private readonly Mock<IRecordCodeAllocationRepository> _allocations = new();
    private readonly Mock<IRecordCodeGroupPrefixRepository> _groupPrefixes = new();
    private readonly Mock<ILegalEntityShortCodeReader> _shortCodes = new();
    private readonly Mock<ITransactionalAuditOutboxWriter> _door = new();
    private readonly RecordingRefusalWriter _refusals = new();
    private string _orgPrefix = "GMG";

    public AllocateRecordCodeHandlerTests()
    {
        _tenants.Setup(x => x.GetByIdAsync(Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tenant { Id = Tenant, Code = "T", Slug = "t", Name = "T", DisplayName = "T", Domain = "t", Status = TenantStatus.Active });
        _shortCodes.Setup(x => x.ReadAsync(Tenant, Company, It.IsAny<CancellationToken>())).ReturnsAsync(LegalEntityShortCodeResult.Found("MYG"));
        _counters.Setup(x => x.NextAsync(Tenant, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(7);
        _groupPrefixes.Setup(x => x.LockAsync(It.IsAny<IPlatformTransactionSession>(), Tenant, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecordCodeGroupPrefixLockResult(true, true));
        _door.Setup(x => x.TryEnqueueAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<AuditOutboxWriteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    // ── order ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Step4_an_inactive_tenant_is_refused_before_the_body_and_its_record_has_no_body_keys()
    {
        _tenants.Setup(x => x.GetByIdAsync(Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tenant { Id = Tenant, Code = "T", Slug = "t", Name = "T", DisplayName = "T", Domain = "t", Status = TenantStatus.Suspended });

        var answer = await Handle(Command(request: null));

        Assert.Equal((403, "RECORD_CODE_TENANT_NOT_ACTIVE"), (answer.StatusCode, answer.ReasonCode));
        var (request, outcome) = Assert.Single(_refusals.Written);
        Assert.Equal(AuditOutcome.Denied, outcome);
        Assert.Equal(4, request.Intent!.Metadata!["EvaluationStep"]);
        Assert.Equal(new[] { "EvaluationStep", "EventName", "ReasonCode" }, request.Intent.Metadata!.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal(Tenant, request.TenantId);
    }

    [Fact]
    public async Task Step4_an_absent_tenant_is_filed_under_the_platform_audit_tenant_naming_the_token_tenant()
    {
        _tenants.Setup(x => x.GetByIdAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync((Tenant?)null);

        await Handle(Command());

        var (request, _) = Assert.Single(_refusals.Written);
        Assert.Equal(Diten.Platform.Domain.Entities.Audit.AuditTenantIds.PlatformSystemTenantId, request.TenantId);
        Assert.Equal(Tenant, request.Intent!.TargetTenantId);
    }

    [Fact]
    public async Task Steps_6_to_8_refuse_in_order_and_before_idempotency()
    {
        Assert.Equal("RECORD_TYPE_UNKNOWN", (await Handle(Command(Body("XYZ")))).ReasonCode);
        Assert.Equal("RECORD_CODE_FORBIDDEN", (await Handle(Command(Body("BNK", RecordCodePrefixScope.Group, null)))).ReasonCode);
        Assert.Equal("RECORD_CODE_PERMISSION_MISSING", (await Handle(Command(Body("PAY"), permission: false))).ReasonCode);
        Assert.Equal("RECORD_CODE_FORBIDDEN", (await Handle(Command(Body("PLN", RecordCodePrefixScope.Platform, null), types: ["PAY"]))).ReasonCode);
        _allocations.Verify(x => x.FindByIdempotencyKeyAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── AC-5 — replay equality field by field, default applied ─────────────────────────────────────────────

    public static TheoryData<string, RecordCodePrefixScope, bool, bool> Replays => new()
    {
        { "PAY", RecordCodePrefixScope.LegalEntity, true, true },     // same
        { "APD", RecordCodePrefixScope.LegalEntity, true, false },    // record type
        { "PAY", RecordCodePrefixScope.Group, true, false },          // scope (the same legal entity)
        { "PAY", RecordCodePrefixScope.LegalEntity, false, false }    // legal entity
    };

    [Theory]
    [MemberData(nameof(Replays))]
    public async Task AC05_a_replay_is_the_same_body_only_when_all_three_fields_are_equal(
        string recordType, RecordCodePrefixScope scope, bool sameCompany, bool same)
    {
        _allocations.Setup(x => x.FindByIdempotencyKeyAsync(Tenant, Finance, "k", It.IsAny<CancellationToken>())).ReturnsAsync(Stored());

        var answer = await Handle(Command(new AllocateRecordCodeRequest(recordType, scope, sameCompany ? Company : Guid.NewGuid(), "k"),
            types: ["PAY", "APD"]));

        if (same)
        {
            Assert.Equal((200, "MYG-PAY-000003"), (answer.StatusCode, answer.Data!.Code));
            _counters.VerifyNoOtherCalls();
            _shortCodes.Verify(x => x.ReadAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.Empty(_refusals.Written);
        }
        else
        {
            Assert.Equal((400, "IDEMPOTENCY_KEY_REUSED"), (answer.StatusCode, answer.ReasonCode));
            Assert.Equal(9, Assert.Single(_refusals.Written).Request.Intent!.Metadata!["EvaluationStep"]);
        }
    }

    // ── AC-8 / AC-10 / AC-35 ────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("PAY", RecordCodePrefixScope.Platform, false)]
    [InlineData("PAY", RecordCodePrefixScope.Group, false)]
    [InlineData("PAY", RecordCodePrefixScope.LegalEntity, false)]
    [InlineData("BNK", RecordCodePrefixScope.Group, true)]
    [InlineData("BNK", RecordCodePrefixScope.LegalEntity, true)]
    public async Task AC08_an_incompatible_scope_is_422_before_MDM_and_the_counter(string type, RecordCodePrefixScope scope, bool withCompany)
    {
        var answer = await Handle(Command(new AllocateRecordCodeRequest(type, scope, withCompany ? Company : null, "k"), types: [type]));

        Assert.Equal((422, "RECORD_CODE_SCOPE_INVALID"), (answer.StatusCode, answer.ReasonCode));
        Assert.Equal(AuditOutcome.Failed, Assert.Single(_refusals.Written).Outcome);
        _shortCodes.VerifyNoOtherCalls();
        _counters.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AC10_without_a_group_setting_the_answer_is_409_and_no_default_is_used()
    {
        var answer = await Handle(Command(Body("BNK", RecordCodePrefixScope.Group, null), types: ["BNK"]));

        Assert.Equal((409, "GROUP_PREFIX_MISSING"), (answer.StatusCode, answer.ReasonCode));
        _counters.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AC35_a_setting_that_differs_from_the_effective_OrgPrefix_is_409_mismatch_with_the_prefix_recorded()
    {
        _groupPrefixes.Setup(x => x.GetAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(Setting("GMG"));
        _orgPrefix = "GMX";

        var answer = await Handle(Command(Body("BNK", RecordCodePrefixScope.Group, null), types: ["BNK"]));

        Assert.Equal((409, "GROUP_PREFIX_MISMATCH"), (answer.StatusCode, answer.ReasonCode));
        Assert.Equal("GMX", Assert.Single(_refusals.Written).Request.Intent!.Metadata!["Prefix"]);
        _counters.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AC35_a_setting_equal_to_the_effective_OrgPrefix_allocates_and_locks()
    {
        _groupPrefixes.Setup(x => x.GetAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(Setting("GMG"));

        var answer = await Handle(Command(Body("BNK", RecordCodePrefixScope.Group, null), types: ["BNK"]));

        Assert.Equal((200, "GMG-BNK-000007"), (answer.StatusCode, answer.Data!.Code));
        _groupPrefixes.Verify(x => x.LockAsync(It.IsAny<IPlatformTransactionSession>(), Tenant, "GMG", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── AC-13 — the reader's outcomes ───────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(LegalEntityShortCodeOutcome.NotFound, 404, "LEGAL_ENTITY_NOT_FOUND", AuditOutcome.Denied)]
    [InlineData(LegalEntityShortCodeOutcome.ReadPermissionMissing, 403, "LEGAL_ENTITY_READ_PERMISSION_MISSING", AuditOutcome.Denied)]
    [InlineData(LegalEntityShortCodeOutcome.NotEligible, 422, "LEGAL_ENTITY_NOT_ELIGIBLE", AuditOutcome.Failed)]
    [InlineData(LegalEntityShortCodeOutcome.ShortCodeInvalid, 409, "LEGAL_ENTITY_SHORT_CODE_MISSING", AuditOutcome.Failed)]
    [InlineData(LegalEntityShortCodeOutcome.Unavailable, 503, "LEGAL_ENTITY_SOURCE_UNAVAILABLE", AuditOutcome.Failed)]
    public async Task AC13_each_reader_outcome_has_its_answer_and_record_and_never_moves_the_counter(
        LegalEntityShortCodeOutcome outcome, int status, string reason, AuditOutcome recorded)
    {
        _shortCodes.Setup(x => x.ReadAsync(Tenant, Company, It.IsAny<CancellationToken>())).ReturnsAsync(LegalEntityShortCodeResult.Of(outcome, "test"));

        var answer = await Handle(Command());

        Assert.Equal((status, reason), (answer.StatusCode, answer.ReasonCode));
        Assert.Null(answer.Data);
        Assert.Equal(recorded, Assert.Single(_refusals.Written).Outcome);
        _counters.VerifyNoOtherCalls();
    }

    // ── AC-42 ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC42_a_short_code_changed_after_use_is_409_with_the_new_code_recorded()
    {
        _allocations.Setup(x => x.FindLatestPrefixForLegalEntityAsync(Tenant, Company, It.IsAny<CancellationToken>())).ReturnsAsync("MYQ");

        var answer = await Handle(Command());

        Assert.Equal((409, "LEGAL_ENTITY_SHORT_CODE_CHANGED"), (answer.StatusCode, answer.ReasonCode));
        Assert.Equal("MYG", Assert.Single(_refusals.Written).Request.Intent!.Metadata!["Prefix"]);
        _counters.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AC42_a_short_code_already_used_by_another_company_is_409()
    {
        _allocations.Setup(x => x.ExistsPrefixForOtherLegalEntityAsync(Tenant, "MYG", Company, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var answer = await Handle(Command());

        Assert.Equal((409, "LEGAL_ENTITY_SHORT_CODE_CHANGED"), (answer.StatusCode, answer.ReasonCode));
        _counters.VerifyNoOtherCalls();
    }

    // ── AC-19 — a refusal record never carries a code ───────────────────────────────────────────────────────

    [Fact]
    public async Task AC19_a_refusal_record_after_step_5_carries_the_body_keys_and_never_a_code_or_sequence()
    {
        _shortCodes.Setup(x => x.ReadAsync(Tenant, Company, It.IsAny<CancellationToken>()))
            .ReturnsAsync(LegalEntityShortCodeResult.Of(LegalEntityShortCodeOutcome.NotFound, "x"));

        await Handle(Command());

        var metadata = Assert.Single(_refusals.Written).Request.Intent!.Metadata!;
        Assert.Equal(new[] { "EvaluationStep", "EventName", "IdempotencyKey", "LegalEntityId", "PrefixScope", "ReasonCode", "RecordType" },
            metadata.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Null(Assert.Single(_refusals.Written).Request.EntityId);
        Assert.StartsWith("record-code:refused:", Assert.Single(_refusals.Written).Request.IdempotencyKey);
    }

    // ── harness ─────────────────────────────────────────────────────────────────────────────────────────────

    private Task<Diten.Platform.Application.Common.Response<RecordCodeAllocationResult>> Handle(AllocateRecordCodeCommand command) =>
        new AllocateRecordCodeHandler(new InlineTransactionExecutor(), _door.Object, _refusals, _tenants.Object, _counters.Object,
                _allocations.Object, _groupPrefixes.Object, _shortCodes.Object,
                Microsoft.Extensions.Options.Options.Create(new DocumentCodingOptions { OrgPrefix = _orgPrefix }),
                NullLogger<AllocateRecordCodeHandler>.Instance)
            .Handle(command, default);

    private static AllocateRecordCodeRequest Body(string type, RecordCodePrefixScope scope = RecordCodePrefixScope.LegalEntity, Guid? company = null) =>
        new(type, scope, scope == RecordCodePrefixScope.LegalEntity ? company ?? Company : company, "k");

    private static AllocateRecordCodeCommand Command(
        AllocateRecordCodeRequest? request = null, bool permission = true, string[]? types = null, bool parsed = true) =>
        new(Tenant, Guid.NewGuid(), Finance, (types ?? ["PAY", "APD", "ARI", "CPS", "CTP"]).ToHashSet(StringComparer.Ordinal),
            parsed ? request ?? Body("PAY") : null, ["bad body"], permission, "corr");

    private static RecordCodeAllocation Stored() => new()
    {
        TenantId = Tenant, CallerService = Finance, IdempotencyKey = "k", RecordType = "PAY", PrefixScope = RecordCodePrefixScope.LegalEntity,
        LegalEntityId = Company, Prefix = "MYG", Sequence = 3, Code = "MYG-PAY-000003", OnBehalfOfUserId = Guid.NewGuid(),
        CorrelationId = "c", AllocatedAtUtc = DateTime.UtcNow
    };

    private static RecordCodeGroupPrefix Setting(string prefix) => new()
    {
        TenantId = Tenant, GroupPrefix = prefix, AlignedAtUtc = DateTime.UtcNow, AlignedBy = "admin"
    };
}
