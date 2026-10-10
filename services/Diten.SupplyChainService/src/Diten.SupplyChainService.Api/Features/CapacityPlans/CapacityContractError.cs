namespace Diten.SupplyChainService.Api.Features.CapacityPlans;
public static class CapacityContractError
{
    public static object Create(string code,int status,Guid correlation) => new {
        error=new { code, message=Message(code,status), correlationId=correlation }, contractVersion="v1" };
    private static string Message(string code,int status) => code switch
    {
        "UNKNOWN_CAPACITY_PLAN"=>"Capacity plan not found",
        "UNKNOWN_CAPACITY_SCENARIO"=>"Capacity scenario not found",
        "UNKNOWN_CAPACITY_EVALUATION"=>"Capacity evaluation not found",
        "CAPACITY_PLAN_ALREADY_EXISTS"=>"Capacity plan already exists for this horizon and demand version",
        "CAPACITY_PLAN_STATE_CONFLICT"=>"Capacity plan state does not allow a new scenario",
        "CAPACITY_SCENARIO_NAME_CONFLICT"=>"Capacity scenario name already exists in this plan",
        "EVALUATION_ALREADY_ACTIVE"=>"An evaluation is already active for this scenario",
        "IDEMPOTENCY_KEY_REUSED"=>"Idempotency key was used with a different valid payload",
        "INVALID_DEMAND_REFERENCE"=>"Supplied checksum differs from the exact scoped test fixture",
        "INVALID_CONSTRAINT_REFERENCE"=>"Constraint reference is invalid",
        _=>status switch {401=>"Authentication required",403=>"Permission denied",
            503=>code=="COMMIT_RESULT_UNRESOLVED"?"Commit result unresolved; retry with the same key":"Dependency unavailable",_=>"Invalid request"}
    };
}
