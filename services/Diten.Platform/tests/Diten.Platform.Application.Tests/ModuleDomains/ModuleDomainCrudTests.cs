using Diten.Platform.Application.Features.ModuleDomains;
using Diten.Platform.Application.Features.ModuleDomains.Commands;
using Diten.Platform.Application.Features.ModuleDomains.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.ModuleDomains.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.ModuleDomains.Queries;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.ModuleDomains;

// UI #2 — Domain Management CRUD over the operator-managed platform_module_domains collection.
public sealed class ModuleDomainCrudTests
{
    [Fact]
    public async Task Create_normalizes_code_to_uppercase_and_persists()
    {
        var repo = new InMemoryModuleDomainRepository();
        var result = await new CreateModuleDomainCommandHandler(repo).Handle(
            new CreateModuleDomainCommand(new CreateModuleDomainRequest("finance ops", "Finance Ops", " core ", 30, true)),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(201, result.StatusCode);
        var item = Assert.Single(repo.Items);
        Assert.Equal("FINANCE-OPS", item.Code);
        Assert.Equal("Finance Ops", item.DisplayName);
        Assert.Equal("core", item.Description);
        Assert.Equal(30, item.SortOrder);
        Assert.True(item.IsActive);
    }

    [Fact]
    public async Task Create_with_blank_code_is_bad_request()
    {
        var repo = new InMemoryModuleDomainRepository();
        var result = await new CreateModuleDomainCommandHandler(repo).Handle(
            new CreateModuleDomainCommand(new CreateModuleDomainRequest("   ", "Name", null, null, true)),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        Assert.Empty(repo.Items);
    }

    [Fact]
    public async Task Create_duplicate_code_returns_409()
    {
        var repo = new InMemoryModuleDomainRepository();
        repo.Items.Add(new ModuleDomain { Code = "FINANCE", DisplayName = "Finance" });

        var result = await new CreateModuleDomainCommandHandler(repo).Handle(
            new CreateModuleDomainCommand(new CreateModuleDomainRequest("Finance", "Finance Again", null, null, true)),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains(ModuleDomainErrorCodes.DomainCodeInUse, result.Errors);
        Assert.Single(repo.Items);
    }

    [Fact]
    public async Task Create_reuses_code_of_soft_deleted_record()
    {
        var repo = new InMemoryModuleDomainRepository();
        repo.Items.Add(new ModuleDomain { Code = "FINANCE", DisplayName = "Old", IsDeleted = true });

        var result = await new CreateModuleDomainCommandHandler(repo).Handle(
            new CreateModuleDomainCommand(new CreateModuleDomainRequest("finance", "Finance", null, null, true)),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(201, result.StatusCode);
    }

    [Fact]
    public async Task Update_changes_fields_and_returns_204()
    {
        var repo = new InMemoryModuleDomainRepository();
        var existing = new ModuleDomain { Code = "FINANCE", DisplayName = "Finance", IsActive = true };
        repo.Items.Add(existing);

        var result = await new UpdateModuleDomainCommandHandler(repo).Handle(
            new UpdateModuleDomainCommand(existing.Id, new UpdateModuleDomainRequest("FINANCE", "Finance & Accounting", "desc", 50, false)),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(204, result.StatusCode);
        Assert.Equal("Finance & Accounting", existing.DisplayName);
        Assert.Equal(50, existing.SortOrder);
        Assert.False(existing.IsActive);
    }

    [Fact]
    public async Task Update_missing_item_returns_404()
    {
        var repo = new InMemoryModuleDomainRepository();
        var result = await new UpdateModuleDomainCommandHandler(repo).Handle(
            new UpdateModuleDomainCommand(Guid.NewGuid(), new UpdateModuleDomainRequest("X", "X", null, null, true)),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Update_to_existing_other_code_returns_409()
    {
        var repo = new InMemoryModuleDomainRepository();
        var a = new ModuleDomain { Code = "FINANCE", DisplayName = "Finance" };
        var b = new ModuleDomain { Code = "SALES", DisplayName = "Sales" };
        repo.Items.AddRange([a, b]);

        var result = await new UpdateModuleDomainCommandHandler(repo).Handle(
            new UpdateModuleDomainCommand(b.Id, new UpdateModuleDomainRequest("finance", "Sales", null, null, true)),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task Delete_soft_deletes_and_returns_204()
    {
        var repo = new InMemoryModuleDomainRepository();
        var existing = new ModuleDomain { Code = "FINANCE", DisplayName = "Finance" };
        repo.Items.Add(existing);

        var result = await new DeleteModuleDomainCommandHandler(repo).Handle(
            new DeleteModuleDomainCommand(existing.Id), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(204, result.StatusCode);
        Assert.True(existing.IsDeleted);
    }

    [Fact]
    public async Task GetById_and_List_return_expected_data()
    {
        var repo = new InMemoryModuleDomainRepository();
        var existing = new ModuleDomain { Code = "FINANCE", DisplayName = "Finance", SortOrder = 10 };
        repo.Items.Add(existing);

        var byId = await new GetModuleDomainByIdQueryHandler(repo).Handle(
            new GetModuleDomainByIdQuery(existing.Id), CancellationToken.None);
        Assert.True(byId.IsSuccessful);
        Assert.Equal("FINANCE", byId.Data!.Code);

        var list = await new GetModuleDomainsQueryHandler(repo).Handle(
            new GetModuleDomainsQuery(new ModuleDomainFilterRequest(null, null)), CancellationToken.None);
        Assert.True(list.IsSuccessful);
        Assert.Equal(1, list.Data!.TotalCount);
        Assert.Single(list.Data.Items);
    }

    private sealed class InMemoryModuleDomainRepository : IModuleDomainRepository
    {
        public List<ModuleDomain> Items { get; } = [];

        public Task<ModuleDomain> CreateAsync(ModuleDomain item, CancellationToken ct = default)
        {
            Items.Add(item);
            return Task.FromResult(item);
        }

        public Task<ModuleDomain?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Id == id && !x.IsDeleted));

        public Task<ModuleDomain?> GetByCodeAsync(string code, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Code == code && !x.IsDeleted));

        public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default) =>
            Task.FromResult(Items.Any(x => x.Code == code && !x.IsDeleted && (!excludeId.HasValue || x.Id != excludeId.Value)));

        public Task UpdateAsync(ModuleDomain item, CancellationToken ct = default)
        {
            var index = Items.FindIndex(x => x.Id == item.Id);
            if (index >= 0) Items[index] = item;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var item = Items.FirstOrDefault(x => x.Id == id);
            if (item is not null) item.IsDeleted = true;
            return Task.CompletedTask;
        }

        public Task<(IReadOnlyList<ModuleDomain> Items, long TotalCount)> QueryAsync(ModuleDomainQuery query, CancellationToken ct = default)
        {
            var live = Items.Where(x => !x.IsDeleted);
            if (query.IsActive.HasValue) live = live.Where(x => x.IsActive == query.IsActive.Value);
            if (!string.IsNullOrWhiteSpace(query.Search))
                live = live.Where(x => x.Code.Contains(query.Search, StringComparison.OrdinalIgnoreCase)
                    || x.DisplayName.Contains(query.Search, StringComparison.OrdinalIgnoreCase));
            var ordered = live.OrderBy(x => x.SortOrder).ToList();
            IReadOnlyList<ModuleDomain> page = ordered
                .Skip((Math.Max(query.Page, 1) - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();
            return Task.FromResult((page, (long)ordered.Count));
        }

        public Task<IReadOnlyList<ModuleDomain>> GetActiveAsync(CancellationToken ct = default)
        {
            IReadOnlyList<ModuleDomain> result = Items
                .Where(x => x.IsActive && !x.IsDeleted)
                .OrderBy(x => x.SortOrder)
                .ToList();
            return Task.FromResult(result);
        }
    }
}
