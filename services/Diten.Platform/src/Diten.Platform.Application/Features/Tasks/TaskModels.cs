using Diten.Platform.Domain.Enums.Tasks;

namespace Diten.Platform.Application.Features.Tasks;

// MOD-0024 — ALL task DTOs live in this single models file (live Platform convention). Permission CONSTANTS are
// declared here only; the seed/grant belongs to MOD-0018 / Diten.AuthService and is a separate task. The keys are
// attributed to Module="tasks" + Scope=Tenant through the MODULE MANIFEST (see TaskManifestProvider): a key first
// created by the A1 reflection worker would be stamped Module="platform" + Scope=PlatformAdmin, which AuthService
// cannot downgrade. The startup ordering gate makes the manifest win.

public static class TaskPermissions
{
    public const string Read = "platform.tasks.read";
    public const string Create = "platform.tasks.create";
    public const string Update = "platform.tasks.update";
    public const string Delete = "platform.tasks.delete";
    public const string BulkDelete = "platform.tasks.bulk-delete";
    public const string Assign = "platform.tasks.assign";
    public const string Claim = "platform.tasks.claim";
    public const string Complete = "platform.tasks.complete";
    public const string Cancel = "platform.tasks.cancel";
    public const string FieldDefinitionsManage = "platform.tasks.field-definitions.manage";

    /// <summary>
    /// Who may define TASK TYPES (DCP-005 slice 1).
    ///
    /// <para><b>Separate from creating tasks, and the control statement depends on it.</b> QA's commitment is
    /// that "a manually created, unclassified task may not produce a GxP quality record" — which holds only
    /// because a person who can open a task cannot also mint the type that classifies it. Reading types stays
    /// open to anyone who can create a task; they have to be able to choose one.</para>
    /// </summary>
    public const string TaskTypesManage = "platform.tasks.task-types.manage";

    /// <summary>
    /// Who may IMPORT the controlled-document reference list (DCP-005 slice 2).
    ///
    /// <para>Separate from managing task types, and separate again from reading the list: importing replaces
    /// what the whole tenant can cite, while SEARCHING it is something anyone linking a document has to do.</para>
    /// </summary>
    public const string DocumentListImport = "platform.tasks.document-list.import";

    /// <summary>
    /// Who may READ the controlled-document reference list.
    ///
    /// <para><b>⚠ ITS OWN PERMISSION, and a guard is why.</b> The screen was first opened to
    /// <see cref="Read"/> so a QA reader could see the register without being able to replace it — and
    /// <c>TaskManifestProviderTests</c> refused it: <c>Read</c> is in
    /// <see cref="PersonalWorkSurfaceScoped"/>, so a nav-VISIBLE page behind it would have become a second
    /// answer to "where is my work" and fragmented the Task Center.</para>
    ///
    /// <para>The register is not personal work — it is reference data about the organisation's procedures. It
    /// gets a permission that says so, and the guard keeps holding for everything that really is personal.</para>
    /// </summary>
    public const string DocumentListRead = "platform.tasks.document-list.read";

    /// <summary>
    /// Managing WHEN work gets created — the recurrence rules (BL-052).
    ///
    /// <para>Its own key rather than <see cref="Create"/>, and the reason is the same one that keeps
    /// <see cref="FieldDefinitionsManage"/> separate: defining a schedule is a configuration authority, not the
    /// act of doing or receiving work. The rule endpoints used to sit on Read/Create/Update/Delete, all of which
    /// are <see cref="PersonalWorkSurfaceScoped"/> — so a menu entry for the screen would have been a second
    /// answer to "where is my work". Nobody could reach those endpoints from a screen before this slice, so
    /// there is no caller to strand by moving them.</para>
    /// </summary>
    public const string RecurrenceManage = "platform.tasks.recurrence-rules.manage";

    /// <summary>
    /// Who may define the reusable CHECKLIST shapes a template or a task is instantiated from (BL-054).
    ///
    /// <para>Its own key, for the same reason <see cref="RecurrenceManage"/> has one: writing the steps a whole
    /// tenant's work will be measured against is a configuration authority, not the act of doing the work. It is
    /// deliberately outside <see cref="PersonalWorkSurfaceScoped"/>, so the screen behind it may appear in the
    /// menu without becoming a second answer to "where is my work".</para>
    /// </summary>
    public const string ChecklistTemplatesManage = "platform.tasks.checklist-templates.manage";

    /// <summary>
    /// Who may define the reusable TASK shapes — title, priority, due offset, checklist (BL-054).
    ///
    /// <para><b>Separate from <see cref="ChecklistTemplatesManage"/> even though the two screens ship together.</b>
    /// A task template says what a piece of work looks like; a checklist template says what must be TICKED before
    /// it can be called done, and in a regulated tenant those are not the same authority — the second is the one
    /// QA cares about. One key for both would mean granting the ability to rewrite every gate in order to let
    /// somebody set a default due date.</para>
    /// </summary>
    public const string TemplatesManage = "platform.tasks.templates.manage";

    /// <summary>
    /// The permissions that gate a <b>personal work surface</b> — a page that shows or acts on the viewer's own
    /// task INSTANCES.
    ///
    /// <para><b>Why this set exists, and why it is a set rather than a list of page codes.</b> Görev Merkezi is
    /// the single answer to "where is my work", so no second sidebar entry may answer it too — that is the rule
    /// <c>TaskManifestProviderTests</c> enforces. Expressing it as "these four page codes" would mean the fifth
    /// personal page, added a year from now, silently lands on the permissive side of a rule written to catch
    /// exactly it.</para>
    ///
    /// <para>The permission is the honest discriminator because it already states what authority the page needs:
    /// "may I read/claim/complete a task" is a work question, and a page asking it is a work surface. Managing the
    /// field SCHEMA is a different authority entirely, which is why
    /// <see cref="FieldDefinitionsManage"/> is deliberately absent — that screen configures the catalogue rather
    /// than showing anybody their work, so it does not fragment the Task Center and may appear in the menu.</para>
    /// </summary>
    public static readonly IReadOnlySet<string> PersonalWorkSurfaceScoped = new HashSet<string>(StringComparer.Ordinal)
    {
        Read, Create, Update, Delete, BulkDelete, Assign, Claim, Complete, Cancel
    };
}

