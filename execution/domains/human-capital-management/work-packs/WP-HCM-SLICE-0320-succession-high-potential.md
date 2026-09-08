WORK PACKAGE — full end-to-end module slice (pack + backend + API + frontend CRUD + test)

WP ID:            WP-HCM-SLICE-0320
Prompt ID:        P-HCM-SLICE-0320
Prompt Version:   v1.0
Task Class:       full module slice (greenfield)
Golden-Flow Profile: A (state-changing CRUD)
Risk Class:       MEDIUM
State:            READY
Target Agent / Entry Point: @orchestrator / module-pack-author + backend-architect + frontend-ui-ux (paste-block leads @module-pack-author)

Module: Succession & High-Potential Tracking · planning id MOD-0320 · proposed alias CAND-CAP-0031 · resource slug `succession` · service Diten.HumanCapitalService (5059)
Reference (proven pattern): CompetencySkills (0307) + LearningTraining (0309) end-to-end.
Sequence: last of R3-C. Next after this: MOD-0310 Workforce Planning (R3-D).

Repository: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Worktree: /Users/cihan/Desktop/ERP-vNext
Runtime UP (9/9): frontend http://localhost:5001, gateway 5080, HCM 5059. Login: /account/login?tenantId=00000000-0000-0000-0000-000000000001 (admin@diten.com / Admin123!). USE localhost.

Scope: new CAND-CAP-0031 pack; HCM backend for Succession; gateway ocelot (succession routes → 5059); auth DataSeeder
(hcm.succession.* permissions); frontend HumanCapital/Succession golden-compact CRUD + nav; HCM tests.
Protected: other modules, 4 read-only governance modules, dt-defaults/auth, other services, existing packs/registry rows.

Objective: build MOD-0320 as a metadata-only readiness slice, end-to-end, shape-identical to LearningTraining (0309).
Fix-forward: in the new module's Guard, forbidden-marker matching must use token/word-boundary (NOT bare substring
Contains) so legitimate values like "taxonomy"/"succession" are not false-rejected (the systemic "tax"-substring bug
in existing guards is a separate REWORK — do NOT copy it into this new module).

Acceptance (E3/E4): build green; login → "Succession / High-Potential" nav visible; page opens no reload loop; +New →
create → list → evaluate → delete; Mongo DitenHumanCapital collection doc + soft-delete; l10n tr/en real Turkish;
console clean; list DTO ↔ JS columns match (no drift). PASS ≠ CT ACCEPTED (K13).

---

## Agent Prompt (paste-ready)

@module-pack-author
WP: WP-HCM-SLICE-0320 · Prompt P-HCM-SLICE-0320 v1.0

Repository: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Worktree: /Users/cihan/Desktop/ERP-vNext
Runtime UP (9/9): frontend http://localhost:5001 · gateway 5080 · HCM 5059. Login: http://localhost:5001/account/login?tenantId=00000000-0000-0000-0000-000000000001 (admin@diten.com / Admin123!). localhost KULLAN.

Modül: Succession & High-Potential Tracking · planning id MOD-0320 · önerilen alias CAND-CAP-0031 · resource slug `succession` · servis Diten.HumanCapitalService (5059).
REFERANS (kanıtlanmış desen — kopyala/uyarla): LearningTraining (0309) + CompetencySkills (0307) uçtan uca.

Önce oku:
1. module-packs/CAND-CAP-0030-learning-training-records.md (en yeni pack ŞABLONU) + CAND-CAP-0028
2. Backend referans: services/.../Application/Features/LearningTraining/** + Domain/Entities/LearningTrainingReadinessMetadata.cs + Api/Controllers/Hcm/LearningTrainingController.cs
3. Frontend referans: frontend/Diten.Web/{Controllers/LearningTrainingController.cs, Views/HumanCapital/LearningTraining/**, wwwroot/assets/js/HumanCapital/LearningTraining/**}
4. AGENTS.md (§3 port; API gateway 5080) · services/Diten.AuthService/.../Seed/DataSeeder.cs (izin seed) · gateway/Diten.ApiGateway/ocelot.json (learning-training rota bloğu) · _LayoutTenantShell.cshtml (learning-training nav bloğu)

NE: MOD-0320'yi 0309 deseniyle UÇTAN UCA, metadata-only readiness slice olarak kur:
    a) PACK: CAND-CAP-0031-succession-high-potential.md (draft, §20 waiver K19-tam).
    b) BACKEND: SuccessionReadinessMetadata (+ ReadinessState enum, repo, Mongo repo `hcm_succession_readiness`, DI),
       Application/Features/Succession (Commands/Queries/Handlers/Guard/Models), Api/Hcm/SuccessionController (read/manage/evaluate/audit.read).
       Succession-temalı boundary/dependency/governance alanları (pool/high-potential/nomination/readiness); para alanı YOK.
    c) API+İZİN: ocelot'a /api/succession rotaları → 5059; DataSeeder'a hcm.succession.{read,manage,evaluate,audit.read}.
    d) FRONTEND: golden-compact CRUD + nav ("Succession / High-Potential", Perms.Has("hcm.succession.read")) + SharedResource menü anahtarı; l10n tr/en gerçek Türkçe.
    e) TEST: SuccessionTests.cs (fix-absent→RED, K3).
NASIL: List DTO'daki alanlar = JS kolonları (drift YOK). Response<T> zarfı. Guard forbidden-marker'ı **token/word-boundary**
       eşleştir (bare `Contains` substring DEĞİL) — "taxonomy"/"succession" gibi meşru değerler yanlış reddedilmesin.
       Auth restart sonrası hcm.succession.* izinlerinin SuperAdmin'e düştüğünü doğrula.
YAPMA: Backend contract uydurma (K12); reserved manager/employee UX; governance modülleri/dt-defaults/auth/diğer modüllere dokunma.
DOĞRULA: Build yeşil; login → nav'da Succession görünür; sayfa reload döngüsüz; +Yeni Ekle → create → liste → evaluate → delete;
    Mongo kayıt + soft-delete; l10n gerçek Türkçe; konsol temiz; list↔JS drift yok. E3/E4 kanıtı + ekran görüntüsü.

Durma koşulları: eksik contract/alan · reserved UX · scope dışı. Dur ve raporla.
Rapor: §22 + oluşturulan/değişen dosyalar + izin seed diff + E3/E4 kanıtı + golden-compact ekran görüntüsü.
Senin PASS'in kapanış değildir (K13); CT bağımsız doğrular, sonra sıradaki modül (MOD-0310 Workforce Planning).
