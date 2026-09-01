using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Queries;

// MOD-0024 — read side. Queries are sealed records; handlers carry no Query suffix.

public sealed record GetTaskItemListQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<TaskItemListItemDto>>>;

public sealed record GetTaskItemByIdQuery(Guid Id, string CorrelationId)
    : IRequest<Response<TaskItemDetailDto>>;

/// <summary>
/// Positions a task may be pooled to (pack §12 K4). Returns the organization unit CODE and NAME alongside the
/// position, because <c>PositionDto</c> exposes only <c>OrganizationUnitId</c> — without the unit label a picker
/// cannot tell "QA Specialist — Facility A" from "QA Specialist — Facility B" and work lands in the wrong pool.
/// Draft/archived positions are excluded (<c>Position.Status</c> defaults to Draft, so an unfiltered list would
/// offer positions that are not real yet).
/// </summary>
public sealed record GetTaskAssignmentPositionLookupQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<AssignablePositionDto>>>;

/// <summary>
/// People a task may be assigned to (pack §12 K6.4). Assignability comes from holding a POSITION: the source is
/// the active <c>PositionAssignment</c> set, which keeps the list consistent with the organization context (K6)
/// and avoids exposing the whole employee directory. A person with no position is therefore absent — the UI must
/// say so plainly rather than render a silent empty list.
///
/// <para>Each row carries the position AND its organization unit, for the same reason the position lookup does:
/// two people holding "QA Specialist" in different facilities are otherwise indistinguishable.</para>
/// </summary>
/// <para>BL-057 — <paramref name="Purpose"/> selects which rule applies. An ASSIGNMENT list is limited to the
/// actor's company scope; a DECISION list (approver, reviewer) is exempt from it, because approval authority
/// belongs to the process rather than to the requester. One query and one handler on purpose: two handlers would
/// be two places to disagree about who holds a live position.</para>
public sealed record GetTaskAssignmentPersonLookupQuery(
    string CorrelationId,
    TaskPersonLookupPurpose Purpose = TaskPersonLookupPurpose.Assignment)
    : IRequest<Response<AssignablePersonLookupDto>>;

/// <summary>
/// BL-023 — is <paramref name="TargetUserId"/> ABOVE the caller in the reporting chain? The create form asks so
/// the submit button can say "Talep gönder" instead of "Oluştur"; the server answers from the same scope it uses
/// when it opens the request, so the label cannot drift from the behaviour.
/// </summary>
public sealed record GetTaskAssignmentDirectionQuery(Guid TargetUserId, string CorrelationId)
    : IRequest<Response<TaskAssignmentDirectionDto>>;

/// <summary>
/// Whether the assignment goes up. Counts and booleans only — WHO the caller's managers are is not answered
/// here; that would be a second, unguarded way to read the org chart.
/// </summary>
public sealed record TaskAssignmentDirectionDto(bool IsUpward);

/// <summary>
/// Templates a recurrence rule may be bound to (BL-052).
///
/// <para>A LOOKUP, not a management list: id + name is everything a picker needs. It exists because the rule
/// screen offers "generate each task from this template" and there was no way to enumerate them — the repository
/// could already list them, nothing exposed it, and a picker with no source is a control that can never be
/// filled. Active only: binding a rule to a retired template would generate work from a shape nobody maintains.</para>
/// </summary>
public sealed record GetTaskTemplateLookupQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<TaskTemplateLookupDto>>>;

/// <summary>
/// Every recurrence rule the tenant can see, ACTIVE OR NOT — a paused rule that vanished from the list could
/// never be resumed (Phase 4).
/// </summary>
public sealed record GetTaskRecurrenceRuleListQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<TaskRecurrenceRuleDto>>>;

public sealed record GetTaskRecurrenceRuleByIdQuery(Guid Id, string CorrelationId)
    : IRequest<Response<TaskRecurrenceRuleDto>>;

// ── BL-054: the template chain ───────────────────────────────────────────────

public sealed record GetChecklistTemplateListQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<ChecklistTemplateDto>>>;

public sealed record GetChecklistTemplateByIdQuery(Guid Id, string CorrelationId)
    : IRequest<Response<ChecklistTemplateDto>>;

/// <summary>
/// Checklist templates the TASK-TEMPLATE form's picker is filled from. Its own query rather than reusing the
/// list: a picker offers only what may still be bound (active, not retired), while the management list has to
/// show a paused template or it could never be switched back on.
/// </summary>
public sealed record GetChecklistTemplateLookupQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<ChecklistTemplateLookupDto>>>;