public static class TaskReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFound = "TASK_NOT_FOUND";
    public const string AssignmentTargetInvalid = "ASSIGNMENT_TARGET_INVALID";
    public const string PositionNotAssignable = "POSITION_NOT_ASSIGNABLE";
    public const string AssigneeInvalid = "ASSIGNEE_INVALID";
    public const string OrganizationUnitUnresolved = "ORGANIZATION_UNIT_UNRESOLVED";
    public const string AlreadyClaimed = "TASK_ALREADY_CLAIMED";
    public const string AlreadyAccepted = "TASK_ALREADY_ACCEPTED";
    public const string NotClaimable = "TASK_NOT_CLAIMABLE";
    public const string InvalidState = "TASK_INVALID_STATE";
    public const string ConcurrencyConflict = "TASK_CONCURRENCY_CONFLICT";
    public const string SpentHoursNotSettable = "SPENT_HOURS_NOT_SETTABLE";
    public const string FieldDefinitionUnknown = "TASK_FIELD_DEFINITION_UNKNOWN";

    /// <summary>
    /// BL-024 Phase 2 — the caller may not see or write this configurable field.
    ///
    /// <para>Its OWN code, separate from the generic validation failure: this is a refusal of AUTHORITY, not a
    /// malformed payload. A client told "validation failed" will helpfully retry with a corrected value forever;
    /// a client told this knows the value is not theirs to send and stops.</para>
    /// </summary>
    public const string FieldAccessDenied = "TASK_FIELD_ACCESS_DENIED";
    public const string FieldValueInvalid = "TASK_FIELD_VALUE_INVALID";
    public const string FieldLimitExceeded = "TASK_FIELD_LIMIT_EXCEEDED";
    public const string ChecklistIncomplete = "CHECKLIST_INCOMPLETE";

    /// <summary>
    /// A template-owned checklist item was asked to change its own text, or to leave the list.
    ///
    /// <para>Its OWN code rather than a validation failure, for the same reason as
    /// <see cref="FieldAccessDenied"/>: nothing about the payload is malformed, so a client told "validation
    /// failed" will keep correcting and resending text that was never going to be accepted. This says the item
    /// is not the caller's to word — the template decides that, for every task instantiated from it.</para>
    /// </summary>
    public const string ChecklistItemTemplateOwned = "CHECKLIST_ITEM_TEMPLATE_OWNED";

    /// <summary>
    /// Somebody else put this step on the list, so it is not this caller's to reword, re-level, re-flag or
    /// remove. Ticking it stays open to everyone — that is the work.
    ///
    /// <para>Its OWN code, for the same reason as <see cref="FieldAccessDenied"/> and
    /// <see cref="ChecklistItemTemplateOwned"/>: nothing about the payload is wrong, so a client told
    /// "validation failed" will keep correcting and resending a request that was never going to be accepted.
    /// This says the item is not yours, which is a different answer and a final one.</para>
    /// </summary>
    public const string ChecklistItemNotAuthor = "CHECKLIST_ITEM_NOT_AUTHOR";

    /// <summary>Entering Waiting without saying what is being waited for.</summary>
    public const string WaitingReasonRequired = "TASK_WAITING_REASON_REQUIRED";

    /// <summary>Handing work back or on without saying why. Both are statements to another person.</summary>
    public const string HandoverReasonRequired = "TASK_HANDOVER_REASON_REQUIRED";

    /// <summary>Only the current holder may return work they were given.</summary>
    public const string ReturnNotAssignee = "TASK_RETURN_NOT_ASSIGNEE";

    /// <summary>Reassigning is the holder's or the requester's to do — nobody else's.</summary>
    public const string ReassignNotPermitted = "TASK_REASSIGN_NOT_PERMITTED";

    /// <summary>
    /// This task was marked as NOT delegable when it was created, so it may not be handed to anybody.
    ///
    /// <para>Its OWN code, distinct from <see cref="ReassignNotPermitted"/>: that one says "not YOU", this one
    /// says "not AT ALL". A caller told the wrong one would go looking for a permission that would never help.</para>
    /// </summary>
    public const string DelegationNotAllowed = "TASK_DELEGATION_NOT_ALLOWED";

    /// <summary>The proposed assignee is not someone work may be assigned to.</summary>
    public const string AssigneeNotAssignable = "TASK_ASSIGNEE_NOT_ASSIGNABLE";

    /// <summary>
    /// Cancelling someone else's task. A REFUSAL OF AUTHORITY, not a state conflict — an assignee who does not
    /// want the work returns it; only the requester (or administrative authority) calls it off.
    /// </summary>
    public const string CancelNotRequester = "TASK_CANCEL_NOT_REQUESTER";

    /// <summary>The workflow gate refused: approval is outstanding (MOD-0023 owns the decision).</summary>
    public const string ApprovalPending = "APPROVAL_PENDING";

    /// <summary>
    /// The workflow gate refused completion: a REVIEW is outstanding. Separate from
    /// <see cref="ApprovalPending"/> because the two gates block different acts and are cleared by different
    /// people — telling a holder "approval pending" when a reviewer is holding the work would send them to the
    /// wrong person.
    /// </summary>
    public const string ReviewPending = "REVIEW_PENDING";

    /// <summary>Review was submitted on a task that never asked for one.</summary>
    public const string ReviewNotRequired = "REVIEW_NOT_REQUIRED";

    /// <summary>The recurrence rule does not exist, or belongs to another tenant.</summary>
    public const string RecurrenceRuleNotFound = "RECURRENCE_RULE_NOT_FOUND";

    /// <summary>The checklist template does not exist, or belongs to another tenant.</summary>
    public const string ChecklistTemplateNotFound = "CHECKLIST_TEMPLATE_NOT_FOUND";

    /// <summary>
    /// Another checklist template already uses this code. Codes are how a template is named in an import, an
    /// export and a support conversation, so two carrying one code make every such reference ambiguous.
    /// </summary>
    public const string ChecklistTemplateCodeTaken = "CHECKLIST_TEMPLATE_CODE_TAKEN";

    /// <summary>
    /// A checklist template was saved with no items. Refused: a gate with nothing in it is not a lighter gate,
    /// it is a template that instantiates an empty list onto every task bound to it and explains nothing.
    /// </summary>
    public const string ChecklistTemplateEmpty = "CHECKLIST_TEMPLATE_EMPTY";

    /// <summary>Two items in one checklist template carry the same code — the key a run item is matched by.</summary>
    public const string ChecklistItemCodeDuplicate = "CHECKLIST_ITEM_CODE_DUPLICATE";

    /// <summary>Another task template already uses this code.</summary>
    public const string TaskTemplateCodeTaken = "TASK_TEMPLATE_CODE_TAKEN";

    /// <summary>
    /// A template's code was edited. Immutable for the same reason a field definition's is: it is how the
    /// template is referred to from outside the database, and moving it silently re-points those references.
    /// </summary>
    public const string TemplateCodeImmutable = "TEMPLATE_CODE_IMMUTABLE";

    /// <summary>
    /// The template names a checklist template that does not exist, is retired, or belongs to another tenant.
    ///
    /// <para>Refused at the WRITE rather than at generation. A template bound to a checklist that cannot resolve
    /// produces tasks with no gates at all, silently — which is the same class of defect as the recurrence rule
    /// that generated work assigned to nobody: it looks configured and does the opposite of what it says.</para>
    /// </summary>
    public const string TemplateChecklistUnresolved = "TEMPLATE_CHECKLIST_UNRESOLVED";

    /// <summary>
    /// The template's default assignment does not hold together — a pool with no position, or a person named on
    /// a pool default. Checked against the SAME shared rule task creation uses.
    /// </summary>
    public const string TemplateAssignmentInvalid = "TEMPLATE_ASSIGNMENT_INVALID";

    /// <summary>The field definition does not exist, or belongs to another tenant.</summary>
    public const string FieldDefinitionNotFound = "FIELD_DEFINITION_NOT_FOUND";

    /// <summary>Another definition already uses this code. Codes are the join key for stored values.</summary>
    public const string FieldDefinitionCodeTaken = "FIELD_DEFINITION_CODE_TAKEN";

    /// <summary>
    /// Neither label source was given, or both were. Exactly one — a resource key for a system field, text for a
    /// tenant field — because the projection has to know which contract label form to emit.
    /// </summary>
    public const string FieldLabelSourceInvalid = "FIELD_LABEL_SOURCE_INVALID";

    /// <summary>
    /// The definition would take the tenant past the contract's six-section cap. Refused at the write, because
    /// past it the surface DROPS the whole item rather than trimming a section.
    /// </summary>
    public const string FieldSectionLimitExceeded = "FIELD_SECTION_LIMIT_EXCEEDED";

    /// <summary>A definition's code was edited. It is the join key for every value already stored under it.</summary>
    public const string FieldCodeImmutable = "FIELD_CODE_IMMUTABLE";

    /// <summary>
    /// A field's option list could not be produced: it declares no source, or the lookup key / reference set it
    /// names does not resolve. REPORTED rather than answered with an empty list — an empty list and an
    /// unresolvable source look identical to a client, and the difference decides whether the field is offered
    /// at all.
    /// </summary>
    public const string FieldOptionsUnresolved = "FIELD_OPTIONS_UNRESOLVED";

    /// <summary>
    /// The DEFINITION itself names an option source that cannot work: a module record source no module has
    /// registered, or a record source on a value type that cannot hold an identity.
    ///
    /// <para>Refused at the WRITE, deliberately. The reader already drops a field whose source will not resolve,
    /// and that protection is correct — but a field the administrator saved and then never saw again is a defect
    /// they cannot diagnose. Saying no at the moment of typing costs one message; saying nothing costs a support
    /// call.</para>
    /// </summary>
    public const string FieldOptionSourceInvalid = "FIELD_OPTION_SOURCE_INVALID";

    /// <summary>
    /// A bulk retire named more definitions than one request may carry. REFUSED rather than truncated: silently
    /// processing the first N and reporting success is the "5 deleted" lie in another form.
    /// </summary>
    public const string BulkLimitExceeded = "BULK_LIMIT_EXCEEDED";

    /// <summary>A recurrence rule was defined with no repeat — a schedule that never fires.</summary>
    public const string RecurrenceFrequencyRequired = "RECURRENCE_FREQUENCY_REQUIRED";

    /// <summary>The rule ends before it starts, so it can never produce an occurrence.</summary>
    public const string RecurrenceWindowInvalid = "RECURRENCE_WINDOW_INVALID";

    /// <summary>
    /// A review was requested with nobody to route it to. MOD-0023 refuses to start an instance with an empty
    /// candidate list, so this is caught at the WRITE rather than left to surface as a review that will not start.
    /// </summary>
    public const string ReviewerRequired = "REVIEW_REVIEWER_REQUIRED";

    /// <summary>
    /// The review could not be OPENED — deliberately distinct from <see cref="ReviewPending"/>, which means a
    /// reviewer is holding the work. Nothing is waiting here; the handoff failed and the caller can retry. Telling
    /// someone "waiting for the reviewer" when no reviewer was ever asked sends them to wait on nobody.
    /// </summary>
    public const string ReviewStartFailed = "REVIEW_START_FAILED";

    /// <summary>A subtask may not itself have subtasks — one level only (pack §12 E2).</summary>
    public const string SubtaskDepthExceeded = "SUBTASK_DEPTH_EXCEEDED";

    /// <summary>The parent task does not exist, or belongs to another tenant.</summary>
    public const string ParentTaskNotFound = "PARENT_TASK_NOT_FOUND";

    /// <summary>The referenced checklist or task template is missing/inactive.</summary>
    public const string TemplateNotFound = "TASK_TEMPLATE_NOT_FOUND";

    /// <summary>The checklist item code does not exist on this task's run.</summary>
    public const string ChecklistItemNotFound = "CHECKLIST_ITEM_NOT_FOUND";
    public const string DependencyInvalid = "TASK_DEPENDENCY_INVALID";

    /// <summary>The other end of the edge does not exist, or belongs to another tenant.</summary>
    public const string DependencyTaskNotFound = "TASK_DEPENDENCY_TASK_NOT_FOUND";

    /// <summary>A task cannot depend on itself — a cycle of length one.</summary>
    public const string DependencySelf = "TASK_DEPENDENCY_SELF";

    /// <summary>The edge would close a cycle (A→B→A): the work could then never start.</summary>
    public const string DependencyCycle = "TASK_DEPENDENCY_CYCLE";

    /// <summary>That edge already exists; adding it twice would double every blocker sentence.</summary>
    public const string DependencyDuplicate = "TASK_DEPENDENCY_DUPLICATE";

    /// <summary>
    /// A predecessor has not reached the state its edge waits for, so this transition is refused.
    ///
    /// <para>Deliberately the SAME string as <c>WorkAggregationReasonCodes.DependencyBlocked</c>: the projection
    /// disabling the button and the handler refusing the write are one fact seen from two sides, and the client
    /// should not need two entries in its message map to say one thing. TaskDependencyTests asserts they are
    /// equal, so a rename on either side is caught rather than silently producing an unmapped code.</para>
    /// </summary>
    public const string DependencyBlocked = "DEPENDENCY_BLOCKED";

    /// <summary>
    /// A subtask is still open, so its parent cannot be completed (BL-035).
    ///
    /// <para>Same string as <c>WorkAggregationReasonCodes.SubtaskBlocked</c>, for the same reason DependencyBlocked
    /// is: the greyed button and this refusal are one fact seen from two sides, and a second spelling would need a
    /// second entry in the client's message map — the one nobody adds is the one that reaches a user raw.</para>
    /// </summary>
    public const string SubtaskBlocked = "SUBTASK_BLOCKED";

    /// <summary>
    /// The task is closed, so nothing more can be said on it. The composer is already hidden for a terminal task,
    /// but hiding a control is presentation and refusing the write is the rule — three separate gaps of exactly
    /// this shape have been closed in this module already.
    /// </summary>
    public const string CommentTaskClosed = "TASK_COMMENT_TASK_CLOSED";

    /// <summary>Empty, whitespace-only, or longer than <see cref="TaskCommentLimits.MaxTextLength"/>.</summary>
    public const string CommentTextInvalid = "TASK_COMMENT_TEXT_INVALID";

    /// <summary>
    /// Somebody else's comment. The author is the only person who may rewrite or withdraw their own words —
    /// there is deliberately no administrator override (owner decision 2026-08-14): an authority over other
    /// people's sentences is much easier to grant than to withdraw.
    /// </summary>
    public const string CommentNotAuthor = "TASK_COMMENT_NOT_AUTHOR";

    /// <summary>Already withdrawn. A tombstone has no text to rewrite and nothing left to take back.</summary>
    public const string CommentWithdrawn = "TASK_COMMENT_WITHDRAWN";

    /// <summary>
    /// No planned date was supplied — including a JSON body that omits the field, which deserializes
    /// <see cref="PlanTaskItemRequest.PlannedDate"/> to its zero value rather than throwing. Deliberately the
    /// ONLY thing this endpoint refuses: a date in the past, or one after the source due date, is a real personal
    /// plan and is accepted (see the handler for why).
    /// </summary>
    public const string PlanDateRequired = "TASK_PLAN_DATE_REQUIRED";

    /// <summary>Empty, whitespace-only, or longer than <see cref="TaskPersonalNoteLimits.MaxTextLength"/>.</summary>
    public const string PersonalNoteTextInvalid = "TASK_PERSONAL_NOTE_TEXT_INVALID";

    /// <summary>
    /// The note id names nothing in THIS reader's overlay. Deliberately the same answer whether the note belongs
    /// to somebody else or does not exist at all: distinguishing them would confirm that another person's note
    /// exists, which is precisely what the read rule refuses to tell.
    /// </summary>
    public const string PersonalNoteNotFound = "TASK_PERSONAL_NOTE_NOT_FOUND";

    /// <summary>
    /// The snooze date is in the past (or absent when one was required). A snooze until yesterday hides nothing
    /// and would leave the reader believing they had parked the work.
    /// </summary>
    public const string SnoozeDateInvalid = "TASK_SNOOZE_DATE_INVALID";

    // ── DCP-005 slice 1 — task types ─────────────────────────────────────
    /*
     * ⚠ STABLE CODES, NOT SENTENCES. The handler's message is English and always will be: a service has no
     * business holding seven translations of a rule. The code is what crosses the wire, and the tenant surface
     * turns it into the reader's language — the same bridge the password rules use, and for the same reason
     * (a raw English sentence in a Turkish form is a defect this repository has already paid for).
     */
    public const string TaskTypeCodeImmutable = "TASK_TYPE_CODE_IMMUTABLE";
    public const string TaskTypeCodeTaken = "TASK_TYPE_CODE_TAKEN";
    public const string TaskTypeClassificationInvalid = "TASK_TYPE_CLASSIFICATION_INVALID";
    public const string TaskTypeFunctionCodeInvalid = "TASK_TYPE_FUNCTION_CODE_INVALID";

    // ── DCP-005 slice 2 — the document reference list ────────────────────
    public const string DocumentListInvalid = "DOCUMENT_LIST_INVALID";
    public const string DocumentListAlreadyImported = "DOCUMENT_LIST_ALREADY_IMPORTED";
    public const string DocumentListWithdrawReasonRequired = "DOCUMENT_LIST_WITHDRAW_REASON_REQUIRED";
    public const string DocumentListAlreadyWithdrawn = "DOCUMENT_LIST_ALREADY_WITHDRAWN";

    // ── DCP-005 slice 3: citing a document ───────────────────────────────────
    /// <summary>The UID is not in the CURRENT list version. A state, not a fault — the register may simply not list it.</summary>
    public const string DocumentReferenceNotFound = "DOCUMENT_REFERENCE_NOT_FOUND";

    /// <summary>The row exists and says it may not be cited (<c>linkable_in_erp = no</c>), with the register's own reason.</summary>
    public const string DocumentReferenceBlocked = "DOCUMENT_REFERENCE_BLOCKED";

    /// <summary>Nothing has been imported yet, so there is no register to freeze a citation against.</summary>
    public const string DocumentListNotImported = "DOCUMENT_LIST_NOT_IMPORTED";
}

