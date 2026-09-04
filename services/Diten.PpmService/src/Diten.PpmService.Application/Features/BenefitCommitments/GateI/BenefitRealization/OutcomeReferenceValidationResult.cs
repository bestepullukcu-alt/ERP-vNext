using System.Security.Cryptography;
using System.Text;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.GateI.BenefitRealization;

namespace Diten.PpmService.Application.Features.BenefitCommitments.GateI.BenefitRealization;


public sealed record OutcomeReferenceValidationResult(int StatusCode, string Code, BenefitCommitmentOutcomeReferenceV1? Reference = null)
{
    public static OutcomeReferenceValidationResult From(int status, string code) => new(status, code);
}
