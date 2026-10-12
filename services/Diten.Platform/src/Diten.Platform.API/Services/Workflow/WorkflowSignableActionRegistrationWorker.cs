using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Common.Tenancy;
using MediatR;

namespace Diten.Platform.API.Services.Workflow;

/// <summary>
/// MOD-0022 E5W2 (CT-D19) — registers MOD-0023's signable action (<see cref="WorkflowSignableActions.TaskApprove"/>) in the
/// global e-signature action catalog at startup, IN-PROCESS: the same <c>RegisterSignableActionsCommand</c> the S2S endpoint
/// (§4.23) sends for remote modules, without a self-call or an internal key. Idempotent (an unchanged action is a no-op;
/// existing tenant policy rows are never touched). Best-effort like the module self-registration: a failure is logged and
/// retried a few times, and never blocks startup — until it succeeds the dialog falls back to the visual mode Optional.
/// </summary>
public sealed class WorkflowSignableActionRegistrationWorker : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(15);
    private const int Attempts = 4;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkflowSignableActionRegistrationWorker> _logger;

    public WorkflowSignableActionRegistrationWorker(IServiceScopeFactory scopeFactory, ILogger<WorkflowSignableActionRegistrationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        for (var attempt = 1; attempt <= Attempts && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                // The catalog is global; the platform scope is what the S2S endpoint runs under (no tenant).
                scope.ServiceProvider.GetRequiredService<ITenantContext>().SetPlatformContext(Guid.Empty);
                var response = await scope.ServiceProvider.GetRequiredService<IMediator>()
                    .Send(WorkflowSignableActions.Registration(), stoppingToken);
                if (response.IsSuccessful)
                {
                    _logger.LogInformation("Workflow signable action {ActionCode} registered (registered={Registered}, unchanged={Unchanged}).",
                        WorkflowSignableActions.TaskApprove, response.Data?.Registered, response.Data?.Unchanged);
                    return;
                }

                _logger.LogWarning("Workflow signable action registration failed ({Status} {ReasonCode}), attempt {Attempt}/{Attempts}.",
                    response.StatusCode, response.ReasonCode, attempt, Attempts);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Workflow signable action registration threw, attempt {Attempt}/{Attempts}.", attempt, Attempts);
            }

            try
            {
                await Task.Delay(RetryDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
