using System.Text.Json.Serialization;

namespace Diten.Platform.API.Models.BusinessReferenceData;

public sealed class CreateBusinessReferenceDataSetRequest
{
    [JsonPropertyName("set_code")]
    public required string SetCode { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("scope_type")]
    public required string ScopeType { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }
}

public sealed class PatchBusinessReferenceDataSetRequest
{
    [JsonPropertyName("row_version")]
    public long RowVersion { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("set_code")]
    public string? SetCode { get; set; }

    [JsonPropertyName("scope_type")]
    public string? ScopeType { get; set; }
}

public sealed class CreateBusinessReferenceDataVersionRequest
{
    [JsonPropertyName("source_version_id")]
    public Guid? SourceVersionId { get; set; }
}

public sealed class SubmitBusinessReferenceDataVersionRequest
{
    [JsonPropertyName("evidence_ref")]
    public string? EvidenceRef { get; set; }

    [JsonPropertyName("evidence_link_id")]
    public string? EvidenceLinkId { get; set; }

    [JsonPropertyName("document_version_id")]
    public string? DocumentVersionId { get; set; }

    [JsonPropertyName("requirement_code")]
    public string? RequirementCode { get; set; }

    [JsonPropertyName("override_action")]
    public bool OverrideAction { get; set; }

    [JsonPropertyName("override_reason")]
    public string? OverrideReason { get; set; }

    [JsonPropertyName("expected_concurrency_token")]
    public string? ExpectedConcurrencyToken { get; set; }
}

public sealed class ApproveBusinessReferenceDataVersionRequest
{
    [JsonPropertyName("decision")]
    public string Decision { get; set; } = "approve";

    [JsonPropertyName("rejection_reason")]
    public string? RejectionReason { get; set; }

    [JsonPropertyName("evidence_ref")]
    public string? EvidenceRef { get; set; }

    [JsonPropertyName("evidence_link_id")]
    public string? EvidenceLinkId { get; set; }

    [JsonPropertyName("document_version_id")]
    public string? DocumentVersionId { get; set; }

    [JsonPropertyName("requirement_code")]
    public string? RequirementCode { get; set; }

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    [JsonPropertyName("target_step")]
    public string? TargetStep { get; set; }

    [JsonPropertyName("override_action")]
    public bool OverrideAction { get; set; }

    [JsonPropertyName("override_reason")]
    public string? OverrideReason { get; set; }

    [JsonPropertyName("expected_concurrency_token")]
    public string? ExpectedConcurrencyToken { get; set; }
}

public sealed class PublishBusinessReferenceDataVersionRequest
{
    [JsonPropertyName("publish_mode")]
    public string PublishMode { get; set; } = "Immediate";

    [JsonPropertyName("publish_at")]
    public DateTimeOffset? PublishAt { get; set; }

    [JsonPropertyName("expected_concurrency_token")]
    public string? ExpectedConcurrencyToken { get; set; }

    [JsonPropertyName("override_action")]
    public bool OverrideAction { get; set; }

    [JsonPropertyName("override_reason")]
    public string? OverrideReason { get; set; }
}

public sealed class RegisterBusinessReferenceDataUsageRequest
{
    [JsonPropertyName("set_code")]
    public required string SetCode { get; set; }

    [JsonPropertyName("consumer_module")]
    public required string ConsumerModule { get; set; }

    [JsonPropertyName("consumer_name")]
    public required string ConsumerName { get; set; }

    [JsonPropertyName("consumer_endpoint")]
    public string? ConsumerEndpoint { get; set; }

    [JsonPropertyName("scope_type")]
    public string? ScopeType { get; set; }

    [JsonPropertyName("scope_key")]
    public string? ScopeKey { get; set; }

    [JsonPropertyName("version_pin")]
    public int? VersionPin { get; set; }

    [JsonPropertyName("as_of_date")]
    public DateTimeOffset? AsOfDate { get; set; }

    [JsonPropertyName("resolution_mode")]
    public string? ResolutionMode { get; set; }

