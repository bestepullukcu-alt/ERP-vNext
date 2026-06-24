using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Handlers.CommandHandlers;

public sealed class DeleteQmsBaselineDefinitionHandler
    : IRequestHandler<DeleteQmsBaselineDefinitionCommand, Response<NoContent>>
{
    private readonly IBaselineReleaseRepository _baselineRepository;
    private readonly ICollectionDefinitionRepository _definitionRepository;
    private readonly ITenantContext _tenantContext;

    public DeleteQmsBaselineDefinitionHandler(
        IBaselineReleaseRepository baselineRepository,
        ICollectionDefinitionRepository definitionRepository,
        ITenantContext tenantContext)
    {
        _baselineRepository = baselineRepository;
        _definitionRepository = definitionRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<NoContent>> Handle(DeleteQmsBaselineDefinitionCommand request, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var baseline = await _baselineRepository.GetByIdAsync(request.BaselineReleaseId, ct);
        if (baseline is null)
        {
            return Response<NoContent>.Fail("Baseline not found.", 404, QmsBaselineReasonCodes.NotFoundNonLeakage, request.CorrelationId);
        }

        if (baseline.Status != BaselineReleaseStatus.Draft)
        {
            return Response<NoContent>.Fail("Only a DRAFT baseline can be edited.", 400, QmsBaselineReasonCodes.ValidationFailed, request.CorrelationId);
        }

        var target = await _definitionRepository.GetByCanonicalIdAsync(baseline.Id, request.CanonicalId, ct);
        if (target is null)
        {
            return Response<NoContent>.Fail("Definition not found.", 404, QmsBaselineReasonCodes.NotFoundNonLeakage, request.CorrelationId);
        }

        if (request.VersionToken > 0 && target.Version != request.VersionToken)
        {
            return Response<NoContent>.Fail("Stale definition version.", 409, QmsBaselineReasonCodes.Conflict, request.CorrelationId);
        }

        var definitions = await _definitionRepository.GetByBaselineAsync(baseline.Id, ct);
        var activeChildCount = definitions.Count(d => string.Equals(d.ParentCanonicalId, target.CanonicalId, StringComparison.OrdinalIgnoreCase));
        if (activeChildCount > 0)
        {
            return Response<NoContent>.Fail(
                $"Definition has {activeChildCount} active child node(s). Delete children first.",
                409,
                QmsBaselineReasonCodes.Conflict,
                request.CorrelationId);
        }

        var expectedVersion = request.VersionToken > 0 ? request.VersionToken : target.Version;
        var deleted = await _definitionRepository.SoftDeleteAsync(target, expectedVersion, ct);
        if (!deleted)
        {
            return Response<NoContent>.Fail("Stale definition version.", 409, QmsBaselineReasonCodes.Conflict, request.CorrelationId);
        }

        return Response<NoContent>.Success(204, request.CorrelationId);
    }
}
