# WORK PACKAGE — WP-CT-BE-A · Uyum motoru: D2 reversal (addresses/evidences) + template conformance-diagnostics query (backend, CrmService)

> **CT (SoR).** MOD-0162 Concept Graph / Chain Template. Branch `feature/crm-chain-template`. Zincir Şablonu v2 (tip modeli) mockup'ının **"Uyumsuz" sekmesini** besleyen **ortak conform motoru**. **Kullanıcı kararları:** D1=B (tip modeli — bu WP'yi etkilemez), **D2=(i) backend'e reversal ekle**, conform mantığı ekran+backend'de **tek ortak fonksiyon** olmalı (ayrışmayı önle). **CrmService (Application + Api).** Conformance **asla enforce edilmez** (D8) — yalnız türetilir/gösterilir. Resolution persist (ignore) = **ayrı WP (BE-B)**. Frontend YOK.

## Kanıt
- **`ConceptRelationshipGraph.IsConforming`** (`.../Concept/Relationship/ConceptRelationshipHandlers.cs:68`): `(subjectTemplates, fromTypeId, toTypeId)` → ilişkinin **ham From→To tip çifti** omurgada bitişik sıralı mı diye bakar; **reversal YOK**. 3 çağıran: `Node/CreateConceptNodeWithRelationshipHandler.cs:201` · `Relationship/ConceptRelationshipHandlers.cs:196` (create) · `:324` (update re-derive).
- **RelationshipType sözlüğü** (`ConceptGraphVocabulary.cs` / `ConceptRelationshipTypes`): `leads-to`, `requires`, `addresses`, `evidences`, `belongs-to`, `custom`. **Reversal yalnız `addresses` + `evidences`** (anlatı yönü ters: "Bileşen İhtiyacı karşılar" → sıra İhtiyaç→Bileşen).
- **Template conformance-diagnostics query YOK** — yalnız contract flag `SupportsTemplateConformanceDiagnostics:true` (`ConceptGraphContract.cs:58`). Contract **traversal/resolution/recommendation/AI'yı dışlar** (satır 114) — bu WP yalnız **tanı (diagnostics)** ekler, çözümleme/AI DEĞİL.
- **Mockup conform sınıflandırması:** her ilişki → kaynak/hedef düğüm tiplerini al · addresses/evidences ters oku · tiplerden biri omurgada yoksa **`out`** (eksik tipleri de döndür) · ikisi de var ama sıra tersse **`order`** · aksi **`conforming`**. Omurga değişince anında yeniden hesap.

