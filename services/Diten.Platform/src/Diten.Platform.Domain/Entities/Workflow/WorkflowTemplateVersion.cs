using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.Workflow;

namespace Diten.Platform.Domain.Entities.Workflow;

public sealed class WorkflowTemplateVersion : TenantScopedEntity
{
    public required Guid TemplateId { get; set; }
    public required int VersionNumber { get; set; }
    public required string DefinitionJson { get; set; }
    public required string SchemaVersion { get; set; }
    public required string ExpressionVersion { get; set; }
    public WorkflowTemplateVersionStatus Status { get; set; } = WorkflowTemplateVersionStatus.Draft;
    public bool IsImmutable { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? PublishedBy { get; set; }
    public string? PublishReason { get; set; }
    public string? ConcurrencyToken { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset? DeletedAt { get; set; }
}
