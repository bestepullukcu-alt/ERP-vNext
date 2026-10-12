using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.ObjectionResponses;

/// <summary>
/// WP-VW-W3 (W3-BE-b, §2.3) — the ONE check of an objection record's content, on create and update:
/// <list type="number">
/// <item>question ≤ 500 and answer ≤ 4000, both required; at most 10 distinct claims — else 400
/// <c>objection_response_invalid</c>;</item>
/// <item>the category is an active value of the published <c>visit-objection-category</c> set — else 400
/// <c>objection_category_invalid</c>; the set unreadable ⇒ 503 <c>reference_data_unavailable</c> (fail-closed);</item>
/// <item>the product is an MDM Global Product (when the MDM check is wired) — else 400 / 503;</item>
/// <item>every claim is this tenant's, APPROVED and not archived — else 400 <c>objection_claim_not_approved</c>.</item>
/// </list>
/// </summary>
public sealed class ObjectionResponseRules
{
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

    public async Task<(string[]? Errors, int Status)> CheckAsync(
        Guid tenantId, Guid productId, string? categoryCode, string? question, string? answer, IReadOnlyList<Guid>? claimIds,
        CancellationToken cancellationToken)
    {
        var q = Trim(question);
        var a = Trim(answer);
        var claims = (claimIds ?? Array.Empty<Guid>()).Distinct().ToList();
        if (q is null || a is null || q.Length > ObjectionResponseLimits.MaxQuestionLength
            || a.Length > ObjectionResponseLimits.MaxAnswerLength || claims.Count > ObjectionResponseLimits.MaxClaims
            || claims.Contains(Guid.Empty))
        {
            return (new[]
            {
                ObjectionResponseErrors.Invalid,
                $"A question (≤ {ObjectionResponseLimits.MaxQuestionLength}) and an answer (≤ {ObjectionResponseLimits.MaxAnswerLength}) are required, with at most {ObjectionResponseLimits.MaxClaims} claims."
            }, 400);
        }

        if (productId == Guid.Empty)
        {
            return (new[] { ObjectionResponseErrors.ProductInvalid, "A product (MDM Global Product id) is required." }, 400);
        }

        var category = Trim(categoryCode);
        if (category is null)
        {
            return (new[] { ObjectionResponseErrors.CategoryInvalid, "A category is required." }, 400);
        }

        var verdict = await _categories.ValidateAsync(VisitObjectionCategories.SetCode, category, cancellationToken);
        switch (verdict.Status)
        {
            case ReferenceValidationStatus.SetMissing:
                return (new[]
                {
                    ObjectionResponseErrors.ReferenceDataUnavailable,
                    $"The category list ('{VisitObjectionCategories.SetCode}') could not be read. Nothing was saved."
                }, 503);
            case ReferenceValidationStatus.InvalidValue:
                return (new[]
                {
                    ObjectionResponseErrors.CategoryInvalid, $"'{category}' is not an active '{VisitObjectionCategories.SetCode}' value."
                }, 400);
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

        var now = DateTimeOffset.UtcNow;
        var entity = new ObjectionResponse
        {
            TenantId = tenantId,
            ProductId = request.ProductId,
            CategoryCode = ObjectionResponseRules.Trim(request.CategoryCode)!,
            Question = request.Question!.Trim(),
            Answer = request.Answer!.Trim(),
            ClaimIds = (request.ClaimIds ?? Array.Empty<Guid>()).Distinct().ToList(),
            Status = ObjectionResponseStatuses.Draft,
            CreatedAt = now,
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
            return Response<bool>.Fail(new[] { ObjectionResponseErrors.NotFound, "Objection response not found." }, 404);
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
        entity.CategoryCode = ObjectionResponseRules.Trim(request.CategoryCode)!;
        entity.Question = request.Question!.Trim();
        entity.Answer = request.Answer!.Trim();
        entity.ClaimIds = (request.ClaimIds ?? Array.Empty<Guid>()).Distinct().ToList();
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedBy = _actor.ActorName;
        return await _responses.ReplaceAsync(entity, entity.Version, cancellationToken)
            ? Response<bool>.Success(true)
            : Response<bool>.Fail("The answer was modified by another writer; reload and retry.", 409);
    }
}

/// <summary>
/// draft → approved. SoD: the approver is neither the author nor the last editor (case-insensitive); an unknown actor
/// cannot approve (nobody can be told apart). Every linked claim must STILL be an approved claim (it may have been
/// withdrawn since the draft was written) — else 409 <c>objection_claim_not_approved</c>.
/// </summary>
public sealed class ApproveObjectionResponseHandler : IRequestHandler<ApproveObjectionResponseCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IObjectionResponseRepository _responses;
    private readonly ObjectionResponseRules _rules;

    public ApproveObjectionResponseHandler(
        ITenantContext tenant, IActorContext actor, IObjectionResponseRepository responses, ObjectionResponseRules rules)
    {
        _tenant = tenant;
        _actor = actor;
        _responses = responses;
        _rules = rules;
    }

    public async Task<Response<bool>> Handle(ApproveObjectionResponseCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var entity = await _responses.GetByIdAsync(tenantId, request.Id, cancellationToken);
        if (entity is null)
        {
            return Response<bool>.Fail(new[] { ObjectionResponseErrors.NotFound, "Objection response not found." }, 404);
        }

        if (!entity.IsDraft())
        {
            return Response<bool>.Fail(new[] { ObjectionResponseErrors.NotDraft, $"Only a draft can be approved (this one is {entity.Status})." }, 409);
        }

        if (request.ExpectedVersion is { } ev && ev != entity.Version)
        {
            return Response<bool>.Fail("The answer was modified by another writer; reload and retry.", 409);
        }

        var approver = ObjectionResponseRules.Trim(_actor.ActorName);
        if (approver is null || SamePerson(approver, entity.CreatedBy) || SamePerson(approver, entity.UpdatedBy))
        {
            return Response<bool>.Fail(new[]
            {
                ObjectionResponseErrors.SeparationOfDuties, "The author of an answer cannot approve it (separation of duties)."
            }, 403);
        }

        if (await _rules.UnapprovedClaimAsync(tenantId, entity.ClaimIds, cancellationToken) is { } claimId)
        {
            return Response<bool>.Fail(new[]
            {
                ObjectionResponseErrors.ClaimNotApproved, $"Claim {claimId} is no longer an approved claim; the answer cannot be approved."
            }, 409);
        }

        var now = DateTimeOffset.UtcNow;
        entity.Status = ObjectionResponseStatuses.Approved;
        entity.ApprovedAt = now;
        entity.ApprovedBy = approver;
        entity.UpdatedAt = now;
        return await _responses.ReplaceAsync(entity, entity.Version, cancellationToken)
            ? Response<bool>.Success(true)
            : Response<bool>.Fail("The answer was modified by another writer; reload and retry.", 409);
    }

    private static bool SamePerson(string actor, string? other)
        => !string.IsNullOrWhiteSpace(other) && string.Equals(actor, other.Trim(), StringComparison.OrdinalIgnoreCase);
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
            return Response<bool>.Fail(new[] { ObjectionResponseErrors.NotFound, "Objection response not found." }, 404);
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

public sealed class GetObjectionResponseHandler : IRequestHandler<GetObjectionResponseQuery, Response<ObjectionResponseDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IObjectionResponseRepository _responses;

    public GetObjectionResponseHandler(ITenantContext tenant, IObjectionResponseRepository responses)
    {
        _tenant = tenant;
        _responses = responses;
    }

    public async Task<Response<ObjectionResponseDto>> Handle(GetObjectionResponseQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ObjectionResponseDto>.Fail("Tenant context is required.", 400);
        }

        var entity = await _responses.GetByIdAsync(tenantId, request.Id, cancellationToken);
        return entity is null
            ? Response<ObjectionResponseDto>.Fail(new[] { ObjectionResponseErrors.NotFound, "Objection response not found." }, 404)
            : Response<ObjectionResponseDto>.Success(ObjectionResponseDto.From(entity));
    }
}

