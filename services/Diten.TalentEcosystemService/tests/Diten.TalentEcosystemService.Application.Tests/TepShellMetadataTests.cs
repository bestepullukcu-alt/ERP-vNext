using System.Reflection;
using System.Security.Claims;
using System.Text;
using Diten.TalentEcosystemService.Api.Controllers.Tep;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.TepShell;
using Diten.TalentEcosystemService.Application.Features.TepShell.Commands;
using Diten.TalentEcosystemService.Application.Features.TepShell.Handlers;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using Diten.TalentEcosystemService.Persistence.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Diten.TalentEcosystemService.Application.Tests;

public sealed class TepShellMetadataTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static readonly Guid Holding = Guid.Parse("1e9a1000-0000-0000-0000-000000000001");
    private static readonly Guid Medikal = Guid.Parse("1e9a1000-0000-0000-0000-000000000002");
    private static readonly Guid Teknoloji = Guid.Parse("1e9a1000-0000-0000-0000-000000000003");

    private static FixedLegalEntityContext PilotLegalEntityContext() =>
        new(Holding, new[] { Holding, Medikal, Teknoloji });

    [Fact]
    public async Task Create_rejects_duplicate_active_code_in_tenant_scope()
    {
        var repository = new InMemoryTepShellMetadataRepository();
        var handler = new CreateTepShellMetadataHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext());
        var request = ValidRequest("TEP-SHELL");

        var first = await handler.Handle(new CreateTepShellMetadataCommand(request), CancellationToken.None);
        var second = await handler.Handle(new CreateTepShellMetadataCommand(request), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Get_by_id_is_tenant_scoped_and_returns_404_for_cross_tenant()
    {
        var repository = new InMemoryTepShellMetadataRepository();
        var create = new CreateTepShellMetadataHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext());
        var created = await create.Handle(new CreateTepShellMetadataCommand(ValidRequest("TEP-A")), CancellationToken.None);

        var crossTenant = new GetTepShellMetadataByIdHandler(repository, new FixedTenantContext(TenantB), PilotLegalEntityContext());
        var result = await crossTenant.Handle(new(created.Data), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Archive_sets_soft_delete_and_deleted_at()
    {
        var repository = new InMemoryTepShellMetadataRepository();
        var create = new CreateTepShellMetadataHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext());
        var created = await create.Handle(new CreateTepShellMetadataCommand(ValidRequest("TEP-ARCH")), CancellationToken.None);
        var archive = new ArchiveTepShellMetadataHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext());

        var result = await archive.Handle(new(created.Data), CancellationToken.None);
        var stored = repository.Items.Single();

        Assert.True(result.IsSuccessful);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(TepShellState.Archived, stored.ShellState);
    }

    [Fact]
    public async Task Active_state_requires_available_dependency_boundaries()
    {
        var repository = new InMemoryTepShellMetadataRepository();
        var handler = new CreateTepShellMetadataHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext());
        var request = ValidRequest("TEP-ACTIVE") with
        {
            ShellState = TepShellState.Active,
            HcmFoundationState = TepShellDependencyStatus.Deferred
        };

        var result = await handler.Handle(new CreateTepShellMetadataCommand(request), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Deferred_dependency_state_is_allowed_for_non_active_shell()
    {
        var repository = new InMemoryTepShellMetadataRepository();
        var handler = new CreateTepShellMetadataHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext());
        var request = ValidRequest("TEP-DEFERRED") with
        {
            ShellState = TepShellState.Deferred,
            HcmFoundationState = TepShellDependencyStatus.Deferred
        };

        var result = await handler.Handle(new CreateTepShellMetadataCommand(request), CancellationToken.None);

        Assert.True(result.IsSuccessful);
    }

    [Theory]
    [InlineData("candidate-profile")]
    [InlineData("raw_payload")]
    [InlineData("national-id")]
    [InlineData("reference_exchange")]
    public async Task Forbidden_runtime_markers_are_rejected(string marker)
    {
        var repository = new InMemoryTepShellMetadataRepository();
        var handler = new CreateTepShellMetadataHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext());
        var request = ValidRequest("TEP-FORBIDDEN") with { DisplayName = marker };

        var result = await handler.Handle(new CreateTepShellMetadataCommand(request), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public void Controller_permissions_match_shell_namespace()
    {
        Assert.NotNull(typeof(TepShellMetadataController).GetCustomAttribute<AuthorizeAttribute>());
        AssertPermission(nameof(TepShellMetadataController.GetAll), TepShellPermissions.Read);
        AssertPermission(nameof(TepShellMetadataController.GetById), TepShellPermissions.Read);
        AssertPermission(nameof(TepShellMetadataController.Create), TepShellPermissions.Manage);
        AssertPermission(nameof(TepShellMetadataController.Update), TepShellPermissions.Manage);
        AssertPermission(nameof(TepShellMetadataController.Archive), TepShellPermissions.Manage);
        AssertPermission(nameof(TepShellMetadataController.GetHealth), TepShellPermissions.Read);
    }

    [Fact]
    public async Task Has_permission_attribute_supports_space_and_comma_claim_lists()
    {
        var attribute = new HasPermissionAttribute(TepShellPermissions.Manage);
        var context = AuthorizationContext("tep.shell.read,tep.shell.manage other.permission");

        await attribute.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public async Task Has_permission_attribute_forbids_missing_permission()
    {
        var attribute = new HasPermissionAttribute(TepShellPermissions.Manage);
        var context = AuthorizationContext(TepShellPermissions.Read);

        await attribute.OnAuthorizationAsync(context);

        Assert.IsType<ForbidResult>(context.Result);
    }

    [Fact]
    public async Task Has_permission_attribute_returns_unauthorized_for_anonymous_user()
    {
        var attribute = new HasPermissionAttribute(TepShellPermissions.Read);
        var context = AuthorizationContext(TepShellPermissions.Read, authenticated: false);

        await attribute.OnAuthorizationAsync(context);

        Assert.IsType<UnauthorizedResult>(context.Result);
    }

    [Fact]
    public void Jwt_bearer_validation_parameters_match_repo_pattern()
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "Diten",
            ValidAudience = "Diten",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("development-only-secret-replace-with-vault-value-32")),
            ClockSkew = TimeSpan.Zero
        };

        Assert.True(parameters.ValidateIssuer);
        Assert.True(parameters.ValidateAudience);
        Assert.True(parameters.ValidateLifetime);
        Assert.True(parameters.ValidateIssuerSigningKey);
        Assert.Equal("Diten", parameters.ValidIssuer);
        Assert.Equal("Diten", parameters.ValidAudience);
        Assert.Equal(TimeSpan.Zero, parameters.ClockSkew);
        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, "Bearer");
    }

    [Fact]
    public void Mongo_repository_contract_names_are_tenant_aware()
    {
        Assert.Equal("tep_shell_metadata", MongoTepShellMetadataRepository.CollectionName);
        Assert.Equal("ux_tep_shell_metadata_tenant_code_active", MongoTepShellMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_tep_shell_metadata_tenant_state", MongoTepShellMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public async Task LegalEntity_create_stamps_the_selected_legal_entity()
    {
        var repository = new InMemoryTepShellMetadataRepository();
        var handler = new CreateTepShellMetadataHandler(
            repository, new FixedTenantContext(TenantA), new FixedLegalEntityContext(Medikal, new[] { Medikal }));

        var created = await handler.Handle(new CreateTepShellMetadataCommand(ValidRequest("TEP-LE")), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(Medikal, repository.Items.Single().LegalEntityId);
    }

    [Fact]
    public async Task LegalEntity_create_without_a_permitted_selection_is_forbidden()
    {
        var repository = new InMemoryTepShellMetadataRepository();
        var handler = new CreateTepShellMetadataHandler(
            repository, new FixedTenantContext(TenantA), new FixedLegalEntityContext(Teknoloji, selectionAllowed: false));

        var response = await handler.Handle(new CreateTepShellMetadataCommand(ValidRequest("TEP-403")), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
        Assert.Empty(repository.Items);
    }

    private static TepShellMetadataRequest ValidRequest(string code) =>
        new(
            code,
            "TEP Shell",
            TepShellState.Draft,
            TepShellDependencyStatus.Available,
            TepShellDependencyStatus.Available,
            TepShellDependencyStatus.Available,
            TepShellDependencyStatus.Available,
            "v1",
            [new TepShellDependencyStateDto("hcm.employee-projections", TepShellDependencyStatus.Available, null)],
            DateTimeOffset.UtcNow,
            1);

    private static void AssertPermission(string methodName, string expected)
    {
        var method = typeof(TepShellMetadataController)
            .GetMethods()
            .Single(method => method.Name == methodName);
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;

        Assert.Equal(expected, field.GetValue(attribute));
    }

    private static AuthorizationFilterContext AuthorizationContext(string permissions, bool authenticated = true)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                authenticated ? [new Claim("permissions", permissions)] : [],
                authenticated ? "test" : null))
        };

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, []);
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;
        public Guid? TenantId { get; }
    }

    private sealed class FixedLegalEntityContext : ILegalEntityContext
    {
        private readonly IReadOnlyCollection<Guid> _effective;
        private readonly bool _selectionAllowed;

        public FixedLegalEntityContext(
            Guid? selected,
            IReadOnlyCollection<Guid>? effective = null,
            bool? selectionAllowed = null)
        {
            SelectedLegalEntityId = selected;
            _effective = effective ?? (selected is { } s ? new[] { s } : Array.Empty<Guid>());
            _selectionAllowed = selectionAllowed ?? selected.HasValue;
        }

        public Guid? SelectedLegalEntityId { get; }

        public Task<bool> IsSelectionAllowedAsync(CancellationToken ct) => Task.FromResult(_selectionAllowed);

        public Task<IReadOnlyCollection<Guid>> GetEffectiveLegalEntityIdsAsync(CancellationToken ct) =>
            Task.FromResult(_effective);
    }

    private sealed class InMemoryTepShellMetadataRepository : ITepShellMetadataRepository
    {
        public List<TepShellMetadata> Items { get; } = [];

        public Task<IReadOnlyList<TepShellMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TepShellMetadata>>(Items
                .Where(item => item.TenantId == tenantId && !item.IsDeleted && legalEntityIds.Contains(item.LegalEntityId))
                .OrderBy(item => item.Code)
                .ToList());

        public Task<TepShellMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct) =>
            Task.FromResult(Items.SingleOrDefault(item => item.TenantId == tenantId && item.Id == id && !item.IsDeleted && legalEntityIds.Contains(item.LegalEntityId)));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(Items.Any(item =>
                item.TenantId == tenantId
                && item.LegalEntityId == legalEntityId
                && !item.IsDeleted
                && string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase)
                && item.Id != excludingId));

        public Task CreateAsync(TepShellMetadata metadata, CancellationToken ct)
        {
            Items.Add(metadata);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TepShellMetadata metadata, CancellationToken ct) => Task.CompletedTask;
    }
}
