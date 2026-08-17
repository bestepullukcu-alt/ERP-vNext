using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.HrisSources.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.HrisSources;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.HrisSources.Handlers.CommandHandlers;

public sealed class CreateHrisSourceProfileHandler : IRequestHandler<CreateHrisSourceProfileCommand, Response<Guid>>
{
    private readonly IHrisSourceRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateHrisSourceProfileHandler(IHrisSourceRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateHrisSourceProfileCommand request, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var canonicalCode = HrisSourceCodeNormalizer.Normalize(request.Request.Code);
        if (string.IsNullOrWhiteSpace(canonicalCode))
        {
            return Response<Guid>.Fail("HRIS source code is required.", 400);
        }

        if (await _repository.ExistsActiveSourceCodeAsync(canonicalCode, null, ct))
        {
            return Response<Guid>.Fail("HRIS source code already exists.", 409);
        }

        var entity = new HrisSourceProfile
        {
            TenantId = tenantId,
            Code = canonicalCode,
            DisplayName = request.Request.DisplayName.Trim(),
            ProviderKind = request.Request.ProviderKind,
            ExternalTenantKey = NormalizeOptional(request.Request.ExternalTenantKey),
            ConnectionProfileReference = request.Request.ConnectionProfileReference.Trim(),
            LifecycleState = request.Request.LifecycleState,
            SyncMode = request.Request.SyncMode,
            MappingProfileId = request.Request.MappingProfileId,
            CorrelationId = NormalizeOptional(request.Request.CorrelationId)
        };

        await _repository.CreateSourceProfileAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
