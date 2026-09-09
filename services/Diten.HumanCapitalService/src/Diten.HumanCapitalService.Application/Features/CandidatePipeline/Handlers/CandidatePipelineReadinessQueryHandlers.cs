using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.CandidatePipeline.Queries;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.CandidatePipeline.Handlers;

public sealed class GetCandidatePipelineReadinessListHandler
    : IRequestHandler<GetCandidatePipelineReadinessListQuery, Response<IReadOnlyList<CandidatePipelineReadinessListItemDto>>>
{
    private readonly ICandidatePipelineReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetCandidatePipelineReadinessListHandler(
        ICandidatePipelineReadinessMetadataRepository repository,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<CandidatePipelineReadinessListItemDto>>> Handle(
        GetCandidatePipelineReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = CandidatePipelineGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<CandidatePipelineReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var rows = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<CandidatePipelineReadinessListItemDto>>.Success(rows.Select(CandidatePipelineMapper.ToListItem).ToList());
    }
}

public sealed class GetCandidatePipelineReadinessByIdHandler
    : IRequestHandler<GetCandidatePipelineReadinessByIdQuery, Response<CandidatePipelineReadinessDto>>
{
    private readonly ICandidatePipelineReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetCandidatePipelineReadinessByIdHandler(
        ICandidatePipelineReadinessMetadataRepository repository,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<CandidatePipelineReadinessDto>> Handle(GetCandidatePipelineReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = CandidatePipelineGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<CandidatePipelineReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<CandidatePipelineReadinessDto>.Fail("Candidate pipeline readiness record was not found.", 404)
            : Response<CandidatePipelineReadinessDto>.Success(CandidatePipelineMapper.ToDto(entity));
    }
}

public sealed class GetCandidatePipelineAuditMetadataHandler
    : IRequestHandler<GetCandidatePipelineAuditMetadataQuery, Response<CandidatePipelineAuditMetadataDto>>
{
    private readonly ICandidatePipelineReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetCandidatePipelineAuditMetadataHandler(
        ICandidatePipelineReadinessMetadataRepository repository,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<CandidatePipelineAuditMetadataDto>> Handle(GetCandidatePipelineAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = CandidatePipelineGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<CandidatePipelineAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<CandidatePipelineAuditMetadataDto>.Fail("Candidate pipeline readiness record was not found.", 404)
            : Response<CandidatePipelineAuditMetadataDto>.Success(CandidatePipelineMapper.ToAuditMetadata(entity));
    }
}
