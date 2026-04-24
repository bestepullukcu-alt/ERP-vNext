using Diten.Application.Common.Models;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Diten.Application.EnterpriseStrategy.Shared;

public static class EnterpriseStrategyPermissions
{
    public const string GoalView = "strategy.goal.view";
    public const string GoalCreate = "strategy.goal.create";
    public const string GoalEdit = "strategy.goal.edit";
    public const string GoalArchive = "strategy.goal.archive";
    public const string GoalActivate = "strategy.goal.activate";

    public const string ObjectiveView = "strategy.objective.view";
    public const string ObjectiveCreate = "strategy.objective.create";
    public const string ObjectiveEdit = "strategy.objective.edit";
    public const string ObjectiveArchive = "strategy.objective.archive";
    public const string ObjectiveActivate = "strategy.objective.activate";

    public const string ConnectionView = "strategy.connection.view";
    public const string ConnectionCreate = "strategy.connection.create";
    public const string ConnectionEdit = "strategy.connection.edit";
    public const string ConnectionDelete = "strategy.connection.delete";
    public const string ConnectionValidate = "strategy.connection.validate";

    public const string InitiativeView = "strategy.initiative.view";
    public const string InitiativeLink = "strategy.initiative.link";
    public const string InitiativeUnlink = "strategy.initiative.unlink";
    public const string InitiativeSync = "strategy.initiative.sync";

    public const string ProjectView = "strategy.project.view";
    public const string ProjectLink = "strategy.project.link";
    public const string ProjectUnlink = "strategy.project.unlink";
    public const string ProjectSync = "strategy.project.sync";

    public const string PlanningCycleView = "strategy.planning_cycle.view";
    public const string PlanningCycleCreate = "strategy.planning_cycle.create";
    public const string StrategyPeriodView = "strategy.strategy_period.view";
    public const string StrategyPeriodCreate = "strategy.strategy_period.create";

    public const string LibraryView = "strategy.library.view";
    public const string LibraryImport = "strategy.library.import";
    public const string LibraryEdit = "strategy.library.edit";
    public const string LibraryApprove = "strategy.library.approve";
    public const string LibraryPublish = "strategy.library.publish";
    public const string LibraryRetire = "strategy.library.retire";
    public const string LibraryInstantiate = "strategy.library.instantiate";
}

public static class EnterpriseStrategyErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string Forbidden = "FORBIDDEN";
    public const string DependencyUnavailable = "DEPENDENCY_UNAVAILABLE";
    public const string StaleVersion = "STALE_VERSION";
    public const string InternalError = "INTERNAL_ERROR";
}

public static class EnterpriseStrategyEventNames
{
    public const string GoalCreated = "goal.created.v1";
    public const string GoalUpdated = "goal.updated.v1";
    public const string GoalStatusChanged = "goal.status_changed.v1";
    public const string GoalArchived = "goal.archived.v1";
    public const string GoalRestored = "goal.restored.v1";

    public const string ObjectiveCreated = "objective.created.v1";
    public const string ObjectiveUpdated = "objective.updated.v1";
    public const string ObjectiveStatusChanged = "objective.status_changed.v1";
    public const string ObjectiveArchived = "objective.archived.v1";
    public const string ObjectiveRestored = "objective.restored.v1";
    public const string ObjectiveReparented = "objective.reparented.v1";

    public const string ConnectionCreated = "connection.created.v1";
    public const string ConnectionUpdated = "connection.updated.v1";
    public const string ConnectionStatusChanged = "connection.status_changed.v1";
    public const string ConnectionValidationFailed = "connection.validation_failed.v1";

    public const string InitiativeStrategyLinked = "initiative.strategy_linked.v1";
    public const string InitiativeStrategyUpdated = "initiative.strategy_link_updated.v1";
    public const string InitiativeStrategyUnlinked = "initiative.strategy_unlinked.v1";
    public const string InitiativeSyncCompleted = "initiative.sync_completed.v1";
    public const string InitiativeSyncFailed = "initiative.sync_failed.v1";

    public const string ProjectStrategyLinked = "project.strategy_linked.v1";
    public const string ProjectStrategyUpdated = "project.strategy_link_updated.v1";
    public const string ProjectStrategyUnlinked = "project.strategy_unlinked.v1";
    public const string ProjectCreated = "project.created.v1";
    public const string ProjectAnchorChanged = "project.anchor_changed.v1";
    public const string ProjectTemplateApplied = "project.template_applied.v1";
    public const string ProjectTemplateChanged = "project.template_changed.v1";
    public const string ProjectTemplateCleared = "project.template_cleared.v1";
    public const string ProjectTemplateReapplied = "project.template_reapplied.v1";
    public const string ProjectStatusChanged = "project.status_changed.v1";
    public const string ProjectBudgetChanged = "project.budget_changed.v1";
    public const string ProjectSyncCompleted = "project.sync_completed.v1";
    public const string ProjectSyncFailed = "project.sync_failed.v1";

