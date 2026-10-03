using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Services.Audit;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

/// <summary>
/// WP-PLATFORM-AUDIT-INTX-01 — the in-transaction audit door, measured on the production writer
/// (<see cref="CanonicalTransactionalAuditOutboxWriter"/>) and the production mapper
/// (<see cref="AuditOutboxPayloadMapper"/>).
///
/// <para><b>The contract.</b> Whatever the writer hands to the store, the mapper can turn into an
/// <c>audit_events</c> row. The list of fields the mapper REQUIRES is not restated here: it is discovered from the
/// mapper itself (drop a key, see whether <c>Map</c> refuses), so a field the mapper starts to require tomorrow fails
/// this test until the one builder supplies it.</para>
///
/// <para><b>The actor.</b> A person is named or the write is refused; a job is named or the write is refused.</para>
/// </summary>
public sealed class TransactionOwnedAuditDoorTests
{
    private static readonly Guid Tenant = Guid.Parse("7a000000-0000-4000-8000-000000000001");
    private static readonly Guid Administrator = Guid.Parse("7a000000-0000-4000-8000-0000000000ad");

    // ── the contract: every row the door writes is deliverable ──────────────────────────────────────────

    [Theory]
    [InlineData("platform_admin", AuditActorType.PlatformAdministrator)]
    [InlineData("partner_admin", AuditActorType.PartnerAdministrator)]
    [InlineData("tenant_user", AuditActorType.TenantUser)]
    public async Task A_persons_change_becomes_a_row_the_mapper_delivers_with_that_person_named(string actorTypeClaim, AuditActorType expected)
    {
        var store = new CapturingStore();
        var writer = Writer(store, Person(actorTypeClaim, Administrator));

        Assert.True(await writer.TryEnqueueAsync(Session(), Request(Intent()), CancellationToken.None));

        var audit = Deliver(store.Single);
        Assert.Equal(expected, audit.ActorType);
        Assert.Equal(Administrator, audit.ActorId);
        Assert.Equal("p***@di10.test", audit.ActorEmailMasked);
        Assert.Equal("P***n", audit.ActorDisplayNameMasked);
        Assert.Equal(Tenant, audit.TenantId);
        Assert.Equal(Tenant, audit.TargetTenantId);
        Assert.Equal(AuditCategory.SubscriptionBilling, audit.Category);
        Assert.Equal("TenantModuleEntitlement", audit.EntityType);
        Assert.Equal(AuditOperation.Deactivate, audit.Operation);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal("Diten.Platform", audit.SourceService);
        Assert.Equal("subscription-billing", audit.SourceModule);
        Assert.Equal(true, audit.BeforeState!["IsEnabled"]);
        Assert.Equal(false, audit.AfterState!["IsEnabled"]);
        Assert.Equal("CRM", audit.Metadata["ModuleCode"]);
        Assert.False(audit.Metadata.ContainsKey(CanonicalTransactionalAuditOutboxWriter.SystemActorMetadataKey));
    }

    [Fact]
    public async Task A_named_jobs_change_becomes_a_row_the_mapper_delivers_as_the_system_actor_with_the_jobs_name()
    {
        var store = new CapturingStore();
        var writer = Writer(store, Nobody());

        await writer.TryEnqueueAsync(Session(), Request(Intent() with { SystemActor = " subscription-plan-startup-seed " }), CancellationToken.None);

        var audit = Deliver(store.Single);
        Assert.Equal(AuditActorType.System, audit.ActorType);
        Assert.Null(audit.ActorId);
        Assert.Null(audit.ActorEmailMasked);
        Assert.Null(audit.ActorDisplayNameMasked);
        Assert.Equal("subscription-plan-startup-seed", audit.Metadata[CanonicalTransactionalAuditOutboxWriter.SystemActorMetadataKey]);
    }

    /// <summary>
    /// A signed-in person is recorded as that person even when the intent also names a job: the job name only
    /// stands in when there is nobody to name.
    /// </summary>
    [Fact]
    public async Task A_person_is_never_recorded_as_the_job_the_intent_names()
    {
        var store = new CapturingStore();

        await Writer(store, Person("platform_admin", Administrator))
            .TryEnqueueAsync(Session(), Request(Intent() with { SystemActor = "some-job" }), CancellationToken.None);

        var audit = Deliver(store.Single);
        Assert.Equal(AuditActorType.PlatformAdministrator, audit.ActorType);
        Assert.Equal(Administrator, audit.ActorId);
        Assert.False(audit.Metadata.ContainsKey(CanonicalTransactionalAuditOutboxWriter.SystemActorMetadataKey));
    }