/// <summary>
/// Shared between the handler and its tests, so the limit cannot be asserted at a value nobody enforces — the
/// same arrangement <see cref="TaskCommentLimits"/> has, and the same ceiling.
/// </summary>
public static class TaskPersonalNoteLimits
{
    public const int MaxTextLength = 2000;

    /// <summary>
    /// How many notes one reader may keep on one task. A cap exists because the list is EMBEDDED in the overlay
    /// document and every projection read carries the whole of it; without one, a single reader could make their
    /// own detail page slow and nobody else would ever see why.
    /// </summary>
    public const int MaxNotesPerTask = 50;
}

/// <summary>
/// Notification event codes this module declares in its manifest (pack §14). Email only — there is no in-app
/// channel (<c>NotificationChannelCode { Email = 0 }</c>); the header bell is BL-025.
/// </summary>
/// <summary>Shared between the handler and its tests, so the limit cannot be asserted at a value nobody enforces.</summary>
public static class TaskCommentLimits
{
    /// <summary>
    /// 2000 characters — the same ceiling the executable contract puts on a business-context text field. Long
    /// enough for a real explanation, short enough that one paste cannot bloat every read of the task.
    /// </summary>
    public const int MaxTextLength = 2000;
}

public static class TaskNotificationEvents
{
    // NOTE: event codes are validated against ^[a-z0-9]+(\.[a-z0-9]+)*$ — HYPHENS ARE NOT ALLOWED (unlike
    // permission keys, which do permit kebab-case). A hyphenated code fails validation, which forces the event to
    // stay Draft, and a Draft event never dispatches — the email would silently never arrive.
    public const string Assigned = "platform.tasks.assigned";
    public const string Claimed = "platform.tasks.claimed";
    public const string DueSoon = "platform.tasks.duesoon";
    public const string Completed = "platform.tasks.completed";
    public const string ApprovalRequested = "platform.tasks.approvalrequested";

