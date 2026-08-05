using Diten.FpaService.Domain.Modules.ScenarioPlanning;

namespace Diten.FpaService.Application.Modules.ScenarioPlanning;

public static class ScenarioPlanningAuthorizationManifest
{
    public const string OwnerModule="MOD-0138";public const string Audience="diten-fpa-service";public const string ClientId="diten.fpa";public const string WorkerOperation="fpa.scenario-planning.comparators.execute";
    public static readonly IReadOnlyList<OperationPermission> Entries=Array.AsReadOnly(new[]{"scenarios.read","scenarios.create","scenarios.update","version-drafts.read","version-drafts.create","version-drafts.update","version-drafts.abandon","versions.read","versions.publish","versions.retire","comparators.read","comparators.run","selections.read","selections.replace","selections.close","references.validate"}.Select(x=>new OperationPermission($"fpa.scenario-planning.{x}",$"fpa.scenario-planning.{x}")).ToArray());
}
public sealed record OperationPermission(string OperationId,string Permission);
public enum ReferenceValidationMode{HistoricalResolve,NewReferenceEligibility,CurrentSelectionEligibility}
public sealed record ReferenceValidationRequest(Guid TenantId,ReferenceValidationMode Mode,object Reference,InvestmentCaseContextV1? Context);
public sealed record ReferenceValidationResult(int StatusCode,bool Resolved,bool Eligible,bool Current);
public sealed record InvestmentCaseScenarioVersionReferenceV1(string ContractName,string ContractVersion,InvestmentCaseContextV1 InvestmentCaseContext,ScenarioVersionReferenceV1 ScenarioVersionReference);
public sealed record InvestmentCaseComparatorOutputReferenceV1(string ContractName,string ContractVersion,InvestmentCaseContextV1 InvestmentCaseContext,ComparatorOutputReferenceV1 ComparatorOutputReference);
public sealed record SelectedScenarioReferenceV1(string ContractName,string ContractVersion,InvestmentCaseContextV1 InvestmentCaseContext,ScenarioVersionReferenceV1 ScenarioVersionReference);

public sealed record IdempotencyIdentity(Guid TenantId,string PrincipalIdentity,string Operation,string IdempotencyKey);
public sealed record IdempotencyReceipt(IdempotencyIdentity Identity,string PayloadHash,string State,string ResponseLocator);
public enum ReceiptReconciliation{ReplayCommitted,BeginFreshAfterAborted,Indeterminate503}
public static class IdempotencyContract
{
    public static int ValidateReplay(IdempotencyReceipt existing,string payloadHash)=>existing.PayloadHash==payloadHash?200:409;
    public static ReceiptReconciliation Reconcile(string? state)=>state switch{"Committed"=>ReceiptReconciliation.ReplayCommitted,"Aborted"=>ReceiptReconciliation.BeginFreshAfterAborted,_=>ReceiptReconciliation.Indeterminate503};
}
public sealed record TransactionParticipant(string Collection,string Mode);
public sealed record TransactionSnapshot(string Name,IReadOnlyList<TransactionParticipant> Participants,bool CommitOnlyRetryOnSameSession);
public interface IScenarioPlanningTransaction{Task<T> ExecuteAsync<T>(TransactionSnapshot snapshot,Func<CancellationToken,Task<T>> action,CancellationToken cancellationToken);}
public interface IScenarioPlanningReceiptStore{Task<IdempotencyReceipt?> FindAsync(IdempotencyIdentity identity,CancellationToken cancellationToken);}
public interface IScenarioPlanningAuditIntentWriter{Task AppendAsync(ReadOnlyMemory<byte> canonicalAuditIntent,CancellationToken cancellationToken);}
public interface IScenarioPlanningOutboxWriter{Task AppendAsync(Guid messageId,ReadOnlyMemory<byte> canonicalPayload,CancellationToken cancellationToken);}
public interface IComparatorWorkSubmission{Task SubmitAcceptedAsync(ComparatorRunDefinition definition,CancellationToken cancellationToken);}
public interface IBudgetVersionReferenceProvider{Task<bool> ValidateAsync(Guid tenantId,BudgetVersionReferenceV1 reference,CancellationToken cancellationToken);}

public sealed record CreateScenarioCommand(Guid Id,string Code,string Name,string? Description);
public sealed record ArchiveScenarioCommand(Guid ScenarioId,int ExpectedVersion);
public sealed record CreateScenarioVersionDraftCommand(Guid DraftId,Guid ScenarioId,string VersionLabel,DateOnly EffectiveFrom,DateOnly EffectiveTo,string? Notes,Guid? BasedOnScenarioVersionId);
public sealed record UpdateScenarioVersionDraftCommand(Guid DraftId,int ExpectedVersion,IReadOnlyList<ScenarioAssumptionV1> Assumptions,IReadOnlyList<BudgetVersionReferenceV1> ForeignReferences);
public sealed record PublishScenarioVersionCommand(Guid DraftId,long NextScenarioVersionNumber);
public sealed record RequestComparatorRunCommand(Guid ComparatorRunId,IReadOnlyList<ComparatorInput> Inputs,IReadOnlyDictionary<string,string> Parameters);
public sealed record ReplaceScenarioSelectionCommand(long ExpectedRevision,InvestmentCaseContextV1 Context,ScenarioVersionReferenceV1 ScenarioVersionReference);
public sealed record CloseScenarioSelectionCommand(long ExpectedRevision,InvestmentCaseContextV1 Context);

public static class ScenarioPlanningValidators
{
    public static void Validate(ReferenceValidationRequest request)
    {if(request.TenantId==Guid.Empty)throw new ArgumentException("tenant-required");if(request.Mode==ReferenceValidationMode.CurrentSelectionEligibility&&request.Reference is not SelectedScenarioReferenceV1)throw new ArgumentException("current-mode-selected-wrapper-only");}
    public static void ValidateNoVersionCoercion(string contractVersion){if(contractVersion!="1.0")throw new NotSupportedException("unsupported-contract-version");}
}
