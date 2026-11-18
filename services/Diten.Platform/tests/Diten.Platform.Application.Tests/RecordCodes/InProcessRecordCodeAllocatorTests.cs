using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.RecordCodes;
using Diten.Platform.Application.Features.RecordCodes;
using Diten.Platform.Application.Features.RecordCodes.Commands;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Observability;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Enums.RecordCodes;
using MediatR;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.RecordCodes;

/// <summary>
/// MOD-0040-FU02 DL-2 AC-57 / AC-58 — the in-process port in isolation: the shipped <see cref="InProcessRecordCodeAllocator"/>
/// over a fake <see cref="ISender"/> and fake tenant / authorization / permission contexts. What it may send, and what it
/// refuses before anything is sent. The allocation itself (steps 4–13, the refusal rows) is measured over real HTTP in
/// <c>OrganizationRecordCodeHttpTests</c>.
/// </summary>
public sealed class InProcessRecordCodeAllocatorTests
{
    private static readonly Guid Tenant = Guid.Parse("40fc0002-0000-4000-8000-00000000a0d1");
    private static readonly Guid OtherTenant = Guid.Parse("40fc0002-0000-4000-8000-00000000b0d1");
    private static readonly Guid User = Guid.Parse("40fc0002-0000-4000-8000-0000000000d1");
    private static readonly Guid Company = Guid.Parse("6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7");
    private static readonly Guid Record = Guid.Parse("7a000000-0000-4000-8000-0000000000d1");

    private readonly List<AllocateRecordCodeCommand> _sent = [];
    private Response<RecordCodeAllocationResult> _answer = Response<RecordCodeAllocationResult>.Success(
        new RecordCodeAllocationResult("MYG-ORG-000001", 1), 200);

    // ── AC-57 ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC57_a_contradicting_tenant_context_never_reaches_the_allocator()
    {
        var result = await Allocator(tenant: Tenant, tokenTenant: OtherTenant).AllocateAsync(Org(), default);

        Assert.Equal(new InProcessRecordCodeResult(false, null, null, 403, RecordCodeReasonCodes.Forbidden), result);
        Assert.Empty(_sent);
    }

    [Fact]
    public async Task AC57_the_command_carries_the_token_tenant_and_user_and_the_platform_as_caller()
    {
        var result = await Allocator().AllocateAsync(Org(), default);

        var command = Assert.Single(_sent);
        Assert.Equal((Tenant, User, InProcessRecordCodeAllocator.CallerService), (command.TenantId, command.OnBehalfOfUserId, command.CallerService));
        Assert.Equal(["ORG"], command.AllowedRecordTypes);
        Assert.Equal(new AllocateRecordCodeRequest("ORG", RecordCodePrefixScope.LegalEntity, Company, $"organization-unit:{Record:N}"),
            command.Request);
        Assert.Empty(command.RequestErrors);
        Assert.True(command.HasRequiredPermission);
        Assert.Equal("corr-d1", command.CorrelationId);
        Assert.Equal(new InProcessRecordCodeResult(true, "MYG-ORG-000001", 1, 200, null), result);
    }

    [Theory]
    [InlineData("ORG", 1, "organization-unit:{0}")]
    [InlineData("ORG", 2, "organization-unit:{0}:2")]
    [InlineData("POS", 1, "position:{0}")]
    [InlineData("POS", 3, "position:{0}:3")]
    public async Task AC55_the_key_is_the_record_id_and_a_collision_attempt_appends_its_number(string type, int attempt, string key)
    {
        await Allocator().AllocateAsync(new InProcessRecordCodeRequest(type, Company, Record, attempt), default);

        Assert.Equal(string.Format(key, Record.ToString("N")), Assert.Single(_sent).Request!.IdempotencyKey);
    }

    [Fact]
    public async Task F14_without_a_correlation_context_a_fresh_correlation_is_made()
    {
        await new InProcessRecordCodeAllocator(Sender(), TenantContext(Tenant), new FakeAuthorization(Tenant, User, "tenant_user"),
            new FakePermissions(_ => true)).AllocateAsync(Org(), default);

        Assert.Matches("^[0-9a-f]{32}$", Assert.Single(_sent).CorrelationId);
    }

    [Theory]
    [InlineData("PAY")]
    [InlineData("PLN")]
    [InlineData("XYZ")]
    public async Task AC48_a_type_whose_catalogue_caller_is_not_the_platform_is_sent_with_no_allowed_type(string type)
    {
        await Allocator().AllocateAsync(new InProcessRecordCodeRequest(type, Company, Record), default);

        Assert.Empty(Assert.Single(_sent).AllowedRecordTypes);
    }

    [Fact]
    public async Task AC54_a_refused_allocation_is_answered_with_its_own_status_and_reason()
    {
        _answer = Response<RecordCodeAllocationResult>.Fail("short code changed", 409, RecordCodeReasonCodes.LegalEntityShortCodeChanged);

        var result = await Allocator().AllocateAsync(Org(), default);

        Assert.Equal(new InProcessRecordCodeResult(false, null, null, 409, RecordCodeReasonCodes.LegalEntityShortCodeChanged), result);
    }

