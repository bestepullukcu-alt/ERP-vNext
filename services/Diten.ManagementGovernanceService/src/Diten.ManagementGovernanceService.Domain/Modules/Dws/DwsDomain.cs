using System.Collections;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Diten.ManagementGovernanceService.Domain.Modules.Dws;

public abstract class DwsTenantEntity
{
    private Guid _id = Guid.NewGuid(), _tenantId;
    private DateTime _createdAtUtc = DateTime.UtcNow;
    public Guid Id { get => _id; init => _id = value != Guid.Empty ? value : throw new DwsValidationException(DwsErrors.InvalidRequest); }
    public required Guid TenantId { get => _tenantId; init => _tenantId = value != Guid.Empty ? value : throw new DwsValidationException(DwsErrors.InvalidRequest); }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get => _createdAtUtc; init => _createdAtUtc = RequireUtc(value); }
    public DateTime? UpdatedAtUtc { get; private set; }
    public int Version { get; private set; } = 1;
    public void RequireVersion(int expected) { if (expected != Version) throw new DwsConflictException(DwsErrors.ConcurrencyConflict); }
    protected void Touch(DateTime utcNow) { UpdatedAtUtc = RequireUtc(utcNow); checked { Version++; } }
    public void SoftDelete(int expectedVersion, DateTime utcNow)
    {
        RequireVersion(expectedVersion);
        if (IsDeleted) throw new DwsNotFoundException();
        IsDeleted = true;
        DeletedAtUtc = RequireUtc(utcNow);
        Touch(utcNow);
    }
    public static DateTime RequireUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : throw new DwsValidationException(DwsErrors.InvalidRequest);
}

public enum ExternalContextKind { Portfolio, Initiative, Program, Project }
public sealed record ExternalContextReference
{
    public const string RequiredContractName = "ppm.external-context-reference", RequiredContractVersion = "1.0";
    public string ContractName { get; }
    public string ContractVersion { get; }
    public ExternalContextKind ContextKind { get; }
    public Guid ContextId { get; }
    public ExternalContextReference(string contractName, string contractVersion, ExternalContextKind contextKind, Guid contextId)
    {
        if (contractName != RequiredContractName || contractVersion != RequiredContractVersion || contextId == Guid.Empty) throw new DwsValidationException(DwsErrors.InvalidContextReference);
        ContractName = contractName; ContractVersion = contractVersion; ContextKind = contextKind; ContextId = contextId;
    }
}

public sealed record StructuralMetadata
{
    public string Name { get; }
    public string? Description { get; }
    public StructuralMetadata(string name, string? description) { Name = DwsText.Required(name, 200); Description = DwsText.Optional(description, 2000); }
}

public sealed class StructureDefinition : DwsTenantEntity
{
    public required ExternalContextReference ExternalContextReference { get; init; }
    public int? CurrentWorkingRevisionNumber { get; private set; } = 1;
    public int LatestRevisionNumber { get; private set; } = 1;
    public int AllocateNextRevision(int expectedVersion, DateTime utcNow) { RequireVersion(expectedVersion); if (CurrentWorkingRevisionNumber is not null) throw new DwsConflictException(DwsErrors.WorkingRevisionExists); CurrentWorkingRevisionNumber = checked(++LatestRevisionNumber); Touch(utcNow); return LatestRevisionNumber; }
    public void MarkSealed(int expectedVersion, DateTime utcNow) { RequireVersion(expectedVersion); CurrentWorkingRevisionNumber = null; Touch(utcNow); }
}

