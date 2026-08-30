using System.Text;
using System.Text.Json;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.GateI.DecisionTrace;

namespace Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;


public enum DecisionReferenceProviderResultKind { Resolved, AuthenticationFailure, PermissionDenied, NotFound, Ineligible, Stale, Conflict, UnsupportedVersion, Timeout, Unavailable, Malformed, Indeterminate }
