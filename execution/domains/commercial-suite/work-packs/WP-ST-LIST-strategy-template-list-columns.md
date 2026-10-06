# WORK PACKAGE — WP-ST-LIST · StrategyTemplate liste konsolu mockup kolonları + kapsam + ülke filtresi (frontend)

> **CT (SoR).** MOD-0167-FU04. Branch `feature/crm-scmm-studio` (`19022503` üstü). **Kullanıcı:** Faz 2'den ÖNCE liste UI. Mockup `Liste` görünümü: OYUN / KAPSAM / SEGMENT / ÜRÜN / İÇERİK / DURUM / GÜNCELLENDİ kolonları + durum + **ülke filtresi**. **Frontend** (`Index.cshtml` + `_DataTable.cshtml` + `_Filter.cshtml` + `index.js` + `index.l10n.js` + `_IndexL10n.cshtml` + 7 resx) **+ 1 Web proxy satırı** (scope-options). Backend (CrmService) DEĞİŞMEZ — list DTO zaten scope + sayıları taşıyor (WP-ST-SCOPE).

## Kanıt (mevcut)
- **List DTO (`StrategyTemplateModels.cs` list row) HAZIR:** ScopeType/EffectiveScopeType/CountryScope/LegalEntityId/BusinessUnitId/ScopeRef + SegmentBindingCount/ProductLineCount/SkuAllocationCount/ContentBindingCount + templateCode/templateName/subjectType/templateStatus/templateVersion/frequencyIntentMode/effectiveFrom/effectiveTo/isArchived/updatedAt/superseded.
- **index.js** şu an 17-kolon DataTable (colReorder + Save View + actions). Mockup **konsolide** ~7 kolon istiyor → kolon yeniden düzeni (frontend).
- **scope-options** endpoint backend'de var (`GET api/crm/strategy-templates/scope-options`) ama **Web proxy'si YOK** → eklenecek (KAPSAM label + ülke filtresi bunu tüketir).
- Mockup Liste: başlık "Strateji Oyunları" + alt "Kapsam, segment, ürün ağırlığı ve hikâyeyi bağlayan şablonlar. Silme yok — yalnızca arşiv." + "+ Yeni oyun"; filtreler **Tümü/Taslak/Aktif/Arşiv** + **ülke** (Tümü/…); "N / N oyun" sayacı.

## Mockup kolonları (konsolide)
| Kolon | İçerik | Kaynak alan |
|---|---|---|
| **OYUN** | Ad (fw-medium) + alt satır kod (STR-…) + superseded rozeti | templateName + templateCode + superseded |
| **KAPSAM** | Scope etiketi (isim) + alt satır scope-tip etiketi | EffectiveScopeType + ScopeRef/CountryScope/LegalEntityId/BusinessUnitId → **scope-options map ile isim** |
| **SEGMENT** | Sayı + alt satır tip (kişi/kurum) | SegmentBindingCount + subjectType |
| **ÜRÜN** | Ürün sayısı (+ alt satır "N SKU") | ProductLineCount + SkuAllocationCount |
| **İÇERİK** | Sayı | ContentBindingCount |
| **DURUM** | Durum rozeti + sürüm (vN) | templateStatus + templateVersion |
| **GÜNCELLENDİ** | Tarih (dd.MM.yyyy) | updatedAt |
| (Actions) | View/Edit/Activate/New-version/Archive KORUNUR | mevcut |

**KAPSAM label çözümü:** `scope-options`'ı bir kez yükle → map'ler (country code→ad, LE id→ad, BU code→ad, territory→ad). Satır: tenant→"Tüm şirket" (L10n), country→countryMap[code] ?? code, legal-entity→leMap[id] ?? kısa-id, business-unit→buMap[code] ?? code. Scope-tip alt satırı L10n (Tenant/Ülke/Tüzel kişilik/İş birimi). Map'te yoksa ham ref (graceful).

**ÜRÜN "· %":** mockup "N · 100%" gösterir ama list DTO Σ% taşımıyor → ÜRÜN = ProductLineCount (+ "N SKU" alt satır). Σ% list'e KOYULMAZ (Detay/Düzenle'de gerçek Σ var). Hardcode "100%" YAZMA (geçersiz taslakta yanıltır).

