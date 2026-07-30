using System.Reflection;
using Diten.PpmService.Application.Common;
using Diten.PpmService.Application.Features.Portfolios;
using Diten.PpmService.Application.Features.Projects;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Exceptions;
using Diten.PpmService.Domain.Repositories;
using Diten.PpmService.Persistence;
using Diten.PpmService.Persistence.Mongo;
using Diten.PpmService.Persistence.Repositories;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Driver;
using Xunit;

namespace Diten.PpmService.IntegrationTests;

public sealed class MongoPersistenceIntegrationTests
{
    private static readonly string ReplicaSetConnection =
        Environment.GetEnvironmentVariable("PPM_TEST_MONGO_REPLICA_SET")
        ?? "mongodb://127.0.0.1:27018/?replicaSet=ppm-rs&serverSelectionTimeoutMS=5000";

    private static readonly string StandaloneConnection =
        Environment.GetEnvironmentVariable("PPM_TEST_MONGO_STANDALONE")
        ?? "mongodb://127.0.0.1:27019/?directConnection=true&serverSelectionTimeoutMS=5000";

    [Fact]
    public async Task Replica_set_readiness_is_healthy()
    {
        var database = CreateDatabase(ReplicaSetConnection);
        var health = new PpmMongoTransactionHealthCheck(database);

        var result = await health.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task Entity_mutation_and_audit_intent_commit_atomically()
    {
        var fixture = await Fixture.Create();
        var entity = fixture.NewPortfolio("P-ATOMIC");
        var intent = fixture.Intent(entity, "created");

        await fixture.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await fixture.Portfolios.AddAsync(entity, ct);
            await fixture.Audit.AddAsync(intent, ct);
            return true;
        }, default);

