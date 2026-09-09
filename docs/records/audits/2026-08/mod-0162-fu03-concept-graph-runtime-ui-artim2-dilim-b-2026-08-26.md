# MOD-0162-FU03 — Artım 2 / Dilim B: 3 Slim aggregate tab (frontend)

- **Tarih:** 2026-08-26
- **Pack:** `execution/domains/commercial-suite/module-packs/MOD-0162-FU03-concept-graph-runtime-ui.md` (`ready-for-dev`, `runtime_code_allowed: true`)
- **Kapsam:** §7.1 hibrit yüzey haritasının Slim yarısı — Tab 1 ConceptType, Tab 3 ConceptRelationship, Tab 4 ConceptChainTemplate
- **Öncesi:** Artım 1 (backend runtime) + Dilim A (ConceptNode Compact UI) — SHIPPED
- **Sonuç:** **PARTIAL** — statik teslim tam; authenticated runtime smoke ertelendi (aşağıda §7)

---

## 1. Teslim edilenler

### Yeni view partial'ları (`frontend/Diten.Web/Views/CRM/KnowledgeConcepts/`)

| Dosya | Rol |
|---|---|
| `_TypeCreateEditOffcanvas.cshtml` | Tab 1 create/edit offcanvas (`#offcanvasTypeCreateEdit`) |
| `_TypeDetailsQuickView.cshtml` | Tab 1 salt-okunur alan haritası |
| `_RelationshipCreateEditOffcanvas.cshtml` | Tab 3 create/edit offcanvas (`#offcanvasRelationshipCreateEdit`) |
| `_RelationshipDetailsQuickView.cshtml` | Tab 3 salt-okunur alan haritası |
| `_TemplateCreateEditOffcanvas.cshtml` | Tab 4 create/edit offcanvas (`#offcanvasTemplateCreateEdit`) + tip sırası editörü |
| `_TemplateDetailsQuickView.cshtml` | Tab 4 salt-okunur alan haritası |
| `_TypesFilter.cshtml` · `_TypesDataTable.cshtml` | Tab 1 inline filter host + DataTable v2 |
| `_RelationshipsFilter.cshtml` · `_RelationshipsDataTable.cshtml` | Tab 3 inline filter host + DataTable v2 |
| `_TemplatesFilter.cshtml` · `_TemplatesDataTable.cshtml` | Tab 4 inline filter host + DataTable v2 |

> §11 Views globu açık olduğu için filter/DataTable partial'ları listelenmemiş olsa da kapsam içidir; MVC kuralı
> ("karmaşık view `_` prefixli partial'lara bölünür") gereği her tab kendi iki partial'ına ayrıldı.

### Değişen dosyalar

