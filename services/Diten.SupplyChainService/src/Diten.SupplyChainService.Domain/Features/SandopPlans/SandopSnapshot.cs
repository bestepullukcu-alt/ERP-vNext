namespace Diten.SupplyChainService.Domain.Features.SandopPlans;
public sealed record SandopInputReference(string Source,string ResourceId,string ResourceVersion);
public sealed record SandopProvenance(string DemandPlanId,string DemandPlanVersion,string SourceContract,string SourceContractVersion,DateTimeOffset SourceCapturedAt,string SourceChecksum);
public sealed class SandopSnapshot
{ public Guid Id {get;set;} public Guid PlanId {get;set;} public Guid TenantId {get;set;} public Guid LegalEntityId {get;set;} public int Sequence {get;set;} public SandopProvenance Provenance {get;set;}=null!; public IReadOnlyList<SandopInputReference> SupplyInputRefs {get;set;}=[]; public DateTimeOffset CapturedAt {get;set;} public bool IsDeleted {get;set;} public DateTimeOffset? DeletedAt {get;set;} }
