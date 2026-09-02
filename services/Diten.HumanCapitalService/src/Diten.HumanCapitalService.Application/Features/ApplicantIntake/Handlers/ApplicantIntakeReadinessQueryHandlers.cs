using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.ApplicantIntake.Queries;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.ApplicantIntake.Handlers;

public sealed class GetApplicantIntakeReadinessListHandler
    : IRequestHandler<GetApplicantIntakeReadinessListQuery, Response<IReadOnlyList<ApplicantIntakeReadinessListItemDto>>>
{
    private readonly IApplicantIntakeReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetApplicantIntakeReadinessListHandler(
        IApplicantIntakeReadinessMetadataRepository repository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<ApplicantIntakeReadinessListItemDto>>> Handle(
        GetApplicantIntakeReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = ApplicantIntakeGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<ApplicantIntakeReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var rows = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<ApplicantIntakeReadinessListItemDto>>.Success(rows.Select(ApplicantIntakeMapper.ToListItem).ToList());
    }
}

public sealed class GetApplicantIntakeReadinessByIdHandler
    : IRequestHandler<GetApplicantIntakeReadinessByIdQuery, Response<ApplicantIntakeReadinessDto>>
{
    private readonly IApplicantIntakeReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetApplicantIntakeReadinessByIdHandler(
        IApplicantIntakeReadinessMetadataRepository repository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<ApplicantIntakeReadinessDto>> Handle(GetApplicantIntakeReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = ApplicantIntakeGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<ApplicantIntakeReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<ApplicantIntakeReadinessDto>.Fail("Applicant intake readiness record was not found.", 404)
            : Response<ApplicantIntakeReadinessDto>.Success(ApplicantIntakeMapper.ToDto(entity));
    }
}

public sealed class GetApplicantIntakeAuditMetadataHandler
    : IRequestHandler<GetApplicantIntakeAuditMetadataQuery, Response<ApplicantIntakeAuditMetadataDto>>
{
    private readonly IApplicantIntakeReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetApplicantIntakeAuditMetadataHandler(
        IApplicantIntakeReadinessMetadataRepository repository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<ApplicantIntakeAuditMetadataDto>> Handle(GetApplicantIntakeAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = ApplicantIntakeGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<ApplicantIntakeAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<ApplicantIntakeAuditMetadataDto>.Fail("Applicant intake readiness record was not found.", 404)
            : Response<ApplicantIntakeAuditMetadataDto>.Success(ApplicantIntakeMapper.ToAuditMetadata(entity));
    }
}
