using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Handlers.QueryHandlers;

public sealed class GetPayrollIntegrationMappingControlsHandler : IRequestHandler<GetPayrollIntegrationMappingControlsQuery, Response<IReadOnlyList<PayrollIntegrationMappingControlDto>>>
{
    private readonly IPayrollIntegrationGovernanceRepository _repository;

    public GetPayrollIntegrationMappingControlsHandler(IPayrollIntegrationGovernanceRepository repository) => _repository = repository;

    public async Task<Response<IReadOnlyList<PayrollIntegrationMappingControlDto>>> Handle(GetPayrollIntegrationMappingControlsQuery request, CancellationToken ct)
    {
        if (await _repository.GetRunByIdAsync(request.RunId, ct) == null)
        {
            return Response<IReadOnlyList<PayrollIntegrationMappingControlDto>>.Fail("Payroll integration run not found.", 404);
        }

        var items = await _repository.GetMappingControlsAsync(request.RunId, ct);
        return Response<IReadOnlyList<PayrollIntegrationMappingControlDto>>.Success(items.Select(PayrollIntegrationGovernanceMapper.ToDto).ToList());
    }
}
