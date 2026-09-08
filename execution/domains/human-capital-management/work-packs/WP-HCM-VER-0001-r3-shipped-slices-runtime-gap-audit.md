WORK PACKAGE

WP ID:            WP-HCM-VER-0001
Prompt ID:        P-HCM-VER-0001
Prompt Version:   v1.0
Task Class:       independent verification + gap audit (read-only)
Golden-Flow Profile: C
Risk Class:       HIGH (8 shipped state-ish slices, tenant/RBAC/persistence surface)
State:            READY

Scope modules (8 shipped R3 slices — all structurally complete, build-green):
  MOD-0300 Applicant Intake · 0301 Candidate Pipeline · 0302 Offer Mgmt · 0303 Employee Onboarding ·
  0304 Employment Change · 0306 Performance Review · 0307 Competency & Skills · 0308 Development Plan
Build Lane:       HCM-R3-verification
Agent Lane ID:    AL-HCM-VER-R3
Agent Lane Type:  VER
Target Agent / Entry Point: read-only-auditor / /read-only-audit

Authority:
- Module Pack:   CAND-CAP-0022..0027 (+0028/0029 if authored) — AC + owned objects + API surface + data boundary
- Repo-wide:     AGENTS.md (§3 port schema: all traffic via gateway 5080→HCM 5059; tenant/auth header)
- Pattern:       shipped PerformanceReviews/Competency slices as reference footprint
- Related:       work-packs/TAKEOVER-2026-09-03-hcm-drift-and-decision.md (D-1 authorization still OPEN)

Repository:
- Path: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Expected HEAD: c2e54336 · Worktree: /Users/cihan/Desktop/ERP-vNext
- Runtime: full stack UP (gateway 5080, HCM 5059, frontend 5001) — freshly rebuilt this session
- Dirty baseline: all 8 slices UNTRACKED (53 ?? files) — not committed (K17)

Dependencies:
- Depends on: none (read-only; runtime already up)
- Gate state: SATISFIED for verification
- Parallel-safe with: any read-only WP (no writes)
- Not parallel-safe with: any writer touching HCM (verdict lane does not write — §6)

