using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Diten.PpmService.Domain.GateI.BenefitRealization;


public sealed record BenefitCommitmentOutcomeReferenceV1(
    string ContractName,
    string ContractVersion,
    Guid BenefitCommitmentId,
    OutcomeReferenceV1 OutcomeReference)
{
    public const string ExactContractName = "ppm.benefit-commitment-outcome-reference";
    public const string ExactContractVersion = "1.0";

    public void ValidateIdentity()
    {
        if (!string.Equals(ContractName, ExactContractName, StringComparison.Ordinal) ||
            !string.Equals(ContractVersion, ExactContractVersion, StringComparison.Ordinal) ||
            BenefitCommitmentId == Guid.Empty || OutcomeReference is null)
        {
            throw new OutcomeReferenceContractException(OutcomeReferenceContractError.InvalidValue);
        }

        OutcomeReference.ValidateIdentity();
    }
}
