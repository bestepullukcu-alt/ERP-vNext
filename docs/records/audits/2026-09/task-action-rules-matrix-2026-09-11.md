# Görev motoru (MOD-0024) — işlem kuralları tablosu · 2026-09-11

> **Kayıt türü:** salt-okunur inceleme çıktısı (SOP Profil C). **Üreten:** CONTROL TOWER'ın alt ajanı (Sonnet 5),
> 2026-09-11, HEAD `9623bee3` civarı; tekrarlayan kural uçları kapsam dışı bırakıldı (o sırada başka ajan
> düzenliyordu). **CT'nin kendi doğruladıkları:** (1) `TransitionTaskItemHandler` yalnız iptal için aktör soruyor;
> başlat/sürdür/tamamla holder'a bakmıyor; `SubmitTaskForReviewHandler` ve `PlanTaskItemHandler` hiç aktör
> sormuyor; (2) `TaskWorkItemProvider.BuildActions` `isHolder`'ı hesaplıyor (`:1648`) ama accept/inquire/release
> yapılarında kullanmıyor; (3) `?scope=team` (BL-023 Ekibim) astların görevlerini listeliyor. **Doğrulanmayan:**
> Tablo 4 (kiracı referans verisi) ve "düşük" bulgular — alt ajanın ölçümü. Canlı oturumla hiçbir şey denenmedi.
> Sonuçlar backlog'da: BL-361 (erişim modeli kararı), BL-362 (projeksiyon–handler uyumsuzluğu), BL-363 (küçükler).
> Satır numaraları okuma anına aittir; kayıt değişmez, kod değişir — ölçüm komutları backlog maddelerindedir.

---

# MOD-0024 Task Engine — Action Matrix (as implemented)

Read-only audit. All line numbers are as of branch `fix/workcenter-role-testing` at the time of reading (no files
in the repo were modified to produce this document). Recurrence-rule endpoints/handlers are explicitly OUT OF
SCOPE (another agent is editing `TaskRecurrenceRuleHandlers.cs` and two test files) and are only listed by name.

Legend for column 2 (actor relation): **requester** = `TaskItem.CreatedByUserId == caller`; **holder** =
`TaskItem.AssigneeUserId == caller`; **pool member** = caller currently holds the offered `PoolPositionId`;
**author** = wrote the specific comment/checklist-item row; **self** = the personal-overlay row is keyed by
caller's own id by construction; **anyone(perm)** = no per-record relation is checked, the `[HasPermission]` (or
the dispatcher's permission map) is the only gate.

