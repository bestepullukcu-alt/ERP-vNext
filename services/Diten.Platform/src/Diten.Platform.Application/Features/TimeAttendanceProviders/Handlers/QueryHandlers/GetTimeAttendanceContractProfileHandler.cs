using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeAttendanceProviders.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Handlers.QueryHandlers;

public sealed class GetTimeAttendanceContractProfileHandler : IRequestHandler<GetTimeAttendanceContractProfileQuery, Response<TimeAttendanceContractProfileDto>>
{
    private readonly ITimeAttendanceProviderRepository _repository;

    public GetTimeAttendanceContractProfileHandler(ITimeAttendanceProviderRepository repository) => _repository = repository;

    public async Task<Response<TimeAttendanceContractProfileDto>> Handle(GetTimeAttendanceContractProfileQuery request, CancellationToken ct)
    {
        if (await _repository.GetProviderProfileByIdAsync(request.ProviderProfileId, ct) == null)
        {
            return Response<TimeAttendanceContractProfileDto>.Fail("Time attendance provider not found.", 404);
        }

        var item = await _repository.GetContractProfileAsync(request.ProviderProfileId, ct);
        return item == null
            ? Response<TimeAttendanceContractProfileDto>.Fail("Time attendance contract profile not found.", 404)
            : Response<TimeAttendanceContractProfileDto>.Success(TimeAttendanceProviderMapper.ToDto(item));
    }
}