    // ── AC-58 ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC58_a_user_without_the_catalogue_permission_is_sent_as_not_holding_it()
    {
        var asked = new List<string?>();
        await Allocator(permissions: new FakePermissions(key => { asked.Add(key); return false; })).AllocateAsync(Org(), default);

        Assert.False(Assert.Single(_sent).HasRequiredPermission);
        Assert.Equal(["platform.organization-units.create"], asked);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void AC58_a_blank_catalogue_permission_is_never_held(string? key) =>
        Assert.False(InProcessRecordCodeAllocator.HoldsRequiredPermission(key, new FakePermissions(_ => true)));

    [Fact]
    public void AC58_a_named_catalogue_permission_is_asked_of_the_permission_context() =>
        Assert.True(InProcessRecordCodeAllocator.HoldsRequiredPermission("platform.positions.create",
            new FakePermissions(key => key == "platform.positions.create")));

    [Theory]
    [InlineData("tenant_user", true)]
    [InlineData("Tenant_User", true)]
    [InlineData("TENANT_USER", true)]
    [InlineData("platform_admin", false)]
    [InlineData("partner_admin", false)]
    [InlineData("tenant_user ", false)]
    [InlineData(null, false)]
    public async Task AC58_actor_type_is_compared_like_the_middleware(string? actorType, bool reaches)
    {
        var result = await Allocator(actorType: actorType).AllocateAsync(Org(), default);

        Assert.Equal(reaches ? 1 : 0, _sent.Count);
        if (!reaches)
        {
            Assert.Equal((403, RecordCodeReasonCodes.Forbidden), (result.StatusCode, result.ReasonCode));
        }
    }

    [Fact]
    public async Task AC58_an_empty_subject_never_reaches_the_allocator()
    {
        var result = await Allocator(user: Guid.Empty).AllocateAsync(Org(), default);

        Assert.Equal((403, RecordCodeReasonCodes.Forbidden), (result.StatusCode, result.ReasonCode));
        Assert.Empty(_sent);
    }

    // ── harness ─────────────────────────────────────────────────────────────────────────────────────────────

    private static InProcessRecordCodeRequest Org() => new("ORG", Company, Record);

    private InProcessRecordCodeAllocator Allocator(
        Guid? tenant = null,
        Guid? tokenTenant = null,
        Guid? user = null,
        string? actorType = "tenant_user",
        IActorPermissionContext? permissions = null)
    {
        var correlation = new Mock<ICorrelationContext>();
        correlation.SetupGet(c => c.CorrelationId).Returns("corr-d1");
        return new InProcessRecordCodeAllocator(
            Sender(),
            TenantContext(tenant ?? Tenant),
            new FakeAuthorization(tokenTenant ?? Tenant, user ?? User, actorType),
            permissions ?? new FakePermissions(_ => true),
            correlation.Object);
    }

    private ISender Sender()
    {
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.IsAny<IRequest<Response<RecordCodeAllocationResult>>>(), It.IsAny<CancellationToken>()))
            .Callback((IRequest<Response<RecordCodeAllocationResult>> command, CancellationToken _) => _sent.Add((AllocateRecordCodeCommand)command))
            .ReturnsAsync(() => _answer);
        return sender.Object;
    }

    private static TenantContext TenantContext(Guid tenant)
    {
        var context = new TenantContext();
        context.SetTenant(tenant);
        return context;
    }

    private sealed class FakePermissions(Func<string?, bool> has) : IActorPermissionContext
    {
        public bool IsPlatformActor => false;

        public bool Has(string? permissionKey) => has(permissionKey);
    }

    private sealed class FakeAuthorization(Guid tenant, Guid user, string? actorType) : ITenantAuthorizationContext
    {
        public Guid TenantId => tenant;
        public Guid UserId => user;
        public string? ActorType => actorType;
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
}

/// <summary>
/// A stand-in port for the MOD-0288 handler unit tests (<c>TenantOrganization/**</c>): it counts per record type and answers
/// <c>{Prefix}-{TYPE}-{n:000000}</c>, or <see cref="Refusal"/> when set, and keeps every request it received.
/// </summary>
internal sealed class FakeInProcessRecordCodeAllocator : IInProcessRecordCodeAllocator
{
    private readonly Dictionary<string, long> _counters = new(StringComparer.Ordinal);

    public string Prefix { get; init; } = "MYG";

    public InProcessRecordCodeResult? Refusal { get; init; }

    public List<InProcessRecordCodeRequest> Requests { get; } = [];

    public Task<InProcessRecordCodeResult> AllocateAsync(InProcessRecordCodeRequest request, CancellationToken ct)
    {
        Requests.Add(request);
        if (Refusal is { } refusal)
        {
            return Task.FromResult(refusal);
        }

        var next = _counters[request.RecordType] = _counters.GetValueOrDefault(request.RecordType) + 1;
        return Task.FromResult(new InProcessRecordCodeResult(true, $"{Prefix}-{request.RecordType}-{next:000000}", next, 200, null));
    }
}
