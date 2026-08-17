using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeAttendanceProviders.Commands;
using Diten.Platform.Domain.Entities.TimeAttendanceProviders;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Handlers.CommandHandlers;

public sealed class UpdateTimeAttendanceContractProfileHandler : IRequestHandler<UpdateTimeAttendanceContractProfileCommand, Response<NoContent>>
{
    private readonly ITimeAttendanceProviderRepository _repository;

    public UpdateTimeAttendanceContractProfileHandler(ITimeAttendanceProviderRepository repository) => _repository = repository;

    public async Task<Response<NoContent>> Handle(UpdateTimeAttendanceContractProfileCommand request, CancellationToken ct)
    {
        var profile = await _repository.GetProviderProfileByIdAsync(request.ProviderProfileId, ct);
        if (profile == null)
        {
            return Response<NoContent>.Fail("Time-attendance provider not found.", 404);
        }

        var existing = await _repository.GetContractProfileAsync(profile.Id, ct);
        if (existing != null && !string.Equals(existing.ContractVersion, request.Request.ContractVersion.Trim(), StringComparison.Ordinal))
        {
            return Response<NoContent>.Fail("Time-attendance contract version is immutable once created.", 409);
        }

        var contract = existing ?? new TimeAttendanceContractProfile
        {
            TenantId = profile.TenantId,
            ProviderProfileId = profile.Id,
            ContractVersion = request.Request.ContractVersion.Trim()
        };

        contract.EffectiveFrom = request.Request.EffectiveFrom;
        contract.EffectiveTo = request.Request.EffectiveTo;
        contract.SupportedObjectTypes = request.Request.SupportedObjectTypes.ToList();
        contract.StatusVocabulary = request.Request.StatusVocabulary.Select(x => x.Trim()).ToList();
        contract.ErrorVocabulary = (request.Request.ErrorVocabulary ?? []).Select(x => x.Trim()).ToList();
        contract.CorrelationIdPattern = string.IsNullOrWhiteSpace(request.Request.CorrelationIdPattern) ? null : request.Request.CorrelationIdPattern.Trim();

        await _repository.UpsertContractProfileAsync(contract, ct);
        profile.ContractProfileId = contract.Id;
        await _repository.UpdateProviderProfileAsync(profile, ct);
        return Response<NoContent>.Success(204);
    }
}