public sealed class StructureRevision : DwsTenantEntity
{
    private StructuralMetadata _structuralMetadata = null!;
    public required Guid StructureDefinitionId { get; init; }
    public required int RevisionNumber { get; init; }
    public required StructuralMetadata StructuralMetadata { get => _structuralMetadata; init => _structuralMetadata = value; }
    public bool IsSealed { get; private set; }
    public DateTime? SealedAtUtc { get; private set; }
    public void Seal(int expectedVersion, DateTime utcNow) { RequireVersion(expectedVersion); if (IsSealed) throw new DwsConflictException(DwsErrors.SealedRevisionImmutable); IsSealed = true; SealedAtUtc = RequireUtc(utcNow); Touch(utcNow); }
    public bool UpdateMetadata(StructuralMetadata metadata, int expectedVersion, DateTime utcNow)
    {
        RequireMutable(expectedVersion);
        if (StructuralMetadata == metadata) return false;
        _structuralMetadata = metadata;
        Touch(utcNow);
        return true;
    }
    public void RecordStructuralMutation(int expectedVersion, DateTime utcNow)
    {
        RequireMutable(expectedVersion);
        Touch(utcNow);
    }
    private void RequireMutable(int expectedVersion)
    {
        RequireVersion(expectedVersion);
        if (IsSealed) throw new DwsConflictException(DwsErrors.SealedRevisionImmutable);
    }
}

public sealed class StructureNode : DwsTenantEntity
{
    private Guid? _parentLogicalNodeId;
    private int _siblingOrder;
    private StructureNode() { }
    public Guid StructureRevisionId { get; private init; }
    public Guid LogicalNodeId { get; private init; }
    public Guid? ParentLogicalNodeId { get => _parentLogicalNodeId; private init => _parentLogicalNodeId = value; }
    public string Code { get; private init; } = null!;
    public string Title { get; private init; } = null!;
    public string? Description { get; private init; }
    public int SiblingOrder { get => _siblingOrder; private init => _siblingOrder = value; }
    public static StructureNode Create(Guid tenantId, Guid revisionId, Guid? parentId, string code, string title, string? description, int order, Guid? logicalNodeId = null)
    {
        var logical = logicalNodeId ?? Guid.NewGuid();
        if (tenantId == Guid.Empty || revisionId == Guid.Empty || logical == Guid.Empty || order < 0) throw new DwsValidationException(DwsErrors.InvalidStructure);
        if (parentId == logical) throw new DwsValidationException(DwsErrors.InvalidStructure);
        return new() { TenantId = tenantId, StructureRevisionId = revisionId, LogicalNodeId = logical, ParentLogicalNodeId = parentId, Code = DwsText.Required(code, 100), Title = DwsText.Required(title, 300), Description = DwsText.Optional(description, 4000), SiblingOrder = order };
    }
    public bool Move(Guid? parentLogicalNodeId, int siblingOrder, int expectedVersion, DateTime utcNow)
    {
        if (siblingOrder < 0 || parentLogicalNodeId == LogicalNodeId) throw new DwsValidationException(DwsErrors.InvalidStructure);
        RequireVersion(expectedVersion);
        if (ParentLogicalNodeId == parentLogicalNodeId && SiblingOrder == siblingOrder) return false;
        _parentLogicalNodeId = parentLogicalNodeId;
        _siblingOrder = siblingOrder;
        Touch(utcNow);
        return true;
    }
    public bool Reorder(int siblingOrder, int expectedVersion, DateTime utcNow)
    {
        if (siblingOrder < 0) throw new DwsValidationException(DwsErrors.InvalidStructure);
        RequireVersion(expectedVersion);
        if (SiblingOrder == siblingOrder) return false;
        _siblingOrder = siblingOrder;
        Touch(utcNow);
        return true;
    }
}

public sealed class StructuralDependency : DwsTenantEntity
{
    private StructuralDependency() { }
    public Guid StructureRevisionId { get; private init; }
    public Guid FromLogicalNodeId { get; private init; }
    public Guid ToLogicalNodeId { get; private init; }
    public static StructuralDependency Create(Guid tenantId, Guid revisionId, Guid from, Guid to, DateTime createdAtUtc)
    {
        if (tenantId == Guid.Empty || revisionId == Guid.Empty || from == Guid.Empty || to == Guid.Empty || from == to) throw new DwsValidationException(DwsErrors.InvalidStructure);
        return new() { TenantId = tenantId, StructureRevisionId = revisionId, FromLogicalNodeId = from, ToLogicalNodeId = to, CreatedAtUtc = createdAtUtc };
    }
}

