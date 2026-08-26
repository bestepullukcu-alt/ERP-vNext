# DCP-005 — Task ↔ Controlled Document Reference — Delivery Capability Pack

## 1. Identity and status

| | |
|---|---|
| Id | `DCP-005` |
| Name | Task ↔ Controlled Document Reference |
| Status | `draft` — charter agreed with QA, not yet scheduled |
| Owner | Platform / Task Center (this repo) |
| Counterparty | GMG QA Documentation / Records Management |
| Source of agreement | Four rounds of written correspondence, 2026-08-11 → 2026-08-24 |
| Depends on | `MOD-0024` (Task & Checklist Engine) · `MOD-0029` (Document Management) · `DCP-004` (Work Aggregation / Task Center) |

⚠ **This pack exists because the decisions in it were reached in correspondence, not in code.**
Four letters settled a design that nobody can reconstruct from the repository: why the frozen
tuple has six fields and not four, why documents are a lookup and not a table, why folders are
imported but never instantiated. Without this file those reasons live on somebody's desktop.

---

## 2. Business outcome

A person opening a task can see **which controlled procedure governs the work**, and an auditor
opening a closed task can see **which document, at which version, was actually relied upon**.

That is the whole of this capability. Writing evidence back into the quality repository is a
separate capability and is explicitly excluded (§12).

---

## 3. Problem statement

Today a task carries no link to any procedure. The organisation's controlled documents live in
a QMS repository outside this system, and the two have never been connected — so the answer to
"which SOP were you following?" exists only in people's heads and, after the fact, nowhere.

Two attempts to design around this failed before the third succeeded, and both failures came
from the same cause: the counterparty designed against master data this system does not have.

| Round | Assumed | Measured here |
|---|---|---|
| 1 | a business-process master carries the classification | no process entity exists; `TaskItem.ProcessInstanceId` is a recurrence discriminator |
| 2 | a task-type list already exists and is closed | no task type field, enum or entity exists |

The third round put the classification on a task type **this system will build**, which is the
design recorded here.

---

## 4. Capability boundary

**In:** the reference direction — task → document.
**Out:** the evidence direction — task → file → folder. See `DCP-006` when it is written.

The boundary is not arbitrary: the reference direction needs a link, and the evidence direction
needs an attachment store this product does not have (§9).

---

## 5. Member modules

| Module | Role |
|---|---|
| `MOD-0024` | owns the task; gains the task-type object and the document link |
| `MOD-0029` | owns the folder taxonomy import; contributes nothing else here |
| `MOD-0280` | not involved — named only to prevent the recurring assumption that time entry belongs here |

---

## 6. Architecture decisions

These were argued to a conclusion and are not open. Each carries the reasoning, because the
conclusion alone reads as arbitrary.

### 6.1 Documents are a LOOKUP, not a table

The ERP holds a versioned search list of controlled documents. It does **not** hold a
`documents` table.

Three reasons, in the order they carry weight:

1. **Structural, not disciplinary.** If a table exists, somebody eventually corrects a title or
   a version in it — and at that moment a second authority over the document exists. A lookup
   has nothing to correct; a refresh overwrites it.
2. **It makes `ControlledDocument.CollectionInstanceId` moot.** That field is required, which
   would force every referenced document into a provisioned folder. As a lookup, no document
   record exists, so no folder is needed.
3. **36 of 358 documents cannot be linked** (23 planned, 7 void, 6 declared mandatory but absent
   from the master register). Importing them as records would show them in the ERP as though
   they exist.

⚠ **The rejected reasoning matters too.** The first argument offered for the lookup was that it
avoids staleness. It does not: both options put a snapshot in the ERP, and a list ages exactly
as a record does — it merely does not look like something that should be maintained. The real
distinction is snapshot vs live query, and neither option is a live query.

### 6.2 The reference is frozen as SIX fields

```
(document_uid, document_code, title, version, status, referenced_at)
```

Four was the original agreement. Two were added when the lookup model exposed a hole: if the
list refreshes and a title changes or a code is reallocated, a closed task begins showing an old
version number under a new name — the record silently reads wrong.

Code and title are the human-readable identity the user actually relied on. They are part of the
record, and they cost two varchars.

⚠ This is an **extension** of the freezing principle already agreed (frozen on closed tasks,
followed on open ones), not a reversal of it.

### 6.3 Classification lives on the TASK TYPE

```
record_class(task) =
  1. task.record_class_override      ← if chosen at creation
  2. task_type.record_class          ← the main path
  3. department.default_record_class ← from the mapping table
  4. NOT_A_RECORD                    ← default. NOT quarantine
```

The default is deliberately "not a record". The earlier design classified everything and
quarantined what it could not resolve — which collapses here, because manually created tasks are
daily work and quarantine would become the main path rather than the exception.

**Control statement, to be enforced:** *a manually created, unclassified task may not produce a
GxP quality record.* GxP work must come from a task type carrying the classification.

### 6.4 `governing_documents` is TWO layers, not a cross product

```
governing_documents(type, org) = type.group_documents[]        ← always
                               + type.local_documents[org][]   ← sparse
```

24 types × 5 orgs would be 120 cells. The counterparty's own registers are already split this
way — a group layer (24 mandatory SOPs, all entities) and a local layer (7 deliverables × 4
companies) — so the model mirrors their governance rather than inventing a shape.

### 6.5 The taxonomy is imported as DEFINITIONS and stopped there

```
taxonomy CSV → folder DEFINITION      ← stop here
             → publish → approve → mark effective
             → real FOLDER instantiated   ← not yet needed
```

Instantiating now would produce 103 empty folders, visible to users, that nothing can be written
into. The definitions are still worth importing: the taxonomy becomes a versioned object here,
and the evidence capability later needs only the final step.

