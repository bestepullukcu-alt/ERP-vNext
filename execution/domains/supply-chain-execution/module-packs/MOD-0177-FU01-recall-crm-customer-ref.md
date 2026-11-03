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
status_note: "draft r1 (2026-11-03) — sahip kararları (Beste / MVP-1 CT, 2026-11-03) §19.5'te. Kimlik ön kontrolü çıkış 0; keşif çıkış 3 (iki gateway bulgusu, mükerrer değil — §19.1). Registry satırı yok — satırı CT ekler. Kod yazılmaz: CT-APR-02 bağımsız inceleme + CT-APR-01 onay satırı gerekir. DL-1 kabul kapısı SHIPMENT-BUNDLE 3.5.0 (MVP-6) dondurulması ve CRM-ACCOUNT-LOOKUP 1.0.0 U-1 şeklinin teyidi (§7). Açık sorular Q-1…Q-6 (§19.4), CT kararları CT-1, CT-2 (§19.6)."
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
> kararı ve gerekçesi §19.1'de (üretici-tüketici ilişkisi ve ebeveynin kendi rotası; mükerrer değil). **Registry satırı bu paketle yazılmaz** —
> satırı CT ekler.
>
> **Yetki:** bu paket > `execution/domains/supply-chain-execution/domain-config.md` > `AGENTS.md` > `.antigravity/`. Ebeveyn
> `MOD-0177-recall-readiness.md` §8.6 "Customer identity" (`:611-613`) ve ASSUMPTION-0177-07 (`:1183`) bu paketin **tabanıdır**; bu paket
> FU-0177-CUST-01'i (`:1588`, "Customer party id / names from MVP-6") **CRM kimliğiyle** karşılar ve tabanı genişletir: anahtar sırasının başına
> `customerRef` eklenir, sonraki iki adım (shipToReference, `DOC:`) **aynen** kalır. Görevde "§8.4 müşteri kimliği, satır ~611" diye anılan yer
> ebeveynde **§8.6**'dır (§8.4 KR-B otomatik karantina isteğidir) — satır numarası doğru, bölüm numarası düzeltildi.
>
> **Ölçüm tabanı:** `services/Diten.InventoryService`, `frontend/Diten.Web`, `docs/analysis/contracts/recall.openapi.yaml`,
> `shipment-bundle.openapi.yaml` ve CRM taslağı `8b5205226`'da okundu; bu yollar uygulama dalı `feature/sce/mod-0173-inventory-ledger-valuation`
> @ `372082aa0` ile **aynıdır** (`git diff --stat 372082aa0 8b5205226 -- <yollar>` boş). Satır numaraları bu tabandandır.

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
Yeni yazma komutu, yeni izin anahtarı, yeni Gateway rotası yoktur.

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
| `CustomerRefSource` | `RecallCustomerRefSource?` (`Crm`, `ShipTo`, `Document`, `Manual`) | yalnız `Kind = Shipment`'ta dolu; FU01'den önce yazılmış satırda `null` (= kaynak bilinmiyor, eski kayıt). Enum değerleri yalnız sona eklenir. **rs-scope-v1 mührüne girmez** (CT-1, §8.5). |

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
  `R3Handlers.cs` (yalnız `LinkRecallReturnHandler`)
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
| D-6 | MOD-0177 ebeveyn (RECALL 1.9.0 FROZEN, `recall.openapi.yaml:14`) | uygulandı | 1.10.0 bu paketin tek yazıcı adımı; `1.10.0` numarası başka uçuşta değil (CT-2) |

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

**Elle eklenen Shipment kapsam satırı** (`CaseCommandHandlers.cs:366-373`): bugünkü gibi serbest metin, `CustomerRefSource = Manual`;
CRM hesabı seçilemez (Q-6). Uzunluk sınırı `RecallContract.MaxCustomerRefLength = 200` (`RecallContract.cs:20`) değişmez; GUID 36 karakterdir.

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
(`RecallTraceService.cs:141` `s.CustomerRef = t.CustomerRef`; tek çağıran `CaseCommandHandlers.cs:553`). FU01 kuralı:

