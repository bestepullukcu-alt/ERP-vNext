using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.Eligibility;

public sealed class ListEligibilityPoliciesHandler
    : IRequestHandler<ListEligibilityPoliciesQuery, Response<EligibilityPolicyListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IEligibilityPolicyRepository _policies;

    public ListEligibilityPoliciesHandler(ITenantContext tenant, IEligibilityPolicyRepository policies)
    {
        _tenant = tenant;
        _policies = policies;
    }

    public async Task<Response<EligibilityPolicyListDto>> Handle(
        ListEligibilityPoliciesQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<EligibilityPolicyListDto>.Fail("Tenant context is required.", 400);
        }

        IEnumerable<EligibilityPolicy> rows = await _policies.ListAsync(tenantId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = EligibilityPolicyStatuses.Normalize(request.Status);
            rows = rows.Where(x => x.Status == status);
        }

        if (request.EffectiveAt is { } at)
        {
            rows = rows.Where(x => x.IsEffectiveAt(at));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            rows = rows.Where(x =>
                x.PolicyName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.PolicyCode.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!request.IncludeArchived)
        {
            rows = rows.Where(x => !x.IsArchived());
        }

        var items = rows.Select(EligibilityPolicyMapper.ToDto).ToList();
        return Response<EligibilityPolicyListDto>.Success(new EligibilityPolicyListDto(items, items.Count));
    }
}

public sealed class GetEligibilityPolicyHandler
    : IRequestHandler<GetEligibilityPolicyQuery, Response<EligibilityPolicyDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IEligibilityPolicyRepository _policies;

    public GetEligibilityPolicyHandler(ITenantContext tenant, IEligibilityPolicyRepository policies)
    {
        _tenant = tenant;
        _policies = policies;
    }

    public async Task<Response<EligibilityPolicyDto>> Handle(
        GetEligibilityPolicyQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<EligibilityPolicyDto>.Fail("Tenant context is required.", 400);
        }

        var entity = await _policies.GetByIdAsync(tenantId, request.EligibilityPolicyId, cancellationToken);
        return entity is null
            ? Response<EligibilityPolicyDto>.Fail("Eligibility policy not found.", 404)
            : Response<EligibilityPolicyDto>.Success(EligibilityPolicyMapper.ToDto(entity));
    }
}
