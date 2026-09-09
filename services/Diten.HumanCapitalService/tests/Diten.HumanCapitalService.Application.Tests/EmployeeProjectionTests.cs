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
        var handler = new CreateEmployeeProjectionHandler(repository, new ValidReferenceValidator(), new FixedTenantContext(tenantId), PilotLegalEntityContext());

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
        var create = new CreateEmployeeProjectionHandler(repository, new ValidReferenceValidator(), new FixedTenantContext(tenantA), PilotLegalEntityContext());
        var created = await create.Handle(new CreateEmployeeProjectionCommand(ValidRequest()), CancellationToken.None);
        var get = new GetEmployeeProjectionByIdHandler(repository, new FixedTenantContext(tenantB), PilotLegalEntityContext());

        var response = await get.Handle(new GetEmployeeProjectionByIdQuery(created.Data), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Archive_sets_soft_delete_and_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryEmployeeProjectionRepository();
        var create = new CreateEmployeeProjectionHandler(repository, new ValidReferenceValidator(), new FixedTenantContext(tenantId), PilotLegalEntityContext());
        var created = await create.Handle(new CreateEmployeeProjectionCommand(ValidRequest()), CancellationToken.None);
        var archive = new ArchiveEmployeeProjectionHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext());

        var archived = await archive.Handle(new ArchiveEmployeeProjectionCommand(created.Data), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, new[] { Holding }, created.Data, CancellationToken.None);
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
            new FixedTenantContext(Guid.NewGuid()),
            PilotLegalEntityContext());

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
            new FixedTenantContext(Guid.NewGuid()),
            PilotLegalEntityContext());

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
            new FixedTenantContext(Guid.NewGuid()),
            PilotLegalEntityContext());

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
            new FixedTenantContext(Guid.NewGuid()),
            PilotLegalEntityContext());

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

    [Fact]
    public async Task Create_stamps_the_selected_legal_entity()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryEmployeeProjectionRepository();
        var handler = new CreateEmployeeProjectionHandler(
            repository,
            new ValidReferenceValidator(),
            new FixedTenantContext(tenantId),
            new FixedLegalEntityContext(Medikal, new[] { Medikal }));

        var created = await handler.Handle(new CreateEmployeeProjectionCommand(ValidRequest()), CancellationToken.None);
        var stored = RepositoryItems(repository)[created.Data];

        Assert.True(created.IsSuccessful);
        Assert.Equal(Medikal, stored.LegalEntityId);
    }

    [Fact]
    public async Task Create_without_a_permitted_legal_entity_is_forbidden()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryEmployeeProjectionRepository();
        // Selection is present but not within the caller's actable set → fail closed, nothing written.
        var handler = new CreateEmployeeProjectionHandler(
            repository,
            new ValidReferenceValidator(),
            new FixedTenantContext(tenantId),
            new FixedLegalEntityContext(Teknoloji, selectionAllowed: false));

        var response = await handler.Handle(new CreateEmployeeProjectionCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
        Assert.Empty(RepositoryItems(repository));
    }

    [Fact]
    public async Task List_rolls_up_holding_and_isolates_sibling_legal_entities()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryEmployeeProjectionRepository();

        await new CreateEmployeeProjectionHandler(repository, new ValidReferenceValidator(), new FixedTenantContext(tenantId), new FixedLegalEntityContext(Medikal, new[] { Medikal }))
            .Handle(new CreateEmployeeProjectionCommand(ValidRequest(code: "MED-01")), CancellationToken.None);
        await new CreateEmployeeProjectionHandler(repository, new ValidReferenceValidator(), new FixedTenantContext(tenantId), new FixedLegalEntityContext(Teknoloji, new[] { Teknoloji }))
            .Handle(new CreateEmployeeProjectionCommand(ValidRequest(code: "TEK-01")), CancellationToken.None);

        var medikalOnly = await ListWith(repository, tenantId, new[] { Medikal });
        var holdingRollup = await ListWith(repository, tenantId, new[] { Holding, Medikal, Teknoloji });

        Assert.Equal(new[] { "MED-01" }, medikalOnly.Select(x => x.Code).ToArray());
        Assert.Equal(new[] { "MED-01", "TEK-01" }, holdingRollup.Select(x => x.Code).OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task Same_code_is_unique_per_legal_entity_not_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryEmployeeProjectionRepository();
        var medikal = new FixedLegalEntityContext(Medikal, new[] { Medikal });
        var teknoloji = new FixedLegalEntityContext(Teknoloji, new[] { Teknoloji });

        var first = await new CreateEmployeeProjectionHandler(repository, new ValidReferenceValidator(), new FixedTenantContext(tenantId), medikal)
            .Handle(new CreateEmployeeProjectionCommand(ValidRequest(code: "SHARED-01")), CancellationToken.None);
        var duplicateSameEntity = await new CreateEmployeeProjectionHandler(repository, new ValidReferenceValidator(), new FixedTenantContext(tenantId), medikal)
            .Handle(new CreateEmployeeProjectionCommand(ValidRequest(code: "SHARED-01")), CancellationToken.None);
        var sameCodeOtherEntity = await new CreateEmployeeProjectionHandler(repository, new ValidReferenceValidator(), new FixedTenantContext(tenantId), teknoloji)
            .Handle(new CreateEmployeeProjectionCommand(ValidRequest(code: "SHARED-01")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.Equal(409, duplicateSameEntity.StatusCode);
        Assert.True(sameCodeOtherEntity.IsSuccessful);
    }

    private static async Task<IReadOnlyList<EmployeeProjectionListItemDto>> ListWith(
        IEmployeeProjectionRepository repository,
        Guid tenantId,
        IReadOnlyCollection<Guid> effective)
    {
        var handler = new GetEmployeeProjectionListHandler(
            repository,
            new FixedTenantContext(tenantId),
            new FixedLegalEntityContext(effective.First(), effective));
        var response = await handler.Handle(new GetEmployeeProjectionListQuery(), CancellationToken.None);
        return response.Data!;
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

    // Fixed legal-entity ids mirroring the MDM demo hierarchy: HOLDING(root) → { MEDIKAL, TEKNOLOJI }.
    private static readonly Guid Holding = Guid.Parse("1e9a1000-0000-0000-0000-000000000001");
    private static readonly Guid Medikal = Guid.Parse("1e9a1000-0000-0000-0000-000000000002");
    private static readonly Guid Teknoloji = Guid.Parse("1e9a1000-0000-0000-0000-000000000003");

    // Default pilot context: HOLDING selected, rolls up over the whole demo hierarchy.
    private static FixedLegalEntityContext PilotLegalEntityContext() =>
        new(Holding, new[] { Holding, Medikal, Teknoloji });

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid tenantId) => TenantId = tenantId;
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

        public Task<IReadOnlyList<EmployeeProfileProjection>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct)
        {
            _ = ct;
            return Task.FromResult<IReadOnlyList<EmployeeProfileProjection>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)).OrderBy(x => x.Code).ToList());
        }

        public Task<EmployeeProfileProjection?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct)
        {
            _ = ct;
            return Task.FromResult(
                _items.TryGetValue(id, out var item) && item.TenantId == tenantId && !item.IsDeleted && legalEntityIds.Contains(item.LegalEntityId)
                    ? item
                    : null);
        }

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct)
        {
            _ = ct;
            return Task.FromResult(_items.Values.Any(item =>
                item.TenantId == tenantId
                && item.LegalEntityId == legalEntityId
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
