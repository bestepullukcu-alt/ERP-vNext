using Diten.Platform.Common.Authorization;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class NoOpDataScopeResolverTests
{
    [Fact]
    public async Task ResolveAsync_returns_empty_for_arbitrary_inputs()
    {
        var resolver = new NoOpDataScopeResolver();

        var scopes = await resolver.ResolveAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "HR",
            "EmployeeRead",
            CancellationToken.None);

        Assert.NotNull(scopes);
        Assert.Empty(scopes);
    }

    [Fact]
    public async Task ResolveAsync_returns_empty_when_feature_code_is_null()
    {
        var resolver = new NoOpDataScopeResolver();

        var scopes = await resolver.ResolveAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "HR",
            featureCode: null,
            CancellationToken.None);

        Assert.NotNull(scopes);
        Assert.Empty(scopes);
    }

    [Fact]
    public async Task ResolveAsync_returns_empty_for_empty_guids()
    {
        var resolver = new NoOpDataScopeResolver();

        var scopes = await resolver.ResolveAsync(
            Guid.Empty,
            Guid.Empty,
            "HR",
            null,
            CancellationToken.None);

        Assert.Empty(scopes);
    }

    [Fact]
    public async Task ResolveAsync_throws_when_cancellation_already_requested()
    {
        var resolver = new NoOpDataScopeResolver();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            resolver.ResolveAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "HR",
                null,
                cts.Token));
    }
}
