using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollSources.Commands;
using Diten.Platform.Domain.Entities.PayrollSources;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollSources.Handlers.CommandHandlers;

public sealed class UpdatePayrollContractProfileHandler : IRequestHandler<UpdatePayrollContractProfileCommand, Response<NoContent>>
{
    private readonly IPayrollSourceRepository _repository;

    public UpdatePayrollContractProfileHandler(IPayrollSourceRepository repository) => _repository = repository;

    public async Task<Response<NoContent>> Handle(UpdatePayrollContractProfileCommand request, CancellationToken ct)
    {
        var source = await _repository.GetExternalSystemProfileByIdAsync(request.SourceProfileId, ct);
        if (source == null)
        {
            return Response<NoContent>.Fail("Payroll source not found.", 404);
        }

        var existing = await _repository.GetContractProfileAsync(source.Id, ct);
        if (existing != null && !string.Equals(existing.ContractVersion, request.Request.ContractVersion.Trim(), StringComparison.Ordinal))
        {
            return Response<NoContent>.Fail("Payroll contract version is immutable once created.", 409);
        }

        var contract = existing ?? new PayrollContractProfile
        {
            TenantId = source.TenantId,
            PayrollExternalSystemProfileId = source.Id,
            ContractVersion = request.Request.ContractVersion.Trim()
        };

        contract.EffectiveFrom = request.Request.EffectiveFrom;
        contract.EffectiveTo = request.Request.EffectiveTo;
        contract.SupportedObjectTypes = request.Request.SupportedObjectTypes.ToList();
        contract.StatusVocabulary = request.Request.StatusVocabulary.Select(x => x.Trim()).ToList();
        contract.ErrorVocabulary = (request.Request.ErrorVocabulary ?? []).Select(x => x.Trim()).ToList();
        contract.CorrelationIdPattern = string.IsNullOrWhiteSpace(request.Request.CorrelationIdPattern) ? null : request.Request.CorrelationIdPattern.Trim();

        await _repository.UpsertContractProfileAsync(contract, ct);
        source.ContractProfileId = contract.Id;
        await _repository.UpdateExternalSystemProfileAsync(source, ct);
        return Response<NoContent>.Success(204);
    }
}
