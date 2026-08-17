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

    public GetConsentVisibilityPolicyListHandler(ITepConsentVisibilityPolicyRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<ConsentVisibilityPolicyListItemDto>>> Handle(GetConsentVisibilityPolicyListQuery request, CancellationToken ct)
    {
        var tenant = ConsentVisibilityPolicyGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<ConsentVisibilityPolicyListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var items = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<ConsentVisibilityPolicyListItemDto>>.Success(items.Select(ConsentVisibilityPolicyMapper.ToListItemDto).ToList());
    }
}

public sealed class GetConsentVisibilityPolicyByIdHandler
    : IRequestHandler<GetConsentVisibilityPolicyByIdQuery, Response<ConsentVisibilityPolicyDto>>
{
    private readonly ITepConsentVisibilityPolicyRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetConsentVisibilityPolicyByIdHandler(ITepConsentVisibilityPolicyRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<ConsentVisibilityPolicyDto>> Handle(GetConsentVisibilityPolicyByIdQuery request, CancellationToken ct)
    {
        var tenant = ConsentVisibilityPolicyGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<ConsentVisibilityPolicyDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var item = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
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

    public GetConsentVisibilityPolicyAuditMetadataHandler(ITepConsentVisibilityPolicyRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<ConsentVisibilityPolicyAuditMetadataDto>> Handle(GetConsentVisibilityPolicyAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = ConsentVisibilityPolicyGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<ConsentVisibilityPolicyAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var item = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
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
