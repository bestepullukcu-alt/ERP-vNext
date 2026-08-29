using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementGDocPCorrection.Services;

/// <summary>
/// MOD-0029-FU21 — GDocP correction policy authoring (GMG-QMS-SOP-0001 §21). Policies declare what a correction to
/// a given subject type / field must carry and whether it is permitted at all after approval or effectiveness.
///
/// NO DEFAULT SEED: FU21 deliberately ships no policies. The evaluator's built-in safe default already protects an
/// unpoliced field (reason mandatory, high-risk types demand a deviation, regulated timestamp corrections demand
/// review), so an empty policy set is safe rather than permissive. Seeding tenant policies is a governance
/// decision, not a migration.
///
/// A retired policy stops applying to new corrections but is never deleted, so historic risk classifications stay
/// explainable.
/// </summary>
public sealed class DocumentGDocPCorrectionPolicyService
{
    private readonly IDocumentGDocPCorrectionPolicyRepository _policies;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public DocumentGDocPCorrectionPolicyService(
        IDocumentGDocPCorrectionPolicyRepository policies,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _policies = policies;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<GDocPCorrectionPolicyModel>> CreateAsync(
        GDocPCorrectionPolicyInput input, string correlationId, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);

        if (string.IsNullOrWhiteSpace(input.PolicyKey))
        {
            return Fail("A policy key is required.", 400, GDocPCorrectionReasonCodes.PolicyKeyRequired, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.PolicyName))
        {
            return Fail("A policy name is required.", 400, GDocPCorrectionReasonCodes.PolicyNameRequired, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.FieldPathPattern))
        {
            return Fail("A field path pattern is required (use '*' to match every field).", 400,
                GDocPCorrectionReasonCodes.FieldPathPatternRequired, correlationId);
        }

        var key = input.PolicyKey.Trim().ToUpperInvariant();
        if (await _policies.GetByKeyAsync(key, ct) is not null)
        {
            return Fail($"A correction policy with key '{key}' already exists.", 409,
                GDocPCorrectionReasonCodes.PolicyKeyDuplicate, correlationId);
        }

        var policy = new DocumentGDocPCorrectionPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PolicyKey = key,
            PolicyName = input.PolicyName.Trim(),
            FieldPathPattern = input.FieldPathPattern.Trim(),
            PolicyStatus = GDocPCorrectionPolicyStatus.Draft,
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };
        Apply(policy, input);
        await _policies.CreateAsync(policy, ct);
        return Response<GDocPCorrectionPolicyModel>.Success(GDocPCorrectionWire.ToPolicy(policy), 201, correlationId);
    }

    public async Task<Response<GDocPCorrectionPolicyModel>> ActivateAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var (fail, policy) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (policy!.PolicyStatus == GDocPCorrectionPolicyStatus.Retired)
        {
            return Fail("A retired policy cannot be reactivated; create a new policy.", 409,
                GDocPCorrectionReasonCodes.PolicyAlreadyRetired, correlationId);
        }

        policy.PolicyStatus = GDocPCorrectionPolicyStatus.Active;
        Touch(policy);
        await _policies.UpdateAsync(policy, ct);
        return Response<GDocPCorrectionPolicyModel>.Success(GDocPCorrectionWire.ToPolicy(policy), correlationId: correlationId);
    }

    /// <summary>Retiring is a status change — the policy row survives so past classifications stay explainable.</summary>
    public async Task<Response<GDocPCorrectionPolicyModel>> RetireAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var (fail, policy) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        policy!.PolicyStatus = GDocPCorrectionPolicyStatus.Retired;
        Touch(policy);
        await _policies.UpdateAsync(policy, ct);
        return Response<GDocPCorrectionPolicyModel>.Success(GDocPCorrectionWire.ToPolicy(policy), correlationId: correlationId);
    }

    public async Task<Response<GDocPCorrectionPolicyModel>> GetAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var (fail, policy) = await LoadAsync(id, correlationId, ct);
        return fail ?? Response<GDocPCorrectionPolicyModel>.Success(
            GDocPCorrectionWire.ToPolicy(policy!), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<GDocPCorrectionPolicyModel>>> ListAsync(string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var rows = await _policies.GetAllForTenantAsync(ct);
        return Response<IReadOnlyList<GDocPCorrectionPolicyModel>>.Success(
            rows.Select(GDocPCorrectionWire.ToPolicy).ToList(), correlationId: correlationId);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static void Apply(DocumentGDocPCorrectionPolicy p, GDocPCorrectionPolicyInput i)
    {
        p.SubjectType = GDocPCorrectionWire.ParseSubjectType(i.SubjectType) ?? GDocPSubjectType.Other;
        p.RequiresCorrectionReason = i.RequiresCorrectionReason;
        p.RequiresEvidenceReference = i.RequiresEvidenceReference;
        p.RequiresReview = i.RequiresReview;
        p.RequiresDeviationReferenceForHighRisk = i.RequiresDeviationReferenceForHighRisk;
        p.AllowCorrectionAfterApproval = i.AllowCorrectionAfterApproval;
        p.AllowCorrectionAfterEffective = i.AllowCorrectionAfterEffective;
        p.IsBackdatingSensitive = i.IsBackdatingSensitive;
        p.IsStatusSensitive = i.IsStatusSensitive;
        p.IsEvidenceSensitive = i.IsEvidenceSensitive;
        p.Notes = string.IsNullOrWhiteSpace(i.Notes) ? null : i.Notes.Trim();
    }

    private async Task<(Response<GDocPCorrectionPolicyModel>? Fail, DocumentGDocPCorrectionPolicy? Policy)> LoadAsync(
        Guid id, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var policy = await _policies.GetByIdAsync(id, ct);
        return policy is null
            ? (Fail("Correction policy not found.", 404, GDocPCorrectionReasonCodes.PolicyNotFound, correlationId), null)
            : (null, policy);
    }

    private void Touch(DocumentGDocPCorrectionPolicy p)
    {
        p.UpdatedAt = DateTimeOffset.UtcNow;
        p.UpdatedBy = _currentUser.ActorName;
    }

    private static Response<GDocPCorrectionPolicyModel> Fail(string error, int status, string reason, string correlationId) =>
        Response<GDocPCorrectionPolicyModel>.Fail(error, status, reason, correlationId);
}
