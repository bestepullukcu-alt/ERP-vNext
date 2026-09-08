WORK PACKAGE — full end-to-end TEP module slice (R4 program start)

WP ID:            WP-TEP-SLICE-0336
Prompt ID:        P-TEP-SLICE-0336
Prompt Version:   v1.0
Task Class:       full module slice (greenfield, TEP domain)
Golden-Flow Profile: A
Risk Class:       MEDIUM
State:            READY
Target Agent / Entry Point: @orchestrator (paste-block leads @module-pack-author)

Program context (SoR record):
- HR R3 COMPLETE (all HCM modules built, CAND-CAP-0006-0040). TEP R2 MVP built (CAND-CAP-0011-0021).
- Analytics backbone (MOD-0004, 0059-0064) DEFERRED — owner decision 2026-09-07: not HCM; belongs to a
  Data-&-Knowledge / Enterprise-Control-Points domain that does not exist in the repo; HR façade 0312 handles absence.
- R4 TEP completion (0332-0351) NOT built = remaining program. This WP starts it.

Module: Talent Data Foundation · planning id MOD-0336 · proposed alias CAND-CAP-0041 · slug `talent-data-foundation` · domain talent-ecosystem-platform · service Diten.TalentEcosystemService (port 5060)
Reference (proven TEP pattern): the R2 MVP TEP modules — e.g. CandidateProfiles/ReferenceExchange readiness slices in
services/Diten.TalentEcosystemService/src/** + packs CAND-CAP-0017/0019/0021. NOT the HCM service.
Sequence: R4-B foundation (underpins 0332-0334 risk, 0337-0349). Next after this: MOD-0332 Hiring Risk Indicators.

Repository: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Worktree: /Users/cihan/Desktop/ERP-vNext
Runtime UP (9/9): frontend 5001, gateway 5080, TEP 5060. Login: /account/login?tenantId=00000000-0000-0000-0000-000000000001 (admin@diten.com / Admin123!). USE localhost.

Scope: new CAND-CAP-0041 pack (talent-ecosystem-platform domain); TEP backend (Domain/Application/Persistence/Api)
for TalentDataFoundation; gateway ocelot (talent-data-foundation routes → 5060); auth DataSeeder (tep.talent-data-foundation.*
permissions); frontend TEP area golden-compact CRUD + nav (TEP nav section); TEP tests.
Protected: HCM domain/service, other TEP modules, dt-defaults/auth, other services, existing packs/registry rows.

Objective: build MOD-0336 as a metadata-only readiness slice, end-to-end, following the TEP R2 MVP pattern (in the
TalentEcosystemService, NOT HCM). Guard forbidden-marker = token/word-boundary (not bare substring). List DTO ↔ JS
columns must match (no drift). Metadata-only; no PII-heavy/credential/raw-payload persistence; no money-as-float.

Acceptance (E3/E4): build green; login → TEP nav shows "Talent Data Foundation"; page opens no reload loop; +New →
create → list → evaluate → delete; Mongo (TEP db) doc + soft-delete + TenantId; l10n tr/en real Turkish; console clean.
PASS ≠ CT ACCEPTED (K13).

---

## Agent Prompt (paste-ready)

@module-pack-author
WP: WP-TEP-SLICE-0336 · Prompt P-TEP-SLICE-0336 v1.0

Repository: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Worktree: /Users/cihan/Desktop/ERP-vNext
Runtime UP (9/9): frontend http://localhost:5001 · gateway 5080 · TEP servisi 5060. Login: http://localhost:5001/account/login?tenantId=00000000-0000-0000-0000-000000000001 (admin@diten.com / Admin123!). localhost KULLAN.

Modül: Talent Data Foundation · planning id MOD-0336 · önerilen alias CAND-CAP-0041 · slug `talent-data-foundation` · domain talent-ecosystem-platform · servis Diten.TalentEcosystemService (5060). BU HCM DEĞİL — TEP servisinde kur.
REFERANS (kanıtlanmış TEP deseni — kopyala/uyarla): R2 MVP TEP modülleri (services/Diten.TalentEcosystemService/src/** — CandidateProfiles/ReferenceExchange readiness slice'ları) + packs CAND-CAP-0017/0019/0021.

Önce oku:
1. execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0017-*.md (+ 0019, 0021) — TEP pack ŞABLONU
2. Backend referans: services/Diten.TalentEcosystemService/src/** (bir R2 readiness slice: Domain entity + Application Features + Api controller + Mongo repo + DI)
3. Frontend referans: TEP alanındaki mevcut bir liste sayfası (Views + js) + golden-compact deseni (HCM LearningTraining golden-compact yapısını UI deseni olarak al)
4. AGENTS.md (§3 port: TEP=5060; API gateway 5080) · DataSeeder.cs (izin seed; tep.* desenini izle) · gateway/ocelot.json (mevcut tep rota bloğu) · _LayoutTenantShell.cshtml (TEP nav bloğu)

NE: MOD-0336'yı TEP R2 deseniyle UÇTAN UCA, metadata-only readiness slice olarak TalentEcosystemService'te kur:
    a) PACK: CAND-CAP-0041-talent-data-foundation.md (talent-ecosystem-platform domain, §20 waiver K19-tam).
    b) BACKEND: TalentDataFoundationReadinessMetadata (+ enum, repo, Mongo repo, DI), Application/Features, Api controller (read/manage/evaluate/audit.read).
    c) API+İZİN: ocelot'a /api/talent-data-foundation rotaları → 5060; DataSeeder'a tep.talent-data-foundation.{read,manage,evaluate,audit.read}.
    d) FRONTEND: golden-compact CRUD + TEP nav girişi (Perms.Has("tep.talent-data-foundation.read")) + SharedResource menü anahtarı; l10n tr/en gerçek Türkçe.
    e) TEST: TalentDataFoundationTests.cs (fix-absent→RED, K3).
NASIL: List DTO alanları = JS kolonları (drift YOK). Guard forbidden-marker token/word-boundary (substring DEĞİL).
       Response<T> zarfı. Auth restart sonrası tep.talent-data-foundation.* izinlerinin SuperAdmin'e düştüğünü doğrula.
YAPMA: HCM servisine/modüllerine dokunma; backend contract uydurma (K12); reserved UX; dt-defaults/auth/diğer servislere gereksiz dokunma.
DOĞRULA: Build yeşil; login → TEP nav'da "Talent Data Foundation" görünür; sayfa reload döngüsüz; +Yeni Ekle → create → liste →
    evaluate → delete; Mongo (TEP db) kayıt + soft-delete + TenantId; l10n gerçek Türkçe; konsol temiz; drift yok. E3/E4 kanıtı + ekran görüntüsü.

Durma koşulları: eksik contract/alan · reserved UX · scope dışı. Dur ve raporla.
Rapor: §22 + oluşturulan/değişen dosyalar + izin seed diff + E3/E4 kanıtı + golden-compact ekran görüntüsü.
Senin PASS'in kapanış değildir (K13); CT bağımsız doğrular, sonra sıradaki modül (MOD-0332 Hiring Risk Indicators).