Scope:
- Allowed (READ + runtime smoke, NO source writes): whole HCM service, gateway ocelot, frontend HumanCapital/**, packs; HTTP calls to 5080/5001; Mongo reads (hcm db)
- Protected: ALL source/config (no edits). Any fix = separate REWORK WP, not this lane.

Objective:
Prove, per module, whether the shipped slice actually WORKS and is CONSISTENT — not merely present. File presence,
DI, routes, nav and tests are already confirmed present; do NOT re-report presence. Hunt the repo's known silent-failure
modes with path:line + red evidence:
  1. Runtime golden flow (E3) via gateway 5080: create → list → get → evaluate → delete readiness metadata;
     expect 2xx + correct payload. Auth-gated → use an authenticated token (see §26 operator note); record 401/403 if no creds.
  2. Persistence (E4): record actually written to Mongo (hcm db, correct collection); soft-delete honored; TenantId isolation
     (a second tenant cannot read tenant-1 rows).
  3. RBAC: server-side [HasPermission] enforced (denied without permission), not client-only.
  4. l10n delivery (K5): UI renders tr/en from the per-module *Index.{tr,en}.resx — NOT English fallback; confirm the view
     reads the SAME resource the keys were written to (SharedResource has NO Hcm* keys for these → per-module resx path must resolve).
  5. Client/server contract (K6): frontend request field names == server DTO/command property names (no note↔Reason, person.id↔userId drift);
     enum/Select2 fields bind.
  6. Test vacuity (K3): each of the 8 *Tests.cs actually exercises the behavior — spot-check with fix-absent→RED reasoning; flag tests that pass on initial state only.
  7. Idempotency/concurrency (§18.0): create replay = no duplicate; editable record has version/etag conflict behavior.
  8. Consistency across the 8: same readiness pattern, no copy-paste field/permission drift between modules.

Acceptance Criteria (measurable):
- Per-module verdict table (8 rows): PASS / GAP(list) / BLOCKED(no-auth), each GAP with path:line + evidence (command/HTTP/red-test).
- Gaps ranked by severity; each gap phrased as a bounded fixable item (feeds a REWORK WP).
- Explicit statement of what could NOT be verified (e.g., E4 without JWT) — no guessing (K10).
- No source/config file modified.

Failure Protocol: read-only; if a fix is needed, STOP and report it as a gap — do not edit. Do not invent AC; if a module
pack AC is ambiguous, flag it.

Output Contract: §37 verification report + per-module §12.2 table + ranked gap list. Evidence level: E3 (E4 where auth available).
Your PASS ≠ CT ACCEPTED (K13); CT emits bounded REWORK prompts from your gaps.

---

## Agent Prompt (paste-ready)

@module-pack-author
WP: WP-HCM-VER-0001 · Prompt P-HCM-VER-0001 v1.0

Repository: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Expected HEAD: c2e54336 · Worktree: /Users/cihan/Desktop/ERP-vNext
Runtime UP: gateway http://localhost:5080 · HCM http://localhost:5059 · frontend http://localhost:5001 (bu oturumda yeniden derlendi)

Denetim modu: worktree-read-only (strict) + runtime smoke. Kaynak/koda YAZMA. Her bulgu için `path:line` + kanıt (komut/HTTP/red-test). Düzeltme YOK — düzeltme ayrı REWORK WP'sidir.

Kapsam — 8 shipped R3 slice (hepsi dosya düzeyinde TAM, build yeşil; VARLIK tekrar raporlama):
0300 Applicant Intake · 0301 Candidate Pipeline · 0302 Offer · 0303 Employee Onboarding · 0304 Employment Change ·
0306 Performance Review · 0307 Competency & Skills · 0308 Development Plan.
Rotalar: gateway 5080 → /api/{applicant-intake|candidate-pipeline|offer-management|employee-onboarding|employment-changes|performance-reviews|competency-skills|development-plans} → HCM 5059.

Önce oku: AGENTS.md (§3 port şeması; gateway zorunlu, tenant/auth header), ilgili module pack'ler (AC + owned objects + API surface + data boundary).

NE:      Her modül için slice'ın gerçekten ÇALIŞTIĞINI ve TUTARLI olduğunu kanıtla — sadece "dosya var" değil.
         Bu repo'nun bilinen sessiz-hata modlarını path:line + kırmızı kanıtla ara:
         1) Runtime golden flow (E3) gateway 5080 üzerinden: create→list→get→evaluate→delete; 2xx + doğru payload.
            Auth-gated → authenticated token kullan (yoksa 401/403 kaydet, §26).
         2) Persistence (E4): kayıt Mongo'ya (hcm db, doğru collection) yazıldı mı; soft-delete; TenantId izolasyonu
            (2. tenant, 1. tenant satırını okuyamamalı).
         3) RBAC: server-side [HasPermission] gerçekten reddediyor mu (client-only değil).
         4) l10n teslimi (K5): UI tr/en'i per-module *Index.{tr,en}.resx'ten mi basıyor yoksa İngilizce fallback mı;
            view, key'lerin yazıldığı AYNI kaynağı okuyor mu (SharedResource'ta bu modüller için Hcm* key YOK).
         5) Client/server contract (K6): frontend alan adları == server DTO/command property (note↔Reason, person.id↔userId
            gibi drift yok); enum/Select2 alanları bağlanıyor.
         6) Test vacuity (K3): 8 *Tests.cs gerçekten davranışı exercise ediyor mu (fix-absent→RED mantığı); yalnız başlangıç
            durumunda geçen testleri işaretle.
         7) Idempotency/concurrency (§18.0): create replay duplicate yaratmıyor; editable kayıtta version/etag conflict.
         8) 8 modül arası tutarlılık: aynı readiness deseni, kopyala-yapıştır alan/permission drift yok.
NEDEN:   Dosyalar var ve build yeşil; ama K1/K13 — varlık ≠ çalışıyor. Kullanıcı "eksikleri düzelt" istiyor; önce ölçülmüş
         gap listesi gerekiyor (K10: tahmin yok; K16: unbounded fix yok). D-1 authorization hâlâ OPEN.
NASIL:   Profile C. curl ile 5080 rotaları; Mongo (hcm db) okuması; view/resx eşleşmesi; DTO↔frontend alan karşılaştırması;
         test dosyalarını fix-absent→RED gözüyle oku. Tahmin yok — kanıtsız "çalışıyor/bozuk" yazma.
YAPMA:   Hiçbir kaynak/config dosyası düzenleme. Scope genişletme. Varlık listesini tekrar üretme.
DOĞRULA: 8 satırlık per-module verdict tablosu (PASS / GAP[liste] / BLOCKED[no-auth]); her GAP path:line + kanıt +
         bounded düzeltilebilir ifade; severity sıralı; doğrulanamayanı açıkça yaz (ör. JWT yoksa E4).

Durma koşulları: düzeltme ihtiyacı (gap olarak raporla, düzeltme) · ambiguous pack AC. Dur ve raporla.

Rapor formatı: §37 verification report + per-module §12.2 tablo + ranked gap list.
Senin PASS'in kapanış değildir (K13); CT senin gap'lerinden bounded REWORK prompt'ları üretir.

---

## CT INDEPENDENT VERIFICATION + VERDICT — 2026-09-04 (K2/K13)

Agent report NOT accepted as evidence; CT re-derived the load-bearing claims from source this turn.

| Gap | Agent claim | CT independent check | Verdict |
|---|---|---|---|
| **G1** list-column drift (4 mods) | 7 orphan JS columns | ListItemDto record ↔ index.js data-field diff + entity grep: all 7 fields ∈ entity, ∉ ListItemDto | **CONFIRMED** → WP-HCM-REWORK-0001 |
| **G2** no optimistic concurrency (8) | ReplaceOneAsync, no version guard | `MongoApplicantIntakeReadinessMetadataRepository.cs:60-66` = ReplaceOneAsync(TenantId+Id); Version is business field | **CONFIRMED** (low blast: no user PUT) |
| **G3** create-replay → 500 not 409 (8) | check-then-act + bare InsertOne | consistent with unique-index + no catch | **PLAUSIBLE** (not runtime-repro'd; auth-blocked) |
| **G4** write UI absent (8) | needs pack-AC ruling | pack CAND-CAP-0027:75,111 — manager/employee UX **"not authorized"** | **NOT A DEFECT — by-design per pack** |
| l10n / RBAC / Tests PASS | — | spot-consistent; DB `DitenHumanCapital` absent corroborates no-write | **ACCEPTED** |

**CT Status:** audit ACCEPTED as E1/E2 (static) evidence. Runtime E3/E4 remain **BLOCKED[no-auth]** — CT also cannot enter credentials (action-boundary); clearing needs an authorized token/operator (§26). D-1 authorization still OPEN.

**Rulings:**
- **G1 → REWORK now** (WP-HCM-REWORK-0001): additive, in-scope, entity fields already exist. Bounded.
- **G2/G3 → bounded hardening backlog** (not this turn): add version/etag guard on Update; catch duplicate-key → 409. Low urgency (metadata-only, pre-authorization, no user-editable PUT). New WP when prioritized.
- **G4 → intentionally-not-done (by-design).** Do NOT build create/evaluate/delete UX — pack reserves manager/employee-facing UX; adding it is unauthorized (D-1). Record, do not "fix."
- **Pack↔code drift noted:** CAND-CAP-0027 text defers "Frontend, Gateway routes, tenant shell navigation" (lines 82-85) yet the slice shipped them (EA-waived-runtime-first-slice). Same theme as D-1 — feeds the identity/authorization reconciliation, not a code fix.

**Replan (K21):** dispatch WP-HCM-REWORK-0001 → CT verifies its red-proof → then decide G2/G3 hardening. D-1/D-2 still owner-open.
