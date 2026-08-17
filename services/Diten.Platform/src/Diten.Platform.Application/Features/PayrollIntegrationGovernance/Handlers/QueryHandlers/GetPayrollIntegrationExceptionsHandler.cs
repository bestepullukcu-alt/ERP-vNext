using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Handlers.QueryHandlers;

public sealed class GetPayrollIntegrationExceptionsHandler : IRequestHandler<GetPayrollIntegrationExceptionsQuery, Response<IReadOnlyList<PayrollIntegrationExceptionDto>>>
{
    private readonly IPayrollIntegrationGovernanceRepository _repository;

    public GetPayrollIntegrationExceptionsHandler(IPayrollIntegrationGovernanceRepository repository) => _repository = repository;

    public async Task<Response<IReadOnlyList<PayrollIntegrationExceptionDto>>> Handle(GetPayrollIntegrationExceptionsQuery request, CancellationToken ct)
    {
        if (await _repository.GetRunByIdAsync(request.RunId, ct) == null)
        {
            return Response<IReadOnlyList<PayrollIntegrationExceptionDto>>.Fail("Payroll integration run not found.", 404);
        }

        var items = await _repository.GetExceptionsAsync(request.RunId, ct);
        return Response<IReadOnlyList<PayrollIntegrationExceptionDto>>.Success(items.Select(PayrollIntegrationGovernanceMapper.ToDto).ToList());
    }
}
