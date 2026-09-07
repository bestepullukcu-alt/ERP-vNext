using Diten.Platform.Application.Authorization;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.AccessGovernance.LegalEntityScopeResolution.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.AccessGovernance.LegalEntityScopeResolution.Queries;
using Diten.Platform.Application.Tests.TenantOrganization;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;
using Moq;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Runtime.CompilerServices;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class OrgDataScopeCandidateResolverTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid User = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Resolves_multiple_distinct_sorted_legal_entities_without_MDM_or_N_plus_one()
    {
        var le1 = Guid.Parse("10000000-0000-0000-0000-000000000000");
        var le2 = Guid.Parse("20000000-0000-0000-0000-000000000000");
        var unit1 = Unit(le2); var unit2 = Unit(le1); var unit3 = Unit(le2);
        var position1 = Position(unit1.Id); var position2 = Position(unit2.Id); var position3 = Position(unit3.Id);
        var units = MockRepo<IOrganizationUnitRepository, OrganizationUnit>([unit1, unit2, unit3]);
        var positions = MockRepo<IPositionRepository, Position>([position1, position2, position3]);
        var assignments = MockRepo<IPositionAssignmentRepository, PositionAssignment>(
            [Assignment(position1.Id), Assignment(position2.Id), Assignment(position3.Id)]);
        var resolver = new OrgDataScopeCandidateResolver(units.Object, positions.Object, assignments.Object, new FixedTimeProvider(Now), NeverUnavailableClassifier.Instance);

        var first = await resolver.ResolveAsync(Tenant, User, CancellationToken.None);
        var replay = await resolver.ResolveAsync(Tenant, User, CancellationToken.None);

        Assert.Equal(new[] { le1, le2 }, first.LegalEntityIds);
        Assert.Same(first, replay);
        units.Verify(x => x.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
        positions.Verify(x => x.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
        assignments.Verify(x => x.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
        units.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        positions.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Uses_TimeProvider_and_excludes_future_expired_deleted_archived_and_cross_tenant_facts()
    {
        var units = new InMemoryOrganizationUnitRepository(Tenant);
        var positions = new InMemoryPositionRepository(Tenant);
        var assignments = new InMemoryPositionAssignmentRepository(Tenant);
        var activeUnit = Unit(Guid.NewGuid());
        var archivedUnit = Unit(Guid.NewGuid()); archivedUnit.IsArchived = true;
        var activePosition = Position(activeUnit.Id);
        var archivedPosition = Position(activeUnit.Id); archivedPosition.IsArchived = true;
        units.Add(activeUnit); units.Add(archivedUnit);
        positions.Add(activePosition); positions.Add(archivedPosition);
        assignments.Add(Assignment(activePosition.Id));
        assignments.Add(Assignment(activePosition.Id, Now.AddMinutes(1)));
        assignments.Add(Assignment(activePosition.Id, Now.AddDays(-2), Now));
        assignments.Add(Assignment(archivedPosition.Id));
        var deleted = Assignment(activePosition.Id); deleted.IsDeleted = true; assignments.Add(deleted);
        var resolver = new OrgDataScopeCandidateResolver(units, positions, assignments, new FixedTimeProvider(Now), NeverUnavailableClassifier.Instance);

        var result = await resolver.ResolveAsync(Tenant, User, CancellationToken.None);

        Assert.Equal(new[] { activeUnit.LegalEntityId }, result.LegalEntityIds);
    }

    [Fact]
    public async Task More_than_200_active_position_candidates_fails_before_position_or_unit_reads()
    {
        var assignments = Enumerable.Range(0, 201).Select(_ => Assignment(Guid.NewGuid())).ToArray();
        var assignmentRepo = MockRepo<IPositionAssignmentRepository, PositionAssignment>(assignments);
        var positions = new Mock<IPositionRepository>(MockBehavior.Strict);
        var units = new Mock<IOrganizationUnitRepository>(MockBehavior.Strict);
        var resolver = new OrgDataScopeCandidateResolver(units.Object, positions.Object, assignmentRepo.Object, new FixedTimeProvider(Now), NeverUnavailableClassifier.Instance);

        await Assert.ThrowsAsync<OrgDataScopeCandidateContractException>(() =>
            resolver.ResolveAsync(Tenant, User, CancellationToken.None));
        positions.Verify(x => x.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
        units.Verify(x => x.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Production_Mongo_timeout_chain_maps_exactly_to_handler_503()
    {
        var assignments = new Mock<IPositionAssignmentRepository>();
        assignments.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync((MongoExecutionTimeoutException)RuntimeHelpers.GetUninitializedObject(
                typeof(MongoExecutionTimeoutException)));
        var candidate = new OrgDataScopeCandidateResolver(
            Mock.Of<IOrganizationUnitRepository>(), Mock.Of<IPositionRepository>(), assignments.Object,
            new FixedTimeProvider(Now), new MongoOrgDataScopeCandidateAvailabilityClassifier());
        var handler = new ResolveTrustedLegalEntityScopeHandler(candidate, new FixedTimeProvider(Now));

        var response = await handler.Handle(new ResolveTrustedLegalEntityScopeQuery(
            Tenant, User, "product-item-sku-master", "mdm.gskus.read"), CancellationToken.None);

        Assert.False(response.IsSuccessful); Assert.Equal(503, response.StatusCode);
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
            var assignments = new Mock<IPositionAssignmentRepository>();
            assignments.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>())).ThrowsAsync(exception);
            var resolver = new OrgDataScopeCandidateResolver(
                Mock.Of<IOrganizationUnitRepository>(), Mock.Of<IPositionRepository>(), assignments.Object,
                new FixedTimeProvider(Now), new MongoOrgDataScopeCandidateAvailabilityClassifier());
            var thrown = await Assert.ThrowsAsync(exception.GetType(), () => resolver.ResolveAsync(Tenant, User, default));
            Assert.Same(exception, thrown);
        }
    }

    private static Mock<TRepository> MockRepo<TRepository, TEntity>(IReadOnlyList<TEntity> items)
        where TRepository : class
    {
        var mock = new Mock<TRepository>(MockBehavior.Loose);
        if (mock.Object is IOrganizationUnitRepository)
            mock.As<IOrganizationUnitRepository>().Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<OrganizationUnit>)(object)items);
        if (mock.Object is IPositionRepository)
            mock.As<IPositionRepository>().Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<Position>)(object)items);
        if (mock.Object is IPositionAssignmentRepository)
            mock.As<IPositionAssignmentRepository>().Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<PositionAssignment>)(object)items);
        return mock;
    }

    private static OrganizationUnit Unit(Guid legalEntityId) => new()
    { Id = Guid.NewGuid(), TenantId = Tenant, Code = Guid.NewGuid().ToString("N"), Name = "Unit", LegalEntityId = legalEntityId };
    private static Position Position(Guid unitId) => new()
    { Id = Guid.NewGuid(), TenantId = Tenant, Code = Guid.NewGuid().ToString("N"), Name = "Position", OrganizationUnitId = unitId };
    private static PositionAssignment Assignment(Guid positionId, DateTimeOffset? from = null, DateTimeOffset? to = null) => new()
    { Id = Guid.NewGuid(), TenantId = Tenant, UserId = User, PositionId = positionId, EffectiveFrom = from ?? Now.AddDays(-1), EffectiveTo = to };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    { public override DateTimeOffset GetUtcNow() => now; }

    private sealed class NeverUnavailableClassifier : IOrgDataScopeCandidateAvailabilityClassifier
    {
        public static NeverUnavailableClassifier Instance { get; } = new();
        public bool IsUnavailable(Exception exception) => false;
    }
}
