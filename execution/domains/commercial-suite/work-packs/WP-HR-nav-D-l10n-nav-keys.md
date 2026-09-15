# WORK PACKAGE — WP-HR-nav-D · HR nav L10n (Nav.* resx, 7 dil)

> **CT (SoR).** Branch `fix/hr-integration-gaps`. Plan: PLAN-HR-nav-wiring-module-catalog.md. **Yalnız frontend resx** (`Diten.Web/Resources/SharedResource.*.resx`). WP-A/B (manifest providers) + WP-C (entitlement/grant, menü canlı) tamamlandı; bu WP gap #5'i (L10n) kapatır ve WP-A/B'nin kırdığı `NavManifestL10nGuardTests`'i yeşile döndürür → PR→main.

## Ölçülen durum (CT, 2026-09-15)
- Guard: `frontend/Diten.Web.Tests/Navigation/NavManifestL10nGuardTests.cs`. `services/**/*ManifestProvider.cs`'i **recursive** tarar → 3 HR provider'ı görür → her nav-visible sayfa/modül/domain için `Nav.Page/Module/Domain.<Normalize(code)>` anahtarını **7 dilde** ister. Değer: **boş olamaz** ve **key'i (veya son segmentini) yankılayamaz**.
- `NavNameLocalizer.Normalize` (frontend/Diten.Web/Services/Navigation/NavNameLocalizer.cs): **uppercase + tüm alfanumerik-olmayan karakterleri sil**. Ör. `APPLICANT_INTAKE`→`APPLICANTINTAKE`, `HCM-EMPLOYEE-MASTER`→`HCMEMPLOYEEMASTER`, `"Human Capital"`→`HUMANCAPITAL`. Bu tek kaynak; guard resx anahtarlarını **bu metodla** türetir.
- **Mevcut durum: HR Nav anahtarları 7 dilin HEPSİNDE 0** (module 0/3, domain 0/2, page 0/54 — her dilde). Yani "5 eksik dil" DEĞİL → **59 yeni anahtar × 7 dil = 413 resx satırı**.
- 7 resx dosyası: `SharedResource.{en,tr,fr,es,zh,ar,ru}.resx` (her biri şu an 143 Nav.* anahtar → 202 olacak).
- resx satır formatı (aynen): `  <data name="Nav.Page.XXX" xml:space="preserve">\n    <value>Etiket</value>\n  </data>`
- Floor `NavVisiblePageKeyFloor = 45` bir **alt sınır** (`>=`); 54 sayfa eklemek onu aşar, **değiştirme gerekmez**.

## Kapsam (59 anahtar)
**İngilizce değer kaynağı (SoT):** her sayfanın İngilizce etiketi = ilgili provider'daki `new ModuleManifestPage(CODE, "<Etiket>", ...)` **2. argümanı** (birebir). Modül etiketi = provider `DisplayName`; domain etiketi = `Domain`. Bu İngilizce metni diğer 6 dile **gerçek çeviri** ile aktar (placeholder/echo YOK; CLAUDE.md: bir dil eksikse iş bitmemiştir).

- **Nav.Module (3):** HUMANCAPITAL="Human Capital" · HCMEMPLOYEEMASTER="Employee Master" · TALENTECOSYSTEM="Talent Ecosystem"
- **Nav.Domain (2):** HUMANCAPITAL="Human Capital" · TALENTECOSYSTEM="Talent Ecosystem"
- **Nav.Page HumanCapital (23):** APPLICANTINTAKE, CANDIDATEPIPELINE, COMPENSATIONBENEFITS, COMPETENCYSKILLS, DEVELOPMENTPLANS, EMPLOYEEONBOARDING, EMPLOYEEPROJECTIONS, EMPLOYMENTCHANGES, HEADCOUNTBUDGET, HRCASEMANAGEMENT, HRCOMPLIANCE, HRDOCUMENTATION, HRKPIANALYTICS, LEARNINGTRAINING, OFFBOARDINGCASES, OFFERMANAGEMENT, PERFORMANCEREVIEWS, POSITIONASSIGNMENTS, SELFSERVICE, SENSITIVEACCESS, SUCCESSION, TIMEATTENDANCELEAVE, WORKFORCEPLANNING
- **Nav.Page HCM (1):** EMPLOYEEMASTER="Employee Master"
- **Nav.Page TalentEcosystem (30):** ASSOCIATIONMEMBERSHIPS, ASSOCIATIONOPERATIONS, CANDIDATECAREERPASSPORT, CANDIDATEDISPUTES, CANDIDATEPROFILES, CONSENTVISIBILITYPOLICIES, EARLYWARNINGSIGNALS, EXITREFERENCERECORDS, HIRINGRISKINDICATORS, INDUSTRYKNOWLEDGENETWORK, INDUSTRYSKILLPASSPORT, INDUSTRYSUCCESSIONPOOL, INDUSTRYTALENTPOOL, MENTORSHIPRECOMMENDATIONNETWORK, PAYBENCHMARKING, PROFESSIONALREPUTATIONLEDGER, REFERENCEEXCHANGE, REHIRERECOMMENDATIONS, RESTRICTEDINTEGRITYREGISTRY, REVIEWBOARD, SECTORMOBILITYINTELLIGENCE, SECTORTALENTTRENDS, SKILLSGAPHEATMAP, TALENTDATAFOUNDATION, TALENTDEVELOPMENTNETWORK, TALENTSUPPLYDEMANDFORECASTING, TRUSTLEVELS, VERIFIEDCERTIFICATIONREGISTRY, VERIFIEDPARTICIPANTS, WORKFORCEANALYTICS

