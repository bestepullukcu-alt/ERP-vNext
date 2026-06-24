namespace Diten.Platform.API.Models.DocumentManagement;

/// <summary>MOD-0028-FU02 dry-run import request. No client TenantId is ever accepted.</summary>
public sealed class QmsBaselineDryRunRequest
{
    public string FileName { get; set; } = string.Empty;
    public string Format { get; set; } = "xlsx";
    public string ContentBase64 { get; set; } = string.Empty;
    public string SourceBaselineKey { get; set; } = string.Empty;
}

/// <summary>MOD-0028-FU02 commit import request.</summary>
public sealed class QmsBaselineCommitRequest
{
    public string FileName { get; set; } = string.Empty;
    public string Format { get; set; } = "xlsx";
    public string ContentBase64 { get; set; } = string.Empty;
    public string SourceBaselineKey { get; set; } = string.Empty;
    public string BaselineVersion { get; set; } = string.Empty;
    public string? ChangeSummary { get; set; }
}

/// <summary>MOD-0028-FU02 publish request. Optional optimistic-concurrency token (0 = unchecked).</summary>
public sealed class QmsBaselinePublishRequest
{
    public int ExpectedVersion { get; set; }
}

/// <summary>MOD-0028-FU04 manual DRAFT baseline creation request. No client TenantId is ever accepted.</summary>
public sealed class ManualQmsBaselineRequest
{
    public string BaselineVersion { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? ChangeSummary { get; set; }
    public DateTimeOffset? EffectiveDate { get; set; }
}

/// <summary>MOD-0028-FU04 manual CollectionDefinition create/update request.</summary>
public sealed class QmsCollectionDefinitionUpsertRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ParentCanonicalId { get; set; }
    public string? PurposeScope { get; set; }
    public string? RequiredByScope { get; set; }
    public string? AllowedDocClass { get; set; }
    public string? DefaultClassificationLevel { get; set; }
    public string? DefaultRetentionHint { get; set; }
    public int DisplayOrder { get; set; }
    public bool AllowsManualChildren { get; set; } = true;
    public bool TemplatesAllowed { get; set; }
    public bool IsMandatory { get; set; }
    public bool IsProtected { get; set; }
    public int VersionToken { get; set; }
}

/// <summary>MOD-0028-FU04 manual move/reorder request.</summary>
public sealed class QmsCollectionDefinitionMoveRequest
{
    public string? ParentCanonicalId { get; set; }
    public int DisplayOrder { get; set; }
    public int VersionToken { get; set; }
}
