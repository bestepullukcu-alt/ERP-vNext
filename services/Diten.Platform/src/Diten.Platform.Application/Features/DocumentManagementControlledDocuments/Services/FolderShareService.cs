using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;

/// <summary>
/// MOD-0029-FU01 — folder/branch share dry-run → execute. Dry-run mutates nothing; execute shares/copies ONLY
/// the selected branch and its included templates. The same flow <c>correlation_id</c> threads dry-run →
/// execute. Per-item outcomes carry honest counts and reason codes.
/// </summary>
public sealed class FolderShareService
{
    private readonly IFolderSharePlanner _planner;
    private readonly TemplateSharingService _sharing;
    private readonly IFolderShareOperationRepository _operations;
    private readonly IFolderShareOutcomeRepository _outcomes;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;

    public FolderShareService(
        IFolderSharePlanner planner,
        TemplateSharingService sharing,
        IFolderShareOperationRepository operations,
        IFolderShareOutcomeRepository outcomes,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext)
    {
        _planner = planner;
        _sharing = sharing;
        _operations = operations;
        _outcomes = outcomes;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    public async Task<Response<FolderShareResultModel>> DryRunAsync(FolderShareInput input, string correlationId, CancellationToken ct)
    {
        var planResponse = await _planner.PlanAsync(
            input.SourceBranchCollectionInstanceId, input.TargetCompanyId, input.IncludeTemplates,
            ControlledDocumentWire.ParseShareMode(input.ShareMode), correlationId, ct);
        if (!planResponse.IsSuccessful)
        {
            return Response<FolderShareResultModel>.Fail(planResponse.Errors, planResponse.StatusCode, planResponse.ReasonCode, correlationId);
        }

        var plan = planResponse.Data!;
        var outcomes = new List<FolderShareOutcomeModel>();
        foreach (var folder in plan.Folders)
        {
            outcomes.Add(new FolderShareOutcomeModel("FOLDER", folder.CanonicalId, "SHARED", "FOLDER_WOULD_SHARE", folder.FullPath, false));
        }

        foreach (var template in plan.IncludedTemplates)
        {
            outcomes.Add(new FolderShareOutcomeModel("TEMPLATE", template.TemplateKey, "SHARED", "TEMPLATE_WOULD_SHARE", template.Title, false));
        }

        foreach (var skip in plan.SkippedTemplates)
        {
            outcomes.Add(new FolderShareOutcomeModel("TEMPLATE", skip.TemplateKey, "SKIPPED", skip.ReasonCode, skip.Message, false));
        }

        var result = new FolderShareResultModel(
            plan.OperationId,
            plan.SourceCompanyId,
            plan.TargetCompanyId,
            plan.SourceBranchCollectionInstanceId,
            plan.IncludeTemplates,
            plan.ShareMode.ToWire(),
            FolderShareOperationType.DryRun.ToWire(),
            FolderShareStatus.Completed.ToWire(),
            plan.Folders.Count,
            plan.IncludedTemplates.Count,
            plan.SkippedTemplates.Count,
            0,
            outcomes.Count,
            correlationId,
            outcomes);

        return Response<FolderShareResultModel>.Success(result, correlationId: correlationId);
    }

    public async Task<Response<FolderShareResultModel>> ExecuteAsync(FolderShareInput input, string correlationId, CancellationToken ct)
    {
        var planResponse = await _planner.PlanAsync(
            input.SourceBranchCollectionInstanceId, input.TargetCompanyId, input.IncludeTemplates,
            ControlledDocumentWire.ParseShareMode(input.ShareMode), correlationId, ct);
        if (!planResponse.IsSuccessful)
        {
            return Response<FolderShareResultModel>.Fail(planResponse.Errors, planResponse.StatusCode, planResponse.ReasonCode, correlationId);
        }

        var plan = planResponse.Data!;
        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var now = DateTimeOffset.UtcNow;
        var outcomeRows = new List<FolderShareOutcome>();
        var templatesIncluded = 0;
        var failed = 0;

        foreach (var folder in plan.Folders)
        {
            outcomeRows.Add(Outcome(tenantId, plan.OperationId, FolderShareItemType.Folder, folder.CanonicalId, FolderShareOutcomeStatus.Shared, "FOLDER_SHARED", folder.FullPath, false));
        }

        foreach (var skip in plan.SkippedTemplates)
        {
            outcomeRows.Add(Outcome(tenantId, plan.OperationId, FolderShareItemType.Template, skip.TemplateKey, FolderShareOutcomeStatus.Skipped, skip.ReasonCode, skip.Message, false));
        }

        foreach (var template in plan.IncludedTemplates)
        {
            try
            {
                var (status, _) = await _sharing.ShareTemplateForFolderAsync(template, plan.TargetCompanyId, plan.ShareMode, plan.OperationId, correlationId, ct);
                if (status is FolderShareOutcomeStatus.Shared or FolderShareOutcomeStatus.Copied)
                {
                    templatesIncluded++;
                }

                outcomeRows.Add(Outcome(tenantId, plan.OperationId, FolderShareItemType.Template, template.TemplateKey, status, status.ToWire(), template.Title, false));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                failed++;
                outcomeRows.Add(Outcome(tenantId, plan.OperationId, FolderShareItemType.Template, template.TemplateKey, FolderShareOutcomeStatus.Failed, ControlledDocumentReasonCodes.Conflict, "Share failed.", true));
            }
        }

        var skippedCount = plan.SkippedTemplates.Count;
        var operationStatus = failed > 0 && (templatesIncluded > 0 || skippedCount > 0)
            ? FolderShareStatus.Partial
            : failed > 0 ? FolderShareStatus.Failed : FolderShareStatus.Completed;

        var operation = new FolderShareOperation
        {
            TenantId = tenantId,
            OperationId = plan.OperationId,
            SourceCompanyId = plan.SourceCompanyId,
            TargetCompanyId = plan.TargetCompanyId,
            SourceBranchCollectionInstanceId = plan.SourceBranchCollectionInstanceId,
            IncludeTemplates = plan.IncludeTemplates,
            ShareMode = plan.ShareMode,
            OperationType = FolderShareOperationType.Execute,
            Status = operationStatus,
            FoldersIncluded = plan.Folders.Count,
            TemplatesIncluded = templatesIncluded,
            TemplatesSkipped = skippedCount,
            Failed = failed,
            Total = outcomeRows.Count,
            CorrelationId = correlationId,
            RequestedBy = _currentUser.ActorName,
            StartedAt = now,
            CompletedAt = DateTimeOffset.UtcNow,
            CreatedBy = _currentUser.ActorName
        };

        await _operations.CreateAsync(operation, ct);
        await _outcomes.CreateManyAsync(outcomeRows, ct);

        return Response<FolderShareResultModel>.Success(ToResult(operation, outcomeRows, correlationId), 201, correlationId);
    }

    public async Task<Response<FolderShareResultModel>> GetOperationAsync(Guid operationId, string correlationId, CancellationToken ct)
    {
        var operation = await _operations.GetByOperationIdAsync(operationId, ct);
        if (operation is null)
        {
            return Response<FolderShareResultModel>.Fail("Not found.", 404, ControlledDocumentReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var outcomes = await _outcomes.GetByOperationIdAsync(operationId, ct);
        return Response<FolderShareResultModel>.Success(ToResult(operation, outcomes, correlationId), correlationId: correlationId);
    }

    private static FolderShareOutcome Outcome(Guid tenantId, Guid operationId, FolderShareItemType type, string key, FolderShareOutcomeStatus status, string reason, string message, bool retryable) => new()
    {
        TenantId = tenantId,
        OperationId = operationId,
        ItemType = type,
        ItemKey = key,
        Status = status,
        ReasonCode = reason,
        Message = message,
        Retryable = retryable
    };

    private static FolderShareResultModel ToResult(FolderShareOperation op, IReadOnlyList<FolderShareOutcome> outcomes, string correlationId) => new(
        op.OperationId,
        op.SourceCompanyId,
        op.TargetCompanyId,
        op.SourceBranchCollectionInstanceId,
        op.IncludeTemplates,
        op.ShareMode.ToWire(),
        op.OperationType.ToWire(),
        op.Status.ToWire(),
        op.FoldersIncluded,
        op.TemplatesIncluded,
        op.TemplatesSkipped,
        op.Failed,
        op.Total,
        correlationId,
        outcomes.Select(o => new FolderShareOutcomeModel(o.ItemType.ToWire(), o.ItemKey, o.Status.ToWire(), o.ReasonCode, o.Message, o.Retryable)).ToList());
}

public sealed record FolderShareInput(
    Guid SourceBranchCollectionInstanceId,
    Guid TargetCompanyId,
    bool IncludeTemplates,
    string? ShareMode);