| Saklı satırın kaynağı | Yeni izin kaynağı | Sonuç |
|---|---|---|
| `null` / `ShipTo` / `Document` (eski) | `Crm` | **saklı anahtar kalır** — eski satır yeni kimliğe otomatik bağlanmaz (SB-19) |
| `Crm` | `ShipTo` / `Document` (ör. o an MVP-6 erişilemez) | **saklı CRM anahtarı kalır** — geçici okuma hatası anahtarı düşürmez, kayıtlar yetim kalmaz |
| `Crm` | `Crm`, farklı GUID | saklı anahtar kalır; olay `recall.scope.customer_ref_changed` günlüğe (değer yok, yalnız düğüm anahtarı) — veri sorunu MVP-6'dadır |
| eski | eski | **bugünkü davranış** (anahtar güncellenir) — bu paketin kapsamı dışında; gözlem O-1 (§20) |

Yeni sevkiyat düğümleri (ekleme) §8.2 sırasıyla anahtar alır. Sonuç: aynı müşteri bir vakada hem eski anahtarlı (eski sevkiyat) hem CRM
anahtarlı (yeni sevkiyat) iki satır olarak görünebilir — **kabul edilen sınır** (SB-19), ekranda "Eski kayıt" rozetiyle ayrılır (§8.8).

**Mühür (CT-1):** `CustomerRefSource` rs-scope-v1 kanonik biçimine **yazılmaz** (`RecallRules.cs:282-318`; biçim adı `:243` değişmez);
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
`CaseDetailReadHandlers.cs:80-83`). Komut gövdeleri (`:467-470`, `:496-499`, `:548-552`) değişmez; açıklamalarına §8.3 harf kuralı eklenir.

### 8.8 Gösterim kuralı

- CRM anahtarlı müşteri (ekran, CSV, PDF): `Resolved` → "{ad} · {kod}"; `Unresolved` → "Çözülemedi"; `NotFound` → "Bulunamadı".
  **GUID metin, `title`, araç ipucu, CSV / PDF hücresi olarak hiçbir yerde görünmez**; yalnız komut gönderen öğenin değerinde (`data-*` /
  `option.value`) ve API'nin `customerRef` alanında taşınır.
