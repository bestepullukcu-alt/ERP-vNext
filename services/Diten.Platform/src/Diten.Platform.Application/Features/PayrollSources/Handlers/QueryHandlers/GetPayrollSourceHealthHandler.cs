using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollSources.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollSources.Handlers.QueryHandlers;

public sealed class GetPayrollSourceHealthHandler : IRequestHandler<GetPayrollSourceHealthQuery, Response<PayrollSourceHealthSnapshotDto>>
{
    private readonly IPayrollSourceRepository _repository;

    public GetPayrollSourceHealthHandler(IPayrollSourceRepository repository) => _repository = repository;

    public async Task<Response<PayrollSourceHealthSnapshotDto>> Handle(GetPayrollSourceHealthQuery request, CancellationToken ct)
    {
        if (await _repository.GetExternalSystemProfileByIdAsync(request.SourceProfileId, ct) == null)
        {
            return Response<PayrollSourceHealthSnapshotDto>.Fail("Payroll source not found.", 404);
        }

        var item = await _repository.GetLatestHealthSnapshotAsync(request.SourceProfileId, ct);
        return item == null
            ? Response<PayrollSourceHealthSnapshotDto>.Fail("Payroll source health snapshot not found.", 404)
            : Response<PayrollSourceHealthSnapshotDto>.Success(PayrollSourceMapper.ToDto(item));
    }
}
