using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Queries;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Handlers.QueryHandlers;

public sealed class GetQmsBaselineListHandler
    : IRequestHandler<GetQmsBaselineListQuery, Response<IReadOnlyList<QmsBaselineSummaryModel>>>
{
    private readonly IBaselineReleaseRepository _baselineRepository;
    private readonly ICollectionDefinitionRepository _definitionRepository;
    private readonly ITenantContext _tenantContext;

    public GetQmsBaselineListHandler(
        IBaselineReleaseRepository baselineRepository,
        ICollectionDefinitionRepository definitionRepository,
        ITenantContext tenantContext)
    {
        _baselineRepository = baselineRepository;
        _definitionRepository = definitionRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<QmsBaselineSummaryModel>>> Handle(GetQmsBaselineListQuery request, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var baselines = await _baselineRepository.GetAllAsync(ct);

        var models = new List<QmsBaselineSummaryModel>(baselines.Count);
        foreach (var baseline in baselines)
        {
            var definitions = await _definitionRepository.GetByBaselineAsync(baseline.Id, ct);
            models.Add(QmsBaselineMapping.ToSummaryModel(baseline, definitions.Count));
        }

        return Response<IReadOnlyList<QmsBaselineSummaryModel>>.Success(models, 200, request.CorrelationId);
    }
}

public sealed class GetQmsBaselineByIdHandler
    : IRequestHandler<GetQmsBaselineByIdQuery, Response<QmsBaselineSummaryModel>>
{
    private readonly IBaselineReleaseRepository _baselineRepository;
    private readonly ICollectionDefinitionRepository _definitionRepository;
    private readonly ITenantContext _tenantContext;

    public GetQmsBaselineByIdHandler(
        IBaselineReleaseRepository baselineRepository,
        ICollectionDefinitionRepository definitionRepository,
        ITenantContext tenantContext)
    {
        _baselineRepository = baselineRepository;
        _definitionRepository = definitionRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<QmsBaselineSummaryModel>> Handle(GetQmsBaselineByIdQuery request, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var baseline = await _baselineRepository.GetByIdAsync(request.BaselineReleaseId, ct);
        if (baseline is null)
        {
            return Response<QmsBaselineSummaryModel>.Fail(
                "Baseline not found.", 404, QmsBaselineReasonCodes.NotFoundNonLeakage, request.CorrelationId);
        }

        var definitions = await _definitionRepository.GetByBaselineAsync(baseline.Id, ct);
        return Response<QmsBaselineSummaryModel>.Success(
            QmsBaselineMapping.ToSummaryModel(baseline, definitions.Count), 200, request.CorrelationId);
    }
}

public sealed class GetQmsBaselineDefinitionsHandler
    : IRequestHandler<GetQmsBaselineDefinitionsQuery, Response<IReadOnlyList<QmsCollectionDefinitionModel>>>
{
    private readonly IBaselineReleaseRepository _baselineRepository;
    private readonly ICollectionDefinitionRepository _definitionRepository;
    private readonly ITenantContext _tenantContext;

    public GetQmsBaselineDefinitionsHandler(
        IBaselineReleaseRepository baselineRepository,
        ICollectionDefinitionRepository definitionRepository,
        ITenantContext tenantContext)
    {
        _baselineRepository = baselineRepository;
        _definitionRepository = definitionRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<QmsCollectionDefinitionModel>>> Handle(GetQmsBaselineDefinitionsQuery request, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var baseline = await _baselineRepository.GetByIdAsync(request.BaselineReleaseId, ct);
        if (baseline is null)
        {
            return Response<IReadOnlyList<QmsCollectionDefinitionModel>>.Fail(
                "Baseline not found.", 404, QmsBaselineReasonCodes.NotFoundNonLeakage, request.CorrelationId);
        }

        var definitions = await _definitionRepository.GetByBaselineAsync(baseline.Id, ct);
        var models = definitions.Select(QmsBaselineMapping.ToDefinitionModel).ToList();
        return Response<IReadOnlyList<QmsCollectionDefinitionModel>>.Success(models, 200, request.CorrelationId);
    }
}

public sealed class GetQmsBaselineDefinitionByCanonicalIdHandler
    : IRequestHandler<GetQmsBaselineDefinitionByCanonicalIdQuery, Response<QmsCollectionDefinitionModel>>
{
    private readonly IBaselineReleaseRepository _baselineRepository;
    private readonly ICollectionDefinitionRepository _definitionRepository;
    private readonly ITenantContext _tenantContext;

    public GetQmsBaselineDefinitionByCanonicalIdHandler(
        IBaselineReleaseRepository baselineRepository,
        ICollectionDefinitionRepository definitionRepository,
        ITenantContext tenantContext)
    {
        _baselineRepository = baselineRepository;
        _definitionRepository = definitionRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<QmsCollectionDefinitionModel>> Handle(GetQmsBaselineDefinitionByCanonicalIdQuery request, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var baseline = await _baselineRepository.GetByIdAsync(request.BaselineReleaseId, ct);
        if (baseline is null)
        {
            return Response<QmsCollectionDefinitionModel>.Fail(
                "Baseline not found.", 404, QmsBaselineReasonCodes.NotFoundNonLeakage, request.CorrelationId);
        }

        var definition = await _definitionRepository.GetByCanonicalIdAsync(baseline.Id, request.CanonicalId, ct);
        if (definition is null)
        {
            return Response<QmsCollectionDefinitionModel>.Fail(
                "Definition not found.", 404, QmsBaselineReasonCodes.NotFoundNonLeakage, request.CorrelationId);
        }

        return Response<QmsCollectionDefinitionModel>.Success(
            QmsBaselineMapping.ToDefinitionModel(definition), 200, request.CorrelationId);
    }
}