public sealed class StructureBaseline : DwsTenantEntity
{
    public const string Algorithm = "SHA-256", CanonicalVersion = "dws.structural-baseline.v1";
    public required Guid StructureDefinitionId { get; init; }
    public required int SourceRevisionNumber { get; init; }
    public required int BaselineNumber { get; init; }
    public string HashAlgorithm { get; init; } = Algorithm;
    public string CanonicalizationVersion { get; init; } = CanonicalVersion;
    public required string ContentHash { get; init; }
    public required string Snapshot { get; init; }
}

public static class DwsStructuralValidator
{
    public static void ValidateHierarchy(Guid ownerTenantId, Guid ownerRevisionId, IReadOnlyCollection<StructureNode> nodes)
    {
        RequireOwner(ownerTenantId, ownerRevisionId);
        if (nodes.Any(x => x.TenantId != ownerTenantId || x.StructureRevisionId != ownerRevisionId)) throw new DwsNotFoundException();
        var byId = nodes.ToDictionary(x => x.LogicalNodeId);
        foreach (var node in nodes) { if (node.ParentLogicalNodeId == node.LogicalNodeId) throw new DwsValidationException(DwsErrors.InvalidStructure); if (node.ParentLogicalNodeId is Guid p && !byId.ContainsKey(p)) throw new DwsNotFoundException(); }
        if (nodes.GroupBy(x => (x.ParentLogicalNodeId, x.SiblingOrder)).Any(x => x.Count() > 1)) throw new DwsConflictException(DwsErrors.DuplicateSiblingOrder);
        if (nodes.GroupBy(x => x.Code, StringComparer.Ordinal).Any(x => x.Count() > 1)) throw new DwsConflictException(DwsErrors.DuplicateNodeCode);
        foreach (var node in nodes) { var active = new HashSet<Guid>(); var cursor = node; while (cursor.ParentLogicalNodeId is Guid p) { if (!active.Add(cursor.LogicalNodeId)) throw new DwsConflictException(DwsErrors.HierarchyCycle); cursor = byId[p]; } }
    }
    public static void ValidateDependencies(Guid ownerTenantId, Guid ownerRevisionId, IReadOnlyCollection<StructureNode> nodes, IReadOnlyCollection<StructuralDependency> dependencies)
    {
        RequireOwner(ownerTenantId, ownerRevisionId);
        if (nodes.Any(x => x.TenantId != ownerTenantId || x.StructureRevisionId != ownerRevisionId) || dependencies.Any(x => x.TenantId != ownerTenantId || x.StructureRevisionId != ownerRevisionId)) throw new DwsNotFoundException();
        var ids = nodes.Select(x => x.LogicalNodeId).ToHashSet();
        if (dependencies.Any(x => !ids.Contains(x.FromLogicalNodeId) || !ids.Contains(x.ToLogicalNodeId))) throw new DwsNotFoundException();
        if (dependencies.GroupBy(x => (x.FromLogicalNodeId, x.ToLogicalNodeId)).Any(x => x.Count() > 1)) throw new DwsConflictException(DwsErrors.DuplicateDependency);
        var outgoing = dependencies.GroupBy(x => x.FromLogicalNodeId).ToDictionary(x => x.Key, x => x.Select(y => y.ToLogicalNodeId).ToArray()); var done = new HashSet<Guid>();
        foreach (var id in ids) Visit(id, new HashSet<Guid>());
        void Visit(Guid id, HashSet<Guid> active) { if (done.Contains(id)) return; if (!active.Add(id)) throw new DwsConflictException(DwsErrors.DependencyCycle); if (outgoing.TryGetValue(id, out var next)) foreach (var target in next) Visit(target, active); active.Remove(id); done.Add(id); }
    }
    private static void RequireOwner(Guid tenantId, Guid revisionId) { if (tenantId == Guid.Empty || revisionId == Guid.Empty) throw new DwsValidationException(DwsErrors.InvalidStructure); }
}

