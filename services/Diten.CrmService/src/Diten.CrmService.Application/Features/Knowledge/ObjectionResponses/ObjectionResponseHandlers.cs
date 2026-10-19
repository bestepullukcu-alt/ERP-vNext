using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Approvals;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.ObjectionResponses;

/// <summary>
/// WP-VW-W3 (W3-BE-b, §2.3) — the ONE check of an objection record's content.
/// <para>WP-VW-W3 (W3-BE-c, K-A3) — two levels:</para>
/// <list type="number">
/// <item><b>draft</b> (create / update): the product is required; question ≤ 500, answer ≤ 4000 and the category MAY be
/// empty (a half-written draft is saved); a given category is an active <c>visit-objection-category</c> value; at most 10
/// distinct claims, each this tenant's APPROVED live claim; the product an MDM Global Product (when wired);</item>
/// <item><b>submit</b>: also the question, the answer and the category are filled (400 <c>objection_response_incomplete</c>)
/// and every claim is still approved (400 <c>objection_claim_not_approved</c>) AND of the answer's product (400
/// <c>objection_claim_product_mismatch</c>).</item>
/// </list>
/// The category set unreadable ⇒ 503 <c>reference_data_unavailable</c> (fail-closed).
/// </summary>
public sealed class ObjectionResponseRules
{
    public const string ClaimProductMismatch = "objection_claim_product_mismatch";

    private readonly IReferenceDataValidator _categories;
    private readonly IClaimRepository _claims;
    private readonly IStrategyTemplateProductReferenceValidator? _products;

    public ObjectionResponseRules(
        IReferenceDataValidator categories, IClaimRepository claims, IStrategyTemplateProductReferenceValidator? products = null)
    {
        _categories = categories;
        _claims = claims;
        _products = products;
    }

    public static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>The draft check (create / update). Null errors = valid.</summary>
    public async Task<(string[]? Errors, int Status)> CheckAsync(
        Guid tenantId, Guid productId, string? categoryCode, string? question, string? answer, IReadOnlyList<Guid>? claimIds,
        CancellationToken cancellationToken)
    {
        var q = Trim(question);
        var a = Trim(answer);
        var claims = (claimIds ?? Array.Empty<Guid>()).Distinct().ToList();
        if (q?.Length > ObjectionResponseLimits.MaxQuestionLength || a?.Length > ObjectionResponseLimits.MaxAnswerLength
            || claims.Count > ObjectionResponseLimits.MaxClaims || claims.Contains(Guid.Empty))
        {
            return (new[]
            {
                ObjectionResponseErrors.Invalid,
                $"A question ≤ {ObjectionResponseLimits.MaxQuestionLength}, an answer ≤ {ObjectionResponseLimits.MaxAnswerLength} and at most {ObjectionResponseLimits.MaxClaims} claims."
            }, 400);
        }

        if (productId == Guid.Empty)
        {
            return (new[] { ObjectionResponseErrors.ProductInvalid, "A product (MDM Global Product id) is required." }, 400);
        }

        if (Trim(categoryCode) is { } category && await CategoryAsync(category, cancellationToken) is { } categoryFailure)
        {
            return categoryFailure;
        }

        if (_products is not null)
        {
            switch (await _products.ValidateAsync(
                        IStrategyTemplateProductReferenceValidator.ReferenceKind.GlobalProduct, productId, cancellationToken))
            {
                case IStrategyTemplateProductReferenceValidator.Outcome.NotFound:
                    return (new[] { ObjectionResponseErrors.ProductInvalid, "The product is not an MDM Global Product." }, 400);
                case IStrategyTemplateProductReferenceValidator.Outcome.Unavailable:
                    return (new[] { ObjectionResponseErrors.ReferenceDataUnavailable, "The product master could not be read. Nothing was saved." }, 503);
            }
        }

        if (await UnapprovedClaimAsync(tenantId, claims, cancellationToken) is { } claimId)
        {
            return (new[]
            {
                ObjectionResponseErrors.ClaimNotApproved, $"Claim {claimId} is not an approved claim of this tenant; only approved claims can back an answer."
            }, 400);
        }

        return (null, 200);
    }

