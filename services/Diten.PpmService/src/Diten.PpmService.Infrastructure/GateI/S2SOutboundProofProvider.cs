using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.PpmService.Application.GateI;

namespace Diten.PpmService.Infrastructure.GateI;

internal sealed class UnavailableS2SOutboundProofProvider : IS2SOutboundProofProvider
{
    public bool IsAvailable => false;

    public ValueTask<S2SOutboundProofResult> IssueAsync(
        S2SOutboundProofRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new S2SOutboundProofResult(
            S2SOutboundProofDisposition.Unavailable,
            StableCode: "ppm_gate_i_s2s_proof_unavailable"));
    }
}

internal sealed record LocalTestS2SOutboundProof(string BearerToken) : IS2SOutboundProof;

public static class S2SOutboundLocalEvidenceTestHost
{
    public static IS2SOutboundProofProvider CreateEphemeralProvider(TimeProvider? timeProvider = null) =>
        new EphemeralRsaS2SOutboundProofProvider(timeProvider ?? TimeProvider.System);

    private sealed class EphemeralRsaS2SOutboundProofProvider : IS2SOutboundProofProvider, IDisposable
    {
        private const string Issuer = "diten-auth-service.local-evidence.test-only";
        private const string Kid = "ppm-gate-i-r3-ephemeral.test-only";
        private readonly RSA rsa = RSA.Create(3072);
        private readonly TimeProvider timeProvider;

        public EphemeralRsaS2SOutboundProofProvider(TimeProvider timeProvider) =>
            this.timeProvider = timeProvider;

        public bool IsAvailable => true;

        public ValueTask<S2SOutboundProofResult> IssueAsync(
            S2SOutboundProofRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!S2SOutboundReceiverProfiles.All.Contains(request.Receiver)
                || request.TrustedContext.TenantId == Guid.Empty
                || request.TrustedContext.EffectiveActorId == Guid.Empty
                || !request.TrustedContext.DelegatedActorId.HasValue
                || request.TrustedContext.DelegatedActorId.Value == Guid.Empty
                || !request.TrustedContext.DelegatedActorProofValidated)
            {
                return ValueTask.FromResult(new S2SOutboundProofResult(
                    S2SOutboundProofDisposition.Unauthenticated,
                    StableCode: "ppm_gate_i_s2s_context_invalid"));
            }

            var expected = S2SOutboundCanonicalRequestBinding.Compute(
                request.Receiver.Method,
                request.Receiver.Path,
                request.RawBody.Span,
                request.TrustedContext.TenantId,
                request.Receiver.Operation,
                [request.Receiver.Permission]);
            if (!S2SOutboundCanonicalRequestBinding.FixedTimeMatches(request.RequestHash, expected))
            {
                return ValueTask.FromResult(new S2SOutboundProofResult(
                    S2SOutboundProofDisposition.Unauthenticated,
                    StableCode: "ppm_gate_i_s2s_request_binding_invalid"));
            }

            var now = timeProvider.GetUtcNow();
            var header = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
            {
                ["alg"] = "RS256",
                ["kid"] = Kid,
                ["typ"] = "diten-delegated-actor-proof+jwt"
            });
            var payload = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
            {
                ["iss"] = Issuer,
                ["aud"] = request.Receiver.Audience,
                ["client_id"] = request.Receiver.ClientId,
                ["tenant_id"] = request.TrustedContext.TenantId.ToString("D"),
                ["sub"] = request.TrustedContext.EffectiveActorId.ToString("D"),
                ["delegated_actor_id"] = request.TrustedContext.DelegatedActorId!.Value.ToString("D"),
                ["scope"] = "diten.s2s.delegated.invoke",
                ["operation_id"] = request.Receiver.Operation,
                ["permission"] = request.Receiver.Permission,
                ["request_hash"] = request.RequestHash,
                ["test_identity"] = "ppm-gate-i-r3-local-evidence-v1",
                ["iat"] = now.ToUnixTimeSeconds(),
                ["exp"] = now.AddMinutes(5).ToUnixTimeSeconds(),
                ["jti"] = Guid.NewGuid().ToString("D"),
                ["nonce"] = Guid.NewGuid().ToString("D")
            });
            var signingInput = Base64Url(header) + "." + Base64Url(payload);
            var signature = rsa.SignData(
                Encoding.ASCII.GetBytes(signingInput),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            return ValueTask.FromResult(new S2SOutboundProofResult(
                S2SOutboundProofDisposition.Issued,
                new LocalTestS2SOutboundProof(signingInput + "." + Base64Url(signature))));
        }

        public void Dispose() => rsa.Dispose();

        private static string Base64Url(ReadOnlySpan<byte> bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
