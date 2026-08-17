using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Handlers.QueryHandlers;

public sealed class GetPayrollIntegrationSourceLinksHandler : IRequestHandler<GetPayrollIntegrationSourceLinksQuery, Response<IReadOnlyList<PayrollIntegrationSourceLinkDto>>>
{
    private readonly IPayrollIntegrationGovernanceRepository _repository;

    public GetPayrollIntegrationSourceLinksHandler(IPayrollIntegrationGovernanceRepository repository) => _repository = repository;

    public async Task<Response<IReadOnlyList<PayrollIntegrationSourceLinkDto>>> Handle(GetPayrollIntegrationSourceLinksQuery request, CancellationToken ct)
    {
        if (await _repository.GetRunByIdAsync(request.RunId, ct) == null)
        {
            return Response<IReadOnlyList<PayrollIntegrationSourceLinkDto>>.Fail("Payroll integration run not found.", 404);
        }

        var items = await _repository.GetSourceLinksAsync(request.RunId, ct);
        return Response<IReadOnlyList<PayrollIntegrationSourceLinkDto>>.Success(items.Select(PayrollIntegrationGovernanceMapper.ToDto).ToList());
    }
}
