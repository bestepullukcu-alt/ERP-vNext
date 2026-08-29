using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementElectronicSignature.Services;

/// <summary>
/// MOD-0029-FU23 — signature policy lifecycle: draft → active → retired (GMG-QMS-SOP-0001 §11.2).
///
/// THE SELECTION RULE MATTERS MORE THAN THE CRUD: when several active policies match a subject type, the MOST
/// RESTRICTIVE one wins. Picking the newest or the first would let a permissive policy added later silently
/// weaken an existing control, which is the wrong failure direction for regulated evidence.
///
/// WHEN NO POLICY MATCHES, the caller applies <see cref="SafeDefault"/> — everything required, nothing claimed —
/// rather than treating "unconfigured" as "unconstrained".
/// </summary>
public sealed class DocumentSignaturePolicyService
{
    private readonly IDocumentSignaturePolicyRepository _policies;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public DocumentSignaturePolicyService(
        IDocumentSignaturePolicyRepository policies,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _policies = policies;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    /// <summary>
    /// The fail-closed policy applied when a tenant has configured nothing: a meaning statement, an object
    /// fingerprint and a manifestation are all mandatory, and no compliance claim is ever produced.
    /// </summary>
    public static DocumentSignaturePolicy SafeDefault(SignableSubjectType subjectType, SignatureMeaning meaning) => new()
    {
        Id = Guid.Empty,
        TenantId = Guid.Empty,
        PolicyKey = "__fu23_safe_default__",
        PolicyName = "FU23 safe default (no tenant policy configured)",
        PolicyStatus = SignaturePolicyStatus.Active,
        SignableSubjectType = subjectType,
        SignatureMeaning = meaning,
        RequiresReAuthentication = false,
        RequiresSecondFactor = false,
        RequiresMeaningStatement = true,
        RequiresRepositoryAssessment = false,
        RequiresObjectFingerprint = true,
        RequiresManifestation = true,
        AllowInterimRepositorySignature = true
    };

    public async Task<Response<SignaturePolicyModel>> CreateAsync(
        CreateSignaturePolicyInput input, string correlationId, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);

        if (string.IsNullOrWhiteSpace(input.PolicyKey))
        {
            return Fail("A signature policy key is required.", 400,
                ElectronicSignatureReasonCodes.PolicyKeyRequired, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.PolicyName))
        {
            return Fail("A signature policy name is required.", 400,
                ElectronicSignatureReasonCodes.PolicyNameRequired, correlationId);
        }

        var key = input.PolicyKey.Trim();
        if (await _policies.GetByKeyAsync(key, ct) is not null)
        {
            return Fail($"A signature policy with the key '{key}' already exists in this tenant.", 409,
                ElectronicSignatureReasonCodes.PolicyKeyDuplicate, correlationId);
        }

        var allowedTypes = (input.AllowedRepositoryTypes ?? [])
            .Select(ElectronicSignatureWire.ParseRepositoryType)
            .Where(t => t is not null)
            .Select(t => t!.Value)
            .Distinct()
            .ToList();

        var policy = new DocumentSignaturePolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PolicyKey = key,
            PolicyName = input.PolicyName.Trim(),
            PolicyStatus = SignaturePolicyStatus.Draft,
            SignableSubjectType = ElectronicSignatureWire.ParseSubjectType(input.SignableSubjectType),
            SignatureMeaning = ElectronicSignatureWire.ParseMeaning(input.SignatureMeaning) ?? SignatureMeaning.Other,
            RequiresReAuthentication = input.RequiresReAuthentication,
            RequiresSecondFactor = input.RequiresSecondFactor,
            RequiresMeaningStatement = input.RequiresMeaningStatement,
            RequiresRepositoryAssessment = input.RequiresRepositoryAssessment,
            RequiresObjectFingerprint = input.RequiresObjectFingerprint,
            RequiresManifestation = input.RequiresManifestation,
            AllowedRepositoryTypes = allowedTypes,
            AllowInterimRepositorySignature = input.AllowInterimRepositorySignature,
            InterimRepositoryBoundaryStatement = Trim(input.InterimRepositoryBoundaryStatement),
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };

