using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollSources.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollSources.Handlers.QueryHandlers;

public sealed class GetPayrollEmployeeReferenceMapsHandler : IRequestHandler<GetPayrollEmployeeReferenceMapsQuery, Response<IReadOnlyList<PayrollEmployeeReferenceMapDto>>>
{
    private readonly IPayrollSourceRepository _repository;

    public GetPayrollEmployeeReferenceMapsHandler(IPayrollSourceRepository repository) => _repository = repository;

    public async Task<Response<IReadOnlyList<PayrollEmployeeReferenceMapDto>>> Handle(GetPayrollEmployeeReferenceMapsQuery request, CancellationToken ct)
    {
        if (await _repository.GetExternalSystemProfileByIdAsync(request.SourceProfileId, ct) == null)
        {
            return Response<IReadOnlyList<PayrollEmployeeReferenceMapDto>>.Fail("Payroll source not found.", 404);
        }

        var items = await _repository.GetEmployeeReferenceMapsAsync(request.SourceProfileId, ct);
        return Response<IReadOnlyList<PayrollEmployeeReferenceMapDto>>.Success(items.Select(PayrollSourceMapper.ToDto).ToList());
    }
}