    /// <summary>The submit check: the draft complete, its category still active, its claims approved and of its product.</summary>
    public async Task<(string[]? Errors, int Status)> CheckCompleteAsync(ObjectionResponse record, CancellationToken cancellationToken)
    {
        if (Trim(record.Question) is null || Trim(record.Answer) is null || Trim(record.CategoryCode) is null)
        {
            return (new[] { ObjectionResponseErrors.Incomplete, "The question, the answer and the category are required before approval." }, 400);
        }

        if (await CategoryAsync(record.CategoryCode, cancellationToken) is { } categoryFailure)
        {
            return categoryFailure;
        }

        foreach (var id in record.ClaimIds)
        {
            var claim = await _claims.GetByIdAsync(record.TenantId, id, cancellationToken);
            if (claim is null || claim.TenantId != record.TenantId || !claim.IsApproved() || claim.IsArchived())
            {
                return (new[] { ObjectionResponseErrors.ClaimNotApproved, $"Claim {id} is no longer an approved claim." }, 400);
            }

            if (claim.ProductId != record.ProductId)
            {
                return (new[] { ClaimProductMismatch, $"Claim {claim.ClaimCode} is not a claim of this answer's product." }, 400);
            }
        }

        return (null, 200);
    }

    private async Task<(string[] Errors, int Status)?> CategoryAsync(string category, CancellationToken cancellationToken)
    {
        var verdict = await _categories.ValidateAsync(VisitObjectionCategories.SetCode, category.Trim(), cancellationToken);
        return verdict.Status switch
        {
            ReferenceValidationStatus.SetMissing => (new[]
            {
                ObjectionResponseErrors.ReferenceDataUnavailable,
                $"The category list ('{VisitObjectionCategories.SetCode}') could not be read. Nothing was saved."
            }, 503),
            ReferenceValidationStatus.InvalidValue => (new[]
            {
                ObjectionResponseErrors.CategoryInvalid, $"'{category}' is not an active '{VisitObjectionCategories.SetCode}' value."
            }, 400),
            _ => null
        };
    }

    /// <summary>The first claim among <paramref name="claimIds"/> that is not this tenant's approved, live claim.</summary>
    public async Task<Guid?> UnapprovedClaimAsync(Guid tenantId, IEnumerable<Guid> claimIds, CancellationToken cancellationToken)
    {
        foreach (var id in claimIds)
        {
            var claim = await _claims.GetByIdAsync(tenantId, id, cancellationToken);
            if (claim is null || claim.TenantId != tenantId || !claim.IsApproved() || claim.IsArchived())
            {
                return id;
            }
        }

        return null;
    }

    public static Response<T> Fail<T>(string code, string message, int status) => Response<T>.Fail(new[] { code, message }, status);

    public static Response<T> NotFound<T>() => Fail<T>(ObjectionResponseErrors.NotFound, "Objection response not found.", 404);

    public static Response<T> Locked<T>() => Fail<T>(ObjectionResponseErrors.InReviewLocked,
        "The answer is under review; withdraw it to change it.", 409);
}

