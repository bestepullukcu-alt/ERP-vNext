using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeAttendanceProviders.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Handlers.QueryHandlers;

public sealed class GetTimeAttendanceEventReferencesHandler : IRequestHandler<GetTimeAttendanceEventReferencesQuery, Response<IReadOnlyList<TimeAttendanceEventReferenceDto>>>
{
    private readonly ITimeAttendanceProviderRepository _repository;

    public GetTimeAttendanceEventReferencesHandler(ITimeAttendanceProviderRepository repository) => _repository = repository;

    public async Task<Response<IReadOnlyList<TimeAttendanceEventReferenceDto>>> Handle(GetTimeAttendanceEventReferencesQuery request, CancellationToken ct)
    {
        if (await _repository.GetProviderProfileByIdAsync(request.ProviderProfileId, ct) == null)
        {
            return Response<IReadOnlyList<TimeAttendanceEventReferenceDto>>.Fail("Time attendance provider not found.", 404);
        }

        var items = await _repository.GetEventReferencesAsync(request.ProviderProfileId, ct);
        return Response<IReadOnlyList<TimeAttendanceEventReferenceDto>>.Success(items.Select(TimeAttendanceProviderMapper.ToDto).ToList());
    }
}
