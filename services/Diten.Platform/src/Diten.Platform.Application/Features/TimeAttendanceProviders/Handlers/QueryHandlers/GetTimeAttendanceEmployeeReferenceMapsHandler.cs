using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeAttendanceProviders.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Handlers.QueryHandlers;

public sealed class GetTimeAttendanceEmployeeReferenceMapsHandler : IRequestHandler<GetTimeAttendanceEmployeeReferenceMapsQuery, Response<IReadOnlyList<TimeAttendanceEmployeeReferenceMapDto>>>
{
    private readonly ITimeAttendanceProviderRepository _repository;

    public GetTimeAttendanceEmployeeReferenceMapsHandler(ITimeAttendanceProviderRepository repository) => _repository = repository;

    public async Task<Response<IReadOnlyList<TimeAttendanceEmployeeReferenceMapDto>>> Handle(GetTimeAttendanceEmployeeReferenceMapsQuery request, CancellationToken ct)
    {
        if (await _repository.GetProviderProfileByIdAsync(request.ProviderProfileId, ct) == null)
        {
            return Response<IReadOnlyList<TimeAttendanceEmployeeReferenceMapDto>>.Fail("Time attendance provider not found.", 404);
        }

        var items = await _repository.GetEmployeeReferenceMapsAsync(request.ProviderProfileId, ct);
        return Response<IReadOnlyList<TimeAttendanceEmployeeReferenceMapDto>>.Success(items.Select(TimeAttendanceProviderMapper.ToDto).ToList());
    }
}
