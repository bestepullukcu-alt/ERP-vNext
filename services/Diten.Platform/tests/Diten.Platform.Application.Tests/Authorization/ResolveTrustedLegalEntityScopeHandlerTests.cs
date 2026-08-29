using Diten.Platform.Application.Authorization;
using Diten.Platform.Application.Features.AccessGovernance.LegalEntityScopeResolution.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.AccessGovernance.LegalEntityScopeResolution.Queries;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.AccessGovernance;

public sealed class ResolveTrustedLegalEntityScopeHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly ResolveTrustedLegalEntityScopeQuery Query = new(Guid.NewGuid(), Guid.NewGuid(), "product-item-sku-master", "mdm.gskus.read");

    [Fact]
    public async Task Success_preserves_sorted_multiple_candidates_and_deterministic_time()
    {
        var ids = new[] { Guid.Parse("10000000-0000-0000-0000-000000000000"), Guid.Parse("20000000-0000-0000-0000-000000000000") };
        var resolver = new Mock<IOrgDataScopeCandidateResolver>();
        resolver.Setup(x => x.ResolveAsync(Query.TenantId, Query.SubjectId, It.IsAny<CancellationToken>())).ReturnsAsync(new OrgDataScopeCandidateSet(ids));

        var response = await new ResolveTrustedLegalEntityScopeHandler(resolver.Object, new FixedTimeProvider(Now)).Handle(Query, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(ids, response.Data!.LegalEntityIds);
        Assert.Equal(Now, response.Data.EvaluatedAtUtc);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("unsorted")]
    [InlineData("empty")]
    public async Task Invalid_candidate_contract_returns_409(string kind)
    {
        var a = Guid.Parse("10000000-0000-0000-0000-000000000000");
        var b = Guid.Parse("20000000-0000-0000-0000-000000000000");
        IReadOnlyList<Guid> ids = kind switch { "duplicate" => [a, a], "unsorted" => [b, a], _ => [Guid.Empty] };
        var resolver = new Mock<IOrgDataScopeCandidateResolver>();
        resolver.Setup(x => x.ResolveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new OrgDataScopeCandidateSet(ids));

        var response = await new ResolveTrustedLegalEntityScopeHandler(resolver.Object, TimeProvider.System).Handle(Query, CancellationToken.None);

        Assert.False(response.IsSuccessful); Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Only_explicit_unavailable_maps_503_and_programming_or_cancellation_propagates()
    {
        var resolver = new Mock<IOrgDataScopeCandidateResolver>();
        var handler = new ResolveTrustedLegalEntityScopeHandler(resolver.Object, TimeProvider.System);
        resolver.Setup(x => x.ResolveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OrgDataScopeCandidateUnavailableException("down"));
        Assert.Equal(503, (await handler.Handle(Query, CancellationToken.None)).StatusCode);
        resolver.Setup(x => x.ResolveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bug"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(Query, CancellationToken.None));
        resolver.Setup(x => x.ResolveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        await Assert.ThrowsAsync<OperationCanceledException>(() => handler.Handle(Query, CancellationToken.None));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    { public override DateTimeOffset GetUtcNow() => now; }
}
