# MOD-0162-FU03 — Artım 2 / Dilim C: Graph Preview + FU02 `_Form` AC-UI-3

- **Tarih:** 2026-08-26
- **Pack:** `execution/domains/commercial-suite/module-packs/MOD-0162-FU03-concept-graph-runtime-ui.md` (`ready-for-dev`, `runtime_code_allowed: true`)
- **Kapsam:** §7.1 Tab 5 (Graph Preview, salt-okunur) + AC-UI-3 (FU02 içerik formundaki concept selector'ın etkinleştirilmesi)
- **Öncesi:** Artım 1 (backend) + Dilim A (ConceptNode Compact) + Dilim B (3 Slim tab) — SHIPPED
- **Sonuç:** **PARTIAL** — statik teslim tam; authenticated tarayıcı smoke ertelendi (§7)

---

## 1. Teslim edilenler

### Graph Preview (Tab 5)

| Dosya | Rol |
|---|---|
| `Views/CRM/KnowledgeConcepts/_GraphPreview.cshtml` | **YENİ** — salt-okunur komşuluk görünümü (DataTable yok, golden set yok) |
| `wwwroot/assets/js/CRM/KnowledgeConcepts/graph-preview.js` | **YENİ** — 3 kapsamın render'ı |
| `Views/CRM/KnowledgeConcepts/Index.cshtml` | 5. sekme (`#tab-concept-graph`) + script include |
| `Views/CRM/KnowledgeConcepts/_IndexL10n.cshtml` | 25 Graph Preview anahtarı köprüye eklendi |
| `Controllers/CRM/KnowledgeConceptsController.cs` | 3 salt-okuma proxy allowlist satırı: `api/concept-graph/by-node/{id}`, `api/concept-graph/by-content/{id}`, `api/contents` |

### FU02 `_Form` AC-UI-3

| Dosya | Değişiklik |
|---|---|
| `Views/CRM/Knowledge/_Form.cshtml` | **Pack'in adlandırdığı tek dokunuş** — `disabled` concept selector, canlı Subject → ConceptType → ConceptNode zincirine dönüştü |
| `wwwroot/assets/js/CRM/Knowledge/form.js` | `setupConceptCascade()` eklendi, `boot()`'a bağlandı |
| `Controllers/CRM/KnowledgeController.cs` | `concept-types` / `concept-nodes` option yüklemesi + `EnsureSelectedAsync` + label çözümleyicisine `conceptNodeName/Code`, `conceptTypeName/Code` |
| `Models/CRM/KnowledgeViewModels.cs` | `ConceptTypeOptions` + `ConceptNodeOptions` (additive) |
| `Resources/Views/CRM/Knowledge/KnowledgeIndex.{7}.resx` | +3 anahtar × 7 dil |
| `Resources/Views/CRM/KnowledgeConcepts/KnowledgeConceptsIndex.{7}.resx` | +25 anahtar × 7 dil |

> **Kapsam notu — "tek dokunuş" nasıl okundu.** §12, FU02 tarafında *`_Form.cshtml`*'i kasıtlı istisna olarak adlandırır;
> korunan şey **FU02 sözleşmesidir** (`KnowledgeContent` alanları + `IKnowledgeContentLinkageReader` imzası), FU02
> frontend'inin tamamı değil. Alanı gerçekten canlı yapmak için besleyici üç ek dosya gerekti (VM + controller + form.js);
> üçü de **tamamen additive**'dir: alan eklenmedi, kaldırılmadı, imza değişmedi, yeni endpoint açılmadı. Bunun yerine
> istemci tarafından FU03 proxy'sini çağırmak, sayfayı kendi modülünün proxy'si dışına çıkarır ve arşivli-değer
> korumasını JS'e taşırdı — ikisi de daha kötü. FU02 verifier koşusu bu üç dosyadan sonra da **baseline'da** (§5).

**Yeni backend endpoint açılmadı.** `ocelot.json` değişmedi.

---

## 2. Graph Preview — sınır (FU01C) nasıl korundu

Üç mevcut read endpoint'i, olduğu gibi tüketilir:

| Kapsam | Çağrı | Derinlik |
|---|---|---|
| Tüm konu | `GET api/concept-graph?subjectId=…&effectiveAt=…&includeArchived=…` | komşuluk okuması (node + kenar + şablon listesi) |
| Dügüm çevresi | `GET api/concept-graph/by-node/{id}?includeArchived=…` | **tam 1 hop** |
| İçerikten | `GET api/concept-graph/by-content/{id}?includeArchived=…` | **tam 2 kenar katmanı** |

Yapılmayanlar — ve neden kod düzeyinde yapılamayacağı:

- **`depth` / `maxHops` kontrolü YOK.** UI'da böyle bir alan yok, JS böyle bir parametre üretmiyor, proxy böyle bir
  parametre iletmiyor. Derinlik sözleşme tarafından sabittir (AC-GRAPH-DEPTH); buraya bir derinlik kontrolü eklemek
  özellik değil, sınır ihlali olurdu.
- **Transitif kapanış / ikinci hop istemci tarafında hesaplanmıyor.** `graph-preview.js` yalnızca endpoint'in döndürdüğü
  `nodes` / `edges` / `templates` dizilerini basar; kenarlardan yeni kenar türetmez.
- **Skorlama / en-iyi-yol / öneri / best-next-content YOK.** Kenarlar servisin verdiği sırayla (`Priority` →
  `RelationshipCode`) basılır; UI yeniden sıralamaz.
- **"Bu düğüme odaklan" düğmesi gezinmedir, gezinti (traversal) değil.** Aynı 1-hop endpoint'ini farklı bir çıkış
  düğümüyle yeniden okur; hop biriktirmez, yol hatırlamaz.
- **`effectiveAt` bir derinlik değildir.** Contract'ın `SupportedFilters.Graph` listesinde yayınlanan etkinlik-tarihi
  filtresidir; UI'da öyle etiketlenir ve yalnız "tüm konu" kapsamında görünür (by-node/by-content zaten almaz).
- **Yazma yüzeyi YOK.** Sekmede hiçbir POST/PUT yok; ekranda kalıcı "salt okunur" notu var.
- **Boş = boş.** Veri yoksa boş durum gösterilir; varsayılan graf uydurulmaz (MOD-0151 R11 ruhu). `by-content`'in
  "hiç link yok" hâli, "konuda veri yok" hâlinden **ayrı** bir mesajla gösterilir — 200 + boş graf bir hata değildir.

Golden reference: **yok** (§7.1: read-only ⇒ golden set yok, DataTable yok, verifier'dan muaf).

---

## 3. AC-UI-3 — kararlar

1. **Zincir: Subject → ConceptType → ConceptNode.** `ConceptType` yalnızca **daraltma kontrolüdür**: `name` attribute'u
   bilinçli olarak yoktur, dolayısıyla **post edilmez**. Kalıcı tek değer hâlâ `KnowledgeContent.ConceptNodeId`'dir —
   FU02 form sözleşmesi genişlemedi.
2. **Arşivli düğüm listelenmez** (AC-UI-3). `LoadOptionsAsync` arşivli satırları zaten atar.
3. **Ama kayıtlı arşivli değer korunur.** `EnsureSelectedAsync` ile listeye geri eklenir ve `(Archived)` etiketiyle
   gösterilir. Bu, sessiz veri kaybına karşı zorunludur: backend V17 **dirty-check**'tir — yalnız *değişmemiş* değeri
   doğrulamadan geçer. Değer round-trip'te düşerse "değişmiş" sayılır ve içeriğin başka bir alanını düzenleyip Save
   eden kullanıcı ya 400 alır ya da referansı sessizce siler. Korunan seçenek ikisini de engeller.
4. **Cascade yalnız gerçek kullanıcı değişiminde çalışır**, sayfa açılışında değil — aksi hâlde mevcut kaydı açmak,
   kullanıcı hiçbir şeye dokunmadan kayıtlı düğümü temizlerdi. (FU02'nin Subject→Topic cascade'i ile aynı desen.)
5. **Konu değişirse hem tip hem düğüm sıfırlanır** — ikisi de eski konuya bağlı kalamaz.

---

## 4. Kalite kapısı

### Build

```
dotnet build frontend/Diten.Web/Diten.Web.csproj -t:CoreCompile
→ Oluşturma başarılı oldu. 0 Hata
```

### JS

```
node --check graph-preview.js        → OK
node --check CRM/Knowledge/form.js   → OK
```

### L10n

- `KnowledgeConceptsIndex.*.resx` → +25 anahtar × 7 dil
- `KnowledgeIndex.*.resx` (FU02) → +3 anahtar × 7 dil
- 14 dosyanın 14'ü `xml.etree` ile parse edildi → well-formed
- `graph-preview.js`'in kullandığı her `L.*` anahtarı köprüde; köprüdeki her anahtar 7 RESX'in 7'sinde de var;
  `_Form.cshtml`'in kullandığı her `Localizer[...]` anahtarı 7 FU02 RESX'inde de var → **eksik anahtar yok**
- `en`/`tr` gerçek metin; `ar`/`es`/`fr`/`ru`/`zh` İngilizce placeholder → follow-up **F-L10N**

### Boundary grep

`maxHops|depth=|recommend|best-next|traversal|closure|score|ranking` → yalnızca **yorum satırı** eşleşmeleri
(neden yapılmadığını açıklayan metin); çalışan kodda sıfır.
`localhost:5|:5000|Bearer |document.cookie|access_token` → çalışan kodda sıfır (tek eşleşme bir yorum satırı).

---

## 5. DataTable verifier — ham çıktı diff'i

Üç koşu yapıldı. Graph Preview §7.1 gereği verifier'dan **muaftır** (golden set yok, DataTable yok), bu yüzden ayrı bir
koşusu yoktur; ancak Tab 5'i eklemek `Index.cshtml`'i değiştirdiği için **mevcut iki koşu regresyon kapısı olarak**
yeniden çalıştırıldı. AC-UI-3, FU02 dosyalarına dokunduğu için **FU02 modülü de** yeniden koşuldu.

| Koşu | Passed | Failed | Dilim B sonucu | Değişim |
|---|---|---|---|---|
| `--area CRM --module KnowledgeConcepts --reference compact` | 87 | 8 | 87 / 8 | **yok** |
| `--area CRM --module KnowledgeConcepts --reference slim` | 80 | 10 | 80 / 10 | **yok** |
| `--area CRM --module Knowledge --reference compact` (FU02 baseline) | 87 | 8 | 87 / 8 | **yok** |

### FAIL setleri satır satır

**KnowledgeConcepts `--reference compact` (8) — FU02 baseline ile birebir aynı:**

```
[FAIL] personalizationClient sends tenant header only for tenant users
[FAIL] _DataTable.cshtml has select-all checkbox header (dt-checkboxes-select-all)
[FAIL] index.js declares bulk action config (bulkOptions / bulkBarSelector)
[FAIL] index.js wires bulk selection (getSelectedIds(...) or onBulkAction)
[FAIL] index.js calls bulk endpoint (.../bulk)
[FAIL] index.js wires bulk delete trigger (#btnBulkDelete | .bulk-delete-btn | [data-bulk-action])
[FAIL] index.js uses shared reload-with-toast lifecycle (DitenDataTable.reloadWithToast)
[FAIL] index.js wires clear-selection (clearSelectionSelector or clearSelection())
```

**Knowledge (FU02) `--reference compact` (8) — aynı 8 kontrol, yalnız dosya yolları FU02'nin:**

```
[FAIL] personalizationClient sends tenant header only for tenant users
[FAIL] _DataTable.cshtml has select-all checkbox header (dt-checkboxes-select-all)
[FAIL] index.js declares bulk action config (bulkOptions / bulkBarSelector)
[FAIL] index.js wires bulk selection (getSelectedIds(...) or onBulkAction)
[FAIL] index.js calls bulk endpoint (.../bulk)
[FAIL] index.js wires bulk delete trigger (#btnBulkDelete | .bulk-delete-btn | [data-bulk-action])
[FAIL] index.js uses shared reload-with-toast lifecycle (DitenDataTable.reloadWithToast)
[FAIL] index.js wires clear-selection (clearSelectionSelector or clearSelection())
```

⇒ **diff = ∅.** 7'si archive-only modülün belgeli N/A'sı (DELETE endpoint'i yok), 1'i modüle değil paylaşılan
`personalization-client.js` dosyasına ait.

**KnowledgeConcepts `--reference slim` (10) = yukarıdaki 8 + hibrit F-VERIFY 2'si:**

```
[FAIL] Slim reference has _CreateEditOffcanvas.cshtml
  - Missing: ...\Views\CRM\KnowledgeConcepts\_CreateEditOffcanvas.cshtml
[FAIL] Slim _CreateEditOffcanvas.cshtml has #offcanvasCreateEdit
  - Slim modules must provide create/edit offcanvas
```

Bu ikisi Dilim B'de gerekçelendirildi ve pack §18'e **F-VERIFY** olarak yazıldı: script modül klasörü başına *tek*
`_CreateEditOffcanvas.cshtml` varsayar, §11 ise aggregate başına ayrı offcanvas ister. Kapatmanın tek yolu, yalnız
dosya-adı kontrolü için bir dosya üretmekti; Index'ten include edilseydi **compact koşusu** `Compact Index does not
include create/edit offcanvas` ile 9. FAIL'i alırdı, edilmeseydi ölü kod olurdu.

**Dilim C'nin AC-UI-3 dokunuşunun FU02 koşusunda kritik olan iki kontrolü — ikisi de PASS:**

```
[PASS] Compact _Form.cshtml matches Details.cshtml section/card map
[PASS] Required label markers match ViewModel required metadata
```

Yani yeni iki select mevcut section haritasını bozmadı ve sahte required üretmedi.

*(Üç koşunun tam ham çıktısı bu dilimin teslim mesajına yapıştırıldı.)*

---

## 6. Sınır uyumu (aşılmadı)

- **FU01C:** motor / traversal / recommendation / scoring / best-next-content **yok** (§2'de kod düzeyinde gerekçelendirildi).
- **FU02 sözleşmesi:** `KnowledgeContent` alanı eklenmedi/kaldırılmadı; `IKnowledgeContentLinkageReader` imzasına
  dokunulmadı; FU02 contract flag'leri değişmedi. Tek davranış değişikliği, pack §5'in zaten öngördüğü V17
  dirty-check'idir ve o **Artım 1'de** shipped.
- **MDM:** bu dilim MDM'i hiç çağırmaz.
- **Protected paths:** `ocelot.json` · `services/Diten.MdmService/**` · MOD-0165/0164/0155 · RBAC seed/role template ·
  MOD-0048 publish · Mongo hand-edit · `execution/registries/**` · FU01A/FU01B pack dosyaları — **hiçbiri değişmedi**.
- **RBAC:** seed/grant yok; Dilim A/B'nin belgelenmiş DEV-ONLY fallback'i aynen kullanılır, guard gevşetilmedi.

---

## 7. Ertelenenler / açık maddeler

| # | Madde | Neden |
|---|---|---|
| 1 | **Authenticated tarayıcı smoke** | Fleet ayakta (Gateway `:5000` → contract 401 = runtime canlı) ama Web `:5001` bu dilimden önceki build'i koşuyor ve `.resx` tam restart ister; operatör login'i gerekiyor. |
| 2 | FU02 `Details.cshtml`'de ConceptNodeId'nin ham GUID yerine çözümlenmiş ad göstermesi | AC-UI-3 yalnız **form** alanını adlandırır; Details'e dokunmak kapsam genişletmesi olurdu → yeni follow-up **F-UI-DETAILS** |
| 3 | 5 dil gerçek çevirisi | Follow-up **F-L10N** |
| 4 | Verifier hibrit slim desteği | Follow-up **F-VERIFY** |
| 5 | `crm.knowledge.concept.*` katalog + grant | Follow-up **F-RBAC** (AC-SEQ-3) |

### Smoke için sıradaki adım

```powershell
# 1) Fleet'i yeniden başlat (RESX değişikliği tam restart ister)
# 2) /CRM/KnowledgeConcepts → Graph Preview sekmesi:
#    - "Tum konu" + subject seç → node/kenar/şablon listeleri dolmalı; ekranda derinlik notu görünmeli
#    - Bir düğümde "Bu düğüme odaklan" → kapsam otomatik "node"a geçmeli, TAM 1 hop dönmeli
#    - "Bir icerikten" + linki olmayan bir içerik → hata değil, "bağlı değil" boş durumu
#    - Ekranda hiçbir yerde derinlik/hop ayarı OLMAMALI
# 3) /CRM/Knowledge → bir içerik Edit:
#    - Concept Type seç → Concept Node listesi daralmalı; arşivli düğüm listede OLMAMALI
#    - Kayıtlı arşivli düğümü olan bir içeriği aç, BAŞKA bir alanı değiştirip Save → 400 almamalı,
#      ConceptNodeId aynı kalmalı (V17 dirty-check + korunan seçenek)
#    - Subject değiştir → hem tip hem düğüm sıfırlanmalı
```
