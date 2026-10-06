# WORK PACKAGE — WP-HR-nav-B · TalentEcosystemManifestProvider (31 modül)

> **CT (SoR).** Branch `fix/hr-integration-gaps`. Plan: PLAN-HR-nav-wiring-module-catalog.md. **Yalnız backend/Platform**. WP-A ile paralel olabilir (farklı dosya; DI'de ayrı satır). Frontend/entitlement/grant ayrı.

## Kapsam
1. **`TalentEcosystemManifestProvider`** (Features/TalentEcosystem/SelfRegistration/, CrmManifestProvider aynası): ModuleCode "TALENT-ECOSYSTEM", Domain "Talent Ecosystem", Service "DitenTalentEcosystemService", IsTenantAssignable true, IsBaseline false, Icon/SortOrder.
   - **Pages (31)** — PLAN'daki TalentEcosystem/ route listesi; her biri `ModuleManifestPage(CODE,"Label","/TalentEcosystem/<Route>",readPerm,null,true,"List",order,[actions])`.
   - readPerm/actions: web controller HasPermission sabitleri + backend Diten.TalentEcosystemService `[HasPermission]` anahtarlarından türet.
   - **TepShellMetadata** = shell/metadata → nav'a EKLEME (page değil), belgele.
2. **DI:** `AddSingleton<IModuleManifestProvider, TalentEcosystemManifestProvider>()`.

## YAPMA
WP-A ile aynı: frontend/entitlement/grant/HR-servis-kodu/legal_entities/token/başka modül DOKUNMA; uydurma route/izin yok — DUR+raporla.

## Acceptance
- **E2:** Platform build temiz; provider DI'de; GetManifest ~30 (TepShellMetadata hariç) page geçerli route+readPerm; Platform test baseline-diff sıfır-yeni-fail.
- **E4:** Platform restart → catalog TALENT-ECOSYSTEM + page_descriptors + izin auto-register (menü WP-C sonrası).

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-HR-nav-B · TalentEcosystemManifestProvider (HR nav wiring, 31 modül — Platform)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: fix/hr-integration-gaps · Expected HEAD: <WP-A sonrası ya da PLAN commit> · Worktree: ana checkout
(WP-A ile paralel olabilir — farklı dosya; DependencyInjection.cs'te 2 ayrı satır, çakışırsa sıralı.)

Önce oku:
1. PLAN-HR-nav-wiring-module-catalog.md (31 TalentEcosystem route enumerasyonu + manifest imzası) + WP-HR-nav-B-talentecosystem-manifest.md (bu WP)
2. MIRROR: services/Diten.Platform/src/Diten.Platform.Application/Features/Crm/SelfRegistration/CrmManifestProvider.cs + DependencyInjection.cs ~426-439
3. Route kaynağı: frontend/Diten.Web/Controllers/**/*.cs Route("TalentEcosystem/...") ; readPerm için controller HasPermission sabitleri + services/Diten.TalentEcosystemService [HasPermission]

NE (yalnız Platform backend):
 1) Features/TalentEcosystem/SelfRegistration/TalentEcosystemManifestProvider.cs (CrmManifestProvider aynası): ModuleCode "TALENT-ECOSYSTEM", Domain "Talent Ecosystem", Service "DitenTalentEcosystemService".
 2) Pages (31): PLAN TalentEcosystem/ listesi → ModuleManifestPage(CODE,"Label","/TalentEcosystem/<Route>",readPerm,null,true,"List",order,[actions]). TepShellMetadata shell → nav'a EKLEME (belgele).
 3) DI: AddSingleton<IModuleManifestProvider, TalentEcosystemManifestProvider>().
NASIL: CrmManifestProvider birebir; route/izin controller+backend'den; UYDURMA YOK, emin olmadığın izinde DUR+raporla.
YAPMA: frontend/web; entitlement/RBAC grant; WP-A HumanCapital; HR backend servis kodu; legal_entities/token; başka modül.
DOĞRULA (E2): Platform build temiz; provider DI'de; GetManifest ~30 page geçerli route+readPerm; Platform test baseline-diff sıfır-yeni-fail. Ayrı commit. §22 TÜRKÇE. K13.

Durma koşulları: readPerm bulunamıyorsa · shell sayfa · imza farklı · kapsam dışı → DUR+raporla.
```

## §37 CT bağımsız doğrulama (2026-09-15) → **ACCEPTED (E2)** · E4 = Platform restart + WP-C sonrası menü
```text
Commit: 9b89a7a5 (tek) · Agent: PASS (WP-A ile paralel) · CT: ACCEPTED E2
```
- ✅ **Scope:** 2 dosya — TalentEcosystemManifestProvider (255) + DI (+3). WP-A ile çakışmadan birleşti (DI'de 3 provider da mevcut: TalentEcosystem 439 + HumanCapital 457 + HcmEmployeeMaster 458).
- ✅ **Build (HEAD, WP-A+B, Release):** Platform.Application **0 hata**; **UYDURMA YOK: 31/31 read izni** (tep.<kebab>.read) backend `Diten.TalentEcosystemService`'te **verbatim mevcut** (eksik 0).
- ✅ **Manifest:** **30 sayfa** (31 controller − TepShellMetadata shell, doğru hariç tutuldu); ModuleCode TALENT-ECOSYSTEM.
- ✅ **Test:** manifest/module-registration/nav **160/160** (HEAD, WP-A+B birlikte).
- ⏳ **E4:** Platform restart → catalog TALENT-ECOSYSTEM + page_descriptors 30 + izin auto-register; menü WP-C sonrası.

## Kalan (bu WP dışı)
- **WP-C entitlement+RBAC grant (sıradaki)** · WP-D L10n+Nav.Page.*. → menüde görünür → PR→main (Ali).