- Eski anahtarlı satır: bugünkü etiket + "Eski kayıt" rozeti (araç ipucu: "CRM müşteri kimliğinden önce kaydedildi; yeni kimliğe otomatik
  bağlanmaz."); CRM satırıyla **birleştirilmez**, ayrı satırdır.
- Görünen yerler (ölçüldü): vaka detayı kapsam sekmesi (`case-detail.js:135`), müşteriler sekmesi (`:248` — bugün anahtarı tek aralıklı alt
  satırda basar), iade bağları (`:259`), etkinlik tablosu (`:287`), bildirim / tüketim / iade bağlama pencereleri (`:363`, `:380`, `:405`),
  iz ekranı (`trace-view.js:32`), iz dışa aktarımı (`RecallTraceService.cs:213` "place"), kanıt paketi müşteri, iade ve iz bölümleri
  (`RecallEvidence.cs:283-295`, `:428-440`). Tatbikat çalıştırmasının `Customers` listesi (`DrillRunHandlers.cs:284`) gösterilmez
  (kanıt paketinde "notApplicableDrill", `RecallEvidence.cs:382`) — değişmez.
- Kişisel veri: tarayıcıya müşteri hakkında yalnız ad ve kod gider; adres, iletişim, durum, faturalanabilirlik gitmez.

### 8.9 Durum ve komut tablosu

Yeni durum ve yeni komut yok. Okuma uçları (`getCaseCustomers`, `getCaseEffectiveness`, `getCaseScope`, `runTrace`, `exportTrace`, kanıt
paketi önizleme / dışa aktarma) `customer` nesnesini ekler; izinleri değişmez.

## 9. Layout & Shell Contract

Ebeveynin sayfaları (`_LayoutTenantShell`, Scope = Tenant). Yeni sayfa, yeni modal, yeni form alanı yok (`form_field_count: 0`). Rozet mevcut
`badge bg-label-*` sınıflarıyla.

## 10. Backend File Convention

Ebeveyn §10 düzeni: port `Application/Features/Recall/Ports/`, kural `Domain/Recall/Rules/`, HTTP bağdaştırıcı `Infrastructure/Recall/`, DI
`Api/Recall/RecallServiceCollectionExtensions.cs`. Yeni denetleyici yok.

## 11. Frontend File Contract

- DL-2 yalnız `case-detail.js` ve `trace-view.js` gösterimini değiştirir; `case-view.js` `customerRows` anahtarla eşleşmeye devam eder.
- Metin anahtarları (7 dil, kiracı modülü): `Recall.Customer.Unresolved` ("Çözülemedi"), `Recall.Customer.NotFound` ("Bulunamadı"),
  `Recall.Customer.LegacyBadge` ("Eski kayıt"), `Recall.Customer.LegacyHint` (araç ipucu), `Recall.Customer.NameCode` ("{0} · {1}").
  Kanıt paketi PDF / CSV sözcükleri servis ailesinde (`RecallEvidenceText_{lang}.resx`); **parite bekçisi** (`EvidenceTextParityTests`)
  ortak anahtarın Web ailesinde aynı değerle bulunmasını ister → iki ailenin anahtarları **DL-1'de** birlikte eklenir; DL-2 yalnız kullanır.
- Hardcoded metin yok; `recall.l10n.js` / `Recall.cs` anahtar listesine eklenir.

## 12. Validation Rules

| Girdi | Kural | Sonuç |
|---|---|---|
| SHIPMENT `customerRef` | string, `Guid.TryParseExact(…, "D")`, `≠ Guid.Empty` | aksi hâlde yok sayılır, sıra 2 (§8.2) |
| komut `customerRef` | 1–200 karakter (bugünkü); GUID ise kanonikleştirilir (§8.3) | kapsamda yoksa 404 `RECALL_UNKNOWN_CUSTOMER` |
| CRM 200 gövdesi | §8.6 tablo | `Unresolved` |

## 13. Failure Path to Verify

1. MVP-6 3.4.0 (alan yok) ya da `customerRef` boş / geçersiz → sıra 2 / 3, bugünkü sonuç bayt eşit (AC-2).
2. CRM yanıt vermiyor (zaman aşımı) → satır "Çözülemedi", istek ≤ 2 sn + pay içinde biter (AC-12).
3. Yetkisiz: kullanıcıda `crm.account.lookup-validation` yok → CRM 403 → "Çözülemedi", recall 200 (AC-18); recall okuma izni yok → bugünkü 403.
4. Başka kiracının hesabı (MVP-6 veri hatası) → CRM 404 → "Bulunamadı", ad sızmaz (AC-11).
5. Tekrar: aynı idempotency anahtarı, GUID farklı harfle → 200 tekrar (AC-3).
6. Yeniden izleme sırasında MVP-6 erişilemez → saklı CRM anahtarı korunur (AC-6).

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
- [ ] AC-6 (zorunlu) — Yeniden izlemede kararlılık (§8.5): (a) Taslak vaka shipTo anahtarıyla izlenmiş; MVP-6 artık `customerRef = A` döner;
  `submitScope` → saklı satır shipTo anahtarını ve kaynağını korur; aynı izde yeni belge düğümü `A` / `Crm` alır; `getCaseCustomers` iki
  satır döner, eski satırın `customer.state = Legacy`; (b) saklı `Crm` satırı, yeniden izlemede sevkiyat `Unavailable` → anahtar A kalır;
  (c) eski → eski (DOC → shipTo) bugünkü gibi güncellenir. Test `RecallCrmReconcileTests.*`. Sabotaj S-6: `Reconcile`'da
  `s.CustomerRef = t.CustomerRef` satırını koşulsuz bırak (bugünkü `:141`) → (a) ve (b) kırmızı.
- [ ] AC-7 (zorunlu) — Çözüldü: CRM 200 `{accountId: A, code, name, status: "inactive", isBillable: false, contractVersion: "crm-account-lookup/1.0"}`
  → satır `customer = {source: Crm, state: Resolved, name, code}`, `label` yok. Test `RecallCustomerDirectoryTests.Resolved_ignores_billability`.
  Sabotaj S-7: çözümü `isBillable = true` koşuluna bağla → `Unresolved`, kırmızı.
- [ ] AC-8 (zorunlu) — Çözülemedi, her biri ayrı fixture: 400, 401, 403, 500, 503, zaman aşımı, ulaşılamıyor, `accountId` farklı, `name` boş,
  `code` yok, `contractVersion = crm-account-lookup/2.0`, ayrıştırılamayan gövde → `state = Unresolved`, `name` / `code` yok. Test
  `RecallCustomerDirectoryTests.Unresolved_*`. Sabotaj S-8a: 403'ü `NotFound` say → kırmızı; S-8b: `accountId` karşılaştırmasını kaldır → kırmızı.
- [ ] AC-9 (zorunlu) — Bulunamadı: CRM 404 → `state = NotFound`. Test `RecallCustomerDirectoryTests.NotFound_is_404_only`. Sabotaj S-9:
  404'ü `Unresolved`'a eşle → kırmızı.
- [ ] AC-10 (zorunlu) — GUID gösterilmez (sunucu): A'lı vakada `getCaseCustomers`, `getCaseEffectiveness`, `getCaseScope`, `runTrace`
  yanıtlarında A metni yalnız `customerRef` adlı anahtar alanlarında geçer — `label` ve `customer.*` alanlarında
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
- [ ] AC-16 (zorunlu) — RECALL 1.10.0: `recall.openapi.yaml` `version: 1.10.0`, değişiklik notu, `CustomerDisplay` şeması ve beş isteğe bağlı
  `customer` alanı; AC-1…AC-13 yanıtları şemaya karşı doğrulanır (`ContractSchemaValidator`); mevcut `RecallContract1x0Tests` sınıfları
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

**DL-2**

- [ ] AC-20 (zorunlu) — Müşteriler sekmesi: `Resolved` satır "{ad} · {kod}", altında anahtar satırı yok; `Unresolved` "Çözülemedi",
  `NotFound` "Bulunamadı"; sayfanın metin içeriğinde ve `title` özniteliklerinde A geçmez (`data-notify` / `data-consume` değerinde
  geçebilir). Test `recall-fu01.test.mjs` "customers tab never prints the account id". Sabotaj S-20: `case-detail.js:248`'deki tek aralıklı
  `customerRef` alt satırını CRM satırında bırak → kırmızı.
- [ ] AC-21 (zorunlu) — Diğer görünümler: kapsam sekmesi, iade bağları, etkinlik tablosu, üç pencere ve seçim listesinin `option` metni,
  iz ekranı — A metin olarak geçmez, ad + kod ya da durum metni görünür. Test `recall-fu01.test.mjs` "no view prints the account id".
  Sabotaj (her biri ayrı): S-21a `trace-view.js:32` `n.customerRef`'e geri dönsün → kırmızı; S-21b etkinlik tablosu (`:287`) `r.customerRef`
  → kırmızı.
- [ ] AC-22 (zorunlu) — Eski kayıt rozeti: AC-6 (a) verisiyle ekran iki satır çizer; eski satırda "Eski kayıt" rozeti ve araç ipucu, CRM
  satırında rozet yok; satırlar ad eşleşse de birleşmez. Test `recall-fu01.test.mjs` "legacy row stays separate with badge".
  Sabotaj S-22: satırları `customer.name`'e göre birleştir → kırmızı.
- [ ] AC-23 (zorunlu) — Sabit metin yok: dil `ar` iken üç durum metni ve rozet `Recall.ar.resx` değerleriyle çizilir. Test
  `recall-fu01.test.mjs` "texts come from l10n". Sabotaj S-23: `case-detail.js`'e "Çözülemedi" sabit yaz → kırmızı.

## 17. Test Expectations

- Build: InventoryService ve Web çözümü uyarı politikası değişmeden derlenir.
- **Tam paket:** servis dokunulduğu için kabul koşusu filtresiz Inventory test paketi + `Diten.Web.Tests` (CT kuralı); temel kırmızılar aynı
  gün `main` ile karşılaştırılır. Inventory testleri kendi Mongo replica set'ini başlatır (27017 kullanılmaz).
- Sözleşme: RECALL 1.10.0 şema doğrulaması (AC-16); SHIPMENT 3.5.0 hizası (AC-17, kapı).
- İzolasyon: AC-11; mevcut `RecallTenantBoundaryTests` değiştirilmeden yeşil.
- Denetim: yeni olay yok; mevcut `recall.customer.notified` / `recall.consumption.declared` / iade bağlama olaylarının ayrıntısı CRM
  anahtarında kanonik GUID taşır (AC-4 içinde ölçülür).
- Smoke (E5, FU01 kapanışı — DL kabulü değil): MOD-0149 DL-1 ve MVP-6 3.5.0 yayındayken bir vakada müşteri adı ve kodu görünür, GUID görünmez.

### 17.1 Sabotaj tablosu

Her AC'nin sabotajı metnindedir (S-1…S-23, alt harfli olanlar ayrı koşu). Sabotajı uygulanamayan ölçüt yoktur. AC-17 tasarım gereği
D-1 kapanana dek kırmızıdır; sabotajı (S-17) 3.5.0 FROZEN olduktan sonra koşulur.

## 18. Ready-for-dev Checklist

- [x] Golden Reference / Backend File Convention ebeveyn düzeniyle (§10)
- [x] Frontmatter zorunlu alanlar dolu; ekranlar ebeveynin üç sayfası (§9)
- [x] Validation Rules (§12), Failure Path ≥ 4 (§13), Authorization (§14), Gateway kararı (§15)
- [x] AC'lerin hepsi üretim koduna karşı ve adlı sabotajlı (§16, 23 AC)
- [x] Audited Events (§21) — yeni yazma komutu yok
- [x] Teslim dilimleri (§22) ve sayfa yardımı kararı (§23)
- [ ] Q-1 (çağrı yolu) — CRM yanıtı; Q-2 (alan adı / tipi) — MVP-6 teyidi; CT-1, CT-2 kararları
- [ ] CT-APR-02 bağımsız inceleme (yazandan ayrı oturum) + CT-APR-01 onay satırı (yüksek risk)
- [ ] Registry satırı (CT)
- [ ] `status: ready-for-dev` — yukarıdakiler kapanınca

## 19. Implementation Notes

### 19.1 Kimlik ve keşif kanıtı

- Ön kontrol: çıkış 0 (yukarıdaki künye notu). Araç `verify_module_id.py` sha256 `ae87a566…b51a`; depo HEAD `8b5205226`; origin/main `66864f1ab`
  (son fetch 2026-11-02T21:18, uzak durum doğrulanmadı).
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
| Q-5 | Hiç müşteri kaydı (bildirim / beyan / iade bağı) olmayan vakada yeniden izleme eski anahtarı CRM'e yükseltebilir mi? Öneri: **hayır** (SB-19 tek kural, §8.5) | Beste (sahip) | TBD |
| Q-6 | Elle eklenen Shipment kapsam satırında CRM hesabı seçilebilsin mi? Öneri: bu FU'da hayır (serbest metin, `Manual`, eski kayıt rozeti) | Beste (sahip) | TBD |

### 19.5 Verilen kararlar (sahip — Beste / MVP-1 CT, 2026-11-03)

1. `customerRef` okuma ve sıra (customerRef → shipToReference → DOC), harf duyarsız karşılaştırma — §8.2, §8.3.
2. Ad ve kod CRM-ACCOUNT-LOOKUP 1.0.0 U-1'den; zaman aşımı ve önbellek mevcut okuyucu deseniyle (okuma başına 2 sn, önbelleksiz) — §8.6.
3. GUID gösterilmez; "Çözülemedi" / "Bulunamadı" — §8.8.
4. **SB-19:** eski anahtarlı satırlar yeni kimliğe otomatik bağlanmaz; "Eski kayıt" rozetiyle ayrı satır — **kabul edilen sınır** (§8.5).
5. Kayıtlar yeni anahtarı kabul eder; eski kayıtlar değişmez — §8.4.
6. MOD-0177 CRM tüketicisi olarak yazılır (sözleşme CRM'in); izin ataması Auth işi — §7 D-4, D-5.
7. Yeni metinler 7 dilde — §11.

### 19.6 CT kararları (öneri, MVP-1 CT — TBD)

| # | Karar | Öneri |
|---|---|---|
| CT-1 | `CustomerRefSource` rs-scope-v1 mührüne girer mi? | **Hayır** — biçim adı ve FU01 öncesi mühürler değişmez (§8.5, AC-15) |
| CT-2 | Sözleşme numarası | RECALL **1.10.0** (1.9.0'dan sonra uçuşta başka numara yok — onay öncesi yeniden ölçülür) |

## 20. Follow-up Items

| ID | Konu | Sahip |
|---|---|---|
| O-1 | **Gözlem (ebeveyn, bu FU'nun kapsamı dışı):** bugün yeniden izlemede MVP-6 o an erişilemezse saklı shipTo anahtarı `DOC:` anahtarına düşer (`RecallTraceService.cs:141`); o müşterinin kayıtları yetim kalır. FU01 yalnız CRM anahtarını korur (§8.5). Eski → eski düşüşün düzeltilmesi ayrı karar | MOD-0177 / CT |
| FU-0177-CUST-01 | Ebeveyn §20 satırı bu paketle karşılanır (MVP-6 party id yerine CRM `Account.Id` + U-1) — ebeveyn satırının güncellenmesi CT adımı | CT |
| FU-0177-FU01-SEL | Elle eklenen kapsam satırında CRM müşteri seçici (Q-6 "evet" olursa) | MOD-0177 |
| FU-0177-FU01-REBIND | Eski anahtarlı kayıtların CRM kimliğine **elle**, denetimli bağlanması (SB-19'un otomatik yasağı korunarak) — ürün kararı | Ürün / CT |

## 21. Audited Events

Yeni yazma komutu ve yeni denetim olayı yok. Mevcut olaylar (ebeveyn §8.12): `recall.customer.notified`, `recall.consumption.declared`,
iade bağlama — ayrıntıdaki `customerRef` CRM anahtarında kanonik GUID'dir (kayıtta kalır). CRM okuması AUD-001 okuma olayı üretmez
(CRM taslağı §1).

## 22. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | arka uç: customerRef okuma + anahtar sırası + ad çözümü + kayıtlar + RECALL 1.10.0 + 7 dil metinleri | Owned Objects, Entity Fields, Repo Scope, Dependencies, Müşteri anahtarı sırası, Büyük/küçük harf ve anahtar biçimi, Kayıtlar, Yeniden izlemede anahtar kararlılığı (SB-19), Ad çözümü (CRM-ACCOUNT-LOOKUP U-1), Sözleşme — RECALL 1.10.0, Gösterim kuralı, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Audited Events | E4 | plan kimliği yok | MVP-1 |
| DL-2 | ekran: ad + kod, durum metinleri, eski kayıt rozeti | Gösterim kuralı, Frontend File Contract, Layout & Shell Contract, Page Help (UXH-001) | E4 | plan kimliği yok | MVP-1 |

Kabul ölçütleri: DL-1 → AC-1…AC-19; DL-2 → AC-20…AC-23. **DL-1 kabul kapısı:** D-1 (SHIPMENT 3.5.0 FROZEN, AC-17 yeşil) ve D-2 (CRM U-1
şekli teyitli ya da sözleşme FROZEN). D-1 / D-2 kapanmadan DL-1 ikizlerle kodlanabilir, kabul edilemez. DL-2 DL-1'e dayanır. FU01'in
kapanışı ayrıca E5 smoke ister (§17; MOD-0149 DL-1 ve MVP-6 3.5.0 yayında). 7 dil metinleri parite bekçisi gereği DL-1'dedir (§11);
"Gösterim kuralı" iki dilime bölünür: DL-1 sunucu çıktıları (API, CSV, PDF), DL-2 ekran.

## 23. Page Help (UXH-001)

Yeni sayfa yok. Vaka detayı "?" panelindeki müşteriler adımı (`PageHelpResource`, 7 dil) "Eski kayıt" rozetini bir cümleyle anlatır
(DL-2; yeni tur yok).