Structure: Table 1 = the task's own lifecycle/ownership verbs (full 7 columns — this is the heart of the audit).
Table 2 = content sub-resources hung off a task (checklist/comments/personal/dependencies — full 7 columns).
Table 3 = the Work-Item aggregation surface that re-exposes Table 1's verbs through a second address (full 7
columns). Table 4 = tenant-wide reference/admin data with no per-task ownership dimension (condensed to 3
columns: full detail here would not add information — every row's actor relation is simply "anyone holding the
named permission").

Row/column totals: Table 1 = 18 rows × 7 cols. Table 2 = 14 rows × 7 cols. Table 3 = 2 rows × 7 cols. Table 4 = 41
rows × 3 cols (condensed). Recurrence rules = 5 rows, listed only, not analyzed. **Grand total analyzed: 75 rows.**

---

## Table 1 — Task lifecycle & ownership (`/api/v1/tasks/...`)

Source: `TasksController.cs`; handlers in `TaskItemTransitionHandlers.cs`, `CreateTaskItemHandler.cs`,
`ClaimTaskItemHandler.cs`, `TaskItemWriteHandlers.cs`; lifecycle matrix in `TaskLifecycleService.cs`; domain gates
in `TaskItem.cs`.

### 1. Create — `POST /tasks`
1. **Permission**: `TaskPermissions.Create` (`TasksController.cs:42`).
2. **Who**: anyone(perm). Self-assignment and naming another Person/Pool both go through `CreateTaskItemHandler`
   (`CreateTaskItemHandler.cs:26`); target `Person`≠self or `PositionPool` is checked by the assignment guard
   (below), not by an actor-relation rule — there is no actor relation to a task that does not exist yet.
3. **From/To**: n.a. → `Lifecycle = ResolveInitialLifecycle(...)` = always `Open` (`TaskLifecycleService.cs:23-27`,
   `CreateTaskItemHandler.cs:286`).
4. **Flags/gates**: `TaskAssignmentIntentRules.Validate` (`CreateTaskItemHandler.cs:134-139`); assignment guard
   `CheckTargetAsync` (`CreateTaskItemHandler.cs:172-178`), skipped only when `command.IsScheduledGeneration` —
   verified NOT client-settable: it is a MediatR-command-only positional parameter defaulting to `false`, absent
   from `CreateTaskItemRequest`/the JSON body (`Commands/TaskItemCommands.cs:37`, controller passes only
   `(request, CorrelationId)` at `TasksController.cs:45`); approval-manager-required-if-ApprovalRequired
   (`CreateTaskItemHandler.cs:231-236`); reviewer-required-if-ReviewRequired via `TaskReviewRules.ReviewerMissing`
   (`CreateTaskItemHandler.cs:243-247`); subtask one-level-only (`CreateTaskItemHandler.cs:250-268`); document
   citations frozen via `TaskDocumentReferenceFreezer` (`CreateTaskItemHandler.cs:329-340`).
5. **Assignment guard asked**: yes — `CheckTargetAsync` (Person or Pool; self exempt).
6. **Projection**: n.a. (no "create" action code in `TaskWorkItemProvider`/dispatcher — creation is a screen, not
   a work-item action).
7. **Side effects**: assignee/pool set as requested (post-guard); `TaskAssignment` row (`Created`) written
   (`CreateTaskItemHandler.cs:387-396`); watchers created **visibility-only** (`CreateTaskItemHandler.cs:398-416`,
   comment: "never action rights"); upward-request to MOD-0023 if assignee outranks requester (BL-023,
   `CreateTaskItemHandler.cs:359-369`); approval handoff if `ApprovalRequired` (`:375-383`); assignment + (if
   applicable) approval-requested notifications (`:418`, `NotifyAssignedAsync` `:538-559`).

### 2. GetList — `GET /tasks`
1. **Permission**: `TaskPermissions.Read` (`TasksController.cs:50`).
2. **Who**: anyone(perm), but the RESULT is pre-scoped to the caller: `GetTaskItemListHandler` returns only tasks
   the caller **holds** (`ListByAssigneeAsync`) plus **unclaimed pool** tasks for positions the caller occupies
   (`GetTaskItemListHandler.cs:41-47`) — it does **not** include a requester's outbox.
3. **From/To**: n.a. (read).
4. **Flags/gates**: none beyond permission.
5. **Assignment guard**: n.a.
6. **Projection**: n.a. (this is the plain `TaskItemListItemDto` list, not the Work-Item board).
7. **Side effects**: none (read).

### 3. GetById — `GET /tasks/{id}`
1. **Permission**: `TaskPermissions.Read` (`TasksController.cs:58`).
2. **Who**: **anyone(perm) in the tenant, by id — no ownership/relationship filter.** `GetTaskItemByIdHandler.cs:40-46`
   filters only by the tenant execution filter ("Cross-tenant reads land here too... the caller learns nothing");
   there is no check that the caller is holder/requester/watcher/pool-eligible. This is the read-side counterpart
   of finding **S-3** below.
3. **From/To**: n.a. (read).
4. **Flags/gates**: none beyond permission + tenant filter.
5. **Assignment guard**: n.a.
6. **Projection**: n.a.
7. **Side effects**: none.

### 4. Update — `PUT /tasks/{id}`
1. **Permission**: `TaskPermissions.Update` (`TasksController.cs:66`).
2. **Who**: **anyone(perm) — no requester/holder check anywhere in `UpdateTaskItemHandler`**
   (`TaskItemWriteHandlers.cs:16-314`, verified by reading the whole handler top to bottom — only a
   closed-task check at `:64-68`). This can rewrite Title/Description/Priority/DueAt/EstimateHours/Tags,
   and can flip `ReviewRequired`, `ApprovalRequired` (+ manager), `DelegationAllowed`,
   `EmailNotificationsEnabled`/`NotifyOnEvents`. See finding **S-3**.
3. **From/To**: any non-closed lifecycle (`CompletedAt is null && CancelledAt is null`, `:64-68`); lifecycle
   itself is not one of the editable fields.
4. **Flags/gates**: reviewer-required-if-ReviewRequired (`:123-128`); approval toggle starts/cancels a MOD-0023
   instance (`:168-205`, decided here, acted on after the write commits); document citations re-frozen
   (`:230-244`; existing frozen entries are never re-resolved).
5. **Assignment guard**: n.a. (Update does not change `AssigneeUserId`/`AssignmentTarget` — those move only
   through Accept/Claim/Release/Reassign/Return).
6. **Projection**: n.a. — "edit" has no action code in the Work-Item dispatcher; only reachable via
   `/api/v1/tasks/{id}` directly.
7. **Side effects**: `ExpectedVersion`-conditional write (`:248-253`); `Declare(Edited, ...)` for history
   (`:246`); approval/review instance start-or-cancel as noted above.

### 5. Delete — `DELETE /tasks/{id}`
1. **Permission**: `TaskPermissions.Delete` (`TasksController.cs:74`).
2. **Who**: anyone(perm) — `DeleteTaskItemHandler.cs:316-337` has no requester check; soft delete only.
3. **From/To**: any state → soft-deleted (`IsDeleted`); no lifecycle guard.
4. **Flags/gates**: none.
5. **Assignment guard**: n.a.
6. **Projection**: n.a. (no dispatcher action code).
7. **Side effects**: `DeletedAt`/`IsDeleted` set by the repository base; no cascading effect on children/edges
   observed in this handler.

### 6. BulkDelete — `POST /tasks/bulk-delete`
1. **Permission**: `TaskPermissions.BulkDelete` (`TasksController.cs:82`).
2. **Who**: anyone(perm); each id is independently re-read through the tenant filter and silently skipped if not
   found (`TaskItemWriteHandlers.cs:348-360`) — no per-task ownership check.
3–5. Same as Delete.
6. **Projection**: n.a.
7. **Side effects**: same as Delete, looped.

### 7. Accept — `POST /tasks/{id}/accept`
1. **Permission**: `TaskPermissions.Update` (`TasksController.cs:91`).
2. **Who**: **holder** — `task.AssignmentTarget != Person || task.AssigneeUserId != _currentUser.UserId` → 403
   (`TaskItemTransitionHandlers.cs:45-49`).
3. **From/To**: acceptance-gate only (`AcceptedByUserId is null`, `:59-64`, 409 `ALREADY_ACCEPTED` if not); if
   `Lifecycle == Open` it is promoted to `InProgress` (`:75-78`) — Planned stays Planned.
4. **Flags/gates**: none beyond the above; `ExpectedVersion` write (`:92`).
5. **Assignment guard**: n.a.
6. **Projection**: `BuildActions` offers `accept` gated **only** on `actor.Has(Update)`
   (`TaskWorkItemProvider.cs:1709-1714`) — **not** on `isHolder`, even though `isHolder` is already computed at
   `:1648`. See finding **S-2**.
7. **Side effects**: `CloseAcceptanceGate` (`TaskItem.cs:65`); `Declare(Accepted, ...)`; `TaskAssignment` row
   (`EventType=Accepted`).

### 8. Claim — `POST /tasks/{id}/claim`
1. **Permission**: `TaskPermissions.Claim` (`TasksController.cs:100`).
2. **Who**: **pool member** — `HoldsPositionAsync(actorId, task.PoolPositionId)` via `ITaskSeatDirectory`
   (`ClaimTaskItemHandler.cs:82-87`).
3. **From/To**: pool task must be unclaimed (`AssigneeUserId is null`, `:73-77`) and not closed (`:65-69`).
4. **Flags/gates**: `ExpectedVersion`-conditional write is the actual race-breaker (`:95-101`; the earlier
   `AssigneeUserId is not null` check is only a fast-path message).
5. **Assignment guard**: n.a. (claiming is a self-claim gated by seat-holding, which is the guard for this verb).
6. **Projection**: `claim` gated on `unclaimed` (pool + no assignee) **and** `actor.Has(Claim)`
   (`TaskWorkItemProvider.cs:1703-1708`) — consistent; a caller only sees `claim` on pool rows, and
   `HoldsPositionAsync` is re-checked by the handler regardless.
7. **Side effects**: `AssigneeUserId = actorId`; `TaskAssignment` row (`Claimed`); requester notified
   (`ClaimTaskItemHandler.cs:114-126`), other position-holders deliberately not (OD-2).

### 9. Release — `POST /tasks/{id}/release`
1. **Permission**: `TaskPermissions.Claim` (`TasksController.cs:108`).
2. **Who**: **holder** — `task.AssigneeUserId != _currentUser.UserId` → 403 (`TaskItemTransitionHandlers.cs:150-155`),
   after confirming the task is a claimed pool task (`:143-148`).
3. **From/To**: pool+claimed → pool+unclaimed; `Lifecycle` rewound to `Open`; `ReopenAcceptanceGate()` (`:163`,
   BL-051 fix — this is the class of bug the acceptance-gate history explicitly documents as previously broken).
4. **Flags/gates**: `ExpectedVersion` write (`:172-177`).
5. **Assignment guard**: n.a. (removing the holder, not naming one).
6. **Projection**: `release` gated on `isPool && !unclaimed` **and** `actor.Has(Claim)`
   (`TaskWorkItemProvider.cs:1875-1879`) — **not** on `isHolder`. See finding **S-2**.
7. **Side effects**: `TaskAssignment` row (`Released`).

### 10. Plan — `POST /tasks/{id}/plan`
1. **Permission**: `TaskPermissions.Update` (`TasksController.cs:122`).
2. **Who**: **anyone(perm) — no actor check at all** in `PlanTaskItemHandler.cs:835-908` (verified end to end:
   only a required-date check and `CanTransition`). See finding **S-1**.
3. **From/To**: `CanTransition(..., Planned)` — allowed from `Open`/`Planned` (self-loop, re-plan) per
   `TaskLifecycleService.cs:190-196`.
4. **Flags/gates**: `PlannedDate != default` required (`:856-860`); deliberately **no** range check against
   `DueAt`/today (documented as intentional, `:875-887`); `ExpectedVersion` write.
5. **Assignment guard**: n.a.
6. **Projection**: `plan` gated only on `openOrPlanned && !unclaimed` **and** `actor.Has(Update)`
   (`TaskWorkItemProvider.cs:1832-1835`) — no `isHolder` gate either. Same root cause as S-1/S-2.
7. **Side effects**: `Lifecycle=Planned`, `PlannedDate` set; `Declare(Planned, ...)` even on a no-op re-plan
   (deliberate, so "this slipped twice" is visible in history).

### 11. Start — `POST /tasks/{id}/start` (⇒ `TransitionTaskItemCommand(..., InProgress, ...)`)
1. **Permission**: `TaskPermissions.Update` (`TasksController.cs:130`).
2. **Who**: **anyone(perm) — no holder check.** `TransitionTaskItemHandler.Handle` (`TaskItemTransitionHandlers.cs:252-571`)
   contains exactly one actor check in the whole method, and it fires only for `target == Cancelled`
   (`:296-303`); nothing gates `InProgress`. See finding **S-1** — this is the headline issue.
3. **From/To**: `CanTransition` — `Open|Planned → InProgress`, or `Waiting → InProgress` ("resume", same
   endpoint/code) per `TaskLifecycleService.cs:190-199`. Pool tasks must be claimed first (`:180-186`).
4. **Flags/gates**: workflow **approval** gate via `IWorkflowTransitionGate` when `task.ApprovalRequired`
   (`:311-332`, fail-closed); dependency gate for `FinishToStart`/`StartToStart` edges (`:410-419`); closure
   outcome gate does not apply (only Done/Cancelled targets, `:457`); `ExpectedVersion` write.
5. **Assignment guard**: n.a.
6. **Projection**: `start`/`resume` gated only on lifecycle branch + `actor.Has(Update)`
   (`TaskWorkItemProvider.cs:1741-1752`) — **not** on `isHolder`. Server and projection **agree** in the wrong
   direction (both permissive) — this is a genuine bypass, not merely a UI/handler disagreement.
7. **Side effects**: `StartAt` stamped if unset (`:538-540`); `Declare(Started|Resumed, ...)` with **the
   caller's** `_currentUser.UserId` as actor, regardless of who the assignee is.

### 12. SubmitReview — `POST /tasks/{id}/submitReview`
1. **Permission**: `TaskPermissions.Update` (`TasksController.cs:144`).
2. **Who**: **anyone(perm) — no holder check.** `SubmitTaskForReviewHandler.Handle` (`:735-823`) checks only
   `task.ReviewRequired` and the resubmission/rejection state; no `AssigneeUserId` comparison anywhere in the
   class. Same class of bug as S-1.
3. **From/To**: `InProgress → PendingReview`, or `PendingReview → PendingReview` resubmission only when MOD-0023
   reports the current review `Rejected` (`:766-778`).
4. **Flags/gates**: MOD-0023 instance opened via `ITaskReviewService.TryStartReviewAsync` **before** the
   lifecycle write (`:787-802`, deliberately reversed order vs. the approval toggle on edit); `ExpectedVersion`
   write (`:809`).
5. **Assignment guard**: n.a.
6. **Projection**: gated only on lifecycle branch + `actor.Has(Update)` (`TaskWorkItemProvider.cs:1763-1774`,
   `:1804-1811`) — not on `isHolder`.
7. **Side effects**: `ReviewWorkflowInstanceId` set; `Declare(SubmittedForReview, ...)`.

### 13. Complete — `POST /tasks/{id}/complete` (⇒ `TransitionTaskItemCommand(..., Done, ...)`)
1. **Permission**: `TaskPermissions.Complete` (`TasksController.cs:152`) — the one verb with its own permission
   key instead of `Update`.
2. **Who**: **anyone(perm) — no holder check**, same handler/method as Start (`TaskItemTransitionHandlers.cs:252-571`).
   See finding **S-1**.
3. **From/To**: `InProgress|PendingReview → Done` (`TaskLifecycleService.cs:197-201`).
4. **Flags/gates**: blocking-checklist gate (`:276-285`, mirrors `ITaskChecklistService.BlocksCompletion` —
   verified same predicate as the projection's `ChecklistBlocksCompletion`, `TaskWorkItemProvider.cs:1626-1628`);
   approval gate (`:311-332`); review gate — requires an instance to exist and MOD-0023 to report not-blocked
   (`:349-394`); dependency gate for `FinishToFinish`/`StartToFinish`-style edges (`:410-419`); open-subtask gate,
   completion only (`:432-441`, BL-035); closure-outcome-required/valid/reason gate read from the task's TYPE
   (`:457-494`). All five gates are enforced in the handler, not only projected — confirmed no bypass on any of
   them.
5. **Assignment guard**: n.a.
6. **Projection**: `complete` gated on lifecycle branch + `actor.Has(Complete)`
   (`TaskWorkItemProvider.cs:1781-1789`, `:1814-1826`) — **not** on `isHolder`. Same bypass as Start.
7. **Side effects**: `CompletedAt`, `ClosureReasonCode` stamped; `Declare(Completed, ...)`; requester notified
   (`:562-568`) — note the requester is told "your work is finished" even when it was actually completed by a
   third party exploiting S-1, not by the true holder.

### 14. Inquire ("waiting") — `POST /tasks/{id}/inquire`
1. **Permission**: `TaskPermissions.Update` (`TasksController.cs:165`).
2. **Who**: **holder** — `task.AssigneeUserId != _currentUser.UserId` → 403
   (`TaskItemTransitionHandlers.cs:966-971`, "a statement about their own work").
3. **From/To**: `CanTransition(..., Waiting)` — from `Open|Planned|InProgress`.
4. **Flags/gates**: `Reason` required (`:949-957`); `WaitingOnUserId`, if given, validated against
   `TaskAssigneeEligibility.ResolveAssignableUserIds(..., scope: null)` (`:991-1011`) — **deliberately
   scope-exempt** (a "decision" question per `TaskAssigneeEligibility.cs:41-43`), confirmed intentional, not a
   drift from the assignment guard's scope check used elsewhere.
5. **Assignment guard**: no (uses the eligibility resolver directly, by design, not `ITaskAssignmentGuard`).
6. **Projection**: `inquire` gated only on lifecycle + `actor.Has(Update)`
   (`TaskWorkItemProvider.cs:1845-1848`) — **not** on `isHolder`. See finding **S-2**.
7. **Side effects**: `WaitingReason`/`WaitingOnUserId` set; `Declare(Waiting, reason)`.

### 15. Return — `POST /tasks/{id}/return`
1. **Permission**: `TaskPermissions.Update` (`TasksController.cs:180`).
2. **Who**: **holder** — `task.AssigneeUserId != _currentUser.UserId` → 403
   (`TaskItemTransitionHandlers.cs:1084-1089`).
3. **From/To**: lifecycle untouched in the sense of stage, but rewound to `Open` and gate reopened (`:1105-1108`);
   refused if there is no separate requester (`:1093-1098`).
4. **Flags/gates**: `Reason` required (`:1070-1076`).
5. **Assignment guard**: n.a. (returns to the already-legitimate requester, not a new arbitrary target).
6. **Projection**: `return` included **only** when `!isPool && isHolder && hasSeparateRequester`
   (`TaskWorkItemProvider.cs:1861-1864`) — correctly matches the handler. No mismatch.
7. **Side effects**: `AssigneeUserId = CreatedByUserId`; `ReopenAcceptanceGate()`; `Declare(Returned, reason)`;
   `TaskAssignment` row logged as `Reassigned` (no `Returned` enum value exists — documented, not a defect).

### 16. Reassign — `POST /tasks/{id}/reassign`
1. **Permission**: `TaskPermissions.Assign` (`TasksController.cs:192`).
2. **Who**: **holder OR requester** — `isHolder = AssigneeUserId==caller`, `isRequester = CreatedByUserId==caller`;
   neither → 403 `ReassignNotPermitted` (`TaskItemTransitionHandlers.cs:1203-1210`). Pool tasks refused outright
   (`:1196-1201`).
3. **From/To**: no lifecycle-stage change; rewound to `Open`, gate reopened.
4. **Flags/gates**: **BL-357 fix, confirmed correctly implemented**: `DelegationAllowed` is checked only when
   `isHolder && !isRequester` (`:1232-1237`) — i.e. it limits the holder's further hand-off, never the
   requester's own correction, matching the description of the already-fixed bug (commit `d35f8f32`) given in
   this task's brief. `Reason` required (`:1182-1188`); new assignee ≠ current (`:1246-1251`).
5. **Assignment guard asked**: **yes** — `_assignmentGuard.CheckPersonAsync` (`:1259-1263`), no self-exemption
   (correctly, per its own comment: "taking a task over is still an assignment").
6. **Projection**: `reassign` included only when `!isPool && (isHolder || isRequester)`
   (`TaskWorkItemProvider.cs:1866-1872`), and `ReassignAction` re-derives the exact same DelegationAllowed policy
   (`:1932-1936`, `isRequester` passed through, never re-read). Matches the handler. No mismatch.
7. **Side effects**: `AssigneeUserId` set; `ReopenAcceptanceGate()`; `Declare(Reassigned, reason)`;
   `TaskAssignment` row.

### 17. Cancel — `POST /tasks/{id}/cancel` (⇒ `TransitionTaskItemCommand(..., Cancelled, mayCancelAnyTask)`)
1. **Permission**: `TaskPermissions.Cancel` (`TasksController.cs:207`).
2. **Who**: **requester, OR anyone holding `TaskPermissions.Delete`** ("administrative authority"). Computed at
   the controller from JWT claims via `PermissionClaimEvaluator.Evaluate(User.Claims, TaskPermissions.Delete)`
   (`TasksController.cs:210`, **not** client-supplied) and passed as data into the command; enforced in the
   handler at `TaskItemTransitionHandlers.cs:296-303` (403, not 409 — "a refusal of AUTHORITY").
3. **From/To**: `CanTransition(..., Cancelled)` — from any non-terminal state.
4. **Flags/gates**: none of the Start/Complete gates apply to Cancel (approval/review/dependency/subtask checks
   are all scoped to `InProgress`/`Done` targets only, `:311`, `:349`, `:410`).
5. **Assignment guard**: n.a.
6. **Projection**: `cancel` included when `isRequester || actor.Has(Delete)`
   (`TaskWorkItemProvider.cs:1899-1902`) — **exactly** mirrors the controller/handler's `mayCancelAnyTask`
   derivation. No mismatch. Same rule reused for the Work-Item dispatcher (`TaskWorkItemActionDispatcher.cs:159-172`).
7. **Side effects**: `CancelledAt`, `ClosureReasonCode`; `Declare(Cancelled, ...)`; `CancelOpenSubtasksAsync` —
   cascades cancellation to open (non-Done/Cancelled) subtasks, best-effort per child (`:609-648`); parent's
   closure reason/outcome is deliberately **not** copied onto the child (documented fix).

### 18. CreateFromTemplate — `POST /tasks/from-template`
1. **Permission**: `TaskPermissions.Create` (`TasksController.cs:222`).
2. **Who**: anyone(perm) — identical to Create.
3–7. **Delegates entirely to `CreateTaskItemCommand` via MediatR** (`CreateTaskItemFromTemplateHandler.cs:16-27,
   90-93`) — "every rule that governs task creation... therefore applies unchanged." Verified no duplicated/
   drifted assignment-guard or validation logic. No bug found.

---

## Table 2 — Content sub-resources

### 19. SetChecklistItemState — `POST .../checklist/items/state`
1. **Permission**: `Update` (`TasksController.cs:237`).
2. **Who**: anyone(perm) — **deliberately not author-restricted** ("doing the work is everyone's",
   `ChecklistHandlers.cs:447`).
3. **From/To**: task must not be closed (`:61-66`).
4. **Flags**: `ExpectedVersion` on the `ChecklistRun` (own version, independent of the task's).
5. **Assignment guard**: n.a.
6. **Projection**: checklist card is visibility/data, not a dispatcher action; tick state feeds `checklistBlocks`.
7. **Side effects**: `CompletedByUserId/At` stamped; run `Status` recomputed.

### 20. AddChecklistItem — `POST .../checklist/items`
1–2. Same as #19 (Update, anyone(perm)).
3. Closed-task check present (`ChecklistHandlers.cs:134-139`) — **note**: the shared-guard doc-comment at
   `ChecklistHandlers.cs:391-393` still says "the tick verb has [the closed check] and the add verb does not,"
   which is now stale (Add clearly has its own inline check). Doc/code drift only, see **S-6** (low).
4. New item stamped `AddedByUserId = caller` (`:187`), which is what makes #21/#22's author check possible.
5. n.a. 6. n.a. 7. Creates the `ChecklistRun` if none exists, or appends.

### 21. UpdateChecklistItem — `PUT .../checklist/items/{code}`
1–2. Permission `Update`; **who**: **author** of that specific ad-hoc item, via `RefuseNotYours`
   (`ChecklistHandlers.cs:253-254, 456-478`) — a template-owned item (`LabelResourceKey` set) is refused to
   everyone (409 `ChecklistItemTemplateOwned`), a null-author legacy/template row is refused to everyone (409
   `ChecklistItemNotAuthor`).
3. Closed-task check via shared `ChecklistWriteGuards.ResolveAsync`. 4. n.a. 5. n.a.
6. n.a. (not a dispatcher action). 7. `Requirement`/`EvidenceRequired` can flip a Blocking item to Optional —
   correctly re-runs `ResolveStatus` afterward so the completion gate cannot go stale.

### 22. RemoveChecklistItem — `DELETE .../checklist/items/{code}`
Same author/template rule as #21 (`ChecklistHandlers.cs:317-318`). Deliberately reversed from an earlier
looser rule (comment at `:280-286` — the reversal itself, not the current state, so not a finding).

### 23. ReorderChecklist — `PUT .../checklist/order`
1–2. Permission `Update`; **who: anyone(perm) — no author restriction** at all (`ChecklistHandlers.cs:337-385`).
   Reordering does not touch content/requirement, so this looks consistent with "ticking is everyone's," but it
   is the one write of the five that neither re-checks authorship nor recomputes `Status`— both deliberate per
   the code's own comments. Not flagged as a bug (order does not affect the completion gate).

### 24. AddComment — `POST .../comments`
1. **Permission**: `Read` (`TasksController.cs:318`, deliberately not Update — "commenting is not a change to the
   work").
2. **Who**: anyone(perm) who can read the task — by design, no holder/requester restriction.
3. Task must not be `Done`/`Cancelled` (`TaskCommentHandlers.cs:78-83`).
4–5. n.a. 6. n.a. (comments are not dispatcher actions).
7. Notifies holder+requester+watchers+prior commenters, actor excluded by the shared notification service
   (`:110-144`).

### 25. UpdateComment — `PUT .../comments/{commentId}`
1. **Permission**: `Read`. 2. **Who**: **author only** — `TaskCommentAuthority.LoadOwnAsync`
   (`TaskCommentHandlers.cs:191-197, 258-295`), 403 `CommentNotAuthor` for anyone else (not 404 — the comment is
   already visible, so pretending otherwise "would be a confusing lie," per the code's own comment).
3. **Note**: unlike Add, Update/Withdraw do **not** re-check `task.Lifecycle` — a comment can be edited/withdrawn
   on a closed task. Plausibly intentional (Add's closed-task refusal is about new content on live work; editing
   your own prior remark is arguably harmless either way) but not documented as a deliberate asymmetry the way
   most of this module's edge cases are. Low-confidence, low severity — see **S-7**.
4–7. Sets `EditedAt`; no notification (documented: "a typo correction does not earn anybody's inbox").

### 26. WithdrawComment — `DELETE .../comments/{commentId}`
Same author-only rule as #25; tombstones (`Text=null`, `WithdrawnAt` set), never a hard delete.

### 27. AddPersonalNote / 28. DeletePersonalNote / 29. SetSnooze / 30. SetPinned
1. **Permission**: `Read` for all four (deliberately not `Update` — "a person who may look at a task but not move
   it" must still be able to leave themselves a reminder).
2. **Who**: **self**, by construction — every read/write is keyed by `(taskId, _currentUser.UserId)`
   (`TaskPersonalOverlayHandlers.cs` throughout); the code's own comment calls this an authorization **shape**
   rather than a **check** ("there is no branch here that could delete somebody else's note"). Confirmed true by
   inspection — no id or user parameter from the request is ever used to select whose overlay to touch.
3–5. n.a. (the task itself is read only to confirm existence/visibility, never mutated).
6. n.a. — not part of the Work-Item actions[] surface.
7. Overlay row created/updated; task untouched (contract rule `SNOOZE_MUST_NOT_CREATE_WAITING`, verified: no
   write to `TaskItem.Lifecycle`/`WaitingReason` anywhere in this file). **No `ExpectedVersion`** on any of the
   four (overlay has no optimistic-concurrency field) — low severity, single-writer-per-row by construction.

### 31. AddDependency — `POST .../dependencies`
1. **Permission**: `Update` (`TasksController.cs:414`).
2. **Who**: anyone(perm) who can resolve **both** task ids through the tenant filter
   (`TaskDependencyHandlers.cs:47-63`) — **no check that the caller holds/requested either end**; a user could
   link two tasks that are both somebody else's, as long as both ids resolve in-tenant. Self-cycle, duplicate and
   graph-cycle checks are all present (`:41-45, 65-82`) — this is a data-integrity guard, not an authority one.
3–5. n.a. 6. n.a. (dependencies are visibility/data on the projection, not an actionable dispatcher verb).
7. Creates a `TaskDependency` row; no history entry on either task (by measured design, `TaskDependencyHandlers.cs`
   has no `Declare(...)` call).

### 32. RemoveDependency — `DELETE .../dependencies/{dependencyId}`
Same as #31: 404 if the edge does not belong to the URL's task (`:159-163`), but no holder/requester check on
either end of the edge.

---

## Table 3 — Work-Item aggregation surface (`/api/v1/work-items/...`)

Source: `WorkItemsController.cs`, `TaskWorkItemProvider.cs` (`GetWorkItemsAsync`, `BuildActions`),
`TaskWorkItemActionDispatcher.cs`, `TaskTeamResolver.cs`.

### 33. GetMine — `GET /work-items/mine?scope=self|team`
1. **Permission**: `WorkAggregationPermissions.InboxView` (`WorkItemsController.cs:85`); the union of every bound
   provider's action permissions is separately evaluated from JWT claims into `granted`
   (`:94-104`, server-side, not client-supplied) and passed through as `WorkItemActor.GrantedPermissions`.
2. **Who**: `scope=self` (default) → tasks the caller **holds**, **may claim** (pool positions held), or
   **initiated but does not hold** (outbox) — `TaskWorkItemProvider.cs:247-288`. `scope=team` → the caller's
   **actual subordinates'** own tasks, resolved from the org chart via `ITaskAssignmentScopeResolver`
   (`TaskTeamResolver.cs:49-64`, confirmed properly scope-limited — not exploitable to see arbitrary users).
   Scope is a plain `[FromQuery]` parameter (`WorkItemsController.cs:88`); no extra permission gates `team`
   beyond the same `InboxView` and having an actual org-chart team.
3–5. Per constituent action, see Table 1 — this endpoint only **projects**, via `BuildActions`.
6. **Projection — this is where findings S-1/S-2 live.** `BuildActions` computes `isHolder`/`isRequester`
   (`TaskWorkItemProvider.cs:1647-1649`) and, for the `scope=self` **outbox** rows, correctly passes
   `initiatorOnly=true` so holder-only actions are **withheld entirely** (not just disabled) —
   `:1684-1701`, extensively documented (BL-016). **For `scope=team` rows there is no equivalent flag at all**:
   `GetWorkItemsAsync`'s Team branch (`:233-246`) computes no per-row ownership marker, and `Project(...)`
   (`:560-564`) passes only `initiatorOnly` into `BuildActions` — never anything for "I am viewing this as
   someone else's manager, not as its holder." The executable contract itself only models one non-holder
   relation at all — `VIEWER_RELATIONS = ['initiator']`
   (`frontend/.../fixture-contract.js:82`) — there is no "team"/"observer" relation in the wire contract, and the
   WorkCenterNext client applies no extra filtering of its own (`getActions` in `mock-data.js:606-609` only
   strips non-deeplink actions from **terminal** items — it does not know about scope at all). The browser
   renders exactly what the server sends, and the server sends **fully-enabled** `accept`/`start`/`resume`/
   `submitReview`/`complete`/`plan`/`inquire`/`release` action objects for a subordinate's task whenever the
   viewing manager holds the underlying `Update`/`Complete`/`Claim` permission — see S-1/S-2.
7. Read-only endpoint itself; side effects happen only via #34.

### 34. DispatchAction — `POST /work-items/{itemId}/actions/{actionCode}`
1. **Permission**: resolved per action code from `TaskWorkItemActionDispatcher.Permissions`
   (`TaskWorkItemActionDispatcher.cs:40-54`) and checked against JWT claims
   (`WorkItemsController.cs:188-198`, server-side). **Cross-checked against every controller `[HasPermission]` in
   Table 1 for the same eleven verbs — all eleven match exactly** (accept/claim/release/plan/start/submitReview/
   complete/inquire/return/reassign/cancel), and `RequiredActionPermissions`
   (`TaskWorkItemProvider.cs:202-210`) declares the same six permission keys the dispatcher/BuildActions actually
   use — no drift found in either direction (this pairing is also pinned by
   `WorkItemActionDispatchTests`/`TaskWorkItemProviderTests` per the code's own comments).
2. **Who**: whatever the underlying command's handler enforces (see Table 1, column 2, rows 7–17) — this
   endpoint adds **no** extra ownership check of its own beyond the permission-key gate, and it does **not** know
   or care what `scope` the row was fetched under (there is no `scope` parameter here at all). This is exactly
   why S-1 is reachable end-to-end: `scope=team` hands the manager a task id and a working `start`/`complete`
   button; `DispatchAction` forwards it straight to `TransitionTaskItemCommand`, which — per Table 1 rows 11/13 —
   never checks `AssigneeUserId`.
3–5. Identical to the corresponding Table-1 row; the dispatcher does not re-implement any rule
   (`TaskWorkItemActionDispatcher.cs:14-17`, explicit design intent, verified true by reading the whole
   `switch`).
6. n.a. (this is the write side; #33 is the read/projection side).
7. Identical to the corresponding Table-1 row's side effects. `cancel`'s `mayCancelAnyTask` is independently
   re-derived here from `request.Actor.Has(TaskPermissions.Delete)` (`:166-172`) — confirmed to match the
   controller's own derivation (`TasksController.cs:210`) exactly, so no drift between the two entry points for
   Cancel specifically.

---

## Table 4 — Tenant-wide reference/admin data (condensed)

No per-task ownership dimension applies to any row below — column 2 is always "anyone holding the named
permission," there is no from/to lifecycle, and none of these appear in the Work-Item actions[] surface. Listed
for completeness per the "one row per endpoint" instruction; no suspicious cells were found in this table beyond
the two noted inline.

| # | Endpoint(s) | Permission | Notes |
|---|---|---|---|
| 35 | `POST document-list/dry-run` | `DocumentListImport` | Read-without-store preview. |
| 36 | `POST document-list/import` | `DocumentListImport` | New list VERSION; old versions never deleted. |
| 37 | `PUT document-list/versions/{id}/withdraw` | `DocumentListImport` | No delete route (deliberate — closed tasks may cite a withdrawn version). |
| 38 | `GET document-list/versions` | `DocumentListRead` | |
| 39 | `GET document-list/search` | `DocumentListRead` | |
| 40 | `GET task-types/{id}/governing-documents` | `DocumentListRead` (same key as search — deliberate, see `TasksController.cs:505-511`) | |
| 41 | `GET work-report` | `WorkReportRead` | Scope resolved server-side via MOD-0018-FU15 `IDataScopeResolver`; `scope` query param is a display preference only, never widens results (`TasksController.cs:528-533, 556-560`) — verified by reading the controller doc-comments; the resolver itself is outside this audit's file list. |
| 42 | `GET work-report/items` | `WorkReportRead` | Same query/scope as #41 by construction. |
| 43 | `GET work-report/export` | `WorkReportRead` | Same. |
| 44–50 | `task-types` CRUD + `active` + `closure-outcome-catalog` (7 routes) | `TaskTypesManage`, except `task-types/active` = `Read` (deliberate split — creating a type is administrative, picking one for a new task is not, `TasksController.cs:691-697`) | No delete route (types are retired, never removed). |
| 51–59 | `field-definitions` read/manage (9 routes) | `Read` for read/options/records; `FieldDefinitionsManage` for option-sources/create/update/bulk-delete/delete | Same "reading to fill a form is ordinary work, shaping the form is administrative" split as task-types. |
| 60–64 | `checklist-templates` CRUD (5 routes) | `ChecklistTemplatesManage` | |
| 65–69 | `templates` CRUD (5 routes) | `TemplatesManage` | |
| 70 | `lookups/checklist-templates` | `TemplatesManage` (not the checklist key — deliberate, `TasksController.cs:1046-1050`) | |
| 71 | `lookups/assignable-positions` | `Create` | |
| 72 | `lookups/assignable-people` | `Assign` | |
| 73 | `lookups/assignment-direction/{userId}` | `Create` | Answers via the same `ManagerChain` scope the server later uses for real (BL-023) — label and behavior can't disagree. |
| 74 | `lookups/decision-makers` | `Create` | **Deliberately NOT** the assignment-scoped list — decision authority (approve/review) is process-level, not company-scoped; confirmed intentional via `TaskAssigneeEligibility`'s `scope: null` path (`TaskAssigneeEligibility.cs:41-43`) and the controller's own extensive comment (`TasksController.cs:1085-1097`). |
| 75 | `lookups/task-templates` | `RecurrenceManage` | |

**Out of scope, not analyzed** (per instructions — another agent is editing these): `recurrence-rules` CRUD (5
routes, all `RecurrenceManage`, `TasksController.cs:908-948`) and `TaskRecurrenceRuleHandlers.cs`.

---

## Suspicious cells — full detail (condensed version returned in chat)

### S-1 — HIGH — Start/Complete/SubmitReview/Plan have no holder check; a subordinate's manager can execute them
**Action(s)**: Start (`InProgress`), Complete (`Done`), SubmitReview (`PendingReview`), Plan (`Planned`) — Table 1
rows 10–13, reachable both directly (`/api/v1/tasks/{id}/...`) and via the Work-Item dispatcher (Table 3 row 34).

**What the code does**: `TransitionTaskItemHandler.Handle` (`TaskItemTransitionHandlers.cs:252-571`) checks
`task.CreatedByUserId != _currentUser.UserId` **only** when `command.Target == Cancelled`
(`:296-303`); no branch anywhere in the method compares `task.AssigneeUserId` to the caller for `InProgress` or
`Done`. `SubmitTaskForReviewHandler.Handle` (`:735-823`) and `PlanTaskItemHandler.Handle` (`:851-908`) have no
actor check of any kind. The projection (`TaskWorkItemProvider.BuildActions`) offers these four actions gated
only by `actor.Has(TaskPermissions.Update|Complete)` (`:1712, 1743, 1750, 1771, 1787, 1808, 1824, 1834`) — never
by the already-computed `isHolder` (`:1648`).

**Why it looks wrong**: every sibling verb that changes who-does-the-work-or-when (Accept, Release, Inquire,
Return) explicitly checks `AssigneeUserId == caller`, and Reassign/Cancel/Return are all backed by an extensive,
well-reasoned actor discussion in the code's own comments (BL-357, the outbox carve-out, etc.). Start/Complete/
SubmitReview/Plan are the FOUR verbs that most directly mean "I am doing/finished the work," and they are the
ones with no such check at all — an inversion of the pattern the rest of the module follows carefully.

**How it is reached today**: `GET /api/v1/work-items/mine?scope=team` (`WorkItemsController.cs:84-110`) shows a
manager their **actual** subordinates' own tasks (`TaskTeamResolver.cs`, properly org-chart-scoped — not a
cross-tenant or arbitrary-user issue). For those rows `isHolder` is false for the manager, yet
`BuildActions`/`GetWorkItemsAsync` apply **no** equivalent of the outbox's `initiatorOnly` withholding
(`GetWorkItemsAsync` Team branch: `TaskWorkItemProvider.cs:233-246`; `Project(...)` passes only `initiatorOnly`
into `BuildActions`, never a team/holder flag: `:560-564`). The executable contract itself only defines one
non-holder viewer relation, `initiator` (`fixture-contract.js:82`) — "team" was never modeled at the contract
level. The WorkCenterNext client (`mock-data.js:606-609`) applies no independent filtering, so a manager sees a
fully-enabled, working "Tamamla"/"Başlat" button on a subordinate's task and pressing it succeeds via
`POST /work-items/{id}/actions/complete` → `TaskWorkItemActionDispatcher.cs:112-115` →
`TransitionTaskItemCommand(..., Done, ...)` → the same unguarded handler. Independently of the Team-scope UI, the
same bypass is reachable directly against `/api/v1/tasks/{anyId}/complete` for anyone who can name a task id and
holds the (apparently broadly-granted, since it also gates ordinary checklist-ticking) `Update`/`Complete`
permission — `GetTaskItemByIdHandler` applies no ownership filter either (tenant-only, `GetTaskItemByIdHandler.cs:40-46`),
so a task id is not meaningfully secret.

**Evidence**: `TaskItemTransitionHandlers.cs:201-571` (Start/Complete), `:708-824` (SubmitReview), `:835-908`
(Plan); `TaskWorkItemProvider.cs:233-246, 560-564, 1647-1652, 1703-1835`; `TaskTeamResolver.cs:49-66`;
`WorkItemsController.cs:84-110`; `WorkAggregationModels.cs:1096-1107`; `fixture-contract.js:82`;
`mock-data.js:606-609`.

**Severity**: HIGH — wrong actor can perform a real state-changing/closing write on someone else's task, through
a legitimate, discoverable UI path with a working button, not merely a raw-API edge case.

### S-2 — MEDIUM — Accept/Inquire/Release: projection offers them without the holder gate the handler enforces
**Action(s)**: Accept, Inquire, Release — Table 1 rows 7, 9, 14.

**What the code does**: the handlers correctly refuse a non-holder (`AcceptTaskItemHandler.cs:45-49`,
`ReleaseTaskItemHandler` at `TaskItemTransitionHandlers.cs:150-155`, `InquireTaskItemHandler` at `:966-971`), but
`BuildActions` includes/enables all three based only on lifecycle-shape + permission
(`TaskWorkItemProvider.cs:1709-1714` accept, `:1845-1848` inquire, `:1875-1879` release) — never on `isHolder`.

**Why it looks wrong**: same root cause as S-1 (Team-scope rows have no holder-gating in the projection at all),
but here the handler is correctly defensive, so the practical effect is a wrong hint rather than a bypass: a
manager viewing `scope=team` sees an enabled "Accept"/"Bekliyor"/"Havuza bırak" button on a subordinate's row that
will always answer 403 if pressed.

**Evidence**: as above plus the three handler citations.

**Severity**: MEDIUM — this is exactly the "button enabled but server refuses" pattern the brief asked to look
for; no data is actually put at risk, but it is a broken/misleading control and the same missing carve-out that
makes S-1 dangerous.

### S-3 — MEDIUM (confidence: medium — role-grant data not in the read set) — Update/Delete/BulkDelete/Dependency-writes/most checklist-writes have no ownership check
**Action(s)**: Update (Table 1 row 4), Delete/BulkDelete (rows 5–6), AddDependency/RemoveDependency (rows 31–32),
SetChecklistItemState/ReorderChecklist (rows 19, 23).

**What the code does**: none of these handlers compare `AssigneeUserId`/`CreatedByUserId` to the caller; the
`[HasPermission]` attribute (`Update`, `Delete`, `BulkDelete`) is the only gate, and `GetTaskItemByIdHandler`
applies no ownership filter either, so any tenant user holding the permission can read, edit, or delete any task
by id, and can link/tick/reorder any task's dependencies/checklist.

**Why it looks wrong, but with a caveat**: this is internally consistent with a design where `Update`/`Delete`
are genuinely tenant-wide administrative permissions (the Cancel handler explicitly treats `Delete` as
"administrative authority over any task," `TaskItemTransitionHandlers.cs:200-205` doc-comment). It looks
inconsistent because `Update` is simultaneously the permission that gates ordinary, plainly-not-administrative
self-service actions (ticking one's own checklist, editing dependencies) that the rest of the module goes out of
its way to scope narrowly elsewhere (personal overlay is deliberately gated by `Read` rather than `Update`
specifically so it is available to a **wider** audience than task-editors, `TaskPersonalOverlayHandlers.cs:11-25`
— implying the authors think of `Update` as a real, non-universal gate, not a formality). Whether this is a bug
depends on how broadly `Update`/`Delete` are actually granted by the default role templates, which is not among
this audit's source files — **flagged with reduced confidence** rather than asserted.

**Evidence**: `TaskItemWriteHandlers.cs:16-362` (whole file); `TaskDependencyHandlers.cs:19-168` (whole file);
`ChecklistHandlers.cs:17-83, 337-385`; `GetTaskItemByIdHandler.cs:40-46`.

**Severity**: MEDIUM, confidence-qualified.

### S-4 — insufficient evidence — real-world exploitability of S-1/S-3 depends on role-permission-template grants
Whether `TaskPermissions.Update`/`Complete`/`Claim` are, in practice, held broadly (making S-1 trivially
exploitable by any manager) or narrowly (making it require an unusual grant) is determined by seed data
(`DefaultRolePermissionTemplate` or similar) that is **not** among the files this audit was scoped to read. The
code-level gap is proven regardless; its blast radius is not independently confirmed here.

### S-5 — LOW — dependency-cycle/duplicate check has a narrow TOCTOU window
`AddTaskDependencyHandler` reads existing edges and walks the graph for a cycle (`TaskDependencyHandlers.cs:65-82,
106-140`) with no lock and no version check on the edge collection itself (dependencies are independent rows, not
a versioned sub-document like the checklist run). Two concurrent adds that are each individually fine could both
pass the cycle check and jointly create one. Not evaluated further (no evidence of it happening; low blast
radius — a graph-shape defect, not an authority bypass).

### S-6 — LOW (doc/code drift, not a behavior bug) — stale comment about the checklist closed-task check
`ChecklistHandlers.cs:391-393` (the `ChecklistWriteGuards` class doc-comment) states "the tick verb has [the
closed-task check] and the add verb does not," citing BL-093 as still-open. Reading `AddChecklistItemHandler`
directly (`:126-139`) shows it **does** have its own inline closed-task check today. The comment was not updated
when BL-093 was fixed. No functional impact — noted only because the task brief asked to record inconsistencies.

### S-7 — LOW / insufficient evidence — comment edit/withdraw doesn't re-check task closure
`UpdateTaskCommentHandler`/`WithdrawTaskCommentHandler` (`TaskCommentHandlers.cs:170-243`) never check
`task.Lifecycle`, unlike `AddTaskCommentHandler` (`:78-83`). Plausibly intentional (editing/retracting your own
past remark is arguably harmless on closed work, unlike posting new content) but the module does not document
this asymmetry the way it documents almost every other edge case, so I cannot confirm intent either way.

### Confirmed NOT bugs (checked because the brief specifically asked about this class; recorded so the negative
result is not lost)
- Open→Done is **not** reachable in one hop — `TaskLifecycleService.CanTransition` (`:190-191`) has no
  `Open → Done` edge; `Done` is reachable only from `InProgress`/`PendingReview` (`:197-201`).
- Complete with an incomplete **blocking** checklist item is refused server-side (`TaskItemTransitionHandlers.cs:276-285`),
  using the identical predicate the projection uses (`TaskWorkItemProvider.cs:1626-1628`).
- Start while approval is outstanding is refused server-side via the fail-closed `IWorkflowTransitionGate` call
  (`:311-332`), independent of the projection's disabled hint.
- BL-357 (`DelegationAllowed` asked of the wrong actor) — the example bug given in the brief — is **correctly
  fixed** in the current tree: the flag is checked only for `isHolder && !isRequester`
  (`TaskItemTransitionHandlers.cs:1232-1237`), and the projection's `ReassignAction` mirrors the same condition
  (`TaskWorkItemProvider.cs:1932-1936`).
- Assignment-scope-at-the-write (the class of bug fixed in `01bc0915`) is correctly present today for both
  targets that need it: Create (`CreateTaskItemHandler.cs:172-178`) and Reassign
  (`TaskItemTransitionHandlers.cs:1259-1263`), both via `ITaskAssignmentGuard`.
- Controller `[HasPermission]` vs. `TaskWorkItemActionDispatcher.Permissions` map: all eleven dispatched verbs
  checked pairwise, zero drift found in either direction (Table 3 row 34).
- `Cancel`'s `mayCancelAnyTask`/administrative-authority flag is derived identically (from `Delete`-permission
  JWT claims, never client input) at both of its two entry points (`TasksController.cs:210` and
  `TaskWorkItemActionDispatcher.cs:166-172`).
- `CreateTaskItemCommand.IsScheduledGeneration` (which skips the assignment guard) is verified **not**
  client-reachable — it is a command-only parameter absent from the public request DTO
  (`Commands/TaskItemCommands.cs:37`; controller constructs the command with only two positional args,
  `TasksController.cs:45`).
- `TaskAssigneeEligibility`'s scope-exempt path (used by "waiting on," "decision-makers," approver/reviewer
  candidates) is a deliberate, documented design choice (`TaskAssigneeEligibility.cs:41-43`), not a drift from
  the assignment guard's scope-restricted path used for actual assignment.
