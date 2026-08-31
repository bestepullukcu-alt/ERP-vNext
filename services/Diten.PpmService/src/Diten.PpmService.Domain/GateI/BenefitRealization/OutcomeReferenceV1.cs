using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Diten.PpmService.Domain.GateI.BenefitRealization;


public sealed record OutcomeReferenceV1(
    string ContractName,
    string ContractVersion,
    Guid OutcomeId,
    Guid OutcomeVersionId,
    int OutcomeVersionNumber)
{
    public const string ExactContractName = "diten.decision-intelligence.outcome-reference";
    public const string ExactContractVersion = "1.0";

    public void ValidateIdentity()
    {
        if (!string.Equals(ContractName, ExactContractName, StringComparison.Ordinal) ||
            !string.Equals(ContractVersion, ExactContractVersion, StringComparison.Ordinal) ||
            OutcomeId == Guid.Empty || OutcomeVersionId == Guid.Empty || OutcomeVersionNumber < 1)
        {
            throw new OutcomeReferenceContractException(OutcomeReferenceContractError.InvalidValue);
        }
    }
}
