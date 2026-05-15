using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.PlatformAccount;
using Diten.Platform.Application.Features.PlatformAccount.Commands;
using Diten.Platform.Application.Features.PlatformAccount.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.PlatformAccount.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.PlatformAccount.Queries;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.PlatformAccount;

public sealed class PlatformAccountRulesTests
{
    [Fact]
    public async Task Get_profile_returns_current_platform_actor()
    {
        var repository = new InMemoryPlatformAdministratorRepository();
        repository.Add(CreateAdministrator());
        var handler = new GetPlatformAccountProfileHandler(
            repository,
            new TestCurrentUserContext(" ADMIN@DITEN.COM "));

        var response = await handler.Handle(new GetPlatformAccountProfileQuery(), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(200, response.StatusCode);
        Assert.NotNull(response.Data);
        Assert.Equal("admin@diten.com", response.Data!.Email);
        Assert.Equal("Admin User", response.Data.DisplayName);
        Assert.Equal("PlatformAdmin", response.Data.ActorType);
        Assert.Contains("SuperAdmin", response.Data.Roles);
    }

    [Fact]
    public async Task Update_settings_changes_only_display_name()
    {
        var repository = new InMemoryPlatformAdministratorRepository();
        var administrator = CreateAdministrator();
        repository.Add(administrator);
        var handler = new UpdatePlatformAccountSettingsHandler(
            repository,
            new TestCurrentUserContext("admin@diten.com", actorName: "admin@diten.com"));

        var response = await handler.Handle(
            new UpdatePlatformAccountSettingsCommand(new UpdatePlatformAccountSettingsRequest(" Updated Admin ", administrator.Version)),
            CancellationToken.None);

        var updated = repository.Items.Single();
        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Equal("Updated Admin", updated.DisplayName);
        Assert.Equal("admin@diten.com", updated.Email);
        Assert.Equal("admin", updated.UserName);
        Assert.Equal(AdministratorStatus.Active, updated.Status);
        Assert.Equal(new[] { AdministratorRole.SuperAdmin, AdministratorRole.SupportAdmin }, updated.Roles);
        Assert.Equal("admin@diten.com", updated.UpdatedBy);
        Assert.Equal(2, updated.Version);
    }

    [Fact]
    public async Task Update_settings_rejects_unauthenticated_request()
    {
        var repository = new InMemoryPlatformAdministratorRepository();
        repository.Add(CreateAdministrator());
        var handler = new UpdatePlatformAccountSettingsHandler(
            repository,
            new TestCurrentUserContext(null, isAuthenticated: false));

        var response = await handler.Handle(
            new UpdatePlatformAccountSettingsCommand(new UpdatePlatformAccountSettingsRequest("Blocked", 1)),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
        Assert.Equal("Admin User", repository.Items.Single().DisplayName);
    }

    [Fact]
    public async Task Update_settings_rejects_stale_version()
    {
        var repository = new InMemoryPlatformAdministratorRepository();
        var administrator = CreateAdministrator();
        administrator.Version = 3;
        repository.Add(administrator);
        var handler = new UpdatePlatformAccountSettingsHandler(
            repository,
            new TestCurrentUserContext("admin@diten.com"));

        var response = await handler.Handle(
            new UpdatePlatformAccountSettingsCommand(new UpdatePlatformAccountSettingsRequest("Stale Update", 2)),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal("Admin User", repository.Items.Single().DisplayName);
        Assert.Equal(3, repository.Items.Single().Version);
    }

    private static PlatformAdministrator CreateAdministrator() => new()
    {
        Email = "admin@diten.com",
        NormalizedEmail = "admin@diten.com",
        UserName = "admin",
        NormalizedUserName = "admin",
        DisplayName = "Admin User",
        ActorType = ActorType.PlatformAdmin,
        Status = AdministratorStatus.Active,
        Roles = [AdministratorRole.SuperAdmin, AdministratorRole.SupportAdmin],
        InvitationStatus = AdministratorInvitationStatus.Accepted,
        LastLoginAtUtc = DateTimeOffset.Parse("2026-05-01T10:00:00Z"),
        InvitedAtUtc = DateTimeOffset.Parse("2026-04-01T10:00:00Z"),
        CreatedBy = "seed"
    };

    private sealed class TestCurrentUserContext : ICurrentUserContext
    {
        public TestCurrentUserContext(
            string? email,
            bool isAuthenticated = true,
            string actorName = "tester@diten.com")
        {
            Email = email;
            IsAuthenticated = isAuthenticated;
            ActorName = actorName;
        }

        public Guid UserId { get; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
        public string? Email { get; }
        public string? DisplayName => "Tester";
        public string ActorName { get; }
        public bool IsAuthenticated { get; }
    }

    private sealed class InMemoryPlatformAdministratorRepository : IPlatformAdministratorRepository
    {
        private readonly List<PlatformAdministrator> _items = [];
        public IReadOnlyList<PlatformAdministrator> Items => _items;

        public void Add(PlatformAdministrator administrator) => _items.Add(Clone(administrator));

        public Task<PlatformAdministrator> CreateAsync(PlatformAdministrator administrator, CancellationToken ct = default)
        {
            var clone = Clone(administrator);
            _items.Add(clone);
            return Task.FromResult(Clone(clone));
        }

        public Task<PlatformAdministrator?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(CloneOrNull(_items.FirstOrDefault(x => x.Id == id && !x.IsDeleted)));

        public Task<PlatformAdministrator?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct = default) =>
            Task.FromResult(CloneOrNull(_items.FirstOrDefault(x => x.NormalizedEmail == normalizedEmail && !x.IsDeleted)));

        public Task<bool> ExistsByEmailAsync(string normalizedEmail, Guid? excludeId = null, CancellationToken ct = default) =>
            Task.FromResult(_items.Any(x => x.NormalizedEmail == normalizedEmail && !x.IsDeleted && (!excludeId.HasValue || x.Id != excludeId.Value)));

        public Task<bool> ExistsByUserNameAsync(string normalizedUserName, Guid? excludeId = null, CancellationToken ct = default) =>
            Task.FromResult(_items.Any(x => x.NormalizedUserName == normalizedUserName && !x.IsDeleted && (!excludeId.HasValue || x.Id != excludeId.Value)));

        public Task<bool> UpdateAsync(PlatformAdministrator administrator, int expectedVersion, CancellationToken ct = default)
        {
            var item = _items.FirstOrDefault(x => x.Id == administrator.Id && !x.IsDeleted && x.Version == expectedVersion);
            if (item is null)
            {
                return Task.FromResult(false);
            }

            item.DisplayName = administrator.DisplayName;
            item.UpdatedBy = administrator.UpdatedBy;
            item.UpdatedAt = DateTimeOffset.UtcNow;
            item.Version = expectedVersion + 1;
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
            IReadOnlyList<PlatformAdministrator> items = _items.Where(x => !x.IsDeleted).Select(Clone).ToList();
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

        public Task<long> CountActiveSuperAdminsAsync(Guid? excludeId = null, CancellationToken ct = default) =>
            Task.FromResult((long)_items.Count(x =>
                !x.IsDeleted &&
                x.Status == AdministratorStatus.Active &&
                x.Roles.Contains(AdministratorRole.SuperAdmin) &&
                (!excludeId.HasValue || x.Id != excludeId.Value)));

        private static PlatformAdministrator? CloneOrNull(PlatformAdministrator? administrator) =>
            administrator is null ? null : Clone(administrator);

        private static PlatformAdministrator Clone(PlatformAdministrator administrator) => new()
        {
            Id = administrator.Id,
            CreatedAt = administrator.CreatedAt,
            CreatedBy = administrator.CreatedBy,
            UpdatedAt = administrator.UpdatedAt,
            UpdatedBy = administrator.UpdatedBy,
            IsDeleted = administrator.IsDeleted,
            Version = administrator.Version,
            Email = administrator.Email,
            NormalizedEmail = administrator.NormalizedEmail,
            UserName = administrator.UserName,
            NormalizedUserName = administrator.NormalizedUserName,
            DisplayName = administrator.DisplayName,
            ActorType = administrator.ActorType,
            PartnerId = administrator.PartnerId,
            AllowedTenantIds = administrator.AllowedTenantIds.ToList(),
            Status = administrator.Status,
            Roles = administrator.Roles.ToList(),
            LastLoginAtUtc = administrator.LastLoginAtUtc,
            InvitationStatus = administrator.InvitationStatus,
            InvitedAtUtc = administrator.InvitedAtUtc,
            InviteToken = administrator.InviteToken,
            InviteExpiresAtUtc = administrator.InviteExpiresAtUtc,
            LastStatusReason = administrator.LastStatusReason
        };
    }
}