    [Fact]
    public async Task Every_field_the_mapper_requires_is_in_the_payload_the_door_writes()
    {
        var store = new CapturingStore();
        await Writer(store, Person("platform_admin", Administrator)).TryEnqueueAsync(Session(), Request(Intent()), CancellationToken.None);
        var written = store.Single;

        // Discovered from the mapper: a key is required when Map refuses the payload without it.
        var required = written.Payload.Keys
            .Where(key => Refuses(written, written.Payload.Where(pair => pair.Key != key).ToDictionary(pair => pair.Key, pair => pair.Value)))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ["ActorType", "Category", "CorrelationId", "EntityType", "Operation", "Outcome", "SourceService", "TenantId"],
            required);
        Assert.All(required, key => Assert.NotNull(written.Payload[key]));
    }

    /// <summary>
    /// The two audit doors write the SAME payload: same keys in the same order. The central pipeline's is pinned
    /// here as it was before the builder was shared (AuditService.BuildPayload, 2026-10-02).
    /// </summary>
    [Fact]
    public async Task The_in_transaction_payload_has_exactly_the_keys_of_the_central_pipelines_payload_in_its_order()
    {
        var store = new CapturingStore();
        await Writer(store, Person("platform_admin", Administrator)).TryEnqueueAsync(Session(), Request(Intent()), CancellationToken.None);

        Assert.Equal(
            [
                "TenantId", "CorrelationId", "RequestType", "ActorType", "ActorId", "ActorEmailMasked", "ActorDisplayNameMasked",
                "TargetTenantId", "Category", "EntityType", "EntityId", "Operation", "Outcome", "BeforeState", "AfterState",
                "Metadata", "IpAddressMasked", "UserAgent", "OccurredAtUtc", "SourceService", "SourceModule", "IsMetaAudit",
                "RedactionStatus"
            ],
            store.Single.Payload.Keys.ToArray());
    }

    [Fact]
    public async Task Before_and_after_state_pass_through_the_redactor_like_the_central_pipelines()
    {
        var store = new CapturingStore();
        var intent = Intent() with
        {
            BeforeState = new Dictionary<string, object?> { ["IsEnabled"] = true, ["Password"] = "hunter2" },
            AfterState = new Dictionary<string, object?> { ["IsEnabled"] = false, ["ApiKey"] = "sk-123" }
        };

        await Writer(store, Person("platform_admin", Administrator)).TryEnqueueAsync(Session(), Request(intent), CancellationToken.None);

        var audit = Deliver(store.Single);
        Assert.Equal("[REDACTED]", audit.BeforeState!["Password"]);
        Assert.Equal("[REDACTED]", audit.AfterState!["ApiKey"]);
        Assert.Equal(AuditRedactionStatus.SensitiveFieldsRedacted, audit.RedactionStatus);
    }

    // ── fail-closed: who made the change must be nameable ───────────────────────────────────────────────

    [Fact]
    public async Task A_call_with_no_signed_in_person_and_no_named_job_is_refused_and_nothing_is_stored()
    {
        var store = new CapturingStore();

        var refusal = await Assert.ThrowsAsync<TransactionOwnedAuditRefusedException>(() =>
            Writer(store, Nobody()).TryEnqueueAsync(Session(), Request(Intent()), CancellationToken.None));

        Assert.Contains("no signed-in principal", refusal.Message);
        Assert.Empty(store.Requests);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("superuser")]
    public async Task A_signed_in_principal_whose_token_names_no_known_actor_type_is_refused_never_recorded_as_system(string? actorTypeClaim)
    {
        var store = new CapturingStore();

        await Assert.ThrowsAsync<TransactionOwnedAuditRefusedException>(() =>
            Writer(store, Person(actorTypeClaim, Administrator))
                .TryEnqueueAsync(Session(), Request(Intent() with { SystemActor = "a-job-name-must-not-rescue-a-person" }), CancellationToken.None));

        Assert.Empty(store.Requests);
    }

    [Fact]
    public async Task A_signed_in_principal_with_no_user_id_is_refused()
    {
        var store = new CapturingStore();

        var refusal = await Assert.ThrowsAsync<TransactionOwnedAuditRefusedException>(() =>
            Writer(store, Person("platform_admin", Guid.Empty)).TryEnqueueAsync(Session(), Request(Intent()), CancellationToken.None));

        Assert.Contains("no user id", refusal.Message);
        Assert.Empty(store.Requests);
    }

    [Fact]
    public async Task A_request_with_a_hand_written_payload_and_no_intent_is_refused()
    {
        var store = new CapturingStore();
        var handWritten = new AuditOutboxWriteRequest
        {
            TenantId = Tenant,
            CorrelationId = Guid.NewGuid(),
            IdempotencyKey = "physical-entitlement:x:1",
            RequestType = "DisableTenantModuleEntitlementCommand",
            Operation = AuditOperation.Deactivate,
            EntityType = "TenantModuleEntitlement",
            Payload = new Dictionary<string, object?> { ["ModuleCode"] = "CRM", ["Outcome"] = "Succeeded" } // the pre-fix shape
        };

        await Assert.ThrowsAsync<TransactionOwnedAuditRefusedException>(() =>
            Writer(store, Person("platform_admin", Administrator)).TryEnqueueAsync(Session(), handWritten, CancellationToken.None));

        Assert.Empty(store.Requests);
    }

    [Fact]
    public async Task An_intent_without_a_category_is_refused_rather_than_filed_under_a_guess()
    {
        var store = new CapturingStore();

        await Assert.ThrowsAsync<TransactionOwnedAuditRefusedException>(() =>
            Writer(store, Person("platform_admin", Administrator))
                .TryEnqueueAsync(Session(), Request(Intent() with { Category = AuditCategory.Unknown }), CancellationToken.None));

        Assert.Empty(store.Requests);
    }

    // ── one door, one builder: nothing else in production writes an in-transaction payload ──────────────

    [Fact]
    public void Production_has_exactly_one_in_transaction_writer_and_it_is_the_canonical_one()
    {
        var implementers = new[] { typeof(CanonicalTransactionalAuditOutboxWriter).Assembly, typeof(AuditOutboxPayloadMapper).Assembly }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(ITransactionalAuditOutboxWriter).IsAssignableFrom(type))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Equal([typeof(CanonicalTransactionalAuditOutboxWriter).FullName], implementers);
    }

    [Fact]
    public void The_three_callers_state_an_intent_and_write_no_payload_of_their_own()
    {
        foreach (var caller in new[]
                 {
                     Source("Features", "Tenants", "Commercial", "Entitlements", "Handlers", "CommandHandlers", "PhysicalEntitlementAuditIntent.cs"),
                     Source("Features", "Tenants", "Commercial", "Subscriptions", "TenantSubscriptionTransactionWriter.cs"),
                     Source("Features", "GlobalApplicability", "GlobalApplicabilityTransactionCoordinator.cs")
                 })
        {
            Assert.Contains("Intent = new TransactionOwnedAuditIntent", caller, StringComparison.Ordinal);
            Assert.DoesNotContain("Payload =", caller, StringComparison.Ordinal);
            Assert.DoesNotContain("[\"Outcome\"]", caller, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Only_the_canonical_writer_reaches_the_store_and_only_the_one_builder_names_the_payload_keys()
    {
        var application = Directory.EnumerateFiles(ApplicationRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToDictionary(path => Path.GetFileName(path), File.ReadAllText, StringComparer.Ordinal);

        Assert.Equal(
            ["CanonicalTransactionalAuditOutboxWriter.cs"],
            application.Where(file => file.Value.Contains(".TryInsertAsync(session", StringComparison.Ordinal)).Select(file => file.Key).ToArray());
        // The payload's key list lives in the builder and nowhere else in Application.
        Assert.Equal(
            ["AuditOutboxPayload.cs"],
            application.Where(file => file.Value.Contains("[\"ActorEmailMasked\"] =", StringComparison.Ordinal)).Select(file => file.Key).ToArray());
        Assert.Contains("AuditOutboxPayload.Build(", application["AuditService.cs"], StringComparison.Ordinal);
        Assert.Contains("AuditOutboxPayload.Build(", application["CanonicalTransactionalAuditOutboxWriter.cs"], StringComparison.Ordinal);
    }

    // ── harness ─────────────────────────────────────────────────────────────────────────────────────────

    private static TransactionOwnedAuditIntent Intent() => new()
    {
        Category = AuditCategory.SubscriptionBilling,
        TargetTenantId = Tenant,
        BeforeState = new Dictionary<string, object?> { ["IsEnabled"] = true },
        AfterState = new Dictionary<string, object?> { ["IsEnabled"] = false },
        Metadata = new Dictionary<string, object?> { ["ModuleCode"] = "CRM" },
        SourceModule = "subscription-billing"
    };

    private static AuditOutboxWriteRequest Request(TransactionOwnedAuditIntent intent) => new()
    {
        TenantId = Tenant,
        CorrelationId = Guid.NewGuid(),
        IdempotencyKey = $"physical-entitlement:DisableTenantModuleEntitlementCommand:{Guid.NewGuid():N}",
        RequestType = "DisableTenantModuleEntitlementCommand",
        Operation = AuditOperation.Deactivate,
        EntityType = "TenantModuleEntitlement",
        EntityId = Guid.NewGuid(),
        Intent = intent
    };

    private static AuditEvent Deliver(AuditOutboxWriteRequest written) =>
        new AuditOutboxPayloadMapper().Map(Item(written, written.Payload), DateTimeOffset.UtcNow);

    private static bool Refuses(AuditOutboxWriteRequest written, IReadOnlyDictionary<string, object?> payload)
    {
        try
        {
            new AuditOutboxPayloadMapper().Map(Item(written, payload), DateTimeOffset.UtcNow);
            return false;
        }
        catch (AuditOutboxPayloadMappingException)
        {
            return true;
        }
    }

    private static AuditOutboxProcessingItem Item(AuditOutboxWriteRequest written, IReadOnlyDictionary<string, object?> payload) => new(
        Guid.NewGuid(), written.TenantId, written.CorrelationId, written.IdempotencyKey, written.RequestType, written.Operation,
        written.EntityType, written.EntityId, payload, Diten.Platform.Infrastructure.Persistence.Models.AuditOutboxStatus.Processing, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private static CanonicalTransactionalAuditOutboxWriter Writer(CapturingStore store, (ITenantAuthorizationContext Principal, ICurrentUserContext User) who) =>
        new(store, who.Principal, who.User, new SensitiveFieldRedactor(new SensitiveFieldRedactionRegistry()));

    private static (ITenantAuthorizationContext, ICurrentUserContext) Person(string? actorTypeClaim, Guid userId)
    {
        var principal = new Mock<ITenantAuthorizationContext>();
        principal.SetupGet(x => x.IsAuthenticated).Returns(true);
        principal.SetupGet(x => x.ActorType).Returns(actorTypeClaim);
        principal.SetupGet(x => x.UserId).Returns(userId);
        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(x => x.IsAuthenticated).Returns(true);
        user.SetupGet(x => x.UserId).Returns(userId);
        user.SetupGet(x => x.Email).Returns("platform.admin@di10.test");
        user.SetupGet(x => x.DisplayName).Returns("Platform Admin");
        user.SetupGet(x => x.ActorName).Returns("platform.admin@di10.test");
        return (principal.Object, user.Object);
    }

    private static (ITenantAuthorizationContext, ICurrentUserContext) Nobody()
    {
        var principal = new Mock<ITenantAuthorizationContext>();
        principal.SetupGet(x => x.IsAuthenticated).Returns(false);
        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(x => x.IsAuthenticated).Returns(false);
        user.SetupGet(x => x.ActorName).Returns("system");
        return (principal.Object, user.Object);
    }

    private static IPlatformTransactionSession Session() => Mock.Of<IPlatformTransactionSession>();

    private static string Source(params string[] parts) => File.ReadAllText(Path.Combine([ApplicationRoot(), .. parts]));

    private static string ApplicationRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "services", "Diten.Platform", "src", "Diten.Platform.Application")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("repo root"), "services", "Diten.Platform", "src", "Diten.Platform.Application");
    }

    private sealed class CapturingStore : ITransactionalAuditOutboxStore
    {
        public List<AuditOutboxWriteRequest> Requests { get; } = [];

        public AuditOutboxWriteRequest Single => Assert.Single(Requests);

        public Task<bool> TryInsertAsync(IPlatformTransactionSession session, AuditOutboxWriteRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            return Task.FromResult(true);
        }
    }
}
