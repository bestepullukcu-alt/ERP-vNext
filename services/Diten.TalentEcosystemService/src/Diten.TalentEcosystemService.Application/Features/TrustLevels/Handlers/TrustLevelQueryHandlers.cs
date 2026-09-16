using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.TrustLevels.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TrustLevels.Handlers;

public sealed class GetTrustLevelPolicyListHandler
    : IRequestHandler<GetTrustLevelPolicyListQuery, Response<IReadOnlyList<TrustLevelPolicyListItemDto>>>
{
    private readonly ITepTrustLevelPolicyMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetTrustLevelPolicyListHandler(ITepTrustLevelPolicyMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<TrustLevelPolicyListItemDto>>> Handle(GetTrustLevelPolicyListQuery request, CancellationToken ct)
    {
        var tenant = TrustLevelGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<TrustLevelPolicyListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var items = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<TrustLevelPolicyListItemDto>>.Success(items.Select(TrustLevelMapper.ToListItemDto).ToList());
    }
}

public sealed class GetTrustLevelPolicyByIdHandler
    : IRequestHandler<GetTrustLevelPolicyByIdQuery, Response<TrustLevelPolicyDto>>
{
    private readonly ITepTrustLevelPolicyMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetTrustLevelPolicyByIdHandler(ITepTrustLevelPolicyMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<TrustLevelPolicyDto>> Handle(GetTrustLevelPolicyByIdQuery request, CancellationToken ct)
    {
        var tenant = TrustLevelGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<TrustLevelPolicyDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<TrustLevelPolicyDto>.Fail("Trust-level policy was not found.", 404)
            : Response<TrustLevelPolicyDto>.Success(TrustLevelMapper.ToDto(entity));
    }
}

public sealed class GetTrustLevelPolicyAuditMetadataHandler
    : IRequestHandler<GetTrustLevelPolicyAuditMetadataQuery, Response<TrustLevelAuditMetadataDto>>
{
    private readonly ITepTrustLevelPolicyMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetTrustLevelPolicyAuditMetadataHandler(ITepTrustLevelPolicyMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<TrustLevelAuditMetadataDto>> Handle(GetTrustLevelPolicyAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = TrustLevelGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<TrustLevelAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<TrustLevelAuditMetadataDto>.Fail("Trust-level policy was not found.", 404)
            : Response<TrustLevelAuditMetadataDto>.Success(new TrustLevelAuditMetadataDto(
                entity.Id,
                entity.SignatureSubstrateState,
                entity.SignatureSubstrateReference,
                entity.AuditEvidenceState,
                entity.RetentionState,
                entity.DependencyStates.Select(TrustLevelMapper.ToDto).ToList(),
                entity.DeferredReason));
    }
}