## NE (frontend + 1 Web proxy)
1. **Web proxy** (`StrategyTemplatesController.cs`): `[HttpGet("api/scope-options")] ScopeOptions → ProxyGetAsync("/api/crm/strategy-templates/scope-options", ReadPermission, ct, ReadFallback)` (mevcut proxy deseni).
2. **Index.cshtml + _DataTable.cshtml:** başlık/alt-metin/"+ Yeni oyun" mockup; DataTable kolon başlıkları konsolide sete (OYUN/KAPSAM/SEGMENT/ÜRÜN/İÇERİK/DURUM/GÜNCELLENDİ + Actions). Mevcut DataTable/Save View/colReorder/actions **infra korunur**.
3. **_Filter.cshtml:** mevcut durum filtresine ek **ülke filtresi** (scope-options country feed'inden; select2/inline-filter deseni). "N / N oyun" sayacı.
4. **index.js:** kolon tanımlarını + renderer'ları mockup'a getir (KAPSAM scope-options map, SEGMENT/ÜRÜN/İÇERİK sayı+alt-satır, DURUM badge+vN, tarih). scope-options'ı bir kez yükle (label map + ülke filtre opsiyonları). Ülke filtresi client-side (EffectiveScopeType=country ise CountryScope eşleşmesi; ayrıca LE/BU'nun ülkesi map'ten türetilebiliyorsa opsiyonel — türetme yoksa yalnız country-scope satırları filtrelenir, DUR gerekmez). Save View personalizationContext korunur. Actions/goCreate/goEdit KORUNUR.
5. **index.l10n.js + _IndexL10n.cshtml + resx (7 dil):** yeni kolon başlıkları (KAPSAM/SEGMENT/ÜRÜN/İÇERİK/DURUM) + scope-tip etiketleri (ScopeType_tenant/country/legal-entity/business-unit) + "Tüm şirket" + başlık/alt-metin + "Tüm ülkeler". PascalCase alias köprüsü ([[l10n-bridge-pascalcase-loader]]) korunur.

## KORU / YAPMA
- Backend (CrmService) DEĞİŞMEZ (list DTO hazır). Yalnız Web proxy (1 satır) + liste frontend. Editör/Detay (Create/Edit/Details + form.js/details.js) DOKUNMA (Faz 2/3). DataTable/Save View/colReorder/actions/goCreate/goEdit + RBAC gate + soft-delete davranışı KORUNUR. scope-options map yoksa ham ref (graceful degrade). Hardcode scope/ülke/Σ% YOK. Tema/L10n köprüsü. Actions kolonu + row-click→Details korunur.

## Acceptance
- **E2:** Diten.Web.Tests yeşil (137→güncel sayı, main sonrası 201; regresyon yok). git diff: StrategyTemplatesController(proxy) + Index/_DataTable/_Filter.cshtml + index.js + index.l10n.js + _IndexL10n.cshtml + resx. CrmService diff YOK.
- **E4:** Liste mockup kolonları (OYUN/KAPSAM isim+tip/SEGMENT/ÜRÜN/İÇERİK/DURUM+vN/GÜNCELLENDİ) + durum & ülke filtresi + sayaç; row-click detay; Save View çalışır; tema-duyarlı. **Razor .cshtml → FLEET RESTART.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ST-LIST · StrategyTemplate liste konsolu mockup kolonları + kapsam + ülke filtresi (MOD-0167-FU04, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: 19022503 üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-LIST-strategy-template-list-columns.md · frontend/Diten.Web/Views/CRM/StrategyTemplates/{Index.cshtml,_DataTable.cshtml,_Filter.cshtml,_IndexL10n.cshtml} + wwwroot/assets/js/CRM/StrategyTemplates/{index.js,index.l10n.js} + Controllers/CRM/StrategyTemplatesController.cs (proxy deseni ~171-181) · (referans) MOD-0165 VisitFrequencyPolicies index.js (mockup-kolon+scope-options-map deseni benzer) · list DTO alanları services/.../StrategyTemplate/StrategyTemplateModels.cs (list row: ScopeType/EffectiveScopeType/CountryScope/LegalEntityId/BusinessUnitId/ScopeRef + *Count). Mockup: /c/tmp/mockup-strategy.html "Liste" (gerekirse py -m http.server ile aç).

NE (frontend + 1 Web proxy; CrmService DEĞİŞMEZ):
 1) Web proxy: StrategyTemplatesController'a [HttpGet("api/scope-options")] ScopeOptions → ProxyGetAsync("/api/crm/strategy-templates/scope-options", ReadPermission, ct, ReadFallback).
 2) Index/_DataTable: başlık "Strateji Oyunları"+alt-metin+"+ Yeni oyun"; kolonlar konsolide → OYUN(ad+kod alt+superseded) / KAPSAM(isim+scope-tip alt) / SEGMENT(sayı+subjectType) / ÜRÜN(ProductLineCount+"N SKU" alt) / İÇERİK(sayı) / DURUM(badge+vN) / GÜNCELLENDİ(dd.MM.yyyy) + Actions KORU. DataTable/SaveView/colReorder/actions infra korunur.
 3) _Filter: durum + ülke filtresi (scope-options country feed) + "N / N oyun" sayaç.
 4) index.js: kolon def+renderer mockup'a; scope-options bir kez yükle → KAPSAM label map (tenant→"Tüm şirket"/country→ad/legal-entity→ad/business-unit→ad, yoksa ham ref) + ülke filtre opsiyonları; ülke filtresi client-side (country-scope satırları eşleşir). SaveView/goCreate/goEdit/actions KORU.
 5) L10n 7 dil: kolon başlıkları + ScopeType_* etiketleri + "Tüm şirket"/"Tüm ülkeler" + başlık/alt-metin; PascalCase alias köprüsü.
