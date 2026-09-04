using System.Text;
using System.Text.Json;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.GateI.DecisionTrace;

namespace Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;


public interface IDecisionReferenceValidationPort { Task<DecisionReferenceProviderResult> ValidateAsync(DecisionTraceValidationRequest request, DecisionTraceTrustedContext trustedContext, CancellationToken cancellationToken); }
