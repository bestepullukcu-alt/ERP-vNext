using Diten.PpmService.Application.Common;
using Diten.PpmService.Application.Features.BenefitCommitments;
using Diten.PpmService.Application.Features.InvestmentCases;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Exceptions;
using Diten.PpmService.Domain.Repositories;
using Xunit;

namespace Diten.PpmService.Tests;

public sealed class GateLApplicationTests
{
    [Fact]
    public async Task Cross_tenant_investment_parent_is_indistinguishable_404()
    {
        var tenantA = Guid.NewGuid(); var tenantB = Guid.NewGuid(); var actor = Guid.NewGuid();
        var portfolios = new PortfolioRepo();
        var portfolio = new Portfolio(tenantA, actor, "P", "Portfolio", null, null);
        portfolio.Transition(actor, PortfolioLifecycleState.Active); portfolios.Items.Add(portfolio);
        var service = InvestmentService(tenantB, actor, new InvestmentCaseRepo(), portfolios, new Audit(), new Unit());
        var response = await service.Create(new("IC", "Case", null, portfolio.Id, null, null), default);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Benefit_parent_is_tenant_scoped_and_terminal_parent_is_404()
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid();
        var cases = new InvestmentCaseRepo();
        var investment = new InvestmentCase(tenant, actor, "IC", "Case", null, Guid.NewGuid(), null, null);
        investment.Transition(actor, InvestmentCaseLifecycleState.Withdrawn); cases.Items.Add(investment);
        var service = BenefitService(tenant, actor, new BenefitCommitmentRepo(), cases, new Audit(), new Unit());
        var response = await service.Create(new("BC", "Benefit", null, investment.Id, "Target", null), default);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_lifecycle_returns_409_without_mutation()
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid();
        var repo = new BenefitCommitmentRepo();
        var entity = new BenefitCommitment(tenant, actor, "BC", "Benefit", null, Guid.NewGuid(), "Target", null);
        repo.Items.Add(entity);
        var response = await BenefitService(tenant, actor, repo, new InvestmentCaseRepo(), new Audit(), new Unit())
            .Transition(new(entity.Id, BenefitCommitmentLifecycleState.Closed, entity.Version), default);
        Assert.Equal(409, response.StatusCode); Assert.Equal(BenefitCommitmentLifecycleState.Draft, entity.LifecycleState);
    }