public sealed class ListObjectionResponsesHandler
    : IRequestHandler<ListObjectionResponsesQuery, Response<IReadOnlyList<ObjectionResponseDto>>>
{
    private readonly ITenantContext _tenant;
    private readonly IObjectionResponseRepository _responses;

    public ListObjectionResponsesHandler(ITenantContext tenant, IObjectionResponseRepository responses)
    {
        _tenant = tenant;
        _responses = responses;
    }

    public async Task<Response<IReadOnlyList<ObjectionResponseDto>>> Handle(
        ListObjectionResponsesQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<IReadOnlyList<ObjectionResponseDto>>.Fail("Tenant context is required.", 400);
        }

        var status = ObjectionResponseRules.Trim(request.Status)?.ToLowerInvariant();
        if (status is not null && !ObjectionResponseStatuses.IsKnown(status))
        {
            return Response<IReadOnlyList<ObjectionResponseDto>>.Fail(new[]
            {
                ObjectionResponseErrors.Invalid, $"Unknown status '{status}'. Known: {string.Join(", ", ObjectionResponseStatuses.All)}."
            }, 400);
        }

        var category = ObjectionResponseRules.Trim(request.CategoryCode);
        var rows = (await _responses.ListAsync(tenantId, cancellationToken))
            .Where(r => request.ProductId is not { } p || r.ProductId == p)
            .Where(r => category is null || string.Equals(r.CategoryCode, category, StringComparison.Ordinal))
            .Where(r => status is null || string.Equals(r.Status, status, StringComparison.Ordinal))
            .Select(ObjectionResponseDto.From)
            .ToList();
        return Response<IReadOnlyList<ObjectionResponseDto>>.Success(rows);
    }
}
