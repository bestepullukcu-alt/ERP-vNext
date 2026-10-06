# WORK PACKAGE — WP-FREQ-F1 · Ağırlık band modeli (5 tier) + liste AĞIRLIK/FREKANS gösterimi (backend+frontend)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (`35a124ef` üstü). FREQ-A..E tamam. Kullanıcı KARARI: mockup'ın **5 kavramsal ağırlık tier'ı** contract'a alınacak (mevcut 10 specificity band'ı DEĞİŞTİR). Ağırlık = "çakışmada kim kazanır" (küçük değer öne geçer). **Resolve davranışı DEĞİŞMEZ** (ham Priority int kullanır; band'lar authoring/gösterim etiketidir). Ayrıca liste FREKANS gösterimi mockup'a hizalanır.

## Kullanıcı kararı (onaylı band modeli — küçük değer kazanır)
| Kod | Value | Renk (tone) | Etiket (tr) | Açıklama (tr) |
|---|---|---|---|---|
| `override-all` | 100 | danger (kırmızı) | Her şeyin üstünde | Kampanya ve segment kuralları dahil hepsini geçersiz kılar. |
| `campaign-level` | 300 | warning (turuncu) | Kampanya seviyesi | Kampanya döneminde standart kuralların önüne geçer. |
| `standard` | 500 | primary (mor) | Standart | Normal segment ve hedef kuralları için önerilen seviye. |
| `baseline` | 700 | info (mavi) | Temel | Bölge geneli için taban frekans; daha dar kurallar bunu geçer. |
| `last-resort` | 900 | secondary (gri) | Son çare | Başka hiçbir kural denk gelmezse devreye girer. |

## Kapsam
### 1) Backend (contract band listesi + Suggest + testler)
- `services/.../Domain/Entities/VisitFrequencyPolicy.cs` `FrequencyPriorityBands`: sabitleri + `All` listesini **yukarıdaki 5 tier'a** çevir (Code+Value). Eski 10 specificity band'ı kaldır.
- `Suggest(source, targetType)`: yeni tier'lara eşle — source manager-override → `override-all`; targetType campaign-target (veya source campaign) → `campaign-level`; aksi → `standard`. (Suggest çağrılmıyor ama tutarlı kalsın; auto-default YOK — Priority hâlâ explicit yazılır/valide edilir.)
- `Contract/VisitFrequencyContract.cs`: PriorityBands zaten `FrequencyPriorityBands.All`'dan gelir → 5 tier otomatik yansır (ek kod gerekmez; teyit et).
- `tests/.../VisitFrequencyPolicyTests.cs`: band kod/value assert'lerini 5 tier'a güncelle. **CrmService.Application.Tests yeni baseline yeşil** (sıfır-yeni-fail).
- **Priority int validation DEĞİŞMEZ** (>0). Resolve/CRUD/archive/soft-delete DEĞİŞMEZ.

### 2) L10n (7 dil, additive)
- `Band_{override-all,campaign-level,standard,baseline,last-resort}` etiket + `Band_{code}_Desc` açıklama (tablo). Eski `Band_{account,segment,...}` anahtarları artık kullanılmıyor — kaldırılabilir veya bırakılabilir (kullanılmadıkları için zararsız; tercihen temizle).
- `Period_{day,week,month,quarter,cycle,campaign-period,custom}` etiketleri (FREKANS "N / <dönem>" için). tr örnekleri: day→gün, week→hafta, month→ay, quarter→çeyrek, cycle→dönem, campaign-period→kampanya dönemi, custom→özel dönem.

### 3) Frontend
- `_DetailsQuickView.cshtml`: hardcoded `bandCodes` dizisini **5 yeni koda** güncelle (bandLabels doğru dolsun). Mümkünse contract'tan türet; değilse 5 kodu yaz.
- `index.js` (liste):
  - **AĞIRLIK** (`weightCell`): renkli badge — kod→tone map (`override-all`→danger, `campaign-level`→warning, `standard`→primary, `baseline`→info, `last-resort`→secondary); etiket `bandLabels[code]` (contract value→code eşlemesi mevcut `bandByWeight` ile). Eşleşme yoksa ham sayı fallback.
  - **FREKANS** (`frequencyCell`): frequencyType badge'ini **KALDIR**; `"{RequiredVisitCount} / {Period_<periodType>}"` (yerelleştirilmiş dönem) büyük + altyazı `FreqBounded`/`FreqOpenEnded` (EffectiveTo dolu/boş). Mockup resim 4: "2 / ay" + "süresiz geçerli".