    /// <summary>
    /// Somebody said something on the task. ADDED to the existing list rather than opened as a parallel path:
    /// the module already owns a dispatcher, a policy and a recipient resolver, and a second notification road
    /// would need its own copy of the master switch, the per-event preference and the actor exclusion.
    ///
    /// <para>NEW comments only. An edit and a withdrawal deliberately send nothing — a typo correction does not
    /// earn anybody's inbox, and a retraction that emailed everyone would shout louder than the sentence it
    /// takes back.</para>
    /// </summary>
    public const string Commented = "platform.tasks.commented";
}

/// <summary>Contract limits, mirrored from fixture-contract.js LIMITS. The contract is the authority.</summary>
public static class TaskFieldLimits
{
    public const int MaxSections = 6;
    public const int MaxFieldsPerSection = 8;
    public const int MaxTextLengthPerField = 2000;
    public const int MaxPrimaryFields = 8;
    public const int MaxRelatedRecords = 20;
    public const int MaxTags = 20;
    public const int MaxTagLength = 40;
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 4000;
}

// ── Requests ─────────────────────────────────────────────────────────────────

/// <summary>
/// Create payload. TenantId is intentionally absent (never client-supplied). <c>Lifecycle</c> is absent too: the
/// system decides the initial state (pack §12 Y2). <c>SpentHours</c> is absent by design (pack §12 Y1).
/// </summary>
public sealed record CreateTaskItemRequest(
    string Title,
    string? Description,
    TaskPriority Priority,
    TaskAssignmentTarget AssignmentTarget,
    Guid? AssigneeUserId,
    Guid? PoolPositionId,
    Guid? OrganizationUnitId,
    DateTimeOffset? DueAt,
    DateTimeOffset? StartAt,
    DateTimeOffset? PlannedDate,
    decimal? EstimateHours,
    IReadOnlyList<string>? Tags,
    bool ReviewRequired,
    bool ApprovalRequired,
    Guid? ApprovalManagerUserId,
    bool EmailNotificationsEnabled,
    bool DelegationAllowed,
    IReadOnlyList<TaskFieldValueDto>? FieldValues,
    IReadOnlyList<TaskWatcherRequest>? Watchers,
    // Phase 2: present when this task is created AS a subtask of another. Optional and trailing so every
    // Phase-1 caller and payload stays valid.
    Guid? ParentTaskItemId = null,
    /// <summary>Instantiate this checklist template onto the new task (pack §12 E1/E5).</summary>
    Guid? ChecklistTemplateId = null,
    /// <summary>
    /// The task TYPE (DCP-005 slice 1). Optional and trailing, so every existing caller and payload stays valid.
    /// </summary>
    Guid? TaskTypeId = null,
    /// <summary>
    /// Who the requester SUGGESTS should review — required whenever <c>ReviewRequired</c> is set, because
    /// MOD-0023 cannot start a review with nobody to route it to. A candidate hint, not a decision: MOD-0023 and
    /// MOD-0018 resolve who may actually act.
    /// </summary>
    Guid? ReviewerCandidateUserId = null,
    /// <summary>
    /// BL-065 — which events this task emails about (<see cref="TaskNotificationEvents"/> codes). NULL means the
    /// caller is not choosing, and the task keeps the "every event" behaviour every task had before this field;
    /// an empty list means "none". Trailing and optional so every earlier payload stays valid.
    /// </summary>
    IReadOnlyList<string>? NotifyOnEvents = null,
    /// <summary>BL-065 — days before the due date to send the reminder. Null: no reminder.</summary>
    int? ReminderLeadDays = null,
    /// <summary>
    /// Checklist items typed on the create form, in the order the author put them.
    ///
    /// <para><b>Why they travel WITH the task instead of following it.</b> Until now the only way to give a new
    /// task a checklist was a second call, and a second call has a failure mode with no good answer: the task is
    /// written, the checklist is not, and the user is looking at a success message. One request removes that
    /// question rather than answering it.</para>
    ///
    /// <para>Composes with <see cref="ChecklistTemplateId"/> rather than competing: a template's items land
    /// first and these are appended after them, which is the order the author saw on screen.</para>
    /// </summary>
    IReadOnlyList<CreateChecklistItemRequest>? ChecklistItems = null,
    /// <summary>
    /// Controlled-document UIDs this task cites (DCP-005 slice 3). Trailing and optional, so every earlier
    /// caller and payload stays valid.
    ///
    /// <para>The six frozen fields are resolved SERVER-SIDE from the current register. The client sends UIDs and
    /// nothing else on purpose: a client that sent titles and versions would be a second authority over what a
    /// citation says, and the first stale tab would write a citation nobody could reproduce.</para>
    /// </summary>
    IReadOnlyList<string>? DocumentUids = null);

/// <summary>
/// One checklist item as the create form describes it. No <c>Code</c> and no <c>SortOrder</c>: the code is the
/// server's to mint (it is an identifier, not content), and the order is the ARRAY's — a client that sends a
/// separate sort field can contradict its own list, and then two readers disagree about what "first" means.
/// </summary>
public sealed record CreateChecklistItemRequest(
    /// <summary>What the author typed. TEXT, never a resource key — see <see cref="AddChecklistItemRequest"/>.</summary>
    string Text,
    ChecklistItemRequirement Requirement = ChecklistItemRequirement.Optional,
    /// <summary>Needs supporting evidence before it can be ticked. The evidence itself is MOD-0031's.</summary>
    bool EvidenceRequired = false);

public sealed record UpdateTaskItemRequest(
    string Title,
    string? Description,
    TaskPriority Priority,
    Guid? OrganizationUnitId,
    DateTimeOffset? DueAt,
    DateTimeOffset? StartAt,
    DateTimeOffset? PlannedDate,
    decimal? EstimateHours,
    IReadOnlyList<string>? Tags,
    bool ReviewRequired,
    bool EmailNotificationsEnabled,
    bool DelegationAllowed,
    IReadOnlyList<TaskFieldValueDto>? FieldValues,
    int ExpectedVersion,
    // Phase 3: approval can be switched on or off by an edit. NULL means "this caller is not editing approval" —
    // a form that never renders the toggle must not be able to silently drop an approval that is already running.
    // Trailing and optional so every Phase 1-2 payload stays valid.
    bool? ApprovalRequired = null,
    Guid? ApprovalManagerUserId = null,
    /// <summary>
    /// The reviewer candidate, on the same terms as creation. Unlike <c>ApprovalRequired</c> above this is NOT
    /// nullable-means-untouched: <c>ReviewRequired</c> is a plain bool here, so this request is a FULL REPLACE of
    /// the review requirement and the reviewer has to travel with it. An edit that drops the reviewer while the
    /// requirement stays on is refused rather than silently stripping it.
    /// </summary>
    Guid? ReviewerCandidateUserId = null,
    /// <summary>
    /// BL-065 — which events this task emails about (<see cref="TaskNotificationEvents"/> codes). NULL means the
    /// caller is not choosing, and the task keeps the "every event" behaviour every task had before this field;
    /// an empty list means "none". Trailing and optional so every earlier payload stays valid.
    /// </summary>
    IReadOnlyList<string>? NotifyOnEvents = null,
    /// <summary>BL-065 — days before the due date to send the reminder. Null: no reminder.</summary>
    int? ReminderLeadDays = null,
    /// <summary>
    /// The task's citation list AFTER this edit (DCP-005 slice 3), as UIDs.
    ///
    /// <para>⚠ NULL and EMPTY are different answers. Null means "not choosing" and leaves the citations alone —
    /// which is what every payload written before this field existed says. An empty list means "no documents"
    /// and clears them.</para>
    ///
    /// <para>⚠ A UID already cited is NOT re-resolved. It keeps the values frozen when it was chosen, title and
    /// code included; only UIDs new to this task are read from the register.</para>
    /// </summary>
    IReadOnlyList<string>? DocumentUids = null);

public sealed record TaskWatcherRequest(Guid UserId, TaskWatcherRole Role, Guid? PositionId);