public sealed record DwsCanonicalValue(byte[] Bytes, string Sha256) { public string Text => Encoding.UTF8.GetString(Bytes); }
public static class DwsCanonicalJson
{
    public const string RequestVersion = "dws.request-canonical-json.v1";
    public static DwsCanonicalValue Build(IReadOnlyDictionary<string, object?> projection, string version = RequestVersion)
    {
        if (version != RequestVersion) throw new DwsValidationException(DwsErrors.UnknownCanonicalizationVersion);
        using var stream = new MemoryStream(); using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping })) WriteObject(writer, projection);
        var bytes = stream.ToArray(); return new(bytes, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }
    private static void WriteObject(Utf8JsonWriter writer, IEnumerable<KeyValuePair<string, object?>> source)
    {
        var p = source.Select(x => new KeyValuePair<string, object?>(DwsText.Normalize(x.Key), x.Value)).ToArray(); if (p.GroupBy(x => x.Key, StringComparer.Ordinal).Any(x => x.Count() > 1)) throw new DwsValidationException(DwsErrors.InvalidRequest);
        writer.WriteStartObject(); foreach (var x in p.OrderBy(x => Encoding.UTF8.GetBytes(x.Key), ByteComparer.Instance)) { writer.WritePropertyName(x.Key); WriteValue(writer, x.Value); } writer.WriteEndObject();
    }
    private static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null: writer.WriteNullValue(); break; case string s: writer.WriteStringValue(DwsText.Normalize(s)); break; case Guid g: writer.WriteStringValue(g.ToString("D").ToLowerInvariant()); break; case bool b: writer.WriteBooleanValue(b); break; case int n: writer.WriteNumberValue(n); break; case long n: writer.WriteNumberValue(n); break; case ExternalContextKind k: writer.WriteStringValue(k.ToString().ToLowerInvariant()); break;
            case IReadOnlyDictionary<string, object?> d: WriteObject(writer, d); break; case IDictionary d: WriteObject(writer, d.Cast<DictionaryEntry>().Select(x => new KeyValuePair<string, object?>((string)x.Key, x.Value))); break;
            case IEnumerable e when value is not string: writer.WriteStartArray(); foreach (var item in e) WriteValue(writer, item); writer.WriteEndArray(); break; default: throw new DwsValidationException(DwsErrors.InvalidRequest);
        }
    }
    private sealed class ByteComparer : IComparer<byte[]> { public static readonly ByteComparer Instance = new(); public int Compare(byte[]? a, byte[]? b) { if (a is null) return b is null ? 0 : -1; if (b is null) return 1; for (var i = 0; i < Math.Min(a.Length, b.Length); i++) if (a[i] != b[i]) return a[i].CompareTo(b[i]); return a.Length.CompareTo(b.Length); } }
}

public static class DwsBaselineBuilder
{
    public static StructureBaseline Build(Guid tenantId, Guid definitionId, int revisionNumber, int baselineNumber, ExternalContextReference context, StructuralMetadata metadata, IReadOnlyCollection<StructureNode> nodes, IReadOnlyCollection<StructuralDependency> dependencies, DateTime createdAtUtc)
    {
        if (tenantId == Guid.Empty || definitionId == Guid.Empty || revisionNumber <= 0 || baselineNumber <= 0 || nodes.Count == 0) throw new DwsValidationException(DwsErrors.InvalidStructure);
        DwsTenantEntity.RequireUtc(createdAtUtc);
        var revisionIds=nodes.Select(x=>x.StructureRevisionId).Distinct().ToArray();if(revisionIds.Length!=1)throw new DwsNotFoundException();var ownerRevisionId=revisionIds[0];
        DwsStructuralValidator.ValidateHierarchy(tenantId, ownerRevisionId, nodes);
        DwsStructuralValidator.ValidateDependencies(tenantId, ownerRevisionId, nodes, dependencies);
        var ns = nodes.OrderBy(x => x.ParentLogicalNodeId.HasValue).ThenBy(x => x.ParentLogicalNodeId?.ToString("D"), StringComparer.Ordinal).ThenBy(x => x.SiblingOrder).ThenBy(x => x.LogicalNodeId.ToString("D"), StringComparer.Ordinal);
        var ds = dependencies.OrderBy(x => x.FromLogicalNodeId.ToString("D"), StringComparer.Ordinal).ThenBy(x => x.ToLogicalNodeId.ToString("D"), StringComparer.Ordinal);
        var projection = new Dictionary<string, object?> { ["canonicalizationVersion"] = StructureBaseline.CanonicalVersion, ["dependencies"] = ds.Select(x => new Dictionary<string, object?> { ["fromLogicalNodeId"] = x.FromLogicalNodeId, ["toLogicalNodeId"] = x.ToLogicalNodeId }).ToArray(), ["externalContext"] = new Dictionary<string, object?> { ["contextId"] = context.ContextId, ["contextKind"] = context.ContextKind, ["contractName"] = context.ContractName, ["contractVersion"] = context.ContractVersion }, ["nodes"] = ns.Select(x => new Dictionary<string, object?> { ["code"] = x.Code, ["description"] = x.Description, ["logicalNodeId"] = x.LogicalNodeId, ["parentLogicalNodeId"] = x.ParentLogicalNodeId, ["siblingOrder"] = x.SiblingOrder, ["title"] = x.Title }).ToArray(), ["revisionMetadata"] = new Dictionary<string, object?> { ["description"] = metadata.Description, ["name"] = metadata.Name } };
        var c = DwsCanonicalJson.Build(projection); return new() { TenantId = tenantId, CreatedAtUtc = createdAtUtc, StructureDefinitionId = definitionId, SourceRevisionNumber = revisionNumber, BaselineNumber = baselineNumber, ContentHash = c.Sha256, Snapshot = c.Text };
    }
}

