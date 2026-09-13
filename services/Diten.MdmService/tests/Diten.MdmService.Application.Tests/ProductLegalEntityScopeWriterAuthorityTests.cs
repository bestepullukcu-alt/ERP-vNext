using System.Security.Claims;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Commands;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductLegalEntityScopeWriterAuthorityTests
{
    private const string Permission = "mdm.product-legal-entity-scopes.replace";

    [Fact]
    public async Task ResolveForegroundReplaceAsync_ExactCanonicalHumanAndPermission_IssuesBoundAuthority()
    {
        var tenantId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var aggregateId = Guid.NewGuid();
        var mutation = Mutation();
        var provider = Provider(tenantId, subjectId, [new("permission", Permission)]);

        var authority = await provider.ResolveForegroundReplaceAsync(aggregateId, mutation);

        Assert.NotNull(authority);
        Assert.Equal(tenantId, authority.TenantId);
        Assert.Equal(subjectId, authority.SubjectId);
        Assert.Equal(mutation.CommandId, authority.CommandId);
        Assert.Equal(AuditAggregateType.ProductLegalEntityScopePolicy, authority.AggregateType);
        Assert.Equal(ProductAuditOperation.ProductLegalEntityScopePolicyReplaced, authority.Operation);
        Assert.Equal(Permission, authority.Permission);
        Assert.True(authority.MatchesForegroundReplace(
            tenantId,
            subjectId,
            mutation.CommandId,
            aggregateId,
            mutation.Kind,
            mutation.PayloadFingerprint));
    }

    [Fact]
    public async Task ResolveForegroundReplaceAsync_WrongTenantPurposeOperationAggregateOrMutation_Denies()
    {
        var tenantId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var aggregateId = Guid.NewGuid();
        var mutation = Mutation();
        var provider = Provider(tenantId, subjectId, [new("permission", Permission)]);
        var authority = Assert.IsType<Domain.ValueObjects.ProductLegalEntityScopeVerifiedWriterAuthority>(
            await provider.ResolveForegroundReplaceAsync(aggregateId, mutation));

        Assert.False(authority.MatchesForegroundReplace(
            Guid.NewGuid(), subjectId, mutation.CommandId, aggregateId, mutation.Kind, mutation.PayloadFingerprint));
        Assert.False(authority.MatchesForegroundReplace(
            tenantId, Guid.NewGuid(), mutation.CommandId, aggregateId, mutation.Kind, mutation.PayloadFingerprint));
        Assert.False(authority.MatchesForegroundReplace(
            tenantId, subjectId, Guid.NewGuid(), aggregateId, mutation.Kind, mutation.PayloadFingerprint));
        Assert.False(authority.MatchesForegroundReplace(
            tenantId, subjectId, mutation.CommandId, Guid.NewGuid(), mutation.Kind, mutation.PayloadFingerprint));
        Assert.False(authority.MatchesForegroundReplace(
            tenantId, subjectId, mutation.CommandId, aggregateId, "replace", mutation.PayloadFingerprint));
        Assert.False(authority.MatchesForegroundReplace(
            tenantId, subjectId, mutation.CommandId, aggregateId, mutation.Kind, new string('B', 64)));

        Assert.Null(await provider.ResolveForegroundReplaceAsync(
            aggregateId,
            mutation with { Kind = "BackgroundReplace" }));
        Assert.Null(await provider.ResolveForegroundReplaceAsync(
            aggregateId,
            mutation with { PayloadFingerprint = mutation.PayloadFingerprint.ToLowerInvariant() }));
        Assert.Null(await provider.ResolveForegroundReplaceAsync(
            aggregateId,
            mutation with { EnforcedGlobalProductCreateProhibited = true }));
    }

    [Fact]
    public async Task ResolveForegroundReplaceAsync_MissingWrongDuplicateOrCaseDriftPermission_Denies()
    {
        var tenantId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var mutation = Mutation();
        var aggregateId = Guid.NewGuid();

        Assert.Null(await Provider(tenantId, subjectId, []).ResolveForegroundReplaceAsync(aggregateId, mutation));
        Assert.Null(await Provider(tenantId, subjectId, [new("permission", "mdm.product-legal-entity-scopes.read")])
            .ResolveForegroundReplaceAsync(aggregateId, mutation));
        Assert.Null(await Provider(tenantId, subjectId, [new("permission", Permission.ToUpperInvariant())])
            .ResolveForegroundReplaceAsync(aggregateId, mutation));
        Assert.Null(await Provider(tenantId, subjectId,
            [new("permission", Permission), new("permissions", Permission)])
            .ResolveForegroundReplaceAsync(aggregateId, mutation));
    }

    [Fact]
    public async Task ResolveForegroundReplaceAsync_ServiceForgedOrConflictingHumanContext_Denies()
    {
        var tenantId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var mutation = Mutation();
        var aggregateId = Guid.NewGuid();

        Assert.Null(await Provider(tenantId, subjectId, [new("permission", Permission)], "service")
            .ResolveForegroundReplaceAsync(aggregateId, mutation));
        Assert.Null(await Provider(tenantId, subjectId, [new("permission", Permission)], "platform_admin")
            .ResolveForegroundReplaceAsync(aggregateId, mutation));
        Assert.Null(await Provider(tenantId, subjectId, [
            new("permission", Permission),
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D"))
        ]).ResolveForegroundReplaceAsync(aggregateId, mutation));
        Assert.Null(await Provider(tenantId, subjectId, [
            new("permission", Permission),
            new("tenant_id", tenantId.ToString("D"))
        ]).ResolveForegroundReplaceAsync(aggregateId, mutation));

        var wrongHeader = ProviderContext(
            tenantId,
            subjectId,
            [new("permission", Permission)],
            "tenant_user");
        wrongHeader.HttpContext!.Request.Headers["X-Tenant-Id"] = Guid.NewGuid().ToString("D");
        Assert.Null(await wrongHeader.Provider.ResolveForegroundReplaceAsync(aggregateId, mutation));
    }

    [Fact]
    public async Task ResolveForegroundReplaceAsync_CancelledRequest_PropagatesWithoutAuthority()
    {
        var tenantId = Guid.NewGuid();
        var provider = Provider(tenantId, Guid.NewGuid(), [new("permission", Permission)]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            provider.ResolveForegroundReplaceAsync(Guid.NewGuid(), Mutation(), cancellation.Token));
    }

    private static ProductLegalEntityScopeMutationIdentity Mutation()
    {
        var command = new ReplaceProductLegalEntityScopePolicyCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new()
            {
                ExpectedVersion = 3,
                Mode = ProductLegalEntityScopeMode.GroupWide
            });
        return ProductLegalEntityScopeMutationIdentity.Create(command);
    }

    private static ProductLegalEntityScopeWriterAuthorityProvider Provider(
        Guid tenantId,
        Guid subjectId,
        IReadOnlyCollection<Claim> additionalClaims,
        string actorType = "tenant_user") =>
        ProviderContext(tenantId, subjectId, additionalClaims, actorType).Provider;

    private static ProviderHarness ProviderContext(
        Guid tenantId,
        Guid subjectId,
        IReadOnlyCollection<Claim> additionalClaims,
        string actorType)
    {
        var claims = new List<Claim>
        {
            new("actor_type", actorType),
            new("tenant_id", tenantId.ToString("D")),
            new("sub", subjectId.ToString("D"))
        };
        claims.AddRange(additionalClaims);
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
        };
        context.Request.Headers["X-Tenant-Id"] = tenantId.ToString("D");
        var tenant = new TenantContext();
        tenant.SetTenant(tenantId);
        return new ProviderHarness(
            new ProductLegalEntityScopeWriterAuthorityProvider(
                new HttpContextAccessor { HttpContext = context },
                tenant),
            context);
    }

    private sealed record ProviderHarness(
        ProductLegalEntityScopeWriterAuthorityProvider Provider,
        DefaultHttpContext HttpContext)
    {
        public static implicit operator ProductLegalEntityScopeWriterAuthorityProvider(ProviderHarness value) =>
            value.Provider;
    }
}
