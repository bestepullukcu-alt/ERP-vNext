using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Handlers.CommandHandlers;

public sealed class UpdatePayrollIntegrationRunStatusHandler : IRequestHandler<UpdatePayrollIntegrationRunStatusCommand, Response<NoContent>>
{
    private readonly IPayrollIntegrationGovernanceRepository _repository;

    public UpdatePayrollIntegrationRunStatusHandler(IPayrollIntegrationGovernanceRepository repository) => _repository = repository;

    public async Task<Response<NoContent>> Handle(UpdatePayrollIntegrationRunStatusCommand request, CancellationToken ct)
    {
        var run = await _repository.GetRunByIdAsync(request.RunId, ct);
        if (run == null)
        {
            return Response<NoContent>.Fail("Payroll integration run not found.", 404);
        }

        run.Status = request.Request.Status;
        run.StartedAt = request.Request.StartedAt;
        run.CompletedAt = request.Request.CompletedAt;
        run.Summary = string.IsNullOrWhiteSpace(request.Request.Summary) ? null : request.Request.Summary.Trim();
        await _repository.UpdateRunAsync(run, ct);
        return Response<NoContent>.Success(204);
    }
}
