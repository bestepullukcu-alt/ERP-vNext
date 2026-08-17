using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeAttendanceProviders.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Handlers.CommandHandlers;

public sealed class ArchiveTimeAttendanceProviderProfileHandler : IRequestHandler<ArchiveTimeAttendanceProviderProfileCommand, Response<NoContent>>
{
    private readonly ITimeAttendanceProviderRepository _repository;

    public ArchiveTimeAttendanceProviderProfileHandler(ITimeAttendanceProviderRepository repository) => _repository = repository;

    public async Task<Response<NoContent>> Handle(ArchiveTimeAttendanceProviderProfileCommand request, CancellationToken ct)
    {
        if (!await _repository.ArchiveProviderProfileAsync(request.Id, ct))
        {
            return Response<NoContent>.Fail("Time-attendance provider not found.", 404);
        }

        return Response<NoContent>.Success(204);
    }
}
