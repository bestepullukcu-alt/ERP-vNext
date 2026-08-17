using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollSources.Commands;
using Diten.Platform.Domain.Entities.PayrollSources;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollSources.Handlers.CommandHandlers;

public sealed class RecordPayrollSourceHealthSnapshotHandler : IRequestHandler<RecordPayrollSourceHealthSnapshotCommand, Response<Guid>>
{
    private readonly IPayrollSourceRepository _repository;

    public RecordPayrollSourceHealthSnapshotHandler(IPayrollSourceRepository repository) => _repository = repository;

    public async Task<Response<Guid>> Handle(RecordPayrollSourceHealthSnapshotCommand request, CancellationToken ct)
    {
        var source = await _repository.GetExternalSystemProfileByIdAsync(request.SourceProfileId, ct);
        if (source == null)
        {
            return Response<Guid>.Fail("Payroll source not found.", 404);
        }

        var snapshot = new PayrollSourceHealthSnapshot
        {
            TenantId = source.TenantId,
            PayrollExternalSystemProfileId = source.Id,
            HealthState = request.Request.HealthState,
            CheckedAt = request.Request.CheckedAt,
            RedactedMessage = string.IsNullOrWhiteSpace(request.Request.RedactedMessage) ? null : request.Request.RedactedMessage.Trim(),
            CorrelationId = string.IsNullOrWhiteSpace(request.Request.CorrelationId) ? null : request.Request.CorrelationId.Trim()
        };

        await _repository.CreateHealthSnapshotAsync(snapshot, ct);
        return Response<Guid>.Success(snapshot.Id, 201);
    }
}
