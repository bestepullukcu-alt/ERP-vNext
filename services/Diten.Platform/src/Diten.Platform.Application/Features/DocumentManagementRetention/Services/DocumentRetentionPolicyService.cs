using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementRetention.Services;

/// <summary>
/// MOD-0029-FU15 — retention policy authoring (GMG-QMS-SOP-0001 §22). Policies are drafted, activated and
/// eventually retired; a retired policy stops applying to new evaluations but is never deleted, so historic
/// verdicts stay explainable.
///
/// The Retention Schedule itself is expected to be governed as a controlled document in the FU06 register; these
/// rows are its machine-readable projection, which is why every policy carries its regulatory basis.
/// </summary>
public sealed class DocumentRetentionPolicyService
{
    private readonly IDocumentRetentionPolicyRepository _policies;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public DocumentRetentionPolicyService(
        IDocumentRetentionPolicyRepository policies,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _policies = policies;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<RetentionPolicyModel>> CreateAsync(RetentionPolicyFieldsInput input, string correlationId, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        if (Validate(input) is { } failure)
        {
            return Fail(failure.Message, 400, failure.ReasonCode, correlationId);
        }

        var key = input.PolicyKey.Trim().ToUpperInvariant();
        if (await _policies.GetByKeyAsync(key, ct) is not null)
        {
            return Fail($"A retention policy with key '{key}' already exists.", 409, RetentionReasonCodes.PolicyKeyDuplicate, correlationId);
        }

        var policy = new DocumentRetentionPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PolicyKey = key,
            PolicyName = input.PolicyName.Trim(),
            PolicyStatus = RetentionPolicyStatus.Draft,
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };
        Apply(policy, input);
        await _policies.CreateAsync(policy, ct);
        return Response<RetentionPolicyModel>.Success(RetentionWire.ToPolicy(policy), 201, correlationId);
    }

    public async Task<Response<RetentionPolicyModel>> UpdateAsync(Guid id, RetentionPolicyFieldsInput input, string correlationId, CancellationToken ct)
    {
        var (fail, policy) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (policy!.PolicyStatus == RetentionPolicyStatus.Retired)
        {
            return Fail("A retired policy cannot be edited; create a new policy.", 409, RetentionReasonCodes.PolicyAlreadyRetired, correlationId);
        }

        if (Validate(input) is { } failure)
        {
            return Fail(failure.Message, 400, failure.ReasonCode, correlationId);
        }

        policy.PolicyName = input.PolicyName.Trim();
        Apply(policy, input);
        Touch(policy);
        await _policies.UpdateAsync(policy, ct);
        return Response<RetentionPolicyModel>.Success(RetentionWire.ToPolicy(policy), correlationId: correlationId);
    }

    public async Task<Response<RetentionPolicyModel>> ActivateAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var (fail, policy) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (policy!.PolicyStatus == RetentionPolicyStatus.Retired)
        {
            return Fail("A retired policy cannot be reactivated.", 409, RetentionReasonCodes.PolicyAlreadyRetired, correlationId);
        }

        policy.PolicyStatus = RetentionPolicyStatus.Active;
        Touch(policy);
        await _policies.UpdateAsync(policy, ct);
        return Response<RetentionPolicyModel>.Success(RetentionWire.ToPolicy(policy), correlationId: correlationId);
    }

    /// <summary>Retiring is a status change — the policy row is retained so past verdicts stay explainable.</summary>
    public async Task<Response<RetentionPolicyModel>> RetireAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var (fail, policy) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        policy!.PolicyStatus = RetentionPolicyStatus.Retired;
        Touch(policy);
        await _policies.UpdateAsync(policy, ct);
        return Response<RetentionPolicyModel>.Success(RetentionWire.ToPolicy(policy), correlationId: correlationId);
    }

    public async Task<Response<RetentionPolicyModel>> GetAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var (fail, policy) = await LoadAsync(id, correlationId, ct);
        return fail ?? Response<RetentionPolicyModel>.Success(RetentionWire.ToPolicy(policy!), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<RetentionPolicyModel>>> ListAsync(string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var rows = await _policies.GetAllForTenantAsync(ct);
        return Response<IReadOnlyList<RetentionPolicyModel>>.Success(
            rows.Select(RetentionWire.ToPolicy).ToList(), correlationId: correlationId);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private sealed record ValidationFailure(string Message, string ReasonCode);

    private static ValidationFailure? Validate(RetentionPolicyFieldsInput i)
    {
        if (string.IsNullOrWhiteSpace(i.PolicyKey))
        {
            return new ValidationFailure("A policy key is required.", RetentionReasonCodes.PolicyKeyRequired);
        }

        if (string.IsNullOrWhiteSpace(i.PolicyName))
        {
            return new ValidationFailure("A policy name is required.", RetentionReasonCodes.PolicyNameRequired);
        }

        if (i.MinimumRetentionYears < 0 || i.RetainAfterRetirementYears < 0 || i.RetainAfterSupersessionYears < 0)
        {
            return new ValidationFailure("Retention years cannot be negative.", RetentionReasonCodes.RetentionYearsInvalid);
        }

        return null;
    }

    private static void Apply(DocumentRetentionPolicy p, RetentionPolicyFieldsInput i)
    {
        p.SubjectType = RetentionWire.ParseSubjectType(i.SubjectType) ?? RetentionSubjectType.Other;
        p.RetentionClass = Trim(i.RetentionClass);
        p.MinimumRetentionYears = i.MinimumRetentionYears;
        p.RetentionTrigger = RetentionWire.ParseTrigger(i.RetentionTrigger);
        p.RetainWhileEffective = i.RetainWhileEffective;
        p.RetainAfterRetirementYears = i.RetainAfterRetirementYears;
        p.RetainAfterSupersessionYears = i.RetainAfterSupersessionYears;
        p.IsPermanentRetention = i.IsPermanentRetention;
        p.RegulatoryBasis = Trim(i.RegulatoryBasis);
        p.Jurisdiction = Trim(i.Jurisdiction);
        p.IsLongestApplicableCandidate = i.IsLongestApplicableCandidate;
    }

    private async Task<(Response<RetentionPolicyModel>? Fail, DocumentRetentionPolicy? Policy)> LoadAsync(
        Guid id, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var policy = await _policies.GetByIdAsync(id, ct);
        return policy is null
            ? (Fail("Retention policy not found.", 404, RetentionReasonCodes.PolicyNotFound, correlationId), null)
            : (null, policy);
    }

    private void Touch(DocumentRetentionPolicy p)
    {
        p.UpdatedAt = DateTimeOffset.UtcNow;
        p.UpdatedBy = _currentUser.ActorName;
    }

    private static Response<RetentionPolicyModel> Fail(string error, int status, string reason, string correlationId) =>
        Response<RetentionPolicyModel>.Fail(error, status, reason, correlationId);

    private static string? Trim(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