KORU/YAPMA: CrmService DEĞİŞMEZ (list DTO hazır); editör/detay(Create/Edit/Details+form.js/details.js) DOKUNMA; DataTable/SaveView/colReorder/actions/RBAC/soft-delete KORU; scope-options yoksa graceful ham ref; hardcode scope/ülke/Σ% YOK (özellikle ÜRÜN'de "100%" hardcode etme); tema/L10n köprüsü.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo (regresyon yok); git diff StrategyTemplatesController+Index/_DataTable/_Filter/_IndexL10n.cshtml+index.js+index.l10n.js+resx; CrmService diff yok. Ayrı commit ("feat(strategy): WP-ST-LIST — liste konsolu mockup kolonları (KAPSAM/SEGMENT/ÜRÜN/İÇERİK) + ülke filtresi (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: ÜRÜN Σ% için backend gerekiyorsa (koyma, count göster); scope-options label semantiği belirsizse; değişiklik liste kapsamı dışına (editör/detay/CrmService) taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-21) → **ACCEPTED (E2)**
```
Commit: 5d14e953 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-stlist-verify @5d14e953
```
- ✅ **Kapsam (13 dosya, +233/−46):** StrategyTemplatesController (tek Web proxy action `api/scope-options`) + Index/_DataTable/_Filter/_IndexL10n.cshtml + index.js + 7 resx. **CrmService diff YOK** (list DTO scope+sayıları zaten taşıyor). Editör/Detay (Create/Edit/Details + form.js/details.js) dokunulmadı.
- ✅ **Kolonlar konsolide (17→7):** OYUN(ad+kod+superseded) / KAPSAM(isim+scope-tip) / SEGMENT(sayı+subjectType) / ÜRÜN(ProductLineCount+"N SKU") / İÇERİK(sayı) / DURUM(badge+vN) / GÜNCELLENDİ(dd.MM.yyyy) + Actions. SaveView(colIndexes 1-7)/colReorder/actions/goCreate/goEdit/RBAC/soft-delete KORUNDU.
- ✅ **KAPSAM label:** scope-options bir kez yüklenir → countryMap/leMap/buMap; `scopeName(row)` effectiveScopeType'a göre (tenant→"Tüm şirket"/country/legal-entity/business-unit; map-miss→ham ref graceful; feed hatası→liste yine render). **ÜRÜN Σ% hardcode YOK** (count + SKU count).
- ✅ **Ülke filtresi** client-side (country-scope satırları CountryScope eşleşmesi; feed scope-options; "Tüm ülkeler") + "N / N oyun" sayaç. Mevcut durum/subject/frequency/BU/include-archived filtreleri korundu.
- ✅ **L10n:** 12 yeni anahtar × 7 dil + PageDescription mockup alt-metni; PascalCase alias köprüsü korundu.
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **201/0**.
- ⏳ E4: Liste mockup kolonları + KAPSAM isim + ülke filtresi. **Razor .cshtml → FLEET RESTART.**

**WP-ST-LIST KOMPLE. Sıra: StrategyTemplate Faz 2 (Düzenle KAPSAM cascade) + Faz 3 (Detay); paralelde Campaign mockup bekleniyor.**
```
