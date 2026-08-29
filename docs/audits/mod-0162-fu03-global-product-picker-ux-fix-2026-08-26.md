# MOD-0162-FU03 — Global Product picker UX fix (frontend-only)

- **Tarih:** 2026-08-26
- **Pack:** `execution/domains/commercial-suite/module-packs/MOD-0162-FU03-concept-graph-runtime-ui.md` (`ready-for-dev`)
- **Kapsam:** ConceptNode Compact formundaki `ExternalRefId` alanı — AC-UI-2 picker'ının keşfedilebilir hâle getirilmesi
- **Backend:** **YOK** (proxy + MDM selector mevcuttu)
- **Sonuç:** **PARTIAL** — statik teslim tam; authenticated tarayıcı smoke ertelendi

---

## 1. Sorun

`ExternalRefId` düz bir `<input>` idi. `ExternalRefType = global-product` seçildiğinde `form.js` bu input'a bir
`<datalist>` bağlıyordu. `<datalist>` tarayıcıda **hiçbir görsel ipucu vermez** — ok yok, chip yok, açılır liste yok;
kullanıcı yazmaya başlamadan varlığını anlayamaz. Formun diğer tüm referans alanları (Subject, Concept Type, Status,
External Ref Type) Select2 chip olduğu için alan hem **keşfedilemez** hem **tutarsızdı**.

Ek olarak `<datalist>` MDM selector'ünü **tek sayfa** çekiyordu (`pageSize` gönderilmediği için varsayılan **20**),
yani 20'den fazla global ürün olan bir tenant'ta liste sessizce eksikti.

---

## 2. Çözüm

Aynı slot'ta **iki kontrol**, ekranda her zaman **tam biri**:

| `ExternalRefType` | Ekrandaki kontrol | Etiket |
|---|---|---|
| `global-product` | Select2 picker (MDM selector üzerinden aranabilir) | **Global Product** |
| diğer (`document` / `audience-profile` / `reference-data-value` / `other`) | serbest metin kutusu | **External Ref Id** |

**Form sözleşmesi DEĞİŞMEDİ.** Kalıcı tek kontrol serbest metin input'udur: `name="ExternalRefId"` taşır, DOM'da
**her zaman durur** ve picker devredeyken yalnızca **gizlenir** (`d-none`) — **disable edilmez**. Bu ayrım kritik:
*disabled bir input post edilmez, gizli input edilir.* Böylece kayıtlı değer her hâlükârda round-trip'i atlatır.
Picker'ın `name` attribute'u **yoktur**, hiç post edilmez; seçimini input'a yansıtır.

### Select2 init — neden `width: '100%'`, `width: 'element'` değil

Talepte `frontend-js-standard` init parametreleri (`dropdownParent`, `width:'element'`, `dropdownCssClass`) anıldı.
`width: 'element'` **inline-filter chip**'lerinin parametresidir; bu bir **form alanıdır** ve formun diğer Select2'leri
`initWidgets()` içinde `width: '100%'` + `position-relative` sarmalayıcı `dropdownParent` ile kuruluyor. Picker aynı
şekilde kuruldu:

```js
$(picker).select2({
    dropdownParent: $(pickerWrap),                       // position-relative sarmalayıcı — kardeşleriyle aynı
    dropdownCssClass: 'concept-global-product-dropdown',
    width: '100%',
    ...
});
```

`width: 'element'` burada **iki kez** yanlış olurdu: (a) kardeş alanlardan farklı genişlik → düzeltilmek istenen
tutarsızlığın ta kendisi; (b) picker `d-none` içinde doğduğu için 'element' genişliği **0** ölçerdi. Bu yüzden Select2
ayrıca **ilk görünür olduğu anda** kurulur (`ensureSelect2()`), sayfa açılışında değil.

> Select2 `<select>`'i gizleyip **kendi kardeş container**'ını çizdiği için görünürlük `<select>` üzerinden
> yönetilemez; bu yüzden `#conceptGlobalProductWrap` sarmalayıcısı eklendi ve `d-none` ona uygulanıyor.