public sealed record GetTaskTemplateListQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<TaskTemplateDto>>>;

public sealed record GetTaskTemplateByIdQuery(Guid Id, string CorrelationId)
    : IRequest<Response<TaskTemplateDto>>;

/// <summary>
/// Every field definition the tenant can see, ACTIVE OR NOT — a retired definition must stay visible so the
/// values already stored under it keep an explanation, and so it can be switched back on.
/// </summary>
public sealed record GetTaskFieldDefinitionListQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<TaskFieldDefinitionDto>>>;

public sealed record GetTaskFieldDefinitionByIdQuery(Guid Id, string CorrelationId)
    : IRequest<Response<TaskFieldDefinitionDto>>;

/// <summary>
/// The option list ONE configurable field offers, resolved from that definition's own
/// <c>OptionsSourceKind</c>/<c>OptionsSourceKey</c>.
///
/// <para>The caller names a FIELD, never a lookup key or a reference set. That is the whole point of resolving
/// server-side: the definition is the allow-list, so a tenant cannot reach a data set merely by asking for it,
/// and the browser never has to know which of the three source kinds a field uses.</para>
///
/// <para><b>ONE query for all three kinds, and that is the point.</b> A platform lookup and a reference set are
/// short and fixed; another module's records are neither, so they are SEARCHED. Giving records their own query
/// would have produced two resolution paths, and the second path is where a source stops obeying the contract —
/// the WC-1 lesson. Instead the shape that a large source needs (a term and a cap) is on the ONE query, and the
/// short sources simply apply it to the list they already had.</para>
/// </summary>
/// <param name="Term">
/// What the user typed. Null or empty = the first page, so a picker opens with something in it.
/// </param>
/// <param name="Ids">
/// Identities already stored on a task, to be resolved back into records for the EDIT form. When present the
/// term is ignored: this is a hydration, not a search. Without it the round trip loses data — a value the first
/// page does not contain cannot be rendered, and a control that cannot render its value posts back a different
/// one.
/// </param>
public sealed record GetTaskFieldDefinitionOptionsQuery(
    string Code,
    string CorrelationId,
    string? Term = null,
    IReadOnlyList<string>? Ids = null,
    int? Take = null)
    : IRequest<Response<IReadOnlyList<TaskFieldOptionDto>>>;

/// <summary>
/// Which option sources an administrator may PICK when defining a field, for the kind they chose.
///
/// <para>It exists because the source key used to be free text, and a mistyped key produced a field that
/// silently vanished from the form: the resolver refused the unknown source and the renderer — correctly —
/// dropped the field. The protection was right; the typing was the defect. A key that can only be CHOSEN cannot
/// be mistyped.</para>
/// </summary>
public sealed record GetTaskFieldOptionSourcesQuery(
    Domain.Enums.Tasks.TaskFieldOptionsSourceKind Kind,
    string CorrelationId)
    : IRequest<Response<IReadOnlyList<TaskFieldOptionSourceDto>>>;


// ── DCP-005 slice 1: task types ─────────────────────────────────────────────

/// <summary>Every type, retired ones included — the management screen.</summary>
public sealed record GetTaskTypeListQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<TaskTypeDto>>>;

/// <summary>
/// Types a NEW task may be given. Read by anyone who can create a task — choosing a type is not an
/// administrative act.
/// </summary>
public sealed record GetActiveTaskTypesQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<TaskTypeDto>>>;

public sealed record GetTaskTypeByIdQuery(Guid Id, string CorrelationId)
    : IRequest<Response<TaskTypeDto>>;


// ── DCP-005 slice 2: the document reference list ────────────────────────────

public sealed record GetDocumentReferenceListVersionsQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<DocumentReferenceListVersionDto>>>;

/// <summary>
/// Search the CURRENT list. Blocked rows come back like any other — the caller shows them and refuses them.
/// </summary>
public sealed record SearchDocumentReferencesQuery(string? Term, int Limit, string CorrelationId)
    : IRequest<Response<IReadOnlyList<DocumentReferenceEntryDto>>>;

/// <summary>
/// The governing documents of ONE task type, resolved against the current register (DCP-005 §6.4).
///
/// <para>⚠ A SUGGESTION, never a requirement. The answer feeds a pre-ticked list the author may untick, and
/// adding a document the type never named is equally allowed — the type knows the usual answer, not the only
/// one.</para>
/// </summary>
public sealed record GetTaskTypeGoverningDocumentsQuery(
    Guid TaskTypeId, string? OrganizationCode, string CorrelationId)
    : IRequest<Response<TaskTypeGoverningDocumentsDto>>;
