using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.PayBenchmarking.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.PayBenchmarking.Handlers;

public sealed class GetPayBenchmarkingReadinessListHandler
    : IRequestHandler<GetPayBenchmarkingReadinessListQuery, Response<IReadOnlyList<PayBenchmarkingReadinessListItemDto>>>
{
    private readonly IPayBenchmarkingReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetPayBenchmarkingReadinessListHandler(IPayBenchmarkingReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<PayBenchmarkingReadinessListItemDto>>> Handle(
        GetPayBenchmarkingReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = PayBenchmarkingGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<PayBenchmarkingReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var rows = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<PayBenchmarkingReadinessListItemDto>>.Success(rows.Select(PayBenchmarkingMapper.ToListItem).ToList());
    }
}

public sealed class GetPayBenchmarkingReadinessByIdHandler
    : IRequestHandler<GetPayBenchmarkingReadinessByIdQuery, Response<PayBenchmarkingReadinessDto>>
{
    private readonly IPayBenchmarkingReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetPayBenchmarkingReadinessByIdHandler(IPayBenchmarkingReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<PayBenchmarkingReadinessDto>> Handle(GetPayBenchmarkingReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = PayBenchmarkingGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<PayBenchmarkingReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<PayBenchmarkingReadinessDto>.Fail("PayBenchmarking readiness record was not found.", 404)
            : Response<PayBenchmarkingReadinessDto>.Success(PayBenchmarkingMapper.ToDto(entity));
    }
}

public sealed class GetPayBenchmarkingAuditMetadataHandler
    : IRequestHandler<GetPayBenchmarkingAuditMetadataQuery, Response<PayBenchmarkingAuditMetadataDto>>
{
    private readonly IPayBenchmarkingReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetPayBenchmarkingAuditMetadataHandler(IPayBenchmarkingReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<PayBenchmarkingAuditMetadataDto>> Handle(GetPayBenchmarkingAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = PayBenchmarkingGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<PayBenchmarkingAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<PayBenchmarkingAuditMetadataDto>.Fail("PayBenchmarking readiness record was not found.", 404)
            : Response<PayBenchmarkingAuditMetadataDto>.Success(PayBenchmarkingMapper.ToAuditMetadata(entity));
    }
}
