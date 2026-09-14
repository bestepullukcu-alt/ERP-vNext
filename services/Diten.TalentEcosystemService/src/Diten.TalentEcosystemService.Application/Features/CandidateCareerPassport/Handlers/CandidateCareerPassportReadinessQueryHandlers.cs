using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.CandidateCareerPassport.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.CandidateCareerPassport.Handlers;

public sealed class GetCandidateCareerPassportReadinessListHandler
    : IRequestHandler<GetCandidateCareerPassportReadinessListQuery, Response<IReadOnlyList<CandidateCareerPassportReadinessListItemDto>>>
{
    private readonly ICandidateCareerPassportReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetCandidateCareerPassportReadinessListHandler(ICandidateCareerPassportReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<CandidateCareerPassportReadinessListItemDto>>> Handle(
        GetCandidateCareerPassportReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = CandidateCareerPassportGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<CandidateCareerPassportReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var rows = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<CandidateCareerPassportReadinessListItemDto>>.Success(rows.Select(CandidateCareerPassportMapper.ToListItem).ToList());
    }
}

public sealed class GetCandidateCareerPassportReadinessByIdHandler
    : IRequestHandler<GetCandidateCareerPassportReadinessByIdQuery, Response<CandidateCareerPassportReadinessDto>>
{
    private readonly ICandidateCareerPassportReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetCandidateCareerPassportReadinessByIdHandler(ICandidateCareerPassportReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<CandidateCareerPassportReadinessDto>> Handle(GetCandidateCareerPassportReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = CandidateCareerPassportGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<CandidateCareerPassportReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<CandidateCareerPassportReadinessDto>.Fail("CandidateCareerPassport readiness record was not found.", 404)
            : Response<CandidateCareerPassportReadinessDto>.Success(CandidateCareerPassportMapper.ToDto(entity));
    }
}

public sealed class GetCandidateCareerPassportAuditMetadataHandler
    : IRequestHandler<GetCandidateCareerPassportAuditMetadataQuery, Response<CandidateCareerPassportAuditMetadataDto>>
{
    private readonly ICandidateCareerPassportReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetCandidateCareerPassportAuditMetadataHandler(ICandidateCareerPassportReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<CandidateCareerPassportAuditMetadataDto>> Handle(GetCandidateCareerPassportAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = CandidateCareerPassportGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<CandidateCareerPassportAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<CandidateCareerPassportAuditMetadataDto>.Fail("CandidateCareerPassport readiness record was not found.", 404)
            : Response<CandidateCareerPassportAuditMetadataDto>.Success(CandidateCareerPassportMapper.ToAuditMetadata(entity));
    }
}
