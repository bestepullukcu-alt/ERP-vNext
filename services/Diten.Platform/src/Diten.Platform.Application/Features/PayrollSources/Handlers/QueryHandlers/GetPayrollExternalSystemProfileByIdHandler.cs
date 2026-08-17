using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollSources.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollSources.Handlers.QueryHandlers;

public sealed class GetPayrollExternalSystemProfileByIdHandler : IRequestHandler<GetPayrollExternalSystemProfileByIdQuery, Response<PayrollExternalSystemProfileDto>>
{
    private readonly IPayrollSourceRepository _repository;

    public GetPayrollExternalSystemProfileByIdHandler(IPayrollSourceRepository repository) => _repository = repository;

    public async Task<Response<PayrollExternalSystemProfileDto>> Handle(GetPayrollExternalSystemProfileByIdQuery request, CancellationToken ct)
    {
        var item = await _repository.GetExternalSystemProfileByIdAsync(request.Id, ct);
        return item == null
            ? Response<PayrollExternalSystemProfileDto>.Fail("Payroll source not found.", 404)
            : Response<PayrollExternalSystemProfileDto>.Success(PayrollSourceMapper.ToDto(item));
    }
}
