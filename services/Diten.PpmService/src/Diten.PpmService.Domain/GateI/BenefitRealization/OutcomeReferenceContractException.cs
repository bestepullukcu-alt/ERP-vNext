using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Diten.PpmService.Domain.GateI.BenefitRealization;


public sealed class OutcomeReferenceContractException(OutcomeReferenceContractError error)
    : Exception(error.ToString())
{
    public OutcomeReferenceContractError Error { get; } = error;
}
