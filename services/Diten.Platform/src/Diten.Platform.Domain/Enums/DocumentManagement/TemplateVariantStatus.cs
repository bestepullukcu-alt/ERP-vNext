namespace Diten.Platform.Domain.Enums.DocumentManagement;

/// <summary>MOD-0029-FU03 — lifecycle status of a tenant-scoped template variant governance record.</summary>
public enum TemplateVariantStatus
{
    Draft = 0,
    Active = 1,
    Deprecated = 2,
    Archived = 3
}
