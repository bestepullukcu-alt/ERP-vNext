using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Eventing;
using Diten.PpmService.Contracts.Events;
using Xunit;

namespace Diten.Platform.Application.Tests.Eventing;

public sealed class PpmAuditIntentV1AuditMappingTests
{
    [Theory]
    [InlineData("created", AuditOperation.Create)]
    [InlineData("updated", AuditOperation.Update)]
    [InlineData("lifecycle-changed", AuditOperation.LifecycleTransition)]
    [InlineData("soft-deleted", AuditOperation.Delete)]
    public void Maps_exact_v1_mutation_to_auditable_operation(string mutation, AuditOperation expectedOperation)
    {
        var intent = new PpmAuditIntentSubmittedV1(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Portfolio",
            Guid.NewGuid(),
            mutation,
            DateTime.UtcNow);

        var result = PpmAuditIntentV1AuditMapping.Map(intent);

        Assert.Equal(AuditCategory.PortfolioDelivery, result.Category);
        Assert.Equal(expectedOperation, result.Operation);
        Assert.Equal(intent.AuditIntentId, result.AuditIntentId);
        Assert.Equal(intent.ActorId, result.ActorId);
        Assert.Equal(intent.EntityType, result.EntityType);
        Assert.Equal(intent.EntityId, result.EntityId);
        Assert.Equal(intent.OccurredAtUtc, result.OccurredAtUtc);
    }

    [Fact]
    public void Has_no_target_state_inference_surface()
    {
        var properties = typeof(PpmAuditIntentV1AuditProjection).GetProperties();

        Assert.DoesNotContain(properties, property =>
            string.Equals(property.Name, "TargetState", StringComparison.Ordinal));
        Assert.Equal("Diten.PpmService", PpmAuditIntentV1AuditMapping.SourceService);
    }
}