/// <param name="Redacted">
/// BL-024 Phase 2 — the caller may not see this field, so <paramref name="Value"/> was withheld ON THE SERVER.
///
/// <para>A separate flag rather than "null means hidden", because null already means something: a field that
/// exists and is empty. Without the distinction the form cannot tell a value it may clear from a value it must
/// not touch — and on an edit, which is a FULL REPLACE, guessing wrong deletes somebody else's data.</para>
///
/// <para>Trailing and defaulted, so every request payload written before this field stays valid. On the way IN
/// it is ignored: what a client claims about redaction decides nothing.</para>
/// </param>
public sealed record TaskFieldValueDto(
    string DefinitionCode,
    TaskFieldValueType ValueType,
    string? Value,
    bool Redacted = false);

public sealed record BulkDeleteTaskItemRequest(IReadOnlyList<Guid> Ids);

public sealed record ClaimTaskItemRequest(int ExpectedVersion);

public sealed record TaskTransitionRequest(int ExpectedVersion, string? ReasonCode, string? Note);

/// <summary>
/// Set (or move) a personal plan date. Its OWN request type rather than an optional field bolted onto
/// <see cref="TaskTransitionRequest"/>: the date is REQUIRED for this one transition and meaningless for the
/// other nine — same reasoning that gives <see cref="InquireTaskItemRequest"/> its own mandatory <c>Reason</c>.
/// </summary>
public sealed record PlanTaskItemRequest(int ExpectedVersion, DateTimeOffset PlannedDate);

/// <summary>
/// Park a task in Waiting. <paramref name="Reason"/> is REQUIRED and is the user's own words, so it is stored as
/// text and never routed through a resource key.
/// </summary>
/// <summary>
/// Park the task, saying what it is waiting for and — optionally — WHO.
///
/// <para><paramref name="WaitingOnUserId"/> is OPTIONAL and TRAILING, both deliberately. Optional because a wait
/// is often on somebody this system has never heard of (a supplier, a customer, an authority), and there is
/// nobody to select; <paramref name="Reason"/> already carries that. Trailing with a default so every existing
/// caller — and the client that has not learned about it yet — compiles and behaves exactly as before.</para>
/// </summary>
public sealed record InquireTaskItemRequest(int ExpectedVersion, string Reason, Guid? WaitingOnUserId = null);

/// <summary>
/// Hand assigned work BACK to whoever asked for it. <paramref name="Reason"/> is required: a refusal the
/// requester cannot understand only moves the problem.
/// </summary>
public sealed record ReturnTaskItemRequest(int ExpectedVersion, string Reason);

/// <summary>
/// Hand work ON to somebody else. <paramref name="Reason"/> is required for the same reason as a return — the
/// new holder is being told why this is now theirs.
/// </summary>
public sealed record ReassignTaskItemRequest(int ExpectedVersion, Guid AssigneeUserId, string Reason);

/// <summary>Tick/untick one checklist item. ExpectedVersion guards the RUN, not the task.</summary>
public sealed record SetChecklistItemStateRequest(string ItemCode, bool Completed, int ExpectedVersion);

/// <summary>
/// Add an item the user typed. There is no resource key here on purpose: user text is not translatable content,
/// and routing it through a key is what puts the key itself on screen.
/// </summary>
public sealed record AddChecklistItemRequest(
    string Text,
    ChecklistItemRequirement Requirement,
    int ExpectedVersion);

/// <summary>
/// Edit one checklist item in place. All three fields are sent every time — this is a replace of the item's
/// editable face, not a patch, so "clear the evidence flag" and "don't mention the evidence flag" cannot be
/// confused with one another.
///
/// <para><paramref name="LabelText"/> is REFUSED for a template-owned item (one carrying a resource key). The
/// template's words belong to every task made from it; letting one task reword its copy would leave the same
/// item saying different things on different tasks, in a list whose whole purpose is that they say the same
/// thing. <see cref="ChecklistItemRequirement"/> and evidence are NOT refused: those say how strictly THIS task
/// is being run, which is exactly the kind of judgement the person holding the task is there to make.</para>
/// </summary>
public sealed record UpdateChecklistItemRequest(
    string? LabelText,
    ChecklistItemRequirement Requirement,
    bool EvidenceRequired,
    int ExpectedVersion);

/// <summary>Remove one checklist item. ExpectedVersion guards the RUN, like every other checklist write.</summary>
public sealed record RemoveChecklistItemRequest(int ExpectedVersion);

/// <summary>
/// Reorder the WHOLE list in one call.
///
/// <para>Per-item position writes were the alternative and they are worse in two separate ways: N requests for
/// one drag, and — because each lands independently — two people reordering at once interleave into an order
/// neither of them chose. One call writes one order, and the expected-version check makes the second person's
/// drag a clean 409 they can redo against what they can now see.</para>
///
/// <para><paramref name="ItemCodes"/> must name every item in the run exactly once. A partial list is rejected
/// rather than applied to the part it covers: half a reorder is not a smaller reorder, it is a different one.</para>
/// </summary>
public sealed record ReorderChecklistRequest(IReadOnlyList<string> ItemCodes, int ExpectedVersion);

/// <summary>
/// Add a typed dependency edge. Both ends are MOD-0024 tasks — a dependency on another module's object is
/// deliberately not expressible (pack §12 Y3): MOD-0024 may manage these edges only because it owns both ends.
/// </summary>
public sealed record AddTaskDependencyRequest(Guid DependsOnTaskItemId, TaskDependencyType DependencyType);

/// <summary>
/// Post a comment. Text only: no mentions are parsed, because there is no notification channel to deliver one
/// (WC-4) and a mention nobody is told about is a promise the system does not keep.
/// </summary>
public sealed record AddTaskCommentRequest(string Text);

/// <summary>
/// Rewrite one's OWN comment. Text only: the author, the task and the original instant are all facts about what
/// happened and none of them is being edited — what changes is the sentence, and the fact that it changed.
/// </summary>
public sealed record UpdateTaskCommentRequest(string Text);

// ── The personal overlay (WC-1) ──────────────────────────────────────────────

/// <summary>Add one private note to a task. The author is the caller — it is never sent by the client.</summary>
public sealed record AddTaskPersonalNoteRequest(string Text);

/// <summary>
/// Park a task in the reader's own inbox until <paramref name="SnoozedUntil"/>, or wake it now by sending null.
///
/// <para>ONE endpoint for both directions rather than a snooze and an unsnooze: the state is a nullable date, so
/// two endpoints would be two ways to write one field, and the second one written is always the one that forgets
/// a rule. There is no expected version — see <c>ITaskPersonalOverlayRepository.UpsertAsync</c> for why a
/// document with a single writer has no race to lose.</para>
/// </summary>
public sealed record SetTaskSnoozeRequest(DateTimeOffset? SnoozedUntil);

/// <summary>Pin or unpin this task for the caller. Mirrors <see cref="SetTaskSnoozeRequest"/> — same table,
/// same shape, same "the task is untouched" promise.</summary>
public sealed record SetTaskPinnedRequest(bool Pinned);

/// <summary>Create from a template; the template supplies the shape and (optionally) the checklist.</summary>
public sealed record CreateTaskFromTemplateRequest(
    Guid TaskTemplateId,
    string? TitleOverride,
    DateTimeOffset? DueAt,
    TaskAssignmentTarget? AssignmentTargetOverride,
    Guid? AssigneeUserId,
    Guid? PoolPositionId);

// ── Responses ────────────────────────────────────────────────────────────────

public sealed record TaskItemListItemDto(
    Guid Id,
    string Title,
    string Lifecycle,
    string NormalizedStatus,
    string Priority,
    string AssignmentTarget,
    Guid? AssigneeUserId,
    Guid? PoolPositionId,
    Guid OrganizationUnitId,
    DateTimeOffset? DueAt,
    bool ReviewRequired,
    bool ApprovalRequired,
    int Version,
    DateTimeOffset CreatedAt);

