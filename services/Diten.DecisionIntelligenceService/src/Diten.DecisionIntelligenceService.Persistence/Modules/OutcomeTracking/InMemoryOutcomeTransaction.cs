using Diten.DecisionIntelligenceService.Application.Modules.OutcomeTracking;
using Diten.DecisionIntelligenceService.Domain.Modules.OutcomeTracking;

namespace Diten.DecisionIntelligenceService.Persistence.Modules.OutcomeTracking;

public sealed record TransactionSnapshot(int Business, int Receipts, int AuditIntents, int Outbox);
public sealed class InMemoryOutcomeTransaction : IOutcomeTransaction
{
    private readonly Dictionary<(Guid Tenant,Guid Actor,string Operation,string Key),(string Hash,MutationResult Result)> receipts = [];
    private TransactionSnapshot state = new(0,0,0,0);
    private Guid? sessionId;
    public int BusinessBodyCalls { get; private set; }
    public int CommitOnlyCalls { get; private set; }
    public TransactionSnapshot Snapshot => state;

    public MutationResult Execute(MutationScope scope, Func<MutationResult> mutation, IReadOnlySet<Participant> participants, Participant? failAt = null)
    {
        var key=(scope.TenantId,scope.ActorId,scope.Operation,scope.Key);
        if(receipts.TryGetValue(key,out var prior)) return prior.Hash==scope.Hash?prior.Result:new(409,"conflict");
        ValidateParticipants(participants); var before=state; sessionId=Guid.NewGuid();
        try { foreach(var p in participants.OrderBy(x=>(int)x)){ if(failAt==p)throw new InvalidOperationException("fault:"+p); if(IsBusiness(p)){BusinessBodyCalls++;state=state with{Business=state.Business+1};} else if(p==Participant.Receipt)state=state with{Receipts=state.Receipts+1};else if(p==Participant.AuditIntent)state=state with{AuditIntents=state.AuditIntents+1};else if(p==Participant.Outbox)state=state with{Outbox=state.Outbox+1}; } var result=mutation(); receipts[key]=(scope.Hash,result); return result; }
        catch { state=before; throw; }
    }
    public Guid BeginUnknownCommit(MutationScope scope, MutationResult committed) { var key=(scope.TenantId,scope.ActorId,scope.Operation,scope.Key); sessionId=Guid.NewGuid(); BusinessBodyCalls++; state=state with{Business=state.Business+1,Receipts=state.Receipts+1,AuditIntents=state.AuditIntents+1,Outbox=state.Outbox+1}; receipts[key]=(scope.Hash,committed); return sessionId.Value; }
    public void CommitOnly(Guid sameSession) { if(sessionId!=sameSession)throw new InvalidOperationException("same session required"); CommitOnlyCalls++; }
    public MutationResult Reconcile(MutationScope scope, Reconciliation result) { var key=(scope.TenantId,scope.ActorId,scope.Operation,scope.Key); return result switch { Reconciliation.Matching when receipts.TryGetValue(key,out var r)&&r.Hash==scope.Hash=>r.Result, Reconciliation.Conflicting=>new(409,"conflict"), Reconciliation.ConfirmedAbsent=>new(503,"missing"), _=>new(503,"indeterminate") }; }
    private static bool IsBusiness(Participant p)=>p is Participant.Aggregate or Participant.Version or Participant.Measurement or Participant.Link;
    private static void ValidateParticipants(IReadOnlySet<Participant> p){if(p.Count(x=>IsBusiness(x))!=1||!p.Contains(Participant.Receipt)||!p.Contains(Participant.AuditIntent)||!p.Contains(Participant.Outbox)||p.Count!=4)throw new InvalidOperationException("exact participants required");}
}

public sealed class TenantOutcomeReader(IReadOnlyCollection<Outcome> outcomes)
{
    public Outcome? Find(Guid tenantId, Guid id, Func<Outcome,bool>? visibility = null) => outcomes.SingleOrDefault(x=>x.TenantId==tenantId&&x.Id==id&&(visibility?.Invoke(x)??true));
}
