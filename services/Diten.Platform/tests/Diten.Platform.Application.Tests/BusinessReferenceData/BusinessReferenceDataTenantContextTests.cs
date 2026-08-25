using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class BusinessReferenceDataTenantContextTests
{
    [Fact]
    public void NestedResolverScopes_RestoreReferenceThenConsumerThenPriorContext()
    {
        var context = new TenantContext();
        var prior = Guid.NewGuid();
        var consumer = Guid.NewGuid();
        var reference = Guid.NewGuid();
        context.SetTenant(prior);

        using (TenantScope.Begin(context, consumer))
        {
            Assert.Equal(consumer, context.TenantId);
            using (TenantScope.Begin(context, reference))
            {
                Assert.Equal(reference, context.TenantId);
            }

            Assert.Equal(consumer, context.TenantId);
        }

        Assert.Equal(prior, context.TenantId);
    }

    [Fact]
    public void SequentialTenantRequests_DoNotLeakConsumerOrReferenceScope()
    {
        var context = new TenantContext();
        var prior = Guid.NewGuid();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var reference = Guid.NewGuid();
        context.SetTenant(prior);

        ResolveInScopes(context, tenantA, reference);
        Assert.Equal(prior, context.TenantId);
        ResolveInScopes(context, tenantB, reference);
        Assert.Equal(prior, context.TenantId);
    }

    [Fact]
    public void Exception_RestoresReferenceThenConsumerThenPriorContext()
    {
        var context = new TenantContext();
        var prior = Guid.NewGuid();
        var consumer = Guid.NewGuid();
        var reference = Guid.NewGuid();
        context.SetTenant(prior);

        void ThrowInsideScopes()
        {
            using (TenantScope.Begin(context, consumer))
            using (TenantScope.Begin(context, reference))
            {
                throw new InvalidOperationException("test-only");
            }
        }

        Assert.Throws<InvalidOperationException>((Action)ThrowInsideScopes);

        Assert.Equal(prior, context.TenantId);
    }

    private static void ResolveInScopes(TenantContext context, Guid consumer, Guid reference)
    {
        using (TenantScope.Begin(context, consumer))
        {
            Assert.Equal(consumer, context.TenantId);
            using (TenantScope.Begin(context, reference))
            {
                Assert.Equal(reference, context.TenantId);
            }

            Assert.Equal(consumer, context.TenantId);
        }
    }
}