public sealed record TaskItemDetailDto(
    Guid Id,
    string Title,
    string? Description,
    string Lifecycle,
    string NormalizedStatus,
    string Priority,
    string AssignmentTarget,
    Guid? AssigneeUserId,
    Guid? PoolPositionId,
    Guid? CreatedByUserId,
    Guid OrganizationUnitId,
    DateTimeOffset? DueAt,
    DateTimeOffset? StartAt,
    DateTimeOffset? PlannedDate,
    decimal? EstimateHours,
    decimal SpentHours,
    decimal? RemainingHours,
    IReadOnlyList<string> Tags,
    bool ReviewRequired,
    bool ApprovalRequired,
    Guid? ApprovalManagerUserId,
    Guid? WorkflowInstanceId,
    bool EmailNotificationsEnabled,
    /// <summary>BL-065 — null when the owner never chose; the form reads that as "everything".</summary>
    IReadOnlyList<string>? NotifyOnEvents,
    int? ReminderLeadDays,
    bool DelegationAllowed,
    string? ProcessInstanceId,
    /// <summary>
    /// BL-054 — how old the TEMPLATE was when this task was built from it, or null for a hand-made task.
    ///
    /// <para>Exposed rather than merely stored, and that is the point: a stamp nothing can read is a payload
    /// nobody reads. The question it answers — "has the template been rewritten since this task was born?" —
    /// is asked by a person looking at the task, so the answer has to reach them.</para>
    /// </summary>
    DateTimeOffset? TemplateSnapshotAt,
    IReadOnlyList<TaskFieldValueDto> FieldValues,
    IReadOnlyList<TaskWatcherDto> Watchers,
    IReadOnlyList<TaskDependencyDto> Dependencies,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt,
    string? ClosureReasonCode,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    /// <summary>
    /// The reviewer candidate, so the edit form can re-render what is already on the task. Without it the form
    /// comes back blank and the next save — a FULL REPLACE — is refused, or worse, silently strips it.
    /// </summary>
    Guid? ReviewerCandidateUserId = null,
    /// <summary>The review's MOD-0023 instance. The LINK only; the verdict is read from MOD-0023.</summary>
    Guid? ReviewWorkflowInstanceId = null,
    /// <summary>
    /// BL-023 — the UPWARD WORK REQUEST's MOD-0023 instance, when the assignee sits above the requester. The
    /// LINK only: whether the work was accepted is MOD-0023's answer, never a field here.
    /// </summary>
    Guid? RequestWorkflowInstanceId = null,
    /// <summary>
    /// DCP-005 slice 3 — the task's FROZEN citations, in the order they were added. Empty when it cites
    /// nothing, and the screen draws no card at all in that case (DCP-004: a capability with no data is not a
    /// capability worth announcing).
    /// </summary>
    IReadOnlyList<TaskDocumentReferenceDto>? DocumentReferences = null,
    /// <summary>The task's TYPE, so an edit form can re-render it and ask that type for its governing documents.</summary>
    Guid? TaskTypeId = null);

public sealed record TaskWatcherDto(Guid Id, Guid UserId, string Role, Guid? PositionId);

public sealed record TaskDependencyDto(Guid Id, Guid DependsOnTaskItemId, string DependencyType);

/// <summary>
/// Assignable-position lookup row (pack §12 K4). It carries the organization unit's CODE and NAME because
/// <c>PositionDto</c> exposes only <c>OrganizationUnitId</c> — without the unit label a picker cannot
/// distinguish "QA Specialist — Facility A" from "QA Specialist — Facility B" and work lands in the wrong pool.
/// Joined server-side rather than reassembled client-side.
/// </summary>
/// <summary>
/// A person who may receive a task. <c>DisplayName</c> is nullable on purpose: it is resolved best-effort through
/// <c>IUserDisplayNameResolver</c>, so an AuthService outage leaves the row usable (position + unit) instead of
/// failing the whole lookup. The client renders a name-unavailable label — never the raw user id.
/// </summary>
public sealed record AssignablePersonDto(
    Guid UserId,
    string? DisplayName,
    Guid PositionId,
    string PositionCode,
    string PositionName,
    Guid OrganizationUnitId,
    string OrganizationUnitCode,
    string OrganizationUnitName,
    // BL-057 — which company the row belongs to. The position DTO has carried this from the start; the person
    // DTO did not, so the client could not tell two same-named people in different companies apart even after
    // the scope rule started deciding which of them may appear at all.
    Guid LegalEntityId);

/// <summary>
/// WHY the people picker is shorter than the user expects — BL-072.
///
/// <para>The lookup drops a candidate for six different reasons and used to say none of them: the list simply
/// came back short. On a nine-card form that leaves the user with no move except asking a developer to read the
/// database, which is exactly what happened.</para>
///
/// <para><b>⚠ Counts only, and that is a security boundary rather than a nicety.</b> Naming the people held back
/// by <see cref="OutOfScope"/> would hand back precisely what BL-057's rule withholds. Every member of this
/// record is an <see cref="int"/>, and a test asserts that it stays that way.</para>
/// </summary>
public sealed record ExcludedCandidateSummary(
    /// <summary>Distinct people who have an assignment but produced no row.</summary>
    int Total,
    /// <summary>No assignment in force: not started, already ended, or cancelled.</summary>
    int NoActivePosition,
    /// <summary>Held a position that is Draft/archived, or whose unit is archived.</summary>
    int PositionNotActive,
    /// <summary>Assignable in principle, but outside the actor's scope (BL-057).</summary>
    int OutOfScope)
{
    public static ExcludedCandidateSummary None { get; } = new(0, 0, 0, 0);
}

/// <summary>
/// The people lookup's answer: the rows, plus why the rest are missing.
///
/// <para>The breakdown is computed on the SERVER because only the server knows it. A client that inferred
/// "someone must be hidden" from a short list would be guessing, and it could not distinguish "nobody holds a
/// position" from "they are in another company".</para>
/// </summary>
public sealed record AssignablePersonLookupDto(
    IReadOnlyList<AssignablePersonDto> People,
    ExcludedCandidateSummary Excluded);

/// <summary>
/// WHICH question the people lookup is answering — BL-057, and the single most tempting mistake in that change.
///
/// <para>All four pickers on the create form drew from one list, so applying the scope to "the list" would have
/// silently killed intra-group approval: a task produced in GMG TR is legitimately approved in GMG AZ by
/// somebody who is neither above nor below the author and works for another company.</para>
/// </summary>
public enum TaskPersonLookupPurpose
{
    /// <summary>
    /// Who may RECEIVE the work — assignee, watcher. Scope-limited: doing work for me is an organizational
    /// relationship, and crossing a company boundary without one is a data-protection question.
    /// </summary>
    Assignment = 0,

    /// <summary>
    /// Who may DECIDE about the work — approver, reviewer. Scope-EXEMPT: the authority belongs to the process,
    /// not to the requester (SAP resolves it through agent determination, Oracle through approval rules). Still
    /// bounded by the tenant and by holding a live position; "exempt" means exempt from the COMPANY scope only.
    /// </summary>
    Decision = 1
}

/// <summary>One bindable template, for a picker: nothing but what a picker can show and send.</summary>
public sealed record TaskTemplateLookupDto(Guid Id, string Name);

public sealed record AssignablePositionDto(
    Guid PositionId,
    string PositionCode,
    string PositionName,
    Guid OrganizationUnitId,
    string OrganizationUnitCode,
    string OrganizationUnitName,
    Guid LegalEntityId,
    int ActiveHolderCount);

// ── Phase 5: configurable field definitions ──────────────────────────────────

/// <summary>
/// Define a configurable field. Exactly ONE label source: <c>LabelResourceKey</c> for a system field we ship
/// translations for, <c>LabelText</c> for a tenant's own words. A tenant administrator cannot add a line to our
/// resx files, so demanding a key from them would put the raw key on screen.
/// </summary>
public sealed record CreateTaskFieldDefinitionRequest(
    string Code,
    string? LabelResourceKey,
    string? LabelText,
    TaskFieldValueType ValueType,
    string Section,
    TaskFieldImportance Importance,
    bool IsRequired,
    int SortOrder,
    TaskFieldOptionsSourceKind OptionsSourceKind,
    string? OptionsSourceKey,
    string? AppliesToModuleCode,
    /// <summary>
    /// STORED, never evaluated — a LABEL for how sensitive the field is, not a rule. The rule is the two
    /// permission keys below. (It is still load-bearing in one place: a value whose definition has since been
    /// purged is judged by the classification copied onto it, so a once-classified value cannot become readable
    /// by losing its definition.)
    /// </summary>
    TaskFieldClassification Classification,
    TaskFieldAccessState DefaultAccessState,
    bool IsActive = true,
    /// <summary>
    /// BL-024 Phase 2 — the permission a caller must hold to SEE this field's values. Null: unrestricted.
    ///
    /// <para>A permission KEY, not a role: role identity does not reach this service (Platform receives role
    /// names, never ids) and MOD-0018's grant table has no room for a field. Naming a key means MOD-0018 keeps
    /// deciding who holds it and nothing here duplicates that.</para>
    ///
    /// <para>Trailing and optional, so every payload written before this stays valid and every existing
    /// definition stays unrestricted.</para>
    /// </summary>
    string? ViewPermission = null,
    /// <summary>BL-024 Phase 2 — the permission required to WRITE it. Null: anyone who can edit the task.</summary>
    string? EditPermission = null);