### 6.6 What the auditor sees where

| In the ERP | In the QMS |
|---|---|
| **which** document was cited | **what** it said |

Taking a UID to the QMS is acceptable. The task screen must show enough to answer "which
procedure did you follow" without leaving:

> `GMG-QMS-SOP-0005 v0.3 · Deviation / Incident Management · Draft · referenced 2026-08-24`

---

## 6.7 Closed lists the task type draws from

⚠ **These were referenced by an earlier prompt as "in this pack" and were not.** The
agent building the task type correctly refused to invent them and left a seam instead.
They come from the counterparty's own template, so they are quoted rather than derived.

### `function_code` — 19 values

| Code | Name |
|---|---|
| `QUA` | Quality Administration |
| `RA` | Regulatory Affairs (operational) |
| `PV` | Pharmacovigilance (operational) |
| `MFG` | Manufacturing Operations |
| `SCM` | Supply Chain |
| `RND` | Research and Development |
| `COM` | Commercial and Marketing |
| `FIN` | Finance and Accounting |
| `HR` | Human Resources |
| `LEG` | Legal and Contracts |
| `PRC` | Procurement |
| `ITG` | Information Technology |
| `ISM` | Information Security |
| `FAC` | Facilities and Assets |
| `EHS` | Environment, Health and Safety |
| `PPM` | Projects and Portfolios |
| `CORP` | Corporate Communications |
| `CTY` | Country Operations (residual) |
| `MED` | Medical Affairs |

### `org_code` — 5 values

| Code | Entity | Country |
|---|---|---|
| `GMG` | Grand Medical Group AG | CH |
| `STD` | Setonda S.L. | ES |
| `MYG` | Miquel y Garriga, S.L. | ES |
| `GPL` | Grand Medical Poland Sp. z o.o. | PL |
| `GMT` | GMG Grand Medical Turkey LTD | TR |

⚠ Third parties never receive an ORG code. Work belonging to one carries the ORG of the
GMG entity holding the contract.

---

## 7. Ordered delivery sequence

⚠ **Task type is first and is not optional.** It is the carrier for the classification, the
domain, the quality-event flag and the governing documents. Nothing else in this pack can be
built before it.

| # | Slice | Notes |
|---|---|---|
| 1 | **Task type** — entity, management screen, 7 languages, permission | 31 seed types supplied with all four columns filled; only the container is ours |
| 2 | **Document lookup** — versioned import of the reference list | 358 rows; the task stores which list version it resolved against |
| 3 | **The link** — task → document, the six frozen fields, status display | governing documents from the type first and uncloseable; manual search always available |
| 4 | **Taxonomy import** — definitions only | dry-run already passes; independent of 1–3, can run any time |

---

## 8. Prerequisites

| # | Prerequisite | Owner | Status |
|---|---|---|---|
| 1 | Our own folder-name minimum length raised from 3 | this repo | measured; 14 rows fail on `HR` today |
| 2 | Department → FUNCTION mapping filled | this repo | template supplied |
| 3 | Task type list reviewed against the 31-row seed | this repo | seed is `PROPOSED — QA adoption required` |
| 4 | Six unregistered mandatory SOPs entered in the register | QA | open finding on their side |

---

## 9. Explicit exclusions

**The evidence direction is not in this pack**, and the reason is measured: there is no
attachment store in this product. No entity, no blob storage, and the `attachments` capability is
emitted by none of the live work items. The counterparty's "Phase A" — every attachment filed as
`NOT_A_RECORD`, undeletable, audit-trailed, exportable three ways — requires no new module on
their side and **is an entire module on ours**.

Also excluded: quarantine, the disposition workflow, retention purging, the three audit exports,
and folder instantiation.

---

## 10. Open decisions

| # | Decision | Holder |
|---|---|---|
| 1 | When this capability is scheduled relative to the Task Center's remaining work | product |
| 2 | Whether the local document layer waits for `LOG-0009` (all 26 rows are NOT STARTED) | QA |

---

## 11. Known consequences, recorded now

**9 of the 31 task types cannot show a governing document today** — `DEV-GMP`, `DEV-GDP`,
`BATCH-RELEASE`, `SPEC-CONTROL`, `VAL-QUAL`, `PQR`, `GDP-OPS`, `REG-VARIATION`, `ARTWORK`. Their
governing SOPs are declared mandatory and have UIDs allocated but no row in the master register.

The types still work — classification, folder rule and quality-event flag all function. What is
missing is the sentence the user needs: a GMP batch-release task can be opened and filed
correctly and cannot tell the operator which SOP to follow.

**No document in the register is Effective** — 320 draft, 23 planned, 7 void, 0 effective. Phase 1
therefore permits referencing any version and shows its status, in wording agreed with QA:

> *"Taslak sürüm — en güncel hâli. Yürürlük onayı GQMS'te beklemede."*

The wording is deliberately not an alarm: every document will carry it, and a warning shown
everywhere is ignored within days.

---

## 12. Agreed and closed — do not reopen without new measurement

Lifecycle folder mapping · reference is linked, never copied · no approval workflow is built in
the ERP · version freezing (frozen on closed, followed on open) · retention belongs to the record
class and the longer wins · legal hold overrides · no cascade delete of attachments when a task is
purged · the user is never shown a folder tree · quarantine is the user's objection route, not the
system's failure path · the creator chooses the destination once and the doer may object.

---

*Sources: four QA/ERP letters (2026-08-11, 2026-08-11 round 2, 2026-08-24 round 3, 2026-08-24
round 4) · `GMG_ERP_Folder_Taxonomy_v2` · `GMG_ERP_Document_Reference_List` (358 rows) ·
`GMG_ERP_Task_Type_Seed` (31 types) · live measurements taken in this repository 2026-08-24.*
