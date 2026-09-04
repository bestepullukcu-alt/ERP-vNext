using System.Text;
using System.Text.Json;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.GateI.DecisionTrace;

namespace Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;


public sealed record DecisionReferenceProviderResult(DecisionReferenceProviderResultKind Kind, DecisionRevisionReferenceV1? Reference = null, DecisionTraceValidationMode? Mode = null, bool? Resolved = null, bool? EligibleForNewReference = null, DecisionReferenceDisposition? Disposition = null);
