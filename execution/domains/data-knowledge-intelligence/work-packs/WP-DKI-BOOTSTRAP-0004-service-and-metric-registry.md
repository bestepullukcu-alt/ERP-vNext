WORK PACKAGE — NEW SERVICE BOOTSTRAP + first module (MOD-0004)

WP ID:            WP-DKI-BOOTSTRAP-0004
Prompt ID:        P-DKI-BOOTSTRAP-0004
Prompt Version:   v1.0
Task Class:       new-service scaffold (architectural) + full module slice
Golden-Flow Profile: A
Risk Class:       HIGH (new service, gateway/port/auth wiring, protected-path adjacent)
State:            READY
Target Agent / Entry Point: @orchestrator (paste-block leads @module-pack-author)

Owner decision (SoR): 2026-09-07 — analytics-backbone deferral REVERSED. Owner authorized creating the
data-plane domain + service and building MOD-0004/0059-0064. Blueprint-canonical ids (not CAND-CAP).

New service:  Diten.DataKnowledgeService · port 5062 · namespace Diten.DataKnowledgeService.* · Mongo db DitenDataKnowledge
Domain:       data-knowledge-intelligence (execution/domains/data-knowledge-intelligence — packs already drafted)
Permission owner key: dki.*
First module: MOD-0004 Metric & Semantic Registry · slug `metric-semantic-registry`
Reference (mirror whole service structure): services/Diten.TalentEcosystemService/** (freshest, proven — 5-layer + tests)
Sequence after this: MOD-0063, MOD-0064 (W-3 infra) → MOD-0059, 0060, 0061, 0062 (W-4).

Repository: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Worktree: /Users/cihan/Desktop/ERP-vNext
Runtime UP (9/9): gateway 5080, frontend 5001. Login: /account/login?tenantId=00000000-0000-0000-0000-000000000001 (admin@diten.com / Admin123!). USE localhost.

CRITICAL correctness (bake in from the START — these were the TEP/HCM bugs, do NOT repeat):
- Api appsettings.Development.json JwtSettings = shared secret + PreviousSecrets, Issuer `diten-auth-service`,
  Audience `diten-erp` (copy from TEP appsettings.Development.json).
- Persistence DI: mongoClientSettings.GuidRepresentation = GuidRepresentation.Standard (copy from TEP DI).
- Guard forbidden-marker = word-boundary Regex `\b(...)\b` (not substring Contains).
- List DTO fields == frontend JS DataTable columns (no drift).

Scope (WRITE — authorized incl. gateway/ocelot/auth-DataSeeder/nav/port, this WP):
- services/Diten.DataKnowledgeService/** (new 5-layer service + tests, mirror TEP)
- gateway/Diten.ApiGateway/ocelot.json (metric-semantic-registry routes → 5062)
- services/Diten.AuthService/.../Seed/DataSeeder.cs (dki.metric-semantic-registry.* permissions)
- frontend/Diten.Web/{Controllers,Views/DataKnowledge,Resources/Views/DataKnowledge,wwwroot/assets/js/DataKnowledge,Models/DataKnowledge}/** (golden-compact CRUD)
- frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml (new "Data & Knowledge" nav section)
- frontend/Diten.Web/Resources/SharedResource.{en,tr}.resx (menu keys)
- execution/domains/data-knowledge-intelligence/module-packs/MOD-0004-*.md (finalize) + registry row
- AGENTS.md §3 port table (add Data Knowledge Service 5062) — protected doc, minimal append
Protected: HCM/TEP/other services, dt-defaults/auth-loop, existing modules.

Objective: scaffold Diten.DataKnowledgeService (port 5062) mirroring TEP's structure with correct JWT/GUID/CORS from
the start, then build MOD-0004 Metric & Semantic Registry as a metadata-only readiness slice end-to-end (backend +
gateway route + dki.* perms + golden-compact CRUD frontend + nav + tests). Start the new service on 5062 and verify.

Acceptance (E3/E4): solution builds green; new service listens on 5062; gateway /api/metric-semantic-registry → 5062
(401 without token, 200 with token); login → "Data & Knowledge" nav shows "Metric & Semantic Registry"; page no reload
loop; +New → create → list → evaluate → delete; Mongo DitenDataKnowledge doc + soft-delete + TenantId; l10n tr/en real
Turkish; drift-clean; console clean. PASS ≠ CT ACCEPTED (K13).

---

## Agent Prompt (paste-ready)

@module-pack-author
WP: WP-DKI-BOOTSTRAP-0004 · Prompt P-DKI-BOOTSTRAP-0004 v1.0

Repository: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Worktree: /Users/cihan/Desktop/ERP-vNext
Runtime UP: gateway 5080 · frontend 5001. Login: http://localhost:5001/account/login?tenantId=00000000-0000-0000-0000-000000000001 (admin@diten.com / Admin123!). localhost KULLAN.

YENİ SERVİS bootstrap + ilk modül. Sahibi analytics-backbone ertelemesini geri aldı; data-plane domain+servis kur, sonra MOD-0004'ü kur.
Yeni servis: Diten.DataKnowledgeService · port 5062 · namespace Diten.DataKnowledgeService.* · Mongo db DitenDataKnowledge · permission owner key dki.*
Domain: data-knowledge-intelligence (execution/domains/data-knowledge-intelligence — pack taslakları var)
İlk modül: MOD-0004 Metric & Semantic Registry · slug `metric-semantic-registry` (Blueprint-canonical, CAND-CAP DEĞİL)
REFERANS (tüm servis yapısını mirror'la): services/Diten.TalentEcosystemService/** (5-katman + tests, en taze kanıtlı desen)

Önce oku:
1. services/Diten.TalentEcosystemService/** — solution/csproj/DI/Persistence/appsettings yapısı + bir readiness slice
2. services/Diten.TalentEcosystemService/src/*.Api/appsettings.Development.json (JwtSettings — AYNISINI kullan) + *.Persistence/DependencyInjection.cs (GuidRepresentation.Standard — AYNISINI koy)
3. AGENTS.md §3 (port şeması) · gateway/ocelot.json (bir servis rota bloğu) · DataSeeder.cs (izin seed deseni) · _LayoutTenantShell.cshtml (nav bölümü)
4. execution/domains/data-knowledge-intelligence/module-packs/MOD-0004-metric-semantic-registry.md (taslak)

NE:
    A) SERVİS SCAFFOLD — Diten.DataKnowledgeService'i TEP'i mirror'layarak kur: 5-katman (Domain/Application/Persistence/Api + tests),
       solution/csproj'lar, Program.cs (JWT auth + HasPermission + gateway cookie→Bearer uyumu), appsettings.Development.json
       JwtSettings=TEP ile AYNI (shared secret, issuer diten-auth-service, audience diten-erp), Persistence DI'da
       GuidRepresentation.Standard, Mongo db DitenDataKnowledge, --urls 5062.
    B) MOD-0004 MODÜLÜ (metadata-only readiness slice, TEP slice deseni): MetricSemanticRegistryReadinessMetadata
       (+enum/repo/Mongo repo `dki_metric_semantic_registry_readiness`/DI), Features (Commands/Queries/Handlers/Guard[word-boundary]/Models),
       Api controller (read/manage/evaluate/audit.read). Metric/semantic-registry temalı boundary/dependency/governance alanları; para alanı YOK.
    C) WIRING: ocelot /api/metric-semantic-registry (+/{everything}) → 5062; DataSeeder dki.metric-semantic-registry.{read,manage,evaluate,audit.read};
       nav'a YENİ "Data & Knowledge" bölümü + menu-item (Perms.Has("dki.metric-semantic-registry.read")); SharedResource menü anahtarı; AGENTS.md §3 port tablosuna "Data Knowledge Service | 5062" satırı.
    D) FRONTEND: golden-compact CRUD (Views/DataKnowledge/MetricSemanticRegistry + js + resx) TEP/HCM golden-compact deseniyle; l10n tr/en gerçek Türkçe.
    E) TEST: MetricSemanticRegistryTests.cs (fix-absent→RED). Registry'ye MOD-0004 satırı (Blueprint-canonical).
    F) BAŞLAT: yeni servisi 5062'de başlat (bare-DLL, ASPNETCORE_ENVIRONMENT=Development), auth+gateway restart (yeni izin+rota), tenant-login ile doğrula.
NEDEN: Blueprint-canonical data-plane; HR façade (MOD-0312) bunları tüketecek. Ayrı domain+servis (§4.1) — HCM/TEP'e sokulamaz.
NASIL: JWT/GUID/CORS/word-boundary/drift baştan doğru (TEP/HCM bug'larını TEKRARLAMA). Response<T> zarfı. Auth restart → dki.* izni SuperAdmin'e düşsün.
YAPMA: HCM/TEP/diğer servisleri değiştirme; contract uydurma (K12); reserved UX; dt-defaults/auth-loop'a dokunma.
DOĞRULA: solution build yeşil; 5062 dinliyor; gateway /api/metric-semantic-registry → 401 (token'sız) / 200 (token'lı);
    login → "Data & Knowledge" nav'da "Metric & Semantic Registry"; reload döngüsüz; +Yeni Ekle→create→liste→evaluate→delete;
    Mongo DitenDataKnowledge + soft-delete + TenantId; l10n gerçek TR; drift yok; konsol temiz. E3/E4 + ekran görüntüsü.

Durma koşulları: eksik contract · reserved UX · scope dışı · protected-path onayı gereken durum. Dur ve raporla.
Rapor: §22 + oluşturulan servis+modül dosyaları + izin/rota/port diff + E3/E4 kanıtı + ekran görüntüsü.
Senin PASS'in kapanış değil (K13); CT bağımsız doğrular, sonra MOD-0063 Data Warehouse/Lakehouse.
