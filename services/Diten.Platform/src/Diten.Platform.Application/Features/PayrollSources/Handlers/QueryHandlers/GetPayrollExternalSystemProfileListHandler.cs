using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollSources.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollSources.Handlers.QueryHandlers;

public sealed class GetPayrollExternalSystemProfileListHandler : IRequestHandler<GetPayrollExternalSystemProfileListQuery, Response<IReadOnlyList<PayrollExternalSystemProfileDto>>>
{
    private readonly IPayrollSourceRepository _repository;

    public GetPayrollExternalSystemProfileListHandler(IPayrollSourceRepository repository) => _repository = repository;

    public async Task<Response<IReadOnlyList<PayrollExternalSystemProfileDto>>> Handle(GetPayrollExternalSystemProfileListQuery request, CancellationToken ct)
    {
        var items = await _repository.GetExternalSystemProfilesAsync(ct);
        return Response<IReadOnlyList<PayrollExternalSystemProfileDto>>.Success(items.Select(PayrollSourceMapper.ToDto).ToList());
    }
}