| Dosya | Değişiklik |
|---|---|
| `Views/CRM/KnowledgeConcepts/Index.cshtml` | 4 sekmeli konsol iskeleti (Types / Nodes / Connections / Templates) + paylaşılan `#offcanvasDetailsPreview` kabuğu + 3 Slim offcanvas include'u + `concept-slim.js` script'i |
| `Views/CRM/KnowledgeConcepts/_IndexL10n.cshtml` | 51 yeni anahtar `window.L10n` köprüsü payload'ına eklendi |
| `wwwroot/assets/js/CRM/KnowledgeConcepts/concept-slim.js` | **YENİ** — 3 Slim tab'ın tamamı (tek paylaşılan builder) |
| `wwwroot/assets/js/CRM/KnowledgeConcepts/index.js` | Toolbar arama seçicileri **bu tablonun container'ına** scope'landı (sayfa artık 4 DataTable taşıyor) |
| `Controllers/CRM/KnowledgeConceptsController.cs` | `GET api/subjects` proxy allowlist satırı (mevcut FU02 endpoint'ine salt-okuma) |
| `Resources/Views/CRM/KnowledgeConcepts/KnowledgeConceptsIndex.{7}.resx` | ×7 dil, dosya başına **+51 anahtar** |
| Pack §18 | `F-L10N` ve `F-VERIFY` follow-up satırları |

**Yeni backend endpoint açılmadı.** Tüm trafik mevcut `/api/crm/knowledge/*` yüzeyine, mevcut Gateway wildcard'ı
üzerinden gider; `ocelot.json` değişmedi.

---

## 2. §7.1 sözleşmesine uyum

| Gereklilik | Durum | Not |
|---|---|---|
| Tab 1 ConceptType Slim, `SubjectId` ZORUNLU | ✔ | Form alanı `required` + kırmızı yıldız; subject create'te sabitlenir (update sözleşmesinde `SubjectId` yok) |
| Cross-subject reddi backend'de | ✔ | UI kural taşımaz; V03/V05 backend'de kalır, hata mesajı toast + inline alert olarak yüzeye çıkar |
| Tab 3 `RelationshipType` = D3 kanonik seti, **text input değil** | ✔ | Select2, kaynağı `contract.vocabularies.relationshipTypes` (`leads-to` · `requires` · `addresses` · `evidences` · `belongs-to` · `custom`) — hardcoded liste yok |
| From/To picker'ları subject-scoped | ✔ | `nodeOptionsFor(subjectId)`; subject değişince picker'lar yeniden kurulur |
| Duplicate (From,To,Type) → 409, cycle → 400 | ✔ | UI ön-kontrol yapmaz; backend cevabı offcanvas alert + toast olarak gösterilir |
| `IsTemplateConforming` **GÖRÜNÜR**, sessiz gizleme/ret yok | ✔ | Kendi DataTable kolonu (rozet) + quick view alanı + edit formunda uyarı bandı + conformance filtre çipi |
| Tab 4 beklenen TİP sırası editörü | ✔ | Ekle / yukarı / aşağı / kaldır + numaralı liste; sıradaki tip picker'dan düşer |
| V12/V13 backend'de | ✔ | Editör yalnız "min 2" ve "tekrarlı tip" için inline uyarı verir; kararı yine backend yazar (yabancı subject tipi, örtüşen published pencere) |
| Graph Preview (Tab 5) | — | Dilim C; bu dilimde sekme **eklenmedi** (ölü placeholder üretilmedi) |
| FU02 `_Form.cshtml` AC-UI-3 dokunuşu | — | Dilim C; bu dilimde dokunulmadı |

---

## 3. Dilim A deseninin birebir izlenmesi

- `DtDefaults.create()` + `DtDefaults.exportButtons(...)` + DataTables v2 constructor
- Inline filter: `#...FilterHost` (`class="dt-inline-filter-host"` — ikinci host'un stilsiz kalmaması için zorunlu)
  + `#...FilterCollapse` + `pt-0 pb-3` sarmalayıcı + `data-no-tracker` form; `index.js`/`concept-slim.js`
  host'u toolbar'ın altına taşır ve `px-3` uygular (`mx-*` yok)
- Select2 çipleri: `dropdownParent: body`, `dropdownCssClass: 'dt-inline-filter-dropdown'`, `width: 'element'`,
  multi-select için `syncMultiSelectSummary` (placeholder + sayaç rozeti + clear)
- Save View: paylaşılan `personalizationClient`, tab başına bir `pageKey`
  (`KnowledgeConceptTypes` / `KnowledgeConceptRelationships` / `KnowledgeConceptChainTemplates`);
  payload `viewName` boş gönderilmez (`… || L.SaveView || 'Default'`)
- **Reset = fabrika durumu** (boş filtreler + boş arama + default colVis + doğal kolon sırası + default sıralama),
  saved view'e dönüş DEĞİL
- `colReorder: { columns: ':gt(0):not(:last-child)' }` + `column-reorder.dt` / `columns-reordered.dt` dirty-state'e bağlı
- Archive lifecycle: DELETE yok; `showConfirm` → `POST /{kind}/{id}/archive` → toast → tablo reload.
  Bu üç aggregate'te **unarchive endpoint'i yok**, bu yüzden arşivli satır yalnızca görüntülenir (Edit/Archive sunulmaz)
- L10n köprüsü: `_IndexL10n.cshtml` JSON payload → `index.l10n.js` → `window.L10n` (PascalCase anahtarlar)
- Proxy profile: her çağrı `/CRM/KnowledgeConcepts/api/...`; JS'te `document.cookie`, `access_token`,
  `Authorization: Bearer` veya doğrudan servis portu **yok**

### Çoklu tablo için gereken düzeltme (Dilim A'da gizli hata)

Dilim A'nın `index.js`'i `document.querySelector('.dt-filter-btn' | '.dt-save-filter-btn' | '.add-new')` kullanıyordu.
Sayfa artık 4 DataTable taşıdığı için bu global seçiciler ilk sekmenin butonlarını yakalardı; üçü de
`api.table().container()` içine scope'landı. `DtDefaults.updateVisualState` zaten container-scoped olduğundan
rozet sızıntısı oluşmuyor.

---

## 4. Kararlar ve gerekçeleri

1. **Varsayılan açık sekme = Nodes (Tab 2), sıra §7.1'e sadık (Types · Nodes · Connections · Templates).**
   Compact ConceptNode yüzeyi rota tabanlı Create/Edit/Details sayfalarından `/CRM/KnowledgeConcepts`'e döner;
   dönüşte Types sekmesine düşmek yanıltıcı olurdu. Sekme *sırası* pack'teki gibidir.
2. **Aggregate başına ayrı create/edit offcanvas** (§11 birebir), FU02 `Taxonomy` sayfasındaki tek paylaşılan
   canvas yerine. Üç formun şekli gerçekten farklı (Type 6 alan; Relationship 12, iki node picker'lı;
   Template 9, sıra editörlü) — paylaşılan canvas `d-none` yığınına dönerdi.
3. **Paylaşılan salt-okunur quick view** (`#offcanvasDetailsPreview`), aggregate başına alan haritası partial'ı ile.
   Golden Slim preview kabuğu tek; içerik `data-preview-kind` ile açılır.
4. **`api/subjects` proxy satırı eklendi.** Üç Slim tab da subject-scoped ve subject listesi tarayıcıya
   same-origin proxy'den ulaşmak zorunda. Yeni backend endpoint değil — mevcut FU02 `GET /api/crm/knowledge/subjects`
   okumasının allowlist'e alınması. **Yan fayda:** Dilim A'nın Subject filtre çipi bu route olmadığı için sessizce
   boş geliyordu; artık doluyor.
5. **`concept-slim.js` ayrı dosya.** `index.js` zaten 24 KB; üç tabloyu içine koymak ~60 KB'a çıkarırdı.
   Repo emsali aynı yönde (`Knowledge/taxonomy.js`, `ConsentPreferences/*`).
6. **Effective window tarihleri** `<input type="date">` ile alınır, `YYYY-MM-DDT00:00:00Z` olarak gönderilir.
   İki `DateTimeOffset` alanı UI'da index'lenmez/sort edilmez — parallel-array tuzağına girilmez.

---

## 5. Kalite kapısı

### Build

```
dotnet build frontend/Diten.Web/Diten.Web.csproj -t:CoreCompile
→ Oluşturma başarılı oldu. 0 Hata / 14 Uyarı
```

14 uyarının tamamı bu dilim dışındaki dosyalarda (WorkCenter/DevScenarios, TerritoryManagementController,
EnterpriseStrategy partial'ları) ve öncesinde de mevcut.

### JS

```
node --check concept-slim.js   → OK
node --check index.js          → OK
```

### RESX

7 dosyanın 7'si de `xml.etree` ile parse edildi → well-formed. Dosya başına +51 anahtar.
`en` + `tr` gerçek metin; `ar` / `es` / `fr` / `ru` / `zh` Dilim A konvansiyonuyla İngilizce placeholder
(follow-up **F-L10N**). `tr` metinleri Dilim A gibi ASCII-katlanmış yazıldı (dosyanın mevcut konvansiyonu).

### DataTable verifier

```
py .antigravity/scripts/verify_datatable_page.py . --area CRM --module KnowledgeConcepts --reference compact --api-profile proxy
py .antigravity/scripts/verify_datatable_page.py . --area CRM --module KnowledgeConcepts --reference slim    --api-profile proxy
```

> Verifier **modül klasörü** bazlıdır; üç Slim yüzey de `Views/CRM/KnowledgeConcepts/` altında olduğundan
> "×3 (Type/Relationship/Template)" tek koşuya karşılık gelir — aynı komutu üç kez çalıştırmak birebir aynı
> çıktıyı verir.

| Koşu | Passed | Failed |
|---|---|---|
| `--reference compact` (ConceptNode primary) | **87** | **8** |
| `--reference slim` (3 Slim yüzey) | **80** | **10** |

#### FAIL seti — baseline karşılaştırması

**Compact koşusu: FU02 baseline ile BİREBİR AYNI 8 FAIL. Dilim B regresyon üretmedi.**

| # | FAIL | Sınıflandırma |
|---|---|---|
| 1 | `personalizationClient sends tenant header only for tenant users` | **Belgeli N/A** — paylaşılan `personalization-client.js` dosyasına ait, modüle ait değil; FU02 `CRM/Knowledge` koşusunda da FAIL |
| 2 | `_DataTable.cshtml has select-all checkbox header` | **Belgeli N/A** — archive-only modül |
| 3 | `index.js declares bulk action config` | **Belgeli N/A** — archive-only |
| 4 | `index.js wires bulk selection` | **Belgeli N/A** — archive-only |
| 5 | `index.js calls bulk endpoint (.../bulk)` | **Belgeli N/A** — archive-only; **DELETE endpoint'i yok** |
| 6 | `index.js wires bulk delete trigger` | **Belgeli N/A** — archive-only |
| 7 | `index.js uses shared reload-with-toast lifecycle` | **Belgeli N/A** — bulk-delete lifecycle'ının parçası |
| 8 | `index.js wires clear-selection` | **Belgeli N/A** — archive-only |

Doğrulama: `--area CRM --module Knowledge --reference compact` (FU02, SHIPPED) → **87 passed / 8 failed**,
FAIL isimleri satır satır aynı. Dilim A öncesi ölçüm de 87/8 idi.

**Slim koşusu: yukarıdaki 8 + 2 yapısal ek.**

| # | FAIL | Sınıflandırma |
|---|---|---|
| 9 | `Slim reference has _CreateEditOffcanvas.cshtml` | **Yapısal N/A** (aşağıda) |
| 10 | `Slim _CreateEditOffcanvas.cshtml has #offcanvasCreateEdit` | **Yapısal N/A** (9'un devamı) |

**Neden N/A ve neden kapatılmadı.** Verifier, modül klasörü başına *tek* bir `_CreateEditOffcanvas.cshtml`
varsayar. Pack §11 ise bu hibrit konsolda **aggregate başına ayrı** create/edit offcanvas ister
(`_TypeCreateEditOffcanvas` / `_RelationshipCreateEditOffcanvas` / `_TemplateCreateEditOffcanvas`) — üç formun
şekli farklı olduğu için bu doğru tasarımdır. §7.1 bu çatışmayı zaten öngörür: *"Verifier her yüzeyi kendi
referansıyla doğrular — tek bir global `--reference` yeterli değildir."*

Bu ikisini kapatmanın tek yolu, yalnızca dosya-adı kontrolünü tatmin etmek için bir `_CreateEditOffcanvas.cshtml`
üretmekti. Bu dosya Index'ten include edilseydi **compact koşusu 9. bir FAIL alacaktı**
(`Compact Index does not include create/edit offcanvas`) — yani Dilim A pariteliği bozulurdu; include edilmeseydi
ölü kod olurdu. İkisi de tasarımı kötüleştirdiği için üretilmedi; bunun yerine pack §18'e **F-VERIFY**
follow-up'ı eklendi (script'in hibrit yüzey desteği).

> Not: Slim koşusunun üçüncü yapısal FAIL'i olan `Index.cshtml has #offcanvasDetailsPreview` **kapatıldı** —
> paylaşılan salt-okunur quick view kabuğu gerçekten Index'te ve gerçekten kullanılıyor.

---

## 6. Sınır uyumu (aşılmadı)

- **FU01C sınırı:** motor / traversal / recommendation / scoring / best-next-content **yok**. `concept-slim.js`
  yalnızca CRUD-minus-delete + archive + filtre + Save View yapar; `/concept-graph` uçlarını hiç çağırmaz.
- **Graph Preview** ve **FU02 `_Form.cshtml` AC-UI-3** dokunuşu Dilim C'de — bu dilimde dosyalarına dokunulmadı.
- **MDM read-only:** bu dilim MDM'i hiç çağırmaz (Global Product picker Dilim A'nın Compact node formunda kalır).
- **FU02 sözleşmesi:** `KnowledgeContent`, `IKnowledgeContentLinkageReader`, FU02 contract flag'leri, Subject/Topic/
  AudienceProfile write yüzeyleri — hiçbirine dokunulmadı. Eklenen tek FU02 teması `subjects` **list okuması**.
- **Protected paths:** `ocelot.json` · `services/Diten.MdmService/**` · MOD-0165/0164/0155 · RBAC seed/role template ·
  MOD-0048 publish · Mongo hand-edit · `execution/registries/**` · FU01A/FU01B pack dosyaları — **hiçbiri değişmedi**.
  Pack'te yalnızca §18 follow-up tablosuna iki satır eklendi.
- **RBAC:** seed/grant yok. Proxy, Dilim A'nın belgelenmiş DEV-ONLY fallback'ini
  (`crm.territory.read` / `crm.territory.model.manage`) aynen kullanır; guard gevşetilmedi.

---

## 7. Ertelenenler / açık maddeler

| # | Madde | Neden |
|---|---|---|
| 1 | **Authenticated UI smoke** (tarayıcı) | Fleet ayakta (Gateway `:5000` → `/api/crm/knowledge/concept-graph/contract` **401** = route + FU03 runtime canlı; Web `:5001` → **302** login) ancak Web servisi bu dilimden önceki build'i koşuyor ve `.resx` değişiklikleri tam restart ister. Operatör login'i gerektiği için asistan tarafından yapılamadı. |

> **Kapsam notu:** Bu UI'ın sürdüğü API sözleşmesi Artım 1'de zaten authenticated olarak kanıtlandı —
> `scripts/smoke-mod0162-fu03-concept-graph-authenticated.ps1` üç Slim aggregate'i de kapsıyor
> (type duplicate 409, self-loop, cycle, chain template create, `tenantId` payload injection'ın yok sayılması,
> PATCH'in yokluğu, archive). Açık kalan yalnızca **tarayıcı katmanı**: sekme render'ı, offcanvas akışları,
> filtre/Save View ve rozetler.
| 2 | 5 dil gerçek çevirisi (`ar` / `es` / `fr` / `ru` / `zh`) | Follow-up **F-L10N** |
| 3 | Verifier hibrit slim desteği | Follow-up **F-VERIFY** |
| 4 | Graph Preview (Tab 5) + FU02 `_Form` AC-UI-3 | **Dilim C** |
| 5 | `crm.knowledge.concept.*` katalog + grant | Follow-up **F-RBAC** (AC-SEQ-3: en sonda) |

### Smoke için sıradaki adım

```powershell
# 1) Fleet'i yeniden başlat (RESX değişikliği tam restart ister)
# 2) Tarayıcıda /CRM/KnowledgeConcepts aç, operatör olarak login ol
# 3) Her Slim tab için: create → edit → quick view → archive; filtre uygula/reset; Save View
# 4) Tab 3'te bilinçli olarak template'e uymayan bir kenar kur → kayıt KABUL edilmeli ve
#    "Non-conforming" rozeti görünmeli (V16). Aynı (From,To,Type) ikinci kez → 409 toast.
# 5) Tab 4'te tek adımlı sıra → inline "en az iki" uyarısı; aynı tipi iki kez ekleme → picker'da sunulmaz.
```
