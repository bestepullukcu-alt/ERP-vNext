using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Handlers.QueryHandlers;

public sealed class GetPayrollReconciliationControlsHandler : IRequestHandler<GetPayrollReconciliationControlsQuery, Response<IReadOnlyList<PayrollReconciliationControlDto>>>
{
    private readonly IPayrollIntegrationGovernanceRepository _repository;

    public GetPayrollReconciliationControlsHandler(IPayrollIntegrationGovernanceRepository repository) => _repository = repository;

    public async Task<Response<IReadOnlyList<PayrollReconciliationControlDto>>> Handle(GetPayrollReconciliationControlsQuery request, CancellationToken ct)
    {
        if (await _repository.GetRunByIdAsync(request.RunId, ct) == null)
        {
            return Response<IReadOnlyList<PayrollReconciliationControlDto>>.Fail("Payroll integration run not found.", 404);
        }

        var items = await _repository.GetReconciliationControlsAsync(request.RunId, ct);
        return Response<IReadOnlyList<PayrollReconciliationControlDto>>.Success(items.Select(PayrollIntegrationGovernanceMapper.ToDto).ToList());
    }
}
