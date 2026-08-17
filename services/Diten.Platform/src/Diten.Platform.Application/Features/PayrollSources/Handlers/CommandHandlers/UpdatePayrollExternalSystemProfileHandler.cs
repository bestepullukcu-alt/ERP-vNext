using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollSources.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollSources.Handlers.CommandHandlers;

public sealed class UpdatePayrollExternalSystemProfileHandler : IRequestHandler<UpdatePayrollExternalSystemProfileCommand, Response<NoContent>>
{
    private readonly IPayrollSourceRepository _repository;

    public UpdatePayrollExternalSystemProfileHandler(IPayrollSourceRepository repository) => _repository = repository;

    public async Task<Response<NoContent>> Handle(UpdatePayrollExternalSystemProfileCommand request, CancellationToken ct)
    {
        var profile = await _repository.GetExternalSystemProfileByIdAsync(request.Id, ct);
        if (profile == null)
        {
            return Response<NoContent>.Fail("Payroll source not found.", 404);
        }

        var code = PayrollSourceCodeNormalizer.Normalize(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(code, profile.Id, ct))
        {
            return Response<NoContent>.Fail("A payroll source with this code already exists.", 409);
        }

        profile.Code = code;
        profile.DisplayName = request.Request.DisplayName.Trim();
        profile.ProviderFamily = request.Request.ProviderFamily;
        profile.ExternalPayrollSystemId = request.Request.ExternalPayrollSystemId.Trim();
        profile.LifecycleState = request.Request.LifecycleState;
        profile.ConnectionProfileReference = string.IsNullOrWhiteSpace(request.Request.ConnectionProfileReference) ? null : request.Request.ConnectionProfileReference.Trim();
        profile.SupportOwner = string.IsNullOrWhiteSpace(request.Request.SupportOwner) ? null : request.Request.SupportOwner.Trim();
        profile.Notes = string.IsNullOrWhiteSpace(request.Request.Notes) ? null : request.Request.Notes.Trim();

        await _repository.UpdateExternalSystemProfileAsync(profile, ct);
        return Response<NoContent>.Success(204);
    }
}
