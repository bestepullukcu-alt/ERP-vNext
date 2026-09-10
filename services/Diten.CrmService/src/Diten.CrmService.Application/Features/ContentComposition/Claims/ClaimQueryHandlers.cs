using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.Claims;

public sealed class ListClaimsHandler : IRequestHandler<ListClaimsQuery, Response<ClaimListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IClaimRepository _claims;

    public ListClaimsHandler(ITenantContext tenant, IClaimRepository claims)
    {
        _tenant = tenant;
        _claims = claims;
    }

    public async Task<Response<ClaimListDto>> Handle(ListClaimsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ClaimListDto>.Fail("Tenant context is required.", 400);
        }

        IEnumerable<Claim> rows = await _claims.ListAsync(tenantId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = ClaimStatuses.Normalize(request.Status);
            rows = rows.Where(x => x.Status == status);
        }

        if (request.EffectiveAt is { } at)
        {
            rows = rows.Where(x => x.EffectiveFrom <= at && (x.EffectiveTo is null || at <= x.EffectiveTo));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            rows = rows.Where(x =>
                x.ClaimName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.ClaimCode.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!request.IncludeArchived)
        {
            rows = rows.Where(x => !x.IsArchived());
        }

        var items = rows.Select(ClaimMapper.ToDto).ToList();
        return Response<ClaimListDto>.Success(new ClaimListDto(items, items.Count));
    }
}

public sealed class GetClaimHandler : IRequestHandler<GetClaimQuery, Response<ClaimDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IClaimRepository _claims;

    public GetClaimHandler(ITenantContext tenant, IClaimRepository claims)
    {
        _tenant = tenant;
        _claims = claims;
    }

    public async Task<Response<ClaimDto>> Handle(GetClaimQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ClaimDto>.Fail("Tenant context is required.", 400);
        }

        var entity = await _claims.GetByIdAsync(tenantId, request.ClaimId, cancellationToken);
        return entity is null
            ? Response<ClaimDto>.Fail("Claim not found.", 404)
            : Response<ClaimDto>.Success(ClaimMapper.ToDto(entity));
    }
}