/// <summary>
/// WP-VW-W3 (W3-BE-c) — the library's read side shared by the handlers: load + reconcile-on-read, the usages (path steps
/// whose presentation notes link the answer), the product names (MDM, fail-open), the abilities, the DTO.
/// </summary>
public sealed class ObjectionResponseReader
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IObjectionResponseRepository _responses;
    private readonly IKnowledgePathRepository? _paths;
    private readonly IProductNameReader? _productNames;
    private readonly WorkflowApprovalFlow? _flow;
    private readonly WorkflowApprovalReconciler? _reconciler;
    private readonly IUserDisplayNameResolver? _names;

    public ObjectionResponseReader(ITenantContext tenant, IActorContext actor, IObjectionResponseRepository responses,
        IKnowledgePathRepository? paths = null, IProductNameReader? productNames = null, WorkflowApprovalFlow? flow = null,
        WorkflowApprovalReconciler? reconciler = null, IUserDisplayNameResolver? names = null)
    {
        _tenant = tenant;
        _actor = actor;
        _responses = responses;
        _paths = paths;
        _productNames = productNames;
        _flow = flow;
        _reconciler = reconciler;
        _names = names;
    }

    public Guid? TenantId => _tenant.TenantId;
    public string? Actor => _actor.ActorName;
    public IObjectionResponseRepository Repository => _responses;

    public async Task<IReadOnlyList<ObjectionResponse>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        var rows = await _responses.ListAsync(tenantId, ct);
        if (_reconciler is not null && rows.Any(r => r.OpenRound() is not null)
            && await _reconciler.ReconcileAsync(tenantId, WorkflowApprovalKind.ObjectionResponse, rows, ct) > 0)
        {
            rows = await _responses.ListAsync(tenantId, ct);
        }

        return rows;
    }

    public async Task<(ObjectionResponse? Record, Response<T>? Error)> LoadAsync<T>(Guid id, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return (null, Response<T>.Fail("Tenant context is required.", 400));
        }

        var record = await _responses.GetByIdAsync(tenantId, id, ct);
        if (record is not null && record.OpenRound() is not null && _reconciler is not null
            && await _reconciler.ReconcileAsync(tenantId, WorkflowApprovalKind.ObjectionResponse, new[] { record }, ct) > 0)
        {
            record = await _responses.GetByIdAsync(tenantId, id, ct);
        }

        return record is null ? (null, ObjectionResponseRules.NotFound<T>()) : (record, null);
    }

    /// <summary>Every (non-archived) path step whose presentation notes link an answer, per answer id. One path scan.</summary>
    public async Task<IReadOnlyDictionary<Guid, List<ObjectionResponseUsageDto>>> UsagesAsync(Guid tenantId, CancellationToken ct)
    {
        var map = new Dictionary<Guid, List<ObjectionResponseUsageDto>>();
        if (_paths is null)
        {
            return map;
        }

        foreach (var path in await _paths.ListAsync(tenantId, ct))
        {
            if (path.IsDeleted || path.IsArchived())
            {
                continue;
            }

            foreach (var step in path.Steps.Where(s => !s.IsArchived() && s.Presentation is not null))
            {
                foreach (var id in step.Presentation!.ObjectionResponseIds.Distinct())
                {
                    if (!map.TryGetValue(id, out var list))
                    {
                        map[id] = list = new List<ObjectionResponseUsageDto>();
                    }

                    list.Add(new ObjectionResponseUsageDto(path.Id, path.PathName, path.PathVersion, path.PathStatus, step.StepId,
                        step.StepOrder, step.StepTitle));
                }
            }
        }

        return map;
    }

    public async Task<Func<ObjectionResponse, ObjectionResponseAbilities>> AbilitiesAsync(
        IEnumerable<ObjectionResponse> rows, CancellationToken ct)
    {
        var mine = _flow is null ? new HashSet<Guid>() : await _flow.MyOpenInstancesAsync(rows, ct);
        var actor = _actor.ActorName;
        return r => new ObjectionResponseAbilities(
            CanEdit: r.IsDraft(),
            CanSubmit: r.IsDraft(),
            CanWithdraw: r.OpenRound() is { } round && Path.Review.KnowledgePathReviewRules.SamePerson(actor, round.SubmittedBy),
            CanDecide: r.OpenRound() is { } open && mine.Contains(open.WorkflowInstanceId) && !WorkflowApprovalRules.IsOwn(r, actor),
            CanRetire: r.IsDraft() || r.IsApproved(),
            CanCopy: !r.IsDeleted);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> ProductNamesAsync(IEnumerable<ObjectionResponse> rows, CancellationToken ct)
    {
        if (_productNames is null)
        {
            return new Dictionary<Guid, string>();
        }

        try
        {
            return await _productNames.ReadNamesAsync(rows.Select(r => r.ProductId).Distinct().ToList(), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new Dictionary<Guid, string>(); // fail-open: a name is display only
        }
    }

    /// <summary>The DTOs of <paramref name="rows"/> with names, usage counts and abilities (one read of each kind).</summary>
    public async Task<IReadOnlyList<ObjectionResponseDto>> DtosAsync(Guid tenantId, IReadOnlyList<ObjectionResponse> rows, CancellationToken ct)
    {
        var usages = await UsagesAsync(tenantId, ct);
        var names = await ProductNamesAsync(rows, ct);
        var abilities = await AbilitiesAsync(rows, ct);
        return rows.Select(r => ObjectionResponseDto.From(r) with
        {
            ProductName = names.TryGetValue(r.ProductId, out var name) ? name : null,
            // The MDM product selector carries no pharmaceutical form; null until the master exposes it (§2.1: null ⇒ 200).
            ProductForm = null,
            UsageCount = usages.TryGetValue(r.Id, out var list) ? list.Count : 0,
            Abilities = abilities(r)
        }).ToList();
    }

    public async Task<Response<ObjectionResponseDto>> DetailAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var record = await _responses.GetByIdAsync(tenantId, id, ct);
        if (record is null)
        {
            return ObjectionResponseRules.NotFound<ObjectionResponseDto>();
        }

        if (_flow is not null)
        {
            await _flow.FillReturnCommentsAsync(record, ct);
        }

        var dto = (await DtosAsync(tenantId, new[] { record }, ct))[0];
        var ids = new[] { record.CreatedBy, record.UpdatedBy, record.ApprovedBy, record.RetiredBy }
            .Concat(record.ReviewRounds.Select(r => r.SubmittedBy)).Concat(record.Decisions.Select(d => d.By))
            .Select(v => Guid.TryParse(v, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).Distinct().ToList();
        if (_names is not null && ids.Count > 0)
        {
            try
            {
                var resolved = await _names.ResolveAsync(ids, ct);
                dto = dto with { DisplayNames = resolved.ToDictionary(kv => kv.Key.ToString("D"), kv => kv.Value) };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // fail-open: the screen shows "unknown user"
            }
        }

        return Response<ObjectionResponseDto>.Success(dto);
    }
}

public sealed class CreateObjectionResponseHandler : IRequestHandler<CreateObjectionResponseCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IObjectionResponseRepository _responses;
    private readonly ObjectionResponseRules _rules;

    public CreateObjectionResponseHandler(
        ITenantContext tenant, IActorContext actor, IObjectionResponseRepository responses, ObjectionResponseRules rules)
    {
        _tenant = tenant;
        _actor = actor;
        _responses = responses;
        _rules = rules;
    }

    public async Task<Response<Guid>> Handle(CreateObjectionResponseCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var (errors, status) = await _rules.CheckAsync(
            tenantId, request.ProductId, request.CategoryCode, request.Question, request.Answer, request.ClaimIds, cancellationToken);
        if (errors is not null)
        {
            return Response<Guid>.Fail(errors, status);
        }

        var entity = new ObjectionResponse
        {
            TenantId = tenantId,
            ProductId = request.ProductId,
            CategoryCode = ObjectionResponseRules.Trim(request.CategoryCode) ?? string.Empty,
            Question = ObjectionResponseRules.Trim(request.Question) ?? string.Empty,
            Answer = ObjectionResponseRules.Trim(request.Answer) ?? string.Empty,
            ClaimIds = (request.ClaimIds ?? Array.Empty<Guid>()).Distinct().ToList(),
            Status = ObjectionResponseStatuses.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = _actor.ActorName,
            Version = 1
        };
        await _responses.InsertAsync(entity, cancellationToken);
        return Response<Guid>.Success(entity.Id, 201);
    }
}

