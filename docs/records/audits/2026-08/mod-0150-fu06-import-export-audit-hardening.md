# MOD-0150-FU06 — Import / Export / Audit Hardening

**Date:** 2026-07-20 · **Service:** `Diten.CrmService` · **Verdict:** **PASS**

Contact / AccountContactLink / AccountRelationship **import** (JSON rows in, validated result summary, dry-run),
**export** and **import-template** (CSV out), MOD-0048 reference validation during import, and a **HTTP-ready
MOD-0021 audit seam** (fail-soft, PII-safe, opt-in). No bulk-merge/dedup engine, no async job queue, no Consent/Zone.

---

## 1. Scope delivered

| Capability | Contact | AccountContactLink | AccountRelationship |
|---|---|---|---|
| Import (JSON rows → `ImportResultDto`) | ✅ | ✅ | ✅ |
| Dry-run / validate-only | ✅ | ✅ | ✅ |
| MOD-0048 reference validation (cached) | contact-type, contact-status | contact-role | account-relationship-type/status |
| Existence resolution (id **or** code/external) | external-ref dup guard | account by id/code, contact by id/external | source/target account by id/code |
| Conflict rows (not 409) | duplicate external-ref | duplicate link, 2nd-primary | self-link, bidirectional-reverse, duplicate |
| Export (CSV) | ✅ | ✅ | ✅ |
| Import template (CSV header) | ✅ | ✅ | ✅ |

## 2. Design decisions

- **Import transport = JSON rows; export/template = CSV.** Import takes a typed JSON body (`{dryRun, rows[]}`) so the
  API stays strongly-typed and validation errors map to row numbers; export/template emit `text/csv` (dependency-free
  RFC-4180 writer, `Csv.Build`) so results round-trip into the template.
- **Reference validation cached per distinct `(setCode, value)`** (`ReferenceCache`) — a bulk import hits the Gateway
  published-values endpoint once per distinct value, never per row, and **never** falls back to a local list. Invalid /
  unpublished values become per-row errors (`invalid_reference` / `set_missing`), consistent with the single-write path.
- **Conflicts are row-level, not HTTP 409.** Duplicate external-ref, duplicate/2nd-primary link, self-link and the
  bidirectional-reverse pair are reported as `ImportRowErrorDto{Severity="conflict"}` rows and counted in
  `ConflictRows`, so a partial import proceeds for the valid rows instead of aborting the batch.
- **Relationship direction is derived from MOD-0048 metadata** (reused `IReferenceMetadataReader` + `RelationshipTypeMetadata`),
  metadata cached per distinct type; self-link allowed only when the type's `selfAllowed` attribute says so.
- **MOD-0021 audit seam made HTTP-ready.** `HttpCrmAuditPublisher` forwards Account/Contact/import/export events to the
  governed append contract (`POST /api/v1/platform/audit/events`, mirrors `GovernedHcmAuditAppendClient`). It is
  **fail-soft** (an unavailable/erroring audit dependency is logged and swallowed — audit is a side effect, never a gate)
  and **PII-safe** (metadata carries only the counts/correlation `detail` string, never row payload or credentials).
  It is **opt-in** via `Crm:Audit:Mode=http`; the default structured-logging seam is unchanged, so this FU does not alter
  live runtime behaviour until the flag is flipped.

## 3. Files

**Application** (`Features/ImportExport/`): `ImportExportModels.cs` (rows + `ImportResultDto`/`ImportRowErrorDto`),
`ImportExportCommands.cs`, `Csv.cs`, `ImportTemplates.cs`, `Handlers/ContactImportExportHandlers.cs`
(`ReferenceCache`, `ImportContactsHandler`, `ExportContactsHandler`), `Handlers/AccountContactImportExportHandlers.cs`,
`Handlers/AccountRelationshipImportExportHandlers.cs`.
**Infrastructure:** `Audit/HttpCrmAuditPublisher.cs` (new), `DependencyInjection.cs` (audit-mode toggle, now takes `IConfiguration`).
**Api:** `Controllers/CRM/ImportExportController.cs` (new), `Program.cs` (`AddInfrastructure(builder.Configuration)`).
**Domain repositories (new methods):** `IAccountRepository.GetByCodeAsync`, `IContactRepository.ListAllAsync`,
`IContactExternalReferenceRepository.GetBySourceExternalAsync`, `IAccountContactLinkRepository.ListAllAsync`,
`IAccountRelationshipRepository.ListAllAsync` (+ Mongo impls).
**Tests:** `ImportExportTests.cs` (new, 11 tests); fakes extended in the four existing CRM test files + `ScaffoldSmokeTests`.

## 4. Endpoints (under existing Gateway wildcards — no new Gateway route)

| Method | Path | Permission |
|---|---|---|
| POST | `/api/crm/contacts/import` | `crm.contact.import` |
| GET | `/api/crm/contacts/export` | `crm.contact.export` |
| GET | `/api/crm/contacts/import-template` | `crm.contact.import` |
| POST | `/api/crm/accounts/contact-links/import` | `crm.account-contact.manage` |
| GET | `/api/crm/accounts/contact-links/export` | `crm.account-contact.read` |
| GET | `/api/crm/accounts/contact-links/import-template` | `crm.account-contact.manage` |
| POST | `/api/crm/accounts/relationships/import` | `crm.account-relationship.manage` |
| GET | `/api/crm/accounts/relationships/export` | `crm.account-relationship.read` |
| GET | `/api/crm/accounts/relationships/import-template` | `crm.account-relationship.manage` |

`account-contacts` / `account-relationships` have **no** dedicated Gateway route, so the import/export endpoints nest under
the `/api/crm/accounts/{everything}` wildcard (`contact-links/*`, `relationships/*`) — same pattern as FU03/FU04.

## 5. Verification

- **Unit/handler tests:** `dotnet test` → **72/72 passing** (61 prior + 11 new FU06). Api build **0 warnings / 0 errors**.
- **New tests:** contact import dry-run (no persist) · actual create + external-ref · invalid-reference error row ·
  duplicate-external conflict row · contact export header+row · account-contact resolve-by-code/external + create ·
  unknown-account-code not_found · relationship self-link blocked · bidirectional reverse-pair conflict (no insert) ·
  relationship actual create with derived direction.
- **Live Gateway smoke (5000):** all **9 FU06 routes return 401** unauthenticated — routed through the wildcard **and**
  guarded by `[Authorize]`/`[HasPermission]` (401, never 404). Proves route wiring + auth/permission gates end-to-end.
- **Guard greps:** no hardcoded reference-value fallback lists; reference checks only via validator/metadata seam; no
  token/password/secret literals in FU06 files; audit path is `catch … fail-soft`.

## 6. Boundary (explicitly NOT in scope)

No bulk merge / fuzzy dedup engine; no async/background import job queue (synchronous request-scoped); no file-upload
parsing (client transforms CSV→JSON rows); no Consent capture, no Zone/territory. Export emits ids (+ blank code/external
columns) for round-trip; code/label enrichment on export is a later nicety.

## 7. Open items

1. **Authenticated happy-path smoke** — dry-run summary + actual import + CSV round-trip against 97c5 requires a runtime
   bearer token (read only via runtime input; not guessed/stored). Route+guard wiring already proven via 401.
2. **`Crm:Audit:Mode=http` runtime cutover** — the HTTP forwarder is implemented, registered and fail-soft but ships
   **off**; flipping the flag in a fleet run (and confirming append acceptance) is the follow-up.
