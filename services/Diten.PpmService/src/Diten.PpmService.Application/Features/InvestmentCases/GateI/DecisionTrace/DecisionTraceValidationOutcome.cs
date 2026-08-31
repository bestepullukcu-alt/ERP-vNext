using System.Text;
using System.Text.Json;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.GateI.DecisionTrace;

namespace Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;


public sealed record DecisionTraceValidationOutcome(int StatusCode, string? FailureCode, DecisionRevisionReferenceV1? Reference, DecisionTraceValidationMode? Mode, bool? Resolved, bool? EligibleForNewReference, DecisionReferenceDisposition? Disposition) { public bool IsSuccess => StatusCode == 200; }
