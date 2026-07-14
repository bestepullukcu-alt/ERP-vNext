using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Handlers.CommandHandlers;

public sealed class CreateManualQmsBaselineHandler
    : IRequestHandler<CreateManualQmsBaselineCommand, Response<QmsBaselineSummaryModel>>
{
    private readonly IBaselineReleaseRepository _baselineRepository;
    private readonly ITenantContext _tenantContext;

    public CreateManualQmsBaselineHandler(IBaselineReleaseRepository baselineRepository, ITenantContext tenantContext)
    {
        _baselineRepository = baselineRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<QmsBaselineSummaryModel>> Handle(CreateManualQmsBaselineCommand request, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var baselineKey = $"BR-MAN-{Guid.NewGuid():N}"[..18].ToUpperInvariant();
        // MOD-0028-FU08 — explicit source key wins (lets a manual baseline join an existing lineage, e.g. an
        // imported register key, so MarkEffective supersedes the prior Effective). Otherwise fall back to the
        // name-derived key, and finally to a unique per-baseline key when both are blank.
        var sourceKey = !string.IsNullOrWhiteSpace(request.Request.SourceBaselineKey)
            ? request.Request.SourceBaselineKey.Trim()
            : string.IsNullOrWhiteSpace(request.Request.Name)
                ? baselineKey
                : $"manual:{request.Request.Name.Trim()}";

        var baseline = new BaselineRelease
        {
            TenantId = tenantId,
            BaselineReleaseId = baselineKey,
            SourceBaselineKey = sourceKey,
            BaselineVersion = request.Request.BaselineVersion.Trim(),
            EffectiveDate = request.Request.EffectiveDate,
            Status = BaselineReleaseStatus.Draft,
            ChangeSummary = request.Request.ChangeSummary?.Trim(),
            DeprecationNoticeWindowDays = 0
        };

        await _baselineRepository.CreateAsync(baseline, ct);
        return Response<QmsBaselineSummaryModel>.Success(
            QmsBaselineMapping.ToSummaryModel(baseline, definitionCount: 0),
            201,
            request.CorrelationId);
    }
}
