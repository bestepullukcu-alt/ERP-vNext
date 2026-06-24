using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Handlers.CommandHandlers;

public sealed class ValidateQmsBaselineDraftHandler
    : IRequestHandler<ValidateQmsBaselineDraftCommand, Response<QmsDraftTreeValidationResult>>
{
    private readonly IBaselineReleaseRepository _baselineRepository;
    private readonly ICollectionDefinitionRepository _definitionRepository;
    private readonly QmsManualStructureService _manualService;
    private readonly ITenantContext _tenantContext;

    public ValidateQmsBaselineDraftHandler(
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

    public async Task<Response<QmsDraftTreeValidationResult>> Handle(ValidateQmsBaselineDraftCommand request, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var baseline = await _baselineRepository.GetByIdAsync(request.BaselineReleaseId, ct);
        if (baseline is null)
        {
            return Response<QmsDraftTreeValidationResult>.Fail(
                "Baseline not found.", 404, QmsBaselineReasonCodes.NotFoundNonLeakage, request.CorrelationId);
        }

        if (baseline.Status != BaselineReleaseStatus.Draft)
        {
            return Response<QmsDraftTreeValidationResult>.Fail(
                "Only a DRAFT baseline can be validated for manual edits.", 400, QmsBaselineReasonCodes.ValidationFailed, request.CorrelationId);
        }

        var definitions = await _definitionRepository.GetByBaselineAsync(baseline.Id, ct);
        var validation = _manualService.ValidateTree(definitions);
        var result = new QmsDraftTreeValidationResult(
            validation.Valid,
            validation.Errors,
            validation.Warnings,
            validation.DuplicateSiblingFindings,
            validation.OrphanParentFindings,
            validation.InvalidHierarchyFindings);
        return Response<QmsDraftTreeValidationResult>.Success(result, 200, request.CorrelationId);
    }
}
