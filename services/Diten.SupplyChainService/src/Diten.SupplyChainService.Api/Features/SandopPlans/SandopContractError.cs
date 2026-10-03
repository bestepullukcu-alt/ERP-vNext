namespace Diten.SupplyChainService.Api.Features.SandopPlans;
public static class SandopContractError
{ public static object Create(string code,Guid correlation)
{ var message=code switch {
 "INVALID_CORRELATION_ID"=>"Invalid correlation identifier","UNAUTHENTICATED"=>"Authentication required","FORBIDDEN"=>"Permission denied","UNKNOWN_SANDOP_PLAN"=>"S&OP plan not found","SANDOP_PLAN_ALREADY_EXISTS"=>"S&OP plan already exists for this horizon and demand version","IDEMPOTENCY_KEY_REUSED"=>"Idempotency key was used with a different valid payload","SANDOP_PLAN_STATE_CONFLICT"=>"S&OP plan state does not allow a new snapshot","SANDOP_SIGN_OFF_STATE_CONFLICT"=>"S&OP plan state does not permit sign-off","INVALID_SNAPSHOT_REFERENCE"=>"Snapshot does not belong to the S&OP plan","SIGN_OFF_ALREADY_RECORDED"=>"This role already recorded a decision for the snapshot","INVALID_DEMAND_REFERENCE"=>"Referenced DEMAND plan version is unknown or not published","COMMIT_RESULT_UNRESOLVED"=>"Commit result unresolved; retry with the same key","DEPENDENCY_UNAVAILABLE"=>"Dependency unavailable",_=>"Invalid request" };
 return new {error=new{code,message,correlationId=correlation},contractVersion="v1"};
} }