public sealed class UpdateObjectionResponseHandler : IRequestHandler<UpdateObjectionResponseCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IObjectionResponseRepository _responses;
    private readonly ObjectionResponseRules _rules;

    public UpdateObjectionResponseHandler(
        ITenantContext tenant, IActorContext actor, IObjectionResponseRepository responses, ObjectionResponseRules rules)
    {
        _tenant = tenant;
        _actor = actor;
        _responses = responses;
        _rules = rules;
    }

    public async Task<Response<bool>> Handle(UpdateObjectionResponseCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var entity = await _responses.GetByIdAsync(tenantId, request.Id, cancellationToken);
        if (entity is null)
        {
            return ObjectionResponseRules.NotFound<bool>();
        }

        if (entity.IsInReview())
        {
            return ObjectionResponseRules.Locked<bool>();
        }

        if (!entity.IsDraft())
        {
            return Response<bool>.Fail(new[]
            {
                ObjectionResponseErrors.NotEditable, $"A {entity.Status} answer never changes; retire it and write a new one."
            }, 409);
        }

        if (request.ExpectedVersion is { } ev && ev != entity.Version)
        {
            return Response<bool>.Fail("The answer was modified by another writer; reload and retry.", 409);
        }

        var (errors, status) = await _rules.CheckAsync(
            tenantId, request.ProductId, request.CategoryCode, request.Question, request.Answer, request.ClaimIds, cancellationToken);
        if (errors is not null)
        {
            return Response<bool>.Fail(errors, status);
        }

        entity.ProductId = request.ProductId;
        entity.CategoryCode = ObjectionResponseRules.Trim(request.CategoryCode) ?? string.Empty;
        entity.Question = ObjectionResponseRules.Trim(request.Question) ?? string.Empty;
        entity.Answer = ObjectionResponseRules.Trim(request.Answer) ?? string.Empty;
        entity.ClaimIds = (request.ClaimIds ?? Array.Empty<Guid>()).Distinct().ToList();
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedBy = _actor.ActorName;
        return await _responses.ReplaceAsync(entity, entity.Version, cancellationToken)
            ? Response<bool>.Success(true)
            : Response<bool>.Fail("The answer was modified by another writer; reload and retry.", 409);
    }
}

