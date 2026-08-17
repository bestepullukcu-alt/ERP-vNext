using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Handlers.QueryHandlers;

public sealed class GetPayrollIntegrationRunListHandler : IRequestHandler<GetPayrollIntegrationRunListQuery, Response<IReadOnlyList<PayrollIntegrationRunDto>>>
{
    private readonly IPayrollIntegrationGovernanceRepository _repository;

    public GetPayrollIntegrationRunListHandler(IPayrollIntegrationGovernanceRepository repository) => _repository = repository;

    public async Task<Response<IReadOnlyList<PayrollIntegrationRunDto>>> Handle(GetPayrollIntegrationRunListQuery request, CancellationToken ct)
    {
        var items = await _repository.GetRunsAsync(ct);
        return Response<IReadOnlyList<PayrollIntegrationRunDto>>.Success(items.Select(PayrollIntegrationGovernanceMapper.ToDto).ToList());
    }
}
