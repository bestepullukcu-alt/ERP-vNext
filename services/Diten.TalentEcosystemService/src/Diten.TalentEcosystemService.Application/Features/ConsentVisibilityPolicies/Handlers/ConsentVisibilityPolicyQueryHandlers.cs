using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.ConsentVisibilityPolicies.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ConsentVisibilityPolicies.Handlers;

public sealed class GetConsentVisibilityPolicyListHandler
    : IRequestHandler<GetConsentVisibilityPolicyListQuery, Response<IReadOnlyList<ConsentVisibilityPolicyListItemDto>>>
{
    private readonly ITepConsentVisibilityPolicyRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetConsentVisibilityPolicyListHandler(ITepConsentVisibilityPolicyRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<ConsentVisibilityPolicyListItemDto>>> Handle(GetConsentVisibilityPolicyListQuery request, CancellationToken ct)
    {
        var tenant = ConsentVisibilityPolicyGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<ConsentVisibilityPolicyListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var items = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<ConsentVisibilityPolicyListItemDto>>.Success(items.Select(ConsentVisibilityPolicyMapper.ToListItemDto).ToList());
    }
}

public sealed class GetConsentVisibilityPolicyByIdHandler
    : IRequestHandler<GetConsentVisibilityPolicyByIdQuery, Response<ConsentVisibilityPolicyDto>>
{
    private readonly ITepConsentVisibilityPolicyRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetConsentVisibilityPolicyByIdHandler(ITepConsentVisibilityPolicyRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<ConsentVisibilityPolicyDto>> Handle(GetConsentVisibilityPolicyByIdQuery request, CancellationToken ct)
    {
        var tenant = ConsentVisibilityPolicyGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<ConsentVisibilityPolicyDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var item = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return item is null
            ? Response<ConsentVisibilityPolicyDto>.Fail("Consent/visibility policy was not found.", 404)
            : Response<ConsentVisibilityPolicyDto>.Success(ConsentVisibilityPolicyMapper.ToDto(item));
    }
}

public sealed class GetConsentVisibilityPolicyAuditMetadataHandler
    : IRequestHandler<GetConsentVisibilityPolicyAuditMetadataQuery, Response<ConsentVisibilityPolicyAuditMetadataDto>>
{
    private readonly ITepConsentVisibilityPolicyRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetConsentVisibilityPolicyAuditMetadataHandler(ITepConsentVisibilityPolicyRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<ConsentVisibilityPolicyAuditMetadataDto>> Handle(GetConsentVisibilityPolicyAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = ConsentVisibilityPolicyGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<ConsentVisibilityPolicyAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var item = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return item is null
            ? Response<ConsentVisibilityPolicyAuditMetadataDto>.Fail("Consent/visibility policy was not found.", 404)
            : Response<ConsentVisibilityPolicyAuditMetadataDto>.Success(new ConsentVisibilityPolicyAuditMetadataDto(
                item.Id,
                item.LocalAuditEvidenceRetentionState,
                item.SourceContractVersion,
                item.LastEvaluatedAt,
                item.PolicyVersion));
    }
}
