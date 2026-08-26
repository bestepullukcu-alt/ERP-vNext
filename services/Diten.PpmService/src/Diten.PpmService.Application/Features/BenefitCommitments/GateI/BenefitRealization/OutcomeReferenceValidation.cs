using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Diten.PpmService.Domain.GateI.BenefitRealization;

namespace Diten.PpmService.Application.Features.BenefitCommitments.GateI.BenefitRealization;

public enum OutcomeReferenceValidationMode
{
    HistoricalResolve,
    NewReferenceEligibility,
    CurrentSelectionEligibility
}

public enum OutcomeReferenceAuthorityDisposition
{
    Accepted,
    MissingOrNonDisclosable,
    IneligibleOrConflicting,
    PermissionDenied,
    Unavailable,
    Timeout,
    Malformed,
    Indeterminate
}

public sealed record GateIFu16ServerContext(
    bool IsAuthenticated,
    Guid TenantId,
    Guid EffectiveActorId,
    Guid? DelegatedActorId,
    bool DelegationVerified,
    string Audience,
    string ClientId,
    string OwnerModule,
    string Scope,
    string Method,
    string Path,
    string Operation,
    string Permission,
    string RequestHash,
    bool EntitlementGranted);

public sealed record OutcomeReferenceAuthorityRequest(
    Guid TenantId,
    Guid EffectiveActorId,
    Guid? DelegatedActorId,
    string RequestHash,
    OutcomeReferenceValidationMode Mode,
    OutcomeReferenceV1 Reference);

public sealed record OutcomeReferenceAuthorityResult(
    OutcomeReferenceAuthorityDisposition Disposition,
    OutcomeReferenceV1? Reference = null);

public interface IOutcomeReferenceAuthorityPort
{
    Task<OutcomeReferenceAuthorityResult> ValidateAsync(
        OutcomeReferenceAuthorityRequest request,
        CancellationToken cancellationToken);
}

public sealed record OutcomeReferenceValidationResult(int StatusCode, string Code, BenefitCommitmentOutcomeReferenceV1? Reference = null)
{
    public static OutcomeReferenceValidationResult From(int status, string code) => new(status, code);
}

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
        GateIFu16ServerContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!context.IsAuthenticated || context.TenantId == Guid.Empty || context.EffectiveActorId == Guid.Empty ||
            (context.DelegatedActorId is not null && (!context.DelegationVerified || context.DelegatedActorId == Guid.Empty)) ||
            !string.Equals(context.Audience, Audience, StringComparison.Ordinal) ||
            !string.Equals(context.ClientId, ClientId, StringComparison.Ordinal) ||
            !string.Equals(context.Scope, Scope, StringComparison.Ordinal) ||
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

public static class GateICanonicalRequestBinding
{
    private static readonly byte[] DomainSeparator = Encoding.UTF8.GetBytes("DITEN-S2S-REQUEST-V1");

    public static bool IsWellFormed(string method, string path, string hash) =>
        !string.IsNullOrEmpty(method) && string.Equals(method, method.ToUpperInvariant(), StringComparison.Ordinal) &&
        !string.IsNullOrEmpty(path) && path.StartsWith("/", StringComparison.Ordinal) && !path.Contains('?', StringComparison.Ordinal) &&
        !string.IsNullOrEmpty(hash) && hash.Length == 43 &&
        hash.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    public static string Compute(string method, string path, Guid tenantId, string operation, ReadOnlySpan<byte> body)
    {
        if (!IsWellFormed(method, path, new string('A', 43)) || tenantId == Guid.Empty ||
            string.IsNullOrEmpty(operation) || !string.Equals(operation, operation.ToLowerInvariant(), StringComparison.Ordinal))
            throw new ArgumentException("Invalid canonical request binding input.");

        using var stream = new MemoryStream();
        stream.Write(DomainSeparator);
        WriteField(stream, Encoding.UTF8.GetBytes(method));
        WriteField(stream, Encoding.UTF8.GetBytes(path));
        WriteField(stream, Encoding.UTF8.GetBytes(tenantId.ToString("D")));
        WriteField(stream, Encoding.UTF8.GetBytes(operation));
        WriteField(stream, SHA256.HashData(body));
        return Convert.ToBase64String(SHA256.HashData(stream.ToArray()))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static void WriteField(Stream stream, ReadOnlySpan<byte> value)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, value.Length);
        stream.Write(length);
        stream.Write(value);
    }
}
