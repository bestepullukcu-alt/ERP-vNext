using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Handlers.CommandHandlers;

public sealed class MoveQmsBaselineDefinitionHandler
    : IRequestHandler<MoveQmsBaselineDefinitionCommand, Response<QmsCollectionDefinitionModel>>
{
    private readonly IBaselineReleaseRepository _baselineRepository;
    private readonly ICollectionDefinitionRepository _definitionRepository;
    private readonly QmsManualStructureService _manualService;
    private readonly ITenantContext _tenantContext;

    public MoveQmsBaselineDefinitionHandler(
        IBaselineReleaseRepository baselineRepository,
        ICollectionDefinitionRepository definitionRepository,
        QmsManualStructureService manualService,
        ITenantContext tenantContext)
    {
        _baselineRepository = baselineRepository;
        _definitionRepository = definitionRepository;
        _manualService = manualService;
        _tenantContext = tenantContext;
    }

    public async Task<Response<QmsCollectionDefinitionModel>> Handle(MoveQmsBaselineDefinitionCommand request, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var baseline = await _baselineRepository.GetByIdAsync(request.BaselineReleaseId, ct);
        if (baseline is null)
        {
            return Response<QmsCollectionDefinitionModel>.Fail(
                "Baseline not found.", 404, QmsBaselineReasonCodes.NotFoundNonLeakage, request.CorrelationId);
        }

        if (baseline.Status != BaselineReleaseStatus.Draft)
        {
            return Response<QmsCollectionDefinitionModel>.Fail(
                "Only a DRAFT baseline can be edited.", 400, QmsBaselineReasonCodes.ValidationFailed, request.CorrelationId);
        }

        var target = await _definitionRepository.GetByCanonicalIdAsync(baseline.Id, request.CanonicalId, ct);
        if (target is null)
        {
            return Response<QmsCollectionDefinitionModel>.Fail(
                "Definition not found.", 404, QmsBaselineReasonCodes.NotFoundNonLeakage, request.CorrelationId);
        }

        if (request.Request.VersionToken > 0 && target.Version != request.Request.VersionToken)
        {
            return Response<QmsCollectionDefinitionModel>.Fail(
                "Stale definition version.", 409, QmsBaselineReasonCodes.Conflict, request.CorrelationId);
        }

        var existing = await _definitionRepository.GetByBaselineAsync(baseline.Id, ct);
        var result = _manualService.MoveDefinition(target, request.Request, existing);
        if (!result.Success)
        {
            return Response<QmsCollectionDefinitionModel>.Fail(result.Errors, result.StatusCode, result.ReasonCode, request.CorrelationId);
        }

        var changed = result.Value!;
        var expectedVersion = request.Request.VersionToken > 0 ? request.Request.VersionToken : target.Version;
        var updated = await _definitionRepository.UpdateAsync(target, expectedVersion, ct);
        if (!updated)
        {
            return Response<QmsCollectionDefinitionModel>.Fail(
                "Stale definition version.", 409, QmsBaselineReasonCodes.Conflict, request.CorrelationId);
        }

        var descendants = changed.Where(d => d.Id != target.Id).ToList();
        if (descendants.Count > 0)
        {
            await _definitionRepository.UpdateManyAsync(descendants, ct);
        }

        return Response<QmsCollectionDefinitionModel>.Success(QmsBaselineMapping.ToDefinitionModel(target), 200, request.CorrelationId);
    }
}