        await _policies.CreateAsync(policy, ct);
        return Response<SignaturePolicyModel>.Success(ElectronicSignatureWire.ToPolicy(policy), 201, correlationId);
    }

    public async Task<Response<SignaturePolicyModel>> ActivateAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var (fail, policy) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (policy!.PolicyStatus == SignaturePolicyStatus.Retired)
        {
            return Fail("A retired signature policy cannot be reactivated.", 409,
                ElectronicSignatureReasonCodes.PolicyInvalidState, correlationId);
        }

        policy.PolicyStatus = SignaturePolicyStatus.Active;
        Touch(policy);
        await _policies.UpdateAsync(policy, ct);
        return Response<SignaturePolicyModel>.Success(
            ElectronicSignatureWire.ToPolicy(policy), correlationId: correlationId);
    }

    public async Task<Response<SignaturePolicyModel>> RetireAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var (fail, policy) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (policy!.PolicyStatus == SignaturePolicyStatus.Retired)
        {
            return Fail("The signature policy is already retired.", 409,
                ElectronicSignatureReasonCodes.PolicyInvalidState, correlationId);
        }

        policy.PolicyStatus = SignaturePolicyStatus.Retired;
        Touch(policy);
        await _policies.UpdateAsync(policy, ct);
        return Response<SignaturePolicyModel>.Success(
            ElectronicSignatureWire.ToPolicy(policy), correlationId: correlationId);
    }

    public async Task<Response<SignaturePolicyModel>> GetAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var (fail, policy) = await LoadAsync(id, correlationId, ct);
        return fail ?? Response<SignaturePolicyModel>.Success(
            ElectronicSignatureWire.ToPolicy(policy!), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<SignaturePolicyModel>>> ListAsync(
        string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var rows = await _policies.GetAllForTenantAsync(ct);
        return Response<IReadOnlyList<SignaturePolicyModel>>.Success(
            rows.Select(ElectronicSignatureWire.ToPolicy).ToList(), correlationId: correlationId);
    }

    /// <summary>
    /// Picks the applicable policy: active, matching the subject type, and matching the meaning exactly or via the
    /// <see cref="SignatureMeaning.Other"/> catch-all. Most restrictive wins; a meaning-specific policy outranks a
    /// catch-all at equal restrictiveness.
    /// </summary>
    public async Task<DocumentSignaturePolicy?> ResolveApplicableAsync(
        SignableSubjectType subjectType, SignatureMeaning meaning, CancellationToken ct)
    {
        var candidates = (await _policies.GetActiveBySubjectTypeAsync(subjectType, ct))
            .Where(p => p.SignatureMeaning == meaning || p.SignatureMeaning == SignatureMeaning.Other)
            .ToList();

        return candidates
            .OrderByDescending(p => p.RestrictivenessScore())
            .ThenByDescending(p => p.SignatureMeaning == meaning)
            .FirstOrDefault();
    }

    private async Task<(Response<SignaturePolicyModel>? Fail, DocumentSignaturePolicy? Policy)> LoadAsync(
        Guid id, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var policy = await _policies.GetByIdAsync(id, ct);
        return policy is null
            ? (Fail("Signature policy not found.", 404,
                ElectronicSignatureReasonCodes.PolicyNotFound, correlationId), null)
            : (null, policy);
    }

    private void Touch(DocumentSignaturePolicy p)
    {
        p.UpdatedAt = DateTimeOffset.UtcNow;
        p.UpdatedBy = _currentUser.ActorName;
    }

    private static Response<SignaturePolicyModel> Fail(string error, int status, string reason, string correlationId) =>
        Response<SignaturePolicyModel>.Fail(error, status, reason, correlationId);

    private static string? Trim(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
