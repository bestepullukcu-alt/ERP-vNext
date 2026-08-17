using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollIntegrationGovernance.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Handlers.QueryHandlers;

public sealed class GetPayrollIntegrationEvidenceExportsHandler : IRequestHandler<GetPayrollIntegrationEvidenceExportsQuery, Response<IReadOnlyList<PayrollIntegrationEvidenceExportReferenceDto>>>
{
    private readonly IPayrollIntegrationGovernanceRepository _repository;

    public GetPayrollIntegrationEvidenceExportsHandler(IPayrollIntegrationGovernanceRepository repository) => _repository = repository;

    public async Task<Response<IReadOnlyList<PayrollIntegrationEvidenceExportReferenceDto>>> Handle(GetPayrollIntegrationEvidenceExportsQuery request, CancellationToken ct)
    {
        if (await _repository.GetRunByIdAsync(request.RunId, ct) == null)
        {
            return Response<IReadOnlyList<PayrollIntegrationEvidenceExportReferenceDto>>.Fail("Payroll integration run not found.", 404);
        }

        var items = await _repository.GetEvidenceExportReferencesAsync(request.RunId, ct);
        return Response<IReadOnlyList<PayrollIntegrationEvidenceExportReferenceDto>>.Success(items.Select(PayrollIntegrationGovernanceMapper.ToDto).ToList());
    }
}
