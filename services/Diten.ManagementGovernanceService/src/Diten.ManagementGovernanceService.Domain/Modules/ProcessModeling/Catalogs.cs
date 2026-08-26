using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Diten.ManagementGovernanceService.Domain.Modules.ProcessModeling;

public enum CatalogLifecycleState { Active, Archived }
public enum ProcessModelVersionState { Draft, Review, Published, Retired }

public static partial class ProcessModelingText
{
    [GeneratedRegex("^[A-Z0-9]+(?:-[A-Z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();

    public static string Code(string value)
    {
        var normalized = Required(value, 100).ToUpperInvariant().Replace('_', '-').Replace(' ', '-');
        if (!CodePattern().IsMatch(normalized)) throw new ArgumentException("Code must be an uppercase dash token.");
        return normalized;
    }

    public static string Required(string value, int max)
    {
        var normalized = value.Trim().Normalize(NormalizationForm.FormC);
        if (normalized.Length is 0 || normalized.Length > max) throw new ArgumentException("Text length is invalid.");
        return normalized;
    }

    public static string? Optional(string? value, int max)
    {
        if (value is null) return null;
        var normalized = value.Trim().Normalize(NormalizationForm.FormC);
        if (normalized.Length > max) throw new ArgumentException("Text length is invalid.");
        return normalized.Length == 0 ? null : normalized;
    }
}

public abstract class MutableCatalog : EntityBase
{
    protected MutableCatalog(Guid id, Guid tenantId, DateTime createdAtUtc, string name, string? description, int sortOrder, int descriptionMax = 2000)
        : base(id, tenantId, createdAtUtc)
    {
        Name = ProcessModelingText.Required(name, 200);
        Description = ProcessModelingText.Optional(description, descriptionMax);
        if (sortOrder < 0) throw new ArgumentOutOfRangeException(nameof(sortOrder));
        SortOrder = sortOrder;
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }
    public int SortOrder { get; private set; }
    public CatalogLifecycleState LifecycleState { get; private set; } = CatalogLifecycleState.Active;

    public void Update(string name, string? description, int sortOrder, int expectedVersion, DateTime nowUtc)
    {
        EnsureMutable(expectedVersion);
        if (sortOrder < 0) throw new ArgumentOutOfRangeException(nameof(sortOrder));
        Name = ProcessModelingText.Required(name, 200);
        Description = ProcessModelingText.Optional(description, 2000);
        SortOrder = sortOrder;
        Touch(nowUtc);
    }

    public void Archive(int expectedVersion, DateTime nowUtc)
    {
        EnsureMutable(expectedVersion);
        LifecycleState = CatalogLifecycleState.Archived;
        Touch(nowUtc);
    }

    private void EnsureMutable(int expectedVersion)
    {
        if (expectedVersion != Version) throw new InvalidOperationException("stale_concurrency");
        if (LifecycleState != CatalogLifecycleState.Active) throw new InvalidOperationException("catalog_archived");
    }
}

public sealed class ProcessArchitecture : MutableCatalog
{
    public ProcessArchitecture(Guid id, Guid tenantId, DateTime createdAtUtc, string architectureCode, string name, string? description, int sortOrder)
        : base(id, tenantId, createdAtUtc, name, description, sortOrder) => ArchitectureCode = ProcessModelingText.Code(architectureCode);
    public string ArchitectureCode { get; }
}

public sealed class ProcessDomain : MutableCatalog
{
    public ProcessDomain(Guid id, Guid tenantId, DateTime createdAtUtc, Guid processArchitectureId, string domainCode, string name, string? description, int sortOrder)
        : base(id, tenantId, createdAtUtc, name, description, sortOrder)
    { if (processArchitectureId == Guid.Empty) throw new ArgumentException(nameof(processArchitectureId)); ProcessArchitectureId = processArchitectureId; DomainCode = ProcessModelingText.Code(domainCode); }
    public Guid ProcessArchitectureId { get; }
    public string DomainCode { get; }
}

public sealed class ProcessFamily : MutableCatalog
{
    public ProcessFamily(Guid id, Guid tenantId, DateTime createdAtUtc, Guid processDomainId, string familyCode, string name, string? description, int sortOrder)
        : base(id, tenantId, createdAtUtc, name, description, sortOrder)
    { if (processDomainId == Guid.Empty) throw new ArgumentException(nameof(processDomainId)); ProcessDomainId = processDomainId; FamilyCode = ProcessModelingText.Code(familyCode); }
    public Guid ProcessDomainId { get; }
    public string FamilyCode { get; }
}

public sealed class ProcessDefinition : EntityBase
{
    public ProcessDefinition(Guid id, Guid tenantId, DateTime createdAtUtc, Guid processFamilyId, string processCode, string name, string? purpose, string? description)
        : base(id, tenantId, createdAtUtc)
    { if (processFamilyId == Guid.Empty) throw new ArgumentException(nameof(processFamilyId)); ProcessFamilyId = processFamilyId; ProcessCode = ProcessModelingText.Code(processCode); Name=ProcessModelingText.Required(name,200); Purpose = ProcessModelingText.Optional(purpose, 2000);Description=ProcessModelingText.Optional(description,4000); }
    public Guid ProcessFamilyId { get; }
    public string ProcessCode { get; }
    public string Name { get; private set; }
    public string? Purpose { get; private set; }
    public string? Description { get; private set; }
    public CatalogLifecycleState LifecycleState { get; private set; }=CatalogLifecycleState.Active;

    public void UpdateDefinition(string name, string? purpose, string? description, int expectedVersion, DateTime nowUtc)
    {
        EnsureMutable(expectedVersion);Name=ProcessModelingText.Required(name,200);Purpose=ProcessModelingText.Optional(purpose,2000);Description=ProcessModelingText.Optional(description,4000);Touch(nowUtc);
    }
    public void Archive(int expectedVersion,DateTime nowUtc){EnsureMutable(expectedVersion);LifecycleState=CatalogLifecycleState.Archived;Touch(nowUtc);}
    private void EnsureMutable(int expectedVersion){if(expectedVersion!=Version)throw new InvalidOperationException("stale_concurrency");if(LifecycleState!=CatalogLifecycleState.Active)throw new InvalidOperationException("catalog_archived");}
}
