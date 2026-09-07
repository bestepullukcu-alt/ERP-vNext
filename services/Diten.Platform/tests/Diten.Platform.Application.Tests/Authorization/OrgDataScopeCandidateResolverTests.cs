using System.Runtime.CompilerServices;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Authorization;
using Diten.Platform.Application.Features.AccessGovernance.LegalEntityScopeResolution.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.AccessGovernance.LegalEntityScopeResolution.Queries;
using Moq;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class OrgDataScopeCandidateResolverTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid User = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Resolves_distinct_sorted_candidates_and_memoizes_the_single_bounded_read()
    {
        var le1 = Guid.Parse("10000000-0000-0000-0000-000000000000");
        var le2 = Guid.Parse("20000000-0000-0000-0000-000000000000");
        var reader = Reader([le2, le1, le2]);
        var resolver = Resolver(reader.Object, NeverUnavailableClassifier.Instance);

        var first = await resolver.ResolveAsync(Tenant, User, CancellationToken.None);
        var replay = await resolver.ResolveAsync(Tenant, User, CancellationToken.None);

        Assert.Equal(new[] { le1, le2 }, first.LegalEntityIds);
        Assert.Same(first, replay);
        reader.Verify(x => x.ResolveLegalEntityIdsAsync(
            Tenant, User, Now, 200, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Empty_tenant_or_subject_returns_empty_without_persistence_read()
    {
        var reader = new Mock<IOrgDataScopeCandidateFactReader>(MockBehavior.Strict);
        var resolver = Resolver(reader.Object, NeverUnavailableClassifier.Instance);

        Assert.Empty((await resolver.ResolveAsync(Guid.Empty, User, default)).LegalEntityIds);
        Assert.Empty((await resolver.ResolveAsync(Tenant, Guid.Empty, default)).LegalEntityIds);
        reader.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task More_than_200_candidates_fails_closed()
    {
        var reader = Reader(Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToArray());
        var resolver = Resolver(reader.Object, NeverUnavailableClassifier.Instance);

        await Assert.ThrowsAsync<OrgDataScopeCandidateContractException>(() =>
            resolver.ResolveAsync(Tenant, User, CancellationToken.None));
    }

    [Fact]
    public async Task Production_Mongo_timeout_chain_maps_exactly_to_handler_503()
    {
        var reader = new Mock<IOrgDataScopeCandidateFactReader>();
        reader.Setup(x => x.ResolveLegalEntityIdsAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync((MongoExecutionTimeoutException)RuntimeHelpers.GetUninitializedObject(
                typeof(MongoExecutionTimeoutException)));
        var candidate = Resolver(reader.Object, new MongoOrgDataScopeCandidateAvailabilityClassifier());
        var handler = new ResolveTrustedLegalEntityScopeHandler(candidate, new FixedTimeProvider(Now));

        var response = await handler.Handle(new ResolveTrustedLegalEntityScopeQuery(
            Tenant, User, "product-item-sku-master", "mdm.gskus.read"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(503, response.StatusCode);
    }

    [Fact]
    public void Production_classifier_accepts_only_the_three_approved_availability_classes()
    {
        var classifier = new MongoOrgDataScopeCandidateAvailabilityClassifier();
        Exception[] approved =
        [
            (MongoConnectionException)RuntimeHelpers.GetUninitializedObject(typeof(MongoConnectionException)),
            (MongoExecutionTimeoutException)RuntimeHelpers.GetUninitializedObject(typeof(MongoExecutionTimeoutException)),
            new TimeoutException("selection timeout")
        ];
        Assert.All(approved, exception => Assert.True(classifier.IsUnavailable(exception)));
        Assert.False(classifier.IsUnavailable(new InvalidOperationException("bug")));
        Assert.False(classifier.IsUnavailable(new ArgumentException("bad argument")));
        Assert.False(classifier.IsUnavailable(new BsonSerializationException("bad bson")));
        Assert.False(classifier.IsUnavailable(new OperationCanceledException("cancel")));
    }

    [Fact]
    public async Task Programming_argument_null_serialization_and_cancellation_exceptions_propagate()
    {
        Exception[] exceptions =
        [
            new InvalidOperationException("bug"), new ArgumentException("bad argument"),
            new NullReferenceException("bug"), new BsonSerializationException("bad bson"),
            new OperationCanceledException("dependency cancellation")
        ];
        foreach (var exception in exceptions)
        {
            var reader = new Mock<IOrgDataScopeCandidateFactReader>();
            reader.Setup(x => x.ResolveLegalEntityIdsAsync(
                    It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);
            var resolver = Resolver(reader.Object, new MongoOrgDataScopeCandidateAvailabilityClassifier());

            var thrown = await Assert.ThrowsAsync(exception.GetType(), () => resolver.ResolveAsync(Tenant, User, default));
            Assert.Same(exception, thrown);
        }
    }

    private static Mock<IOrgDataScopeCandidateFactReader> Reader(IReadOnlyList<Guid> result)
    {
        var reader = new Mock<IOrgDataScopeCandidateFactReader>();
        reader.Setup(x => x.ResolveLegalEntityIdsAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return reader;
    }

    private static OrgDataScopeCandidateResolver Resolver(
        IOrgDataScopeCandidateFactReader reader,
        IOrgDataScopeCandidateAvailabilityClassifier classifier) =>
        new(reader, new FixedTimeProvider(Now), classifier);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class NeverUnavailableClassifier : IOrgDataScopeCandidateAvailabilityClassifier
    {
        public static NeverUnavailableClassifier Instance { get; } = new();
        public bool IsUnavailable(Exception exception) => false;
    }
}
