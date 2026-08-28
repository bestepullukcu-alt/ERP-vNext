using Diten.MdmService.Domain.Repositories;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class AuditIntentTemporalMigrationRunner
{
    private readonly IAuditIntentTemporalMigrationRepository _repository;

    public AuditIntentTemporalMigrationRunner(IAuditIntentTemporalMigrationRepository repository)
    {
        _repository = repository;
    }

    public Task<AuditIntentTemporalMigrationResult> RunAsync(
        AuditIntentTemporalMigrationRequest request,
        CancellationToken cancellationToken = default)
        => _repository.RunAsync(request, cancellationToken);
}
