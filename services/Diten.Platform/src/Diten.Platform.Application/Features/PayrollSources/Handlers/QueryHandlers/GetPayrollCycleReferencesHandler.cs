using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollSources.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollSources.Handlers.QueryHandlers;

public sealed class GetPayrollCycleReferencesHandler : IRequestHandler<GetPayrollCycleReferencesQuery, Response<IReadOnlyList<PayrollCycleReferenceDto>>>
{
    private readonly IPayrollSourceRepository _repository;

    public GetPayrollCycleReferencesHandler(IPayrollSourceRepository repository) => _repository = repository;

    public async Task<Response<IReadOnlyList<PayrollCycleReferenceDto>>> Handle(GetPayrollCycleReferencesQuery request, CancellationToken ct)
    {
        if (await _repository.GetExternalSystemProfileByIdAsync(request.SourceProfileId, ct) == null)
        {
            return Response<IReadOnlyList<PayrollCycleReferenceDto>>.Fail("Payroll source not found.", 404);
        }

        var items = await _repository.GetCycleReferencesAsync(request.SourceProfileId, ct);
        return Response<IReadOnlyList<PayrollCycleReferenceDto>>.Success(items.Select(PayrollSourceMapper.ToDto).ToList());
    }
}
