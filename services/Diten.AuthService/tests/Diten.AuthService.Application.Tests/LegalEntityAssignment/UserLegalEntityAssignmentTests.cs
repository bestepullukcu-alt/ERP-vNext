using System.IdentityModel.Tokens.Jwt;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Handlers.QueryHandlers;
using Diten.AuthService.Application.Features.Users.Queries;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.AuthService.Application.Tests.LegalEntityAssignment;

public sealed class UserLegalEntityAssignmentTests
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid HoldingId = Guid.Parse("1e9a1000-0000-0000-0000-000000000001");

    private sealed class FakeRepo : IUserLegalEntityAssignmentRepository
    {
        private readonly List<UserLegalEntityAssignment> _items;
        public FakeRepo(IEnumerable<UserLegalEntityAssignment> items) => _items = items.ToList();

        public Task<IReadOnlyList<UserLegalEntityAssignment>> GetByUserIdAsync(Guid userId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<UserLegalEntityAssignment>>(_items.Where(x => x.UserId == userId && !x.IsDeleted).ToList());

        public Task<bool> ExistsAsync(Guid userId, Guid legalEntityId, Guid tenantId, CancellationToken ct)
            => Task.FromResult(_items.Any(x => x.UserId == userId && x.LegalEntityId == legalEntityId && x.TenantId == tenantId && !x.IsDeleted));

        public Task<UserLegalEntityAssignment> CreateAsync(UserLegalEntityAssignment assignment, CancellationToken ct)
        {
            _items.Add(assignment);
            return Task.FromResult(assignment);
        }
    }

    [Fact]
    public async Task GetUserLegalEntities_returns_assigned_ids()
    {
        var userId = Guid.NewGuid();
        var repo = new FakeRepo([new UserLegalEntityAssignment(userId, TenantId, HoldingId)]);
        var handler = new GetUserLegalEntitiesQueryHandler(repo);

        var response = await handler.Handle(new GetUserLegalEntitiesQuery(userId), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Single(response.Data!);
        Assert.Equal(HoldingId, response.Data![0]);
    }

    [Fact]
    public async Task GetUserLegalEntities_empty_when_no_assignments()
    {
        var repo = new FakeRepo([]);
        var handler = new GetUserLegalEntitiesQueryHandler(repo);

        var response = await handler.Handle(new GetUserLegalEntitiesQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Empty(response.Data!);
    }

    private static TokenService CreateTokenService() => new(Options.Create(new JwtSettings
    {
        Secret = "UnitTestJwtSigningSecretValueThatIsAtLeast32CharsLong!!",
        Issuer = "diten-auth-service",
        Audience = "diten-erp",
        AccessTokenExpirationMinutes = 15
    }));

    [Fact]
    public void Token_carries_legal_entities_claim_when_assigned()
    {
        var user = new User("admin@diten.com", "hash", "Diten", "Admin", TenantId);
        var token = CreateTokenService().GenerateAccessToken(user, [], [], [HoldingId.ToString()], 15);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var claim = jwt.Claims.FirstOrDefault(c => c.Type == "legal_entities");
        Assert.NotNull(claim);
        Assert.Equal(HoldingId.ToString(), claim!.Value);
    }

    [Fact]
    public void Token_omits_legal_entities_claim_when_none()
    {
        var user = new User("admin@diten.com", "hash", "Diten", "Admin", TenantId);
        var token = CreateTokenService().GenerateAccessToken(user, [], [], 15);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == "legal_entities");
    }

    [Fact]
    public void DataSeeder_seeds_admin_legal_entity_assignment()
    {
        var path = Path.Combine(FindRepoRoot(), "services", "Diten.AuthService", "src",
            "Diten.AuthService.Persistence", "Seed", "DataSeeder.cs");
        var source = File.ReadAllText(path);
        Assert.Contains("SeedLegalEntityAssignmentsAsync", source);
        Assert.Contains("1e9a1000-0000-0000-0000-000000000001", source);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "services")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new DirectoryNotFoundException("Repo root with 'services' not found.");
    }
}
