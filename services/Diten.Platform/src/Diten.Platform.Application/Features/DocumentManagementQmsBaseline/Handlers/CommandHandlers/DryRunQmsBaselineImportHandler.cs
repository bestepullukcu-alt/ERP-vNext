using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Services;
using Diten.Platform.Common.Tenancy;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Handlers.CommandHandlers;

public sealed class DryRunQmsBaselineImportHandler
    : IRequestHandler<DryRunQmsBaselineImportCommand, Response<QmsBaselineImportSummary>>
{
    private readonly QmsBaselineImportService _importService;
    private readonly ITenantContext _tenantContext;

    public DryRunQmsBaselineImportHandler(QmsBaselineImportService importService, ITenantContext tenantContext)
    {
        _importService = importService;
        _tenantContext = tenantContext;
    }

    public async Task<Response<QmsBaselineImportSummary>> Handle(DryRunQmsBaselineImportCommand request, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);

        var plan = await _importService.BuildPlanAsync(
            request.FileName, request.Format, request.ContentBase64, request.SourceBaselineKey, tenantId, ct);

        var failure = QmsBaselineMapping.ClassifyFailure(plan.Summary);
        if (failure is { } f)
        {
            return Response<QmsBaselineImportSummary>.Fail(f.Errors, f.Status, f.ReasonCode, request.CorrelationId);
        }

        // Dry-run persists nothing; the summary is the deliverable.
        return Response<QmsBaselineImportSummary>.Success(plan.Summary, 200, request.CorrelationId);
    }
}
