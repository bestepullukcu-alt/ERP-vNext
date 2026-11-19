using Diten.Platform.Application;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

/// <summary>
/// MOD-0040-FU02 (K-10) AC-29 — the refusal path of the in-transaction door: only Denied and Failed, the same actor and
/// source-service rule as the success path, and its own scoped registration in the Application DI.
/// </summary>
public sealed class TransactionalAuditRefusalWriterTests
{
    [Theory]
    [InlineData(AuditOutcome.Denied)]
    [InlineData(AuditOutcome.Failed)]
    public async Task AC29_a_refusal_is_recorded_with_its_outcome_the_same_actor_and_source_service(AuditOutcome outcome)
    {
        var store = new TransactionOwnedAuditSourceServiceTests.Store();
        var door = TransactionOwnedAuditSourceServiceTests.Door(store);

        await door.TryEnqueueRefusalAsync(Mock.Of<IPlatformTransactionSession>(),
            TransactionOwnedAuditSourceServiceTests.Request("Diten.MdmService"), outcome);

        var audit = TransactionOwnedAuditSourceServiceTests.Deliver(store.Single);
        Assert.Equal(outcome, audit.Outcome);
        Assert.Equal(AuditActorType.TenantUser, audit.ActorType);
        Assert.Equal("Diten.MdmService", audit.SourceService);
    }

    [Theory]
    [InlineData(AuditOutcome.Succeeded)]
    [InlineData(AuditOutcome.Unknown)]
    public async Task AC29_a_refusal_that_says_it_succeeded_is_an_invalid_intent(AuditOutcome outcome)
    {
        var store = new TransactionOwnedAuditSourceServiceTests.Store();

        await Assert.ThrowsAsync<TransactionOwnedAuditIntentInvalidException>(() =>
            TransactionOwnedAuditSourceServiceTests.Door(store).TryEnqueueRefusalAsync(Mock.Of<IPlatformTransactionSession>(),
                TransactionOwnedAuditSourceServiceTests.Request(null), outcome));
        Assert.Empty(store.Requests);
    }

    [Fact]
    public async Task AC29_the_success_path_still_records_Succeeded()
    {
        var store = new TransactionOwnedAuditSourceServiceTests.Store();

        await TransactionOwnedAuditSourceServiceTests.Door(store).TryEnqueueAsync(Mock.Of<IPlatformTransactionSession>(),
            TransactionOwnedAuditSourceServiceTests.Request(null));

        Assert.Equal(AuditOutcome.Succeeded, TransactionOwnedAuditSourceServiceTests.Deliver(store.Single).Outcome);
    }

    [Fact]
    public void AC29_the_refusal_writer_has_its_own_scoped_registration_on_the_canonical_door()
    {
        var services = new ServiceCollection();
        services.AddApplication();

        var registration = Assert.Single(services, d => d.ServiceType == typeof(ITransactionalAuditRefusalWriter));
        Assert.Equal(ServiceLifetime.Scoped, registration.Lifetime);
        Assert.Equal(typeof(CanonicalTransactionalAuditOutboxWriter), registration.ImplementationType);
        Assert.Single(services, d => d.ServiceType == typeof(ITransactionalAuditOutboxWriter));
    }
}
