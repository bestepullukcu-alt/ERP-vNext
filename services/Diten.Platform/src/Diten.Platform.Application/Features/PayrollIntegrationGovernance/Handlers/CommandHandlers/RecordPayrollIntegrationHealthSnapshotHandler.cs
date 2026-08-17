using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;
using Diten.Platform.Domain.Entities.PayrollIntegrationGovernance;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Handlers.CommandHandlers;

public sealed class RecordPayrollIntegrationHealthSnapshotHandler : IRequestHandler<RecordPayrollIntegrationHealthSnapshotCommand, Response<Guid>>
{
    private readonly IPayrollIntegrationGovernanceRepository _repository;

    public RecordPayrollIntegrationHealthSnapshotHandler(IPayrollIntegrationGovernanceRepository repository) => _repository = repository;

    public async Task<Response<Guid>> Handle(RecordPayrollIntegrationHealthSnapshotCommand request, CancellationToken ct)
    {
        if (request.Request.RunId.HasValue && await _repository.GetRunByIdAsync(request.Request.RunId.Value, ct) == null)
        {
            return Response<Guid>.Fail("Payroll integration run not found.", 404);
        }

        var run = request.Request.RunId.HasValue ? await _repository.GetRunByIdAsync(request.Request.RunId.Value, ct) : null;
        var snapshot = new PayrollIntegrationHealthSnapshot
        {
            TenantId = run?.TenantId ?? Guid.Empty,
            RunId = request.Request.RunId,
            HealthState = request.Request.HealthState,
            CheckedAt = request.Request.CheckedAt,
            RedactedMessage = string.IsNullOrWhiteSpace(request.Request.RedactedMessage) ? null : request.Request.RedactedMessage.Trim(),
            CorrelationId = string.IsNullOrWhiteSpace(request.Request.CorrelationId) ? null : request.Request.CorrelationId.Trim()
        };
        if (snapshot.TenantId == Guid.Empty)
        {
            return Response<Guid>.Fail("RunId is required for tenant-scoped health snapshots in this slice.", 404);
        }

        await _repository.CreateHealthSnapshotAsync(snapshot, ct);
        return Response<Guid>.Success(snapshot.Id, 201);
    }
}
