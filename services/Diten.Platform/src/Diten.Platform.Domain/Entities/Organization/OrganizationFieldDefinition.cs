using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.Organization;

/// <summary>
/// MOD-0288-FU02 — the closed set of types an Organization Unit custom field may carry.
///
/// <para>⚠ CLOSED AND EXHAUSTIVE AT v1 (pack §4). No free-form JSON, no expression, no executable/script
/// type, no arbitrary reference target. A ninth member is a follow-up pack, not an edit here: each type
/// carries validation, query and index consequences, and adding one silently is how a value nobody can
/// filter or validate reaches storage.</para>
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum OrganizationFieldDataType
{
    Text = 0,
    MultilineText = 1,
    Integer = 2,
    Decimal = 3,
    Boolean = 4,
    Date = 5,

    /// <summary>Options are declared on the definition's <see cref="OrganizationFieldConstraints.Options"/>.</summary>
    SingleSelect = 6,

    /// <summary>An identity of an Organization Unit or a Position in the SAME tenant. Nothing else.</summary>
    Reference = 7
}

/// <summary>What a <see cref="OrganizationFieldDataType.Reference"/> field may point at. Closed on purpose.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum OrganizationFieldReferenceTarget
{
    OrganizationUnit = 0,
    Position = 1
}

/// <summary>
/// Sensitivity of a field's values. Mirrors <c>TaskFieldClassification</c> deliberately rather than sharing it:
/// FU02 owns organization field semantics and must not create a compile dependency on the Tasks feature.
///
/// <para>⚠ PRESENT FROM v1 ON PURPOSE (§8 decision 4). The governance fields this mechanism exists for —
/// Regulatory role, Accountable executive, Evidence reference — are precisely the sensitive ones, and adding
/// classification later means retro-classifying every value already stored.</para>
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum OrganizationFieldClassification
{
    Normal = 0,
    Internal = 1,
    Confidential = 2,
    Restricted = 3
}

/// <summary>
/// Bounded, declarative constraints. Every member is data the server compares against — never an expression,
/// never a pattern the server executes. A regex was deliberately left out: it is the one "declarative"
/// constraint that is really a program, and an administrator-supplied one is a denial of service with a
/// friendly name.
/// </summary>
public sealed class OrganizationFieldConstraints
{
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }

    /// <summary>The allowed choices for <see cref="OrganizationFieldDataType.SingleSelect"/>.</summary>
    public IReadOnlyList<string>? Options { get; set; }

    /// <summary>Which aggregate a <see cref="OrganizationFieldDataType.Reference"/> field points at.</summary>
    public OrganizationFieldReferenceTarget? ReferenceTarget { get; set; }
}

/// <summary>
/// MOD-0288-FU02 — a tenant-defined Organization Unit field's DEFINITION. This is the mechanism that replaces
/// "add a column for every governance datum": Permanent OU ID, Regulatory role, Accountable executive and the
/// rest are ROWS here, never properties on <see cref="OrganizationUnit"/>.
///
/// <para>Adapted from the <c>TaskFieldDefinition</c> pattern; it shares no collection, no repository and no
/// code with it (pack §7, §10).</para>
/// </summary>
public sealed class OrganizationFieldDefinition : TenantScopedEntity
{
    /// <summary>Normalized, tenant-unique among non-deleted definitions, and IMMUTABLE after creation.</summary>
    public required string Code { get; set; }

    /// <summary>
    /// The administrator's own words, in the language they typed them in. Single-language on purpose: a tenant
    /// cannot add a line to our resx files, and inventing half a translation mechanism here is how a raw key
    /// reaches a screen.
    /// </summary>
    public required string Name { get; set; }

    public required OrganizationFieldDataType DataType { get; set; }

    public bool IsRequired { get; set; }

    /// <summary>Inactive definitions accept no new values; values already stored stay readable.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether this field may be named in a query filter.
    ///
    /// <para>⚠ NOT A PERFORMANCE GATE (§8 decision 5). One compound index on
    /// <c>(TenantId, DefinitionId, Value)</c> covers every field, so nothing here is technically unfilterable.
    /// It is a governed choice about which fields the tenant has decided are filter-worthy, and it is enforced
    /// SERVER-SIDE: a filter naming a non-queryable definition is a 400, never a silently narrowed result. A
    /// wrong result set looks exactly like a correct one, which is what makes silent omission the dangerous
    /// option.</para>
    /// </summary>
    public bool IsQueryable { get; set; }

    public OrganizationFieldConstraints? ValidationRules { get; set; }

    public int DisplayOrder { get; set; }

    /// <summary>Stamped onto every value this definition produces.</summary>
    public OrganizationFieldClassification Classification { get; set; } = OrganizationFieldClassification.Normal;

    public DateTimeOffset? DeletedAt { get; set; }
}
