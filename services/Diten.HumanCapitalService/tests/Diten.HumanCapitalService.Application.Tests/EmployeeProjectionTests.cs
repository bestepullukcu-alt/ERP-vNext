using System.Reflection;
using Diten.HumanCapitalService.Api.Controllers.Hcm;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.EmployeeProjections;
using Diten.HumanCapitalService.Application.Features.EmployeeProjections.Commands;
using Diten.HumanCapitalService.Application.Features.EmployeeProjections.Handlers;
using Diten.HumanCapitalService.Application.Features.EmployeeProjections.Queries;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using Diten.HumanCapitalService.Persistence.Repositories;
using Xunit;

namespace Diten.HumanCapitalService.Application.Tests;

public sealed class EmployeeProjectionTests
{
    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryEmployeeProjectionRepository();
        var handler = new CreateEmployeeProjectionHandler(repository, new ValidReferenceValidator(), new FixedTenantContext(tenantId));

        var first = await handler.Handle(new CreateEmployeeProjectionCommand(ValidRequest(code: "emp-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateEmployeeProjectionCommand(ValidRequest(code: " EMP-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var repository = new InMemoryEmployeeProjectionRepository();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var create = new CreateEmployeeProjectionHandler(repository, new ValidReferenceValidator(), new FixedTenantContext(tenantA));
        var created = await create.Handle(new CreateEmployeeProjectionCommand(ValidRequest()), CancellationToken.None);
        var get = new GetEmployeeProjectionByIdHandler(repository, new FixedTenantContext(tenantB));

        var response = await get.Handle(new GetEmployeeProjectionByIdQuery(created.Data), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Archive_sets_soft_delete_and_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryEmployeeProjectionRepository();
        var create = new CreateEmployeeProjectionHandler(repository, new ValidReferenceValidator(), new FixedTenantContext(tenantId));
        var created = await create.Handle(new CreateEmployeeProjectionCommand(ValidRequest()), CancellationToken.None);
        var archive = new ArchiveEmployeeProjectionHandler(repository, new FixedTenantContext(tenantId));

        var archived = await archive.Handle(new ArchiveEmployeeProjectionCommand(created.Data), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, created.Data, CancellationToken.None);
        var stored = RepositoryItems(repository)[created.Data];

        Assert.True(archived.IsSuccessful);
        Assert.Equal(204, archived.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
    }

    [Fact]
    public async Task Raw_secret_or_payload_markers_are_rejected()
    {
        var handler = new CreateEmployeeProjectionHandler(
            new InMemoryEmployeeProjectionRepository(),
            new ValidReferenceValidator(),
            new FixedTenantContext(Guid.NewGuid()));

        var response = await handler.Handle(
            new CreateEmployeeProjectionCommand(ValidRequest(externalReference: "{\"access_token\":\"secret\"}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Unavailable_reference_contract_defers_non_validated_state()
    {
        var handler = new CreateEmployeeProjectionHandler(
            new InMemoryEmployeeProjectionRepository(),
            new UnavailableReferenceValidator(),
            new FixedTenantContext(Guid.NewGuid()));

        var response = await handler.Handle(
            new CreateEmployeeProjectionCommand(ValidRequest(state: EmployeeProjectionState.SourceLinked)),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
    }

    [Fact]
    public async Task Unavailable_reference_contract_fails_closed_for_validated_state()
    {
        var handler = new CreateEmployeeProjectionHandler(
            new InMemoryEmployeeProjectionRepository(),
            new UnavailableReferenceValidator(),
            new FixedTenantContext(Guid.NewGuid()));

        var response = await handler.Handle(
            new CreateEmployeeProjectionCommand(ValidRequest(state: EmployeeProjectionState.Validated)),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_same_tenant_reference_fails_closed()
    {
        var handler = new CreateEmployeeProjectionHandler(
            new InMemoryEmployeeProjectionRepository(),
            new MissingReferenceValidator(),
            new FixedTenantContext(Guid.NewGuid()));

        var response = await handler.Handle(
            new CreateEmployeeProjectionCommand(ValidRequest(state: EmployeeProjectionState.Validated)),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public void Controller_uses_approved_permission_namespace()
    {
        var permissions = typeof(EmployeeProjectionsController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(method => method.GetCustomAttribute<HasPermissionAttribute>())
            .Where(attribute => attribute is not null)
            .Select(attribute => (string)typeof(HasPermissionAttribute)
                .GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(attribute!)!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("hcm.employee-projections.read", permissions);
        Assert.Contains("hcm.employee-projections.manage", permissions);
        Assert.Contains("hcm.employee-projections.archive", permissions);
        Assert.Contains("hcm.employee-projections.source-link.manage", permissions);
    }

    [Fact]
    public void Mongo_repository_defines_collection_and_tenant_code_unique_index()
    {
        Assert.Equal("hcm_employee_profile_projections", MongoEmployeeProjectionRepository.CollectionName);
        Assert.Equal("ux_hcm_employee_projections_tenant_code_active", MongoEmployeeProjectionRepository.ActiveCodeUniqueIndexName);
    }

    private static EmployeeProjectionCreateRequest ValidRequest(
        string code = "EMP-001",
        string externalReference = "EXT-001",
        EmployeeProjectionState state = EmployeeProjectionState.Validated) =>
        new()
        {
            Code = code,
            DisplayName = "Employee Routing Label",
            HrisSourceProfileId = Guid.NewGuid(),
            PersonReferenceId = Guid.NewGuid(),
            ExternalEmployeeReference = externalReference,
            EmploymentRecordReferenceKey = "EMPLOYMENT-001",
            EmploymentStatusCode = "ACTIVE",
            WorkerTypeCode = "EMPLOYEE",
            SourceContractVersion = "v1",
            ProjectionState = state,
            VisibilityClassification = EmployeeVisibilityClassification.StandardHr,
            ProjectionVersion = 1
        };

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid tenantId) => TenantId = tenantId;
        public Guid? TenantId { get; }
    }

    private sealed class ValidReferenceValidator : IEmployeeProjectionReferenceValidator
    {
        public Task<ReferenceValidationResult> ValidateHrisSourceProfileAsync(Guid tenantId, Guid hrisSourceProfileId, CancellationToken ct) =>
            Task.FromResult(ReferenceValidationResult.Valid());

        public Task<ReferenceValidationResult> ValidatePersonReferenceAsync(Guid tenantId, Guid personReferenceId, CancellationToken ct) =>
            Task.FromResult(ReferenceValidationResult.Valid());
    }

    private sealed class UnavailableReferenceValidator : IEmployeeProjectionReferenceValidator
    {
        public Task<ReferenceValidationResult> ValidateHrisSourceProfileAsync(Guid tenantId, Guid hrisSourceProfileId, CancellationToken ct) =>
            Task.FromResult(ReferenceValidationResult.ContractUnavailable());

        public Task<ReferenceValidationResult> ValidatePersonReferenceAsync(Guid tenantId, Guid personReferenceId, CancellationToken ct) =>
            Task.FromResult(ReferenceValidationResult.ContractUnavailable());
    }

    private sealed class MissingReferenceValidator : IEmployeeProjectionReferenceValidator
    {
        public Task<ReferenceValidationResult> ValidateHrisSourceProfileAsync(Guid tenantId, Guid hrisSourceProfileId, CancellationToken ct) =>
            Task.FromResult(ReferenceValidationResult.NotFound());

        public Task<ReferenceValidationResult> ValidatePersonReferenceAsync(Guid tenantId, Guid personReferenceId, CancellationToken ct) =>
            Task.FromResult(ReferenceValidationResult.Valid());
    }

    private static IReadOnlyDictionary<Guid, EmployeeProfileProjection> RepositoryItems(InMemoryEmployeeProjectionRepository repository)
    {
        var field = typeof(InMemoryEmployeeProjectionRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, EmployeeProfileProjection>)field.GetValue(repository)!;
    }

    private sealed class InMemoryEmployeeProjectionRepository : IEmployeeProjectionRepository
    {
        private readonly Dictionary<Guid, EmployeeProfileProjection> _items = [];

        public Task<IReadOnlyList<EmployeeProfileProjection>> ListAsync(Guid tenantId, CancellationToken ct)
        {
            _ = ct;
            return Task.FromResult<IReadOnlyList<EmployeeProfileProjection>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());
        }

        public Task<EmployeeProfileProjection?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
        {
            _ = ct;
            return Task.FromResult(
                _items.TryGetValue(id, out var item) && item.TenantId == tenantId && !item.IsDeleted
                    ? item
                    : null);
        }

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
        {
            _ = ct;
            return Task.FromResult(_items.Values.Any(item =>
                item.TenantId == tenantId
                && !item.IsDeleted
                && string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase)
                && item.Id != excludingId));
        }

        public Task CreateAsync(EmployeeProfileProjection projection, CancellationToken ct)
        {
            _ = ct;
            _items[projection.Id] = projection;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(EmployeeProfileProjection projection, CancellationToken ct)
        {
            _ = ct;
            _items[projection.Id] = projection;
            return Task.CompletedTask;
        }
    }
}
