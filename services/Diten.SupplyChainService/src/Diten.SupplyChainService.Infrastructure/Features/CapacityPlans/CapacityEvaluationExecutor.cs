using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Diten.SupplyChainService.Domain.Features.CapacityPlans;
namespace Diten.SupplyChainService.Infrastructure.Features.CapacityPlans;
public sealed class CapacityEvaluationExecutor(ICapacityLeaseStore leases, ILogger<CapacityEvaluationExecutor> logger) : BackgroundService
{
    public const string OracleId = "CAPACITY-EVAL-FIXTURE-192-01@1";
    private static readonly CapacityBottleneck FiniteResult = new("line-4","2027-W03","520.000","480.000","40.000","HOUR");
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunOnceAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while(await timer.WaitForNextTickAsync(stoppingToken)) await RunOnceAsync(stoppingToken);
    }
    public async Task RunOnceAsync(CancellationToken ct)
    {
        foreach(var scope in await leases.FindPendingScopesAsync(ct))
        {
            while(!ct.IsCancellationRequested)
            {
                var lease=await leases.ClaimAsync(scope,ct);
                if(lease is null) break;
                try
                {
                    // This is a literal fixture oracle. Repeated computation is allowed; the fence
                    // and transaction, rather than computation count, guard the durable effect.
                    var valid=scope.TenantId==DemandFixtureReader.Tenant && scope.LegalEntityId==DemandFixtureReader.LegalEntity &&
                        lease.ResourceRefs.Count==1 && lease.ResourceRefs[0]=="line-4" &&
                        lease.EvaluationMode is "Finite" or "Infinite";
                    var result=valid && lease.EvaluationMode=="Finite" ? new[]{FiniteResult} : Array.Empty<CapacityBottleneck>();
                    var status=valid ? "Completed" : "Failed";
                    var saved=await leases.TerminalAsync(scope,lease.Id,lease.Version,lease.Fence,status,result,false,ct);
                    if(!saved) logger.LogWarning("Capacity lease lost for evaluation {EvaluationId}",lease.Id);
                }
                catch(OperationCanceledException) when(ct.IsCancellationRequested) { throw; }
                catch(Exception ex)
                {
                    logger.LogError(ex,"Capacity fixture evaluation failed for {EvaluationId}",lease.Id);
                    // The lease will expire and a later scoped scan can reclaim it.
                }
            }
        }
    }
}
