using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeAttendanceProviders.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Handlers.CommandHandlers;

public sealed class UpdateTimeAttendanceProviderProfileHandler : IRequestHandler<UpdateTimeAttendanceProviderProfileCommand, Response<NoContent>>
{
    private readonly ITimeAttendanceProviderRepository _repository;

    public UpdateTimeAttendanceProviderProfileHandler(ITimeAttendanceProviderRepository repository) => _repository = repository;

    public async Task<Response<NoContent>> Handle(UpdateTimeAttendanceProviderProfileCommand request, CancellationToken ct)
    {
        var profile = await _repository.GetProviderProfileByIdAsync(request.Id, ct);
        if (profile == null)
        {
            return Response<NoContent>.Fail("Time-attendance provider not found.", 404);
        }

        var code = TimeAttendanceProviderCodeNormalizer.Normalize(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(code, profile.Id, ct))
        {
            return Response<NoContent>.Fail("A time-attendance provider with this code already exists.", 409);
        }

        profile.Code = code;
        profile.DisplayName = request.Request.DisplayName.Trim();
        profile.ProviderFamily = request.Request.ProviderFamily;
        profile.ExternalProviderAccountId = request.Request.ExternalProviderAccountId.Trim();
        profile.LifecycleState = request.Request.LifecycleState;
        profile.ConnectionProfileReference = string.IsNullOrWhiteSpace(request.Request.ConnectionProfileReference) ? null : request.Request.ConnectionProfileReference.Trim();
        profile.SupportOwner = string.IsNullOrWhiteSpace(request.Request.SupportOwner) ? null : request.Request.SupportOwner.Trim();
        profile.Notes = string.IsNullOrWhiteSpace(request.Request.Notes) ? null : request.Request.Notes.Trim();

        await _repository.UpdateProviderProfileAsync(profile, ct);
        return Response<NoContent>.Success(204);
    }
}
