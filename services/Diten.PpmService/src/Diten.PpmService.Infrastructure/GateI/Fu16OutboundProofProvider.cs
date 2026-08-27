using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.PpmService.Application.GateI;

namespace Diten.PpmService.Infrastructure.GateI;

internal sealed class UnavailableFu16OutboundProofProvider : IFu16OutboundProofProvider
{
    public bool IsAvailable => false;

    public ValueTask<Fu16OutboundProofResult> IssueAsync(
        Fu16OutboundProofRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new Fu16OutboundProofResult(
            Fu16OutboundProofDisposition.Unavailable,
            StableCode: "ppm_gate_i_fu16_proof_unavailable"));
    }
}

internal sealed record LocalTestFu16OutboundProof(string BearerToken) : IFu16OutboundProof;

public static class Fu16OutboundLocalEvidenceTestHost
{
    public static IFu16OutboundProofProvider CreateEphemeralProvider(TimeProvider? timeProvider = null) =>
        new EphemeralRsaFu16OutboundProofProvider(timeProvider ?? TimeProvider.System);

    private sealed class EphemeralRsaFu16OutboundProofProvider : IFu16OutboundProofProvider, IDisposable
    {
        private const string Issuer = "diten-auth-service.local-evidence.test-only";
        private const string Kid = "ppm-gate-i-r3-ephemeral.test-only";
        private readonly RSA rsa = RSA.Create(3072);
        private readonly TimeProvider timeProvider;

        public EphemeralRsaFu16OutboundProofProvider(TimeProvider timeProvider) =>
            this.timeProvider = timeProvider;

        public bool IsAvailable => true;

        public ValueTask<Fu16OutboundProofResult> IssueAsync(
            Fu16OutboundProofRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Fu16OutboundReceiverProfiles.All.Contains(request.Receiver)
                || request.TrustedContext.TenantId == Guid.Empty
                || request.TrustedContext.EffectiveActorId == Guid.Empty
                || !request.TrustedContext.DelegatedActorId.HasValue
                || request.TrustedContext.DelegatedActorId.Value == Guid.Empty
                || !request.TrustedContext.DelegatedActorProofValidated)
            {
                return ValueTask.FromResult(new Fu16OutboundProofResult(
                    Fu16OutboundProofDisposition.Unauthenticated,
                    StableCode: "ppm_gate_i_fu16_context_invalid"));
            }

            var expected = Fu16OutboundCanonicalRequestBinding.Compute(
                request.Receiver.Method,
                request.Receiver.Path,
                request.RawBody.Span,
                request.TrustedContext.TenantId,
                request.Receiver.Operation,
                [request.Receiver.Permission]);
            if (!Fu16OutboundCanonicalRequestBinding.FixedTimeMatches(request.RequestHash, expected))
            {
                return ValueTask.FromResult(new Fu16OutboundProofResult(
                    Fu16OutboundProofDisposition.Unauthenticated,
                    StableCode: "ppm_gate_i_fu16_request_binding_invalid"));
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
            return ValueTask.FromResult(new Fu16OutboundProofResult(
                Fu16OutboundProofDisposition.Issued,
                new LocalTestFu16OutboundProof(signingInput + "." + Base64Url(signature))));
        }

        public void Dispose() => rsa.Dispose();

        private static string Base64Url(ReadOnlySpan<byte> bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