/// <summary>
/// WP-VW-W3 (W3-BE-c) — the W3-BE-b direct approve is retired: an answer is approved only through its MOD-0023 round in
/// the Work Center (submit → decide). Always 409 <c>approval_via_workflow_only</c>; nothing changes.
/// </summary>
public sealed class ApproveObjectionResponseHandler : IRequestHandler<ApproveObjectionResponseCommand, Response<bool>>
{
    public Task<Response<bool>> Handle(ApproveObjectionResponseCommand request, CancellationToken cancellationToken)
        => Task.FromResult(ObjectionResponseRules.Fail<bool>(ObjectionResponseErrors.ApprovalViaWorkflowOnly,
            "An objection answer is approved only through its approval workflow (submit, then decide in the Work Center).", 409));
}

public sealed class RetireObjectionResponseHandler : IRequestHandler<RetireObjectionResponseCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IObjectionResponseRepository _responses;

    public RetireObjectionResponseHandler(ITenantContext tenant, IActorContext actor, IObjectionResponseRepository responses)
    {
        _tenant = tenant;
        _actor = actor;
        _responses = responses;
    }

    public async Task<Response<bool>> Handle(RetireObjectionResponseCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var entity = await _responses.GetByIdAsync(tenantId, request.Id, cancellationToken);
        if (entity is null)
        {
            return ObjectionResponseRules.NotFound<bool>();
        }

        if (entity.IsInReview())
        {
            return ObjectionResponseRules.Locked<bool>();
        }

        if (entity.IsRetired())
        {
            return Response<bool>.Fail(new[] { ObjectionResponseErrors.NotEditable, "The answer is already retired." }, 409);
        }

        if (request.ExpectedVersion is { } ev && ev != entity.Version)
        {
            return Response<bool>.Fail("The answer was modified by another writer; reload and retry.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        entity.Status = ObjectionResponseStatuses.Retired;
        entity.RetiredAt = now;
        entity.RetiredBy = _actor.ActorName;
        entity.UpdatedAt = now;
        return await _responses.ReplaceAsync(entity, entity.Version, cancellationToken)
            ? Response<bool>.Success(true)
            : Response<bool>.Fail("The answer was modified by another writer; reload and retry.", 409);
    }
}

/// <summary>WP-VW-W3 (W3-BE-c) — submit / withdraw / decide through MOD-0023 (the shared flow) and copy.</summary>
public sealed class ObjectionResponseApprovalHandlers :
    IRequestHandler<SubmitObjectionResponseCommand, Response<ObjectionResponseDto>>,
    IRequestHandler<WithdrawObjectionResponseCommand, Response<ObjectionResponseDto>>,
    IRequestHandler<DecideObjectionResponseCommand, Response<ObjectionResponseDto>>,
    IRequestHandler<CopyObjectionResponseCommand, Response<Guid>>
{
    private readonly ObjectionResponseReader _reader;
    private readonly ObjectionResponseRules _rules;
    private readonly WorkflowApprovalFlow _flow;
    private readonly WorkflowApprovalOutcomeApplier _applier;
    private readonly IAuthoringApprovalSettings? _settings;
    private readonly IReferenceDataCatalogReader? _catalog;

    public ObjectionResponseApprovalHandlers(ObjectionResponseReader reader, ObjectionResponseRules rules, WorkflowApprovalFlow flow,
        WorkflowApprovalOutcomeApplier applier, IAuthoringApprovalSettings? settings = null, IReferenceDataCatalogReader? catalog = null)
    {
        _reader = reader;
        _rules = rules;
        _flow = flow;
        _applier = applier;
        _settings = settings;
        _catalog = catalog;
    }

    public async Task<Response<ObjectionResponseDto>> Handle(SubmitObjectionResponseCommand request, CancellationToken ct)
    {
        var (record, error) = await _reader.LoadAsync<ObjectionResponseDto>(request.Id, ct);
        if (error is not null)
        {
            return error;
        }

        if (!record!.IsDraft())
        {
            return ObjectionResponseRules.Fail<ObjectionResponseDto>(ObjectionResponseErrors.NotDraft,
                $"Only a draft can be sent for approval (this one is {record.Status}).", 409);
        }

        var (errors, status) = await _rules.CheckCompleteAsync(record, ct);
        if (errors is not null)
        {
            return Response<ObjectionResponseDto>.Fail(errors, status);
        }

        var names = await _reader.ProductNamesAsync(new[] { record }, ct);
        var product = names.TryGetValue(record.ProductId, out var productName) ? productName : record.ProductId.ToString("D")[..8];
        var category = await CategoryLabelAsync(record.CategoryCode, ct);
        var firstLine = record.Question.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? record.Question;
        var display = new ClaimWorkflowDisplayContext(
            ClaimReviewRules.Truncate($"İtiraz yanıtı onayı · {category}", 200),
            ClaimReviewRules.Truncate(firstLine, 300),
            "crm",
            WorkflowApprovalKind.ObjectionResponse.Link(record.Id),
            [ClaimReviewRules.Truncate(product, 32), ClaimReviewRules.Truncate(category, 32)]);
        var templateCode = _settings?.ObjectionResponseTemplateCode ?? AuthoringApprovalDefaults.ObjectionResponseTemplateCode;
        return await _flow.SubmitAsync<ObjectionResponse, ObjectionResponseDto>(_applier.Objections, record,
                   WorkflowApprovalKind.ObjectionResponse, templateCode, display, ObjectionResponseStatuses.InReview, ct)
               ?? await _reader.DetailAsync(record.TenantId, record.Id, ct);
    }

    public async Task<Response<ObjectionResponseDto>> Handle(WithdrawObjectionResponseCommand request, CancellationToken ct)
    {
        var (record, error) = await _reader.LoadAsync<ObjectionResponseDto>(request.Id, ct);
        return error
               ?? await _flow.WithdrawAsync<ObjectionResponse, ObjectionResponseDto>(_applier.Objections, record!,
                   WorkflowApprovalKind.ObjectionResponse, ct)
               ?? await _reader.DetailAsync(record!.TenantId, record.Id, ct);
    }

    public async Task<Response<ObjectionResponseDto>> Handle(DecideObjectionResponseCommand request, CancellationToken ct)
    {
        var (record, error) = await _reader.LoadAsync<ObjectionResponseDto>(request.Id, ct);
        return error
               ?? await _flow.DecideAsync<ObjectionResponse, ObjectionResponseDto>(_applier.Objections, record!,
                   WorkflowApprovalKind.ObjectionResponse, request.Decision, request.Comment, ObjectionResponseErrors.SeparationOfDuties, ct)
               ?? await _reader.DetailAsync(record!.TenantId, record.Id, ct);
    }

    public async Task<Response<Guid>> Handle(CopyObjectionResponseCommand request, CancellationToken ct)
    {
        var (source, error) = await _reader.LoadAsync<Guid>(request.Id, ct);
        if (error is not null)
        {
            return error;
        }

        var copy = new ObjectionResponse
        {
            TenantId = source!.TenantId,
            ProductId = source.ProductId,
            CategoryCode = source.CategoryCode,
            Question = source.Question,
            Answer = source.Answer,
            ClaimIds = source.ClaimIds.ToList(),
            Status = ObjectionResponseStatuses.Draft,
            CopiedFromId = source.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = _reader.Actor,
            Version = 1
        };
        await _reader.Repository.InsertAsync(copy, ct);
        return Response<Guid>.Success(copy.Id, 201);
    }

    /// <summary>The category's Turkish label for the Work Center title (the reference set; the code when unreadable).</summary>
    private async Task<string> CategoryLabelAsync(string code, CancellationToken ct)
    {
        if (_catalog is null)
        {
            return code;
        }

        try
        {
            var set = await _catalog.GetPublishedValuesAsync(VisitObjectionCategories.SetCode, ct);
            var value = set.Values.FirstOrDefault(v => string.Equals(v.ValueCode, code, StringComparison.Ordinal));
            return value?.Attributes is { } a && a.TryGetValue(VisitObjectionCategories.LabelAttributePrefix + "tr", out var tr)
                   && !string.IsNullOrWhiteSpace(tr) ? tr : value?.DisplayName ?? code;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return code;
        }
    }
}

public sealed class ObjectionResponseQueryHandlers :
    IRequestHandler<GetObjectionResponseQuery, Response<ObjectionResponseDto>>,
    IRequestHandler<ListObjectionResponsesQuery, Response<ObjectionResponseListDto>>,
    IRequestHandler<GetObjectionResponseUsagesQuery, Response<IReadOnlyList<ObjectionResponseUsageDto>>>
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    private readonly ObjectionResponseReader _reader;

    public ObjectionResponseQueryHandlers(ObjectionResponseReader reader) => _reader = reader;

    public async Task<Response<ObjectionResponseDto>> Handle(GetObjectionResponseQuery request, CancellationToken cancellationToken)
    {
        var (record, error) = await _reader.LoadAsync<ObjectionResponseDto>(request.Id, cancellationToken);
        return error ?? await _reader.DetailAsync(record!.TenantId, record.Id, cancellationToken);
    }

    public async Task<Response<ObjectionResponseListDto>> Handle(ListObjectionResponsesQuery request, CancellationToken cancellationToken)
    {
        if (_reader.TenantId is not { } tenantId)
        {
            return Response<ObjectionResponseListDto>.Fail("Tenant context is required.", 400);
        }

        var status = ObjectionResponseRules.Trim(request.Status)?.ToLowerInvariant();
        if (status is not null && !ObjectionResponseStatuses.IsKnown(status))
        {
            return Response<ObjectionResponseListDto>.Fail(new[]
            {
                ObjectionResponseErrors.Invalid, $"Unknown status '{status}'. Known: {string.Join(", ", ObjectionResponseStatuses.All)}."
            }, 400);
        }

        var page = Math.Max(1, request.Page ?? 1);
        var size = Math.Clamp(request.PageSize ?? DefaultPageSize, 1, MaxPageSize);
        var category = ObjectionResponseRules.Trim(request.CategoryCode);
        var q = ObjectionResponseRules.Trim(request.Q);
        var rows = (await _reader.ListAsync(tenantId, cancellationToken))
            .Where(r => request.ProductId is not { } p || r.ProductId == p)
            .Where(r => category is null || string.Equals(r.CategoryCode, category, StringComparison.Ordinal))
            .Where(r => status is null || string.Equals(r.Status, status, StringComparison.Ordinal))
            .Where(r => q is null || r.Question.Contains(q, StringComparison.CurrentCultureIgnoreCase))
            .ToList();
        var pageRows = rows.Skip((page - 1) * size).Take(size).ToList();
        var items = await _reader.DtosAsync(tenantId, pageRows, cancellationToken);
        return Response<ObjectionResponseListDto>.Success(new ObjectionResponseListDto(items, rows.Count, page, size));
    }

    public async Task<Response<IReadOnlyList<ObjectionResponseUsageDto>>> Handle(
        GetObjectionResponseUsagesQuery request, CancellationToken cancellationToken)
    {
        var (record, error) = await _reader.LoadAsync<IReadOnlyList<ObjectionResponseUsageDto>>(request.Id, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var usages = await _reader.UsagesAsync(record!.TenantId, cancellationToken);
        return Response<IReadOnlyList<ObjectionResponseUsageDto>>.Success(
            usages.TryGetValue(record.Id, out var list)
                ? list.OrderBy(u => u.PathName, StringComparer.CurrentCulture).ThenBy(u => u.StepOrder).ToList()
                : Array.Empty<ObjectionResponseUsageDto>());
    }
}
