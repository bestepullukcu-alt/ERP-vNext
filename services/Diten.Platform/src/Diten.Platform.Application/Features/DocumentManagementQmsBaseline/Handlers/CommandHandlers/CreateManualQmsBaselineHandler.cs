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
        var sourceKey = string.IsNullOrWhiteSpace(request.Request.Name)
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
