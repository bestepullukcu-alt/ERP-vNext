using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollSources.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollSources.Handlers.QueryHandlers;

public sealed class GetPayrollContractProfileHandler : IRequestHandler<GetPayrollContractProfileQuery, Response<PayrollContractProfileDto>>
{
    private readonly IPayrollSourceRepository _repository;

    public GetPayrollContractProfileHandler(IPayrollSourceRepository repository) => _repository = repository;

    public async Task<Response<PayrollContractProfileDto>> Handle(GetPayrollContractProfileQuery request, CancellationToken ct)
    {
        if (await _repository.GetExternalSystemProfileByIdAsync(request.SourceProfileId, ct) == null)
        {
            return Response<PayrollContractProfileDto>.Fail("Payroll source not found.", 404);
        }

        var item = await _repository.GetContractProfileAsync(request.SourceProfileId, ct);
        return item == null
            ? Response<PayrollContractProfileDto>.Fail("Payroll contract profile not found.", 404)
            : Response<PayrollContractProfileDto>.Success(PayrollSourceMapper.ToDto(item));
    }
}
