using Diten.PpmService.Application.Common;
using Diten.PpmService.Application.Features.Portfolios;
using Diten.PpmService.Application.Features.Projects;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Exceptions;
using Diten.PpmService.Domain.Repositories;
using Diten.PpmService.Infrastructure;
using Diten.PpmService.Infrastructure.Authorization;
using Diten.Platform.Common.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Diten.PpmService.Tests;

public sealed class ApplicationContractTests
{
    [Fact]
    public async Task Missing_permission_fails_closed_with_403_before_repository_access()
    {
        var repository = new PortfolioRepository();
        var service = new PortfolioService(
            repository, new AuditRepository(), new UnitOfWork(),
            new RequestContext(Guid.NewGuid(), Guid.NewGuid()),
            new RequestContext(Guid.NewGuid(), Guid.NewGuid()),
            new FixedCorrelation(),
            new PermissionEvaluator(false));

        var response = await service.Create(new("P-1", "Portfolio", null, null), default);

        Assert.Equal(403, response.StatusCode);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task Entitlement_dependency_failure_returns_503_before_repository_access()
    {
        var repository = new PortfolioRepository();
        var service = new PortfolioService(
            repository, new AuditRepository(), new UnitOfWork(),
            new RequestContext(Guid.NewGuid(), Guid.NewGuid()),
            new RequestContext(Guid.NewGuid(), Guid.NewGuid()),
            new FixedCorrelation(),
            new FixedAccessAuthorizer(PpmAccessDecision.DependencyUnavailable));

        var response = await service.Create(new("P-1", "Portfolio", null, null), default);

        Assert.Equal(503, response.StatusCode);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task Denied_read_does_not_probe_repository_or_disclose_existence()
    {
        var repository = new PortfolioRepository();
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var entity = new Portfolio(tenant, actor, "P-1", "Portfolio", null, null);
        repository.Items.Add(entity);
        var service = new PortfolioService(
            repository, new AuditRepository(), new UnitOfWork(),
            new RequestContext(tenant, actor), new RequestContext(tenant, actor),
            new FixedCorrelation(),
            new PermissionEvaluator(false));

        var existing = await service.GetById(new(entity.Id), default);
        var missing = await service.GetById(new(Guid.NewGuid()), default);

        Assert.Equal(403, existing.StatusCode);
        Assert.Equal(403, missing.StatusCode);
        Assert.Equal(0, repository.Reads);
    }

    [Fact]
    public async Task Shared_permission_adapter_consumes_authenticated_claim_result()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim("tenant_id", Guid.NewGuid().ToString()),
            new Claim("permission", PpmPermissions.PortfoliosCreate)
        ], "Bearer"));
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        var evaluator = new SharedPermissionClaimEvaluatorAdapter(
            accessor,
            new SignedJwtPermissionClaimEvaluator());

        Assert.True(await evaluator.HasPermissionAsync(PpmPermissions.PortfoliosCreate, default));
        Assert.False(await evaluator.HasPermissionAsync(PpmPermissions.ProjectsCreate, default));
    }

    [Theory]
    [InlineData("PPM.PORTFOLIOS.CREATE")]
    [InlineData(" ppm.portfolios.create")]
    [InlineData("ppm.portfolios.create ")]
    [InlineData("ppm.portfolios.*")]
    [InlineData("ppm.portfolios.archive")]
    public async Task Permission_matching_rejects_case_whitespace_wildcard_and_alias(string claim)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim("tenant_id", Guid.NewGuid().ToString()),
            new Claim("permission", claim)
        ], "Bearer"));
        var evaluator = new SharedPermissionClaimEvaluatorAdapter(
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { User = principal }
            },
            new SignedJwtPermissionClaimEvaluator());

        Assert.False(await evaluator.HasPermissionAsync(
            PpmPermissions.PortfoliosCreate,
            default));
    }

    [Fact]
    public void Production_registration_uses_shared_permission_adapter()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure();

        var descriptor = Assert.Single(
            services,
            item => item.ServiceType == typeof(IEffectivePermissionEvaluator));
        Assert.Equal(typeof(SharedPermissionClaimEvaluatorAdapter), descriptor.ImplementationType);
        Assert.Contains(
            services,
            item => item.ServiceType == typeof(IPermissionClaimEvaluator) &&
                    item.ImplementationType == typeof(SignedJwtPermissionClaimEvaluator));
    }

    [Fact]
    public void Permission_catalog_is_closed_unique_and_canonical()
    {
        var actual = typeof(PpmPermissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
            .Select(field => Assert.IsType<string>(field.GetRawConstantValue()))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var expected = new[]
        {
            "ppm.initiatives.change-lifecycle",
            "ppm.initiatives.create",
            "ppm.initiatives.read",
            "ppm.initiatives.update",
            "ppm.investment-cases.change-lifecycle",
            "ppm.investment-cases.create",
            "ppm.investment-cases.read",
            "ppm.investment-cases.update",
            "ppm.benefit-commitments.change-lifecycle",
            "ppm.benefit-commitments.create",
            "ppm.benefit-commitments.read",
            "ppm.benefit-commitments.update",
            "ppm.portfolios.change-lifecycle",
            "ppm.portfolios.create",
            "ppm.portfolios.read",
            "ppm.portfolios.update",
            "ppm.programs.change-lifecycle",
            "ppm.programs.create",
            "ppm.programs.read",
            "ppm.programs.update",
            "ppm.projects.change-lifecycle",
            "ppm.projects.create",
            "ppm.projects.read",
            "ppm.projects.update"
        };

        Assert.Equal(expected.OrderBy(value => value, StringComparer.Ordinal), actual);
        Assert.Equal(actual.Length, actual.Distinct(StringComparer.Ordinal).Count());
        Assert.All(actual, permission =>
            Assert.Matches("^ppm\\.[a-z-]+\\.[a-z-]+$", permission));
    }

    [Fact]
    public async Task Tenant_scoped_lookup_returns_404_for_cross_tenant_entity()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var repository = new PortfolioRepository();
        repository.Items.Add(new Portfolio(tenantA, actor, "P-1", "Portfolio", null, null));
        var service = new PortfolioService(
            repository, new AuditRepository(), new UnitOfWork(),
            new RequestContext(tenantB, actor), new RequestContext(tenantB, actor),
            new FixedCorrelation(),
            new PermissionEvaluator(true));

        var response = await service.GetById(new(repository.Items[0].Id), default);

        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Project_parent_lookup_is_tenant_scoped_and_cross_tenant_is_404()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var parents = new InitiativeRepository();
        var parent = new Initiative(tenantA, actor, "I-1", "Initiative", null, null, null);
        parents.Items.Add(parent);
        var service = new ProjectService(
            new ProjectRepository(), parents, new ProgramRepository(),
            new AuditRepository(), new UnitOfWork(),
            new RequestContext(tenantB, actor), new RequestContext(tenantB, actor),
            new FixedCorrelation(),
            new PermissionEvaluator(true));

        var response = await service.Create(
            new("PRJ-1", "Project", null, ProjectParentType.Initiative, parent.Id, null), default);

        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Mutation_and_audit_are_one_unit_and_roll_back_when_audit_fails()
    {
        var repository = new PortfolioRepository();
        var unit = new UnitOfWork();
        var service = new PortfolioService(
            repository, new AuditRepository { ThrowOnAdd = true }, unit,
            new RequestContext(Guid.NewGuid(), Guid.NewGuid()),
            new RequestContext(Guid.NewGuid(), Guid.NewGuid()),
            new FixedCorrelation(),
            new PermissionEvaluator(true));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.Create(new("P-1", "Portfolio", null, null), default));

        Assert.True(unit.RolledBack);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task Optimistic_concurrency_mismatch_is_rejected()
    {
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var repository = new PortfolioRepository();
        var entity = new Portfolio(tenant, actor, "P-1", "Portfolio", null, null);
        repository.Items.Add(entity);
        var service = new PortfolioService(
            repository, new AuditRepository(), new UnitOfWork(),
            new RequestContext(tenant, actor), new RequestContext(tenant, actor),
            new FixedCorrelation(),
            new PermissionEvaluator(true));

        await Assert.ThrowsAsync<OptimisticConcurrencyException>(() =>
            service.Update(new(entity.Id, "P-1", "Changed", null, null, 99), default));
    }

    [Fact]
    public void Invalid_command_contract_is_rejected_by_validation()
    {
        var validator = new CreateProjectValidator();
        var command = new CreateProjectCommand(
            "", "", null, (ProjectParentType)99, Guid.Empty, "unvalidated-policy");

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateProjectCommand.Code));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateProjectCommand.Name));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateProjectCommand.ParentType));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateProjectCommand.ParentId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateProjectCommand.VisibilityPolicyKey));
    }

    [Fact]
    public void Api_enum_contract_uses_names_and_rejects_integer_values()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));

        Assert.Equal("\"OnHold\"", JsonSerializer.Serialize(ProjectLifecycleState.OnHold, options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProjectLifecycleState>("2", options));
    }

    private sealed record RequestContext(Guid TenantId, Guid ActorId) : ITenantContext, ICurrentActorContext;
    private sealed record FixedCorrelation : ICorrelationContext
    {
        public Guid CorrelationId { get; } = Guid.NewGuid();
    }
    private sealed class PermissionEvaluator(bool allowed)
        : IEffectivePermissionEvaluator, IPpmAccessAuthorizer
    {
        public Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken) =>
            Task.FromResult(allowed);

        public Task<PpmAccessDecision> AuthorizeAsync(
            string permission,
            CancellationToken cancellationToken) =>
            Task.FromResult(allowed ? PpmAccessDecision.Allowed : PpmAccessDecision.Forbidden);
    }

    private sealed class FixedAccessAuthorizer(PpmAccessDecision decision) : IPpmAccessAuthorizer
    {
        public Task<PpmAccessDecision> AuthorizeAsync(
            string permission,
            CancellationToken cancellationToken) => Task.FromResult(decision);
    }

    private sealed class AuditRepository : IAuditIntentRepository
    {
        public bool ThrowOnAdd { get; init; }
        public Task AddAsync(AuditIntent intent, CancellationToken cancellationToken) =>
            ThrowOnAdd ? throw new InvalidOperationException("audit failed") : Task.CompletedTask;
    }

    private sealed class UnitOfWork : IPpmUnitOfWork
    {
        public bool RolledBack { get; private set; }
        public async Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
        {
            var repositories = MemoryRepositoryRegistry.Repositories.ToArray();
            var snapshots = repositories.ToDictionary(x => x, x => x.Snapshot());
            try { return await operation(cancellationToken); }
            catch
            {
                foreach (var repository in repositories) repository.Restore(snapshots[repository]);
                RolledBack = true;
                throw;
            }
        }
    }

    private interface IMemoryRepository
    {
        object Snapshot();
        void Restore(object snapshot);
    }

    private static class MemoryRepositoryRegistry
    {
        public static List<IMemoryRepository> Repositories { get; } = [];
    }

    private abstract class MemoryRepository<T> : IRepository<T>, IMemoryRepository
        where T : EntityBase
    {
        public List<T> Items { get; } = [];
        public int Reads { get; private set; }
        public MemoryRepository() => MemoryRepositoryRegistry.Repositories.Add(this);
        public Task<T?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(Items.SingleOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));
        }
        public Task<IReadOnlyList<T>> ListAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<T>>(Items.Where(x => x.TenantId == tenantId && !x.IsDeleted).ToArray());
        public Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, Guid? excludingId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(x => x.TenantId == tenantId && !x.IsDeleted && x.Id != excludingId &&
                                           (string)x.GetType().GetProperty("Code")!.GetValue(x)! == normalizedCode));
        public Task AddAsync(T entity, CancellationToken cancellationToken) { Items.Add(entity); return Task.CompletedTask; }
        public Task ReplaceAsync(T entity, int expectedVersion, CancellationToken cancellationToken)
        {
            if (entity.Version != expectedVersion + 1) throw new OptimisticConcurrencyException("version mismatch");
            return Task.CompletedTask;
        }
        public object Snapshot() => Items.ToList();
        public void Restore(object snapshot) { Items.Clear(); Items.AddRange((List<T>)snapshot); }
    }

    private sealed class PortfolioRepository : MemoryRepository<Portfolio>, IPortfolioRepository
    { public Task AdvanceInvestmentCaseCollectionFenceAsync(Portfolio entity, CancellationToken cancellationToken) { entity.AdvanceInvestmentCaseCollectionFence(); return Task.CompletedTask; } }
    private sealed class InitiativeRepository : MemoryRepository<Initiative>, IInitiativeRepository;
    private sealed class ProgramRepository : MemoryRepository<Diten.PpmService.Domain.Entities.Program>, IProgramRepository;
    private sealed class ProjectRepository : MemoryRepository<Project>, IProjectRepository;
}
