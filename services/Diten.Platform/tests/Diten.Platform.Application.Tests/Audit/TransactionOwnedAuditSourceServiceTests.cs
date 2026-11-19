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
/// MOD-0040-FU02 (K-9) AC-28 / AC-40 — the in-transaction door's additive source-service rule: an empty name keeps
/// <c>Diten.Platform</c> (every existing caller unchanged), a name from the closed set is recorded as given, any other —
/// unknown, wrong case, padded — is an invalid intent and nothing reaches the store.
/// </summary>
public sealed class TransactionOwnedAuditSourceServiceTests
{
    private static readonly Guid Tenant = Guid.Parse("40fc0002-0000-4000-8000-00000000a0a7");
    private static readonly Guid User = Guid.Parse("40fc0002-0000-4000-8000-0000000000c7");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AC28_an_intent_without_a_source_service_is_recorded_as_the_Platform(string? sourceService)
    {
        var store = new Store();

        await Door(store).TryEnqueueAsync(Mock.Of<IPlatformTransactionSession>(), Request(sourceService));

        Assert.Equal("Diten.Platform", Deliver(store.Single).SourceService);
    }

    [Theory]
    [InlineData("Diten.FinanceTreasuryService")]
    [InlineData("Diten.MdmService")]
    [InlineData("Diten.Platform")]
    public async Task AC28_a_name_from_the_closed_set_is_recorded_as_given(string sourceService)
    {
        var store = new Store();

        await Door(store).TryEnqueueAsync(Mock.Of<IPlatformTransactionSession>(), Request(sourceService));

        Assert.Equal(sourceService, Deliver(store.Single).SourceService);
    }

    [Theory]
    [InlineData("Diten.Unknown")]
    [InlineData("diten.mdmservice")]
    [InlineData(" Diten.MdmService")]
    [InlineData("Diten.MdmService ")]
    [InlineData("Diten.InventoryService")]
    public async Task AC40_a_name_outside_the_closed_set_is_an_invalid_intent_and_nothing_is_written(string sourceService)
    {
        var store = new Store();

        await Assert.ThrowsAsync<TransactionOwnedAuditIntentInvalidException>(() =>
            Door(store).TryEnqueueAsync(Mock.Of<IPlatformTransactionSession>(), Request(sourceService)));
        await Assert.ThrowsAsync<TransactionOwnedAuditIntentInvalidException>(() =>
            Door(store).TryEnqueueRefusalAsync(Mock.Of<IPlatformTransactionSession>(), Request(sourceService), AuditOutcome.Failed));

        Assert.Empty(store.Requests);
    }

    [Fact]
    public void AC40_the_intent_comment_states_the_closed_set_rule()
    {
        var source = File.ReadAllText(Path.Combine(RepoPaths.Root(), "services", "Diten.Platform", "src", "Diten.Platform.Application",
            "Contracts", "Audit", "TransactionOwnedAuditIntent.cs"));

        Assert.Contains("TransactionOwnedAuditSourceServices", source);
        Assert.Contains("public string? SourceService { get; init; }", source);
    }

    internal static CanonicalTransactionalAuditOutboxWriter Door(ITransactionalAuditOutboxStore store, string actorType = "tenant_user")
    {
        var principal = new Mock<ITenantAuthorizationContext>();
        principal.SetupGet(x => x.IsAuthenticated).Returns(true);
        principal.SetupGet(x => x.ActorType).Returns(actorType);
        principal.SetupGet(x => x.UserId).Returns(User);
        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(x => x.IsAuthenticated).Returns(true);
        user.SetupGet(x => x.UserId).Returns(User);
        user.SetupGet(x => x.Email).Returns("tenant.user@di10.test");
        return new CanonicalTransactionalAuditOutboxWriter(store, principal.Object, user.Object,
            new SensitiveFieldRedactor(new SensitiveFieldRedactionRegistry()));
    }

    internal static AuditOutboxWriteRequest Request(string? sourceService) => new()
    {
        TenantId = Tenant,
        CorrelationId = Guid.NewGuid(),
        IdempotencyKey = $"record-code:refused:{Guid.NewGuid():N}",
        RequestType = "AllocateRecordCodeCommand",
        Operation = AuditOperation.Create,
        EntityType = "RecordCodeAllocation",
        Intent = new TransactionOwnedAuditIntent
        {
            Category = AuditCategory.PlatformConfiguration,
            TargetTenantId = Tenant,
            SourceService = sourceService,
            SourceModule = "MOD-0040-FU02",
            Metadata = new Dictionary<string, object?> { ["EventName"] = "test" }
        }
    };

    internal static AuditEvent Deliver(AuditOutboxWriteRequest written) =>
        new AuditOutboxPayloadMapper().Map(new AuditOutboxProcessingItem(
            Guid.NewGuid(), written.TenantId, written.CorrelationId, written.IdempotencyKey, written.RequestType, written.Operation,
            written.EntityType, written.EntityId, written.Payload, Diten.Platform.Infrastructure.Persistence.Models.AuditOutboxStatus.Processing,
            1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow), DateTimeOffset.UtcNow);

    internal sealed class Store : ITransactionalAuditOutboxStore
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
