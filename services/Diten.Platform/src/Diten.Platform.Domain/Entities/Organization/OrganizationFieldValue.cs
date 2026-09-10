using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.Organization;

/// <summary>
/// MOD-0288-FU02 — one Organization Unit's value for one field definition.
///
/// <para>⚠ ITS OWN DOCUMENT IN ITS OWN COLLECTION, AND THAT IS A DELIBERATE DEPARTURE FROM THE PRECEDENT
/// (§8 decision 5). <c>TaskFieldValue</c> is an array embedded inside <c>task_items</c>. Here each value is a
/// document, so it carries its own <c>Version</c> for compare-and-set and its own soft delete, and the unit
/// document does not grow by one field for every governance datum the tenant defines.</para>
///
/// <para>The consequence for querying is that there is <b>no <c>$elemMatch</c></b> — a plain compound index on
/// <c>(TenantId, DefinitionId, Value)</c> serves the filters, and fifty definitions still cost one index.</para>
/// </summary>
public sealed class OrganizationFieldValue : TenantScopedEntity
{
    public required Guid OrganizationUnitId { get; set; }

    public required Guid DefinitionId { get; set; }

    /// <summary>
    /// Copied from the definition at write time so a stored value can be interpreted without a second read,
    /// and so a later definition change cannot silently re-interpret what is already stored.
    /// </summary>
    public required OrganizationFieldDataType ValueType { get; set; }

    /// <summary>
    /// The CANONICAL string encoding of the value; <see cref="ValueType"/> governs interpretation.
    ///
    /// <para>⚠ ONE FIELD, MIXED KINDS — this is the real technical caveat of the shared index (§8 decision 5).
    /// Equality, <c>in</c> and prefix-anchored contains are safe because they compare the canonical form.
    /// Range operators and sorting are NOT: across mixed types Mongo compares BSON type order, not values. So
    /// range and sort are refused unless the query names exactly one definition with an explicit type.</para>
    /// </summary>
    public string? Value { get; set; }

    /// <summary>Stamped from the definition. Storing it without enforcing it on read would be decoration.</summary>
    public OrganizationFieldClassification Classification { get; set; } = OrganizationFieldClassification.Normal;

    public DateTimeOffset? DeletedAt { get; set; }
}
