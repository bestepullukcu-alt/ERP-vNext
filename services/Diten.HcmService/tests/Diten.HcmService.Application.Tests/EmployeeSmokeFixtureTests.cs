using Diten.HcmService.Application.Common;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Commands;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Handlers;
using Diten.HcmService.Domain.Entities;
using Diten.HcmService.Domain.Repositories;
using Xunit;

namespace Diten.HcmService.Application.Tests;

public sealed class EmployeeSmokeFixtureTests
{
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly TenantContext _tenantContext = new();
    private readonly InMemorySmokeFixtureRepository _repository = new();

    public EmployeeSmokeFixtureTests()
    {
        _tenantContext.SetTenant(_tenantId);
    }

    [Fact]
    public async Task EnsureMinimalEmployee_FailsClosed_WhenFixtureEnvironmentDisabled()
    {
        var handler = new EnsureEmployeeSmokeFixtureHandler(_tenantContext, _repository);

        var response = await handler.Handle(new EnsureEmployeeSmokeFixtureCommand(IsLocalFixtureEnabled: false), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Empty(_repository.Items);
    }

    [Fact]
    public async Task EnsureMinimalEmployee_CreatesTenantScopedSyntheticDraftEmployee()
    {
        var handler = new EnsureEmployeeSmokeFixtureHandler(_tenantContext, _repository);

        var response = await handler.Handle(new EnsureEmployeeSmokeFixtureCommand(IsLocalFixtureEnabled: true), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        Assert.Equal(_tenantId, response.Data!.TenantId);
        Assert.Equal("MOD0251-SMOKE-DETAIL-EMPLOYEE", response.Data.EmployeeNumber);
        Assert.Equal("draft", response.Data.EmployeeStatus);
        var employee = Assert.Single(_repository.Items);
        Assert.Equal("Smoke", employee.LegalFirstName);
        Assert.Equal("Employee", employee.LegalLastName);
        Assert.Null(employee.DateOfBirth);
        Assert.Null(employee.WorkEmail);
        Assert.Null(employee.PersonalEmail);
        Assert.Null(employee.Phone);
        Assert.Equal("standard", employee.SensitivityLevel);
        Assert.False(employee.IsDeleted);
    }

    [Fact]
    public async Task EnsureMinimalEmployee_ReusesExistingFixture()
    {
        var handler = new EnsureEmployeeSmokeFixtureHandler(_tenantContext, _repository);
        var first = await handler.Handle(new EnsureEmployeeSmokeFixtureCommand(IsLocalFixtureEnabled: true), CancellationToken.None);

        var second = await handler.Handle(new EnsureEmployeeSmokeFixtureCommand(IsLocalFixtureEnabled: true), CancellationToken.None);

        Assert.True(second.IsSuccessful);
        Assert.Equal(200, second.StatusCode);
        Assert.Equal(first.Data!.EmployeeId, second.Data!.EmployeeId);
        Assert.True(second.Data.Reused);
        Assert.Single(_repository.Items);
    }

    [Fact]
    public async Task CleanupMinimalEmployee_SoftDeletesOnlySameTenantFixture_AndIsIdempotent()
    {
        var ensure = new EnsureEmployeeSmokeFixtureHandler(_tenantContext, _repository);
        var cleanup = new CleanupEmployeeSmokeFixtureHandler(_tenantContext, _repository);
        var created = await ensure.Handle(new EnsureEmployeeSmokeFixtureCommand(IsLocalFixtureEnabled: true), CancellationToken.None);

        var firstCleanup = await cleanup.Handle(new CleanupEmployeeSmokeFixtureCommand(IsLocalFixtureEnabled: true), CancellationToken.None);
        var secondCleanup = await cleanup.Handle(new CleanupEmployeeSmokeFixtureCommand(IsLocalFixtureEnabled: true), CancellationToken.None);

        Assert.True(firstCleanup.IsSuccessful);
        Assert.True(firstCleanup.Data!.Deleted);
        Assert.True(firstCleanup.Data.WasPresent);
        Assert.Equal(created.Data!.EmployeeId, firstCleanup.Data.EmployeeId);
        Assert.True(_repository.Items.Single().IsDeleted);
        Assert.True(secondCleanup.IsSuccessful);
        Assert.False(secondCleanup.Data!.Deleted);
        Assert.False(secondCleanup.Data.WasPresent);
    }

    private sealed class InMemorySmokeFixtureRepository : IEmployeeSmokeFixtureRepository
    {
        private static readonly Guid FixturePersonId = Guid.Parse("02510000-0000-0000-0000-000000000001");
        private static readonly Guid FixtureActorId = Guid.Parse("02510000-0000-0000-0000-000000000007");

        public List<Employee> Items { get; } = [];

        public Task<EmployeeSmokeFixtureEnsureResult> EnsureMinimalEmployeeAsync(
            Guid tenantId,
            CancellationToken cancellationToken)
        {
            var existing = Items
                .Where(employee =>
                    employee.TenantId == tenantId
                    && employee.EmployeeNumber == EnsureEmployeeSmokeFixtureHandler.FixtureEmployeeNumber)
                .OrderByDescending(employee => employee.UpdatedAt)
                .FirstOrDefault();

            if (existing is not null)
            {
                existing.IsDeleted = false;
                existing.DeletedAt = null;
                existing.PersonId = FixturePersonId;
                existing.LegalFirstName = "Smoke";
                existing.LegalMiddleName = null;
                existing.LegalLastName = "Employee";
                existing.PreferredName = "Smoke Employee";
                existing.DateOfBirth = null;
                existing.WorkEmail = null;
                existing.PersonalEmail = null;
                existing.Phone = null;
                existing.EmployeeStatus = "draft";
                existing.WorkerType = "employee";
                existing.EmploymentType = "full_time";
                existing.SensitivityLevel = "standard";
                existing.UpdatedBy = FixtureActorId;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                existing.Version += 1;
                existing.ETag = Employee.CreateETag(existing.Version);
                return Task.FromResult(new EmployeeSmokeFixtureEnsureResult(existing, Created: false, Reused: true));
            }

            var now = DateTimeOffset.UtcNow;
            var employee = new Employee
            {
                TenantId = tenantId,
                PersonId = FixturePersonId,
                EmployeeNumber = EnsureEmployeeSmokeFixtureHandler.FixtureEmployeeNumber,
                LegalFirstName = "Smoke",
                LegalLastName = "Employee",
                PreferredName = "Smoke Employee",
                EmployeeStatus = "draft",
                WorkerType = "employee",
                EmploymentType = "full_time",
                SensitivityLevel = "standard",
                CreatedBy = FixtureActorId,
                UpdatedBy = FixtureActorId,
                CreatedAt = now,
                UpdatedAt = now
            };
            Items.Add(employee);
            return Task.FromResult(new EmployeeSmokeFixtureEnsureResult(employee, Created: true, Reused: false));
        }

        public Task<EmployeeSmokeFixtureCleanupResult> CleanupMinimalEmployeeAsync(
            Guid tenantId,
            CancellationToken cancellationToken)
        {
            var employee = Items
                .FirstOrDefault(item =>
                    item.TenantId == tenantId
                    && item.EmployeeNumber == EnsureEmployeeSmokeFixtureHandler.FixtureEmployeeNumber
                    && !item.IsDeleted);

            if (employee is null)
            {
                return Task.FromResult(new EmployeeSmokeFixtureCleanupResult(
                    null,
                    EnsureEmployeeSmokeFixtureHandler.FixtureEmployeeNumber,
                    Deleted: false,
                    WasPresent: false));
            }

            employee.IsDeleted = true;
            employee.DeletedAt = DateTimeOffset.UtcNow;
            employee.UpdatedAt = DateTimeOffset.UtcNow;
            employee.UpdatedBy = FixtureActorId;
            employee.Version += 1;
            employee.ETag = Employee.CreateETag(employee.Version);
            return Task.FromResult(new EmployeeSmokeFixtureCleanupResult(
                employee.Id,
                employee.EmployeeNumber,
                Deleted: true,
                WasPresent: true));
        }
    }
}
