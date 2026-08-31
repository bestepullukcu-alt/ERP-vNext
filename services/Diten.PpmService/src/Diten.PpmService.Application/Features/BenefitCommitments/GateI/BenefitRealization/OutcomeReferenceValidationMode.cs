using System.Security.Cryptography;
using System.Text;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.GateI.BenefitRealization;

namespace Diten.PpmService.Application.Features.BenefitCommitments.GateI.BenefitRealization;


public enum OutcomeReferenceValidationMode
{
    HistoricalResolve,
    NewReferenceEligibility,
    CurrentSelectionEligibility
}