public sealed record DwsNodeDifference(Guid LogicalNodeId, string Kind);
public sealed record DwsDependencyDifference(Guid FromLogicalNodeId, Guid ToLogicalNodeId, string Kind);
public sealed record StructureComparisonResult(IReadOnlyList<DwsNodeDifference> Nodes, IReadOnlyList<DwsDependencyDifference> Dependencies);
public static class DwsComparison
{
    public static StructureComparisonResult Compare(IReadOnlyCollection<StructureNode> left, IReadOnlyCollection<StructureNode> right, IReadOnlyCollection<StructuralDependency> ld, IReadOnlyCollection<StructuralDependency> rd)
    {
        var l = left.ToDictionary(x => x.LogicalNodeId); var r = right.ToDictionary(x => x.LogicalNodeId); var changes = new List<DwsNodeDifference>();
        foreach (var id in l.Keys.Union(r.Keys).OrderBy(x => x)) { if (!l.ContainsKey(id)) changes.Add(new(id, "added")); else if (!r.ContainsKey(id)) changes.Add(new(id, "removed")); else if (l[id].ParentLogicalNodeId != r[id].ParentLogicalNodeId) changes.Add(new(id, "moved")); else if (l[id].SiblingOrder != r[id].SiblingOrder) changes.Add(new(id, "reordered")); else if (l[id].Code != r[id].Code || l[id].Title != r[id].Title || l[id].Description != r[id].Description) changes.Add(new(id, "metadata-changed")); }
        var le = ld.Select(x => (x.FromLogicalNodeId, x.ToLogicalNodeId)).ToHashSet(); var re = rd.Select(x => (x.FromLogicalNodeId, x.ToLogicalNodeId)).ToHashSet(); var de = le.Except(re).Select(x => new DwsDependencyDifference(x.FromLogicalNodeId, x.ToLogicalNodeId, "removed")).Concat(re.Except(le).Select(x => new DwsDependencyDifference(x.FromLogicalNodeId, x.ToLogicalNodeId, "added"))).OrderBy(x => x.FromLogicalNodeId).ThenBy(x => x.ToLogicalNodeId).ToArray();
        return new(changes, de);
    }
}

