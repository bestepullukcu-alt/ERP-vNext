using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Handlers.CommandHandlers;

/// <summary>
/// MOD-0028-FU08 — APPROVED → EFFECTIVE. Enforces a single Effective baseline per tenant + source key by
/// superseding the previous Effective baseline (non-destructive: it is retained as SUPERSEDED). Blocked when the
/// source register/package is still Draft/not-for-execution.
/// </summary>
public sealed class MarkEffectiveQmsBaselineHandler
    : IRequestHandler<MarkEffectiveQmsBaselineCommand, Response<QmsBaselineSummaryModel>>
{
    private readonly IBaselineReleaseRepository _baselineRepository;
    private readonly ICollectionDefinitionRepository _definitionRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public MarkEffectiveQmsBaselineHandler(
        IBaselineReleaseRepository baselineRepository,
        ICollectionDefinitionRepository definitionRepository,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _baselineRepository = baselineRepository;
        _definitionRepository = definitionRepository;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<QmsBaselineSummaryModel>> Handle(MarkEffectiveQmsBaselineCommand request, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);

        var baseline = await _baselineRepository.GetByIdAsync(request.BaselineReleaseId, ct);
        if (baseline is null)
        {
            return Response<QmsBaselineSummaryModel>.Fail(
                "Baseline not found.", 404, QmsBaselineReasonCodes.NotFoundNonLeakage, request.CorrelationId);
        }

        if (baseline.Status != BaselineReleaseStatus.Approved)
        {
            return Response<QmsBaselineSummaryModel>.Fail(
                "Only an APPROVED baseline can be marked effective.", 400, QmsBaselineReasonCodes.ValidationFailed, request.CorrelationId);
        }

        // Hard gate: the source register/package must itself be approved for execution.
        if (!BaselinePackageStatus.AllowsEffective(baseline.SourcePackageStatus))
        {
            return Response<QmsBaselineSummaryModel>.Fail(
                "Source register/package is still draft (not for execution); it cannot be marked effective.",
                409, QmsBaselineReasonCodes.PackageNotApproved, request.CorrelationId);
        }

        if (request.ExpectedVersion > 0 && baseline.Version != request.ExpectedVersion)
        {
            return Response<QmsBaselineSummaryModel>.Fail(
                "Stale baseline version.", 409, QmsBaselineReasonCodes.Conflict, request.CorrelationId);
        }

        // Supersede the current Effective baseline of the same source key (non-destructive, retained for history).
        var all = await _baselineRepository.GetAllAsync(ct);
        var previousEffective = all
            .Where(x => x.Id != baseline.Id
                && x.Status == BaselineReleaseStatus.Effective
                && string.Equals(x.SourceBaselineKey, baseline.SourceBaselineKey, StringComparison.Ordinal))
            .OrderByDescending(x => x.EffectiveAt ?? x.CreatedAt)
            .ToList();

        var now = DateTimeOffset.UtcNow;
        foreach (var prior in previousEffective)
        {
            var priorExpected = prior.Version;
            prior.Status = BaselineReleaseStatus.Superseded;
            prior.SupersededAt = now;
            prior.SupersededByBaselineReleaseId = baseline.Id;
            await _baselineRepository.UpdateAsync(prior, priorExpected, ct);
        }

        var expectedVersion = baseline.Version;
        baseline.Status = BaselineReleaseStatus.Effective;
        baseline.EffectiveAt = now;
        baseline.EffectiveBy = _currentUser.ActorName;
        baseline.SupersedesBaselineReleaseId = previousEffective.FirstOrDefault()?.Id;

        var updated = await _baselineRepository.UpdateAsync(baseline, expectedVersion, ct);
        if (!updated)
        {
            return Response<QmsBaselineSummaryModel>.Fail(
                "Stale baseline version.", 409, QmsBaselineReasonCodes.Conflict, request.CorrelationId);
        }

        var definitions = await _definitionRepository.GetByBaselineAsync(baseline.Id, ct);
        return Response<QmsBaselineSummaryModel>.Success(
            QmsBaselineMapping.ToSummaryModel(baseline, definitions.Count), 200, request.CorrelationId);
    }
}