/// <summary>
/// Full replace — except <c>Code</c>, which is absent on purpose. Every <c>TaskFieldValue</c> already stored
/// joins to its definition BY CODE, so an edited code orphans them all and the data loses its label.
/// </summary>
public sealed record UpdateTaskFieldDefinitionRequest(
    string? LabelResourceKey,
    string? LabelText,
    TaskFieldValueType ValueType,
    string Section,
    TaskFieldImportance Importance,
    bool IsRequired,
    int SortOrder,
    TaskFieldOptionsSourceKind OptionsSourceKind,
    string? OptionsSourceKey,
    string? AppliesToModuleCode,
    TaskFieldClassification Classification,
    TaskFieldAccessState DefaultAccessState,
    bool IsActive,
    int ExpectedVersion,
    /// <summary>BL-024 Phase 2 — see the create request. Trailing and optional; an edit that omits them clears
    /// the restriction, which is the same full-replace semantics every other field on this request has.</summary>
    string? ViewPermission = null,
    string? EditPermission = null);

/// <summary>
/// Retire several definitions at once.
///
/// <para><b>Envelope, and POST, matching <see cref="BulkDeleteTaskItemRequest"/> on the same controller.</b> The
/// client was sending a bare array over DELETE; that shape was inherited from the golden-reference script, not
/// designed here. Two bulk shapes in ONE controller is a worse cost than adapting four lines of a hand-written
/// fetch handler — and a body on DELETE is the shape proxies and frameworks treat least predictably, which is
/// presumably why the existing precedent avoided it.</para>
/// </summary>
public sealed record BulkDeleteTaskFieldDefinitionRequest(IReadOnlyList<Guid> Ids);

/// <summary>
/// What a bulk retire actually did.
///
/// <para>The task precedent answers 204 and tells the caller nothing, so a request naming five definitions of
/// which two do not exist reports the same success as one where all five were retired — and the screen then says
/// "5 deleted". That is the trap this response exists to avoid: the counts are reported, and the client says
/// what happened rather than what was asked for.</para>
/// </summary>
public sealed record BulkDeactivateFieldDefinitionsResponse(int Deactivated, int NotFound);

public sealed record TaskFieldDefinitionDto(
    Guid Id,
    string Code,
    string? LabelResourceKey,
    string? LabelText,
    string ValueType,
    string Section,
    string Importance,
    bool IsRequired,
    int SortOrder,
    string OptionsSourceKind,
    string? OptionsSourceKey,
    string? AppliesToModuleCode,
    string Classification,
    string DefaultAccessState,
    bool IsActive,
    int Version,
    DateTimeOffset CreatedAt);

/// <summary>
/// One choice a configurable field offers. Flattened on purpose: a platform lookup, a published reference value
/// and another module's record have three different shapes upstream, and the form must not have to know which
/// kind it is looking at.
///
/// <para><b>Value is always the identity and Label is always what the reader recognises.</b> For a lookup or a
/// reference set those are the code and its label. For a module record the identity is the record's id and the
/// label is its NAME — so renaming the record renames it on the task, and the raw identity never reaches the
/// screen (BL-049).</para>
/// </summary>
/// <param name="Secondary">
/// An optional second line that disambiguates: the business key, and the organization unit where two facilities
/// can each own a "QA Specialist". Null for the short fixed sources, whose label already says everything.
/// </param>
public sealed record TaskFieldOptionDto(string Value, string Label, string? Secondary = null);

/// <summary>
/// One source an administrator may point a field at — a platform lookup key, a reference set code, or a
/// registered module record source. What the field-definition screen offers INSTEAD of a free-text box.
/// </summary>
/// <param name="Key">Exactly what gets stored in <c>OptionsSourceKey</c>. Data, never a display string.</param>
/// <param name="Label">
/// What the administrator reads. A tenant's reference set carries its own name; ours carry a resource key, and
/// <paramref name="LabelResourceKey"/> says which — the same split the field definition itself already makes
/// between a system label and a tenant's own words.
/// </param>
/// <param name="ModuleCode">Which module owns the records. Null for the two non-record kinds.</param>
public sealed record TaskFieldOptionSourceDto(
    string Key,
    string Label,
    string? LabelResourceKey,
    string? ModuleCode);

// ── Phase 4: recurrence ──────────────────────────────────────────────────────

/// <summary>
/// Define a recurring task. The rule says WHEN and WHAT SHAPE; it never says "make one now" — the sweep does
/// that, and only for periods that have actually begun.
/// </summary>
public sealed record CreateTaskRecurrenceRuleRequest(
    string Name,
    TaskRecurrenceFrequency Frequency,
    int Interval,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    /// <summary>
    /// WHO the generated work goes to. <c>SelfAssigned</c> is not accepted: a sweep has no "self", and a rule
    /// that said so produced work assigned to nobody, in nobody's list, while still consuming its period.
    /// </summary>
    TaskAssignmentTarget AssignmentTarget,
    Guid? AssigneeUserId,
    Guid? PoolPositionId,
    /// <summary>Optional override. Creation resolves a unit on its own when this is null.</summary>
    Guid? OrganizationUnitId,
    /// <summary>
    /// The template each generated task is built from. Optional: without one the rule generates a bare task
    /// carrying only the rule's name, which is a legitimate simple reminder.
    /// </summary>
    Guid? TaskTemplateId,
    bool IsActive = true);

/// <summary>Full replace, like every other MOD-0024 update.</summary>
public sealed record UpdateTaskRecurrenceRuleRequest(
    string Name,
    TaskRecurrenceFrequency Frequency,
    int Interval,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    TaskAssignmentTarget AssignmentTarget,
    Guid? AssigneeUserId,
    Guid? PoolPositionId,
    Guid? OrganizationUnitId,
    Guid? TaskTemplateId,
    bool IsActive,
    int ExpectedVersion);

public sealed record TaskRecurrenceRuleDto(
    Guid Id,
    string Name,
    string Frequency,
    int Interval,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    Guid? TaskTemplateId,
    string AssignmentTarget,
    Guid? AssigneeUserId,
    Guid? PoolPositionId,
    Guid? OrganizationUnitId,
    bool IsActive,
    /// <summary>
    /// The last occurrence this rule produced, by NAME. Exposed because it is the answer to "why did nothing
    /// appear today?" — and because a support engineer comparing it against the current period is doing exactly
    /// what the sweep does.
    /// </summary>
    string? LastProcessInstanceId,
    DateTimeOffset? LastGeneratedAt,
    int Version,
    DateTimeOffset CreatedAt);

/// <summary>What one sweep pass did for one tenant.</summary>
public sealed record GenerateDueRecurringTasksResponse(
    int RulesConsidered,
    int TasksGenerated,
    int AlreadyGenerated,
    int Failed,
    /// <summary>
    /// Rules that owed work but could not say WHO it belongs to, so their period was left unclaimed. Only rules
    /// written before assignment existed on the entity can be in this state; counting them separately from
    /// <c>Failed</c> keeps "needs fixing, nothing lost" distinct from "something went wrong".
    /// </summary>
    int SkippedUnassigned = 0);

/// <summary>
/// BL-065 — what one due-soon sweep did.
///
/// <para>Every considered task lands in exactly ONE of these counters. The first version had only "sent" and
/// "failed (threw)", so an outcome that was neither — a provider refusal, nobody reachable — fell through all of
/// them and the sweep logged a clean run while a reminder was lost. Counters that do not add up are how a
/// scheduler reports success it did not have.</para>
/// </summary>
public sealed record SendDueSoonRemindersResponse(
    int TasksConsidered,
    int RemindersSent,
    /// <summary>Already claimed for this deadline — the ordinary case on every sweep after the first.</summary>
    int AlreadyReminded,
    /// <summary>Attempted and NOT delivered (refused, nobody reachable): the claim was released for a retry.</summary>
    int NotDelivered,
    /// <summary>The send threw. Also released for a retry.</summary>
    int Failed);


// ── DCP-005 slice 1: task types ─────────────────────────────────────────────

/// <summary>
/// Create a task type. <c>Code</c> is normalised and then frozen — see <c>TaskTypeRules.CodeImmutableMessage</c>.
/// </summary>
public sealed record CreateTaskTypeRequest(
    string Code,
    string Name,
    string? Description,
    TaskRecordClass RecordClass,
    TaskGqmsDomain? GqmsDomain,
    string? FunctionCode,
    bool IsQualityEvent,
    IReadOnlyList<string>? GroupDocuments,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? LocalDocuments);

