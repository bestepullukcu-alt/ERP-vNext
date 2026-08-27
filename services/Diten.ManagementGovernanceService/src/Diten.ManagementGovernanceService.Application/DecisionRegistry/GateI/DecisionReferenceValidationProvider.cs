using Diten.ManagementGovernanceService.Domain.DecisionRegistry;

namespace Diten.ManagementGovernanceService.Application.DecisionRegistry.GateI;

public static class DecisionReferenceValidationProfile
{
    public const string Operation = "decision-registry.decision-references.validate.v1";
    public const string Permission = "management-governance.decision-references.validate";
    public const string Audience = "diten-management-governance-service";
    public const string ClientId = "diten.management-governance";
    public const string Scope = "diten.s2s.delegated.invoke";
    public const string ContextName = "ppm.investment-case-context";
    public const string GoverningWrapper = "ppm.investment-case-governing-decision-reference";
    public const string SupportingWrapper = "ppm.investment-case-supporting-decision-reference";
    public const string Version = "1.0";
}

public sealed record DecisionReferenceValidationRequest(
    string WrapperName,
    string WrapperVersion,
    string ContextName,
    string ContextVersion,
    Guid InvestmentCaseId,
    DecisionRevisionReferenceV1 Reference,
    DecisionReferenceValidationMode Mode);

public sealed record GateITrustedContext(
    bool Authenticated,
    Guid TenantId,
    Guid EffectiveActorId,
    string ClientId,
    string Audience,
    string Scope,
    string Operation,
    string Permission,
    string RequestHash);

public sealed record DecisionReferenceSnapshot(
    bool Exists,
    Guid TenantId,
    Guid DecisionId,
    Guid RevisionId,
    int RevisionNumber,
    string SubjectKind,
    Guid SubjectId,
    DecisionDisposition Disposition,
    bool Visible,
    bool TechnicallyDeleted);

public sealed record DecisionReferenceValidationResponse(
    int StatusCode,
    string Code,
    DecisionRevisionReferenceV1? Reference = null,
    bool Resolved = false,
    bool Eligible = false,
    DecisionDisposition? Disposition = null);

public interface IDecisionReferenceSnapshotReader
{
    Task<DecisionReferenceSnapshot?> ReadAsync(Guid tenantId, Guid decisionId, Guid revisionId, CancellationToken cancellationToken);
}

public interface IGateITrustedContextAccessor
{
    GateITrustedContext Current { get; }
}

public sealed record DecisionReferenceProviderOptions(bool Enabled)
{
    public static DecisionReferenceProviderOptions Disabled { get; } = new(false);
}

public sealed class DecisionReferenceValidationProvider(
    IDecisionReferenceSnapshotReader reader,
    DecisionReferenceProviderOptions options)
{
    public async Task<DecisionReferenceValidationResponse> ValidateAsync(
        DecisionReferenceValidationRequest request,
        GateITrustedContext context,
        CancellationToken cancellationToken)
    {
        if (!options.Enabled)
            return new(503, "decision_reference_provider_disabled");
        if (!ValidRequest(request))
            return new(400, "decision_reference_request_malformed");
        if (!context.Authenticated || context.TenantId == Guid.Empty || context.EffectiveActorId == Guid.Empty ||
            string.IsNullOrWhiteSpace(context.RequestHash))
            return new(401, "decision_reference_context_invalid");
        if (!ExactAuthority(context))
            return new(403, "decision_reference_forbidden");

        DecisionReferenceSnapshot? snapshot;
        try
        {
            snapshot = await reader.ReadAsync(
                context.TenantId,
                request.Reference.DecisionId,
                request.Reference.DecisionRevisionId,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new(503, "decision_reference_authority_unavailable");
        }

        if (snapshot is null || !snapshot.Exists || snapshot.TenantId != context.TenantId ||
            !snapshot.Visible || snapshot.TechnicallyDeleted ||
            snapshot.DecisionId != request.Reference.DecisionId ||
            snapshot.RevisionId != request.Reference.DecisionRevisionId ||
            snapshot.RevisionNumber != request.Reference.DecisionRevisionNumber ||
            !string.Equals(snapshot.SubjectKind, "ppm.investment-case", StringComparison.Ordinal) ||
            snapshot.SubjectId != request.InvestmentCaseId)
            return new(404, "decision_reference_not_found");

        if (request.Mode == DecisionReferenceValidationMode.NewReferenceEligibility &&
            snapshot.Disposition is DecisionDisposition.Withdrawn or DecisionDisposition.Superseded)
            return new(409, "decision_reference_ineligible", request.Reference, true, false, snapshot.Disposition);

        return new(200, "decision_reference_accepted", request.Reference, true, true, snapshot.Disposition);
    }

    private static bool ValidRequest(DecisionReferenceValidationRequest request) =>
        request.Reference is not null &&
        request.InvestmentCaseId != Guid.Empty &&
        request.Mode != DecisionReferenceValidationMode.CurrentSelectionEligibility &&
        string.Equals(request.WrapperVersion, DecisionReferenceValidationProfile.Version, StringComparison.Ordinal) &&
        string.Equals(request.ContextName, DecisionReferenceValidationProfile.ContextName, StringComparison.Ordinal) &&
        string.Equals(request.ContextVersion, DecisionReferenceValidationProfile.Version, StringComparison.Ordinal) &&
        (string.Equals(request.WrapperName, DecisionReferenceValidationProfile.GoverningWrapper, StringComparison.Ordinal) ||
         string.Equals(request.WrapperName, DecisionReferenceValidationProfile.SupportingWrapper, StringComparison.Ordinal));

    private static bool ExactAuthority(GateITrustedContext context) =>
        string.Equals(context.ClientId, DecisionReferenceValidationProfile.ClientId, StringComparison.Ordinal) &&
        string.Equals(context.Audience, DecisionReferenceValidationProfile.Audience, StringComparison.Ordinal) &&
        string.Equals(context.Scope, DecisionReferenceValidationProfile.Scope, StringComparison.Ordinal) &&
        string.Equals(context.Operation, DecisionReferenceValidationProfile.Operation, StringComparison.Ordinal) &&
        string.Equals(context.Permission, DecisionReferenceValidationProfile.Permission, StringComparison.Ordinal);
}

public sealed class UnavailableDecisionReferenceSnapshotReader : IDecisionReferenceSnapshotReader
{
    public Task<DecisionReferenceSnapshot?> ReadAsync(Guid tenantId, Guid decisionId, Guid revisionId, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Authoritative DecisionRegistry persistence reader is not composed.");
}

public sealed class UnavailableGateITrustedContextAccessor : IGateITrustedContextAccessor
{
    public GateITrustedContext Current { get; } = new(false, Guid.Empty, Guid.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
}
