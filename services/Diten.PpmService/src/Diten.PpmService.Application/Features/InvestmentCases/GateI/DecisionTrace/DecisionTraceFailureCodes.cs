using System.Text;
using System.Text.Json;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.GateI.DecisionTrace;

namespace Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;


public static class DecisionTraceFailureCodes
{
    public const string MalformedRequest = "gate_i_decision_trace_malformed_request";
    public const string AuthenticationFailure = "gate_i_decision_trace_authentication_failure";
    public const string PermissionDenied = "gate_i_decision_trace_permission_denied";
    public const string NotFound = "gate_i_decision_trace_not_found";
    public const string Conflict = "gate_i_decision_trace_conflict";
    public const string DependencyUnavailable = "gate_i_decision_trace_dependency_unavailable";
    public const string NonRuntimeContractOnly = "gate_i_decision_trace_non_runtime_contract_only";
}
