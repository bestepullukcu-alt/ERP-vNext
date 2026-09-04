using System.Text.Json;

namespace Diten.ManagementGovernanceService.Domain.Modules.ProcessModeling.Approval;

public sealed record PublishActorProvenanceV1
{
    public const string CurrentVersion = "1.0";

    public PublishActorProvenanceV1(
        Guid tenantId,
        Guid processModelId,
        Guid processModelVersionId,
        string contentHash,
        Guid modelAuthorActorId,
        Guid publishRequesterActorId,
        DateTime capturedAtUtc,
        string provenanceVersion = CurrentVersion)
    {
        TenantId = Required(tenantId, nameof(tenantId));
        ProcessModelId = Required(processModelId, nameof(processModelId));
        ProcessModelVersionId = Required(processModelVersionId, nameof(processModelVersionId));
        ContentHash = ValidateContentHash(contentHash);
        ModelAuthorActorId = Required(modelAuthorActorId, nameof(modelAuthorActorId));
        PublishRequesterActorId = Required(publishRequesterActorId, nameof(publishRequesterActorId));
        CapturedAtUtc = RequiredUtc(capturedAtUtc, nameof(capturedAtUtc));
        if (!string.Equals(provenanceVersion, CurrentVersion, StringComparison.Ordinal))
            throw new ArgumentException("unsupported_provenance_version", nameof(provenanceVersion));
        ProvenanceVersion = provenanceVersion;
    }

    public Guid TenantId { get; }
    public Guid ProcessModelId { get; }
    public Guid ProcessModelVersionId { get; }
    public string ContentHash { get; }
    public Guid ModelAuthorActorId { get; }
    public Guid PublishRequesterActorId { get; }
    public DateTime CapturedAtUtc { get; }
    public string ProvenanceVersion { get; }

    public PublishApprovalPolicyRequestV1 BindPolicyRequest(Guid publisherActorId) => new(
        TenantId,
        ProcessModelId,
        ProcessModelVersionId,
        ContentHash,
        Required(publisherActorId, nameof(publisherActorId)),
        PublishRequesterActorId,
        ModelAuthorActorId);

    internal static Guid Required(Guid value, string name) =>
        value == Guid.Empty ? throw new ArgumentException("empty_identity", name) : value;

    internal static DateTime RequiredUtc(DateTime value, string name) =>
        value.Kind != DateTimeKind.Utc ? throw new ArgumentException("utc_required", name) : value;

    internal static string ValidateContentHash(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length != 71 || !value.StartsWith("sha256:", StringComparison.Ordinal) ||
            value.AsSpan(7).ContainsAnyExcept("0123456789abcdef"))
            throw new ArgumentException("invalid_content_hash", nameof(value));
        return value;
    }
}

public sealed record PublishApprovalPolicyRequestV1
{
    public PublishApprovalPolicyRequestV1(
        Guid tenantId,
        Guid modelId,
        Guid versionId,
        string contentHash,
        Guid publisherActorId,
        Guid requesterActorId,
        Guid authorActorId)
    {
        TenantId = PublishActorProvenanceV1.Required(tenantId, nameof(tenantId));
        ModelId = PublishActorProvenanceV1.Required(modelId, nameof(modelId));
        VersionId = PublishActorProvenanceV1.Required(versionId, nameof(versionId));
        ContentHash = PublishActorProvenanceV1.ValidateContentHash(contentHash);
        PublisherActorId = PublishActorProvenanceV1.Required(publisherActorId, nameof(publisherActorId));
        RequesterActorId = PublishActorProvenanceV1.Required(requesterActorId, nameof(requesterActorId));
        AuthorActorId = PublishActorProvenanceV1.Required(authorActorId, nameof(authorActorId));
    }

    public Guid TenantId { get; }
    public Guid ModelId { get; }
    public Guid VersionId { get; }
    public string ContentHash { get; }
    public Guid PublisherActorId { get; }
    public Guid RequesterActorId { get; }
    public Guid AuthorActorId { get; }
}

public enum PublishApprovalAuthorityState
{
    Available,
    Unavailable,
    Malformed,
    Indeterminate
}

public enum PublishApprovalRequirement
{
    Required,
    NotRequired
}

