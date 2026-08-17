using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Handlers.QueryHandlers;

public sealed class GetPayrollIntegrationHealthHandler : IRequestHandler<GetPayrollIntegrationHealthQuery, Response<PayrollIntegrationHealthSnapshotDto>>
{
    private readonly IPayrollIntegrationGovernanceRepository _repository;

    public GetPayrollIntegrationHealthHandler(IPayrollIntegrationGovernanceRepository repository) => _repository = repository;

    public async Task<Response<PayrollIntegrationHealthSnapshotDto>> Handle(GetPayrollIntegrationHealthQuery request, CancellationToken ct)
    {
        if (request.RunId.HasValue && await _repository.GetRunByIdAsync(request.RunId.Value, ct) == null)
        {
            return Response<PayrollIntegrationHealthSnapshotDto>.Fail("Payroll integration run not found.", 404);
        }

        var item = await _repository.GetLatestHealthSnapshotAsync(request.RunId, ct);
        return item == null
            ? Response<PayrollIntegrationHealthSnapshotDto>.Fail("Payroll integration health snapshot not found.", 404)
            : Response<PayrollIntegrationHealthSnapshotDto>.Success(PayrollIntegrationGovernanceMapper.ToDto(item));
    }
}
