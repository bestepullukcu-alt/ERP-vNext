using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.TaskTypes;

/// <summary>
/// DCP-005 slice 1 — one task type as the create/edit form carries it.
///
/// <para>Shaped after <c>TaskFieldDefinitionEditViewModel</c>, the sibling this whole surface is modelled on.</para>
/// </summary>
public sealed class TaskTypeEditViewModel
{
    public Guid Id { get; set; }

    /// <summary>
    /// Tenant-unique and IMMUTABLE. The Edit screen renders it read-only; the server refuses a changed one
    /// rather than ignoring it, so the two ends agree instead of one of them being polite.
    /// </summary>
    [Required(ErrorMessage = "CodeRequired")]
    [StringLength(40, ErrorMessage = "CodeTooLong")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "NameRequired")]
    [StringLength(160, ErrorMessage = "NameTooLong")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    /// <summary>GXP_QUALITY_RECORD | OPERATIONAL_RECORD | NOT_A_RECORD — a CODE value, never translated.</summary>
    public string RecordClass { get; set; } = "NOT_A_RECORD";

    /// <summary>QMS|GMP|GDP|PV|RAF|NUT|CSV|RND, or empty. ONE value — never a list; see the enum's own note.</summary>
    public string? GqmsDomain { get; set; }

    /// <summary>
    /// One of the nineteen codes in DCP-005 §6.7, or empty. A picker rather than a text box now that the list
    /// exists; the server refuses anything outside it rather than storing null for it.
    /// </summary>
    public string? FunctionCode { get; set; }

    public bool IsQualityEvent { get; set; }

    /// <summary>
    /// Controlled-document UIDs governing this type everywhere. One per line in the textarea — the document
    /// PICKER is slice 2; until the lookup exists there is nothing to pick from.
    /// </summary>
    public string? GroupDocumentsText { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Group documents as the API carries them, for the round trip through Edit.</summary>
    public IReadOnlyList<string>? GroupDocuments { get; set; }
}

/// <summary>
/// The Platform envelope. Declared per feature namespace exactly as the sibling does — the shape is shared, the
/// type is not, so one module's response contract cannot silently change another's.
/// </summary>
public sealed class GatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string>? Errors { get; set; }

    /// <summary>
    /// The STABLE refusal code. Carried so the tenant surface can say the rule in the reader's language: the
    /// service's own message is English and stays English, because a service holding seven translations of a
    /// rule is a second place for the rule to live.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("reason_code")]
    public string? ReasonCode { get; set; }
}
