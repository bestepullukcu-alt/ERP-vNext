using System.Security.Cryptography;
using System.Text;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.GateI.BenefitRealization;

namespace Diten.PpmService.Application.Features.BenefitCommitments.GateI.BenefitRealization;


public sealed class BenefitCommitmentOutcomeReferenceValidator(IOutcomeReferenceAuthorityPort authority)
{
    public const string Operation = "outcome-tracking.outcome-references.validate";
    public const string Permission = "decision-intelligence.outcome-references.validate";
    public const string Audience = "diten-decision-intelligence-service";
    public const string ClientId = "diten.decision-intelligence";
    public const string OwnerModule = "MOD-0072";
    public const string Scope = "diten.s2s.delegated.invoke";

    public async Task<OutcomeReferenceValidationResult> ValidateAsync(
        ReadOnlyMemory<byte> wrapperUtf8,
        OutcomeReferenceValidationMode mode,
        GateIS2SServerContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!context.IsAuthenticated || context.TenantId == Guid.Empty || context.EffectiveActorId == Guid.Empty ||
            (context.DelegatedActorId is not null && (!context.DelegationVerified || context.DelegatedActorId == Guid.Empty)) ||
            !string.Equals(context.Audience, Audience, StringComparison.Ordinal) ||
            !string.Equals(context.ClientId, ClientId, StringComparison.Ordinal) ||
            !string.Equals(context.Scope, Scope, StringComparison.Ordinal) ||
            !string.Equals(context.Method, S2SOutboundReceiverProfiles.OutcomeTracking.Method, StringComparison.Ordinal) ||
            !string.Equals(context.Path, S2SOutboundReceiverProfiles.OutcomeTracking.Path, StringComparison.Ordinal) ||
            !GateICanonicalRequestBinding.IsWellFormed(context.Method, context.Path, context.RequestHash))
        {
            return OutcomeReferenceValidationResult.From(401, "gate_i_outcome_context_invalid");
        }

        if (!context.EntitlementGranted ||
            !string.Equals(context.OwnerModule, OwnerModule, StringComparison.Ordinal) ||
            !string.Equals(context.Operation, Operation, StringComparison.Ordinal) ||
            !string.Equals(context.Permission, Permission, StringComparison.Ordinal))
        {
            return OutcomeReferenceValidationResult.From(403, "gate_i_outcome_forbidden");
        }

        if (mode is not OutcomeReferenceValidationMode.HistoricalResolve and
            not OutcomeReferenceValidationMode.NewReferenceEligibility and
            not OutcomeReferenceValidationMode.CurrentSelectionEligibility)
            return OutcomeReferenceValidationResult.From(400, "gate_i_outcome_mode_unsupported");

        if (mode == OutcomeReferenceValidationMode.CurrentSelectionEligibility)
            return OutcomeReferenceValidationResult.From(400, "gate_i_outcome_mode_unsupported");

        BenefitCommitmentOutcomeReferenceV1 wrapper;
        try
        {
            wrapper = BenefitCommitmentOutcomeReferenceV1Codec.ParseStrict(wrapperUtf8.Span);
        }
        catch (OutcomeReferenceContractException exception)
        {
            return exception.Error == OutcomeReferenceContractError.UnsupportedVersion
                ? OutcomeReferenceValidationResult.From(503, "gate_i_outcome_version_unsupported")
                : OutcomeReferenceValidationResult.From(400, "gate_i_outcome_request_malformed");
        }

        var actualHash = GateICanonicalRequestBinding.Compute(
            context.Method, context.Path, context.TenantId, context.Operation, wrapperUtf8.Span);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(actualHash), Encoding.ASCII.GetBytes(context.RequestHash)))
        {
            return OutcomeReferenceValidationResult.From(401, "gate_i_outcome_request_binding_invalid");
        }

        var authorityResult = await authority.ValidateAsync(
            new OutcomeReferenceAuthorityRequest(
                context.TenantId,
                context.EffectiveActorId,
                context.DelegatedActorId,
                context.RequestHash,
                mode,
                wrapper.OutcomeReference),
            cancellationToken).ConfigureAwait(false);

        if (authorityResult.Disposition == OutcomeReferenceAuthorityDisposition.Accepted)
        {
            if (authorityResult.Reference is null || authorityResult.Reference != wrapper.OutcomeReference)
                return OutcomeReferenceValidationResult.From(503, "gate_i_outcome_authority_malformed");
            try { authorityResult.Reference.ValidateIdentity(); }
            catch (OutcomeReferenceContractException)
            { return OutcomeReferenceValidationResult.From(503, "gate_i_outcome_authority_malformed"); }
            return new OutcomeReferenceValidationResult(200, "gate_i_outcome_accepted", wrapper);
        }

        return authorityResult.Disposition switch
        {
            OutcomeReferenceAuthorityDisposition.PermissionDenied => OutcomeReferenceValidationResult.From(403, "gate_i_outcome_forbidden"),
            OutcomeReferenceAuthorityDisposition.MissingOrNonDisclosable => OutcomeReferenceValidationResult.From(404, "gate_i_outcome_not_found"),
            OutcomeReferenceAuthorityDisposition.IneligibleOrConflicting => OutcomeReferenceValidationResult.From(409, "gate_i_outcome_conflict"),
            OutcomeReferenceAuthorityDisposition.Unavailable or OutcomeReferenceAuthorityDisposition.Timeout or
                OutcomeReferenceAuthorityDisposition.Malformed or OutcomeReferenceAuthorityDisposition.Indeterminate =>
                OutcomeReferenceValidationResult.From(503, "gate_i_outcome_authority_unavailable"),
            _ => OutcomeReferenceValidationResult.From(503, "gate_i_outcome_authority_unavailable")
        };
    }

}