## NE (CrmService; conformance enforce EDİLMEZ)
1. **D2 reversal — `IsConforming`:** imzaya **relationshipType** ekle; `addresses`/`evidences` ise adjacency kontrolünden **önce (from,to) swap** et; `leads-to`/`requires`/`belongs-to`/`custom` normal yön. 3 çağıranı ilişkinin tipini geçecek şekilde güncelle. Mevcut create/update re-derive yolları yeni imzayı kullansın (6 ALMIBA ilişkisinin bayrağı bir sonraki read/update'te doğru hesaplanır; gerekirse tek seferlik re-derive komutu ekleme — yeni yazımlar yeterli).
2. **Ortak conform sınıflandırıcı (shared):** `Classify(orderedTypeIds, fromTypeId, toTypeId, relationshipType)` → `conforming | order | out` döndüren saf fonksiyon (reversal dahil). `IsConforming` bunu sarmalasın (tek kaynak; ekran+backend ayrışmasın).
3. **Template conformance-diagnostics query:** `GetChainTemplateConformanceDiagnosticsQuery(subjectId, orderedConceptTypeIds[])` → o konunun **aktif ilişkileri** için: her ilişki `{relationshipId, fromNodeId/Name, toNodeId/Name, fromTypeId, toTypeId, relationshipType, result(conforming/order/out), missingTypeIds[] (out ise)}`. **Supplied-spine imzası** şart (taslak editörü, dallardan **anlık türettiği** omurgayı gönderip kaydetmeden sorgulayabilsin). Salt-okunur; yazma yok; traversal/AI yok.
4. **Api:** thin endpoint (ör. `POST /api/crm/knowledge-concepts/chain-conformance-diagnostics` body `{subjectId, orderedConceptTypeIds[]}`) → diagnostics listesi. RBAC = mevcut concept read izni. Gateway route deseni (varsa concept route'una ekle).
5. Tests: reversal (addresses geri-çift → conforming; leads-to literal) · Classify out/order/conforming + missingTypeIds · diagnostics query (supplied spine, aktif ilişkiler, reversal uygulanır) · enforce YOK (yalnız sınıflandırma).

## KORU / YAPMA
- **Conformance ASLA enforce edilmez / reddetmez (D8)** — yalnız türet+döndür. Relationship create/update'in **duplicate-active (V11) / cycle (V10) / type-direction** guard'ları DEĞİŞMEZ (yalnız IsConforming imzası+reversal). Contract'ın **no-traversal/no-resolution/no-AI** sınırı korunur (bu WP tanı-only). **Resolution (ignore) persist = BE-B (bu WP'de YOK).** Chain template step modeli (tip+min/max) DEĞİŞMEZ (D1=B). Frontend YOK. TenantId JWT'den.
- **DUR:** `belongs-to`/`custom` için reversal gerekiyorsa (mockup normal diyor — gerekmiyor) netleştir; supplied-spine query mevcut bir okuma sözleşmesini bozuyorsa → DUR+raporla.

## Acceptance
- **E2:** `dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/... -c Release` → yeşil (baseline + yeni conform/diagnostics testleri); CrmService.Api 0 hata. git diff: Application (IsConforming+Classify+diagnostics query/handler/dto) + Api (endpoint) + 3 çağıran güncel. Relationship guard davranış diff YOK (reversal hariç conformance). Testler: reversal · Classify(out/order/conforming+missing) · diagnostics(supplied spine, reversal).
- **E4 (smoke):** ALMIBA/TUTUKON konusu + bir omurga ver → diagnostics doğru out/order/conforming döndürür; "addresses" geri-çifti conforming çıkar.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-CT-BE-A · Uyum motoru: D2 reversal (addresses/evidences) + template conformance-diagnostics query (Diten.CrmService, backend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-chain-template · Worktree: ana checkout

Amaç: Zincir Şablonu "Uyumsuz" sekmesinin ortak conform motoru. D2=(i): addresses/evidences ilişki tiplerinde adjacency kontrolünden önce from/to swap. + template conformance-diagnostics query (supplied-spine). Conformance ASLA enforce edilmez (D8) — yalnız türet/döndür. Resolution persist AYRI WP (BE-B). Frontend YOK.

Önce oku: execution/domains/commercial-suite/work-packs/WP-CT-BE-A-conformance-engine-reversal-diagnostics.md · services/Diten.CrmService/src/Diten.CrmService.Application/Features/Knowledge/Concept/Relationship/ConceptRelationshipHandlers.cs (IsConforming:68 + çağıran 196/324) · .../Concept/Node/CreateConceptNodeWithRelationshipHandler.cs:201 · services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/ConceptGraphVocabulary.cs (ConceptRelationshipTypes) · .../Concept/Contract/ConceptGraphContract.cs (SupportsTemplateConformanceDiagnostics, no-traversal/AI sınırı) · .../Concept/ChainTemplate/* (OrderedConceptTypes).

NE (CrmService; enforce YOK):
 1) IsConforming imzasına relationshipType ekle; addresses/evidences → (from,to) swap sonra adjacency; leads-to/requires/belongs-to/custom normal. 3 çağıranı ilişki tipini geçecek şekilde güncelle.
 2) Shared saf fonksiyon Classify(orderedTypeIds, fromTypeId, toTypeId, relationshipType) → conforming|order|out (reversal dahil); IsConforming bunu sarmalasın (tek kaynak).
 3) GetChainTemplateConformanceDiagnosticsQuery(subjectId, orderedConceptTypeIds[]) → konunun aktif ilişkileri için {relationshipId, from/toNodeId+Name, from/toTypeId, relationshipType, result, missingTypeIds[]}. Supplied-spine ŞART (taslak editörü kaydetmeden anlık omurgayla sorgular). Salt-okunur.
 4) Api thin endpoint POST /api/crm/knowledge-concepts/chain-conformance-diagnostics {subjectId, orderedConceptTypeIds[]} → liste; RBAC mevcut concept read; gateway route deseni.
KORU/YAPMA: conformance enforce/reddetme YOK (D8); relationship duplicate(V11)/cycle(V10)/type-direction guard'ları DEĞİŞMEZ (yalnız IsConforming imzası+reversal); contract no-traversal/no-resolution/no-AI korunur (tanı-only); resolution persist YOK (BE-B); step modeli tip+min/max DEĞİŞMEZ; frontend YOK; TenantId JWT'den.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Diten.CrmService.Application.Tests.csproj -c Release --nologo → yeşil (baseline+yeni); CrmService.Api 0 hata; git diff Application(IsConforming+Classify+diagnostics)+Api(endpoint)+3 çağıran; relationship guard diff yok. Testler: reversal(addresses geri-çift conforming, leads-to literal), Classify(out/order/conforming+missing), diagnostics(supplied spine+reversal). Ayrı commit ("feat(crm): WP-CT-BE-A — chain conformance engine (addresses/evidences reversal) + template diagnostics query (MOD-0162)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: belongs-to/custom reversal gerekiyorsa; supplied-spine query mevcut okuma sözleşmesini bozuyorsa → DUR+raporla.
```

## §37 CT bağımsız doğrulama (2026-09-24) → **ACCEPTED (E2)**
```
Commit: 59612323 · Agent: PASS (1876/0) · CT: ACCEPTED E2 · izole worktree /c/tmp/ct-bea @59612323 → 1876/0/5 (birebir)
```
- ✅ **Kapsam (7 dosya, +434/−25):** yalnız CrmService (Api controller+requests · `ConceptChainConformance.cs`[yeni ortak Classify] · `ChainTemplateConformanceDiagnostics.cs`[yeni query] · `ConceptRelationshipHandlers.cs`[IsConforming imza+reversal] · Node handler[imza] · testler). **guard (V10 cycle/V11 duplicate/tip-yön) + enforce ekleme YOK** (yalnız IsConforming imza+reversal).
- ✅ **Ortak Classify (kod okundu):** addresses/evidences → (from,to) swap sonra adjacency · biri omurgada yoksa `out`+missingTypeIds · bitişik ileri çift `conforming` · aksi `order`. **Tek kaynak:** IsConforming da Classify'ı sarmalıyor (ekran+kayıtlı bayrak ayrışmaz). Diagnostics **supplied-spine** (taslak kaydetmeden sorgular) + conforming/order/out sayıları (rozet). Salt-okunur, D8 (enforce yok).
- ✅ **Endpoint:** `POST api/crm/knowledge/concept-chain-templates/conformance-diagnostics` — mevcut `/api/crm/knowledge/{everything}` gateway kapsamında (gateway değişmedi).
- ✅ **Build+test (CT izole):** Application.Tests **1876/0/5** (baseline 1864 + 12 yeni: reversal, Classify out/order/conforming+missing, diagnostics supplied-spine). Api 0 hata/0 uyarı.

### Onaylanan/açık sapmalar
- **✅ #1 endpoint yolu:** WP'deki `knowledge-concepts` gateway'de yoktu → canonical `knowledge/concept-chain-templates` altına konuldu (gateway değişmedi). **KABUL** (SCMM-16A "mevcut yüzeyi kullan" dersi).
- **⏳ #2 skip-ahead (F-CT-CONFORM-SKIP):** WP "reversal dışında conformance farkı yok" dediği için **adjacency (katı)** korundu → A→C (ileri ama atlamalı) = `order`. Mockup ise ileri-atlamayı `conforming` sayıyor (yalnız reverse=order). **Kullanıcı kararı gerek:** katı adjacency mı, mockup'ın gevşek forward-order'ı mı? (gevşek seçilirse Classify'da `indexOf(first)<indexOf(second)`=conforming; ⚠ kayıtlı IsTemplateConforming semantiği tüm FU03'te değişir + yeniden hesap.)
- **Not:** 6 ALMIBA ilişkisi toplu re-derive edilmedi (her biri sonraki update'te düzelir; diagnostics query zaten taze hesaplar). Co-Authored `Claude Opus 4.8` = bu oturumun standing attribution kuralı (değişmez).

**WP-CT-BE-A KOMPLE (E2).** Açık: F-CT-CONFORM-SKIP kararı · E4 smoke · push. Sıradaki: BE-B (resolution persist + publish guard'ları).