    [JsonPropertyName("criticality")]
    public string? Criticality { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

public sealed class PreviewBusinessReferenceDataImportRequest
{
    [JsonPropertyName("target_draft_version_id")]
    public Guid TargetDraftVersionId { get; set; }

    [JsonPropertyName("file_name")]
    public required string FileName { get; set; }

    [JsonPropertyName("format")]
    public required string Format { get; set; }

    [JsonPropertyName("content_base64")]
    public required string ContentBase64 { get; set; }
}

public sealed class BusinessReferenceDataVersionValueRequest
{
    [JsonPropertyName("code")]
    public required string Code { get; set; }

    [JsonPropertyName("label")]
    public required string Label { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("sort_order")]
    public int SortOrder { get; set; }

    [JsonPropertyName("parent_value_code")]
    public string? ParentValueCode { get; set; }

    [JsonPropertyName("attributes")]
    public Dictionary<string, string>? Attributes { get; set; }
}

public sealed class ReplaceBusinessReferenceDataVersionValuesRequest
{
    [JsonPropertyName("expected_concurrency_token")]
    public string? ExpectedConcurrencyToken { get; set; }

    [JsonPropertyName("values")]
    public required List<BusinessReferenceDataVersionValueRequest> Values { get; set; }
}

public sealed class BusinessReferenceDataAttributeDefinitionRequest
{
    [JsonPropertyName("attribute_code")]
    public required string AttributeCode { get; set; }

    [JsonPropertyName("display_name")]
    public required string DisplayName { get; set; }

    [JsonPropertyName("data_type")]
    public string DataType { get; set; } = "string";

    [JsonPropertyName("is_required")]
    public bool IsRequired { get; set; }
}

public sealed class ReplaceBusinessReferenceDataVersionAttributeDefinitionsRequest
{
    [JsonPropertyName("expected_concurrency_token")]
    public string? ExpectedConcurrencyToken { get; set; }

    [JsonPropertyName("definitions")]
    public required List<BusinessReferenceDataAttributeDefinitionRequest> Definitions { get; set; }
}

public sealed class BusinessReferenceDataMappingRequest
{
    [JsonPropertyName("mapping_key")]
    public required string MappingKey { get; set; }

    [JsonPropertyName("source_value_code")]
    public required string SourceValueCode { get; set; }

    [JsonPropertyName("target_code")]
    public required string TargetCode { get; set; }

    [JsonPropertyName("target_label")]
    public string? TargetLabel { get; set; }
}

public sealed class ReplaceBusinessReferenceDataVersionMappingsRequest
{
    [JsonPropertyName("expected_concurrency_token")]
    public string? ExpectedConcurrencyToken { get; set; }

    [JsonPropertyName("mappings")]
    public required List<BusinessReferenceDataMappingRequest> Mappings { get; set; }
}

public sealed class ProvisionBusinessReferenceDataEvidenceFixtureRequest
{
    [JsonPropertyName("fixture_code")]
    public required string FixtureCode { get; set; }

    [JsonPropertyName("confirm_fixture_owned")]
    public bool ConfirmFixtureOwned { get; set; }

    [JsonPropertyName("set_code")]
    public string? SetCode { get; set; }

    [JsonPropertyName("set_name")]
    public string? SetName { get; set; }

    [JsonPropertyName("requirement_code")]
    public string? RequirementCode { get; set; }

    [JsonPropertyName("value_code")]
    public string? ValueCode { get; set; }

    [JsonPropertyName("value_label")]
    public string? ValueLabel { get; set; }
}

public sealed class RetireBusinessReferenceDataEvidenceFixtureSetRequest
{
    [JsonPropertyName("fixture_code")]
    public required string FixtureCode { get; set; }

    [JsonPropertyName("confirm_fixture_owned")]
    public bool ConfirmFixtureOwned { get; set; }

    [JsonPropertyName("expected_row_version")]
    public long? ExpectedRowVersion { get; set; }
}
