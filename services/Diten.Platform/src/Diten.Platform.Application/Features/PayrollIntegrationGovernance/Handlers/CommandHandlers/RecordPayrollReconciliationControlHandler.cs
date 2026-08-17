using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;
using Diten.Platform.Domain.Entities.PayrollIntegrationGovernance;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Handlers.CommandHandlers;

public sealed class RecordPayrollReconciliationControlHandler : IRequestHandler<RecordPayrollReconciliationControlCommand, Response<Guid>>
{
    private readonly IPayrollIntegrationGovernanceRepository _repository;

    public RecordPayrollReconciliationControlHandler(IPayrollIntegrationGovernanceRepository repository) => _repository = repository;

    public async Task<Response<Guid>> Handle(RecordPayrollReconciliationControlCommand request, CancellationToken ct)
    {
        var run = await _repository.GetRunByIdAsync(request.RunId, ct);
        if (run == null)
        {
            return Response<Guid>.Fail("Payroll integration run not found.", 404);
        }

        var stateValidation = PayrollIntegrationReferenceGuard.RejectBlindApprovedState(request.Request.ControlState);
        if (!stateValidation.IsSuccessful)
        {
            return Response<Guid>.Fail(stateValidation.Errors, stateValidation.StatusCode);
        }

        var control = new PayrollReconciliationControl
        {
            TenantId = run.TenantId,
            RunId = run.Id,
            ReconciliationType = request.Request.ReconciliationType,
            ExpectedCount = request.Request.ExpectedCount,
            ObservedCount = request.Request.ObservedCount,
            ControlState = request.Request.ControlState,
            EvidenceReferenceId = request.Request.EvidenceReferenceId,
            Notes = string.IsNullOrWhiteSpace(request.Request.Notes) ? null : request.Request.Notes.Trim()
        };
        await _repository.CreateReconciliationControlAsync(control, ct);
        return Response<Guid>.Success(control.Id, 201);
    }
}