public sealed record PublishApprovalPolicyDecisionV1
{
    public PublishApprovalPolicyDecisionV1(
        PublishApprovalAuthorityState authorityState,
        PublishApprovalRequirement? requirement,
        long policyVersion,
        DateTime observedAtUtc,
        DateTime validUntilUtc)
    {
        if (!Enum.IsDefined(authorityState)) throw new ArgumentOutOfRangeException(nameof(authorityState));
        if (authorityState == PublishApprovalAuthorityState.Available != requirement.HasValue)
            throw new ArgumentException("requirement_state_mismatch", nameof(requirement));
        if (requirement.HasValue && !Enum.IsDefined(requirement.Value))
            throw new ArgumentOutOfRangeException(nameof(requirement));
        if (policyVersion < 1) throw new ArgumentOutOfRangeException(nameof(policyVersion));
        ObservedAtUtc = PublishActorProvenanceV1.RequiredUtc(observedAtUtc, nameof(observedAtUtc));
        ValidUntilUtc = PublishActorProvenanceV1.RequiredUtc(validUntilUtc, nameof(validUntilUtc));
        if (validUntilUtc <= observedAtUtc) throw new ArgumentException("invalid_policy_interval", nameof(validUntilUtc));
        AuthorityState = authorityState;
        Requirement = requirement;
        PolicyVersion = policyVersion;
    }

    public PublishApprovalAuthorityState AuthorityState { get; }
    public PublishApprovalRequirement? Requirement { get; }
    public long PolicyVersion { get; }
    public DateTime ObservedAtUtc { get; }
    public DateTime ValidUntilUtc { get; }

    public bool IsFreshAt(DateTime nowUtc) =>
        PublishActorProvenanceV1.RequiredUtc(nowUtc, nameof(nowUtc)) >= ObservedAtUtc && nowUtc < ValidUntilUtc;
}

public sealed record ApprovalOutcomeReferenceV1
{
    public const string ExpectedContractName = "platform.approval-outcome-reference";
    public const string ExpectedContractVersion = "1.0";

    public ApprovalOutcomeReferenceV1(string contractName, string contractVersion, Guid approvalOutcomeId)
    {
        if (!string.Equals(contractName, ExpectedContractName, StringComparison.Ordinal))
            throw new ArgumentException("unsupported_contract_name", nameof(contractName));
        if (!string.Equals(contractVersion, ExpectedContractVersion, StringComparison.Ordinal))
            throw new ArgumentException("unsupported_contract_version", nameof(contractVersion));
        ContractName = contractName;
        ContractVersion = contractVersion;
        ApprovalOutcomeId = PublishActorProvenanceV1.Required(approvalOutcomeId, nameof(approvalOutcomeId));
    }

    public string ContractName { get; }
    public string ContractVersion { get; }
    public Guid ApprovalOutcomeId { get; }

    public static ApprovalOutcomeReferenceV1 ParseExact(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow
        });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new FormatException("approval_reference_object_required");

        string? name = null;
        string? version = null;
        string? id = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!seen.Add(property.Name)) throw new FormatException("duplicate_approval_reference_field");
            if (property.Value.ValueKind != JsonValueKind.String) throw new FormatException("approval_reference_string_required");
            switch (property.Name)
            {
                case nameof(ContractName): name = property.Value.GetString(); break;
                case nameof(ContractVersion): version = property.Value.GetString(); break;
                case nameof(ApprovalOutcomeId): id = property.Value.GetString(); break;
                default: throw new FormatException("unknown_approval_reference_field");
            }
        }
        if (seen.Count != 3 || name is null || version is null || id is null)
            throw new FormatException("missing_approval_reference_field");
        if (!Guid.TryParseExact(id, "D", out var parsed) || !string.Equals(id, parsed.ToString("D"), StringComparison.Ordinal))
            throw new FormatException("noncanonical_approval_outcome_id");
        return new(name, version, parsed);
    }

    public string ToExactJson() =>
        $"{{\"ContractName\":\"{ContractName}\",\"ContractVersion\":\"{ContractVersion}\",\"ApprovalOutcomeId\":\"{ApprovalOutcomeId:D}\"}}";
}
