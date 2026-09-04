using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Diten.PpmService.Domain.GateI.BenefitRealization;


public enum OutcomeReferenceContractError
{
    Malformed,
    InvalidValue,
    UnsupportedVersion
}