        Assert.NotNull(await fixture.Portfolios.GetByIdAsync(fixture.TenantId, entity.Id, default));
        Assert.Equal(
            1,
            await fixture.Context.AuditIntents.CountDocumentsAsync(
                item => item.TenantId == fixture.TenantId && item.EntityId == entity.Id));
    }

    [Fact]
    public async Task Audit_intent_failure_rolls_back_entity_mutation()
    {
        var fixture = await Fixture.Create();
        var entity = fixture.NewPortfolio("P-ROLLBACK");
        var invalidIntent = fixture.Intent(entity, "created") with { ActorId = Guid.Empty };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.UnitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await fixture.Portfolios.AddAsync(entity, ct);
                await fixture.Audit.AddAsync(invalidIntent, ct);
                return true;
            }, default));

        Assert.Null(await fixture.Portfolios.GetByIdAsync(fixture.TenantId, entity.Id, default));
        Assert.Equal(
            0,
            await fixture.Context.AuditIntents.CountDocumentsAsync(
                item => item.TenantId == fixture.TenantId && item.EntityId == entity.Id));
    }

    [Fact]
    public async Task Duplicate_normalized_code_returns_409()
    {
        var fixture = await Fixture.Create();
        var service = fixture.PortfolioService();

        var first = await service.Create(new("Cafe\u0301", "First", null, null), default);
        var duplicate = await service.Create(new("Café", "Second", null, null), default);

        Assert.Equal(201, first.StatusCode);
        Assert.Equal(409, duplicate.StatusCode);
        Assert.Single(await fixture.Portfolios.ListAsync(fixture.TenantId, default));
    }

    [Fact]
    public async Task Stale_version_is_rejected_with_concurrency_contract()
    {
        var fixture = await Fixture.Create();
        var service = fixture.PortfolioService();
        var created = await service.Create(new("P-STALE", "Original", null, null), default);
        var id = Assert.IsType<PortfolioDto>(created.Data).Id;

        var current = await fixture.Portfolios.GetByIdAsync(fixture.TenantId, id, default);
        Assert.NotNull(current);
        current.Update(fixture.ActorId, "P-STALE", "Concurrent", null, null);
        await fixture.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await fixture.Portfolios.ReplaceAsync(current, 1, ct);
            await fixture.Audit.AddAsync(fixture.Intent(current, "updated"), ct);
            return true;
        }, default);

        await Assert.ThrowsAsync<OptimisticConcurrencyException>(() =>
            service.Update(new(id, "P-STALE", "Stale", null, null, 1), default));
    }

    [Fact]
    public async Task Cross_tenant_entity_is_hidden_with_404()
    {
        var fixture = await Fixture.Create();
        var created = await fixture.PortfolioService()
            .Create(new("P-TENANT", "Tenant A", null, null), default);
        var id = Assert.IsType<PortfolioDto>(created.Data).Id;
        var otherTenantService = fixture.PortfolioService(Guid.NewGuid());

        var response = await otherTenantService.GetById(new(id), default);

        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Project_accepts_exactly_one_existing_initiative_or_program_parent()
    {
        var fixture = await Fixture.Create();
        var initiative = new Initiative(
            fixture.TenantId, fixture.ActorId, "I-PARENT", "Initiative", null, null, null);
        var program = new Diten.PpmService.Domain.Entities.Program(
            fixture.TenantId, fixture.ActorId, "PG-PARENT", "Program", null, null, null);

        await fixture.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await fixture.Initiatives.AddAsync(initiative, ct);
            await fixture.Programs.AddAsync(program, ct);
            await fixture.Audit.AddAsync(
                new AuditIntent(Guid.NewGuid(), fixture.TenantId, fixture.ActorId,
                    nameof(Initiative), initiative.Id, "created", DateTime.UtcNow), ct);
            await fixture.Audit.AddAsync(
                new AuditIntent(Guid.NewGuid(), fixture.TenantId, fixture.ActorId,
                    nameof(Diten.PpmService.Domain.Entities.Program), program.Id, "created",
                    DateTime.UtcNow), ct);
            return true;
        }, default);

        var projectService = fixture.ProjectService();
        var underInitiative = await projectService.Create(
            new("PRJ-I", "Under initiative", null, ProjectParentType.Initiative,
                initiative.Id, null), default);
        var underProgram = await projectService.Create(
            new("PRJ-P", "Under program", null, ProjectParentType.Program,
                program.Id, null), default);

        Assert.Equal(201, underInitiative.StatusCode);
        Assert.Equal(201, underProgram.StatusCode);
        Assert.Equal(ProjectParentType.Initiative, underInitiative.Data!.ParentType);
        Assert.Equal(ProjectParentType.Program, underProgram.Data!.ParentType);
    }

    [Fact]
    public async Task Project_rejects_missing_parent_and_contract_has_no_two_parent_shape()
    {
        var fixture = await Fixture.Create();
        var noParent = await fixture.ProjectService().Create(
            new("PRJ-NONE", "No parent", null, ProjectParentType.Initiative,
                Guid.Empty, null), default);
        var invalidKind = await fixture.ProjectService().Create(
            new("PRJ-KIND", "Invalid kind", null, (ProjectParentType)99,
                Guid.NewGuid(), null), default);
        var properties = typeof(CreateProjectCommand)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .ToArray();

        Assert.Equal(404, noParent.StatusCode);
        Assert.Equal(404, invalidKind.StatusCode);
        Assert.Contains(nameof(CreateProjectCommand.ParentType), properties);
        Assert.Contains(nameof(CreateProjectCommand.ParentId), properties);
        Assert.DoesNotContain("InitiativeId", properties);
        Assert.DoesNotContain("ProgramId", properties);
    }

    [Fact]
    public async Task Standalone_mongo_is_unhealthy_and_mutation_fails_as_503_contract()
    {
        var database = CreateDatabase(StandaloneConnection);
        var context = new PpmMongoContext(database.Client, database);
        var health = await new PpmMongoTransactionHealthCheck(database)
            .CheckHealthAsync(new HealthCheckContext());
        var repository = new PortfolioRepository(context);
        var unitOfWork = new PpmUnitOfWork(context);
        var entity = new Portfolio(
            Guid.NewGuid(), Guid.NewGuid(), "P-NO-TXN", "No transaction", null, null);

        var exception = await Assert.ThrowsAsync<TransactionUnavailableException>(() =>
            unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await repository.AddAsync(entity, ct);
                return true;
            }, default));

        Assert.Equal(HealthStatus.Unhealthy, health.Status);
        Assert.Contains("transaction", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(await repository.GetByIdAsync(entity.TenantId, entity.Id, default));
    }

    private static IMongoDatabase CreateDatabase(string connectionString) =>
        new MongoClient(connectionString).GetDatabase($"ppm_integration_{Guid.NewGuid():N}");

    private sealed class Fixture
    {
        private Fixture(PpmMongoContext context)
        {
            Context = context;
            UnitOfWork = new PpmUnitOfWork(context);
            Portfolios = new PortfolioRepository(context);
            Initiatives = new InitiativeRepository(context);
            Programs = new ProgramRepository(context);
            Projects = new ProjectRepository(context);
            Audit = new AuditIntentRepository(context);
        }

        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid ActorId { get; } = Guid.NewGuid();
        public PpmMongoContext Context { get; }
        public PpmUnitOfWork UnitOfWork { get; }
        public PortfolioRepository Portfolios { get; }
        public InitiativeRepository Initiatives { get; }
        public ProgramRepository Programs { get; }
        public ProjectRepository Projects { get; }
        public AuditIntentRepository Audit { get; }

        public static async Task<Fixture> Create()
        {
            var database = CreateDatabase(ReplicaSetConnection);
            var fixture = new Fixture(new PpmMongoContext(database.Client, database));
            await new PpmMongoIndexInitializer(database).StartAsync(default);
            return fixture;
        }

        public Portfolio NewPortfolio(string code) =>
            new(TenantId, ActorId, code, code, null, null);

        public AuditIntent Intent(EntityBase entity, string mutation) =>
            new(Guid.NewGuid(), TenantId, ActorId, entity.GetType().Name, entity.Id,
                mutation, DateTime.UtcNow);

        public PortfolioService PortfolioService(Guid? tenantId = null) =>
            new(Portfolios, Audit, UnitOfWork,
                new RequestContext(tenantId ?? TenantId, ActorId),
                new RequestContext(tenantId ?? TenantId, ActorId),
                new PermissionEvaluator());

        public ProjectService ProjectService() =>
            new(Projects, Initiatives, Programs, Audit, UnitOfWork,
                new RequestContext(TenantId, ActorId),
                new RequestContext(TenantId, ActorId),
                new PermissionEvaluator());
    }

    private sealed record RequestContext(Guid TenantId, Guid ActorId)
        : ITenantContext, ICurrentActorContext;

    private sealed class PermissionEvaluator : IEffectivePermissionEvaluator, IPpmAccessAuthorizer
    {
        public Task<bool> HasPermissionAsync(
            string permission,
            CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<PpmAccessDecision> AuthorizeAsync(
            string permission,
            CancellationToken cancellationToken) =>
            Task.FromResult(PpmAccessDecision.Allowed);
    }
}