## YAPMA
Manifest provider/DI/backend/entitlement/grant DOKUNMA. Yeni anahtar şeması uydurma (yalnız `Nav.{Module,Domain,Page}.<Normalize>`). Floor sabitini değiştirme. Mevcut 143 Nav anahtarına dokunma. İngilizce etiketi provider 2. argümanından farklı yazma (sidebar ile tutarlı olmalı). Değeri key ile aynı yazma (echo → guard fail).

## Acceptance
- **E2:** `frontend/Diten.Web.Tests` yeşil — özellikle `NavManifestL10nGuardTests`'in TÜM fact'leri (module/page/domain anahtarları 7 dilde present+non-empty+non-echo; parser completeness; floor). Baseline-diff sıfır-yeni-fail. Yalnız 7 resx dosyası değişir.
- **E4 (CT):** menü zaten görünüyor (WP-C); bu WP sonrası sidebar etiketleri 7 dilde doğru (İngilizce fallback yok).

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-HR-nav-D · HR nav L10n (Nav.* resx anahtarları, 7 dil — frontend)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: fix/hr-integration-gaps · Expected HEAD: 4a7f7408 (WP-C sonrası) · Worktree: ana checkout

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-HR-nav-D-l10n-nav-keys.md (bu WP — 59 anahtarın tam listesi + Normalize kuralı + resx formatı)
2. frontend/Diten.Web.Tests/Navigation/NavManifestL10nGuardTests.cs (guard — ne bekliyor) + frontend/Diten.Web/Services/Navigation/NavNameLocalizer.cs (Normalize)
3. İngilizce etiket SoT: services/Diten.Platform/.../Features/HumanCapital/SelfRegistration/HumanCapitalManifestProvider.cs + HcmEmployeeMasterManifestProvider.cs + Features/TalentEcosystem/SelfRegistration/TalentEcosystemManifestProvider.cs — her ModuleManifestPage'in 2. argümanı = o sayfanın İngilizce etiketi
4. MIRROR format: frontend/Diten.Web/Resources/SharedResource.en.resx içindeki mevcut Nav.Page.* satırları

NE (yalnız 7 resx dosyası: SharedResource.{en,tr,fr,es,zh,ar,ru}.resx):
 59 yeni anahtar ekle — Nav.Module (3) + Nav.Domain (2) + Nav.Page (54) — HER BİRİNİ 7 DİLDE.
 Anahtar adı = Nav.{Module|Domain|Page}.<NavNameLocalizer.Normalize(code)> (uppercase + alfanumerik-dışı sil). WP-D doc'unda tam liste var.
 Değer: en = provider'daki ModuleManifestPage 2. argümanı (birebir) / modül+domain için DisplayName+Domain; diğer 6 dil = o İngilizce etiketin GERÇEK çevirisi (fr/es/zh/ar/ru dahil). Boş bırakma, key'i yankılama.
NASIL: mevcut Nav.Page.* satır formatını birebir kopyala (<data name=... xml:space="preserve"><value>...</value></data>). Her dosyaya 59 satır → toplam 413 satır.
YAPMA: manifest/DI/backend/entitlement/grant; yeni anahtar şeması; floor sabiti; mevcut 143 anahtar; İngilizce etiketi provider'dan farklı yazma.
DOĞRULA (E2): cd frontend && dotnet test Diten.Web.Tests → NavManifestL10nGuardTests TÜM fact'ler yeşil (module/page/domain 7 dilde present+non-empty+non-echo, parser completeness, floor). Tam suite baseline-diff sıfır-yeni-fail. Tek commit. §22 raporu TÜRKÇE. K13.

Durma koşulları: bir sayfanın İngilizce etiketi provider'da bulunamıyorsa · Normalize sonucu belirsizse · guard beklediğinden farklı anahtar şeması gerekiyorsa · kapsam resx dışına taşıyorsa → DUR + raporla.
```

## §37 CT bağımsız doğrulama → (agent sonrası doldurulacak)
```text
Commit: <agent> · Agent: <PASS/FAIL> · CT: <PENDING>
```
- İzole worktree @commit → `dotnet test frontend/Diten.Web.Tests` → NavManifestL10nGuardTests tüm fact yeşil; 413 satır (59×7) present+non-empty+non-echo; yalnız 7 resx değişti; baseline-diff sıfır-yeni-fail.

## Kalan (bu WP dışı)
- PR `fix/hr-integration-gaps` → main (merge = Ali) → main → `feature/scmm-content-studio` sync (option X) → SCMM/ALMIBA A2d'den devam ([[almiba-retest-checkpoint]]).
