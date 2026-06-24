using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.Workflow;

namespace Diten.Platform.Domain.Entities.Workflow;

// MOD-0023 — central approval/workflow engine (platform shared service, tenant-scoped data).
// WorkflowTemplate is the logical approval workflow definition. Versioning, publish, and instance
// start are owned by later batches; Batch 01 persists the definition and its draft status only.
// TenantId is inherited from TenantScopedEntity and is always resolved server-side (never from the
// client payload).
public sealed class WorkflowTemplate : TenantScopedEntity
{
    // Tenant-unique business key for the definition (TenantId + TemplateCode unique, non-deleted).
    public required string TemplateCode { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public WorkflowTemplateStatus Status { get; set; } = WorkflowTemplateStatus.Draft;

    // Pointer to the active published version (set by the publish batch; null until first publish).
    public Guid? ActivePublishedVersionId { get; set; }

    // Batch 01 exposed CurrentVersionId. Keep it synchronized for existing consumers while Batch 02 uses
    // the explicit ActivePublishedVersionId name from the module contract.
    public Guid? CurrentVersionId { get; set; }
}
