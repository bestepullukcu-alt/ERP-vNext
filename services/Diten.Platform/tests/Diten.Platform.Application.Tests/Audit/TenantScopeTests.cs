using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

public sealed class TenantScopeTests
{
    [Fact]
    public void Begin_ShouldRestorePreviousTenantOnDispose()
    {
        var context = new TenantContext();
        var outer = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var inner = Guid.Parse("22222222-2222-2222-2222-222222222222");
        context.SetTenant(outer);

        using (TenantScope.Begin(context, inner))
        {
            Assert.True(context.IsResolved);
            Assert.Equal(inner, context.TenantId);
            Assert.False(context.IsPlatformContext);
        }

        Assert.True(context.IsResolved);
        Assert.Equal(outer, context.TenantId);
        Assert.False(context.IsPlatformContext);
    }

    [Fact]
    public void Begin_NestedScopes_ShouldUnwindInLifoOrder()
    {
        var context = new TenantContext();
        var outer = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var middle = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var inner = Guid.Parse("55555555-5555-5555-5555-555555555555");
        context.SetTenant(outer);

        using (TenantScope.Begin(context, middle))
        {
            Assert.Equal(middle, context.TenantId);

            using (TenantScope.Begin(context, inner))
            {
                Assert.Equal(inner, context.TenantId);
            }

            Assert.Equal(middle, context.TenantId);
        }

        Assert.Equal(outer, context.TenantId);
    }

    [Fact]
    public void Begin_ShouldClearTenantOnDispose_WhenNoPreviousTenantWasResolved()
    {
        var context = new TenantContext();
        var tenant = Guid.Parse("66666666-6666-6666-6666-666666666666");

        Assert.False(context.IsResolved);

        using (TenantScope.Begin(context, tenant))
        {
            Assert.True(context.IsResolved);
            Assert.Equal(tenant, context.TenantId);
        }

        Assert.False(context.IsResolved);
    }

    [Fact]
    public void BeginPlatform_ShouldRestorePreviousTenantContextOnDispose()
    {
        var context = new TenantContext();
        var tenant = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var target = Guid.Parse("88888888-8888-8888-8888-888888888888");
        context.SetTenant(tenant);

        using (TenantScope.BeginPlatform(context, target))
        {
            Assert.True(context.IsPlatformContext);
            Assert.Equal(target, context.TargetTenantId);
        }

        Assert.True(context.IsResolved);
        Assert.False(context.IsPlatformContext);
        Assert.Equal(tenant, context.TenantId);
    }

    [Fact]
    public void Begin_NestedDispose_ShouldRestorePlatformContextWhenItWasOuterState()
    {
        var context = new TenantContext();
        var platformTarget = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var inner = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        context.SetPlatformContext(platformTarget);

        using (TenantScope.Begin(context, inner))
        {
            Assert.False(context.IsPlatformContext);
            Assert.Equal(inner, context.TenantId);
        }

        Assert.True(context.IsPlatformContext);
        Assert.Equal(platformTarget, context.TargetTenantId);
        Assert.Equal(platformTarget, context.TenantId);
    }

    [Fact]
    public void Dispose_ShouldBeIdempotent()
    {
        var context = new TenantContext();
        var tenant = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var scope = TenantScope.Begin(context, tenant);

        scope.Dispose();
        scope.Dispose();

        Assert.False(context.IsResolved);
    }
}
