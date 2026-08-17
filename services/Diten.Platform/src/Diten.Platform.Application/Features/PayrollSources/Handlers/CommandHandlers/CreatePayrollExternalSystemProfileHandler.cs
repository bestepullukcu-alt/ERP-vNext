using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollSources.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.PayrollSources;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollSources.Handlers.CommandHandlers;

public sealed class CreatePayrollExternalSystemProfileHandler : IRequestHandler<CreatePayrollExternalSystemProfileCommand, Response<Guid>>
{
    private readonly IPayrollSourceRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreatePayrollExternalSystemProfileHandler(IPayrollSourceRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreatePayrollExternalSystemProfileCommand request, CancellationToken ct)
    {
        var code = PayrollSourceCodeNormalizer.Normalize(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(code, null, ct))
        {
            return Response<Guid>.Fail("A payroll source with this code already exists.", 409);
        }

        var profile = new PayrollExternalSystemProfile
        {
            TenantId = _tenantContext.TenantId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            ProviderFamily = request.Request.ProviderFamily,
            ExternalPayrollSystemId = request.Request.ExternalPayrollSystemId.Trim(),
            LifecycleState = request.Request.LifecycleState,
            ConnectionProfileReference = string.IsNullOrWhiteSpace(request.Request.ConnectionProfileReference) ? null : request.Request.ConnectionProfileReference.Trim(),
            SupportOwner = string.IsNullOrWhiteSpace(request.Request.SupportOwner) ? null : request.Request.SupportOwner.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Request.Notes) ? null : request.Request.Notes.Trim()
        };

        await _repository.CreateExternalSystemProfileAsync(profile, ct);
        return Response<Guid>.Success(profile.Id, 201);
    }
}
