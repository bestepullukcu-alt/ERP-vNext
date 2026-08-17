using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;
using Diten.Platform.Domain.Entities.PayrollIntegrationGovernance;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Handlers.CommandHandlers;

public sealed class RecordPayrollIntegrationEvidenceExportReferenceHandler : IRequestHandler<RecordPayrollIntegrationEvidenceExportReferenceCommand, Response<Guid>>
{
    private readonly IPayrollIntegrationGovernanceRepository _repository;

    public RecordPayrollIntegrationEvidenceExportReferenceHandler(IPayrollIntegrationGovernanceRepository repository) => _repository = repository;

    public async Task<Response<Guid>> Handle(RecordPayrollIntegrationEvidenceExportReferenceCommand request, CancellationToken ct)
    {
        var run = await _repository.GetRunByIdAsync(request.RunId, ct);
        if (run == null)
        {
            return Response<Guid>.Fail("Payroll integration run not found.", 404);
        }

        var reference = new PayrollIntegrationEvidenceExportReference
        {
            TenantId = run.TenantId,
            RunId = run.Id,
            ExportPurposeCode = request.Request.ExportPurposeCode.Trim(),
            AuditEventId = request.Request.AuditEventId,
            EvidenceReferenceId = request.Request.EvidenceReferenceId,
            RecordsRetentionReferenceId = request.Request.RecordsRetentionReferenceId,
            ExportState = request.Request.ExportState,
            RequestedByActorId = request.Request.RequestedByActorId,
            CorrelationId = request.Request.CorrelationId.Trim(),
            RedactedNotes = string.IsNullOrWhiteSpace(request.Request.RedactedNotes) ? null : request.Request.RedactedNotes.Trim()
        };
        await _repository.CreateEvidenceExportReferenceAsync(reference, ct);
        return Response<Guid>.Success(reference.Id, 201);
    }
}
