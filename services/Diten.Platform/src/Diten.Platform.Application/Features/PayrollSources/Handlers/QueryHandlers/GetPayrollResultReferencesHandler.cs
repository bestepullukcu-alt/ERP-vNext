using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollSources.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollSources.Handlers.QueryHandlers;

public sealed class GetPayrollResultReferencesHandler : IRequestHandler<GetPayrollResultReferencesQuery, Response<IReadOnlyList<PayrollResultReferenceDto>>>
{
    private readonly IPayrollSourceRepository _repository;

    public GetPayrollResultReferencesHandler(IPayrollSourceRepository repository) => _repository = repository;

    public async Task<Response<IReadOnlyList<PayrollResultReferenceDto>>> Handle(GetPayrollResultReferencesQuery request, CancellationToken ct)
    {
        if (await _repository.GetExternalSystemProfileByIdAsync(request.SourceProfileId, ct) == null)
        {
            return Response<IReadOnlyList<PayrollResultReferenceDto>>.Fail("Payroll source not found.", 404);
        }

        var items = await _repository.GetResultReferencesAsync(request.SourceProfileId, ct);
        return Response<IReadOnlyList<PayrollResultReferenceDto>>.Success(items.Select(PayrollSourceMapper.ToDto).ToList());
    }
}
