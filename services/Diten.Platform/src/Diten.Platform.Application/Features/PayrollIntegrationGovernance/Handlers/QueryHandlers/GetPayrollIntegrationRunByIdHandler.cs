using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Handlers.QueryHandlers;

public sealed class GetPayrollIntegrationRunByIdHandler : IRequestHandler<GetPayrollIntegrationRunByIdQuery, Response<PayrollIntegrationRunDto>>
{
    private readonly IPayrollIntegrationGovernanceRepository _repository;

    public GetPayrollIntegrationRunByIdHandler(IPayrollIntegrationGovernanceRepository repository) => _repository = repository;

    public async Task<Response<PayrollIntegrationRunDto>> Handle(GetPayrollIntegrationRunByIdQuery request, CancellationToken ct)
    {
        var run = await _repository.GetRunByIdAsync(request.RunId, ct);
        return run == null
            ? Response<PayrollIntegrationRunDto>.Fail("Payroll integration run not found.", 404)
            : Response<PayrollIntegrationRunDto>.Success(PayrollIntegrationGovernanceMapper.ToDto(run));
    }
}
