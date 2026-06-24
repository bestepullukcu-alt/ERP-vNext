using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Handlers.CommandHandlers;

public sealed class UpdateQmsBaselineDefinitionHandler
    : IRequestHandler<UpdateQmsBaselineDefinitionCommand, Response<QmsCollectionDefinitionModel>>
{
    private readonly IBaselineReleaseRepository _baselineRepository;
    private readonly ICollectionDefinitionRepository _definitionRepository;
    private readonly QmsManualStructureService _manualService;
    private readonly ITenantContext _tenantContext;

    public UpdateQmsBaselineDefinitionHandler(
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

    public async Task<Response<QmsCollectionDefinitionModel>> Handle(UpdateQmsBaselineDefinitionCommand request, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var guard = await LoadDraftTargetAsync(request.BaselineReleaseId, request.CanonicalId, request.CorrelationId, ct);
        if (!guard.Response.IsSuccessful)
        {
            return guard.Response;
        }

        var existing = await _definitionRepository.GetByBaselineAsync(guard.Baseline!.Id, ct);
        if (request.Request.VersionToken > 0 && guard.Target!.Version != request.Request.VersionToken)
        {
            return Response<QmsCollectionDefinitionModel>.Fail(
                "Stale definition version.", 409, QmsBaselineReasonCodes.Conflict, request.CorrelationId);
        }

        var result = _manualService.UpdateDefinition(guard.Target!, request.Request, existing);
        if (!result.Success)
        {
            return Response<QmsCollectionDefinitionModel>.Fail(result.Errors, result.StatusCode, result.ReasonCode, request.CorrelationId);
        }

        var changed = result.Value!;
        var expectedVersion = request.Request.VersionToken > 0 ? request.Request.VersionToken : guard.Target!.Version;
        var updated = await _definitionRepository.UpdateAsync(guard.Target!, expectedVersion, ct);
        if (!updated)
        {
            return Response<QmsCollectionDefinitionModel>.Fail(
                "Stale definition version.", 409, QmsBaselineReasonCodes.Conflict, request.CorrelationId);
        }

        var descendants = changed.Where(d => d.Id != guard.Target!.Id).ToList();
        if (descendants.Count > 0)
        {
            await _definitionRepository.UpdateManyAsync(descendants, ct);
        }

        return Response<QmsCollectionDefinitionModel>.Success(QmsBaselineMapping.ToDefinitionModel(guard.Target!), 200, request.CorrelationId);
    }

    private async Task<(Response<QmsCollectionDefinitionModel> Response, BaselineRelease? Baseline, CollectionDefinition? Target)> LoadDraftTargetAsync(
        Guid baselineReleaseId,
        string canonicalId,
        string correlationId,
        CancellationToken ct)
    {
        var baseline = await _baselineRepository.GetByIdAsync(baselineReleaseId, ct);
        if (baseline is null)
        {
            return (Response<QmsCollectionDefinitionModel>.Fail(
                "Baseline not found.", 404, QmsBaselineReasonCodes.NotFoundNonLeakage, correlationId), null, null);
        }

        if (baseline.Status != BaselineReleaseStatus.Draft)
        {
            return (Response<QmsCollectionDefinitionModel>.Fail(
                "Only a DRAFT baseline can be edited.", 400, QmsBaselineReasonCodes.ValidationFailed, correlationId), baseline, null);
        }

        var target = await _definitionRepository.GetByCanonicalIdAsync(baseline.Id, canonicalId, ct);
        if (target is null)
        {
            return (Response<QmsCollectionDefinitionModel>.Fail(
                "Definition not found.", 404, QmsBaselineReasonCodes.NotFoundNonLeakage, correlationId), baseline, null);
        }

        return (Response<QmsCollectionDefinitionModel>.Success(QmsBaselineMapping.ToDefinitionModel(target), 200, correlationId), baseline, target);
    }
}
