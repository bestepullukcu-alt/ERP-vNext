using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Handlers.CommandHandlers;

public sealed class ResolvePayrollIntegrationExceptionHandler : IRequestHandler<ResolvePayrollIntegrationExceptionCommand, Response<NoContent>>
{
    private readonly IPayrollIntegrationGovernanceRepository _repository;

    public ResolvePayrollIntegrationExceptionHandler(IPayrollIntegrationGovernanceRepository repository) => _repository = repository;

    public async Task<Response<NoContent>> Handle(ResolvePayrollIntegrationExceptionCommand request, CancellationToken ct)
    {
        if (await _repository.GetRunByIdAsync(request.RunId, ct) == null)
        {
            return Response<NoContent>.Fail("Payroll integration run not found.", 404);
        }

        var exception = await _repository.GetExceptionByIdAsync(request.RunId, request.ExceptionId, ct);
        if (exception == null)
        {
            return Response<NoContent>.Fail("Payroll integration exception not found.", 404);
        }

        exception.ExceptionState = request.Request.ExceptionState;
        exception.ResolutionWorkflowId = request.Request.ResolutionWorkflowId;
        exception.ResolutionNote = string.IsNullOrWhiteSpace(request.Request.ResolutionNote) ? null : request.Request.ResolutionNote.Trim();
        await _repository.UpdateExceptionAsync(exception, ct);
        return Response<NoContent>.Success(204);
    }
}
