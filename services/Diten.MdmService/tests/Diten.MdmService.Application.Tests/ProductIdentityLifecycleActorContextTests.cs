using System.Security.Claims;
using Diten.MdmService.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductIdentityLifecycleActorContextTests
{
    private static readonly Guid ActorId = Guid.Parse("81000000-0000-0000-0000-000000000081");

    [Fact]
    public void Resolve_exact_human_subject_and_permissions_succeeds()
    {
        var context = Create(
            new("actor_type", "tenant_user"),
            new("sub", ActorId.ToString("D")),
            new(ClaimTypes.NameIdentifier, ActorId.ToString("D")),
            new("permissions", "mdm.global-products.read,mdm.global-products.submit"));

        var resolved = context.TryResolveCanonicalHumanSubject(out var subjectId);

        Assert.True(resolved);
        Assert.Equal(ActorId, subjectId);
        Assert.True(context.HasPermission("mdm.global-products.submit"));
        Assert.False(context.HasPermission("mdm.global-products.retire"));
    }

    [Theory]
    [InlineData("service")]
    [InlineData("")]
    public void Resolve_non_tenant_human_actor_fails_closed(string actorType)
    {
        var context = Create(
            new("actor_type", actorType),
            new("sub", ActorId.ToString("D")));

        Assert.False(context.TryResolveCanonicalHumanSubject(out _));
    }

    [Fact]
    public void Resolve_conflicting_or_duplicate_identity_fails_closed()
    {
        var conflict = Create(
            new("actor_type", "tenant_user"),
            new("sub", ActorId.ToString("D")),
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D")));
        var duplicate = Create(
            new("actor_type", "tenant_user"),
            new("sub", ActorId.ToString("D")),
            new("sub", ActorId.ToString("D")));

        Assert.False(conflict.TryResolveCanonicalHumanSubject(out _));
        Assert.False(duplicate.TryResolveCanonicalHumanSubject(out _));
    }

    [Fact]
    public void Resolve_malformed_or_empty_guid_fails_closed()
    {
        var malformed = Create(new("actor_type", "tenant_user"), new("sub", "not-a-guid"));
        var empty = Create(new("actor_type", "tenant_user"), new("sub", Guid.Empty.ToString("D")));

        Assert.False(malformed.TryResolveCanonicalHumanSubject(out _));
        Assert.False(empty.TryResolveCanonicalHumanSubject(out _));
    }

    private static ProductIdentityLifecycleActorContext Create(params Claim[] claims)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"))
        };
        return new ProductIdentityLifecycleActorContext(new HttpContextAccessor { HttpContext = httpContext });
    }
}