- `_IndexL10n.cshtml`: `periodLabels` (code→Period_ L10n) map ekle; `bandLabels` 5 yeni kodu içerir. camelCase/PascalCase köprüsü (FREQ-D) korunur.

## KORU / YAPMA
- Resolve/CRUD/archive/soft-delete/validation (Priority>0) DEĞİŞMEZ — yalnız band **listesi/etiketi** değişir (authoring/gösterim). Segment/başka modül DOKUNMA. FREQ-C detay/çözümleme (resolve.js) davranışı DEĞİŞMEZ (bandLabels contract'tan). Editör ayrı-sayfa dönüşümü bu WP'de YOK (FREQ-F2) — mevcut offcanvas contract-driven olduğu için 5 band'ı otomatik gösterir, bozma. Golden Compact v2 + L10n köprüsü + gotcha'lar birebir. Hardcode vocabulary YOK (kodlar contract/enum'dan, etiketler L10n). Kaynak/verdict/reason etiketleri (FREQ-C/E) bozulmaz.

## Acceptance
- **E2:** CrmService.Application.Tests baseline-diff sıfır-yeni-fail (band testleri 5 tier'a güncel) + Diten.Web.Tests 137/0. git diff: VisitFrequencyPolicy.cs + VisitFrequencyPolicyTests.cs + (contract teyit) + _DetailsQuickView.cshtml + index.js + _IndexL10n.cshtml + resx (7 dil). Resolve/CRUD handler'ları diff YOK.
- **E4:** liste AĞIRLIK 5 renkli tier etiketiyle gelir (Segment/ham sayı değil); FREKANS "N / <yerel dönem>" + altyazı (badge yok); editör offcanvas 5 band'ı gösterir; detay bandLabels doğru.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-FREQ-F1 · Ağırlık band modeli (5 tier) + liste AĞIRLIK/FREKANS gösterimi (MOD-0165-FU03, backend+frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: 35a124ef üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-F1-band-model-list-display.md · services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/VisitFrequencyPolicy.cs (FrequencyPriorityBands + Suggest + FrequencyPeriodType) · services/.../Application/Features/VisitFrequencyPolicy/Contract/VisitFrequencyContract.cs · services/.../tests/Diten.CrmService.Application.Tests/VisitFrequencyPolicyTests.cs · frontend/Diten.Web/Views/CRM/VisitFrequencyPolicies/{_DetailsQuickView,_IndexL10n}.cshtml + wwwroot/assets/js/CRM/VisitFrequencyPolicies/index.js · resx (VisitFrequencyPoliciesIndex.*.resx).

NE:
 Backend: FrequencyPriorityBands sabit+All → 5 tier (override-all=100/campaign-level=300/standard=500/baseline=700/last-resort=900, küçük kazanır). Suggest → manager-override source=override-all; campaign-target/campaign=campaign-level; aksi=standard (auto-default YOK). Contract PriorityBands otomatik yansır (teyit). VisitFrequencyPolicyTests band assert'lerini 5 tier'a güncelle. Resolve/CRUD/archive/soft-delete/Priority>0 validation DEĞİŞMEZ.
 L10n (7 dil additive): Band_{5 kod} + Band_{kod}_Desc (WP tablosu); Period_{day,week,month,quarter,cycle,campaign-period,custom} (tr: gün/hafta/ay/çeyrek/dönem/kampanya dönemi/özel dönem).
 Frontend: _DetailsQuickView bandCodes → 5 yeni kod. index.js weightCell → renkli badge (override-all=danger/campaign-level=warning/standard=primary/baseline=info/last-resort=secondary, etiket bandLabels[code], value→code mevcut bandByWeight; eşleşmezse ham sayı). frequencyCell → frequencyType badge KALDIR, "N / <Period_ yerel>" + altyazı FreqBounded/FreqOpenEnded. _IndexL10n periodLabels map + bandLabels 5 kod. L10n camelCase/PascalCase köprüsü korunur.
KORU/YAPMA: resolve/CRUD/archive/soft-delete/validation DEĞİŞMEZ (yalnız band listesi/etiket + FREKANS gösterim); Segment/başka modül DOKUNMA; resolve.js/FREQ-C davranışı bozma; editör offcanvas'ı bozma (contract-driven, 5 band otomatik); hardcode vocabulary YOK (kod contract/enum, etiket L10n); Golden Compact v2 + gotcha'lar birebir.
DOĞRULA (E2): CrmService.Application.Tests baseline-diff sıfır-yeni-fail + Diten.Web.Tests 137/0; git diff sınırlı (VisitFrequencyPolicy.cs + testler + _DetailsQuickView + index.js + _IndexL10n + resx); resolve/CRUD handler diff yok. Release: dotnet test -c Release. Ayrı commit. §22 TÜRKÇE. K13.
Durma: band value/kod değişikliği resolve veya validation'ı etkiliyorsa; contract PriorityBands otomatik yansımıyorsa; test 5 tier'a uyarlanamıyorsa; kapsam VisitFrequencyPolicy dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)**
```
Commit: 14c82f00 · Agent: PASS · CT: ACCEPTED E2 (gerçek build) · izole /c/tmp/ct-freqf1-verify @14c82f00
```
- ✅ **Kapsam (13 dosya):** VisitFrequencyPolicy.cs + VisitFrequencyPolicyTests.cs + _DetailsQuickView + _IndexL10n + _CreateEditOffcanvas + index.js + 7 resx. **resolve.js / CRUD-resolve handler / ResolveEngine / Segment = 0 değişiklik** (grep 0).
- ✅ **Band modeli 5 tier:** OverrideAll=100 < CampaignLevel=300 < Standard=500 < Baseline=700 < LastResort=900 (küçük kazanır); `All` 5 kod; Suggest yeni tier'lara eşlendi (auto-default yok). Contract PriorityBands = FrequencyPriorityBands.All → 5 otomatik (test doğruluyor).
- ✅ **Resolve/validation değişmedi:** resolver ham int Priority kullanır; band'lar authoring/gösterim etiketi. Priority>0 validation, CRUD, archive, soft-delete diff yok.
- ✅ **Liste AĞIRLIK:** `weightCode(priority)`→`bandTones` renkli badge (override-all=danger/campaign-level=warning/standard=primary/baseline=info/last-resort=secondary), etiket `bandLabels[code]` (contract value→code), eşleşmezse ham sayı. Sort=numeric priority korundu.
- ✅ **Liste FREKANS:** frequencyType badge KALDIRILDI → "N / <Period_ yerel>" + altyazı FreqBounded/FreqOpenEnded (resim 4 gibi).
- ✅ **_CreateEditOffcanvas:** diff **yalnız band kod listesi** (10→5); davranış değişmedi. Ajanın §36.1 dışı bu dosyayı güncellemesi doğru karar — eski Band_ resx anahtarları kalkınca editör key-echo'ya düşerdi ("editörü bozma" KORU'su bunu gerektirdi). Kapsam VisitFrequencyPolicy içi.
- ✅ **L10n (7 dil):** Band_{5 kod}+_Desc, Period_{7 kod}; eski 10 Band_ temizlendi. camelCase/PascalCase köprüsü korundu.
- ✅ **Build+test (CT izole, Release):** CrmService.Application.Tests **1750/0/5** + Diten.Web.Tests **137/0**.
- ⏳ **E4:** hard-refresh sonrası liste AĞIRLIK renkli 5 tier + FREKANS "N / yerel dönem"; editör offcanvas 5 band; detay bandLabels doğru.

**FREQ-F1 KOMPLE. Sıra: FREQ-F2 (editör offcanvas → ayrı Golden Compact Create/Edit sayfası, mockup tam tasarım; 5 tier'ı kullanır).**
