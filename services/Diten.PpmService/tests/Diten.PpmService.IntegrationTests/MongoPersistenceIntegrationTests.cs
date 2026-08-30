using System.Reflection;
using Diten.PpmService.Application.Common;
using Diten.PpmService.Application.Features.Portfolios;
using Diten.PpmService.Application.Features.Projects;
using Diten.PpmService.Application.Features.ExternalContextReferences;
using Diten.PpmService.Application.Features.InvestmentCases;
using Diten.PpmService.Application.Features.BenefitCommitments;
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

[Collection(PpmMongoCollection.CollectionName)]
public sealed class MongoPersistenceIntegrationTests
{
    private static string _replicaSetConnection = string.Empty;
    private readonly PpmDisposableMongo _mongo;

    public MongoPersistenceIntegrationTests(PpmDisposableMongo mongo)
    {
        _mongo = mongo;
        _replicaSetConnection = mongo.ReplicaSetConnectionString;
    }

    [Fact]
    public async Task Replica_set_readiness_is_healthy()
    {
        var database = PpmMongoTestDatabase.Open(_mongo.ReplicaSetConnectionString);
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
                new AuditIntent(Guid.NewGuid(), fixture.TenantId, fixture.ActorId, Guid.NewGuid(),
                    nameof(Initiative), initiative.Id, "created", DateTime.UtcNow), ct);
            await fixture.Audit.AddAsync(
                new AuditIntent(Guid.NewGuid(), fixture.TenantId, fixture.ActorId, Guid.NewGuid(),
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
    public async Task External_context_lookup_is_tenant_first_soft_delete_and_visibility_fail_closed()
    {
        var fixture = await Fixture.Create();
        var portfolio = new Portfolio(fixture.TenantId, fixture.ActorId, "P-REF", "Portfolio", null, null);
        var initiative = new Initiative(fixture.TenantId, fixture.ActorId, "I-REF", "Initiative", null, null, null);
        var program = new Diten.PpmService.Domain.Entities.Program(fixture.TenantId, fixture.ActorId, "PG-REF", "Program", null, null, null);
        var project = new Project(fixture.TenantId, fixture.ActorId, "PRJ-REF", "Project", null, ProjectParentType.Initiative, initiative.Id, null);
        var restricted = new Portfolio(fixture.TenantId, fixture.ActorId, "P-POLICY", "Restricted", null, "policy-v1");

        await fixture.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await fixture.Portfolios.AddAsync(portfolio, ct);
            await fixture.Initiatives.AddAsync(initiative, ct);
            await fixture.Programs.AddAsync(program, ct);
            await fixture.Projects.AddAsync(project, ct);
            await fixture.Portfolios.AddAsync(restricted, ct);
            return true;
        }, default);

        var lookup = new ExternalContextReferenceLookup(
            fixture.Portfolios, fixture.Initiatives, fixture.Programs, fixture.Projects);

        Assert.True((await lookup.FindAsync(fixture.TenantId, "Portfolio", portfolio.Id, default))?.IsReferenceable);
        Assert.True((await lookup.FindAsync(fixture.TenantId, "Initiative", initiative.Id, default))?.IsReferenceable);
        Assert.True((await lookup.FindAsync(fixture.TenantId, "Program", program.Id, default))?.IsReferenceable);
        Assert.True((await lookup.FindAsync(fixture.TenantId, "Project", project.Id, default))?.IsReferenceable);
        Assert.Null(await lookup.FindAsync(Guid.NewGuid(), "Portfolio", portfolio.Id, default));

        var restrictedResult = await lookup.FindAsync(fixture.TenantId, "Portfolio", restricted.Id, default);
        Assert.Equal("policy-v1", restrictedResult?.VisibilityPolicyKey);

        portfolio.SoftDelete(fixture.ActorId);
        await fixture.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await fixture.Portfolios.ReplaceAsync(portfolio, 1, ct);
            return true;
        }, default);
        Assert.Null(await lookup.FindAsync(fixture.TenantId, "Portfolio", portfolio.Id, default));

        initiative.Transition(fixture.ActorId, InitiativeLifecycleState.Cancelled);
        await fixture.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await fixture.Initiatives.ReplaceAsync(initiative, 1, ct);
            return true;
        }, default);
        Assert.False((await lookup.FindAsync(fixture.TenantId, "Initiative", initiative.Id, default))?.IsReferenceable);
    }

    [Fact]
    public async Task Standalone_mongo_is_unhealthy_and_mutation_fails_as_503_contract()
    {
        var database = PpmMongoTestDatabase.Open(_mongo.StandaloneConnectionString);
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

    [Fact]
    public async Task Gate_l_parent_chain_and_audit_intents_commit_atomically()
    {
        var fixture = await Fixture.Create();
        var portfolio = fixture.NewPortfolio("P-GATE-L");
        portfolio.Transition(fixture.ActorId, PortfolioLifecycleState.Active);
        await fixture.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await fixture.Portfolios.AddAsync(portfolio, ct);
            await fixture.Audit.AddAsync(fixture.Intent(portfolio, "created"), ct);
            return true;
        }, default);

        var investment = await fixture.InvestmentCaseService().Create(
            new("IC-GATE-L", "Investment", null, portfolio.Id, null, null), default);
        var benefit = await fixture.BenefitCommitmentService().Create(
            new("BC-GATE-L", "Benefit", null, investment.Data!.Id, "Target", null), default);

        Assert.Equal(201, investment.StatusCode);
        Assert.Equal(201, benefit.StatusCode);
        Assert.Equal(investment.Data.Id, benefit.Data!.InvestmentCaseId);
        Assert.Equal(3, await fixture.Context.AuditIntents.CountDocumentsAsync(x => x.TenantId == fixture.TenantId));
    }

    [Fact]
    public async Task Gate_l_normalized_active_code_is_unique_and_cross_tenant_parent_is_404()
    {
        var fixture = await Fixture.Create();
        var portfolio = fixture.NewPortfolio("P-UNIQUE");
        portfolio.Transition(fixture.ActorId, PortfolioLifecycleState.Active);
        await fixture.UnitOfWork.ExecuteInTransactionAsync(async ct => { await fixture.Portfolios.AddAsync(portfolio, ct); return true; }, default);

        var first = await fixture.InvestmentCaseService().Create(new("Cafe\u0301", "First", null, portfolio.Id, null, null), default);
        var duplicate = await fixture.InvestmentCaseService().Create(new("Café", "Second", null, portfolio.Id, null, null), default);
        var crossTenant = await fixture.InvestmentCaseService(Guid.NewGuid()).Create(new("IC-X", "Cross", null, portfolio.Id, null, null), default);

        Assert.Equal(201, first.StatusCode);
        Assert.Equal(409, duplicate.StatusCode);
        Assert.Equal(404, crossTenant.StatusCode);
    }

    [Fact]
    public async Task Investment_soft_delete_is_blocked_by_same_tenant_active_benefit_without_cascade()
    {
        var fixture = await Fixture.Create();
        var portfolio = fixture.NewPortfolio("P-DELETE-GUARD");
        portfolio.Transition(fixture.ActorId, PortfolioLifecycleState.Active);
        await fixture.UnitOfWork.ExecuteInTransactionAsync(async ct => { await fixture.Portfolios.AddAsync(portfolio, ct); return true; }, default);
        var investment = await fixture.InvestmentCaseService().Create(new("IC-DELETE-GUARD", "Case", null, portfolio.Id, null, null), default);
        var benefit = await fixture.BenefitCommitmentService().Create(new("BC-DELETE-GUARD", "Benefit", null, investment.Data!.Id, "Target", null), default);

        var response = await fixture.InvestmentCaseService().SoftDelete(
            new(investment.Data.Id, investment.Data.Version), default);

        Assert.Equal(409, response.StatusCode);
        Assert.NotNull(await fixture.InvestmentCases.GetByIdAsync(fixture.TenantId, investment.Data.Id, default));
        Assert.NotNull(await fixture.BenefitCommitments.GetByIdAsync(fixture.TenantId, benefit.Data!.Id, default));
    }

    [Fact]
    public async Task Concurrent_gate_l_duplicate_code_has_one_winner_and_one_domain_conflict()
    {
        var fixture = await Fixture.Create();
        var first = new InvestmentCase(fixture.TenantId, fixture.ActorId, "RACE", "First", null, Guid.NewGuid(), null, null);
        var second = new InvestmentCase(fixture.TenantId, fixture.ActorId, "RACE", "Second", null, Guid.NewGuid(), null, null);
        async Task Write(InvestmentCase entity) => await fixture.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        { await fixture.InvestmentCases.AddAsync(entity, ct); await fixture.Audit.AddAsync(fixture.Intent(entity, "created"), ct); return true; }, default);

        var results = await Task.WhenAll(new[] { first, second }.Select(async entity =>
        { try { await Write(entity); return "ok"; } catch (OptimisticConcurrencyException) { return "conflict"; } }));

        Assert.Equal(1, results.Count(x => x == "ok"));
        Assert.Equal(1, results.Count(x => x == "conflict"));
    }

    [Fact]
    public async Task Gate_l_collection_fences_preserve_business_metadata()
    {
        var fixture = await Fixture.Create();
        var portfolio = fixture.NewPortfolio("P-FENCE");
        portfolio.Transition(fixture.ActorId, PortfolioLifecycleState.Active);
        await fixture.UnitOfWork.ExecuteInTransactionAsync(async ct => { await fixture.Portfolios.AddAsync(portfolio, ct); return true; }, default);
        var initialPortfolio = await fixture.Portfolios.GetByIdAsync(fixture.TenantId, portfolio.Id, default);
        var portfolioSnapshot = (initialPortfolio!.Version, initialPortfolio.UpdatedAtUtc, initialPortfolio.UpdatedBy);
        var investment = await fixture.InvestmentCaseService().Create(new("IC-FENCE", "Case", null, portfolio.Id, null, null), default);
        var persistedPortfolio = await fixture.Portfolios.GetByIdAsync(fixture.TenantId, portfolio.Id, default);
        Assert.Equal(portfolioSnapshot, (persistedPortfolio!.Version, persistedPortfolio.UpdatedAtUtc, persistedPortfolio.UpdatedBy));
        Assert.Equal(1, persistedPortfolio.InvestmentCaseCollectionFence);
        var investmentEntity = await fixture.InvestmentCases.GetByIdAsync(fixture.TenantId, investment.Data!.Id, default);
        var investmentSnapshot = (investmentEntity!.Version, investmentEntity.UpdatedAtUtc, investmentEntity.UpdatedBy);
        var benefit = await fixture.BenefitCommitmentService().Create(new("BC-FENCE", "Benefit", null, investmentEntity.Id, "Target", null), default);
        var persistedInvestment = await fixture.InvestmentCases.GetByIdAsync(fixture.TenantId, investmentEntity.Id, default);
        Assert.Equal(investmentSnapshot, (persistedInvestment!.Version, persistedInvestment.UpdatedAtUtc, persistedInvestment.UpdatedBy));
        Assert.Equal(1, persistedInvestment.BenefitCommitmentCollectionFence);
        Assert.Equal(201, benefit.StatusCode);
    }

    [Fact]
    public async Task Portfolio_delete_and_investment_create_never_commit_an_orphan()
    {
        var fixture = await Fixture.Create();
        var portfolio = fixture.NewPortfolio("P-RACE-FENCE");
        portfolio.Transition(fixture.ActorId, PortfolioLifecycleState.Active);
        await fixture.UnitOfWork.ExecuteInTransactionAsync(async ct => { await fixture.Portfolios.AddAsync(portfolio, ct); return true; }, default);
        async Task<int> Delete() { try { return (await fixture.PortfolioService().SoftDelete(new(portfolio.Id, portfolio.Version), default)).StatusCode; } catch (OptimisticConcurrencyException) { return 409; } }
        async Task<int> Create() { try { return (await fixture.InvestmentCaseService().Create(new("IC-RACE-FENCE", "Case", null, portfolio.Id, null, null), default)).StatusCode; } catch (OptimisticConcurrencyException) { return 409; } }
        var results = await Task.WhenAll(Delete(), Create());
        var parent = await fixture.Portfolios.GetByIdAsync(fixture.TenantId, portfolio.Id, default);
        var children = await fixture.InvestmentCases.ListAsync(fixture.TenantId, default);
        Assert.False(results[0] is 204 && results[1] is 201);
        Assert.False(parent is null && children.Any(x => x.PortfolioId == portfolio.Id));
    }

    [Fact]
    public async Task Investment_delete_and_benefit_create_never_commit_an_orphan()
    {
        var fixture = await Fixture.Create();
        var portfolio = fixture.NewPortfolio("P-BC-RACE");
        portfolio.Transition(fixture.ActorId, PortfolioLifecycleState.Active);
        await fixture.UnitOfWork.ExecuteInTransactionAsync(async ct => { await fixture.Portfolios.AddAsync(portfolio, ct); return true; }, default);
        var investment = await fixture.InvestmentCaseService().Create(new("IC-BC-RACE", "Case", null, portfolio.Id, null, null), default);
        async Task<int> Delete() { try { return (await fixture.InvestmentCaseService().SoftDelete(new(investment.Data!.Id, investment.Data.Version), default)).StatusCode; } catch (OptimisticConcurrencyException) { return 409; } }
        async Task<int> Create() { try { return (await fixture.BenefitCommitmentService().Create(new("BC-RACE", "Benefit", null, investment.Data!.Id, "Target", null), default)).StatusCode; } catch (OptimisticConcurrencyException) { return 409; } }
        var results = await Task.WhenAll(Delete(), Create());
        var parent = await fixture.InvestmentCases.GetByIdAsync(fixture.TenantId, investment.Data!.Id, default);
        var children = await fixture.BenefitCommitments.ListAsync(fixture.TenantId, default);
        Assert.False(results[0] is 204 && results[1] is 201);
        Assert.False(parent is null && children.Any(x => x.InvestmentCaseId == investment.Data.Id));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Gate_l_invalid_audit_intent_rolls_back_new_collection_mutation(bool investmentCase)
    {
        var fixture = await Fixture.Create();
        EntityBase entity = investmentCase
            ? new InvestmentCase(fixture.TenantId, fixture.ActorId, "IC-AUDIT-FAIL", "Case", null, Guid.NewGuid(), null, null)
            : new BenefitCommitment(fixture.TenantId, fixture.ActorId, "BC-AUDIT-FAIL", "Benefit", null, Guid.NewGuid(), "Target", null);
        var invalid = fixture.Intent(entity, "created") with { ActorId = Guid.Empty };

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (entity is InvestmentCase ic) await fixture.InvestmentCases.AddAsync(ic, ct);
            else await fixture.BenefitCommitments.AddAsync((BenefitCommitment)entity, ct);
            await fixture.Audit.AddAsync(invalid, ct);
            return true;
        }, default));

        if (entity is InvestmentCase)
            Assert.Null(await fixture.InvestmentCases.GetByIdAsync(fixture.TenantId, entity.Id, default));
        else
            Assert.Null(await fixture.BenefitCommitments.GetByIdAsync(fixture.TenantId, entity.Id, default));
    }

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
            InvestmentCases = new InvestmentCaseRepository(context);
            BenefitCommitments = new BenefitCommitmentRepository(context);
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
        public InvestmentCaseRepository InvestmentCases { get; }
        public BenefitCommitmentRepository BenefitCommitments { get; }
        public AuditIntentRepository Audit { get; }

        public static async Task<Fixture> Create()
        {
            var database = PpmMongoTestDatabase.Open(_replicaSetConnection);
            await PpmMongoTestDatabase.ResetAsync(database);
            var fixture = new Fixture(new PpmMongoContext(database.Client, database));
            await new PpmMongoIndexInitializer(database).StartAsync(default);
            return fixture;
        }

        public Portfolio NewPortfolio(string code) =>
            new(TenantId, ActorId, code, code, null, null);

        public AuditIntent Intent(EntityBase entity, string mutation) =>
            new(Guid.NewGuid(), TenantId, ActorId, Guid.NewGuid(), entity.GetType().Name, entity.Id,
                mutation, DateTime.UtcNow);

        public PortfolioService PortfolioService(Guid? tenantId = null) =>
            new(Portfolios, Audit, UnitOfWork,
                new RequestContext(tenantId ?? TenantId, ActorId),
                new RequestContext(tenantId ?? TenantId, ActorId),
                new CorrelationContext(),
                new PermissionEvaluator(), InvestmentCases);

        public ProjectService ProjectService() =>
            new(Projects, Initiatives, Programs, Audit, UnitOfWork,
                new RequestContext(TenantId, ActorId),
                new RequestContext(TenantId, ActorId),
                new CorrelationContext(),
                new PermissionEvaluator());

        public InvestmentCaseService InvestmentCaseService(Guid? tenantId = null) =>
            new(InvestmentCases, Portfolios, BenefitCommitments, Audit, UnitOfWork,
                new RequestContext(tenantId ?? TenantId, ActorId),
                new RequestContext(tenantId ?? TenantId, ActorId),
                new CorrelationContext(), new PermissionEvaluator());

        public BenefitCommitmentService BenefitCommitmentService() =>
            new(BenefitCommitments, InvestmentCases, Audit, UnitOfWork,
                new RequestContext(TenantId, ActorId), new RequestContext(TenantId, ActorId),
                new CorrelationContext(), new PermissionEvaluator());
    }

    private sealed record RequestContext(Guid TenantId, Guid ActorId)
        : ITenantContext, ICurrentActorContext;
    private sealed class CorrelationContext : ICorrelationContext
    {
        public Guid CorrelationId { get; } = Guid.NewGuid();
    }

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