/// <summary>
/// Update a task type. <c>Code</c> is accepted so the screen can round-trip what it displayed, and REFUSED if it
/// differs — silently keeping the stored one would report success for a change that did not happen.
/// </summary>
public sealed record UpdateTaskTypeRequest(
    string? Code,
    string Name,
    string? Description,
    TaskRecordClass RecordClass,
    TaskGqmsDomain? GqmsDomain,
    string? FunctionCode,
    bool IsQualityEvent,
    IReadOnlyList<string>? GroupDocuments,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? LocalDocuments);

/// <summary>Retire or restore a type. There is no delete — see <c>DeactivateTaskTypeHandler</c>.</summary>
public sealed record SetTaskTypeActiveRequest(bool IsActive);

/// <summary>One task type as the management screen and the task form read it.</summary>
public sealed record TaskTypeDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    TaskRecordClass RecordClass,
    TaskGqmsDomain? GqmsDomain,
    string? FunctionCode,
    bool IsQualityEvent,
    IReadOnlyList<string> GroupDocuments,
    IReadOnlyDictionary<string, IReadOnlyList<string>> LocalDocuments,
    bool IsActive);


// ── DCP-005 slice 2: the controlled-document reference list ────────────────

/// <summary>
/// One import of the register. Shaped after <c>QmsBaselineCommitRequest</c> — same file/version/source triple,
/// because a second import shape would be a second thing to keep in step.
/// </summary>
public sealed record ImportDocumentReferenceListRequest(
    string FileName,
    string ContentBase64,
    string SourceKey,
    string ListVersion);

/// <summary>What a dry run reports without writing anything.</summary>
public sealed record DocumentReferenceListDryRunResult(
    int EntryCount,
    int LinkableCount,
    int BlockedCount,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> MissingColumns,
    /// <summary>Columns the register carries that this importer does not read — reported, never ignored.</summary>
    IReadOnlyList<string> UnreadColumns,
    string ContentHash,
    /// <summary>Set when these exact bytes are already stored: the import would add nothing.</summary>
    string? AlreadyImportedAsVersion);

public sealed record DocumentReferenceListVersionDto(
    Guid Id, string ListVersion, string SourceKey, string FileName,
    string ContentHash, int EntryCount, int LinkableCount, DateTimeOffset ImportedAt,
    /// <summary>Set when this version is no longer in service. The row still travels — it is marked, not gone.</summary>
    DateTimeOffset? WithdrawnAt = null,
    string? WithdrawnReason = null,
    string? WithdrawnBy = null);

/// <summary>Take a version out of service. The reason is required; see the handler.</summary>
public sealed record WithdrawDocumentListVersionRequest(string Reason);

/// <summary>One row of the lookup, as the search returns it.</summary>
/// <summary>
/// One frozen citation as the reader sees it (DCP-005 §6.2). Every field here was written ONCE, when the
/// document was chosen; nothing on the read path re-resolves any of it.
/// </summary>
public sealed record TaskDocumentReferenceDto(
    string DocumentUid,
    string DocumentCode,
    string Title,
    string? DocumentVersion,
    string? Status,
    DateTimeOffset ReferencedAt,
    /// <summary>Which register version said this. Readable even after that version has been withdrawn.</summary>
    Guid ListVersionId);

/// <summary>
/// A task type's governing documents, resolved against the CURRENT list (DCP-005 §6.4).
///
/// <para>⚠ <c>Suggestions</c> being empty is a state with a CAUSE, and the cause is carried rather than left
/// for the screen to guess: a type may name nothing, or name documents the register does not list, or name
/// documents the register refuses to link. Those read differently to the person choosing, and a single empty
/// box would tell them nothing about which one they are looking at.</para>
/// </summary>
public sealed record TaskTypeGoverningDocumentsDto(
    IReadOnlyList<DocumentReferenceEntryDto> Suggestions,
    /// <summary>How many UIDs the type names in total — group plus the org's local layer.</summary>
    int NamedCount,
    /// <summary>Named UIDs the current register does not contain at all.</summary>
    IReadOnlyList<string> UnresolvedUids,
    /// <summary>Named UIDs the register contains but refuses to link, with the register's own reason.</summary>
    IReadOnlyList<DocumentReferenceEntryDto> BlockedSuggestions);

public sealed record DocumentReferenceEntryDto(
    string DocumentUid,
    string DocumentCode,
    string Title,
    string? DocumentVersion,
    string? Status,
    string? GqmsDomain,
    bool IsMandatoryGroupSop,
    /// <summary>False means: SHOW it, and refuse to let it be chosen. Never hide it.</summary>
    bool LinkableInErp,
    string? LinkBlockedReason);

// ── BL-054: the template chain ───────────────────────────────────────────────
//
// Two screens, and the ORDER between them is load-bearing rather than tidy. A task template's form offers a
// checklist-template picker; without a checklist screen that picker is a control with no source — exactly the
// shape the recurrence rule's own template picker had before this slice, where a live-looking select could never
// be filled. The checklist contracts come first here for the same reason they ship first.

/// <summary>One step in a reusable checklist, as it travels on the wire.</summary>
/// <param name="Code">Stable key. A run item is matched back to its template item by this, so it never moves.</param>
/// <param name="LabelResourceKey">
/// System text, translated in all seven languages. Exactly one of this and <paramref name="LabelText"/> is set —
/// conflating them is how a raw resource key reaches the screen, which has happened here before.
/// </param>
/// <param name="LabelText">A tenant author's own words, in the language they typed them in.</param>
public sealed record ChecklistTemplateItemDto(
    string Code,
    string? LabelResourceKey,
    string? LabelText,
    ChecklistItemRequirement Requirement,
    int SortOrder,
    bool EvidenceRequired);

public sealed record CreateChecklistTemplateRequest(
    string Code,
    string Name,
    string? Description,
    IReadOnlyList<ChecklistTemplateItemDto> Items,
    bool IsActive = true);

/// <summary>Full replace, like every other MOD-0024 update — the item list included.</summary>
public sealed record UpdateChecklistTemplateRequest(
    string Code,
    string Name,
    string? Description,
    IReadOnlyList<ChecklistTemplateItemDto> Items,
    bool IsActive,
    int ExpectedVersion);

public sealed record ChecklistTemplateDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    IReadOnlyList<ChecklistTemplateItemDto> Items,
    /// <summary>Rendered as a column so the list answers "how long is this checklist?" without opening it.</summary>
    int ItemCount,
    bool IsActive,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>What the task-template form's checklist picker is filled from.</summary>
public sealed record ChecklistTemplateLookupDto(Guid Id, string Code, string Name, int ItemCount);

/// <summary>
/// Define a reusable task shape.
///
/// <para><b>What is deliberately NOT here.</b> No organization unit: a template says what work LOOKS LIKE, and
/// which department it lands in is the recurrence rule's sentence, not the template's. No subtasks either — a
/// template produces ONE task; a shape that produces a tree is a project template and a different feature.</para>
/// </summary>
public sealed record CreateTaskTemplateRequest(
    string Code,
    string Name,
    string? TitleTemplate,
    string? DescriptionTemplate,
    TaskPriority DefaultPriority,
    /// <summary>
    /// The DEFAULT holder, used only when the recurrence rule leaves its own assignment empty. The rule wins
    /// when it names anybody — the form says so out loud, because two places carrying an assignment with no
    /// stated precedence is how work silently reaches the wrong person.
    /// </summary>
    TaskAssignmentTarget DefaultAssignmentTarget,
    Guid? DefaultPoolPositionId,
    int? DefaultDueInDays,
    Guid? ChecklistTemplateId,
    /// <summary>Null = every company. One id = that company only; see <c>TaskTemplate.LegalEntityId</c>.</summary>
    Guid? LegalEntityId,
    bool IsActive = true);

public sealed record UpdateTaskTemplateRequest(
    string Code,
    string Name,
    string? TitleTemplate,
    string? DescriptionTemplate,
    TaskPriority DefaultPriority,
    TaskAssignmentTarget DefaultAssignmentTarget,
    Guid? DefaultPoolPositionId,
    int? DefaultDueInDays,
    Guid? ChecklistTemplateId,
    Guid? LegalEntityId,
    bool IsActive,
    int ExpectedVersion);

public sealed record TaskTemplateDto(
    Guid Id,
    string Code,
    string Name,
    string? TitleTemplate,
    string? DescriptionTemplate,
    // Enums as STRINGS on the wire — the live Platform convention, and one an enum-as-number defect already
    // cost this module once.
    string DefaultPriority,
    string DefaultAssignmentTarget,
    Guid? DefaultPoolPositionId,
    int? DefaultDueInDays,
    Guid? ChecklistTemplateId,
    Guid? LegalEntityId,
    bool IsActive,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
