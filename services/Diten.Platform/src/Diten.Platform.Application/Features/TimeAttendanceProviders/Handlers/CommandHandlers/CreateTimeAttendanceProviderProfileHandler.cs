using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeAttendanceProviders.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.TimeAttendanceProviders;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Handlers.CommandHandlers;

public sealed class CreateTimeAttendanceProviderProfileHandler : IRequestHandler<CreateTimeAttendanceProviderProfileCommand, Response<Guid>>
{
    private readonly ITimeAttendanceProviderRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateTimeAttendanceProviderProfileHandler(ITimeAttendanceProviderRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateTimeAttendanceProviderProfileCommand request, CancellationToken ct)
    {
        var code = TimeAttendanceProviderCodeNormalizer.Normalize(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(code, null, ct))
        {
            return Response<Guid>.Fail("A time-attendance provider with this code already exists.", 409);
        }

        var profile = new TimeAttendanceExternalProviderProfile
        {
            TenantId = _tenantContext.TenantId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            ProviderFamily = request.Request.ProviderFamily,
            ExternalProviderAccountId = request.Request.ExternalProviderAccountId.Trim(),
            LifecycleState = request.Request.LifecycleState,
            ConnectionProfileReference = string.IsNullOrWhiteSpace(request.Request.ConnectionProfileReference) ? null : request.Request.ConnectionProfileReference.Trim(),
            SupportOwner = string.IsNullOrWhiteSpace(request.Request.SupportOwner) ? null : request.Request.SupportOwner.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Request.Notes) ? null : request.Request.Notes.Trim()
        };

        await _repository.CreateProviderProfileAsync(profile, ct);
        return Response<Guid>.Success(profile.Id, 201);
    }
}
