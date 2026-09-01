namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;

public enum CoreFailure { BadRequest = 400, Unauthenticated = 401, Forbidden = 403, NotFound = 404, Conflict = 409, Unavailable = 503 }
public static class ProcessModelingErrors
{
    public const string MissingOrInvisible="process_model_not_found";
    public const string StaleVersion="process_model_stale_version";
    public const string LifecycleConflict="process_model_lifecycle_conflict";
    public const string IdempotencyConflict="process_model_idempotency_conflict";
    public const string ProviderUnavailable="process_model_provider_unavailable";
    public const string TransactionIndeterminate="process_model_transaction_indeterminate";
}
public static class ProcessModelingCoreBoundary
{
    public static CoreMutationResult Evaluate(bool valid,bool authenticated,bool permitted,bool visible,bool current,bool available)
    {
        if(!valid)return new(false,400,"process_model_bad_request");if(!authenticated)return new(false,401,"process_model_unauthenticated");if(!permitted)return new(false,403,"process_model_permission_denied");if(!visible)return new(false,404,ProcessModelingErrors.MissingOrInvisible);if(!current)return new(false,409,ProcessModelingErrors.StaleVersion);if(!available)return new(false,503,ProcessModelingErrors.ProviderUnavailable);return new(true,200,"process_model_ok");
    }
}

public sealed record CoreMutationRequest(Guid TenantId, Guid SubjectId, string CommandFamily, string IdempotencyKey, string CanonicalPayloadHash, int ExpectedVersion, Guid AggregateId);
public sealed record CoreMutationResult(bool Accepted, int HttpStatus, string StableCode, Guid? AggregateId = null);

public interface IProcessModelingAtomicMutationStore
{
    Task<CoreMutationResult> ExecuteAsync(CoreMutationRequest request, Func<CancellationToken, Task<string>> businessMutation, CancellationToken cancellationToken);
}

public static class PublishProcessModelVersionContract
{
    public const string CommandName = "PublishProcessModelVersionCommand";
    public const string Permission = "management-governance.process-modeling.models.publish";
    public static CoreMutationResult FailClosed() => new(false, 503, "process_model_publish_second_slice_unavailable");
}