    [Fact]
    public async Task Audit_failure_rolls_back_investment_mutation()
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid();
        var portfolios = new PortfolioRepo();
        var portfolio = new Portfolio(tenant, actor, "P", "Portfolio", null, null);
        portfolio.Transition(actor, PortfolioLifecycleState.Active); portfolios.Items.Add(portfolio);
        var repo = new InvestmentCaseRepo(); var unit = new Unit(repo);
        var service = InvestmentService(tenant, actor, repo, portfolios, new Audit(true), unit);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Create(new("IC", "Case", null, portfolio.Id, null, null), default));
        Assert.True(unit.RolledBack); Assert.Empty(repo.Items);
    }

    [Fact]
    public async Task Permission_denial_occurs_before_parent_or_entity_lookup()
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid(); var repo = new InvestmentCaseRepo();
        var service = new InvestmentCaseService(repo, new PortfolioRepo(), new BenefitCommitmentRepo(), new Audit(), new Unit(),
            new Context(tenant, actor), new Context(tenant, actor), new Correlation(), new Access(false));
        var response = await service.Get(new(Guid.NewGuid()), default);
        Assert.Equal(403, response.StatusCode); Assert.Equal(0, repo.Reads);
    }

    [Fact]
    public async Task Investment_soft_delete_is_409_when_active_benefit_exists_and_does_not_cascade()
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid();
        var investments = new InvestmentCaseRepo();
        var investment = new InvestmentCase(tenant, actor, "IC", "Case", null, Guid.NewGuid(), null, null);
        investments.Items.Add(investment);
        var benefits = new BenefitCommitmentRepo();
        var benefit = new BenefitCommitment(tenant, actor, "BC", "Benefit", null, investment.Id, "Target", null);
        benefits.Items.Add(benefit);
        var service = new InvestmentCaseService(investments, new PortfolioRepo(), benefits, new Audit(), new Unit(investments, benefits),
            new Context(tenant, actor), new Context(tenant, actor), new Correlation(), new Access(true));

        var response = await service.SoftDelete(new(investment.Id, investment.Version), default);

        Assert.Equal(409, response.StatusCode);
        Assert.False(investment.IsDeleted);
        Assert.False(benefit.IsDeleted);
    }

    [Fact]
    public async Task Terminal_investment_and_benefit_reject_metadata_update_with_409()
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid();
        var investments = new InvestmentCaseRepo();
        var investment = new InvestmentCase(tenant, actor, "IC", "Case", null, Guid.NewGuid(), null, null);
        investment.Transition(actor, InvestmentCaseLifecycleState.Withdrawn); investments.Items.Add(investment);
        var benefits = new BenefitCommitmentRepo();
        var benefit = new BenefitCommitment(tenant, actor, "BC", "Benefit", null, investment.Id, "Target", null);
        benefit.Transition(actor, BenefitCommitmentLifecycleState.Cancelled); benefits.Items.Add(benefit);

        var investmentResponse = await new InvestmentCaseService(investments, new PortfolioRepo(), benefits, new Audit(), new Unit(),
            new Context(tenant, actor), new Context(tenant, actor), new Correlation(), new Access(true))
            .Update(new(investment.Id, "IC-2", "Changed", null, null, null, investment.Version), default);
        var benefitResponse = await BenefitService(tenant, actor, benefits, investments, new Audit(), new Unit())
            .Update(new(benefit.Id, "BC-2", "Changed", null, "Changed", null, benefit.Version), default);

        Assert.Equal(409, investmentResponse.StatusCode);
        Assert.Equal(409, benefitResponse.StatusCode);
        Assert.Equal("IC", investment.Code);
        Assert.Equal("BC", benefit.Code);
    }

    private static InvestmentCaseService InvestmentService(Guid tenant, Guid actor, InvestmentCaseRepo repo,
        PortfolioRepo parents, Audit audit, Unit unit) => new(repo, parents, new BenefitCommitmentRepo(), audit, unit,
        new Context(tenant, actor), new Context(tenant, actor), new Correlation(), new Access(true));
    private static BenefitCommitmentService BenefitService(Guid tenant, Guid actor, BenefitCommitmentRepo repo,
        InvestmentCaseRepo parents, Audit audit, Unit unit) => new(repo, parents, audit, unit,
        new Context(tenant, actor), new Context(tenant, actor), new Correlation(), new Access(true));

    private sealed record Context(Guid TenantId, Guid ActorId) : ITenantContext, ICurrentActorContext;
    private sealed class Correlation : ICorrelationContext { public Guid CorrelationId { get; } = Guid.NewGuid(); }
    private sealed class Access(bool allowed) : IPpmAccessAuthorizer { public Task<PpmAccessDecision> AuthorizeAsync(string p, CancellationToken c) => Task.FromResult(allowed ? PpmAccessDecision.Allowed : PpmAccessDecision.Forbidden); }
    private sealed class Audit(bool fail = false) : IAuditIntentRepository { public Task AddAsync(AuditIntent i, CancellationToken c) => fail ? throw new InvalidOperationException("audit") : Task.CompletedTask; }
    private sealed class Unit(params IMemory[] repositories) : IPpmUnitOfWork
    {
        public bool RolledBack { get; private set; }
        public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct)
        { var snapshots = repositories.Select(x => x.Snapshot()).ToArray(); try { return await operation(ct); } catch { for (var i = 0; i < repositories.Length; i++) repositories[i].Restore(snapshots[i]); RolledBack = true; throw; } }
    }
    private interface IMemory { object Snapshot(); void Restore(object value); }
    private class MemoryRepository<T> : IRepository<T>, IMemory where T : EntityBase
    {
        public List<T> Items { get; } = []; public int Reads { get; private set; }
        public Task<T?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken c) { Reads++; return Task.FromResult(Items.SingleOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted)); }
        public Task<IReadOnlyList<T>> ListAsync(Guid tenantId, CancellationToken c) => Task.FromResult<IReadOnlyList<T>>(Items.Where(x => x.TenantId == tenantId && !x.IsDeleted).ToArray());
        public Task<bool> CodeExistsAsync(Guid tenantId, string code, Guid? exclude, CancellationToken c) => Task.FromResult(Items.Any(x => x.TenantId == tenantId && !x.IsDeleted && x.Id != exclude && (string)x.GetType().GetProperty("Code")!.GetValue(x)! == code));
        public Task AddAsync(T entity, CancellationToken c) { Items.Add(entity); return Task.CompletedTask; }
        public Task ReplaceAsync(T entity, int expected, CancellationToken c) { if (entity.Version != expected + 1) throw new OptimisticConcurrencyException("version"); return Task.CompletedTask; }
        object IMemory.Snapshot() => Items.ToList(); void IMemory.Restore(object value) { Items.Clear(); Items.AddRange((List<T>)value); }
    }
    private sealed class PortfolioRepo : MemoryRepository<Portfolio>, IPortfolioRepository
    { public Task AdvanceInvestmentCaseCollectionFenceAsync(Portfolio entity, CancellationToken cancellationToken) { entity.AdvanceInvestmentCaseCollectionFence(); return Task.CompletedTask; } }
    private sealed class InvestmentCaseRepo : MemoryRepository<InvestmentCase>, IInvestmentCaseRepository
    {
        public Task AdvanceBenefitCommitmentCollectionFenceAsync(InvestmentCase entity, CancellationToken cancellationToken) { entity.AdvanceBenefitCommitmentCollectionFence(); return Task.CompletedTask; }
        public Task<bool> ExistsForPortfolioAsync(Guid tenantId, Guid portfolioId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(x => x.TenantId == tenantId && x.PortfolioId == portfolioId && !x.IsDeleted));
    }
    private sealed class BenefitCommitmentRepo : MemoryRepository<BenefitCommitment>, IBenefitCommitmentRepository
    {
        public Task<bool> ExistsForInvestmentCaseAsync(Guid tenantId, Guid investmentCaseId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(x => x.TenantId == tenantId && x.InvestmentCaseId == investmentCaseId && !x.IsDeleted));
    }
}
