using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;
using Diten.Platform.Domain.Entities.PayrollIntegrationGovernance;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Handlers.CommandHandlers;

public sealed class CreatePayrollIntegrationExceptionHandler : IRequestHandler<CreatePayrollIntegrationExceptionCommand, Response<Guid>>
{
    private readonly IPayrollIntegrationGovernanceRepository _repository;

    public CreatePayrollIntegrationExceptionHandler(IPayrollIntegrationGovernanceRepository repository) => _repository = repository;

    public async Task<Response<Guid>> Handle(CreatePayrollIntegrationExceptionCommand request, CancellationToken ct)
    {
        var run = await _repository.GetRunByIdAsync(request.RunId, ct);
        if (run == null)
        {
            return Response<Guid>.Fail("Payroll integration run not found.", 404);
        }

        var exception = new PayrollIntegrationException
        {
            TenantId = run.TenantId,
            RunId = run.Id,
            ExceptionCode = request.Request.ExceptionCode.Trim(),
            Severity = request.Request.Severity,
            ExceptionState = request.Request.ExceptionState,
            SourceReferenceId = request.Request.SourceReferenceId,
            AssignedToActorId = request.Request.AssignedToActorId,
            ResolutionWorkflowId = request.Request.ResolutionWorkflowId,
            RedactedMessage = request.Request.RedactedMessage.Trim(),
            ResolutionNote = string.IsNullOrWhiteSpace(request.Request.ResolutionNote) ? null : request.Request.ResolutionNote.Trim()
        };
        await _repository.CreateExceptionAsync(exception, ct);
        return Response<Guid>.Success(exception.Id, 201);
    }
}
