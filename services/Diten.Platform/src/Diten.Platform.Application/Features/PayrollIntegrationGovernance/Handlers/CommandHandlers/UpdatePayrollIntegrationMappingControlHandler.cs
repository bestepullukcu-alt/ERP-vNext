using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;
using Diten.Platform.Domain.Entities.PayrollIntegrationGovernance;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Handlers.CommandHandlers;

public sealed class UpdatePayrollIntegrationMappingControlHandler : IRequestHandler<UpdatePayrollIntegrationMappingControlCommand, Response<NoContent>>
{
    private readonly IPayrollIntegrationGovernanceRepository _repository;

    public UpdatePayrollIntegrationMappingControlHandler(IPayrollIntegrationGovernanceRepository repository) => _repository = repository;

    public async Task<Response<NoContent>> Handle(UpdatePayrollIntegrationMappingControlCommand request, CancellationToken ct)
    {
        var run = await _repository.GetRunByIdAsync(request.RunId, ct);
        if (run == null)
        {
            return Response<NoContent>.Fail("Payroll integration run not found.", 404);
        }

        var stateValidation = PayrollIntegrationReferenceGuard.RejectBlindApprovedState(request.Request.ControlState);
        if (!stateValidation.IsSuccessful)
        {
            return stateValidation;
        }

        var control = await _repository.GetMappingControlByIdAsync(run.Id, request.ControlId, ct);
        if (control != null && (control.MappingScope != request.Request.MappingScope || control.SourceReferenceId != request.Request.SourceReferenceId))
        {
            return Response<NoContent>.Fail("Mapping scope and source reference are immutable once created.", 409);
        }

        control ??= new PayrollIntegrationMappingControl
        {
            Id = request.ControlId,
            TenantId = run.TenantId,
            RunId = run.Id,
            MappingScope = request.Request.MappingScope,
            SourceReferenceId = request.Request.SourceReferenceId
        };

        control.TargetReferenceId = request.Request.TargetReferenceId;
        control.ControlState = request.Request.ControlState;
        control.MismatchCode = string.IsNullOrWhiteSpace(request.Request.MismatchCode) ? null : request.Request.MismatchCode.Trim();
        control.ResolutionNote = string.IsNullOrWhiteSpace(request.Request.ResolutionNote) ? null : request.Request.ResolutionNote.Trim();
        await _repository.UpsertMappingControlAsync(control, ct);
        return Response<NoContent>.Success(204);
    }
}