    public const string LibraryImportStarted = "library.import_started.v1";
    public const string LibraryImportCompleted = "library.import_completed.v1";
    public const string LibraryImportApproved = "library.import_approved.v1";
    public const string LibraryTemplateUpdated = "library.template_updated.v1";
    public const string LibraryTemplatePublished = "library.template_published.v1";
    public const string LibraryTemplateRetired = "library.template_retired.v1";
    public const string LibraryPackPublished = "library.pack_published.v1";
    public const string LibraryPackRetired = "library.pack_retired.v1";
    public const string LibraryInstantiationCompleted = "library.instantiation_completed.v1";
}

public sealed class MutationEnvelope<T>
{
    public T? Payload { get; set; }
    public int ExpectedVersion { get; set; }
    public string? CorrelationId { get; set; }
}

public sealed class AuditEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Actor { get; set; } = "system";
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string ObjectType { get; set; } = string.Empty;
    public string ObjectId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string SourceModule { get; set; } = "enterprise-strategy";
    public string BeforeSummary { get; set; } = string.Empty;
    public string AfterSummary { get; set; } = string.Empty;
}

public sealed class AdapterSyncStatus
{
    public string Dependency { get; set; } = "ppm";
    public DateTime? LastSuccessUtc { get; set; }
    public bool DegradedMode { get; set; }
    public string? LastErrorCode { get; set; }
    public string? LastErrorMessage { get; set; }
}

public interface ICorrelationContextAccessor
{
    string CorrelationId { get; set; }
}

public interface IEnterpriseStrategyAuthorizationService
{
    Task<bool> HasPermissionAsync(string permission, ClaimsPrincipal user, CancellationToken cancellationToken = default);
}

public interface IEnterpriseStrategyAuditSink
{
    Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}

public interface IEnterpriseStrategyAuditStore : IEnterpriseStrategyAuditSink
{
    Task<IReadOnlyList<AuditEvent>> ListAsync(
        string objectType,
        string objectId,
        CancellationToken cancellationToken = default);
}

public interface IEnterpriseStrategyObservability
{
    IDisposable TrackApiLatency(string endpoint);
    void TrackError(string endpoint, string errorCode);
    void TrackAdapterSync(string adapterName, bool success);
}

public sealed class InMemoryCorrelationContextAccessor : ICorrelationContextAccessor
{
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");
}

