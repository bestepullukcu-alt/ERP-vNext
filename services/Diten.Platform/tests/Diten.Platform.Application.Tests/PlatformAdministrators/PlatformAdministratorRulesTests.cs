using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.PlatformAdministrators;
using Diten.Platform.Application.Features.PlatformAdministrators.Commands;
using Diten.Platform.Application.Features.PlatformAdministrators.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.PlatformAdministrators.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.PlatformAdministrators.Queries;
using Diten.Platform.Application.Features.PlatformAdministrators.Validators;
using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.PlatformAdministrators;

public sealed class PlatformAdministratorRulesTests
{
    [Fact]
    public void Invite_validator_accepts_valid_platform_admin()
    {
        var validator = new InvitePlatformAdministratorValidator();
        var result = validator.Validate(new InvitePlatformAdministratorCommand(new InvitePlatformAdministratorRequest(
            "admin@diten.com",
            "admin",
            "Platform Admin",
            "PlatformAdmin",
            null,
            [],
            ["SuperAdmin"])));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Invite_validator_rejects_partner_admin_without_scope()
    {
        var validator = new InvitePlatformAdministratorValidator();
        var result = validator.Validate(new InvitePlatformAdministratorCommand(new InvitePlatformAdministratorRequest(
            "partner@diten.com",
            "partner",
            "Partner Admin",
            "PartnerAdmin",
            null,
            [],
            ["SupportAdmin"])));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("PartnerId"));
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("AllowedTenantIds"));
    }

    [Fact]
    public void Assign_roles_validator_rejects_empty_or_invalid_roles()
    {
        var validator = new AssignPlatformAdministratorRolesValidator();
        var emptyResult = validator.Validate(new AssignPlatformAdministratorRolesCommand(
            Guid.NewGuid(),
            new AssignPlatformAdministratorRolesRequest([], 1)));
        var invalidResult = validator.Validate(new AssignPlatformAdministratorRolesCommand(
            Guid.NewGuid(),
            new AssignPlatformAdministratorRolesRequest(["Owner"], 1)));

        Assert.False(emptyResult.IsValid);
        Assert.False(invalidResult.IsValid);
    }

    [Fact]
    public async Task Invite_handler_rejects_duplicate_normalized_email()
    {
        var repository = new InMemoryPlatformAdministratorRepository();
        await repository.CreateAsync(new PlatformAdministrator
        {
            Email = "admin@diten.com",
            NormalizedEmail = "admin@diten.com",
            UserName = "admin",
            NormalizedUserName = "admin",
            DisplayName = "Existing",
            Roles = [AdministratorRole.SuperAdmin],
            CreatedBy = "test"
        });
        var handler = new InvitePlatformAdministratorHandler(
            repository,
            new TestPlatformAdministratorProvisioningService(),
            new TestCurrentUserContext(),
            NullLogger<InvitePlatformAdministratorHandler>.Instance);

        var response = await handler.Handle(new InvitePlatformAdministratorCommand(new InvitePlatformAdministratorRequest(
            " ADMIN@DITEN.COM ",
            "duplicate",
            "Duplicate",
            "PlatformAdmin",
            null,
            [],
            ["ReadOnly"])), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Single(repository.Items);
    }

    [Fact]
    public async Task Get_by_id_handler_returns_not_found_for_soft_deleted_record()
    {
        var repository = new InMemoryPlatformAdministratorRepository();
        var admin = await repository.CreateAsync(new PlatformAdministrator
        {
            Email = "admin@diten.com",
            NormalizedEmail = "admin@diten.com",
            UserName = "admin",
            NormalizedUserName = "admin",
            DisplayName = "Admin",
            Roles = [AdministratorRole.ReadOnly],
            CreatedBy = "test"
        });
        admin.IsDeleted = true;
        var handler = new GetPlatformAdministratorByIdHandler(repository);

        var response = await handler.Handle(new GetPlatformAdministratorByIdQuery(admin.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Query_excludes_soft_deleted_records()
    {
        var repository = new InMemoryPlatformAdministratorRepository();
        await repository.CreateAsync(new PlatformAdministrator
        {
            Email = "active@diten.com",
            NormalizedEmail = "active@diten.com",
            UserName = "active",
            NormalizedUserName = "active",
            DisplayName = "Active",
            Roles = [AdministratorRole.ReadOnly],
            CreatedBy = "test"
        });
        await repository.CreateAsync(new PlatformAdministrator
        {
            Email = "deleted@diten.com",
            NormalizedEmail = "deleted@diten.com",
            UserName = "deleted",
            NormalizedUserName = "deleted",
            DisplayName = "Deleted",
            Roles = [AdministratorRole.ReadOnly],
            IsDeleted = true,
            CreatedBy = "test"
        });
        var handler = new GetPlatformAdministratorsHandler(repository);

        var response = await handler.Handle(
            new GetPlatformAdministratorsQuery(new PlatformAdministratorFilterRequest(null, null, null, null, null, null)),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.IsType<PagedResult<PlatformAdministratorListItemDto>>(response.Data);
        Assert.Single(response.Data!.Items);
    }

    private sealed class TestCurrentUserContext : ICurrentUserContext
    {
        public Guid UserId { get; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
        public string? Email => "tester@diten.com";
        public string? DisplayName => "Tester";
        public string ActorName => "tester@diten.com";
        public bool IsAuthenticated => true;
    }

    private sealed class InMemoryPlatformAdministratorRepository : IPlatformAdministratorRepository
    {
        private readonly List<PlatformAdministrator> _items = [];
        public IReadOnlyList<PlatformAdministrator> Items => _items;

        public Task<PlatformAdministrator> CreateAsync(PlatformAdministrator administrator, CancellationToken ct = default)
        {
            _items.Add(administrator);
            return Task.FromResult(administrator);
        }

        public Task<PlatformAdministrator?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x => x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsByEmailAsync(string normalizedEmail, Guid? excludeId = null, CancellationToken ct = default) =>
            Task.FromResult(_items.Any(x => x.NormalizedEmail == normalizedEmail && !x.IsDeleted && (!excludeId.HasValue || x.Id != excludeId.Value)));

        public Task<bool> ExistsByUserNameAsync(string normalizedUserName, Guid? excludeId = null, CancellationToken ct = default) =>
            Task.FromResult(_items.Any(x => x.NormalizedUserName == normalizedUserName && !x.IsDeleted && (!excludeId.HasValue || x.Id != excludeId.Value)));

        public Task<bool> UpdateAsync(PlatformAdministrator administrator, int expectedVersion, CancellationToken ct = default)
        {
            if (administrator.Version != expectedVersion + 1)
            {
                return Task.FromResult(false);
            }

            administrator.UpdatedAt = DateTimeOffset.UtcNow;
            return Task.FromResult(true);
        }

        public Task<bool> SoftDeleteAsync(Guid id, int expectedVersion, string actorName, CancellationToken ct = default)
        {
            var item = _items.FirstOrDefault(x => x.Id == id && !x.IsDeleted && x.Version == expectedVersion);
            if (item is null)
            {
                return Task.FromResult(false);
            }

            item.IsDeleted = true;
            item.UpdatedBy = actorName;
            item.Version++;
            return Task.FromResult(true);
        }

        public Task<(IReadOnlyList<PlatformAdministrator> Items, long TotalCount)> QueryAsync(
            PlatformAdministratorQuery query,
            CancellationToken ct = default)
        {
            IReadOnlyList<PlatformAdministrator> items = _items.Where(x => !x.IsDeleted).ToList();
            return Task.FromResult((items, (long)items.Count));
        }

        public Task<PlatformAdministratorStatsSnapshot> GetStatsAsync(CancellationToken ct = default)
        {
            var live = _items.Where(x => !x.IsDeleted).ToList();
            return Task.FromResult(new PlatformAdministratorStatsSnapshot(
                live.Count,
                live.Count(x => x.Status == AdministratorStatus.Active),
                live.Count(x => x.Status == AdministratorStatus.Suspended),
                live.Count(x => x.Status == AdministratorStatus.Disabled),
                live.Count(x => x.InvitationStatus == AdministratorInvitationStatus.PendingInvitation)));
        }

        public Task<PlatformAdministrator?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x => x.NormalizedEmail == normalizedEmail && !x.IsDeleted));

        public Task<long> CountActiveSuperAdminsAsync(Guid? excludeId = null, CancellationToken ct = default) =>
            Task.FromResult((long)_items.Count(x =>
                !x.IsDeleted &&
                x.Status == AdministratorStatus.Active &&
                x.Roles.Contains(AdministratorRole.SuperAdmin) &&
                (!excludeId.HasValue || x.Id != excludeId.Value)));
    }

    private sealed class TestPlatformAdministratorProvisioningService : IPlatformAdministratorProvisioningService
    {
        public Task<PlatformAdministratorProvisioningResult> ProvisionAsync(PlatformAdministratorProvisioningRequest request, CancellationToken ct) =>
            Task.FromResult(new PlatformAdministratorProvisioningResult(null, true));

        public Task SyncAsync(PlatformAdministratorProvisioningSyncRequest request, CancellationToken ct) => Task.CompletedTask;
    }
}