### Kaynak ve arama

Picker mevcut proxy'yi tüketir: `/CRM/KnowledgeConcepts/api/global-product-options` → `/api/global-products/selector`.
Option label = `CanonicalCode — GlobalProductName`, value = Global Product `Id`.

MDM selector **sayfalı bir ARAMA endpoint'idir** (`search`, `pageNumber`, `pageSize`; `pageSize` üst sınırı 100), ve
proxy query string'i olduğu gibi iletir. Bu yüzden picker Select2 `ajax` moduyla çalışır: yazdıkça `search` gönderir,
`pageSize=100` ile çalışır. Liste kaydırarak değil **yazarak** daraltılır — endpoint'in tasarımı budur.
`credentials` gerekmez: same-origin proxy'ye jQuery zaten session cookie'sini gönderir, token'ı MVC proxy server-side
ekler. Tarayıcı `Authorization` header'ı üretmez (grep ile doğrulandı).

### EnsureSelected — Edit'te ham GUID yok

Selector sayfalı olduğu için kayıtlı bir ürün, arama yapılmadan gelen ilk sayfada **genelde bulunmaz**. Bu yüzden
seçili değer **server-side** çözümlenir:

```csharp
model.GlobalProductSelectedLabel = await ResolveGlobalProductLabelAsync(model.ExternalRefType, model.ExternalRefId, ct);
// ExternalRefType == "global-product" && Guid.TryParse(ExternalRefId) → GET /api/global-products/{id}
//   → "canonicalCode — globalProductName"
```

View bu etiketi **ön-seçili option** olarak basar. Çözümleme başarısızsa (404/izinsiz/parse) etiket **ham id**'ye düşer
— asla boş bırakılmaz; değerin round-trip'te kaybolmaması bundan önemlidir.

MDM **read-only**: yalnız okunur, node'a hiçbir master alanı kopyalanmaz.

### AC-UI-2 korundu

Controller selector'ü zaten prob ediyor. **404 → `GlobalProductEndpointMissing`**, **403 →
`GlobalProductPermissionMissing`**, diğer hatalar → `GlobalProductPickerUnavailable`. Bu durumda picker
**disabled + gerekçe notu** ile render edilir — sessiz boş liste değil. Çalışma anında proxy `{ disabled, reason }`
dönerse veya AJAX düşerse aynı yol izlenir. Mevcut değer yine post edilir (gizli ≠ disabled).

### Tip değişiminde veri davranışı (bilinçli)

- **global-product'tan çıkış:** değer **silinmez**. Serbest metin kutusu görünür olur ve kullanıcı tam olarak neyin
  kaydedileceğini görür. Burada otomatik temizlik sessiz veri değişimi olurdu.
- **global-product'a giriş** ve kutudaki değer bir ürün seçimi değilse: her iki kontrol de **temizlenir**. Bu, açık bir
  kullanıcı eylemidir (tip değiştirildi), sessiz düzenleme değil. İlk render'da **çalışmaz** — orada sunucu gerçek bir
  kayıtlı ürünü çözümlemiştir (`booted` bayrağı).

### Yan bulgu (aynı dosyada düzeltildi)

`Create.cshtml` / `Edit.cshtml` `_IndexL10n` köprüsünü yüklemez, dolayısıyla bu sayfalarda `window.L10n` **boştur** —
eski `form.js` picker mesajlarını `L.*`'dan okuyup İngilizce hardcode'a düşüyordu. Artık **her** kullanıcı-yüzü metin
Razor tarafından lokalize edilip `data-*` attribute'undan okunuyor.

---

## 3. Değişen dosyalar

