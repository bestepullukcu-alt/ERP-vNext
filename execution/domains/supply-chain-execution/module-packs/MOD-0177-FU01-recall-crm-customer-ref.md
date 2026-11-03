---
id: MOD-0177-FU01
name: Recall customer identity from CRM
parent: MOD-0177
parent_name: Recall Readiness
domain: supply-chain-execution
service: Diten.InventoryService (5067, Recall in-process) + frontend/Diten.Web
port: 5067
shell: tenant
golden_reference: compact
data_mode: server
screens:
  - module: RecallCaseDetails
    data_mode: server
  - module: RecallTrace
    data_mode: server
  - module: RecallEvidence
    data_mode: server
entity_base: EntityBase
status: draft
approval_status: pending
risk: HIGH
risk_reason: "GxP geri çağırma kaydı: müşteri anahtarı bildirim, tüketim beyanı, iade bağlama ve etkinlik hesabının birleştirme anahtarıdır — yanlış anahtar kaydı yetim bırakır ve etkinliği yanlış gösterir; servisler arası okuma (CRM 5061) ile kiracı izolasyonu ve kişisel veri sınırı; kapsam mührü (rs-scope-v1) customerRef'i içerir; sözleşme değişikliği (RECALL 1.10.0) ve dondurulmamış iki üretici sözleşmesine bağımlılık (SHIPMENT-BUNDLE 3.5.0, CRM-ACCOUNT-LOOKUP 1.0.0)"
status_note: "draft r2 (2026-11-03) — r1'e göre: sahip kararları Q-5 = HAYIR (eski anahtar CRM kimliğine hiçbir vakada yükseltilmez — §8.5, AC-6) ve Q-6 = HAYIR (elle eklenen kapsam satırında CRM müşterisi seçilemez — §8.2, AC-20) işlendi; CT kararları CT-1 (CustomerRefSource rs-scope-v1'e girmez) ve CT-2 (RECALL 1.10.0) verildi (§19.6); keşif bulgularının mükerrer sayılmaması CT'ce kabul edildi (§19.1); registry satırı CT'ce eklendi (8ba30d12b). Kimlik ön kontrolü 8ba30d12b'de yeniden çıkış 0. Kod yazılmaz: CT-APR-02 bağımsız inceleme + CT-APR-01 onay satırı gerekir; ready-for-dev kararı CT'nin. Açık: Q-1…Q-4 (§19.4), D-1…D-5 (§7); DL-1 kabul kapısı D-1, D-2 ve AC-20 (f) veri sayımı (§22). 24 AC (DL-1 AC-1…AC-20, DL-2 AC-21…AC-24)."
owner: supply-chain-execution / mvp-1 control-tower
branch: feature/sce/mod-0173-inventory-ledger-valuation
started: 2026-11-03
target: TBD
form_field_count: 0
dependencies: [MOD-0177, RECALL-v1.9.0 (→ 1.10.0, bu paket), SHIPMENT-BUNDLE-v3.5.0 (MVP-6, customerRef — kabul kapısı; bugün 3.4.0), CRM-ACCOUNT-LOOKUP-v1.0.0 (MOD-0149, U-1 — taslak; uç kodda yok, MOD-0149 DL-1), MOD-0018 (Auth — crm.account.lookup-validation rol eşlemesi, Q-1'e bağlı)]
---

# MOD-0177-FU01 — Recall customer identity from CRM

> **Kimlik (DCP-002) — ölçüldü 2026-11-03, worktree `wms-ct-01`, dal `chore/ct/mod0178-dl1-accept-01` @ `8b5205226`:**
> `python3 -P -X utf8 .antigravity/scripts/verify_module_id.py . --check-id MOD-0177-FU01 --name "Recall customer identity from CRM" --parent MOD-0177`
> → çıkış **0** ("OK  MOD-0177-FU01: proven against Blueprint/registry"). Mükerrer keşfi `--discover "recall customer crm"` → çıkış **3**
> ("inceleme gerekli"; rapor özeti `d6262ba4…2bd2b15`): blueprint 0 · sor 0 · registry 0 · paket 0 · görev 0 · **gateway 2**. İki bulgunun
> kararı ve gerekçesi §19.1'de (üretici-tüketici ilişkisi ve ebeveynin kendi rotası; mükerrer değil — **CT kabul etti, 2026-11-03**).
> **Registry satırı CT tarafından eklendi** (`execution/registries/module-id-registry.md`, commit `8ba30d12b`, durum `draft`). Ön kontrol
> registry satırıyla `8ba30d12b`'de yeniden koşuldu → çıkış **0** (araç sha256 aynı, `ae87a566…b51a`).
>
> **Yetki:** bu paket > `execution/domains/supply-chain-execution/domain-config.md` > `AGENTS.md` > `.antigravity/`. Ebeveyn
> `MOD-0177-recall-readiness.md` §8.6 "Customer identity" (`:611-613`) ve ASSUMPTION-0177-07 (`:1183`) bu paketin **tabanıdır**; bu paket
> FU-0177-CUST-01'i (`:1588`, "Customer party id / names from MVP-6") **CRM kimliğiyle** karşılar ve tabanı genişletir: anahtar sırasının başına
> `customerRef` eklenir, sonraki iki adım (shipToReference, `DOC:`) **aynen** kalır. Görevde "§8.4 müşteri kimliği, satır ~611" diye anılan yer
> ebeveynde **§8.6**'dır (§8.4 KR-B otomatik karantina isteğidir) — satır numarası doğru, bölüm numarası düzeltildi.
>
> **Ölçüm tabanı:** `services/Diten.InventoryService`, `frontend/Diten.Web`, `docs/analysis/contracts/recall.openapi.yaml`,
> `shipment-bundle.openapi.yaml` ve CRM taslağı `8b5205226`'da okundu; bu yollar uygulama dalı `feature/sce/mod-0173-inventory-ledger-valuation`
> @ `372082aa0` ile **aynıdır** (`git diff --stat 372082aa0 8b5205226 -- <yollar>` boş). Satır numaraları bu tabandandır. r2'de
> `git diff --stat 8b5205226 8ba30d12b -- services/Diten.InventoryService frontend/Diten.Web docs/analysis/contracts` boş; r2'nin eklediği
> ölçümler (`CaseCommandHandlers.cs`, `R3Handlers.cs:463-560`, `RecallRules.cs:171-197`, `case-detail.js:165`) `8ba30d12b`'dendir.

## 1. Module Summary

Recall'da müşteri bugün opak bir metinle tanınır: sevkiyat okunduysa MVP-6'nın `shipToReference` değeri, okunamadıysa ya da belge MOD-0183
dışındaysa `"DOC:" + sourceDocumentId` (`RecallTraceEngine.cs:331`, `:375`; `RecallTraceService.cs:93`). Ekranda bu metin ya da belge numarası
görünür; ad yoktur (ebeveyn §8.6: "No customer master link in MVP-1").

Bu FU üç şey yapar:

1. **Anahtar (DL-1):** SHIPMENT-BUNDLE 3.5.0'da MVP-6'nın ekleyeceği `customerRef` (MOD-0149 `Account.Id`) doluysa müşteri anahtarı odur;
   boşsa bugünkü sıra korunur. CRM anahtarı kanonik küçük harf GUID ("D") olarak saklanır; giriş büyük/küçük harf duyarsız eşleşir.
2. **Ad (DL-1):** CRM anahtarlı müşterinin adı ve kodu okuma anında CRM-ACCOUNT-LOOKUP 1.0.0 U-1'den çözülür (önbelleksiz, okuma başına
   2 sn). Çözülemezse "Çözülemedi", hesap yoksa / silinmişse / başka kiracıdaysa "Bulunamadı". **GUID hiçbir zaman gösterilmez** — kayıtta
   ve API'nin anahtar alanında kalır.
3. **Ekran (DL-2):** müşteri görünen her yerde ad + kod; eski anahtarlı satırlar "Eski kayıt" rozetiyle ayrı satır (SB-19).

Bildirim, tüketim beyanı ve iade bağlama yeni anahtarı kabul eder; eski kayıtlar değişmez (yeniden yazma, göç, otomatik bağlama yok).
Eski anahtar hiçbir vakada CRM kimliğine yükseltilmez — vakada hiç müşteri kaydı olmasa da (Q-5 = HAYIR). Elle eklenen kapsam satırında CRM
müşterisi seçilemez; GUID biçimli serbest metin reddedilir (Q-6 = HAYIR). Yeni yazma komutu, yeni izin anahtarı, yeni Gateway rotası yoktur.

## 2. Ownership and Boundaries

| Konu | Sahip | Bu paketin payı |
|---|---|---|
| Recall müşteri anahtarı, kayıtlar, ekran, RECALL sözleşmesi | MOD-0177 (bu paket) | yazar (RECALL 1.10.0 tek yazıcı adımı bu paketin) |
| SHIPMENT-BUNDLE ve `customerRef` alanı | MVP-6 (MOD-0183) | yalnız okur; sözleşmeye yazmaz |
| CRM hesabı ve CRM-ACCOUNT-LOOKUP sözleşmesi | MOD-0149 (commercial-suite, CrmService 5061) | yalnız tüketici tarafını tanımlar; sözleşmeye yazmaz |
| `crm.account.lookup-validation` kataloğu ve rol eşlemesi | Auth (MOD-0018, Ali) | bağımlılık (XMC-001); dokunmaz |
| Gateway rotaları | integration-agent | değişiklik yok (§15) |

**Sınır kuralları**
- InventoryService CRM veritabanını okumaz; yalnız Gateway üzerinden U-1'i çağırır.
- MVP-6 veya CRM sözleşme dosyalarına yazılmaz (`shipment-bundle.openapi.yaml`, `drafts/crm-account-lookup-validation-v1.0-proposal.md`).
  CRM sözleşmesinin tüketici satırına (`x-consumers`) MOD-0177'nin eklenmesi **CRM sahibinin** adımıdır; önerilen satır §7'dedir.
- Ad ve kod saklanmaz, önbelleğe alınmaz, mühre girmez; yalnız yanıt anında çözülür.

## 3. Owned Objects

Yeni koleksiyon yok. Değişen nesneler:

| Nesne | Koleksiyon | Değişiklik |
|---|---|---|
| `RecallScopeItem` | `recall_scope_items` | eklenen alan `CustomerRefSource` (§4.1) |
| `TraceNode` (bellek içi) / `TraceShipmentInfo` | — | eklenen alanlar (§4.2) |
| `RecallCustomerNotification`, `RecallConsumptionDeclaration`, `RecallReturnLink` | mevcut | **alan eklenmez**; `CustomerRef` CRM anahtarında kanonik GUID taşır |
| RECALL yanıt şemaları | `recall.openapi.yaml` | eklemeli `customer` nesnesi (§8.7) |

## 4. Entity Fields

### 4.1 RecallScopeItem eklenen alan

| Alan | Tip | Kural |
|---|---|---|
| `CustomerRefSource` | `RecallCustomerRefSource?` (`Crm`, `ShipTo`, `Document`, `Manual`) | yalnız `Kind = Shipment`'ta dolu; FU01'den önce yazılmış satırda `null` (= kaynak bilinmiyor, eski kayıt). Yazıcılar: iz (`RecallTraceService.ScopeItems`, §8.2), elle ekleme (`Manual`, §8.2), yeniden izleme (§8.5), revizyon kopyası (aynen, §8.5). Enum değerleri yalnız sona eklenir. **rs-scope-v1 mührüne girmez** (CT-1 kararı, 2026-11-03; §8.5). |

### 4.2 Trace modeli eklenen alanlar

- `TraceShipmentInfo` (`TraceModel.cs:55`) sona eklenen `Guid? CustomerAccountId` — SHIPMENT 200 yanıtındaki `customerRef`'in ayrıştırılmış
  hâli; geçersiz, `Guid.Empty` ya da yoksa `null`.
- `TraceNode` eklenen `RecallCustomerRefSource? CustomerRefSource` — `CustomerRef` (`TraceModel.cs:97`) ile birlikte yazılır.
- rt-v1 izi mührü değişmez: müşteri referansı bugün de mühürde değildir (`TraceRules.cs:108-111`).

## 5. Repo Scope

**DL-1 (arka uç + metinler):**
- `services/Diten.InventoryService/src/Diten.InventoryService.Domain/Recall/Trace/TraceModel.cs`
- `services/Diten.InventoryService/src/Diten.InventoryService.Domain/Recall/Entities/RecallEntities.cs` (`RecallScopeItem`, enum)
- `services/Diten.InventoryService/src/Diten.InventoryService.Domain/Recall/Rules/RecallCustomerKeyRules.cs` (yeni — kanonik biçim, eşleşme, sıra)
- `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/Recall/Ports/IRecallCustomerDirectory.cs` (yeni port)
- `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/Recall/Trace/RecallTraceEngine.cs`, `RecallTraceService.cs`
- `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/Recall/Handlers/CaseDetailReadHandlers.cs`, `RecordHandlers.cs`,
  `R3Handlers.cs` (yalnız `LinkRecallReturnHandler` ve `ReviseRecallScopeHandler` kopyası `:527-533`),
  `CaseCommandHandlers.cs` (yalnız `AddRecallScopeItemsHandler` Shipment dalı `:361-382` ve satır kurulumu `:403-427` — Q-6)
- `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/Recall/Services/RecallReads.cs`,
  `RecallCustomerDisplay.cs` (yeni — çözüm + gösterim eşlemesi)
- `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/Recall/RecallModels.cs`, `RecallContract.cs`
- `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/Recall/Evidence/RecallEvidence.cs`,
  `RecallEvidenceText_{en,tr,fr,es,zh,ar,ru}.resx`
- `services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/Recall/ShipmentBundleHttpReader.cs`,
  `CrmAccountLookupHttpReader.cs` (yeni)
- `services/Diten.InventoryService/src/Diten.InventoryService.Api/Recall/RecallServiceCollectionExtensions.cs` (DI)
- `docs/analysis/contracts/recall.openapi.yaml` (1.9.0 → 1.10.0)
- `frontend/Diten.Web/Resources/Views/Recall/Recall.{en,tr,fr,es,zh,ar,ru}.resx`, `frontend/Diten.Web/Views/Recall/Recall.cs` (anahtar listesi) —
  ortak anahtarlar parite bekçisi gereği DL-1'de (§11)
- Testler: `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Recall/` altında yeni sınıflar (§16 adları) ve
  `ShipmentReaderTests.cs` eki; CRM test ikizi `RecallTestInfrastructure.cs` yanında

**DL-2 (ekran):**
- `frontend/Diten.Web/wwwroot/assets/js/Recall/case-detail.js`, `trace-view.js`, `recall.l10n.js`
- `frontend/Diten.Web.Tests/JavaScript/Node/recall-fu01.test.mjs` (yeni), `frontend/Diten.Web.Tests/Recall/` (gerekirse)

## 6. Protected Paths

`.antigravity/**`, `gateway/Diten.ApiGateway/ocelot.json` (yalnız integration-agent), `_Layout.cshtml`, Archive;
`services/Diten.CrmService/**` (commercial-suite), `services/Diten.SupplyChainService/**` (MVP-6), Auth servisi;
`docs/analysis/contracts/shipment-bundle.openapi.yaml` (MVP-6 tek yazıcı), `docs/analysis/contracts/drafts/crm-account-lookup-validation-v1.0-proposal.md`
(CRM tek yazıcı). MOD-0173 / 0174 / 0175 / 0176 dosyalarına dokunulmaz.

## 7. Dependencies

| # | Bağımlılık | Durum (2026-11-03) | Bu pakete etkisi |
|---|---|---|---|
| D-1 | **SHIPMENT-BUNDLE 3.5.0** — `getShipment` 200 yanıtında isteğe bağlı `customerRef` (MOD-0149 `Account.Id`, GUID "D", küçük harf; yalnız ekleme) | yok; bugün **3.4.0** FROZEN (`shipment-bundle.openapi.yaml:15`), `customerRef` / `partyId` geçmiyor. Alan adı ve tipi **kesin değil — MVP-6 taslağıyla teyit edilecek** (Q-2) | **DL-1 kabul kapısı:** 3.5.0 FROZEN ve alan teyitli. Okuyucu alan adını tek sabitte tutar (§8.2); ad değişirse tek satır |
| D-2 | **CRM-ACCOUNT-LOOKUP 1.0.0 U-1** `GET /api/crm/accounts/{accountId:guid}/lookup-validation` | taslak (`drafts/crm-account-lookup-validation-v1.0-proposal.md`); **uç kodda yok** (MOD-0149 DL-1) | DL-1 E4 sözleşme metninden kurulan ikizle; **DL-1 kabul kapısı:** sözleşme FROZEN ya da U-1 şekli (alanlar, 404 kuralı, `contractVersion`) CRM sahibince teyitli (teyit edilen taslağın sha256'sı kabul kaydına). Uçtan uca E5: MOD-0149 DL-1 yayında |
| D-3 | Çağrı yolu: kullanıcı token'ı Gateway üzerinden mi, servis kimliği mi | **CRM'e soruldu — açık** (Q-1) | DL-1 kodu kapı arkasında: varsayılan kullanıcı token'ı (`ForwardCallerHeadersHandler`, mevcut okuyucu deseni); karar servis kimliği olursa yalnız DI işleyicisi değişir, AC-11 aynen geçer |
| D-4 | `crm.account.lookup-validation` izninin geri çağırma rollerine atanması | Auth işi (MOD-0018, **Ali**) — XMC-001; yalnız Q-1 = kullanıcı token'ıysa gerekir | atanmamış kullanıcıda CRM 403 → "Çözülemedi" (AC-18); recall ekranı çalışmaya devam eder |
| D-5 | CRM sözleşmesinin tüketici satırı | CRM sahibinin adımı | önerilen satır: `x-consumers: … MOD-0177 Recall Readiness (U-1 — müşteri adı / kodu gösterimi; uygunluk kararı vermez, isBillable kullanmaz)` |
| D-6 | MOD-0177 ebeveyn (RECALL 1.9.0 FROZEN, `recall.openapi.yaml:14`) | uygulandı | 1.10.0 bu paketin tek yazıcı adımı (CT-2 kararı, 2026-11-03); `8ba30d12b`'de `recall.openapi.yaml` hâlâ `1.9.0` ve "RECALL 1.10.0" yalnız bu pakette geçiyor (`execution/`, `docs/analysis/` taraması) |

Bağımlılık sırası: D-1 ve D-2 olmadan DL-1 **kodlanabilir** (ikizlerle), ama **kabul edilemez**. DL-2 DL-1'e dayanır.

## 8. Runtime Constraints

### 8.1 Servis ve kapsam

Tüm iş InventoryService 5067'de, Recall özelliğinin içinde. Kiracı ve şirket sınırı mevcut kuralla: vaka kiracı + şirkete bağlı; başka
kiracının vakası 404 (`RecallTenantBoundaryTests`). CRM hesabı kiracı geneldir (şirket alanı yok — CRM taslağı "Durum" ölçümü); kiracı CRM'de
token'dan çözülür.

### 8.2 Müşteri anahtarı sırası

Sevkiyat düğümünün anahtarı **ilk tutan** kurala göre (yalnız `RecallTraceEngine.ShipmentsAsync` (`:365-377`) ve `RecallTraceService.ScopeItems`
(`:93`) belirler):

| Sıra | Koşul | `CustomerRef` | `CustomerRefSource` |
|---|---|---|---|
| 1 | SHIPMENT okuması `Ok` ve `CustomerAccountId` dolu | `CustomerAccountId.ToString("D")` (küçük harf) | `Crm` |
| 2 | okuma `Ok` ve `shipToReference` dolu (`:375`, bugünkü) | `shipToReference` aynen | `ShipTo` |
| 3 | belge var (`:331`, `RecallTraceService.cs:93`, bugünkü) | `"DOC:" + sourceDocumentId` | `Document` |
| — | belge yok | `null` + `NO_CUSTOMER_REFERENCE` boşluğu (bugünkü, `:334-338`) | `null` |

**Okuyucu (`ShipmentBundleHttpReader.cs:66-70`):** `customerRef` alanı (adı tek sabit: `ShipmentBundleHttpReader.CustomerRefField`, Q-2)
string ise `Guid.TryParseExact(value, "D", …)` ile ayrıştırılır (ayrıştırma büyük/küçük harf duyarsızdır); başarısız, `Guid.Empty`, string
olmayan ya da yok → `null` ve sıra 2'ye düşülür. Geçersiz değer günlüğe **değeriyle değil**, `recall.shipment.customer_ref_invalid
{ShipmentId}` olarak yazılır. 401 / 403 / 404 / zaman aşımı yolları bugünkü gibidir (§8.6 ebeveyn tablosu); hiçbirinde `customerRef` okunmaz.

**Elle eklenen Shipment kapsam satırı** (`addScopeItems`, `AddRecallScopeItemsHandler`, `CaseCommandHandlers.cs:361-382`) — **Q-6 = HAYIR
(sahip, Beste, 2026-11-03): CRM müşterisi seçilemez.** Kurallar:
- `customerRef` bugünkü gibi serbest metindir; satır `CustomerRefSource = Manual` ile yazılır (`:403-427` kurulumuna eklenir).
- Kırpılmış `customerRef` `Guid.TryParseExact(…, "D")` ile ayrışıyorsa (büyük ya da küçük harf) **istek reddedilir**: 400 `INVALID_REQUEST`
  (`RecallSupport.Invalid`, kod `InventoryErrorCodes.InvalidRequest`; `addScopeItems` `x-error-codes`'unda zaten var). İstek bütündür
  (doğrulama döngüsü `SaveAsync`'ten önce biter, `:432-440`): toplu istekteki tek GUID satırı tüm isteği reddeder — kapsam satırı, vaka olayı
  ve denetim satırı yazılmaz. **Bu bugünkü davranıştan değişikliktir** (bugün her 1–200 karakterlik metin kabul edilir); RECALL 1.10.0
  değişiklik notuna ve `addScopeItems` açıklamasına yazılır (§8.7). Gerekçe: GUID biçimli serbest metin §8.3 kanonik eşleşmesiyle bir `Crm`
  satırının anahtarıyla aynı olur ve okuma gruplamasında (`CaseDetailReadHandlers.cs:75`) CRM müşterisiyle birleşir — bu, elle CRM müşterisi
  seçmenin dolaylı yoludur ve kapanır.
- Yasak yalnız `"D"` biçimine bağlıdır (§8.3 eşleşmesinin tanıdığı tek biçim): `{…}` (B), tiresiz (N) ya da başka metinler serbest metin
  olarak kabul edilir; hiçbiri bir `Crm` anahtarıyla ordinal eşit olamaz.
- Ekranda elle kapsam ekleme formu **yoktur** (ölçüldü: `wwwroot/assets/js/Recall/*.js` içinde `addScopeItems` çağrısı yok); yasak yalnız
  API'dedir, yeni metin anahtarı gerekmez (hata iletisi mevcut `RecallSupport.Invalid` iletileri gibi geliştirici metnidir).
- Yeniden izleme elle satıra dokunmaz (`RecallTraceService.cs:127`, "a hand entry with the same key wins") — aynı belge MVP-6'da `customerRef`
  taşısa da elle satır `Manual` kalır.

Uzunluk sınırı `RecallContract.MaxCustomerRefLength = 200` (`RecallContract.cs:20`) değişmez; GUID 36 karakterdir.

### 8.3 Büyük/küçük harf ve anahtar biçimi

- **CRM anahtarı** her yerde kanonik biçimde saklanır: `Guid.ToString("D")` (küçük harf, 36 karakter). İki sevkiyat aynı hesabı farklı harfle
  dönerse tek müşteri satırı oluşur.
- **Komut girişi** (bildirim, tüketim beyanı, iade bağlama): `customerRef` kırpılır; `Guid.TryParseExact(…, "D")` başarılıysa ve kapsamda
  aynı GUID'li `Crm` kaynaklı satır varsa **kanonik değer** kullanılır (eşleşme, kayıt ve tekrar parmak izi kanonik değerle). Böylece
  `3C9A…` ve `3c9a…` aynı müşteridir, aynı idempotency anahtarıyla gelen ikisi aynı istektir.
- **Eski anahtarlar** (`ShipTo`, `Document`, `Manual`, `null`) bugünkü gibi sıradan (ordinal, harfe duyarlı) eşleşir; parmak izi değişmez.
  Bu, eski kayıtların ve onların tekrar vektörlerinin bayt eşit kalmasının koşuludur (AC-5). Gerekçe: SB-19 eski satırların yeni kimliğe
  otomatik bağlanmasını yasakladığı gibi eski anahtarların eşleşme anlamını da değiştirmeyiz.
- Okuma tarafındaki gruplama (`CaseDetailReadHandlers.cs:75`, `RecallEvidence.cs:283`, `RecallReads.cs:80`, `:137`, etkinlik kuralı
  `RecallRules.cs:342-343`) ordinal kalır: CRM anahtarları kanonik saklandığı için ordinal eşitlik harf duyarsız eşitliğe denktir.
- **GUID biçimli eski anahtar:** FU01'den sonra elle girilen anahtar GUID biçimli olamaz (Q-6, §8.2); `DOC:` anahtarı GUID değildir;
  MVP-6'nın `shipToReference`'ı bugün GUID biçimli değildir (`WarehouseShipmentMapper.cs:51` `warehouse-outbound:{…}:ship-to`, salt okuma).
  Kalan risk: FU01 öncesi elle girilmiş (ya da MVP-6'dan gelmiş) `"D"` biçimli bir eski anahtar aynı vakadaki bir `Crm` satırıyla çakışabilir
  (gruplamada birleşir; §8.3 eşleşmesi onu CRM müşterisine bağlar — SB-19'a aykırı). Büyüklüğü salt-okuma sayımla ölçülür (AC-20 (f)): kaynağı
  `Crm` olmayan Shipment kapsam satırlarında `"D"` biçimli anahtar sayısı; sıfır değilse DL-1 kabulü durur ve CT kararı alınır (gözlem O-2,
  §20). Göç yapılmaz.

### 8.4 Kayıtlar

| Komut | Bugün | FU01 |
|---|---|---|
| `recordNotification` (`RecordHandlers.cs:32-58`) | ordinal eşleşme `s.CustomerRef == customer` (`:58`) | §8.3 eşleşmesi; kayıt kanonik anahtarla |
| `recordConsumption` (`RecordHandlers.cs:125-170`) | `:155`, `:163`, `:169-170` ordinal | §8.3 eşleşmesi; aynı müşteri / SKU toplamları kanonik anahtarla |
| `linkReturn` (`R3Handlers.cs:381-418`) | `ToHashSet(StringComparer.Ordinal)` (`:417`) | §8.3 eşleşmesi; `customers` kümesi kaynağa göre kurulur |

Kapsamda olmayan anahtar bugünkü gibi 404 `RECALL_UNKNOWN_CUSTOMER` (`RecordHandlers.cs:103-105`), yazım 0. **Eski kayıtlar değişmez:**
göç yok, açılışta düzeltme yok, okuma yolunda yeniden yazma yok. Yeni kayıt alanı yok.

### 8.5 Yeniden izlemede anahtar kararlılığı (SB-19)

`submitScope` yeniden izler ve `RecallTraceService.Reconcile` saklı kapsam satırının anahtarını **koşulsuz** üzerine yazar
(`RecallTraceService.cs:141` `s.CustomerRef = t.CustomerRef`; tek çağıran `CaseCommandHandlers.cs:553`). `submitScope` yalnız `Draft`'ta
koşar (`RecallCaseRules.CanSubmit`, `RecallRules.cs:173`), müşteri kayıtları yalnız `Active` / `EffectivenessCheck`'te yazılır
(`AcceptsRecords` = `IsUnderRecall`, `RecallRules.cs:169`, `:197` — `RecordHandlers.cs:49`, `:147`; iade bağı `R3Handlers.cs:396`; geri dönüş
yalnız `PendingScopeApproval → Draft`, `:175`): yeniden izlenen vakada müşteri kaydı **hiç yoktur**.

**Q-5 = HAYIR (sahip, Beste, 2026-11-03):** hiç müşteri kaydı (bildirim / beyan / iade bağı) olmayan vakada da eski anahtar CRM kimliğine
**yükseltilmez**. Kural kayıt varlığına bakmaz; SB-19 tek kuraldır. FU01 kuralı ("eski" = `null`, `ShipTo`, `Document`, `Manual`; anahtar
kalırsa kaynak da kalır, anahtar güncellenirse kaynak da yeni izin kaynağını alır):

| Saklı satırın kaynağı | Yeni izin kaynağı | Sonuç |
|---|---|---|
| eski (`null` / `ShipTo` / `Document`; ya da silinmiş `Manual` satır izle geri geliyor, `:127-136`) | `Crm` | **saklı anahtar ve kaynak kalır** — eski satır yeni kimliğe otomatik bağlanmaz (SB-19), vakada kayıt olmasa da (Q-5) |
| `Crm` | `ShipTo` / `Document` (ör. o an MVP-6 erişilemez) | **saklı CRM anahtarı kalır** — geçici okuma hatası anahtarı düşürmez, kayıtlar yetim kalmaz |
| `Crm` | `Crm`, farklı GUID | saklı anahtar kalır; günlük satırı `recall.scope.customer_ref_changed {NodeKey}` (değer yok) — veri sorunu MVP-6'dadır |
| eski | eski | **bugünkü davranış** (anahtar güncellenir, kaynak yeni izin kaynağını alır) — bu paketin kapsamı dışında; gözlem O-1 (§20) |
| silinmemiş `Manual` | herhangi | dokunulmaz (bugünkü, `:127`) |

**Revizyon (`reviseScope`, `ReviseRecallScopeHandler`, `R3Handlers.cs:463-560`; `Active`, kayıtlar olabilir):** onaylı satırlar r+1'e
kopyalanır (`:527-533`); kopyaya `CustomerRefSource` **aynen** eklenir (bugün kopya `CustomerRef`'i taşır, yeni alanı taşımazsa `Crm` satırı
`null` = eski olur ve büyük harfli GUID'li kayıt 404 alır). Kopyalanan eski satır revizyonda da yükseltilmez (Q-5); yalnız delta düğümleri
(`:502-503`) §8.2 sırasıyla anahtar alır.

Yeni sevkiyat düğümleri (ekleme) §8.2 sırasıyla anahtar alır. Sonuç: aynı müşteri bir vakada hem eski anahtarlı (eski sevkiyat) hem CRM
anahtarlı (yeni sevkiyat) iki satır olarak görünebilir — **kabul edilen sınır** (SB-19), ekranda "Eski kayıt" rozetiyle ayrılır (§8.8).

**Mühür (CT-1 kararı, MVP-1 CT, 2026-11-03):** `CustomerRefSource` rs-scope-v1 kanonik biçimine **yazılmaz** (`RecallRules.cs:282-318`; biçim adı `:243` değişmez);
`customerRef` bugünkü gibi yazılır (`:305`). Gerekçe: FU01 öncesi mühürler bayt eşit yeniden hesaplanır; kaynak bilgisi anahtarın kendisinden
ve iz kaydından türetilen sunucu bilgisidir. Ad ve kod hiçbir mühre girmez.

### 8.6 Ad çözümü (CRM-ACCOUNT-LOOKUP U-1)

**Port:** `IRecallCustomerDirectory.ResolveAsync(IReadOnlyCollection<Guid> accountIds, CancellationToken)` →
`IReadOnlyDictionary<Guid, RecallCustomerName>`; `RecallCustomerName = (Outcome: Resolved | NotFound | Unresolved, Code?, Name?)`.

**Bağdaştırıcı `CrmAccountLookupHttpReader`** — `ShipmentBundleHttpReader` deseni (`:19-22`):
- `GET api/crm/accounts/{id:D}/lookup-validation`, Gateway taban adresiyle (`RecallServiceCollectionExtensions.cs:48-56`); Gateway rotası
  `/api/crm/accounts/{everything}` → 5061 mevcut (ocelot `:2979`).
- Kimlik: Q-1 kapanana dek çağıranın token'ı (`ForwardCallerHeadersHandler`); kiracı yalnız token'dan (CRM taslağı §1). Başlıkla kiracı gönderilmez.
- **Okuma başına 2 sn** (`PerReadTimeout`), **en çok 8 eşzamanlı** (`MaxParallel`), istek içinde tekilleştirme (aynı GUID bir kez),
  **önbellek yok** (mevcut okuyucu da önbelleksizdir; CRM taslağı §7 "yanıtlar güncel durumdur").

**Sınıflandırma** (CRM taslağı §2, §5, §7 ile hizalı; recall uygunluk kararı vermez, `isBillable` / `status` kullanılmaz — pasif müşteriye
de geri çağırma bildirimi gerekir):

| CRM yanıtı | Sonuç | Gösterim |
|---|---|---|
| 200; `data.accountId` istenen GUID (Guid eşitliği); `code` ve `name` boş değil; `contractVersion` `crm-account-lookup/1.` ile başlar | `Resolved` | ad + kod |
| 404 (yok / silinmiş / başka kiracı — CRM'de üçü aynı gövde) | `NotFound` | "Bulunamadı" |
| 200 ama yukarıdaki koşullardan biri tutmuyor; gövde ayrıştırılamıyor; 400; 401 / 403 (izin atanmamış dahil); 5xx; zaman aşımı; ulaşılamıyor | `Unresolved` | "Çözülemedi" |

Hiçbir sonuç recall isteğini başarısız kılmaz: müşteri satırları, etkinlik ve kanıt paketi her durumda döner.

**Günlük:** başarısızlıkta `recall.customer.lookup_unavailable {AccountId}: {Reason}` (Reason = durum kodu ya da istisna türü) — `Information`
düzeyi, mevcut `recall.shipment.read_unavailable` deseni (`ShipmentBundleHttpReader.cs:83`). Yanıt gövdesi, ad ve kod **hiçbir düzeyde**
günlüğe yazılmaz.

### 8.7 Sözleşme — RECALL 1.10.0

Yalnız ekleme (1.9.0 → 1.10.0; `info.version` ve değişiklik notu):

```yaml
CustomerDisplay:
  type: object
  required: [source, state]
  properties:
    source: { type: string, enum: [Crm, ShipTo, Document, Manual, Unknown] }   # Unknown = FU01'den önce yazılmış satır
    state:  { type: string, enum: [Resolved, Unresolved, NotFound, Legacy] }   # Legacy ⇔ source ≠ Crm
    name:   { type: string }   # yalnız state = Resolved
    code:   { type: string }   # yalnız state = Resolved
    label:  { type: string }   # yalnız state = Legacy — bugünkü etiket (shipTo / belge no / serbest metin)
```

İsteğe bağlı `customer: CustomerDisplay` eklenir: `CustomerRow` (`:1530`), `EffectivenessRow` (`:1198`, toplam satırında yok), `ReturnLink`
(`:1558`), `ScopeItem` (`:1308`, yalnız `kind = Shipment`), trace düğümü (`:1466`, yalnız `Shipment`). `customerRef` alanları ve tipleri
**değişmez** (anahtar; komutlar onu gönderir). `CustomerRow.label` CRM satırında **yoktur** (bugün `?? g.Key` ile anahtara düşebilir,
`CaseDetailReadHandlers.cs:80-83`). Trace düğümü şeması `getCaseScopeDiff` yanıtında da kullanılır (`ScopeDiff.added`, `:1499-1504`) — ayrı
alan gerekmez. Komut gövdeleri (`:467-470`, `:496-499`, `:548-552`) değişmez; açıklamalarına §8.3 harf kuralı eklenir.

**Tek davranış değişikliği (Q-6):** `addScopeItems` (`:196`) açıklamasına ve değişiklik notuna "a Shipment item whose trimmed customerRef
parses as a GUID in the 'D' form is refused with 400 INVALID_REQUEST (no CRM customer is chosen by hand; MOD-0177-FU01 Q-6)" eklenir;
`x-error-codes` değişmez (`INVALID_REQUEST` zaten listede). Şema (`ScopeItemInput`) değişmez.

### 8.8 Gösterim kuralı

- CRM anahtarlı müşteri (ekran, CSV, PDF): `Resolved` → "{ad} · {kod}"; `Unresolved` → "Çözülemedi"; `NotFound` → "Bulunamadı".
  **GUID metin, `title`, araç ipucu, CSV / PDF hücresi olarak hiçbir yerde görünmez**; yalnız komut gönderen öğenin değerinde (`data-*` /
  `option.value`) ve API'nin `customerRef` alanında taşınır.
- Eski anahtarlı satır (`state = Legacy`, kaynak `Crm` dışı): bugünkü etiket + "Eski kayıt" rozeti; CRM satırıyla **birleştirilmez**, ayrı
  satırdır. Araç ipucu kaynağa göre: `ShipTo` / `Document` / `Unknown` → "CRM müşteri kimliğinden önce kaydedildi; yeni kimliğe otomatik
  bağlanmaz." (`Recall.Customer.LegacyHint`); `Manual` → "Elle girildi; CRM müşteri kimliğine bağlanmaz." (`Recall.Customer.ManualHint`,
  Q-6 — FU01'den sonra elle girilen satır "önce kaydedildi" değildir). "GUID gösterilmez" kuralı `Crm` kaynaklı anahtarı bağlar; eski
  satırın etiketi saklı metnin kendisidir (kullanıcının ya da MVP-6'nın metni, bugünkü gibi) — AC-10 / AC-22 fixture'larında eski satır
  A metnini taşımaz.
- Görünen yerler (ölçüldü): vaka detayı kapsam sekmesi (`case-detail.js:135`), kapsam farkı listesi (`:165`, `getCaseScopeDiff`
  `added` düğümleri — bugün `n.customerRef`'i basar; r2'de eklendi), müşteriler sekmesi (`:248` — bugün anahtarı tek aralıklı alt
  satırda basar), iade bağları (`:259`), etkinlik tablosu (`:287`), bildirim / tüketim / iade bağlama pencereleri (`:363`, `:380`, `:405`),
  iz ekranı (`trace-view.js:32`), iz dışa aktarımı (`RecallTraceService.cs:213` "place"), kanıt paketi müşteri, iade ve iz bölümleri
  (`RecallEvidence.cs:283-295`, `:428-440`). Tatbikat çalıştırmasının `Customers` listesi (`DrillRunHandlers.cs:284`) gösterilmez
  (kanıt paketinde "notApplicableDrill", `RecallEvidence.cs:382`) — değişmez.
- Kişisel veri: tarayıcıya müşteri hakkında yalnız ad ve kod gider; adres, iletişim, durum, faturalanabilirlik gitmez.

### 8.9 Durum ve komut tablosu

Yeni durum ve yeni komut yok. Okuma uçları (`getCaseCustomers`, `getCaseEffectiveness`, `getCaseScope`, `getCaseScopeDiff`, `runTrace`,
`exportTrace`, kanıt paketi önizleme / dışa aktarma) `customer` nesnesini ekler; izinleri değişmez.

| Komut | Durum | FU01 etkisi |
|---|---|---|
| `addScopeItems` (Shipment) | `Draft` | `Manual` kaynağı yazılır; `"D"` GUID biçimli `customerRef` → 400 `INVALID_REQUEST`, yazım 0 (Q-6) |
| `submitScope` | `Draft` | §8.5 tablosu (Q-5: eski anahtar yükseltilmez) |
| `reviseScope` | `Active` | kopya `CustomerRefSource`'u taşır; eski satır yükseltilmez (§8.5) |
| `recordNotification`, `recordConsumption`, `linkReturn` | `Active` / `EffectivenessCheck` | §8.3 eşleşmesi (§8.4) |
| diğer komutlar | — | değişmez |

## 9. Layout & Shell Contract

Ebeveynin sayfaları (`_LayoutTenantShell`, Scope = Tenant). Yeni sayfa, yeni modal, yeni form alanı yok (`form_field_count: 0`). Rozet mevcut
`badge bg-label-*` sınıflarıyla.

## 10. Backend File Convention

Ebeveyn §10 düzeni: port `Application/Features/Recall/Ports/`, kural `Domain/Recall/Rules/`, HTTP bağdaştırıcı `Infrastructure/Recall/`, DI
`Api/Recall/RecallServiceCollectionExtensions.cs`. Yeni denetleyici yok.

## 11. Frontend File Contract

- DL-2 yalnız `case-detail.js` ve `trace-view.js` gösterimini değiştirir; `case-view.js` `customerRows` anahtarla eşleşmeye devam eder.
- Metin anahtarları (7 dil, kiracı modülü): `Recall.Customer.Unresolved` ("Çözülemedi"), `Recall.Customer.NotFound` ("Bulunamadı"),
  `Recall.Customer.LegacyBadge` ("Eski kayıt"), `Recall.Customer.LegacyHint` (araç ipucu), `Recall.Customer.ManualHint` (elle girilen satırın
  araç ipucu, Q-6, §8.8), `Recall.Customer.NameCode` ("{0} · {1}").
  Kanıt paketi PDF / CSV sözcükleri servis ailesinde (`RecallEvidenceText_{lang}.resx`); **parite bekçisi** (`EvidenceTextParityTests`)
  ortak anahtarın Web ailesinde aynı değerle bulunmasını ister → iki ailenin anahtarları **DL-1'de** birlikte eklenir; DL-2 yalnız kullanır.
- Hardcoded metin yok; `recall.l10n.js` / `Recall.cs` anahtar listesine eklenir.

## 12. Validation Rules

| Girdi | Kural | Sonuç |
|---|---|---|
| SHIPMENT `customerRef` | string, `Guid.TryParseExact(…, "D")`, `≠ Guid.Empty` | aksi hâlde yok sayılır, sıra 2 (§8.2) |
| komut `customerRef` | 1–200 karakter (bugünkü); GUID ise kanonikleştirilir (§8.3) | kapsamda yoksa 404 `RECALL_UNKNOWN_CUSTOMER` |
| `addScopeItems` Shipment `customerRef` | 1–200 karakter (bugünkü); kırpılmış değer `Guid.TryParseExact(…, "D")` ile ayrışmamalı (Q-6, §8.2) | ayrışırsa 400 `INVALID_REQUEST`, tüm istek reddedilir, yazım 0 |
| CRM 200 gövdesi | §8.6 tablo | `Unresolved` |

## 13. Failure Path to Verify

1. MVP-6 3.4.0 (alan yok) ya da `customerRef` boş / geçersiz → sıra 2 / 3, bugünkü sonuç bayt eşit (AC-2).
2. CRM yanıt vermiyor (zaman aşımı) → satır "Çözülemedi", istek ≤ 2 sn + pay içinde biter (AC-12).
3. Yetkisiz: kullanıcıda `crm.account.lookup-validation` yok → CRM 403 → "Çözülemedi", recall 200 (AC-18); recall okuma izni yok → bugünkü 403.
4. Başka kiracının hesabı (MVP-6 veri hatası) → CRM 404 → "Bulunamadı", ad sızmaz (AC-11).
5. Tekrar: aynı idempotency anahtarı, GUID farklı harfle → 200 tekrar (AC-3).
6. Yeniden izleme sırasında MVP-6 erişilemez → saklı CRM anahtarı korunur (AC-6 (b)).
7. Kayıtsız `Draft` vakada yeniden izleme MVP-6'dan `customerRef` alır → eski anahtar ve kaynak kalır, yükseltme yok (Q-5; AC-6 (a)).
8. Elle kapsam satırında GUID biçimli `customerRef` (CRM müşterisi seçme denemesi) → 400 `INVALID_REQUEST`, yazım 0 (Q-6; AC-20).
9. Revizyon kopyası kaynağı düşürür → `Crm` satırı eski görünür ve büyük harfli GUID'le kayıt 404 olur — engellenir (AC-6 (e)).

## 14. Authorization Convention

Yeni recall izin anahtarı yok; okuma uçları mevcut `[HasPermission]` anahtarlarıyla (ebeveyn §14.2). CRM çağrısı CRM'in
`crm.account.lookup-validation` anahtarını ister (CRM taslağı §6, K-1); `crm.account.read` gerekmez ve verilmez. Q-1 = kullanıcı token'ı ise
anahtarın geri çağırma rollerine (ebeveyn §14.3) atanması Auth işidir (D-4, Ali); atanmamışken davranış "Çözülemedi"dir, hata değil.

## 15. Gateway / API Routing Decision

Yeni rota yok. Recall: `/api/recall`, `/api/recall/{everything}` → 5067 (ocelot `:1106`, `:1124`). CRM: `/api/crm/accounts/{everything}` →
5061 (`:2979`); U-1 bu joker rotayla taşınır (CRM taslağı §8, K-4). `ocelot.json` değişmez; teyit integration-agent'ındır (yalnız not).

## 16. Acceptance Criteria

Ortak fixture: kiracı T1 / şirket LE1, ikinci kiracı T2; MVP-6 ikizi (`ShipmentBundleDouble`, mevcut — `customerRef` alanı eklenir) ve
**CRM ikizi** (yeni; kiracıyı yalnız gelen token'dan çözer, çağrı sayar, gecikme / durum kodu programlanır). Hesaplar: A = `3c9a1f2e-7b4d-4e61-9a2c-5d8e0f1a2b3c`
(T1, "Example Hastanesi", `ACC-2026-000042`), B (T2). Test sınıflarının adları **önerilir**, kabulde adlarıyla kayda geçer. Her sabotaj
**üretim kodunda** tek tek uygulanır ve geri alınır; kabul kaydına "kırmızı → geri al → yeşil, diff boş" yazılır.

**DL-1**

- [ ] AC-1 (zorunlu) — Sıra 1: sevkiyat 200 `customerRef = A`, `shipToReference = "CUST-100/ADDR-2"` → `runTrace` Shipment düğümü
  `customerRef = A`, kapsam satırı `CustomerRef = A`, `CustomerRefSource = Crm`. Test `RecallCrmCustomerKeyTests.CustomerRef_wins_over_shipTo`.
  Sabotaj S-1: `RecallTraceEngine.ShipmentsAsync`'te shipTo dalını `CustomerAccountId`'den önce değerlendir → kırmızı.
- [ ] AC-2 (zorunlu) — Sıra 2 / 3, her biri ayrı fixture: (a) alan yok (3.4.0 yanıtı); (b) `""`; (c) `"not-a-guid"`; (d) `Guid.Empty`;
  (e) sayı → anahtar `shipToReference`; (f) shipTo da boş → `DOC:<belge>`; (g) okuma 403 / 404 / zaman aşımı → `DOC:<belge>`. (a)'da
  `runTrace` yanıtı FU01 öncesi koddan sabitlenmiş vektöre (`customer` alanı hariç) bayt eşit. Test `RecallCrmCustomerKeyTests.Fallback_order_*`.
  Sabotaj S-2a: okuyucuda GUID ayrıştırmasını kaldır, boş olmayan her string'i hesap say → (c) kırmızı; S-2b: `Guid.Empty` kontrolünü kaldır → (d) kırmızı.
- [ ] AC-3 (zorunlu) — Büyük/küçük harf: S1 `customerRef = A` büyük harfle, S2 küçük harfle → `getCaseCustomers` **tek** satır, anahtar
  küçük harf; `recordNotification` `customerRef = A` büyük harfle → 201, saklanan değer küçük harf; aynı idempotency anahtarıyla küçük harfle
  tekrar → 200, aynı `notificationId`. Test `RecallCrmCustomerKeyTests.Case_insensitive_key`. Sabotaj S-3a: okuyucuda kanonikleştirmeyi
  kaldır (ham metni sakla) → iki satır, kırmızı; S-3b: komut eşleşmesini ham girişle ordinal yap → 404, kırmızı; S-3c: parmak izini ham
  girişten hesapla → 400 `IDEMPOTENCY_KEY_REUSED`, kırmızı.
- [ ] AC-4 (zorunlu) — Kayıtlar yeni anahtarı kabul eder: onaylı kapsamda A ile `recordNotification`, `recordConsumption`, `linkReturn` → 201;
  `getCaseEffectiveness` (A, SKU, birim) satırında `returned`, `consumed` sayılır; kapsamda olmayan GUID → 404 `RECALL_UNKNOWN_CUSTOMER`,
  üç koleksiyonda yazım 0. Test `RecallCrmRecordTests.Records_accept_crm_key`. Sabotaj S-4: `LinkRecallReturnHandler` müşteri kümesini
  yalnız `Crm` olmayan satırlardan kur → `linkReturn` 404, kırmızı.
- [ ] AC-5 (zorunlu) — Eski kayıtlar değişmez: FU01 öncesi biçimde tohumlanmış vaka (kaynaksız kapsam satırları; `CUST-100/ADDR-2`,
  `warehouse-outbound:…:ship-to`, `DOC:…` anahtarlı bildirim, beyan ve iade bağı). (a) FU01 okumaları ve başka müşteriye yeni CRM kaydı
  sonrası eski belgeler bayt eşit; (b) `CUST-100/ADDR-2` ile bildirim → 201, değer aynen; `cust-100/addr-2` → 404 (eski anahtar harfe
  duyarlı kalır); (c) eski anahtarlı isteğin parmak izi sabit vektöre eşit. Test `RecallCrmLegacyTests.Legacy_records_unchanged`.
  Sabotaj S-5: kanonikleştirmeyi (küçük harf) tüm anahtarlara uygula → (b) ve (c) kırmızı.
- [ ] AC-6 (zorunlu) — Yeniden izlemede ve revizyonda kararlılık (§8.5; Q-5 = HAYIR). Her biri ayrı fixture; (a)…(d) `Draft` vakada, hiç
  bildirim / beyan / iade bağı yokken (ön koşul testte doğrulanır: üç koleksiyonda vaka için 0 belge):
  (a) vaka shipTo anahtarıyla izlenmiş (`ShipTo`); MVP-6 artık aynı sevkiyat için `customerRef = A` döner; `submitScope` → saklı satır
  `CUST-100/ADDR-2` anahtarını ve `ShipTo` kaynağını korur; aynı izde yeni belge düğümü `A` / `Crm` alır; `getCaseCustomers` iki satır döner,
  eski satırın `customer.state = Legacy`; (a2) aynısı FU01 öncesi kaynaksız (`null`) `DOC:` anahtarlı satırla → anahtar ve `null` kaynak
  kalır; (a3) silinmiş `Manual` satırın belgesi izle geri gelir, MVP-6 `customerRef = A` döner → satır geri yüklenir, anahtarı ve `Manual`
  kaynağı kalır; (b) saklı `Crm` satırı, yeniden izlemede sevkiyat `Unavailable` → anahtar A ve `Crm` kalır; (c) eski → eski (`DOC:` → shipTo)
  bugünkü gibi güncellenir, kaynak `ShipTo` olur; (d) saklı `Crm` A, yeni iz `Crm` B → A kalır, günlükte
  `recall.scope.customer_ref_changed` satırı düğüm anahtarıyla, A ve B metni günlükte yok.
  (e) `Active` vaka, kayıtlarla (A'ya ve `CUST-100/ADDR-2`'ye birer bildirim), MVP-6 artık `CUST-100/ADDR-2` sevkiyatı için de
  `customerRef = A` döner ve izde bir yeni sevkiyat bulgusu (`customerRef = A`) var → `reviseScope` (bulgu eklenerek) → r+1'de A satırı
  `Crm`, `CUST-100/ADDR-2` satırı `ShipTo`, FU01 öncesi tohumlanmış kaynaksız `DOC:` satırı `null` olarak kopyalanır; eski satır yükseltilmez; delta düğümü `A` / `Crm` alır;
  revizyon onaylandıktan sonra r+1 üzerinde A büyük harfle
  `recordNotification` → 201, saklanan değer küçük harf. Test `RecallCrmReconcileTests.*`.
  Sabotaj (her biri ayrı): S-6a `Reconcile`'da `s.CustomerRef = t.CustomerRef` satırını koşulsuz bırak (bugünkü `:141`) → (a), (a2), (a3),
  (b), (d) kırmızı; S-6b (Q-5'e özgü) `Reconcile`'a "saklı kaynak eski ve yeni kaynak `Crm` ise anahtarı ve kaynağı yükselt" dalı ekle
  ((b), (d) korumaları yerinde) → (a), (a2), (a3) kırmızı, (b) yeşil; S-6c anahtar güncellenince kaynağı güncelleme → (c) kırmızı;
  S-6d `ReviseRecallScopeHandler` kopyasından `CustomerRefSource`'u çıkar → (e) kırmızı (A satırı `Legacy`, büyük harfli bildirim 404).
- [ ] AC-7 (zorunlu) — Çözüldü: CRM 200 `{accountId: A, code, name, status: "inactive", isBillable: false, contractVersion: "crm-account-lookup/1.0"}`
  → satır `customer = {source: Crm, state: Resolved, name, code}`, `label` yok. Test `RecallCustomerDirectoryTests.Resolved_ignores_billability`.
  Sabotaj S-7: çözümü `isBillable = true` koşuluna bağla → `Unresolved`, kırmızı.
- [ ] AC-8 (zorunlu) — Çözülemedi, her biri ayrı fixture: 400, 401, 403, 500, 503, zaman aşımı, ulaşılamıyor, `accountId` farklı, `name` boş,
  `code` yok, `contractVersion = crm-account-lookup/2.0`, ayrıştırılamayan gövde → `state = Unresolved`, `name` / `code` yok. Test
  `RecallCustomerDirectoryTests.Unresolved_*`. Sabotaj S-8a: 403'ü `NotFound` say → kırmızı; S-8b: `accountId` karşılaştırmasını kaldır → kırmızı.
- [ ] AC-9 (zorunlu) — Bulunamadı: CRM 404 → `state = NotFound`. Test `RecallCustomerDirectoryTests.NotFound_is_404_only`. Sabotaj S-9:
  404'ü `Unresolved`'a eşle → kırmızı.
- [ ] AC-10 (zorunlu) — GUID gösterilmez (sunucu): A'lı vakada `getCaseCustomers`, `getCaseEffectiveness`, `getCaseScope`,
  `getCaseScopeDiff`, `runTrace` yanıtlarında A metni yalnız `customerRef` adlı anahtar alanlarında geçer — `label` ve `customer.*` alanlarında
  geçmez; `exportTrace` CSV'si, kanıt paketi CSV ve PDF metni A içermez (üç durumda: Resolved, Unresolved, NotFound). Test
  `RecallCrmNoGuidDisplayTests.*`. Sabotaj (her biri ayrı): S-10a `CaseDetailReadHandlers` etiketinde CRM satırı için `?? g.Key` →
  kırmızı; S-10b `RecallEvidence` müşteri satırına ham anahtar → kırmızı; S-10c iz dışa aktarımında "place" = `n.CustomerRef` (bugünkü
  `:213`) → kırmızı.
- [ ] AC-11 (zorunlu) — Kiracı izolasyonu: (a) T2 vakası, MVP-6 ikizi `customerRef = A` (T1 hesabı) döner → CRM ikizi T2 token'ıyla 404 →
  `NotFound`; yanıtta T1 adı / kodu yok; (b) T1 kullanıcısı T2 vakasının müşterilerini okur → 404 (bugünkü); (c) CRM isteği çağıranın
  `Authorization` başlığını taşır, kiracı başlığı göndermez. Test `RecallCrmTenantTests.*`. Sabotaj S-11: CRM `HttpClient` kaydından
  `.AddHttpMessageHandler<ForwardCallerHeadersHandler>()`'ı kaldır → (c) kırmızı (ikiz 401 → (a)'nın beklenen `NotFound`'u da `Unresolved` olur).
- [ ] AC-12 (zorunlu) — Zaman aşımı ve eşzamanlılık: (a) CRM ikizi A için 5 sn bekler → satır `Unresolved`, `getCaseCustomers` 2,5 sn içinde
  döner; (b) 9 farklı hesap, her biri 1,5 sn → ikizde aynı anda en çok 8 istek. Test `RecallCustomerDirectoryTests.Timeout_2s_and_8_in_flight`.
  Sabotaj S-12a: `PerReadTimeout` 10 sn → (a) kırmızı; S-12b: `MaxParallel` kapısını kaldır → (b) kırmızı.
- [ ] AC-13 (zorunlu) — Önbellek yok, istek içi tekilleştirme: A'lı üç sevkiyat satırı → bir `getCaseCustomers` isteğinde CRM'e 1 çağrı;
  art arda iki istek → toplam 2 çağrı. Test `RecallCustomerDirectoryTests.No_cache_dedup_per_request`. Sabotaj S-13a: `Distinct`'i kaldır →
  3 çağrı, kırmızı; S-13b: statik sözlükte sonuç sakla → ikinci istek 0 çağrı, kırmızı.
- [ ] AC-14 (zorunlu) — Günlükte kişisel veri yok: AC-7 / AC-8 / AC-12 koşularının günlük yakalamasında ad ve kod hiçbir satırda geçmez;
  başarısızlıkta `recall.customer.lookup_unavailable` satırı `AccountId` ve neden taşır; geçersiz `customerRef` değeri günlüğe yazılmaz
  (§8.2). Test `RecallCrmLoggingTests.*`. Sabotaj S-14: geçersiz sözleşme yanıtında gövdeyi günlüğe yaz → kırmızı.
- [ ] AC-15 (zorunlu) — Mühürler: FU01 öncesi kapsam fixture'ının rs-scope-v1 özeti sabit vektöre bayt eşit; aynı kapsam CRM adı değişse de
  aynı özeti verir; `CustomerRefSource` kanonik metinde yok; rt-v1 özeti değişmez. Test `RecallCrmSealTests.*`. Sabotaj S-15:
  `RecallScopeRules.Canonical`'a `customerRefSource` yaz → vektör kırmızı.
- [ ] AC-16 (zorunlu) — RECALL 1.10.0: `recall.openapi.yaml` `version: 1.10.0`, değişiklik notu (Q-6 `addScopeItems` reddi dahil, §8.7),
  `CustomerDisplay` şeması ve beş isteğe bağlı `customer` alanı; `addScopeItems` açıklaması Q-6 cümlesini taşır, `x-error-codes` değişmez;
  AC-1…AC-13 ve AC-20 yanıtları şemaya karşı doğrulanır (`ContractSchemaValidator`); mevcut `RecallContract1x0Tests` sınıfları
  değiştirilmeden yeşil. Test `RecallContract1100Tests.*`. Sabotaj S-16: sunucu `customer.state` alanını yazmasın → şema doğrulaması kırmızı.
- [ ] AC-17 (zorunlu, **kabul kapısı**) — SHIPMENT 3.5.0 hizası: `shipment-bundle.openapi.yaml` `getShipment` 200 şemasında
  `ShipmentBundleHttpReader.CustomerRefField` adlı, `type: string`, `format: uuid` özellik var ve `info.version` ≥ 3.5.0. Bugün (3.4.0)
  bu test **tasarım gereği kırmızıdır**; yeşile dönmeden DL-1 kabul edilmez. Test `ShipmentBundle350AlignmentTests.CustomerRef_field_matches`.
  Sabotaj S-17: sabitin değerini değiştir → kırmızı.
- [ ] AC-18 (zorunlu) — Yetki: CRM ikizi 403 (anahtar atanmamış kullanıcı) → recall uçları 200, satır `Unresolved`; recall okuma izni
  olmayan kullanıcı → 403 (bugünkü); `AuthorizationGuardTests` değiştirilmeden yeşil (yeni eylem yok, her eylem `[HasPermission]`).
  Test `RecallCrmAuthorizationTests.*`. Sabotaj S-18: CRM 403'ünü yukarı taşı (uç 503) → kırmızı.
- [ ] AC-19 (zorunlu) — 7 dil: `Recall.Customer.*` anahtarları `Recall.{en,tr,fr,es,zh,ar,ru}.resx` ve `RecallEvidenceText_{7}.resx`'te dolu,
  `zh` / `ar` / `ru` İngilizce kopya değil; `EvidenceTextParityTests` ve Recall resx paritesi yeşil. Sabotaj S-19: `Recall.ar.resx`'ten
  `Recall.Customer.LegacyBadge`'ı sil → kırmızı.
- [ ] AC-20 (zorunlu) — Elle kapsam satırında CRM müşterisi seçilemez (§8.2; Q-6 = HAYIR). `Draft` vakada `addScopeItems`, her biri ayrı
  fixture: (a) Shipment `customerRef = "CUST-300/ADDR-1"` → 200; satır `CustomerRefSource = Manual`; `getCaseScope` ve `getCaseCustomers`
  satırında `customer = {source: Manual, state: Legacy, label: "CUST-300/ADDR-1"}`, CRM ikizine 0 çağrı; (b) `customerRef = A` (küçük harf)
  → 400 `INVALID_REQUEST`; (c) A büyük harfle → 400; (d) `"  " + A + " "` (kırpılınca GUID) → 400; (e) iki maddelik toplu istek, ilki geçerli
  (`CUST-301/ADDR-1`), ikincisi A → 400 ve vaka için `recall_scope_items`, vaka olayı ve denetim satırı sayıları istek öncesine eşit
  (ilk madde de yazılmaz); (e2) `"{" + A + "}"` (B biçimi) ve tiresiz A (N biçimi) → 200, `Manual`, `Legacy` (yasak yalnız `"D"`
  biçimine bağlı); (f) **veri sayımı** (salt okuma, kabul kaydına): DL-1 kabul ortamında ve FU01'in canlıya alınmasından önce hedef
  ortamda, `Kind = Shipment` ve `CustomerRefSource` `Crm` olmayan (`null` dahil) kapsam satırlarında `customerRef`'i `"D"` biçimli olanların
  sayısı — beklenen 0; sıfır değilse DL-1 kabul edilmez, satırlar CT'ye gider (O-2). Test `RecallCrmManualScopeTests.*`.
  Sabotaj (her biri ayrı): S-20a GUID kontrolünü kaldır → (b), (c) kırmızı; S-20b kontrolü kırpmadan önce yap → (d) kırmızı; S-20c kontrolü
  yalnız küçük harfe bağla (`Guid.TryParseExact` yerine kanonik metin karşılaştırması) → (c) kırmızı; S-20d reddetmek
  yerine GUID'li maddeyi sessizce atla (öteki maddeler yazılır, 200) → (e) kırmızı; S-20e `Guid.TryParse` (her biçim) kullan → (e2) kırmızı; S-20f satırı
  `CustomerRefSource` yazmadan kur → (a) kırmızı (`source = Unknown`). (f) üretim kodu değildir; sayım sorgusu ve sonucu kabul kaydına
  yazılır, sabotajı yoktur.

**DL-2**

- [ ] AC-21 (zorunlu) — Müşteriler sekmesi: `Resolved` satır "{ad} · {kod}", altında anahtar satırı yok; `Unresolved` "Çözülemedi",
  `NotFound` "Bulunamadı"; sayfanın metin içeriğinde ve `title` özniteliklerinde A geçmez (`data-notify` / `data-consume` değerinde
  geçebilir). Test `recall-fu01.test.mjs` "customers tab never prints the account id". Sabotaj S-21: `case-detail.js:248`'deki tek aralıklı
  `customerRef` alt satırını CRM satırında bırak → kırmızı.
- [ ] AC-22 (zorunlu) — Diğer görünümler: kapsam sekmesi, kapsam farkı listesi (`getCaseScopeDiff` `added`), iade bağları, etkinlik tablosu,
  üç pencere ve seçim listesinin `option` metni, iz ekranı — A metin olarak geçmez, ad + kod ya da durum metni görünür. Test
  `recall-fu01.test.mjs` "no view prints the account id". Sabotaj (her biri ayrı): S-22a `trace-view.js:32` `n.customerRef`'e geri dönsün →
  kırmızı; S-22b etkinlik tablosu (`:287`) `r.customerRef` → kırmızı; S-22c kapsam farkı listesi (`case-detail.js:165`) `n.customerRef` →
  kırmızı.
- [ ] AC-23 (zorunlu) — Eski kayıt rozeti: AC-6 (a) verisiyle ekran iki satır çizer; eski satırda "Eski kayıt" rozeti ve araç ipucu, CRM
  satırında rozet yok; satırlar ad eşleşse de birleşmez; eski satırın araç ipucu `Recall.Customer.LegacyHint`; AC-20 (a)'nın `Manual`
  satırı da rozetle ve `Recall.Customer.ManualHint` araç ipucuyla çizilir. Test `recall-fu01.test.mjs` "legacy row stays separate with
  badge". Sabotaj (her biri ayrı): S-23a satırları `customer.name`'e göre birleştir → kırmızı; S-23b rozeti yalnız `source = Unknown`'a
  bağla → `Manual` satırı rozetsiz, kırmızı; S-23c `Manual` satırına `LegacyHint` bas → kırmızı.
- [ ] AC-24 (zorunlu) — Sabit metin yok: dil `ar` iken üç durum metni ve rozet `Recall.ar.resx` değerleriyle çizilir. Test
  `recall-fu01.test.mjs` "texts come from l10n". Sabotaj S-24: `case-detail.js`'e "Çözülemedi" sabit yaz → kırmızı.

## 17. Test Expectations

- Build: InventoryService ve Web çözümü uyarı politikası değişmeden derlenir.
- **Tam paket:** servis dokunulduğu için kabul koşusu filtresiz Inventory test paketi + `Diten.Web.Tests` (CT kuralı); temel kırmızılar aynı
  gün `main` ile karşılaştırılır. Inventory testleri kendi Mongo replica set'ini başlatır (27017 kullanılmaz).
- Sözleşme: RECALL 1.10.0 şema doğrulaması (AC-16); SHIPMENT 3.5.0 hizası (AC-17, kapı).
- İzolasyon: AC-11; mevcut `RecallTenantBoundaryTests` değiştirilmeden yeşil.
- Denetim: yeni olay yok; mevcut `recall.customer.notified` / `recall.consumption.declared` / iade bağlama olaylarının ayrıntısı CRM
  anahtarında kanonik GUID taşır (AC-4 içinde ölçülür). Q-6 reddinde (`addScopeItems` 400) vaka olayı ve denetim satırı yazılmaz
  (AC-20 (e)); başarılı `addScopeItems` ve `reviseScope` denetim satırları değişmez.
- Veri sayımı: AC-20 (f) salt-okuma sayımının sorgusu ve sonucu DL-1 kabul kaydına yazılır.
- Smoke (E5, FU01 kapanışı — DL kabulü değil): MOD-0149 DL-1 ve MVP-6 3.5.0 yayındayken bir vakada müşteri adı ve kodu görünür, GUID görünmez.

### 17.1 Sabotaj tablosu

Her AC'nin sabotajı metnindedir (S-1…S-24, alt harfli olanlar ayrı koşu). Sabotajı olmayan tek alt ölçüt AC-20 (f)'dir: üretim kodu
değil, salt-okuma veri sayımıdır (kanıtı sorgu + sonuç). AC-17 tasarım gereği D-1 kapanana dek kırmızıdır; sabotajı (S-17) 3.5.0 FROZEN
olduktan sonra koşulur.

## 18. Ready-for-dev Checklist

- [x] Golden Reference / Backend File Convention ebeveyn düzeniyle (§10)
- [x] Frontmatter zorunlu alanlar dolu; ekranlar ebeveynin üç sayfası (§9)
- [x] Validation Rules (§12), Failure Path ≥ 4 (§13), Authorization (§14), Gateway kararı (§15)
- [x] AC'lerin hepsi üretim koduna karşı ve adlı sabotajlı (§16, 24 AC; tek istisna AC-20 (f) veri sayımı, §17.1)
- [x] Audited Events (§21) — yeni yazma komutu yok
- [x] Teslim dilimleri (§22) ve sayfa yardımı kararı (§23)
- [x] Sahip kararları Q-5 = HAYIR, Q-6 = HAYIR (Beste, 2026-11-03) — §8.2, §8.5, AC-6, AC-20
- [x] CT kararları CT-1 (mühre girmez), CT-2 (RECALL 1.10.0) (MVP-1 CT, 2026-11-03) — §19.6
- [x] Keşif bulgularının (gateway crm 5061 / recall 5067) mükerrer sayılmaması CT'ce kabul edildi (§19.1)
- [x] Registry satırı (CT, `8ba30d12b`)
- [ ] CT-APR-02 bağımsız inceleme (yazandan ayrı oturum) + CT-APR-01 onay satırı (yüksek risk)
- [ ] Açık: Q-1…Q-4 (§19.4), D-1…D-5 (§7). Paketin sınıflaması: D-1 (Q-2) ve D-2 (Q-3) **DL-1 kabul kapısıdır**, kodlamayı engellemez
  (§7 "kodlanabilir, kabul edilemez", §22); D-3 (Q-1) kodu engellemez (varsayılan kullanıcı token'ı, karar yalnız DI işleyicisini değiştirir);
  D-4 (Q-4) çalışma zamanı ataması (atanmamışken "Çözülemedi", AC-18); D-5 CRM sahibinin adımı. Ready-for-dev'e geçişte bunların nasıl
  sayılacağı CT kararıdır
- [ ] `status: ready-for-dev` — CT kararıyla

## 19. Implementation Notes

### 19.1 Kimlik ve keşif kanıtı

- Ön kontrol: çıkış 0 (yukarıdaki künye notu). Araç `verify_module_id.py` sha256 `ae87a566…b51a`; depo HEAD `8b5205226`; origin/main `66864f1ab`
  (son fetch 2026-11-02T21:18, uzak durum doğrulanmadı). r2: registry satırıyla (`8ba30d12b`) aynı komut yeniden çıkış 0; keşif yeniden
  koşulmadı (registry satırı bu paketin kendisidir; diğer kaynaklar `8b5205226`'dan beri değişmedi — §künye ölçüm tabanı).
- Keşif `--discover "recall customer crm"`: çıkış 3, rapor sha256 `d6262ba44790f4f0bb6ae4754eb0f163843546e106425a3f3da5a33dcfbd2b15`; 493 ref,
  9 görev kaynağı. Bulgular ve kararlar:

| Bulgu | Karar | Gerekçe |
|---|---|---|
| gateway `'crm'` → 5061 | mükerrer değil — **üretici-tüketici** | CRM-ACCOUNT-LOOKUP'ın üreticisi CrmService; bu paket tüketicidir (§7 D-2) |
| gateway `'recall'` → 5067 | mükerrer değil — **ebeveynin kendi rotası** | `/api/recall*` MOD-0177'nindir (ocelot `:1106`, `:1124`) |

- Blueprint, SoR, registry, paket ve görev eşleşmesi 0. **Kısmi inceleme sınırları** (raporun EKSİK satırları): bu makinede olmayan ya da
  son fetch'ten sonra itilen dallar, commit edilmemiş işler, kayıtsız görev kaynakları ve paketlerin iş kapsamı (yalnız ad / kimlik eşleşti)
  görülmedi.

### 19.2 Ölçülen gerçekler (`8b5205226`, salt okuma)

- Anahtar üretimi: `RecallTraceEngine.cs:331` (`"DOC:" + document`), `:375` (`shipToReference`, yalnız okuma `Ok` ise);
  `RecallTraceService.cs:93` (`n.CustomerRef ?? "DOC:" + document`). `TraceModel.cs:97` sırayı belgeler.
- Okuyucu: `IShipmentReader` `Ports/IRecallTraceSource.cs:86-89` (ayrı dosya değil); `ShipmentBundleHttpReader.cs:19-22` (`MaxParallel = 8`,
  `PerReadTimeout = 2 sn`), `:54` (GUID değilse NotFound), `:66-70` (yalnız `status`, `actualDeliverAt`, `shipToReference` okunur), `:83`
  (başarısızlık günlüğü), **önbellek yok**. DI `RecallServiceCollectionExtensions.cs:48-57` (Gateway taban adresi, `ForwardCallerHeadersHandler`).
- Uzunluk: `RecallContract.MaxCustomerRefLength = 200` (`RecallContract.cs:20`); kullanım `CaseCommandHandlers.cs:369`, `R3Handlers.cs:383`.
- Eşleşmelerin **hepsi ordinal / harfe duyarlı**: `RecordHandlers.cs:58`, `:155`, `:163`, `:169-170`; `R3Handlers.cs:417`;
  `CaseDetailReadHandlers.cs:75` (gruplama), `:78`, `:90`; `RecallEvidence.cs:283-286`; `RecallReads.cs:80`, `:137-138`; etkinlik
  `RecallRules.cs:342-343`.
- Parmak izleri ham `customerRef` ile: `RecordHandlers.cs:34`, `:127`; `R3Handlers.cs:388`.
- Müşteri satırı etiketi: `CaseDetailReadHandlers.cs:80-83` — shipTo, yoksa `DOC:` için belge numarası, **yoksa anahtarın kendisi** (CRM
  anahtarında GUID'e düşerdi → §8.7).
- Yeniden izleme: `RecallTraceService.Reconcile` `:111`, anahtar koşulsuz üzerine yazılır `:141`; tek çağıran `CaseCommandHandlers.cs:553` (`submitScope`).
- Mühürler: rs-scope-v1 `RecallRules.cs:243`, `customerRef` kanonik metinde `:305`; rt-v1 müşteri referansını içermez (`TraceRules.cs:108-111`).
- Gösterim yerleri: `case-detail.js:135`, `:248` (anahtar tek aralıklı alt satırda), `:259`, `:287`, `:363`, `:380`, `:405`;
  `trace-view.js:32`; iz dışa aktarımı `RecallTraceService.cs:213`; kanıt paketi `RecallEvidence.cs:283-295`, `:428-440`.
  Tatbikat `Customers` listesi `DrillRunHandlers.cs:284` — ekranda gösterilmiyor (`drill-view.js` yalnız adım adı).
- Sözleşmeler: RECALL `1.9.0` FROZEN (`recall.openapi.yaml:14-15`), `customerRef` şemaları `:1198`, `:1291`, `:1308`, `:1386`, `:1398`,
  `:1533-1534` (`label`), `:1566`; SHIPMENT-BUNDLE `3.4.0` FROZEN (`shipment-bundle.openapi.yaml:15`), `shipToReference` `:3208`,
  `customerRef` / `partyId` **yok**. CRM taslağı: U-1 200 alanları `accountId, code, name, status, isBillable, contractVersion`; 404 üç durumda
  aynı gövde; izin `crm.account.lookup-validation`; `x-consumers` bugün yalnız MOD-0120.
- MVP-6 eski anahtar biçimi: `WarehouseShipmentMapper.cs:51` `warehouse-outbound:{escaped outboundId}:ship-to` (SupplyChainService, salt okuma).
- Kaynak eşitliği: `git diff --stat 372082aa0 8b5205226 -- <Recall yolları, iki sözleşme, CRM taslağı>` boş.
- **r2 ölçümleri (`8ba30d12b`):** durumlar — `CanEditScope` / `CanSubmit` yalnız `Draft` (`RecallRules.cs:171`, `:173`), `CanReturn` yalnız
  `PendingScopeApproval` (`:175`), `AcceptsRecords` = `IsUnderRecall` (`Active` / `EffectivenessCheck`, `:169`, `:197`); iade bağı
  `R3Handlers.cs:396`. Elle ekleme `AddRecallScopeItemsHandler` (`CaseCommandHandlers.cs:283`): Shipment dalı `:361-382`, satır kurulumu
  `:403-427` (`CustomerRef` `:421`), yazım `:432-440` (doğrulama tümüyle önce — istek bütün). Reconcile elle satır kuralı
  `RecallTraceService.cs:127`, silinmiş satırın geri yüklenmesi `:128-136`. Revizyon `ReviseRecallScopeHandler` `R3Handlers.cs:463`, delta
  `:502-503`, kopya `:527-533` (`CustomerRef = i.CustomerRef` `:531`). Kapsam farkı listesi `case-detail.js:157-170` (`:165` `n.customerRef`
  basar); `ScopeDiff.added` = `TraceNode` (`recall.openapi.yaml:1499-1504`). Web'de `addScopeItems` çağıran ekran yok
  (`wwwroot/assets/js/Recall/*.js` taraması). Mevcut testlerde GUID biçimli elle `customerRef` yok (`tests/…/Recall/*.cs` taraması —
  Q-6 reddi mevcut testi kırmaz).

### 19.3 Varsayımlar

- ASSUMPTION-FU01-1: `customerRef` sevkiyat kökünde (`getShipment` 200) tek değerdir; satır düzeyinde değildir (Q-2 ile teyit).
- ASSUMPTION-FU01-2: MVP-6 yalnız kendi kiracısının hesabını yazar; aksi veri hatası CRM 404 ile "Bulunamadı" olur, sızmaz (AC-11).
- ASSUMPTION-FU01-3: Geri çağırma bildirimi müşterinin CRM durumundan bağımsızdır (pasif / arşivli müşteri de çözülür ve gösterilir).

### 19.4 Açık sorular

| # | Soru | Sahip | Tarih |
|---|---|---|---|
| Q-1 | CRM çağrı yolu: kullanıcı token'ı Gateway üzerinden mi, servis kimliği mi? (servis kimliğinde kiracı nasıl taşınır) | CRM / commercial-suite (Beste) | soruldu 2026-11-03; yanıt TBD |
| Q-2 | SHIPMENT 3.5.0 alanının kesin adı, tipi (`string`, `format: uuid`?), yeri (kök) ve küçük harf garantisi | MVP-6 CT | TBD (3.5.0 taslağıyla teyit) |
| Q-3 | MOD-0149 DL-1 (U-1 ucu) teslim tarihi ve sözleşmenin dondurulma tarihi | CRM / commercial-suite (Beste) | TBD |
| Q-4 | `crm.account.lookup-validation` hangi geri çağırma rollerine atanır (yalnız Q-1 = kullanıcı token'ı ise) | Auth (Ali) | TBD |

Q-5 ve Q-6 r2'de kapandı — §19.5 madde 8, 9.

### 19.5 Verilen kararlar (sahip — Beste / MVP-1 CT, 2026-11-03)

1. `customerRef` okuma ve sıra (customerRef → shipToReference → DOC), harf duyarsız karşılaştırma — §8.2, §8.3.
2. Ad ve kod CRM-ACCOUNT-LOOKUP 1.0.0 U-1'den; zaman aşımı ve önbellek mevcut okuyucu deseniyle (okuma başına 2 sn, önbelleksiz) — §8.6.
3. GUID gösterilmez; "Çözülemedi" / "Bulunamadı" — §8.8.
4. **SB-19:** eski anahtarlı satırlar yeni kimliğe otomatik bağlanmaz; "Eski kayıt" rozetiyle ayrı satır — **kabul edilen sınır** (§8.5).
5. Kayıtlar yeni anahtarı kabul eder; eski kayıtlar değişmez — §8.4.
6. MOD-0177 CRM tüketicisi olarak yazılır (sözleşme CRM'in); izin ataması Auth işi — §7 D-4, D-5.
7. Yeni metinler 7 dilde — §11.
8. **Q-5 = HAYIR** (Beste, 2026-11-03): hiç müşteri kaydı olmayan vakada da eski anahtar CRM kimliğine yükseltilmez; kural kayıt varlığına
   bakmaz, revizyon kopyası için de geçerlidir — §8.5, AC-6, AC-23.
9. **Q-6 = HAYIR** (Beste, 2026-11-03): elle eklenen kapsam satırında CRM müşterisi seçilemez; satır `Manual` kaynaklı serbest metindir,
   `"D"` biçimli GUID metni 400 `INVALID_REQUEST` ile reddedilir (uygulama biçimi paket yazarının tasarımıdır — inceleme konusu), ekranda
   "Eski kayıt" rozeti ve `ManualHint` araç ipucu — §8.2, §8.8, AC-20, AC-23. FU-0177-FU01-SEL kapandı (§20).

### 19.6 CT kararları (MVP-1 CT, 2026-11-03 — verildi)

| # | Karar | Sonuç |
|---|---|---|
| CT-1 | `CustomerRefSource` rs-scope-v1 mührüne girer mi? | **Hayır** — biçim adı ve FU01 öncesi mühürler değişmez (§4.1, §8.5, AC-15) |
| CT-2 | Sözleşme numarası | RECALL **1.10.0** — `8ba30d12b`'de yeniden ölçüldü: sözleşme `1.9.0`, başka uçuşta 1.10.0 yok (§7 D-6) |
| — | `--discover` bulguları (gateway crm 5061 / recall 5067, üretici-tüketici) | mükerrer sayılmaz — CT kabul etti (§19.1) |
| — | Registry satırı | CT ekledi, `8ba30d12b` |

## 20. Follow-up Items

| ID | Konu | Sahip |
|---|---|---|
| O-1 | **Gözlem (ebeveyn, bu FU'nun kapsamı dışı):** bugün yeniden izlemede MVP-6 o an erişilemezse saklı shipTo anahtarı `DOC:` anahtarına düşer (`RecallTraceService.cs:141`); o müşterinin kayıtları yetim kalır. FU01 yalnız CRM anahtarını korur (§8.5). Eski → eski düşüşün düzeltilmesi ayrı karar | MOD-0177 / CT |
| FU-0177-CUST-01 | Ebeveyn §20 satırı bu paketle karşılanır (MVP-6 party id yerine CRM `Account.Id` + U-1) — ebeveyn satırının güncellenmesi CT adımı | CT |
| O-2 | **Gözlem (veri):** FU01 öncesi `"D"` GUID biçimli eski anahtar aynı vakadaki `Crm` satırıyla çakışabilir (§8.3). Ölçüm AC-20 (f) sayımı; sıfır değilse DL-1 kabul edilmez, satırların işlenişi CT kararı (göç yok) | CT |
| FU-0177-FU01-SEL | Elle eklenen kapsam satırında CRM müşteri seçici — **kapandı, açılmaz**: Q-6 = HAYIR (Beste, 2026-11-03). Yeniden istenirse yeni sahip kararı ve yeni FU gerekir | — |
| FU-0177-FU01-REBIND | Eski anahtarlı kayıtların CRM kimliğine **elle**, denetimli bağlanması (SB-19'un otomatik yasağı korunarak) — ürün kararı | Ürün / CT |

## 21. Audited Events

Yeni yazma komutu ve yeni denetim olayı yok. Mevcut olaylar (ebeveyn §8.12): `recall.customer.notified`, `recall.consumption.declared`,
iade bağlama — ayrıntıdaki `customerRef` CRM anahtarında kanonik GUID'dir (kayıtta kalır). Davranışı değişen mevcut yazma komutları:
`addScopeItems` (`RecallContract.AuditScopeItemsAdded`, yol ebeveynin) — başarılı istekte denetim satırı değişmez, Q-6 reddinde (400) hiç
yazılmaz (AC-20 (e)); `submitScope` ve `reviseScope` — denetim satırları değişmez (`CustomerRefSource` denetim ayrıntısına girmez). CRM
okuması AUD-001 okuma olayı üretmez (CRM taslağı §1).

## 22. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | arka uç: customerRef okuma + anahtar sırası + ad çözümü + kayıtlar + RECALL 1.10.0 + 7 dil metinleri | Owned Objects, Entity Fields, Repo Scope, Dependencies, Müşteri anahtarı sırası, Büyük/küçük harf ve anahtar biçimi, Kayıtlar, Yeniden izlemede anahtar kararlılığı (SB-19), Ad çözümü (CRM-ACCOUNT-LOOKUP U-1), Sözleşme — RECALL 1.10.0, Gösterim kuralı, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Audited Events | E4 | plan kimliği yok | MVP-1 |
| DL-2 | ekran: ad + kod, durum metinleri, eski kayıt rozeti | Gösterim kuralı, Frontend File Contract, Layout & Shell Contract, Page Help (UXH-001) | E4 | plan kimliği yok | MVP-1 |

Kabul ölçütleri: DL-1 → AC-1…AC-20; DL-2 → AC-21…AC-24. **DL-1 kabul kapısı:** D-1 (SHIPMENT 3.5.0 FROZEN, AC-17 yeşil), D-2 (CRM U-1
şekli teyitli ya da sözleşme FROZEN) ve AC-20 (f) veri sayımı = 0 (O-2). D-1 / D-2 kapanmadan DL-1 ikizlerle kodlanabilir, kabul edilemez. DL-2 DL-1'e dayanır. FU01'in
kapanışı ayrıca E5 smoke ister (§17; MOD-0149 DL-1 ve MVP-6 3.5.0 yayında). 7 dil metinleri parite bekçisi gereği DL-1'dedir (§11);
"Gösterim kuralı" iki dilime bölünür: DL-1 sunucu çıktıları (API, CSV, PDF), DL-2 ekran.

## 23. Page Help (UXH-001)

Yeni sayfa yok. Vaka detayı "?" panelindeki müşteriler adımı (`PageHelpResource`, 7 dil) "Eski kayıt" rozetini bir cümleyle anlatır
(DL-2; yeni tur yok).
