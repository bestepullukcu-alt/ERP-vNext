using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Queries;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Resolve;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using Vfp = Diten.CrmService.Domain.Entities.VisitFrequencyPolicy;

namespace Diten.CrmService.Application.Features.VisitFrequencyPolicy.Handlers;

public sealed class ListVisitFrequencyPoliciesHandler
    : IRequestHandler<ListVisitFrequencyPoliciesQuery, Response<VisitFrequencyPolicyListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IVisitFrequencyPolicyRepository _repository;

    public ListVisitFrequencyPoliciesHandler(ITenantContext tenant, IVisitFrequencyPolicyRepository repository)
    {
        _tenant = tenant;
        _repository = repository;
    }

    public async Task<Response<VisitFrequencyPolicyListDto>> Handle(
        ListVisitFrequencyPoliciesQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<VisitFrequencyPolicyListDto>.Fail("Tenant context is required.", 400);
        }

        IEnumerable<Vfp> rows = await _repository.ListAsync(tenantId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.TargetType))
        {
            var targetType = FrequencyTargetType.Normalize(request.TargetType);
            rows = rows.Where(p => p.TargetType == targetType);
        }

        if (request.TargetId is { } targetId && targetId != Guid.Empty)
        {
            rows = rows.Where(p => p.TargetId == targetId);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = FrequencyPolicyStatus.Normalize(request.Status);
            rows = rows.Where(p => p.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Source))
        {
            var source = FrequencySource.Normalize(request.Source);
            rows = rows.Where(p => p.Source == source);
        }

        var items = rows.Select(VisitFrequencyPolicyMapper.ToDto).ToList();
        return Response<VisitFrequencyPolicyListDto>.Success(new VisitFrequencyPolicyListDto(items, items.Count));
    }
}

public sealed class GetVisitFrequencyPolicyHandler
    : IRequestHandler<GetVisitFrequencyPolicyQuery, Response<VisitFrequencyPolicyDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IVisitFrequencyPolicyRepository _repository;

    public GetVisitFrequencyPolicyHandler(ITenantContext tenant, IVisitFrequencyPolicyRepository repository)
    {
        _tenant = tenant;
        _repository = repository;
    }

    public async Task<Response<VisitFrequencyPolicyDto>> Handle(
        GetVisitFrequencyPolicyQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<VisitFrequencyPolicyDto>.Fail("Tenant context is required.", 400);
        }

        var policy = await _repository.GetByIdAsync(tenantId, request.PolicyId, cancellationToken);
        return policy is null
            ? Response<VisitFrequencyPolicyDto>.Fail("Visit frequency policy not found.", 404)
            : Response<VisitFrequencyPolicyDto>.Success(VisitFrequencyPolicyMapper.ToDto(policy));
    }
}

/// <summary>
/// Read-only resolve provider. Loads the active candidate policies for the requested target (primary + caller-supplied
/// context ids) and runs the deterministic <see cref="VisitFrequencyResolveEngine"/>. This handler performs NO writes.
/// </summary>
public sealed class ResolveVisitFrequencyPolicyHandler
    : IRequestHandler<ResolveVisitFrequencyPolicyQuery, Response<VisitFrequencyResolveResult>>
{
    private readonly ITenantContext _tenant;
    private readonly IVisitFrequencyPolicyResolver _resolver;

    public ResolveVisitFrequencyPolicyHandler(ITenantContext tenant, IVisitFrequencyPolicyResolver resolver)
    {
        _tenant = tenant;
        _resolver = resolver;
    }

    public async Task<Response<VisitFrequencyResolveResult>> Handle(
        ResolveVisitFrequencyPolicyQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } _)
        {
            return Response<VisitFrequencyResolveResult>.Fail("Tenant context is required.", 400);
        }

        if (VisitFrequencyPolicyValidation.ValidateTargetType(request.TargetType) is { } targetTypeError)
        {
            return Response<VisitFrequencyResolveResult>.Fail(targetTypeError, 400);
        }

        if (VisitFrequencyPolicyValidation.ValidateTargetId(request.TargetId) is { } targetIdError)
        {
            return Response<VisitFrequencyResolveResult>.Fail(targetIdError, 400);
        }

        // Single source of truth: the FU03 endpoint and the FU09B route-candidate reader both call the same resolver.
        var result = await _resolver.ResolveAsync(request, cancellationToken);
        return Response<VisitFrequencyResolveResult>.Success(result);
    }
}
