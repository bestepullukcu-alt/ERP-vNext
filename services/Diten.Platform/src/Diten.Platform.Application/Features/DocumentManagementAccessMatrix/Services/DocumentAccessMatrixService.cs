using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Services;

/// <summary>
/// MOD-0029-FU04 — access matrix CRUD + effective-access preview orchestration. Controllers stay thin; all
/// validation, target/principal resolution, deny-precedence resolution and tenant isolation live here.
/// </summary>
public sealed class DocumentAccessMatrixService
{
    private readonly IDocumentAccessPolicyRepository _policies;
    private readonly DocumentAccessTargetResolver _targetResolver;
    private readonly DocumentAccessResolver _resolver;
    private readonly ITemplateMasterRepository _masters;
    private readonly ITemplateVariantRepository _variants;
    private readonly ITemplateDocumentRepository _templateDocuments;
    private readonly IControlledDocumentRepository _controlledDocuments;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public DocumentAccessMatrixService(
        IDocumentAccessPolicyRepository policies,
        DocumentAccessTargetResolver targetResolver,
        DocumentAccessResolver resolver,
        ITemplateMasterRepository masters,
        ITemplateVariantRepository variants,
        ITemplateDocumentRepository templateDocuments,
        IControlledDocumentRepository controlledDocuments,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _policies = policies;
        _targetResolver = targetResolver;
        _resolver = resolver;
        _masters = masters;
        _variants = variants;
        _templateDocuments = templateDocuments;
        _controlledDocuments = controlledDocuments;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<DocumentAccessPolicyDetailModel>> CreateAsync(DocumentAccessPolicyInput input, string correlationId, CancellationToken ct)
    {
        var parsed = ParseAndValidate(input, correlationId, out var failure);
        if (failure is not null) return failure;

        var resolution = await _targetResolver.ResolveAsync(parsed.TargetType, parsed.TargetId, ct);
        if (!resolution.Exists)
        {
            return NotFound<DocumentAccessPolicyDetailModel>(correlationId);
        }

        if (await _policies.FindDuplicateAsync(parsed.TargetType, parsed.TargetId, parsed.PrincipalType, parsed.PrincipalId, parsed.Effect, ct) is not null)
        {
            return Fail<DocumentAccessPolicyDetailModel>("An access policy with the same target, principal and effect already exists.", 409, AccessMatrixReasonCodes.DuplicatePolicy, correlationId);
        }

        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var id = Guid.NewGuid();
        var entry = new DocumentAccessPolicyEntry
        {
            Id = id,
            TenantId = tenantId,
            AccessPolicyId = id,
            TargetType = parsed.TargetType,
            TargetId = parsed.TargetId,
            PrincipalType = parsed.PrincipalType,
            PrincipalId = parsed.PrincipalId,
            Actions = parsed.Actions,
            Effect = parsed.Effect,
            InheritFromParent = input.InheritFromParent,
            SourcePolicyId = input.SourcePolicyId,
            ValidFrom = input.ValidFrom,
            ValidTo = input.ValidTo,
            Status = parsed.Status,
            Reason = TrimOrNull(input.Reason),
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };

        try
        {
            await _policies.CreateAsync(entry, ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return Fail<DocumentAccessPolicyDetailModel>("An access policy with the same target, principal and effect already exists.", 409, AccessMatrixReasonCodes.DuplicatePolicy, correlationId);
        }

        return Response<DocumentAccessPolicyDetailModel>.Success(AccessMatrixWire.ToDetail(entry, resolution.Label, DateTimeOffset.UtcNow, resolution.CompanyId), 201, correlationId);
    }

    public async Task<Response<DocumentAccessPolicyDetailModel>> UpdateAsync(Guid id, DocumentAccessPolicyInput input, string correlationId, CancellationToken ct)
    {
        var entry = await _policies.GetByIdAsync(id, ct);
        if (entry is null)
        {
            return NotFound<DocumentAccessPolicyDetailModel>(correlationId);
        }

        var parsed = ParseAndValidate(input, correlationId, out var failure);
        if (failure is not null) return failure;

        var resolution = await _targetResolver.ResolveAsync(parsed.TargetType, parsed.TargetId, ct);
        if (!resolution.Exists)
        {
            return NotFound<DocumentAccessPolicyDetailModel>(correlationId);
        }

        var duplicate = await _policies.FindDuplicateAsync(parsed.TargetType, parsed.TargetId, parsed.PrincipalType, parsed.PrincipalId, parsed.Effect, ct);
        if (duplicate is not null && duplicate.Id != entry.Id)
        {
            return Fail<DocumentAccessPolicyDetailModel>("An access policy with the same target, principal and effect already exists.", 409, AccessMatrixReasonCodes.DuplicatePolicy, correlationId);
        }

        entry.TargetType = parsed.TargetType;
        entry.TargetId = parsed.TargetId;
        entry.PrincipalType = parsed.PrincipalType;
        entry.PrincipalId = parsed.PrincipalId;
        entry.Actions = parsed.Actions;
        entry.Effect = parsed.Effect;
        entry.InheritFromParent = input.InheritFromParent;
        entry.SourcePolicyId = input.SourcePolicyId;
        entry.ValidFrom = input.ValidFrom;
        entry.ValidTo = input.ValidTo;
        entry.Status = parsed.Status;
        entry.Reason = TrimOrNull(input.Reason);
        entry.CorrelationId = correlationId;
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        entry.UpdatedBy = _currentUser.ActorName;
        await _policies.UpdateAsync(entry, ct);

        return Response<DocumentAccessPolicyDetailModel>.Success(AccessMatrixWire.ToDetail(entry, resolution.Label, DateTimeOffset.UtcNow, resolution.CompanyId), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<DocumentAccessPolicyListItemModel>>> ListAsync(DocumentAccessPolicyListFilter filter, string correlationId, CancellationToken ct)
    {
        var rows = await _policies.ListAsync(filter.TargetType, filter.TargetId, filter.PrincipalType, filter.PrincipalId, filter.Effect, filter.Action, filter.Status, ct);
        var now = DateTimeOffset.UtcNow;
        var labelCache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var items = new List<DocumentAccessPolicyListItemModel>(rows.Count);
        foreach (var row in rows)
        {
            var key = $"{row.TargetType}:{row.TargetId}";
            if (!labelCache.TryGetValue(key, out var label))
            {
                label = (await _targetResolver.ResolveAsync(row.TargetType, row.TargetId, ct)).Label;
                labelCache[key] = label;
            }
            items.Add(AccessMatrixWire.ToListItem(row, label, now));
        }

        return Response<IReadOnlyList<DocumentAccessPolicyListItemModel>>.Success(items, correlationId: correlationId);
    }

    public async Task<Response<DocumentAccessPolicyDetailModel>> GetDetailAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var entry = await _policies.GetByIdAsync(id, ct);
        if (entry is null)
        {
            return NotFound<DocumentAccessPolicyDetailModel>(correlationId);
        }

        var resolution = await _targetResolver.ResolveAsync(entry.TargetType, entry.TargetId, ct);
        return Response<DocumentAccessPolicyDetailModel>.Success(AccessMatrixWire.ToDetail(entry, resolution.Label, DateTimeOffset.UtcNow, resolution.CompanyId), correlationId: correlationId);
    }

    public async Task<Response<NoContent>> DeleteAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var entry = await _policies.GetByIdAsync(id, ct);
        if (entry is null)
        {
            return NotFound<NoContent>(correlationId);
        }

        await _policies.SoftDeleteAsync(id, ct);
        return Response<NoContent>.Success(correlationId: correlationId);
    }

    public async Task<Response<int>> BulkDeleteAsync(IReadOnlyList<Guid> ids, string correlationId, CancellationToken ct)
    {
        var distinct = (ids ?? []).Where(x => x != Guid.Empty).Distinct().ToList();
        if (distinct.Count == 0)
        {
            return Fail<int>("No identifiers provided for bulk deletion.", 400, AccessMatrixReasonCodes.ValidationFailed, correlationId);
        }

        var deleted = await _policies.BulkSoftDeleteAsync(distinct, ct);
        return Response<int>.Success(deleted, 200, correlationId);
    }

    public async Task<Response<EffectiveDocumentAccessModel>> GetEffectiveAsync(
        string targetType, string targetId, string principalType, string principalId, string correlationId, CancellationToken ct)
    {
        var tt = AccessMatrixWire.ParseTargetType(targetType);
        var pt = AccessMatrixWire.ParsePrincipalType(principalType);
        if (tt is null) return Fail<EffectiveDocumentAccessModel>("TargetType is not recognized.", 400, AccessMatrixReasonCodes.InvalidTarget, correlationId);
        if (pt is null) return Fail<EffectiveDocumentAccessModel>("PrincipalType is not recognized.", 400, AccessMatrixReasonCodes.InvalidPrincipal, correlationId);
        if (string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(principalId))
            return Fail<EffectiveDocumentAccessModel>("TargetId and PrincipalId are required.", 400, AccessMatrixReasonCodes.ValidationFailed, correlationId);

        var resolution = await _targetResolver.ResolveAsync(tt.Value, targetId, ct);
        if (!resolution.Exists)
        {
            return NotFound<EffectiveDocumentAccessModel>(correlationId);
        }

        var model = await _resolver.ResolveAsync(tt.Value, targetId.Trim(), pt.Value, principalId.Trim(), ct);
        return Response<EffectiveDocumentAccessModel>.Success(model, correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<EffectiveDocumentAccessModel>>> GetEffectiveBatchAsync(EffectiveDocumentAccessBatchInput input, string correlationId, CancellationToken ct)
    {
        var pt = AccessMatrixWire.ParsePrincipalType(input.PrincipalType);
        if (pt is null) return Fail<IReadOnlyList<EffectiveDocumentAccessModel>>("PrincipalType is not recognized.", 400, AccessMatrixReasonCodes.InvalidPrincipal, correlationId);
        if (string.IsNullOrWhiteSpace(input.PrincipalId)) return Fail<IReadOnlyList<EffectiveDocumentAccessModel>>("PrincipalId is required.", 400, AccessMatrixReasonCodes.ValidationFailed, correlationId);

        var results = new List<EffectiveDocumentAccessModel>();
        foreach (var target in input.Targets ?? [])
        {
            var tt = AccessMatrixWire.ParseTargetType(target.TargetType);
            if (tt is null || string.IsNullOrWhiteSpace(target.TargetId)) continue;
            var resolution = await _targetResolver.ResolveAsync(tt.Value, target.TargetId, ct);
            if (!resolution.Exists) continue;
            results.Add(await _resolver.ResolveAsync(tt.Value, target.TargetId.Trim(), pt.Value, input.PrincipalId.Trim(), ct));
        }

        return Response<IReadOnlyList<EffectiveDocumentAccessModel>>.Success(results, correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<DocumentAccessPolicyTargetModel>>> GetTargetOptionsAsync(string correlationId, CancellationToken ct)
    {
        var options = new List<DocumentAccessPolicyTargetModel>();
        var tenantId = _tenantContext.TenantId;
        if (tenantId != Guid.Empty)
        {
            options.Add(new DocumentAccessPolicyTargetModel(DocumentAccessTargetType.Tenant.ToWire(), tenantId.ToString("D"), "Tenant"));
        }

        foreach (var m in await _masters.ListAsync(null, null, null, null, null, ct))
        {
            options.Add(new DocumentAccessPolicyTargetModel(DocumentAccessTargetType.TemplateMaster.ToWire(), m.Id.ToString("D"), $"{m.MasterCode} — {m.TemplateName}", m.OwnerCompanyId?.ToString("D")));
        }

        foreach (var v in await _variants.ListAsync(null, null, null, null, null, ct))
        {
            options.Add(new DocumentAccessPolicyTargetModel(DocumentAccessTargetType.TemplateVariant.ToWire(), v.Id.ToString("D"), $"{v.VariantCode} — {v.VariantName}", v.OwnerCompanyId?.ToString("D")));
        }

        // Tenant-wide document targets carry their owning company as Scope so the UI can filter them by a selected
        // company (folder/structure/company targets are picked via the company-scoped collection-instances lookup).
        foreach (var td in await _templateDocuments.GetAllForTenantAsync(ct))
        {
            var label = string.IsNullOrWhiteSpace(td.CollectionPath) ? td.Title : $"{td.Title} — {td.CollectionPath}";
            options.Add(new DocumentAccessPolicyTargetModel(DocumentAccessTargetType.TemplateDocument.ToWire(), td.Id.ToString("D"), label, td.CompanyId.ToString("D")));
        }

        foreach (var cd in await _controlledDocuments.GetAllForTenantAsync(ct))
        {
            var label = string.IsNullOrWhiteSpace(cd.CollectionPath) ? cd.Title : $"{cd.Title} — {cd.CollectionPath}";
            options.Add(new DocumentAccessPolicyTargetModel(DocumentAccessTargetType.ControlledDocument.ToWire(), cd.Id.ToString("D"), label, cd.CompanyId.ToString("D")));
        }

        return Response<IReadOnlyList<DocumentAccessPolicyTargetModel>>.Success(options, correlationId: correlationId);
    }

    public Task<Response<IReadOnlyList<DocumentAccessPrincipalModel>>> GetPrincipalOptionsAsync(string correlationId, CancellationToken ct)
    {
        // Company / CollectionInstance targets and User / Role / Company principals are picked via the existing
        // TenantShell proxy lookups (legal-entities, users) and manual role id entry; no new reference seam is
        // introduced in this FU. The list is intentionally empty server-side.
        return Task.FromResult(Response<IReadOnlyList<DocumentAccessPrincipalModel>>.Success([], correlationId: correlationId));
    }

    private sealed record ParsedInput(
        DocumentAccessTargetType TargetType,
        string TargetId,
        DocumentAccessPrincipalType PrincipalType,
        string PrincipalId,
        List<DocumentAccessMatrixAction> Actions,
        DocumentAccessEffect Effect,
        DocumentAccessPolicyStatus Status);

    private static ParsedInput ParseAndValidate(DocumentAccessPolicyInput input, string correlationId, out Response<DocumentAccessPolicyDetailModel>? failure)
    {
        failure = null;
        var empty = new ParsedInput(default, string.Empty, default, string.Empty, [], default, default);

        if (input is null)
        {
            failure = Fail<DocumentAccessPolicyDetailModel>("Request body is required.", 400, AccessMatrixReasonCodes.ValidationFailed, correlationId);
            return empty;
        }

        var tt = AccessMatrixWire.ParseTargetType(input.TargetType);
        if (tt is null || string.IsNullOrWhiteSpace(input.TargetId))
        {
            failure = Fail<DocumentAccessPolicyDetailModel>("TargetType and TargetId are required.", 400, AccessMatrixReasonCodes.InvalidTarget, correlationId);
            return empty;
        }

        var pt = AccessMatrixWire.ParsePrincipalType(input.PrincipalType);
        if (pt is null || string.IsNullOrWhiteSpace(input.PrincipalId))
        {
            failure = Fail<DocumentAccessPolicyDetailModel>("PrincipalType and PrincipalId are required.", 400, AccessMatrixReasonCodes.InvalidPrincipal, correlationId);
            return empty;
        }

        if (pt == DocumentAccessPrincipalType.Group)
        {
            failure = Fail<DocumentAccessPolicyDetailModel>("Group principals are not available until a group source exists.", 400, AccessMatrixReasonCodes.GroupPrincipalUnavailable, correlationId);
            return empty;
        }

        var effect = AccessMatrixWire.ParseEffect(input.Effect);
        if (effect is null)
        {
            failure = Fail<DocumentAccessPolicyDetailModel>("Effect must be Allow or Deny.", 400, AccessMatrixReasonCodes.ValidationFailed, correlationId);
            return empty;
        }

        var actions = new List<DocumentAccessMatrixAction>();
        foreach (var raw in input.Actions ?? [])
        {
            var action = AccessMatrixWire.ParseAction(raw);
            if (action is null)
            {
                failure = Fail<DocumentAccessPolicyDetailModel>($"Unknown action '{raw}'.", 400, AccessMatrixReasonCodes.InvalidAction, correlationId);
                return empty;
            }
            if (!actions.Contains(action.Value)) actions.Add(action.Value);
        }

        if (actions.Count == 0)
        {
            failure = Fail<DocumentAccessPolicyDetailModel>("At least one action is required.", 400, AccessMatrixReasonCodes.InvalidAction, correlationId);
            return empty;
        }

        if (input.ValidFrom is { } from && input.ValidTo is { } to && to < from)
        {
            failure = Fail<DocumentAccessPolicyDetailModel>("ValidTo must be greater than or equal to ValidFrom.", 400, AccessMatrixReasonCodes.ValidationFailed, correlationId);
            return empty;
        }

        if (!string.IsNullOrEmpty(input.Reason) && input.Reason.Length > 1000)
        {
            failure = Fail<DocumentAccessPolicyDetailModel>("Reason exceeds 1000 characters.", 400, AccessMatrixReasonCodes.ValidationFailed, correlationId);
            return empty;
        }

        var status = AccessMatrixWire.ParseStatus(input.Status) ?? DocumentAccessPolicyStatus.Active;
        return new ParsedInput(tt.Value, input.TargetId.Trim(), pt.Value, input.PrincipalId.Trim(), actions, effect.Value, status);
    }

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Response<T> NotFound<T>(string correlationId) =>
        Response<T>.Fail("Not found.", 404, AccessMatrixReasonCodes.NotFoundNonLeakage, correlationId);

    private static Response<T> Fail<T>(string error, int status, string reason, string correlationId) =>
        Response<T>.Fail(error, status, reason, correlationId);
}
