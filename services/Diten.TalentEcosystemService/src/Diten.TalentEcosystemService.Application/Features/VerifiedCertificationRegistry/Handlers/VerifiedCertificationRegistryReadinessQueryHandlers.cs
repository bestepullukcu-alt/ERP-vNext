using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.VerifiedCertificationRegistry.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.VerifiedCertificationRegistry.Handlers;

public sealed class GetVerifiedCertificationRegistryReadinessListHandler
    : IRequestHandler<GetVerifiedCertificationRegistryReadinessListQuery, Response<IReadOnlyList<VerifiedCertificationRegistryReadinessListItemDto>>>
{
    private readonly IVerifiedCertificationRegistryReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetVerifiedCertificationRegistryReadinessListHandler(IVerifiedCertificationRegistryReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<VerifiedCertificationRegistryReadinessListItemDto>>> Handle(
        GetVerifiedCertificationRegistryReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = VerifiedCertificationRegistryGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<VerifiedCertificationRegistryReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var rows = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<VerifiedCertificationRegistryReadinessListItemDto>>.Success(rows.Select(VerifiedCertificationRegistryMapper.ToListItem).ToList());
    }
}

public sealed class GetVerifiedCertificationRegistryReadinessByIdHandler
    : IRequestHandler<GetVerifiedCertificationRegistryReadinessByIdQuery, Response<VerifiedCertificationRegistryReadinessDto>>
{
    private readonly IVerifiedCertificationRegistryReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetVerifiedCertificationRegistryReadinessByIdHandler(IVerifiedCertificationRegistryReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<VerifiedCertificationRegistryReadinessDto>> Handle(GetVerifiedCertificationRegistryReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = VerifiedCertificationRegistryGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<VerifiedCertificationRegistryReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<VerifiedCertificationRegistryReadinessDto>.Fail("VerifiedCertificationRegistry readiness record was not found.", 404)
            : Response<VerifiedCertificationRegistryReadinessDto>.Success(VerifiedCertificationRegistryMapper.ToDto(entity));
    }
}

public sealed class GetVerifiedCertificationRegistryAuditMetadataHandler
    : IRequestHandler<GetVerifiedCertificationRegistryAuditMetadataQuery, Response<VerifiedCertificationRegistryAuditMetadataDto>>
{
    private readonly IVerifiedCertificationRegistryReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetVerifiedCertificationRegistryAuditMetadataHandler(IVerifiedCertificationRegistryReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<VerifiedCertificationRegistryAuditMetadataDto>> Handle(GetVerifiedCertificationRegistryAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = VerifiedCertificationRegistryGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<VerifiedCertificationRegistryAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<VerifiedCertificationRegistryAuditMetadataDto>.Fail("VerifiedCertificationRegistry readiness record was not found.", 404)
            : Response<VerifiedCertificationRegistryAuditMetadataDto>.Success(VerifiedCertificationRegistryMapper.ToAuditMetadata(entity));
    }
}
