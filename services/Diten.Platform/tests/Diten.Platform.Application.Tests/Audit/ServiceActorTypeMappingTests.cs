using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

/// <summary>
/// MOD-0262-FU01 K-B (owner decision A, Beste 2026-11-02) — AC-31: <c>"service"</c> maps to <see cref="AuditActorType.Service"/>
/// in the ONE resolver switch, a data export still refuses a service actor, and the in-transaction door now writes a
/// service principal's record as <c>Service</c> with <c>ActorId = sub</c>.
/// </summary>
public sealed class ServiceActorTypeMappingTests
{
    [Theory]
    [InlineData("service")]
    [InlineData(" Service ")]
    public void The_service_actor_type_claim_is_Service(string claim) =>
        Assert.Equal(AuditActorType.Service, AuditActorTypeResolver.FromActorTypeClaim(claim));

    [Fact]
    public void A_data_export_by_a_service_principal_is_Unknown_so_the_writer_refuses_it()
    {
        var principal = new Mock<ITenantAuthorizationContext>();
        principal.SetupGet(p => p.IsAuthenticated).Returns(true);
        principal.SetupGet(p => p.ActorType).Returns("service");

        Assert.Equal(AuditActorType.Unknown, AuditActorTypeResolver.ForDataExport(principal.Object));
        Assert.Equal(AuditActorType.Service, AuditActorTypeResolver.ForCommand(principal.Object));
    }

    [Fact]
    public async Task The_transactional_door_writes_a_service_principal_as_Service_with_its_subject()
    {
        var store = new TransactionOwnedAuditSourceServiceTests.Store();

        await TransactionOwnedAuditSourceServiceTests.Door(store, actorType: "service")
            .TryEnqueueAsync(Mock.Of<IPlatformTransactionSession>(), TransactionOwnedAuditSourceServiceTests.Request("Diten.Platform"));

        var record = TransactionOwnedAuditSourceServiceTests.Deliver(store.Single);
        Assert.Equal(AuditActorType.Service, record.ActorType);
        Assert.Equal(Guid.Parse("40fc0002-0000-4000-8000-0000000000c7"), record.ActorId);
    }
}
