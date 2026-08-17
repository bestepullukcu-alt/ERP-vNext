namespace Diten.Platform.Application.Features.ESignature.Services;

public interface IInternalSignaturePolicy
{
    string PolicyCode { get; }
    void ValidateAttestation(string attestationText);
}

public sealed class InternalSignaturePolicy : IInternalSignaturePolicy
{
    public string PolicyCode => "INTERNAL_APPROVAL_EVIDENCE_V1";

    public void ValidateAttestation(string attestationText)
    {
        if (string.IsNullOrWhiteSpace(attestationText) || attestationText.Trim().Length < 10)
        {
            throw new InvalidOperationException("Attestation text must contain at least 10 characters.");
        }

        if (attestationText.Length > 4000)
        {
            throw new InvalidOperationException("Attestation text cannot exceed 4000 characters.");
        }
    }
}