public static class DwsText
{
    public static string Normalize(string value)
    {
        value ??= string.Empty;
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsHighSurrogate(value[i])) { if (i + 1 >= value.Length || !char.IsLowSurrogate(value[++i])) throw new DwsValidationException(DwsErrors.InvalidUnicode); }
            else if (char.IsLowSurrogate(value[i])) throw new DwsValidationException(DwsErrors.InvalidUnicode);
        }
        try { return value.Normalize(NormalizationForm.FormC); } catch (ArgumentException) { throw new DwsValidationException(DwsErrors.InvalidUnicode); }
    }
    public static string Required(string value, int max) { var n = Normalize((value??string.Empty).Trim()); if (n.Length is 0 || n.Length > max) throw new DwsValidationException(DwsErrors.InvalidRequest); return n; }
    public static string? Optional(string? value, int max) { if (value is null) return null; var n = Normalize(value.Trim()); if (n.Length > max) throw new DwsValidationException(DwsErrors.InvalidRequest); return n.Length == 0 ? null : n; }
}

public static class DwsErrors
{
    public const string InvalidRequest="dws_invalid_request",InvalidContextReference="dws_invalid_context_reference",InvalidUnicode="dws_invalid_unicode",InvalidStructure="dws_invalid_structure",UnsupportedContractVersion="dws_unsupported_contract_version",UnknownCanonicalizationVersion="dws_unknown_canonicalization_version",InvalidStableOutcome="dws_invalid_stable_outcome",AuthenticationRequired="dws_authentication_required",ConcurrencyConflict="dws_concurrency_conflict",HierarchyCycle="dws_hierarchy_cycle",DependencyCycle="dws_dependency_cycle",DuplicateDependency="dws_duplicate_dependency",DuplicateSiblingOrder="dws_duplicate_sibling_order",DuplicateNodeCode="dws_duplicate_node_code",NodeHasChildren="dws_node_has_children",SealedRevisionImmutable="dws_sealed_revision_immutable",WorkingRevisionExists="dws_working_revision_exists",ExternalContextImmutable="dws_external_context_immutable",ExternalContextConflict="dws_external_context_conflict",IdempotencyConflict="dws_idempotency_conflict",IdempotencySubjectConflict="idempotency_key_owned_by_different_subject",ComparisonRequiresSealedRevision="dws_comparison_requires_sealed_revision",ResourceNotFound="dws_resource_not_found",PermissionDenied="dws_permission_denied",AuthorizationAuthorityUnavailable="dws_authorization_authority_unavailable",ExternalContextAuthorityUnavailable="dws_external_context_authority_unavailable",TransactionUnavailable="dws_transaction_unavailable",CommitIndeterminate="dws_commit_indeterminate",AuditIntentUnavailable="dws_audit_intent_unavailable";
    public static IReadOnlyDictionary<int,IReadOnlySet<string>> Matrix { get; }=new Dictionary<int,IReadOnlySet<string>>{[400]=new HashSet<string>([InvalidRequest,InvalidContextReference,InvalidUnicode,InvalidStructure,UnsupportedContractVersion,UnknownCanonicalizationVersion,InvalidStableOutcome]),[401]=new HashSet<string>([AuthenticationRequired]),[403]=new HashSet<string>([PermissionDenied]),[404]=new HashSet<string>([ResourceNotFound]),[409]=new HashSet<string>([ConcurrencyConflict,HierarchyCycle,DependencyCycle,DuplicateDependency,DuplicateSiblingOrder,DuplicateNodeCode,NodeHasChildren,SealedRevisionImmutable,WorkingRevisionExists,ExternalContextImmutable,ExternalContextConflict,IdempotencyConflict,IdempotencySubjectConflict,ComparisonRequiresSealedRevision]),[503]=new HashSet<string>([AuthorizationAuthorityUnavailable,ExternalContextAuthorityUnavailable,TransactionUnavailable,CommitIndeterminate,AuditIntentUnavailable])};
}
public static class DwsTenantBoundary { public static T RequireVisible<T>(Guid tenant,T? entity) where T:DwsTenantEntity { if(tenant==Guid.Empty||entity is null||entity.IsDeleted||entity.TenantId!=tenant)throw new DwsNotFoundException(); return entity; } }
public sealed class DwsValidationException(string code):Exception(code){public string Code{get;}=code;} public sealed class DwsConflictException(string code):Exception(code){public string Code{get;}=code;} public sealed class DwsNotFoundException():Exception(DwsErrors.ResourceNotFound){public string Code{get;}=DwsErrors.ResourceNotFound;}
