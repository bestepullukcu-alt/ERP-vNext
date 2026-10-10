namespace Diten.SupplyChainService.Domain.Features.SandopPlans;
public sealed class SandopSignOff
{ public Guid Id {get;set;} public Guid PlanId {get;set;} public Guid SnapshotId {get;set;} public Guid TenantId {get;set;} public Guid LegalEntityId {get;set;} public string Role {get;set;}=""; public string Decision {get;set;}=""; public string? Comment {get;set;} public Guid DecidedBy {get;set;} public DateTimeOffset DecidedAt {get;set;} public bool IsDeleted {get;set;} public DateTimeOffset? DeletedAt {get;set;} }
