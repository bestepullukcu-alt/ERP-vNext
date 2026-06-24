using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Handlers.CommandHandlers;

public sealed class CreateQmsBaselineDefinitionHandler
    : IRequestHandler<CreateQmsBaselineDefinitionCommand, Response<QmsCollectionDefinitionModel>>
{
    private readonly IBaselineReleaseRepository _baselineRepository;
    private readonly ICollectionDefinitionRepository _definitionRepository;
    private readonly QmsManualStructureService _manualService;
    private readonly ITenantContext _tenantContext;

    public CreateQmsBaselineDefinitionHandler(
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

    public async Task<Response<QmsCollectionDefinitionModel>> Handle(CreateQmsBaselineDefinitionCommand request, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);
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

        var existing = await _definitionRepository.GetByBaselineAsync(baseline.Id, ct);
        var result = _manualService.CreateDefinition(tenantId, baseline, request.Request, existing);
        if (!result.Success)
        {
            return Response<QmsCollectionDefinitionModel>.Fail(result.Errors, result.StatusCode, result.ReasonCode, request.CorrelationId);
        }

        var created = await _definitionRepository.CreateAsync(result.Value!, ct);
        return Response<QmsCollectionDefinitionModel>.Success(QmsBaselineMapping.ToDefinitionModel(created), 201, request.CorrelationId);
    }
}
