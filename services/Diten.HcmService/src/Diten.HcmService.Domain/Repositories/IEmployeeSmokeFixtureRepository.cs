using Diten.HcmService.Domain.Entities;

namespace Diten.HcmService.Domain.Repositories;

public interface IEmployeeSmokeFixtureRepository
{
    Task<EmployeeSmokeFixtureEnsureResult> EnsureMinimalEmployeeAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<EmployeeSmokeFixtureCleanupResult> CleanupMinimalEmployeeAsync(
        Guid tenantId,
        CancellationToken cancellationToken);
}

public sealed record EmployeeSmokeFixtureEnsureResult(
    Employee Employee,
    bool Created,
    bool Reused);

public sealed record EmployeeSmokeFixtureCleanupResult(
    Guid? EmployeeId,
    string EmployeeNumber,
    bool Deleted,
    bool WasPresent);
