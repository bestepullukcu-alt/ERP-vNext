using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.EarlyWarningSignals.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.EarlyWarningSignals.Handlers;

public sealed class GetEarlyWarningSignalsReadinessListHandler
    : IRequestHandler<GetEarlyWarningSignalsReadinessListQuery, Response<IReadOnlyList<EarlyWarningSignalsReadinessListItemDto>>>
{
    private readonly IEarlyWarningSignalsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetEarlyWarningSignalsReadinessListHandler(IEarlyWarningSignalsReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<EarlyWarningSignalsReadinessListItemDto>>> Handle(
        GetEarlyWarningSignalsReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = EarlyWarningSignalsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<EarlyWarningSignalsReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var rows = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<EarlyWarningSignalsReadinessListItemDto>>.Success(rows.Select(EarlyWarningSignalsMapper.ToListItem).ToList());
    }
}

public sealed class GetEarlyWarningSignalsReadinessByIdHandler
    : IRequestHandler<GetEarlyWarningSignalsReadinessByIdQuery, Response<EarlyWarningSignalsReadinessDto>>
{
    private readonly IEarlyWarningSignalsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetEarlyWarningSignalsReadinessByIdHandler(IEarlyWarningSignalsReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<EarlyWarningSignalsReadinessDto>> Handle(GetEarlyWarningSignalsReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = EarlyWarningSignalsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<EarlyWarningSignalsReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<EarlyWarningSignalsReadinessDto>.Fail("EarlyWarningSignals readiness record was not found.", 404)
            : Response<EarlyWarningSignalsReadinessDto>.Success(EarlyWarningSignalsMapper.ToDto(entity));
    }
}

public sealed class GetEarlyWarningSignalsAuditMetadataHandler
    : IRequestHandler<GetEarlyWarningSignalsAuditMetadataQuery, Response<EarlyWarningSignalsAuditMetadataDto>>
{
    private readonly IEarlyWarningSignalsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetEarlyWarningSignalsAuditMetadataHandler(IEarlyWarningSignalsReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<EarlyWarningSignalsAuditMetadataDto>> Handle(GetEarlyWarningSignalsAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = EarlyWarningSignalsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<EarlyWarningSignalsAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<EarlyWarningSignalsAuditMetadataDto>.Fail("EarlyWarningSignals readiness record was not found.", 404)
            : Response<EarlyWarningSignalsAuditMetadataDto>.Success(EarlyWarningSignalsMapper.ToAuditMetadata(entity));
    }
}
