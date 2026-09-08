WORK PACKAGE — per-module CRUD runtime test + bounded gap-fix

WP ID:            WP-HCM-VER-0002
Prompt ID:        P-HCM-VER-0002
Prompt Version:   v1.0
Task Class:       runtime verification (E3/E4) + bounded gap remediation
Golden-Flow Profile: A (state-changing CRUD)
Risk Class:       MEDIUM
State:            READY
Target Agent / Entry Point: frontend-ui-ux + testing (paste-block leads with @module-pack-author)

Scope — 8 READINESS modules (all now have golden-compact CRUD scaffolding, measured):
  Applicant Intake · Candidate Pipeline · Offer Management · Employee Onboarding · Employment Changes ·
  Performance Reviews · Competency Skills (reference) · Development Plans.
OUT OF SCOPE (owner decision 2026-09-07 — read-only by design, DO NOT add CRUD or modify):
  Employee Projections · HR Sensitive Access · Position Assignments · Offboarding Cases.

Repository: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Worktree: /Users/cihan/Desktop/ERP-vNext
Runtime UP (9/9): frontend http://localhost:5001, gateway 5080, HCM 5059. Login: /account/login?tenantId=00000000-0000-0000-0000-000000000001 (admin@diten.com / Admin123!). USE localhost.
Reference standard: Competency Skills page (accepted golden-compact CRUD) + GoldenReferenceCompact.

Objective:
Test each of the 8 readiness modules ONE BY ONE at runtime and fix any gap found so every module matches the
Competency golden-compact CRUD standard. Per module:
  1. Open page: NO reload loop, NO console errors, list loads (200).
  2. "+ Yeni Ekle" → create page (real DTO fields) → save → POST → new row appears in list.
  3. Row Evaluate → POST /{id}/evaluate → state updates. Row Delete → DELETE /{id} → row gone (soft-delete).
  4. Persistence (E4): Mongo DitenHumanCapital collection gets the doc; delete sets IsDeleted=true; tenant isolation.
  5. Golden-compact visual parity + l10n tr/en real Turkish (no English fallback) + no raw JSON/exception text in UI.
Fix any GAP inline (bounded), using Competency + the clean modules as the pattern. Add/extend the module's handler
test with fix-absent→RED where a code gap is fixed (K3).

Scope guard:
- Allowed (WRITE): the 8 readiness modules' frontend (Controllers/Views/js/resx) + their Application/tests where a
  measured gap requires it. 
- Protected: the 4 governance modules, backend contracts/entities (don't invent fields, K12), gateway/ocelot, packs,
  dt-defaults.js/auth (already fixed), _LayoutTenantShell.

Acceptance Criteria:
- Per-module status table (8 rows): PASS / GAP(list) with path:line + evidence (HTTP codes, screenshots, Mongo before/after).
- Every gap either fixed (with red-proof) or reported as blocked with reason.
- No governance-module or backend-contract change.
- Build green; no reload loop; no console errors on any of the 8 pages.

Failure Protocol: missing DTO field/endpoint → STOP, report (don't invent). Reserved manager/employee UX → don't add.
Don't touch the 4 read-only governance modules.

Output: §22 report + 8-row per-module table + E3/E4 evidence + list of fixes with red-proof. PASS ≠ CT ACCEPTED (K13).

---

## Agent Prompt (paste-ready)

@module-pack-author
WP: WP-HCM-VER-0002 · Prompt P-HCM-VER-0002 v1.0

Repository: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Worktree: /Users/cihan/Desktop/ERP-vNext
Runtime UP (9/9): frontend http://localhost:5001 · gateway 5080 · HCM 5059. Login: http://localhost:5001/account/login?tenantId=00000000-0000-0000-0000-000000000001 (admin@diten.com / Admin123!). localhost KULLAN.
Referans standart: Competency Skills sayfası (kabul edilmiş golden-compact CRUD) + GoldenReferenceCompact.

Kapsam — 8 READINESS modülü (hepsinde CRUD scaffold var, tek tek TEST + boşlukları DÜZELT):
Applicant Intake · Candidate Pipeline · Offer Management · Employee Onboarding · Employment Changes ·
Performance Reviews · Competency Skills · Development Plans.
KAPSAM DIŞI (sahibi salt-okunur dedi — DOKUNMA): Employee Projections · HR Sensitive Access · Position Assignments · Offboarding Cases.

NE:      Her readiness modülünü TEK TEK runtime test et ve her boşluğu Competency standardına getir. Modül başına:
         1) Sayfayı aç: reload döngüsü YOK, konsol temiz, liste 200.
         2) "+ Yeni Ekle" → create sayfası (GERÇEK DTO alanları) → kaydet → POST → yeni satır listede.
         3) Satır Evaluate → POST /{id}/evaluate → durum değişir; satır Delete → DELETE /{id} → satır gider (soft-delete).
         4) Persistence (E4): Mongo DitenHumanCapital collection'a kayıt; delete'te IsDeleted=true; tenant izolasyonu.
         5) Golden-compact görsel parite + l10n tr/en gerçek Türkçe + UI'da ham JSON/exception METNİ yok.
         Boşluğu yerinde DÜZELT (bounded); Competency + temiz modülleri desen al. Kod boşluğu düzeltirken modülün
         handler testine fix-absent→RED ekle (K3).
NEDEN:   CRUD kodlandı ama hiçbiri CT-doğrulanmadı (K13: dosya var ≠ çalışıyor). Kullanıcı tek tek test istiyor.
NASIL:   Tarayıcıda tıklayarak + Network + Mongo. Eksik alan/uç varsa uydurma (K12) — dur, raporla.
YAPMA:   4 governance modülü, backend contract/entity, gateway/ocelot, pack, dt-defaults/auth, _LayoutTenantShell'e dokunma.
         Reserved manager/employee UX ekleme.
DOĞRULA: 8 satırlık per-module tablo (PASS / GAP[liste] + path:line + kanıt: HTTP kodları, ekran görüntüsü, Mongo öncesi/sonrası);
         her gap ya düzeltildi (red-proof) ya blocked+sebep; build yeşil; hiçbir sayfada reload döngüsü/konsol hatası yok.

Durma koşulları: eksik DTO alan/uç · reserved UX · governance modülü ihtiyacı. Dur ve raporla.

Rapor: §22 + 8 satırlık tablo + E3/E4 kanıtı + düzeltmeler (red-proof).
Senin PASS'in kapanış değildir (K13); CT her modülü bağımsız doğrular.