| Dosya | Değişiklik |
|---|---|
| `Views/CRM/KnowledgeConcepts/_Form.cshtml` | İki kontrollü external-ref slot'u + iki etiket + iki ipucu + ön-seçili option + lokalize `data-msg-*` |
| `wwwroot/assets/js/CRM/KnowledgeConcepts/form.js` | `setupGlobalProductPicker` yeniden yazıldı (datalist → Select2 ajax + görünürlük + AC-UI-2) |
| `Controllers/CRM/KnowledgeConceptsController.cs` | `ResolveGlobalProductLabelAsync` (EnsureSelected, salt-okuma) |
| `Models/CRM/KnowledgeConceptViewModels.cs` | `GlobalProductSelectedLabel` (additive) |
| `Resources/Views/CRM/KnowledgeConcepts/KnowledgeConceptsIndex.{7}.resx` | +6 anahtar × 7 dil (`en`/`tr` gerçek) |

**Dokunulmayan:** diğer aggregate/tab'lar (`concept-slim.js`, `graph-preview.js`, Slim offcanvas'ları, Graph Preview),
FU02 dosyaları, backend, `ocelot.json`, MDM, RBAC, registry.

---

## 4. Kalite kapısı

```
dotnet build frontend/Diten.Web/Diten.Web.csproj -t:CoreCompile  → 0 Hata
node --check form.js                                            → OK
7/7 RESX xml.etree parse                                        → well-formed
_Form.cshtml Localizer anahtarları × 7 dil                       → eksik yok
grep: localhost:5 | :5000 | Bearer | document.cookie | access_token → sıfır
```

### DataTable verifier — ham çıktı diff'i

| Koşu | Passed | Failed |
|---|---|---|
| `--area CRM --module KnowledgeConcepts --reference compact` | **87** | **8** |
| `--area CRM --module Knowledge --reference compact` (FU02 baseline) | **87** | **8** |

FAIL setleri **satır satır aynı** (yalnız dosya yolları farklı):

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

⇒ **diff = ∅.** 7'si archive-only modülün belgeli N/A'sı, 1'i paylaşılan `personalization-client.js`'e ait.

Bu dokunuşun kritik iki kontrolü de **PASS**:

```
[PASS] Compact _Form.cshtml matches Details.cshtml section/card map
[PASS] Required label markers match ViewModel required metadata
```

> **Hedef sayı hakkında düzeltme.** Talepte "85/9 eşdeğerliği" geçiyor; bu modülün ölçülmüş baseline'ı Dilim A, B ve
> C boyunca **87 passed / 8 failed** oldu ve FU02 `CRM/Knowledge` koşusu da aynı değeri veriyor. 85/9 bu repoda hiçbir
> koşuda üretilmedi. Bu teslim baseline'ı **değiştirmedi** (87/8 → 87/8); doğru karşılaştırma noktası budur.

---

## 5. Ertelenenler

| # | Madde | Neden |
|---|---|---|
| 1 | Authenticated tarayıcı smoke | Web `:5001` önceki build'i koşuyor; `.resx` tam restart + operatör login ister |
| 2 | 5 dil gerçek çeviri | Follow-up **F-L10N** |

### Smoke adımları

```
/CRM/KnowledgeConcepts/Create
 1. External Ref Type = global-product  → "Global Product" etiketli Select2 GÖRÜNÜR olmalı,
    komşu picker'larla aynı genişlik/stil; serbest metin kutusu kaybolmalı
 2. Yazınca sunucu-taraflı arama çalışmalı (20 kayıt sınırı YOK), label "kod — ad"
 3. Bir ürün seç → Save → Details'te ExternalRefId o ürünün Id'si olmalı
 4. Edit → picker "kod — ad" ile AÇILMALI (ham GUID DEĞİL)
 5. Type = document → serbest metin kutusu geri gelmeli, değer SİLİNMEMELİ
 6. Type'ı tekrar global-product yap → her iki kontrol de temizlenmeli
 7. mdm.global-products.read izni olmayan kullanıcı → picker disabled + gerekçe notu,
    kayıtlı değer Save sonrası KORUNMALI
```
