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

    /// <summary>
    /// WHAT COUNTS AS AN ENDING for work of this type. Empty is a legitimate, common state — it means this type
    /// asks nothing when a task is closed, which is how every type behaved before the dictionary existed.
    /// </summary>
    /// <summary>
    /// Nullable ONLY because a JSON null overwrites a property initialiser rather than leaving it — the API's own
    /// field is nullable, so a response that omits it would land null here. <c>LoadApiModelAsync</c> normalises it
    /// straight back to empty; nothing downstream should ever see the null.
    /// </summary>
    public List<TaskTypeClosureOutcomeViewModel>? ClosureOutcomes { get; set; } = [];

    /// <summary>
    /// ⚠ THE GUARD FOR THE NULL/EMPTY CONTRACT, and it is the whole reason this property exists.
    ///
    /// <para>The API reads <c>closureOutcomes: null</c> as "not asking, leave the stored dictionary alone" and
    /// <c>[]</c> as "clear it". That asymmetry was put there deliberately because THIS form did not draw the
    /// field: without it, every save from this screen would have deleted a dictionary configured through the
    /// API.</para>
    ///
    /// <para>The moment the form draws the section, the protection has to move here — a posted form with no rows
    /// is genuinely ambiguous between "I removed them all" and "this page never rendered them". So the section
    /// posts a hidden marker: PRESENT means the browser rendered the editor and the list below is the truth
    /// (empty included); ABSENT means it did not, and the controller sends null.</para>
    ///
    /// <para>It is the same defect shape as the <c>GroupDocumentsText</c> rehydration bug found live on
    /// 2026-08-26 — a full-replace save, and a read path that did not hand the form what it had to preserve.
    /// That one cost a type its governing documents with a 302 and a success message.</para>
    /// </summary>
    public bool ClosureOutcomesSubmitted { get; set; }

    /// <summary>
    /// The system catalogue, loaded for the picker. Populated by the controller on GET, never posted back — the
    /// codes and resource keys are code-owned and a client that sent its own would be inventing vocabulary.
    /// </summary>
    public IReadOnlyList<TaskTypeClosureOutcomeViewModel>? SystemOutcomes { get; set; }
}

/// <summary>
/// One closure outcome as the editor carries it.
///
/// <para><b>Two label sources, exactly one set</b> — the split <c>TaskFieldDefinition</c> established and the
/// entity repeats. A SYSTEM outcome carries <see cref="LabelResourceKey"/> and is translated in all seven
/// languages; a TENANT outcome carries <see cref="LabelText"/>, in the one language its author typed it in.
/// The server refuses both at once, so the form must never post both.</para>
/// </summary>
public sealed class TaskTypeClosureOutcomeViewModel
{
    /// <summary>Unique within the type; upper-cased by the server. Stored on every task closed this way.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Set for a SYSTEM outcome. The screen renders the translation and refuses to edit it.</summary>
    public string? LabelResourceKey { get; set; }

    /// <summary>Set for a TENANT outcome — the administrator's own words.</summary>
    public string? LabelText { get; set; }

    /// <summary>"Completed" | "Cancelled" — a CODE value that travels, never a translated word.</summary>
    public string Disposition { get; set; } = "Completed";

    /// <summary>
    /// ⭐ PER ROW, never a switch above the list. "Rejected" asks why and "Approved" does not; one global flag
    /// would force a sentence onto the outcomes that do not need one, and that field then fills with "ok".
    /// </summary>
    public bool RequiresReason { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>
/// What <c>_ClosureOutcomeRow.cshtml</c> needs to draw ONE row: the outcome, the key its inputs bind under, and
/// the catalogue its picker offers.
///
/// <para>Declared so the row markup exists in exactly one file. _Form renders it per stored row AND once inside a
/// <c>&lt;template&gt;</c> with the placeholder key, so the row an administrator edits and the row the Add button
/// clones cannot drift — which is how a cloned row loses a field name and silently stops binding.</para>
/// </summary>
public sealed class TaskTypeClosureOutcomeRow
{
    public required string RowKey { get; init; }
    public required TaskTypeClosureOutcomeViewModel Outcome { get; init; }
    public required IReadOnlyList<TaskTypeClosureOutcomeViewModel> SystemOutcomes { get; init; }
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
