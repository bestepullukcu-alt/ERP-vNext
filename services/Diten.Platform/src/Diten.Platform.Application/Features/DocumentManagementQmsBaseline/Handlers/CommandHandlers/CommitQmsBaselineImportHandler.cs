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

public sealed class CommitQmsBaselineImportHandler
    : IRequestHandler<CommitQmsBaselineImportCommand, Response<QmsBaselineCommitResult>>
{
    private readonly QmsBaselineImportService _importService;
    private readonly IBaselineReleaseRepository _baselineRepository;
    private readonly ICollectionDefinitionRepository _definitionRepository;
    private readonly ITenantContext _tenantContext;

    public CommitQmsBaselineImportHandler(
        QmsBaselineImportService importService,
        IBaselineReleaseRepository baselineRepository,
        ICollectionDefinitionRepository definitionRepository,
        ITenantContext tenantContext)
    {
        _importService = importService;
        _baselineRepository = baselineRepository;
        _definitionRepository = definitionRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<QmsBaselineCommitResult>> Handle(CommitQmsBaselineImportCommand request, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);

        var plan = await _importService.BuildPlanAsync(
            request.FileName, request.Format, request.ContentBase64, request.SourceBaselineKey, tenantId, ct);

        var failure = QmsBaselineMapping.ClassifyFailure(plan.Summary);
        if (failure is { } f)
        {
            return Response<QmsBaselineCommitResult>.Fail(f.Errors, f.Status, f.ReasonCode, request.CorrelationId);
        }

        var baseline = new BaselineRelease
        {
            TenantId = tenantId,
            BaselineReleaseId = $"BR-{Guid.NewGuid():N}"[..15].ToUpperInvariant(),
            SourceBaselineKey = request.SourceBaselineKey,
            BaselineVersion = request.BaselineVersion.Trim(),
            Status = BaselineReleaseStatus.Draft,
            ChangeSummary = request.ChangeSummary?.Trim(),
            DeprecationNoticeWindowDays = 0
        };
        await _baselineRepository.CreateAsync(baseline, ct);

        var definitions = plan.Definitions.Select(d => new CollectionDefinition
        {
            TenantId = tenantId,
            CanonicalId = d.CanonicalId,
            ParentCanonicalId = d.ParentCanonicalId,
            BaselineReleaseId = baseline.Id,
            Name = d.Name,
            PurposeScope = d.PurposeScope,
            RequiredByScope = d.RequiredByScope,
            AllowsManualChildren = d.AllowsManualChildren,
            TemplatesAllowed = d.TemplatesAllowed,
            AllowedDocClass = d.AllowedDocClass,
            DefaultClassificationLevel = d.DefaultClassificationLevel,
            DefaultRetentionHint = d.DefaultRetentionHint,
            IsMandatory = d.IsMandatory,
            IsAutoProvisioned = d.IsAutoProvisioned,
            IsProtected = d.IsProtected,
            PathSegment = d.PathSegment,
            FullPath = d.FullPath,
            DisplayOrder = d.DisplayOrder,
            Status = CollectionDefinitionStatus.Active,
            DefinitionHash = d.DefinitionHash
        }).ToList();
        await _definitionRepository.CreateManyAsync(definitions, ct);

        var committedSummary = plan.Summary with { DryRun = false, Committed = true };
        var result = new QmsBaselineCommitResult(committedSummary, baseline.Id, baseline.BaselineReleaseId);
        return Response<QmsBaselineCommitResult>.Success(result, 201, request.CorrelationId);
    }
}
