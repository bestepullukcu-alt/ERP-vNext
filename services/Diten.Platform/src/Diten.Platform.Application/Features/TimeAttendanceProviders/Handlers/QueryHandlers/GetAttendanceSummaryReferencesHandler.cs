using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeAttendanceProviders.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Handlers.QueryHandlers;

public sealed class GetAttendanceSummaryReferencesHandler : IRequestHandler<GetAttendanceSummaryReferencesQuery, Response<IReadOnlyList<AttendanceSummaryReferenceDto>>>
{
    private readonly ITimeAttendanceProviderRepository _repository;

    public GetAttendanceSummaryReferencesHandler(ITimeAttendanceProviderRepository repository) => _repository = repository;

    public async Task<Response<IReadOnlyList<AttendanceSummaryReferenceDto>>> Handle(GetAttendanceSummaryReferencesQuery request, CancellationToken ct)
    {
        if (await _repository.GetProviderProfileByIdAsync(request.ProviderProfileId, ct) == null)
        {
            return Response<IReadOnlyList<AttendanceSummaryReferenceDto>>.Fail("Time attendance provider not found.", 404);
        }

        var items = await _repository.GetAttendanceSummaryReferencesAsync(request.ProviderProfileId, ct);
        return Response<IReadOnlyList<AttendanceSummaryReferenceDto>>.Success(items.Select(TimeAttendanceProviderMapper.ToDto).ToList());
    }
}