public sealed class DefaultEnterpriseStrategyAuthorizationService : IEnterpriseStrategyAuthorizationService
{
    private static readonly IReadOnlySet<string> KnownPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        EnterpriseStrategyPermissions.GoalView,
        EnterpriseStrategyPermissions.GoalCreate,
        EnterpriseStrategyPermissions.GoalEdit,
        EnterpriseStrategyPermissions.GoalArchive,
        EnterpriseStrategyPermissions.GoalActivate,
        EnterpriseStrategyPermissions.ObjectiveView,
        EnterpriseStrategyPermissions.ObjectiveCreate,
        EnterpriseStrategyPermissions.ObjectiveEdit,
        EnterpriseStrategyPermissions.ObjectiveArchive,
        EnterpriseStrategyPermissions.ObjectiveActivate,
        EnterpriseStrategyPermissions.ConnectionView,
        EnterpriseStrategyPermissions.ConnectionCreate,
        EnterpriseStrategyPermissions.ConnectionEdit,
        EnterpriseStrategyPermissions.ConnectionDelete,
        EnterpriseStrategyPermissions.ConnectionValidate,
        EnterpriseStrategyPermissions.InitiativeView,
        EnterpriseStrategyPermissions.InitiativeLink,
        EnterpriseStrategyPermissions.InitiativeUnlink,
        EnterpriseStrategyPermissions.InitiativeSync,
        EnterpriseStrategyPermissions.ProjectView,
        EnterpriseStrategyPermissions.ProjectLink,
        EnterpriseStrategyPermissions.ProjectUnlink,
        EnterpriseStrategyPermissions.ProjectSync,
        EnterpriseStrategyPermissions.PlanningCycleView,
        EnterpriseStrategyPermissions.PlanningCycleCreate,
        EnterpriseStrategyPermissions.StrategyPeriodView,
        EnterpriseStrategyPermissions.StrategyPeriodCreate,
        EnterpriseStrategyPermissions.LibraryView,
        EnterpriseStrategyPermissions.LibraryImport,
        EnterpriseStrategyPermissions.LibraryEdit,
        EnterpriseStrategyPermissions.LibraryApprove,
        EnterpriseStrategyPermissions.LibraryPublish,
        EnterpriseStrategyPermissions.LibraryRetire,
        EnterpriseStrategyPermissions.LibraryInstantiate
    };

    public Task<bool> HasPermissionAsync(string permission, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        // Local dev bootstrap: enables end-to-end ES&BP testing when upstream auth/permission claims
        // are not yet wired in this environment.
        if (IsDevelopmentPermissionBootstrapEnabled() &&
            KnownPermissions.Contains(permission))
        {
            return Task.FromResult(true);
        }

        if (user?.Identity?.IsAuthenticated != true)
            return Task.FromResult(false);

        var hasPermissionClaim = user.Claims.Any(x =>
            string.Equals(x.Type, "permission", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Value, permission, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(hasPermissionClaim);
    }

    private static bool IsDevelopmentPermissionBootstrapEnabled()
    {
        var enforcePermissions = Environment.GetEnvironmentVariable("DITEN_ESBP_ENFORCE_PERMISSIONS");
        if (!string.IsNullOrWhiteSpace(enforcePermissions))
        {
            return !(string.Equals(enforcePermissions, "1", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(enforcePermissions, "true", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(enforcePermissions, "yes", StringComparison.OrdinalIgnoreCase));
        }

        var configured = Environment.GetEnvironmentVariable("DITEN_ESBP_DEV_PERMISSION_BOOTSTRAP");
        if (string.IsNullOrWhiteSpace(configured))
            return true;

        return string.Equals(configured, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(configured, "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(configured, "yes", StringComparison.OrdinalIgnoreCase);
    }
}

public class NoOpEnterpriseStrategyAuditStore : IEnterpriseStrategyAuditStore
{
    public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<AuditEvent>> ListAsync(
        string objectType,
        string objectId,
        CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AuditEvent>>(Array.Empty<AuditEvent>());
}

public sealed class NoOpEnterpriseStrategyAuditSink : NoOpEnterpriseStrategyAuditStore
{
}

public sealed class LoggerEnterpriseStrategyObservability : IEnterpriseStrategyObservability
{
    private readonly ILogger<LoggerEnterpriseStrategyObservability> _logger;

    public LoggerEnterpriseStrategyObservability(ILogger<LoggerEnterpriseStrategyObservability> logger)
    {
        _logger = logger;
    }

    public IDisposable TrackApiLatency(string endpoint)
    {
        var start = DateTime.UtcNow;
        return new Scope(() =>
        {
            var elapsed = DateTime.UtcNow - start;
            _logger.LogInformation("enterprise_strategy_api_latency endpoint={Endpoint} elapsed_ms={ElapsedMs}", endpoint, elapsed.TotalMilliseconds);
        });
    }

    public void TrackError(string endpoint, string errorCode)
    {
        _logger.LogWarning("enterprise_strategy_api_error endpoint={Endpoint} error_code={ErrorCode}", endpoint, errorCode);
    }

    public void TrackAdapterSync(string adapterName, bool success)
    {
        _logger.LogInformation("enterprise_strategy_adapter_sync adapter={Adapter} success={Success}", adapterName, success);
    }

    private sealed class Scope : IDisposable
    {
        private readonly Action _onDispose;

        public Scope(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            _onDispose();
        }
    }
}

public static class EnterpriseStrategyResult
{
    public static bool IsStaleWrite(int expectedVersion, int currentVersion)
        => expectedVersion > 0 && expectedVersion != currentVersion;

    public static Response<T> Forbidden<T>(string message = "Forbidden")
        => Response<T>.Fail(EnterpriseStrategyErrorCodes.Forbidden, Build(message));

    public static Response<T> DependencyUnavailable<T>(string message = "Dependency unavailable")
        => Response<T>.Fail(EnterpriseStrategyErrorCodes.DependencyUnavailable, Build(message));

    public static Response<T> StaleVersion<T>(string message = "Record has been updated by another request")
        => Response<T>.Fail(EnterpriseStrategyErrorCodes.StaleVersion, Build(message));

    public static Response<T> Validation<T>(string message = "Validation failed")
        => Response<T>.Fail(EnterpriseStrategyErrorCodes.ValidationError, Build(message));

    private static Dictionary<string, List<string>> Build(string message)
        => new(StringComparer.OrdinalIgnoreCase) { ["general"] = new() { message } };
}
