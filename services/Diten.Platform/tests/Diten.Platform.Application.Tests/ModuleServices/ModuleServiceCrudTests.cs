using Diten.Platform.Application.Features.ModuleServices;
using Diten.Platform.Application.Features.ModuleServices.Commands;
using Diten.Platform.Application.Features.ModuleServices.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.ModuleServices.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.ModuleServices.Queries;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.ModuleServices;

// UI #3 — Service Management CRUD over the operator-managed platform_module_services collection.
public sealed class ModuleServiceCrudTests
{
    [Fact]
    public async Task Create_normalizes_code_to_uppercase_and_persists()
    {
        var repo = new InMemoryModuleServiceRepository();
        var result = await new CreateModuleServiceCommandHandler(repo).Handle(
            new CreateModuleServiceCommand(new CreateModuleServiceRequest("ppm service", "PPM Service", " core ", 30, true)),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(201, result.StatusCode);
        var item = Assert.Single(repo.Items);
        Assert.Equal("PPM-SERVICE", item.Code);
        Assert.Equal("PPM Service", item.DisplayName);
        Assert.Equal("core", item.Description);
        Assert.Equal(30, item.SortOrder);
        Assert.True(item.IsActive);
    }

    [Fact]
    public async Task Create_with_blank_code_is_bad_request()
    {
        var repo = new InMemoryModuleServiceRepository();
        var result = await new CreateModuleServiceCommandHandler(repo).Handle(
            new CreateModuleServiceCommand(new CreateModuleServiceRequest("   ", "Name", null, null, true)),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        Assert.Empty(repo.Items);
    }

    [Fact]
    public async Task Create_duplicate_code_returns_409()
    {
        var repo = new InMemoryModuleServiceRepository();
        repo.Items.Add(new ModuleService { Code = "DITENPLATFORM", DisplayName = "Diten.Platform" });

        var result = await new CreateModuleServiceCommandHandler(repo).Handle(
            new CreateModuleServiceCommand(new CreateModuleServiceRequest("DitenPlatform", "Diten.Platform Again", null, null, true)),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains(ModuleServiceErrorCodes.ServiceCodeInUse, result.Errors);
        Assert.Single(repo.Items);
    }

    [Fact]
    public async Task Create_reuses_code_of_soft_deleted_record()
    {
        var repo = new InMemoryModuleServiceRepository();
        repo.Items.Add(new ModuleService { Code = "DITENPLATFORM", DisplayName = "Old", IsDeleted = true });

        var result = await new CreateModuleServiceCommandHandler(repo).Handle(
            new CreateModuleServiceCommand(new CreateModuleServiceRequest("ditenplatform", "Diten.Platform", null, null, true)),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(201, result.StatusCode);
    }

    [Fact]
    public async Task Update_changes_fields_and_returns_204()
    {
        var repo = new InMemoryModuleServiceRepository();
        var existing = new ModuleService { Code = "DITENPLATFORM", DisplayName = "Diten.Platform", IsActive = true };
        repo.Items.Add(existing);

        var result = await new UpdateModuleServiceCommandHandler(repo).Handle(
            new UpdateModuleServiceCommand(existing.Id, new UpdateModuleServiceRequest("DITENPLATFORM", "Diten.Platform Core", "desc", 50, false)),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(204, result.StatusCode);
        Assert.Equal("Diten.Platform Core", existing.DisplayName);
        Assert.Equal(50, existing.SortOrder);
        Assert.False(existing.IsActive);
    }

    [Fact]
    public async Task Update_missing_item_returns_404()
    {
        var repo = new InMemoryModuleServiceRepository();
        var result = await new UpdateModuleServiceCommandHandler(repo).Handle(
            new UpdateModuleServiceCommand(Guid.NewGuid(), new UpdateModuleServiceRequest("X", "X", null, null, true)),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Update_to_existing_other_code_returns_409()
    {
        var repo = new InMemoryModuleServiceRepository();
        var a = new ModuleService { Code = "DITENPLATFORM", DisplayName = "Diten.Platform" };
        var b = new ModuleService { Code = "DITENMDMSERVICE", DisplayName = "Diten.MdmService" };
        repo.Items.AddRange([a, b]);

        var result = await new UpdateModuleServiceCommandHandler(repo).Handle(
            new UpdateModuleServiceCommand(b.Id, new UpdateModuleServiceRequest("ditenplatform", "Diten.MdmService", null, null, true)),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task Delete_soft_deletes_and_returns_204()
    {
        var repo = new InMemoryModuleServiceRepository();
        var existing = new ModuleService { Code = "DITENPLATFORM", DisplayName = "Diten.Platform" };
        repo.Items.Add(existing);

        var result = await new DeleteModuleServiceCommandHandler(repo).Handle(
            new DeleteModuleServiceCommand(existing.Id), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(204, result.StatusCode);
        Assert.True(existing.IsDeleted);
    }

    [Fact]
    public async Task GetById_and_List_return_expected_data()
    {
        var repo = new InMemoryModuleServiceRepository();
        var existing = new ModuleService { Code = "DITENPLATFORM", DisplayName = "Diten.Platform", SortOrder = 10 };
        repo.Items.Add(existing);

        var byId = await new GetModuleServiceByIdQueryHandler(repo).Handle(
            new GetModuleServiceByIdQuery(existing.Id), CancellationToken.None);
        Assert.True(byId.IsSuccessful);
        Assert.Equal("DITENPLATFORM", byId.Data!.Code);

        var list = await new GetModuleServicesQueryHandler(repo).Handle(
            new GetModuleServicesQuery(new ModuleServiceFilterRequest(null, null)), CancellationToken.None);
        Assert.True(list.IsSuccessful);
        Assert.Equal(1, list.Data!.TotalCount);
        Assert.Single(list.Data.Items);
    }

    private sealed class InMemoryModuleServiceRepository : IModuleServiceRepository
    {
        public List<ModuleService> Items { get; } = [];

        public Task<ModuleService> CreateAsync(ModuleService item, CancellationToken ct = default)
        {
            Items.Add(item);
            return Task.FromResult(item);
        }

        public Task<ModuleService?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Id == id && !x.IsDeleted));

        public Task<ModuleService?> GetByCodeAsync(string code, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Code == code && !x.IsDeleted));

        public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default) =>
            Task.FromResult(Items.Any(x => x.Code == code && !x.IsDeleted && (!excludeId.HasValue || x.Id != excludeId.Value)));

        public Task UpdateAsync(ModuleService item, CancellationToken ct = default)
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

        public Task<(IReadOnlyList<ModuleService> Items, long TotalCount)> QueryAsync(ModuleServiceQuery query, CancellationToken ct = default)
        {
            var live = Items.Where(x => !x.IsDeleted);
            if (query.IsActive.HasValue) live = live.Where(x => x.IsActive == query.IsActive.Value);
            if (!string.IsNullOrWhiteSpace(query.Search))
                live = live.Where(x => x.Code.Contains(query.Search, StringComparison.OrdinalIgnoreCase)
                    || x.DisplayName.Contains(query.Search, StringComparison.OrdinalIgnoreCase));
            var ordered = live.OrderBy(x => x.SortOrder).ToList();
            IReadOnlyList<ModuleService> page = ordered
                .Skip((Math.Max(query.Page, 1) - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();
            return Task.FromResult((page, (long)ordered.Count));
        }

        public Task<IReadOnlyList<ModuleService>> GetActiveAsync(CancellationToken ct = default)
        {
            IReadOnlyList<ModuleService> result = Items
                .Where(x => x.IsActive && !x.IsDeleted)
                .OrderBy(x => x.SortOrder)
                .ToList();
            return Task.FromResult(result);
        }
    }
}
