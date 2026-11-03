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
risk_reason: "GxP geri çağırma kaydı: müşteri anahtarı bildirim, tüketim beyanı, iade bağlama ve etkinlik hesabının birleştirme anahtarıdır — yanlış anahtar kaydı yetim bırakır ve etkinliği yanlış gösterir; servisler arası okuma (CRM 5061) ile kiracı izolasyonu ve kişisel veri sınırı; kapsam mührü (rs-scope-v1) customerRef'i içerir; kanıt paketinin saklı baytları müşteri adını dışa aktarım anının anlık görüntüsü olarak taşır; sözleşme değişikliği (RECALL 1.10.0) ve dondurulmamış iki üretici sözleşmesine bağımlılık (SHIPMENT-BUNDLE 3.5.0, CRM-ACCOUNT-LOOKUP 1.0.0)"
status_note: "draft r3 (2026-11-03) — bağımsız inceleme 01 tur 1'in (a66dd353, UYGUN DEĞİL) 14 bulgusu tek geçişte kapatıldı (S-101, §19.7): F-1 sahip kararı (Beste, 2026-11-03) — çözülemeyen CRM müşterisinin satırında teslim yeri metni + sevkiyat hareket numarası görünür, ad çözülene kadar bildirim / tüketim beyanı / iade bağlama ekranda kapalı ve sunucuda 503 RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE (§8.4, AC-25, AC-27); CT kararları (MVP-1 CT, 2026-11-03) — kanıt dışa aktarımı adı anlık görüntü olarak taşır, çözülemeyen satır işaretli girer (§8.10, AC-26); D-3 (Q-1) ve D-4 (Q-4) DL-1 kabul kapısı (§7, §22); Q-5 'eski' tanımı FU01 öncesi anahtarlara daraltıldı (§8.5). Kimlik ön kontrolü 8ba30d12b'de çıkış 0 (değişmedi). Kod yazılmaz: CT-APR-02 yeniden inceleme + CT-APR-01 onay satırı gerekir. Ready-for-dev önkoşulu sahip yanıtı: Q-7 (Bulunamadı satırında komutlar), Q-8 (Q-5 daraltmasının teyidi) (§19.4). Açık: Q-1…Q-4, D-1…D-5; DL-1 kabul kapısı D-1, D-2, D-3, D-4 ve AC-20 (f) veri sayımı (§22). 27 AC (DL-1 AC-1…AC-20, AC-25, AC-26; DL-2 AC-21…AC-24, AC-27)."
owner: supply-chain-execution / mvp-1 control-tower
branch: feature/sce/mod-0173-inventory-ledger-valuation
started: 2026-11-03
target: TBD
form_field_count: 0
dependencies: [MOD-0177, RECALL-v1.9.0 (→ 1.10.0, bu paket), SHIPMENT-BUNDLE-v3.5.0 (MVP-6, customerRef — kabul kapısı; bugün 3.4.0), CRM-ACCOUNT-LOOKUP-v1.0.0 (MOD-0149, U-1 — taslak; uç kodda yok, MOD-0149 DL-1), MOD-0018 (Auth — crm.account.lookup-validation rol eşlemesi; DL-1 kabul kapısı, D-4)]
---

# MOD-0177-FU01 — Recall customer identity from CRM

> **Kimlik (DCP-002) — ölçüldü 2026-11-03, worktree `wms-ct-01`, dal `chore/ct/mod0178-dl1-accept-01` @ `8b5205226`:**
> `python3 -P -X utf8 .antigravity/scripts/verify_module_id.py . --check-id MOD-0177-FU01 --name "Recall customer identity from CRM" --parent MOD-0177`
> → çıkış **0** ("OK  MOD-0177-FU01: proven against Blueprint/registry"). Mükerrer keşfi `--discover "recall customer crm"` → çıkış **3**
> ("inceleme gerekli"; rapor özeti `d6262ba4…2bd2b15`): blueprint 0 · sor 0 · registry 0 · paket 0 · görev 0 · **gateway 2**. İki bulgunun
> kararı ve gerekçesi §19.1'de (üretici-tüketici ilişkisi ve ebeveynin kendi rotası; mükerrer değil — **CT kabul etti, 2026-11-03**).
> **Registry satırı CT tarafından eklendi** (`execution/registries/module-id-registry.md`, commit `8ba30d12b`, durum `draft`). Ön kontrol
> registry satırıyla `8ba30d12b`'de yeniden koşuldu → çıkış **0** (araç sha256 aynı, `ae87a566…b51a`). r3 kimliği değiştirmez.
>
> **Yetki:** bu paket > `execution/domains/supply-chain-execution/domain-config.md` > `AGENTS.md` > `.antigravity/`. Ebeveyn
> `MOD-0177-recall-readiness.md` §8.6 "Customer identity" (`:611-613`) ve ASSUMPTION-0177-07 (`:1183`) bu paketin **tabanıdır**; bu paket
> FU-0177-CUST-01'i (`:1588`, "Customer party id / names from MVP-6") **CRM kimliğiyle** karşılar ve tabanı genişletir: anahtar sırasının başına
> `customerRef` eklenir, sonraki iki adım (shipToReference, `DOC:`) **aynen** kalır. Görevde "§8.4 müşteri kimliği, satır ~611" diye anılan yer
> ebeveynde **§8.6**'dır (§8.4 KR-B otomatik karantina isteğidir) — satır numarası doğru, bölüm numarası düzeltildi.
>
> **Ölçüm tabanı:** `services/Diten.InventoryService`, `frontend/Diten.Web`, `docs/analysis/contracts/recall.openapi.yaml`,
> `shipment-bundle.openapi.yaml` ve CRM taslağı `8b5205226`'da okundu; bu yollar uygulama dalı `feature/sce/mod-0173-inventory-ledger-valuation`
> @ `372082aa0` ile **aynıdır** (`git diff --stat 372082aa0 8b5205226 -- <yollar>` boş). r2 ölçümleri `8ba30d12b`'dendir. **r3 ölçümleri
> `97cf7d170`'dendir**; `git diff --stat 8ba30d12b 97cf7d170 -- services/Diten.InventoryService frontend/Diten.Web docs/analysis/contracts
> services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization` boş — satır numaraları üç tabanda aynıdır.

## 1. Module Summary

Recall'da müşteri bugün opak bir metinle tanınır: sevkiyat okunduysa MVP-6'nın `shipToReference` değeri, okunamadıysa ya da belge MOD-0183
dışındaysa `"DOC:" + sourceDocumentId` (`RecallTraceEngine.cs:331`, `:375`; `RecallTraceService.cs:93`). Ekranda bu metin ya da belge numarası
görünür; ad yoktur (ebeveyn §8.6: "No customer master link in MVP-1").

Bu FU üç şey yapar:

1. **Anahtar (DL-1):** SHIPMENT-BUNDLE 3.5.0'da MVP-6'nın ekleyeceği `customerRef` (MOD-0149 `Account.Id`) doluysa müşteri anahtarı odur;
   boşsa bugünkü sıra korunur. CRM anahtarı kanonik küçük harf GUID ("D") olarak saklanır; giriş büyük/küçük harf duyarsız eşleşir.
2. **Ad (DL-1):** CRM anahtarlı müşterinin adı ve kodu okuma anında CRM-ACCOUNT-LOOKUP 1.0.0 U-1'den çözülür (önbelleksiz, istek başına
   toplam 2 sn bütçe). Çözülemezse "Çözülemedi", hesap yoksa / silinmişse / başka kiracıdaysa "Bulunamadı"; bu iki durumda satır, sevkiyatın
   teslim yeri metni ve sevkiyat hareket numarasıyla ayırt edilir. **GUID hiçbir zaman gösterilmez** — kayıtta ve API'nin anahtar alanında kalır.
   **Ad çözülemeyen ("Çözülemedi") müşteriye bildirim, tüketim beyanı ve iade bağlama yapılamaz** — ekranda kapalı, sunucuda 503 (sahip kararı, F-1).
3. **Ekran (DL-2):** müşteri görünen her yerde ad + kod; FU01 öncesi anahtarlı satırlar "Eski kayıt", FU01 sonrası CRM kimliksiz satırlar
   "CRM kimliği yok" rozetiyle ayrı satır (SB-19).

Bildirim, tüketim beyanı ve iade bağlama yeni anahtarı kabul eder; eski kayıtlar değişmez (yeniden yazma, göç, otomatik bağlama yok).
FU01 öncesi yazılmış anahtar hiçbir vakada CRM kimliğine yükseltilmez — vakada hiç müşteri kaydı olmasa da (Q-5 = HAYIR; kapsamı §8.5).
Elle eklenen kapsam satırında CRM müşterisi seçilemez; GUID biçimli serbest metin reddedilir (Q-6 = HAYIR). Yeni izin anahtarı ve yeni Gateway
rotası yoktur; yeni hata kodu bir tanedir (`RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE`, 503).

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
- **Ad ve kod hiçbir kayıt alanında saklanmaz** (kapsam satırı, bildirim, beyan, iade bağı, vaka olayı, denetim satırı, iz anlık görüntüsü,
  tatbikat kaydı, mühür), önbelleğe alınmaz; yanıt anında çözülür. **Tek istisna kanıt paketi dışa aktarımının saklı baytlarıdır**
  (CT kararı, MVP-1 CT, 2026-11-03): `createEvidenceExport` üretilen baytları hash'leyip saklar (`EvidenceHandlers.cs:63-156`,
  `RecallEvidenceEntities.cs:46` `Sha256`, `:69` `Content`); dosya müşteri adını ve kodunu **dışa aktarım anının anlık görüntüsü** olarak taşır,
  sonradan değişmez (§8.10). Bu bir kayıt alanı değil, GxP kanıt dosyasının içeriğidir; dışa aktarım günlüğü satırının alanlarına ad girmez.

## 3. Owned Objects

Yeni koleksiyon yok. Değişen nesneler:

| Nesne | Koleksiyon | Değişiklik |
|---|---|---|
| `RecallScopeItem` | `recall_scope_items` | eklenen alan `CustomerRefSource` (§4.1) |
| `TraceNode` (bellek içi) / `TraceShipmentInfo` | — | eklenen alanlar (§4.2) |
| `RecallCustomerNotification`, `RecallConsumptionDeclaration`, `RecallReturnLink` | mevcut | **alan eklenmez**; `CustomerRef` CRM anahtarında kanonik GUID taşır |
| `RecallEvidenceExport` / `RecallEvidenceFile` | mevcut | **alan eklenmez**; saklı baytlar adı anlık görüntü olarak taşıyabilir (§2, §8.10) |
| `RecallErrorCodes` (`RecallRules.cs:11`) | — | eklenen kod `RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE` (503, §8.4) |
| RECALL yanıt şemaları | `recall.openapi.yaml` | eklemeli `customer` nesnesi, `DrillRun.customers` adlandırması, yeni hata kodu (§8.7) |

## 4. Entity Fields

### 4.1 RecallScopeItem eklenen alan

| Alan | Tip | Kural |
|---|---|---|
| `CustomerRefSource` | `RecallCustomerRefSource?` (`Crm`, `ShipTo`, `Document`, `Manual`) | yalnız `Kind = Shipment`'ta dolu. **`null` = FU01'den önce yazılmış satır** ("eski", §8.5) ve **yapışkandır**: `null` satır hiçbir yazımda (yeniden izleme, revizyon kopyası, geri yükleme) kaynak almaz (F-14). Yazıcılar: iz (`RecallTraceService.ScopeItems`, §8.2), elle ekleme (`Manual`, §8.2), yeniden izleme (§8.5), revizyon kopyası (aynen, §8.5). Enum değerleri yalnız sona eklenir. **rs-scope-v1 mührüne girmez** (CT-1 kararı, 2026-11-03; §8.5). Mongo eşlemesi bugünkü sözleşme paketiyle (`InventoryBsonMappings.cs:36-43`: yalnız `EnumRepresentationConvention`, `IgnoreExtraElementsConvention`): FU01 sonrası yeniden yazılan eski satırda alan `null` olarak yazılır; okumada alanın yokluğu ile `null` aynıdır. |

### 4.2 Trace modeli eklenen alanlar

- `TraceShipmentInfo` (`TraceModel.cs:55`) sona eklenen `Guid? CustomerAccountId` — SHIPMENT 200 yanıtındaki `customerRef`'in ayrıştırılmış
  hâli; geçersiz, `Guid.Empty` ya da yoksa `null`.
- `TraceNode` eklenen `RecallCustomerRefSource? CustomerRefSource` — `CustomerRef` (`TraceModel.cs:97`) ile birlikte yazılır.
- rt-v1 izi mührü değişmez: müşteri referansı bugün de mühürde değildir (`TraceRules.cs:108-111`).

## 5. Repo Scope

**DL-1 (arka uç + metinler):**
- `services/Diten.InventoryService/src/Diten.InventoryService.Domain/Recall/Trace/TraceModel.cs`
- `services/Diten.InventoryService/src/Diten.InventoryService.Domain/Recall/Entities/RecallEntities.cs` (`RecallScopeItem`, enum)
- `services/Diten.InventoryService/src/Diten.InventoryService.Domain/Recall/Rules/RecallCustomerKeyRules.cs` (yeni — `IsDShaped`, kanonik biçim, eşleşme, sıra)
- `services/Diten.InventoryService/src/Diten.InventoryService.Domain/Recall/Rules/RecallRules.cs` (yalnız `RecallErrorCodes`'a yeni kod, `:11-53`)
- `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/Recall/Ports/IRecallCustomerDirectory.cs` (yeni port)
- `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/Recall/Trace/RecallTraceEngine.cs`, `RecallTraceService.cs`
- `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/Recall/Handlers/CaseDetailReadHandlers.cs`, `RecordHandlers.cs`,
  `R3Handlers.cs` (yalnız `LinkRecallReturnHandler` ve `ReviseRecallScopeHandler` kopyası `:527-533`),
  `CaseCommandHandlers.cs` (yalnız `AddRecallScopeItemsHandler` Shipment dalı `:361-382` ve satır kurulumu `:403-427` — Q-6),
  `EvidenceHandlers.cs` (gerekirse yalnız müşteri çözümünün kurucuya geçirilmesi; saklama akışı `:97-156` değişmez)
- `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/Recall/Services/RecallReads.cs`,
  `RecallCustomerDisplay.cs` (yeni — çözüm, gösterim ve ayırıcı eşlemesi)
- `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/Recall/RecallModels.cs`, `RecallContract.cs`
- `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/Recall/Evidence/RecallEvidence.cs`,
  `RecallEvidenceText_{en,tr,fr,es,zh,ar,ru}.resx`
- `services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/Recall/ShipmentBundleHttpReader.cs`,
  `CrmAccountLookupHttpReader.cs` (yeni)
- `services/Diten.InventoryService/src/Diten.InventoryService.Api/Recall/RecallServiceCollectionExtensions.cs` (DI)
- `docs/analysis/contracts/recall.openapi.yaml` (1.9.0 → 1.10.0)
- `frontend/Diten.Web/Resources/Views/Recall/Recall.{en,tr,fr,es,zh,ar,ru}.resx`, `frontend/Diten.Web/Views/Recall/Recall.cs` (anahtar listesi,
  `Recall.Errors.*` dahil) — ortak anahtarlar parite bekçisi gereği DL-1'de (§11)
- Testler: `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Recall/` altında yeni sınıflar (§16 adları) ve
  `ShipmentReaderTests.cs` eki; CRM test ikizi `RecallTestInfrastructure.cs` yanında
- **Sözleşme testleri (F-4):** `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Contract/Fixtures/recall-v1.9.0.openapi.yaml`
  (yeni — dondurulmuş 1.9.0 dosyası, git blob `6cd16ab00:docs/analysis/contracts/recall.openapi.yaml`, LF sha256
  `7d97b13de27e662cc7f2775ca7fac3458d62bb749ff12e0d370fa768a47f930c`); `Recall/RecallContract190Tests.cs` (yalnız "after" dosyası canlı
  sözleşmeden fixture'a çevrilir, `:39` — emsal `RecallContract180Tests.cs:38` `after = RecallContract190Tests.FixtureV180Path`);
  `Recall/RecallRulesAndShapeTests.cs` (yalnız `:196` sürüm satırı `1.10.0` ve 1.10.0 değişiklik notu belirteçleri, `:222` deseninde)

**DL-2 (ekran):**
- `frontend/Diten.Web/wwwroot/assets/js/Recall/case-detail.js`, `trace-view.js`, `recall.l10n.js`
- `frontend/Diten.Web/Resources/PageHelpResource.{en,tr,fr,es,zh,ar,ru}.resx` (yalnız `Help.RECALLCASEDETAILS.Step4`, §23)
- `frontend/Diten.Web.Tests/JavaScript/Node/recall-fu01.test.mjs` (yeni), `frontend/Diten.Web.Tests/Recall/` (gerekirse)

`drill-view.js` değişmez (`customers` alanını bugün çizmiyor, ölçüldü; AC-22 bunu sabitler).

## 6. Protected Paths

`.antigravity/**`, `gateway/Diten.ApiGateway/ocelot.json` (yalnız integration-agent), `_Layout.cshtml`, Archive;
`services/Diten.CrmService/**` (commercial-suite), `services/Diten.SupplyChainService/**` (MVP-6), Auth servisi;
`docs/analysis/contracts/shipment-bundle.openapi.yaml` (MVP-6 tek yazıcı), `docs/analysis/contracts/drafts/crm-account-lookup-validation-v1.0-proposal.md`
(CRM tek yazıcı). MOD-0173 / 0174 / 0175 / 0176 dosyalarına dokunulmaz. Mevcut `RecallContract120Tests` … `RecallContract180Tests`
değişmez (yalnız `RecallContract190Tests` canlı dosyayı okur — ölçüldü: `ContractSchemas.RecallContractPath` yalnız onda geçiyor).

## 7. Dependencies

| # | Bağımlılık | Durum (2026-11-03) | Bu pakete etkisi |
|---|---|---|---|
| D-1 | **SHIPMENT-BUNDLE 3.5.0** — `getShipment` 200 yanıtında isteğe bağlı `customerRef` (MOD-0149 `Account.Id`, GUID "D", küçük harf; yalnız ekleme) | yok; bugün **3.4.0** FROZEN (`shipment-bundle.openapi.yaml:15`), `customerRef` / `partyId` geçmiyor. Alan adı ve tipi **kesin değil — MVP-6 taslağıyla teyit edilecek** (Q-2) | **DL-1 kabul kapısı:** 3.5.0 FROZEN, alan teyitli ve `shipToReference`'ın `"D"` biçimli GUID olmayacağı MVP-6'ca yazılı (Q-2, F-12). Okuyucu alan adını tek sabitte tutar (§8.2); ad değişirse tek satır |
| D-2 | **CRM-ACCOUNT-LOOKUP 1.0.0 U-1** `GET /api/crm/accounts/{accountId:guid}/lookup-validation` | taslak (`drafts/crm-account-lookup-validation-v1.0-proposal.md`); **uç kodda yok** (MOD-0149 DL-1) | DL-1 E4 sözleşme metninden kurulan ikizle; **DL-1 kabul kapısı:** sözleşme FROZEN ya da U-1 şekli (alanlar, 404 kuralı, `contractVersion`) CRM sahibince teyitli (teyit edilen taslağın sha256'sı kabul kaydına). Uçtan uca E5: MOD-0149 DL-1 yayında |
| D-3 | Çağrı yolu: kullanıcı token'ı Gateway üzerinden mi, servis kimliği mi | **CRM'e soruldu — açık** (Q-1) | **DL-1 kabul kapısı (CT kararı, 2026-11-03; F-8).** Kod varsayılan kullanıcı token'ıyla (`ForwardCallerHeadersHandler`, mevcut okuyucu deseni) yazılır ve kabul edilir. Servis kimliği seçilirse bu **yalnız DI değişikliği değildir**: CRM taslağı §1 kiracının başlık, parametre ya da gövdeyle verilmesini yasaklar (servis kimliğinde kiracı token'dan çözülemez) ve CRM izni kullanıcıdan bağımsızlaşır (her recall okuyucusu adı görür; AC-18'in anlamı değişir) — paket revizyonu (yeni AC'ler, CRM sözleşme değişikliği) gerekir, DL-1 kabul edilmez |
| D-4 | `crm.account.lookup-validation` izninin geri çağırma rollerine atanması | Auth işi (MOD-0018, **Ali**) — XMC-001; Q-1 = kullanıcı token'ıysa gerekir. Ölçüldü: varsayılan Görüntüleyici rolü yalnız `read` eylemli anahtarları alır (`DefaultRolePermissionTemplate.cs:207-211`); `lookup-validation` eylemi kendiliğinden verilmez | **DL-1 kabul kapısı (CT kararı, 2026-11-03; F-8):** Q-4 yanıtlı ve kabul ortamında atama yapılmış. Atanmamış kullanıcıda CRM 403 → "Çözülemedi"; okumalar çalışır (AC-18) ama o kullanıcı CRM anahtarlı müşteriye bildirim / beyan / iade bağı **kaydedemez** (503, §8.4) — bu yüzden kapı |
| D-5 | CRM sözleşmesinin tüketici satırı | CRM sahibinin adımı | önerilen satır: `x-consumers: … MOD-0177 Recall Readiness (U-1 — müşteri adı / kodu gösterimi ve kayıt öncesi çözüm denetimi; uygunluk kararı vermez, isBillable kullanmaz)` |
| D-6 | MOD-0177 ebeveyn (RECALL 1.9.0 FROZEN, `recall.openapi.yaml:14`) | uygulandı | 1.10.0 bu paketin tek yazıcı adımı (CT-2 kararı, 2026-11-03); `97cf7d170`'de `recall.openapi.yaml` hâlâ `1.9.0` |

Bağımlılık sırası: D-1…D-4 olmadan DL-1 **kodlanabilir** (ikizlerle, kullanıcı token'ıyla), ama **kabul edilemez**. DL-2 DL-1'e dayanır.

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
| 2 | okuma `Ok`, `shipToReference` dolu ve `RecallCustomerKeyRules.IsDShaped(shipToReference)` **değil** (`:375`, bugünkü + F-12 bekçisi) | `shipToReference` aynen | `ShipTo` |
| 3 | belge var (`:331`, `RecallTraceService.cs:93`, bugünkü) | `"DOC:" + sourceDocumentId` | `Document` |
| — | belge yok | `null` + `NO_CUSTOMER_REFERENCE` boşluğu (bugünkü, `:334-338`) | `null` |

**Okuyucu (`ShipmentBundleHttpReader.cs:66-70`):** `customerRef` alanı (adı tek sabit: `ShipmentBundleHttpReader.CustomerRefField`, Q-2)
string ise `Guid.TryParseExact(value, "D", …)` ile ayrıştırılır (ayrıştırma büyük/küçük harf duyarsızdır); başarısız, `Guid.Empty`, string
olmayan ya da yok → `null` ve sıra 2'ye düşülür. Geçersiz değer günlüğe **değeriyle değil**, `recall.shipment.customer_ref_invalid
{ShipmentId}` olarak yazılır. 401 / 403 / 404 / zaman aşımı yolları bugünkü gibidir (§8.6 ebeveyn tablosu); hiçbirinde `customerRef` okunmaz.

**`"D"` biçimli `shipToReference` bekçisi (F-12):** MVP-6'nın eşleyicisi bugün GUID biçimli teslim yeri üretmez
(`WarehouseShipmentMapper.cs:51` `warehouse-outbound:{…}:ship-to`). Gelecekte üretirse bu değer bir `Crm` anahtarıyla aynı metni taşıyabilir;
bu yüzden sıra 2 böyle bir değeri anahtar yapmaz (sıra 3'e düşülür) ve günlüğe değersiz `recall.shipment.ship_to_guid_shaped {ShipmentId}`
yazılır. Bugünkü veride davranış değişmez (değer hiç `"D"` biçimli değil). Teyidi D-1 / Q-2'dedir.

**Elle eklenen Shipment kapsam satırı** (`addScopeItems`, `AddRecallScopeItemsHandler`, `CaseCommandHandlers.cs:361-382`) — **Q-6 = HAYIR
(sahip, Beste, 2026-11-03): CRM müşterisi seçilemez.** Kurallar:
- `customerRef` bugünkü gibi serbest metindir; satır `CustomerRefSource = Manual` ile yazılır (`:403-427` kurulumuna eklenir).
- `RecallCustomerKeyRules.IsDShaped(customerRef)` doğruysa **istek reddedilir**: 400 `INVALID_REQUEST` (`RecallSupport.Invalid`, kod
  `InventoryErrorCodes.InvalidRequest`; `addScopeItems` `x-error-codes`'unda zaten var). İstek bütündür (doğrulama döngüsü `SaveAsync`'ten önce
  biter, `:432-440`): toplu istekteki tek GUID satırı tüm isteği reddeder — kapsam satırı, vaka olayı ve denetim satırı yazılmaz. **Bu bugünkü
  davranıştan değişikliktir** (bugün her 1–200 karakterlik metin kabul edilir); RECALL 1.10.0 değişiklik notuna ve `addScopeItems` açıklamasına
  "one input restriction" olarak yazılır (§8.7, F-13). Gerekçe: GUID biçimli serbest metin §8.3 kanonik eşleşmesiyle bir `Crm` satırının
  anahtarıyla aynı olur ve okuma gruplamasında (`CaseDetailReadHandlers.cs:75`) CRM müşterisiyle birleşir — bu, elle CRM müşterisi seçmenin
  dolaylı yoludur ve kapanır.
- Yasak yalnız `"D"` biçimine bağlıdır (§8.3 eşleşmesinin tanıdığı tek biçim): `{…}` (B), tiresiz (N) ya da başka metinler serbest metin
  olarak kabul edilir; hiçbiri bir `Crm` anahtarıyla ordinal eşit olamaz.
- Ekranda elle kapsam ekleme formu **yoktur** (ölçüldü: `wwwroot/assets/js/Recall/*.js` içinde `addScopeItems` çağrısı yok); yasak yalnız
  API'dedir, yeni metin anahtarı gerekmez (hata iletisi mevcut `RecallSupport.Invalid` iletileri gibi geliştirici metnidir).
- Yeniden izleme elle satıra dokunmaz (`RecallTraceService.cs:127`, "a hand entry with the same key wins") — aynı belge MVP-6'da `customerRef`
  taşısa da elle satır `Manual` kalır.

Uzunluk sınırı `RecallContract.MaxCustomerRefLength = 200` (`RecallContract.cs:20`) değişmez; GUID 36 karakterdir.

### 8.3 Anahtar biçimi ve tekrar parmak izi

**Tek biçim kuralı (`RecallCustomerKeyRules`, F-5):** `IsDShaped(value)` = `value` kırpılır (`string.Trim`), kırpılmış uzunluk **tam 36**
karakterdir ve `Guid.TryParseExact(kırpılmış, "D", out _)` başarılıdır. `TryCanonical(value, out canonical)` = `IsDShaped` doğruysa ve
değer `Guid.Empty` değilse `canonical = Guid.ToString("D")` (küçük harf). Uzunluk denetimi kuralın **kendi** adımıdır: .NET
`Guid.TryParseExact` baştaki ve sondaki boşluğu kendisi de kırptığından kırpma tek başına sabotajla ölçülemez; ölçülen, uzunluk denetiminin
kırpılmış değere uygulanmasıdır (AC-3 (d), AC-20 (d)). Kural `addScopeItems` reddi (§8.2), komut girişi, parmak izi ve okuyucu dışındaki her
GUID biçim sorusunun tek kaynağıdır.

- **CRM anahtarı** her yerde kanonik biçimde saklanır: `Guid.ToString("D")` (küçük harf, 36 karakter). İki sevkiyat aynı hesabı farklı harfle
  dönerse tek müşteri satırı oluşur.
- **Tekrar parmak izi (F-2) — sıra bugünkü gibidir:** komut işleyicileri parmak izini ve tekrar denetimini vaka ve kapsam okunmadan **önce**
  yapar (`RecordHandlers.cs:32-42`, `:125-136`; `R3Handlers.cs:388-392`) ve bu sıra değişmez (tekrar, vaka sonradan durum değiştirse de 200
  döner — bugünkü tekrar anlamı korunur). Bu yüzden parmak izi kapsama bakamaz: **parmak izinde `customerRef`, `TryCanonical` başarılıysa
  koşulsuz kanonik değerle, değilse kırpılmış ham değerle** yazılır. Böylece `3C9A…` ve `3c9a…` aynı idempotency anahtarıyla aynı istektir.
- **Eşleşme (kapsam okunduktan sonra):** `TryCanonical` başarılıysa ve kapsamda aynı kanonik değerli **`Crm` kaynaklı** satır varsa o satır
  eşleşir ve kayıt kanonik değerle yazılır; yoksa kırpılmış ham değer bugünkü gibi ordinal (harfe duyarlı) eşleşir ve kayıt ham değerle
  yazılır. Eşleşmede kaynak koşulu vardır, parmak izinde yoktur.
- **Eski anahtarlar** (`ShipTo`, `Document`, `Manual`, `null`) ordinal eşleşir. `"D"` biçimli **olmayan** eski anahtarın parmak izi değişmez
  (AC-5 (c)). **`"D"` biçimli eski anahtarın parmak izi değişebilir** (AC-5 (c2)): FU01 öncesi kodun ham büyük harfli girişle hesapladığı parmak
  izi, FU01'de kanonik küçük harfli değerle hesaplanır; aynı anahtarla yinelenen eski istek 200 yerine 400 `IDEMPOTENCY_KEY_REUSED` alır.
  Küçük harfli `"D"` biçimli eski anahtarda parmak izi aynıdır. Bu durumun gerçek veride olmadığı AC-20 (f) sayımıyla kanıtlanır: kayıtlar
  yalnız kapsamdaki bir anahtarla yazılır (`RecordHandlers.cs:58`, `:155`; `R3Handlers.cs:417`), sayım her revizyonun ve silinmiş satırların
  `"D"` biçimli eski anahtarlarını sayar; sayım 0 ise böyle bir kayıt ve tekrar vektörü de yoktur.
- Okuma tarafındaki gruplama (`CaseDetailReadHandlers.cs:75`, `RecallEvidence.cs:283`, `RecallReads.cs:80`, `:137`, etkinlik kuralı
  `RecallRules.cs:342-343`) ordinal kalır: CRM anahtarları kanonik saklandığı için ordinal eşitlik harf duyarsız eşitliğe denktir.
- **GUID biçimli eski anahtar:** FU01'den sonra elle girilen anahtar GUID biçimli olamaz (Q-6, §8.2); `DOC:` anahtarı GUID değildir;
  `"D"` biçimli `shipToReference` anahtar yapılmaz (§8.2 bekçisi). Kalan risk: FU01 öncesi elle girilmiş (ya da MVP-6'dan gelmiş) `"D"`
  biçimli bir eski anahtar aynı vakadaki bir `Crm` satırıyla çakışabilir (gruplamada birleşir; eşleşme onu CRM müşterisine bağlar — SB-19'a
  aykırı) ve parmak izi değişir (yukarıda). Büyüklüğü AC-20 (f) sayımıyla ölçülür; sıfır değilse DL-1 kabulü durur ve CT kararı alınır
  (gözlem O-2, §20). Göç yapılmaz.

### 8.4 Kayıtlar

| Komut | Bugün | FU01 |
|---|---|---|
| `recordNotification` (`RecordHandlers.cs:32-58`) | ordinal eşleşme `s.CustomerRef == customer` (`:58`) | §8.3 eşleşmesi; kayıt kanonik anahtarla; çözüm denetimi (aşağıda) |
| `recordConsumption` (`RecordHandlers.cs:125-170`) | `:155`, `:163`, `:169-170` ordinal | §8.3 eşleşmesi; aynı müşteri / SKU toplamları kanonik anahtarla; çözüm denetimi |
| `linkReturn` (`R3Handlers.cs:381-418`) | `ToHashSet(StringComparer.Ordinal)` (`:417`) | §8.3 eşleşmesi; `customers` kümesi kaynağa göre kurulur; çözüm denetimi |

Kapsamda olmayan anahtar bugünkü gibi 404 `RECALL_UNKNOWN_CUSTOMER` (`RecordHandlers.cs:103-105`), yazım 0. **Eski kayıtlar değişmez:**
göç yok, açılışta düzeltme yok, okuma yolunda yeniden yazma yok. Yeni kayıt alanı yok.

**Çözüm denetimi — ad çözülmeden yazım yok (sahip kararı, Beste, 2026-11-03; F-1).** Ekran bu üç işlemi ad çözülene kadar kapatır (§8.8);
sunucu da reddeder — ekran tek başına bekçi değildir:
- **Sıra:** kırpma → parmak izi (§8.3) → tekrar denetimi (bugünkü yer) → mevcut doğrulamalar → vaka ve durum → kapsam eşleşmesi →
  **eşleşen satır `Crm` kaynaklıysa** `IRecallCustomerDirectory.ResolveAsync([hesap])` (§8.6 bütçesiyle) → `Unresolved` ise **503
  `RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE`**, yazım 0 (kayıt, vaka olayı, denetim satırı yok). `Resolved` → bugünkü akış. `NotFound` → bugünkü
  akış (Q-7 varsayımı; §19.4). Eşleşen satır `Crm` değilse CRM çağrılmaz (eski anahtarın davranışı ve süresi değişmez).
- Tekrar denetimi çözüm denetiminden **önce** kalır: daha önce başarıyla yazılmış bir isteğin tekrarı CRM kapalıyken de 200 ve aynı kaydı döner
  (ilk çağrı denetimi geçmiştir; tekrar yeni yazım yapmaz).
- **Kod seçimi — 503, 409 değil:** neden kaynağın durumu değil, bağımlı servisin şu anki yanıtıdır; istemci isteği değiştirerek çözemez, yalnız
  sonra yeniden dener ("CRM'e ulaşılamıyor, sonra tekrar deneyin"). Depodaki emsal bağımlılık kesintisi kodları da 503'tür
  (`QUARANTINE_WORKFLOW_UNAVAILABLE`, `LEGAL_ENTITY_MASTER_UNAVAILABLE` — `InventoryErrorCodes.cs:42`, `QuarantineContract.cs:76`). 409 bu
  sözleşmede kaynak çakışmasına ayrılmıştır (`RECALL_CONFLICT`, `RECALL_RETURN_ALREADY_LINKED`). Yanıt ayrıntısı `customerRef` taşımaz
  (GUID istemciye hata gövdesinde de dönmez; istemci gönderdiği değeri zaten bilir).
- `Unresolved`, CRM 401 / 403'ü de kapsar (§8.6): izni atanmamış kullanıcı CRM anahtarlı müşteriye kayıt yapamaz — D-4 bu yüzden DL-1 kabul
  kapısıdır (§7).

### 8.5 Yeniden izlemede anahtar kararlılığı (SB-19)

`submitScope` yeniden izler ve `RecallTraceService.Reconcile` saklı kapsam satırının anahtarını **koşulsuz** üzerine yazar
(`RecallTraceService.cs:141` `s.CustomerRef = t.CustomerRef`; tek çağıran `CaseCommandHandlers.cs:553`). `submitScope` yalnız `Draft`'ta
koşar (`RecallCaseRules.CanSubmit`, `RecallRules.cs:173`), müşteri kayıtları yalnız `Active` / `EffectivenessCheck`'te yazılır
(`AcceptsRecords` = `IsUnderRecall`, `RecallRules.cs:169`, `:197` — `RecordHandlers.cs:49`, `:147`; iade bağı `R3Handlers.cs:396`; geri dönüş
yalnız `PendingScopeApproval → Draft`, `:175`): yeniden izlenen vakada müşteri kaydı **hiç yoktur**.

**Q-5 = HAYIR (sahip, Beste, 2026-11-03):** hiç müşteri kaydı (bildirim / beyan / iade bağı) olmayan vakada da **eski** anahtar CRM kimliğine
**yükseltilmez**; kural kayıt varlığına bakmaz.

**"Eski" tanımı — yalnız FU01 öncesi anahtarlar (CT yönlendirmesi, MVP-1 CT, 2026-11-03; F-7; sahip teyidi Q-8):** eski = `CustomerRefSource
= null` olan satır (FU01'den önce yazılmış, §4.1). FU01'den **sonra** yazılan `ShipTo` / `Document` satırı eski değildir. Gerekçe: böyle bir
satır, iz anında CRM kimliği alınamadığı için (MVP-6 3.4.0 ya da o anki okuma hatası — AC-2 (g)) CRM dışı kalmıştır; "CRM kimliğinden önce
kaydedilmiş" değildir. `Draft` vakada kayıt olmadığından (yukarıdaki kanıt) bu satırın bir sonraki izde `Crm`'e geçmesi hiçbir kaydı bağlamaz;
SB-19'un koruduğu şey (FU01 öncesi kayıtların yeni kimliğe otomatik bağlanmaması) dokunulmadan kalır. Daraltma olmasaydı beş dakikalık bir
MVP-6 kesintisi vakanın müşterisini kalıcı olarak CRM dışı bırakırdı ve satır olgusal olarak yanlış "CRM kimliğinden önce kaydedildi"
ipucunu taşırdı. `Active` vakada (kayıtlar olabilir) hiçbir satır yükseltilmez: revizyon kopyası aynen taşır (aşağıda).

| Saklı satırın kaynağı | Yeni izin kaynağı | Sonuç |
|---|---|---|
| `null` (FU01 öncesi; silinmiş satır izle geri gelse de, `:128-136`) | `Crm` | **saklı anahtar ve `null` kaynak kalır** — eski satır yeni kimliğe otomatik bağlanmaz (SB-19), vakada kayıt olmasa da (Q-5) |
| `null` | `ShipTo` / `Document` | anahtar bugünkü gibi yeni izin anahtarını alır (`:141`; O-1); **kaynak `null` kalır** (yapışkan, §4.1) — anahtar aynı kalsa da değişse de (F-14) |
| `ShipTo` / `Document` (FU01 sonrası) | `Crm` | **anahtar ve kaynak yeni izinkini alır** (`Crm`) — F-7; yalnız `Draft`'ta olur |
| `ShipTo` / `Document` (FU01 sonrası) | `ShipTo` / `Document` | bugünkü davranış: anahtar ve kaynak yeni izinkini alır (O-1) |
| `Crm` | `ShipTo` / `Document` (ör. o an MVP-6 erişilemez) | **saklı CRM anahtarı ve kaynağı kalır** — geçici okuma hatası anahtarı düşürmez |
| `Crm` | `Crm`, farklı GUID | **anahtar yeni GUID'i alır, kaynak `Crm` kalır** (F-6); günlük `recall.scope.customer_ref_changed {NodeKey}` (değer yok). Gerekçe: `Draft`'ta korunacak kayıt yoktur; onaya MVP-6'nın güncel müşterisi gider, onaylayan eski müşteriyi görmez |
| `Manual` (silinmiş, izle geri geliyor) | herhangi | satır geri yüklenir (`:128-136`); **anahtarı ve `Manual` kaynağı kalır** (Q-6: elle girilen satır CRM kimliği almaz) |
| silinmemiş `Manual` | herhangi | dokunulmaz (bugünkü, `:127`) |

**Bayt eşitliği (F-14):** `Draft` yeniden izlemede saklı satır bugün de değiştirilip yeniden yazılır (`UpdatedAt` dahil, `:139-143`); FU01 eski
satırda bayt eşitliği beklemez, şunu bekler: anahtar bugünkü kuralla, `CustomerRefSource` `null` (alan `null` yazılır, §4.1). `Active` vakada
yeniden izleme yoktur; AC-5'in bayt eşitliği `Active` vakaların eski belgelerine bağlıdır.

**Revizyon (`reviseScope`, `ReviseRecallScopeHandler`, `R3Handlers.cs:463-560`; `Active`, kayıtlar olabilir):** onaylı satırlar r+1'e
kopyalanır (`:527-533`); kopyaya `CustomerRefSource` **aynen** eklenir (bugün kopya `CustomerRef`'i taşır, yeni alanı taşımazsa `Crm` satırı
`null` = eski olur ve büyük harfli GUID'li kayıt 404 alır). Kopyalanan hiçbir satır revizyonda yükseltilmez — `null` da, FU01 sonrası
`Document` / `ShipTo` da (kayıtlar olabilir, SB-19); yalnız delta düğümleri (`:502-503`) §8.2 sırasıyla anahtar alır.

Yeni sevkiyat düğümleri (ekleme) §8.2 sırasıyla anahtar alır. Sonuç: aynı müşteri bir vakada hem eski anahtarlı (eski sevkiyat) hem CRM
anahtarlı (yeni sevkiyat) iki satır olarak görünebilir — **kabul edilen sınır** (SB-19), ekranda rozetle ayrılır (§8.8).

**Mühür (CT-1 kararı, MVP-1 CT, 2026-11-03):** `CustomerRefSource` rs-scope-v1 kanonik biçimine **yazılmaz** (`RecallRules.cs:282-318`; biçim adı `:243` değişmez);
`customerRef` bugünkü gibi yazılır (`:305`). Gerekçe: FU01 öncesi mühürler bayt eşit yeniden hesaplanır; kaynak bilgisi anahtarın kendisinden
ve iz kaydından türetilen sunucu bilgisidir. Ad ve kod hiçbir mühre girmez.

### 8.6 Ad çözümü (CRM-ACCOUNT-LOOKUP U-1)

**Port:** `IRecallCustomerDirectory.ResolveAsync(IReadOnlyCollection<Guid> accountIds, CancellationToken)` →
`IReadOnlyDictionary<Guid, RecallCustomerName>`; `RecallCustomerName = (Outcome: Resolved | NotFound | Unresolved, Code?, Name?)`. Sözlük
istenen her hesabı içerir (yanıtsız kalan `Unresolved`).

**Bağdaştırıcı `CrmAccountLookupHttpReader`** — `ShipmentBundleHttpReader` deseni (`:19-22`):
- `GET api/crm/accounts/{id:D}/lookup-validation`, Gateway taban adresiyle (`RecallServiceCollectionExtensions.cs:48-56`); Gateway rotası
  `/api/crm/accounts/{everything}` → 5061 mevcut (ocelot `:2979`).
- Kimlik: çağıranın token'ı ve korelasyon kimliği (`ForwardCallerHeadersHandler`, `ForwardCallerHeadersHandler.cs:21-35` — `Authorization` ve
  `X-Correlation-Id`; CRM taslağı §7 korelasyon beklentisini karşılar). Kiracı yalnız token'dan (CRM taslağı §1); kiracı başlığı ve
  `X-Legal-Entity-Id` gönderilmez (SHIPMENT okuyucusunun `:60` şirket başlığı burada yoktur — CRM hesabı kiracı geneldir).
- **Süre (F-3):** `MaxParallel = 8` eşzamanlı, okuma başına `PerReadTimeout = 2 sn` ve **çağrı başına toplam bütçe `TotalBudget = 2 sn`**:
  `ResolveAsync` tek bir bağlı iptal belirteci açar (`CancelAfter(TotalBudget)`); her okuma bu belirteçle ve kendi 2 sn sınırıyla koşar.
  Bütçe dolunca süren okumalar iptal edilir, başlamamış okumalar **hiç başlamaz**; ikisi de `Unresolved` olur. **Kısmi sonuç kuralı:**
  bütçe içinde yanıtlanan hesaplar kendi sonucunu (`Resolved` / `NotFound` / `Unresolved`) taşır, kalanlar `Unresolved`; çağrı hiçbir zaman
  hata fırlatmaz. Böylece N hesapta CRM'in istek süresine katkısı tavan(N/8) × 2 sn değil, **≤ 2 sn + pay**dır.
- **MVP-6 okumasıyla eşzamanlılık:** `getCaseCustomers` CRM anahtarlarını saklı kapsam satırlarından alır (canlı MVP-6 okumasını beklemez);
  CRM çözümü ve MVP-6 sevkiyat okuması (`CaseDetailReadHandlers.cs:47-50`) **aynı anda** başlatılır, sonuç ikisi bitince birleştirilir. FU01'in
  bu uca eklediği süre ≤ 2 sn + paydır; MVP-6 okumasının kendi süresi (ebeveyn kuralı, tavan(N/8) × 2 sn) FU01 kapsamı dışındadır (O-3, §20).
  `runTrace` / `getCaseScopeDiff`'te anahtar canlı izden doğduğu için çözüm izden sonra koşar (iz süresi + ≤ 2 sn).
- İstek içinde tekilleştirme (aynı GUID bir kez), **önbellek yok** (mevcut okuyucu da önbelleksizdir; CRM taslağı §7 "yanıtlar güncel durumdur").

**Sınıflandırma** (CRM taslağı §2, §5, §7 ile hizalı; recall uygunluk kararı vermez, `isBillable` / `status` kullanılmaz — pasif müşteriye
de geri çağırma bildirimi gerekir):

| CRM yanıtı | Sonuç | Gösterim |
|---|---|---|
| 200; `data.accountId` istenen GUID (Guid eşitliği); `code` ve `name` boş değil; `contractVersion` `crm-account-lookup/1.` ile başlar | `Resolved` | ad + kod |
| 404 (yok / silinmiş / başka kiracı — CRM'de üçü aynı gövde) | `NotFound` | "Bulunamadı" + ayırıcı (§8.8) |
| 200 ama yukarıdaki koşullardan biri tutmuyor; gövde ayrıştırılamıyor; 400; 401 / 403 (izin atanmamış dahil); 5xx; zaman aşımı; bütçe doldu; ulaşılamıyor | `Unresolved` | "Çözülemedi" + ayırıcı (§8.8); kayıt komutları kapalı (§8.4) |

Hiçbir sonuç bir **okuma** isteğini başarısız kılmaz: müşteri satırları, etkinlik ve kanıt paketi her durumda döner. Kayıt komutlarının
`Unresolved` reddi §8.4'tedir.

**Günlük:** başarısızlıkta `recall.customer.lookup_unavailable {AccountId}: {Reason}` (Reason = durum kodu, istisna türü ya da `Budget`) —
`Information` düzeyi, mevcut `recall.shipment.read_unavailable` deseni (`ShipmentBundleHttpReader.cs:83`). Yanıt gövdesi, ad ve kod
**hiçbir düzeyde** günlüğe yazılmaz.

### 8.7 Sözleşme — RECALL 1.10.0

**"Additive + one input restriction"** (1.9.0 → 1.10.0; F-13). Değişiklik notunun ilk satırı `1.10.0 — additive + one input restriction
(MOD-0177-FU01` ile başlar (1.x notlarının hepsi "additive" diye başlar — `recall.openapi.yaml:20-27`; bu not farkı adıyla söyler).

```yaml
CustomerDisplay:
  type: object
  required: [source, state]
  properties:
    source: { type: string, enum: [Crm, ShipTo, Document, Manual, Unknown] }   # Unknown = FU01'den önce yazılmış satır (CustomerRefSource null)
    state:  { type: string, enum: [Resolved, Unresolved, NotFound, Legacy, NoCrm] }   # Legacy ⇔ source = Unknown; NoCrm ⇔ source ∈ {ShipTo, Document, Manual}
    name:   { type: string }   # yalnız state = Resolved
    code:   { type: string }   # yalnız state = Resolved
    label:  { type: string }   # yalnız state ∈ {Legacy, NoCrm} — bugünkü etiket (shipTo / belge no / serbest metin)
    shipTo: { type: string }   # yalnız state ∈ {Unresolved, NotFound} — sevkiyatın canlı teslim yeri metni; okunamadıysa ya da "D" biçimliyse yok
    shipmentNumber: { type: string }   # yalnız state ∈ {Unresolved, NotFound} — sevkiyat hareket numarası (ledger TransactionNumber); yoksa yok
```

İsteğe bağlı `customer: CustomerDisplay` eklenir: `CustomerRow` (`:1530`), `EffectivenessRow` (`:1198`, toplam satırında yok), `ReturnLink`
(`:1558`), `ScopeItem` (`:1308`, yalnız `kind = Shipment`), trace düğümü (`:1466`, yalnız `Shipment`). `customerRef` alanları ve tipleri
**değişmez** (anahtar; komutlar onu gönderir). `CustomerRow.label` CRM satırında **yoktur** (bugün `?? g.Key` ile anahtara düşebilir,
`CaseDetailReadHandlers.cs:80-83`). Trace düğümü şeması `getCaseScopeDiff` yanıtında da kullanılır (`ScopeDiff.added`, `:1499-1504`) — ayrı
alan gerekmez. Komut gövdeleri (`:467-470`, `:496-499`, `:548-552`) değişmez; açıklamalarına §8.3 harf kuralı ve §8.4 çözüm denetimi eklenir.

**Yeni hata kodu (F-1):** üst düzey `x-error-codes` kaydına `RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE: 503` (`recall.openapi.yaml:962`
kaydı; `RecallRulesAndShapeTests.cs:285-290` her `RecallErrorCodes` sabitinin kayıtta olmasını ister); `recordNotification` (`:458`),
`recordConsumption` (`:487`) ve `linkReturn` (`:539`) `x-error-codes` listelerine eklenir, 503 yanıtı tanımlanır. Bu kod yalnız `Crm`
kaynaklı satırla eşleşen girişte doğar — FU01 öncesi hiçbir girişin cevabı değişmez.

**`DrillRun.customers` adlandırması (F-10):** tatbikat yanıtı bugün `customers` dizisini `additionalProperties` üzerinden taşır
(`RecallDrillRunDto.Customers`, `DrillModels.cs:56`; dolduran `DrillSupport.cs:56`, kaynak `DrillRunHandlers.cs:284`; şema `:1688-1691`).
1.10.0'da isteğe bağlı olarak adlandırılır: `customers: { type: array, items: { type: string }, description: "customer keys (customerRef
values) of the drill trace — keys, never display text; a CRM key is a GUID" }`. Gösterim istisnası §8.8'dedir.

**Tek girdi daraltması (Q-6):** `addScopeItems` (`:196`) açıklamasına ve değişiklik notuna "one input restriction: a Shipment item whose
trimmed customerRef is a 36-character GUID in the 'D' form is refused with 400 INVALID_REQUEST (no CRM customer is chosen by hand;
MOD-0177-FU01 Q-6)" eklenir; `x-error-codes` değişmez (`INVALID_REQUEST` zaten listede). Şema (`ScopeItemInput`) değişmez.

### 8.8 Gösterim kuralı

- **CRM anahtarlı müşteri (ekran, CSV, PDF):** `Resolved` → "{ad} · {kod}"; `Unresolved` → "Çözülemedi"; `NotFound` → "Bulunamadı".
  `Unresolved` ve `NotFound` satırı **ayırıcıyla** gösterilir (sahip kararı, F-1): `shipTo` ve `shipmentNumber` (§8.7) — ikisi de varsa
  "{teslim yeri} · {sevkiyat no}", biri varsa o, hiçbiri yoksa yalnız durum metni. Ayırıcının kaynağı: `getCaseCustomers`'ta grubun satırlarından
  `NodeKey` sırasıyla ilk boş olmayan canlı `ShipToReference` (`CaseDetailReadHandlers.cs:72`, okuma `Ok` ise) ve ilk boş olmayan hareket
  numarası (`RecallCustomerMovementDto.Number`, `:68`; bugün `DOC:` etiketinin de kaynağı, `:81-82`); trace düğümünde düğümün kendi sevkiyatı
  (`TraceNode.Shipment.ShipToReference`, `Movement.TransactionNumber`); kanıt paketinde §8.10. `SourceDocumentId` (MVP-6 sevkiyat GUID'i)
  **ayırıcı olarak kullanılmaz**. Ayırıcı değerlerinden `IsDShaped` olan gösterilmez.
- **GUID metin, `title`, araç ipucu, CSV / PDF hücresi olarak hiçbir yerde görünmez**; yalnız komut gönderen öğenin değerinde (`data-*` /
  `option.value`), API'nin `customerRef` alanlarında ve `DrillRun.customers` anahtar dizisinde taşınır. **Tatbikat istisnası (F-10):**
  `DrillRun.customers` bir anahtar listesidir, ekranda çizilmez (`drill-view.js` bugün çizmiyor — ölçüldü; AC-22 sabitler) ve kanıt paketine
  girmez (tatbikatta müşteri bölümü "notApplicableDrill", `RecallEvidence.cs:382`). Tatbikatın "CustomerList" adımı yalnız zamanlayıcıyı
  durdurur; liste insan için ayrıca sunulmaz (değişmez).
- **Kayıt komutları (F-1):** `Unresolved` satırda bildirim ve tüketim beyanı düğmeleri **çizilmez** (`case-detail.js:244-245`); yerine
  `Recall.Customer.CrmUnavailable` ("CRM'e ulaşılamıyor, sonra tekrar deneyin") yazılır. İade bağlama seçim listesinde (`:405`) `Unresolved`
  müşteriler **seçenek olmaz**; en az biri dışarıda kaldıysa pencerede `Recall.Customer.CrmUnavailable` görünür; hiç seçenek kalmadıysa alanlar
  ve bağlama düğmesi çizilmez (`:407-409` deseni). Sunucunun 503'ü `Recall.Errors.RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE` ile çizilir.
  `Resolved` ve `NotFound` satırda düğmeler bugünkü gibidir (Q-7 varsayımı).
- **CRM kimliksiz satırlar (rozet ve ipucu, F-7):**
  `Legacy` (kaynak `Unknown`, FU01 öncesi) → bugünkü etiket + "Eski kayıt" rozeti (`Recall.Customer.LegacyBadge`), ipucu
  `Recall.Customer.LegacyHint` ("CRM müşteri kimliğinden önce kaydedildi; yeni kimliğe otomatik bağlanmaz.").
  `NoCrm`, kaynak `ShipTo` / `Document` (FU01 sonrası) → etiket + "CRM kimliği yok" rozeti (`Recall.Customer.NoCrmBadge`), ipucu
  `Recall.Customer.NoCrmHint` ("Sevkiyat okunduğunda CRM müşteri kimliği yoktu ya da okunamadı.").
  `NoCrm`, kaynak `Manual` → etiket + "CRM kimliği yok" rozeti, ipucu `Recall.Customer.ManualHint` ("Elle girildi; CRM müşteri kimliğine
  bağlanmaz.", Q-6). Hiçbiri CRM satırıyla **birleştirilmez**. "GUID gösterilmez" kuralı `Crm` kaynaklı anahtarı bağlar; CRM kimliksiz satırın
  etiketi saklı metnin kendisidir (kullanıcının ya da MVP-6'nın metni, bugünkü gibi) — AC-10 / AC-22 fixture'larında böyle satır A metnini
  taşımaz.
- **CSV / PDF işareti (F-11):** iz dışa aktarımı ve kanıt paketinde CRM kimliksiz satırın hücresi, **vakada en az bir `Crm` satırı varsa**
  "{etiket} (Eski kayıt)" (`Recall.Customer.LegacyMarked`) ya da "{etiket} (CRM kimliği yok)" (`Recall.Customer.NoCrmMarked`) olur. Yalnız
  CRM kimliksiz satırları olan vakada hücre bugünkü gibi saklı anahtardır (işaretsiz). Gerekçe: işaret yalnız iki türün aynı dosyada yan yana
  durduğu durumda ayırt etmeye yarar; FU01 öncesi vakaların kanıt dosyası bugünkü çıktıyla aynı kalır (mevcut `RecallEvidenceTests.cs:144`
  `4_customers` satırında saklı teslim yeri metnini birebir arar).
- Görünen yerler (ölçüldü): vaka detayı kapsam sekmesi (`case-detail.js:135`), kapsam farkı listesi (`:165`, `getCaseScopeDiff`
  `added` düğümleri — bugün `n.customerRef`'i basar), müşteriler sekmesi (`:248` — bugün anahtarı tek aralıklı alt satırda basar), iade bağları
  (`:259`), etkinlik tablosu (`:287`), bildirim / tüketim / iade bağlama pencereleri (`:363`, `:380`, `:405`), iz ekranı (`trace-view.js:32`),
  iz dışa aktarımı (`RecallTraceService.cs:213` "place"), kanıt paketi müşteri, iade ve iz bölümleri (`RecallEvidence.cs:283-295`, `:428-440`).
  Ayırıcı zorunluluğu komut seçilen yerlerde (müşteriler sekmesi, üç pencere, seçim listesi — hepsi `getCaseCustomers`'tan beslenir), iz
  ekranında ve kanıt paketindedir; kapsam, etkinlik ve iade bağı tablolarında satırın kendi belge / hareket sütunu bugün de görünür ve bu
  tablolardan komut verilmez.
- Kişisel veri: tarayıcıya müşteri hakkında yalnız ad ve kod gider; adres, iletişim, durum, faturalanabilirlik gitmez. `shipTo` MVP-6'nın
  bugün de gösterilen teslim yeri metnidir (`CaseCustomerShipment.shipToReference`), yeni kişisel veri değildir.

### 8.9 Durum ve komut tablosu

Yeni durum ve yeni komut yok. Okuma uçları (`getCaseCustomers`, `getCaseEffectiveness`, `getCaseScope`, `getCaseScopeDiff`, `runTrace`,
`exportTrace`, kanıt paketi önizleme / dışa aktarma) `customer` nesnesini ekler; izinleri değişmez.

| Komut | Durum | FU01 etkisi |
|---|---|---|
| `addScopeItems` (Shipment) | `Draft` | `Manual` kaynağı yazılır; `IsDShaped` `customerRef` → 400 `INVALID_REQUEST`, yazım 0 (Q-6) |
| `submitScope` | `Draft` | §8.5 tablosu (Q-5: FU01 öncesi anahtar yükseltilmez; FU01 sonrası `ShipTo` / `Document` `Crm`'e geçebilir; `Crm` → `Crm` güncellenir) |
| `reviseScope` | `Active` | kopya `CustomerRefSource`'u taşır; hiçbir kopya yükseltilmez (§8.5) |
| `recordNotification`, `recordConsumption`, `linkReturn` | `Active` / `EffectivenessCheck` | §8.3 eşleşmesi (§8.4); `Crm` satırı `Unresolved` → 503 `RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE`, yazım 0 |
| `createEvidenceExport` | — | ad anlık görüntüsü; `Unresolved` satır işaretli girer, dışa aktarım reddedilmez (§8.10) |
| diğer komutlar | — | değişmez |

### 8.10 Kanıt paketi ve ad anlık görüntüsü

CT kararı CT-3 (MVP-1 CT, 2026-11-03; F-1).

- `previewEvidence` ve `createEvidenceExport` aynı kurucuyu kullanır (`EvidenceHandlers.cs:56`, `:97-98`); kurucu vakanın `Crm` anahtarlarını
  `IRecallCustomerDirectory` ile **bir kez** çözer (§8.6 bütçesi). Müşteri hücresi (müşteriler bölümü `RecallEvidence.cs:283-288`, iade
  satırı `:295-296`, etkinlik tabloları, iz bölümü "place" `:440-442`) §8.8 metnidir: `Resolved` → "{ad} · {kod}"; `Unresolved` → "Çözülemedi ({ayırıcı})"
  (`Recall.Customer.UnresolvedWith`); `NotFound` → "Bulunamadı ({ayırıcı})" (`Recall.Customer.NotFoundWith`). Kanıt paketinde ayırıcı =
  müşterinin kapsam sevkiyatlarının `NodeKey` sırasıyla ilk hareket numarası (kurucu ledger hareketlerini zaten okur, `:427`); kurucu MVP-6'yı
  okumaz, bu yüzden teslim yeri metni dosyada yoktur. Müşteri bölümüne kimlik sütunu (`customerRef`) **eklenmez**.
- **Anlık görüntü:** `createEvidenceExport` üretilen baytları saklar ve hash'ler (`EvidenceHandlers.cs:101-156`); dosya, dışa aktarım
  anındaki CRM adını taşır, CRM'de ad sonradan değişse de saklı dosya ve hash değişmez (`getEvidenceFile` saklı baytları döner). Vakada en az
  bir `Crm` satırı varsa özet bölümüne bir satır eklenir: alan `customerNames` (`Recall.Ev.W.customerNames`, "Müşteri adları"), değer
  `Recall.Ev.W.customerNamesSnapshot` ("CRM'den, dışa aktarım anında"). Yalnız CRM kimliksiz satırları olan vakada özet ve müşteri bölümü
  bugünkü çıktıyla aynıdır.
- **Kesinti:** dışa aktarım anında çözülemeyen satır dosyaya "Çözülemedi" işaretiyle girer; dışa aktarım **reddedilmez** ve kısmi sonuçla
  üretilir (§8.6). Kullanıcı CRM düzelince yeni bir dışa aktarım alabilir; eski dosya kanıt olarak kalır.
- Dışa aktarım günlüğü satırının ve `recall.evidence.exported` denetim satırının alanları değişmez; ad onlara girmez (§2).

## 9. Layout & Shell Contract

Ebeveynin sayfaları (`_LayoutTenantShell`, Scope = Tenant). Yeni sayfa, yeni modal, yeni form alanı yok (`form_field_count: 0`). Rozet mevcut
`badge bg-label-*` sınıflarıyla.

## 10. Backend File Convention

Ebeveyn §10 düzeni: port `Application/Features/Recall/Ports/`, kural `Domain/Recall/Rules/`, HTTP bağdaştırıcı `Infrastructure/Recall/`, DI
`Api/Recall/RecallServiceCollectionExtensions.cs`. Yeni denetleyici yok.

## 11. Frontend File Contract

- DL-2 yalnız `case-detail.js` ve `trace-view.js` gösterimini değiştirir; `case-view.js` `customerRows` anahtarla eşleşmeye devam eder.
- **Metin anahtarları, aile bazında (7 dil, kiracı modülü; F-11):**

| Aile | Anahtar | tr değeri |
|---|---|---|
| ortak (Web `Recall.{lang}.resx` + servis `RecallEvidenceText_{lang}.resx`; parite bekçisi) | `Recall.Customer.NameCode` | "{0} · {1}" |
| ortak | `Recall.Customer.Unresolved` | "Çözülemedi" |
| ortak | `Recall.Customer.NotFound` | "Bulunamadı" |
| ortak | `Recall.Customer.Distinguisher` | "{0} · {1}" (teslim yeri · sevkiyat no) |
| ortak | `Recall.Customer.UnresolvedWith` | "Çözülemedi ({0})" |
| ortak | `Recall.Customer.NotFoundWith` | "Bulunamadı ({0})" |
| ortak | `Recall.Customer.LegacyMarked` | "{0} (Eski kayıt)" |
| ortak | `Recall.Customer.NoCrmMarked` | "{0} (CRM kimliği yok)" |
| ortak | `Recall.Ev.W.customerNames` | "Müşteri adları" |
| ortak | `Recall.Ev.W.customerNamesSnapshot` | "CRM'den, dışa aktarım anında" |
| yalnız Web (`Recall.{lang}.resx`, `Recall.cs`, `recall.l10n.js`) | `Recall.Customer.LegacyBadge` | "Eski kayıt" |
| yalnız Web | `Recall.Customer.NoCrmBadge` | "CRM kimliği yok" |
| yalnız Web | `Recall.Customer.LegacyHint` | "CRM müşteri kimliğinden önce kaydedildi; yeni kimliğe otomatik bağlanmaz." |
| yalnız Web | `Recall.Customer.NoCrmHint` | "Sevkiyat okunduğunda CRM müşteri kimliği yoktu ya da okunamadı." |
| yalnız Web | `Recall.Customer.ManualHint` | "Elle girildi; CRM müşteri kimliğine bağlanmaz." |
| yalnız Web | `Recall.Customer.CrmUnavailable` | "CRM'e ulaşılamıyor, sonra tekrar deneyin" |
| yalnız Web | `Recall.Errors.RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE` | "CRM'e ulaşılamıyor, sonra tekrar deneyin" |

- Servis ailesi kanıt paketi PDF / CSV sözcüklerini taşır; **parite bekçisi** (`EvidenceTextParityTests`) ortak anahtarın Web ailesinde aynı
  değerle bulunmasını ister → ortak anahtarlar **DL-1'de** iki ailede birlikte eklenir; DL-2 yalnız kullanır. Yalnız Web anahtarları da DL-1'de
  eklenir (Repo Scope DL-1), DL-2 kullanır.
- Hardcoded metin yok; `recall.l10n.js` / `Recall.cs` anahtar listesine eklenir.

## 12. Validation Rules

| Girdi | Kural | Sonuç |
|---|---|---|
| SHIPMENT `customerRef` | string, `Guid.TryParseExact(…, "D")`, `≠ Guid.Empty` | aksi hâlde yok sayılır, sıra 2 (§8.2) |
| SHIPMENT `shipToReference` | `IsDShaped` değil (F-12) | `IsDShaped` ise anahtar yapılmaz, sıra 3 (§8.2) |
| komut `customerRef` | 1–200 karakter (bugünkü); `TryCanonical` başarılıysa parmak izi kanonik, `Crm` satırıyla eşleşirse kayıt kanonik (§8.3) | kapsamda yoksa 404 `RECALL_UNKNOWN_CUSTOMER`; `Crm` satırı `Unresolved` ise 503 `RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE` (§8.4) |
| `addScopeItems` Shipment `customerRef` | 1–200 karakter (bugünkü); `IsDShaped` olmamalı (Q-6, §8.2) | `IsDShaped` ise 400 `INVALID_REQUEST`, tüm istek reddedilir, yazım 0 |
| CRM 200 gövdesi | §8.6 tablo | `Unresolved` |

## 13. Failure Path to Verify

1. MVP-6 3.4.0 (alan yok) ya da `customerRef` boş / geçersiz → sıra 2 / 3, bugünkü sonuç bayt eşit (AC-2).
2. CRM yanıt vermiyor (zaman aşımı) → satır "Çözülemedi", CRM'in istek süresine katkısı ≤ 2 sn + pay, hesap sayısından bağımsız (AC-12).
3. Yetkisiz: kullanıcıda `crm.account.lookup-validation` yok → CRM 403 → "Çözülemedi", recall okumaları 200, `Crm` satırına kayıt 503 (AC-18);
   recall okuma izni yok → bugünkü 403.
4. Başka kiracının hesabı (MVP-6 veri hatası) → CRM 404 → "Bulunamadı", ad sızmaz (AC-11).
5. Tekrar: aynı idempotency anahtarı, GUID farklı harfle → 200 tekrar (AC-3); CRM kapalıyken daha önce yazılmış isteğin tekrarı → 200 (AC-25).
6. Yeniden izleme sırasında MVP-6 erişilemez → saklı CRM anahtarı korunur (AC-6 (b)).
7. Kayıtsız `Draft` vakada yeniden izleme MVP-6'dan `customerRef` alır → FU01 öncesi anahtar ve kaynak kalır (Q-5; AC-6 (a)); FU01 sonrası
   geçici `Document` satırı `Crm`'e geçer (AC-6 (a4)).
8. Elle kapsam satırında GUID biçimli `customerRef` (CRM müşterisi seçme denemesi) → 400 `INVALID_REQUEST`, yazım 0 (Q-6; AC-20).
9. Revizyon kopyası kaynağı düşürür → `Crm` satırı eski görünür ve büyük harfli GUID'le kayıt 404 olur — engellenir (AC-6 (e)).
10. CRM kesintisinde aynı vakada iki çözülemeyen müşteri → satırlar ayırıcıyla ayrılır, bildirim / beyan / iade bağı ekranda kapalı, sunucuda
    503, yazım 0 (AC-25, AC-27).
11. CRM kesintisinde kanıt dışa aktarımı → dosya üretilir, çözülemeyen satır işaretli; CRM düzelince saklı dosya değişmez (AC-26).

## 14. Authorization Convention

Yeni recall izin anahtarı yok; okuma uçları mevcut `[HasPermission]` anahtarlarıyla (ebeveyn §14.2). CRM çağrısı CRM'in
`crm.account.lookup-validation` anahtarını ister (CRM taslağı §6, K-1); `crm.account.read` gerekmez ve verilmez. Q-1 = kullanıcı token'ı ise
anahtarın geri çağırma rollerine (ebeveyn §14.3) atanması Auth işidir (D-4, Ali) ve **DL-1 kabul kapısıdır** (CT kararı, 2026-11-03):
atanmamışken okumalar "Çözülemedi" ile çalışır, ama `Crm` satırına kayıt 503 ile reddedilir (§8.4). Varsayılan Görüntüleyici rolü bu anahtarı
kendiliğinden almaz (`DefaultRolePermissionTemplate.cs:207-211`, yalnız `read` eylemi). Q-1 = servis kimliği ise yetki modeli değişir; §7 D-3.

## 15. Gateway / API Routing Decision

Yeni rota yok. Recall: `/api/recall`, `/api/recall/{everything}` → 5067 (ocelot `:1106`, `:1124`). CRM: `/api/crm/accounts/{everything}` →
5061 (`:2979`); U-1 bu joker rotayla taşınır (CRM taslağı §8, K-4). `ocelot.json` değişmez; teyit integration-agent'ındır (yalnız not).

## 16. Acceptance Criteria

Ortak fixture: kiracı T1 / şirket LE1, ikinci kiracı T2; MVP-6 ikizi (`ShipmentBundleDouble`, mevcut — `customerRef` alanı eklenir) ve
**CRM ikizi** (yeni; kiracıyı yalnız gelen token'dan çözer, çağrı sayar, gecikme / durum kodu hesap başına programlanır). Hesaplar: A = `3c9a1f2e-7b4d-4e61-9a2c-5d8e0f1a2b3c`
(T1, "Example Hastanesi", `ACC-2026-000042`), B (T2), C ve D (T1, ikizde programlanan durumla), N (T1, ikizde 404). Test sınıflarının adları **önerilir**,
kabulde adlarıyla kayda geçer. Her ölçüt tek satırdır (kabul aracı biçimi). Her sabotaj **üretim kodunda** tek tek uygulanır ve geri alınır;
kabul kaydına "kırmızı → geri al → yeşil, diff boş" yazılır.

**DL-1**

- [ ] AC-1 (zorunlu) — Sıra 1: sevkiyat 200 `customerRef = A`, `shipToReference = "CUST-100/ADDR-2"` → `runTrace` Shipment düğümü `customerRef = A`, kapsam satırı `CustomerRef = A`, `CustomerRefSource = Crm`. Test `RecallCrmCustomerKeyTests.CustomerRef_wins_over_shipTo`. Sabotaj S-1: `RecallTraceEngine.ShipmentsAsync`'te shipTo dalını `CustomerAccountId`'den önce değerlendir → kırmızı.
- [ ] AC-2 (zorunlu) — Sıra 2 / 3, her biri ayrı fixture: (a) alan yok (3.4.0 yanıtı); (b) `""`; (c) `"not-a-guid"`; (d) `Guid.Empty`; (e) sayı → anahtar `shipToReference`; (f) shipTo da boş → `DOC:<belge>`; (g) okuma 403 / 404 / zaman aşımı → `DOC:<belge>`; (h) `customerRef` yok ve `shipToReference` `"D"` biçimli bir GUID → anahtar `DOC:<belge>`, kaynak `Document`, günlükte `recall.shipment.ship_to_guid_shaped` satırı değersiz. (a)'da `runTrace` yanıtı FU01 öncesi koddan sabitlenmiş vektöre (`customer` alanı hariç) bayt eşit. Test `RecallCrmCustomerKeyTests.Fallback_order_*`. Sabotaj S-2a: okuyucuda GUID ayrıştırmasını kaldır, boş olmayan her string'i hesap say → (c) kırmızı; S-2b: `Guid.Empty` kontrolünü kaldır → (d) kırmızı; S-2c: sıra 2'nin `IsDShaped` bekçisini kaldır → (h) kırmızı.
- [ ] AC-3 (zorunlu) — Büyük/küçük harf ve kırpma: S1 `customerRef = A` büyük harfle, S2 küçük harfle → `getCaseCustomers` **tek** satır, anahtar küçük harf; (b) `recordNotification` `customerRef = A` büyük harfle → 201, saklanan değer küçük harf; (c) aynı idempotency anahtarıyla küçük harfle tekrar → 200, aynı `notificationId`; (d) `recordConsumption` `customerRef = "  " + A büyük harf + " "` → 201, saklanan değer kanonik A. Test `RecallCrmCustomerKeyTests.Case_insensitive_key`. Sabotaj S-3a: okuyucuda kanonikleştirmeyi kaldır (ham metni sakla) → iki satır, kırmızı; S-3b: komut eşleşmesini ham girişle ordinal yap → (b) 404, kırmızı; S-3c: parmak izini ham girişten hesapla → (c) 400 `IDEMPOTENCY_KEY_REUSED`, kırmızı; S-3d: `TryCanonical`'ın 36 karakter denetimini kırpmadan önceki ham değere uygula → (d) 404, kırmızı.
- [ ] AC-4 (zorunlu) — Kayıtlar yeni anahtarı kabul eder: onaylı kapsamda A (CRM ikizi `Resolved`) ile `recordNotification`, `recordConsumption`, `linkReturn` → 201; `getCaseEffectiveness` (A, SKU, birim) satırında `returned`, `consumed` sayılır; vaka olayı ve denetim satırının `customerRef` ayrıntısı kanonik A; kapsamda olmayan GUID → 404 `RECALL_UNKNOWN_CUSTOMER`, üç koleksiyonda yazım 0. Test `RecallCrmRecordTests.Records_accept_crm_key`. Sabotaj S-4: `LinkRecallReturnHandler` müşteri kümesini yalnız `Crm` olmayan satırlardan kur → `linkReturn` 404, kırmızı.
- [ ] AC-5 (zorunlu) — Eski kayıtlar değişmez, `Active` vakada: FU01 öncesi biçimde tohumlanmış vaka (kaynaksız kapsam satırları; `CUST-100/ADDR-2`, `warehouse-outbound:…:ship-to`, `DOC:…` anahtarlı bildirim, beyan ve iade bağı). (a) FU01 okumaları ve başka müşteriye yeni CRM kaydı sonrası eski belgeler bayt eşit; (b) `CUST-100/ADDR-2` ile bildirim → 201, değer aynen, CRM ikizine 0 çağrı; `cust-100/addr-2` → 404 (eski anahtar harfe duyarlı kalır); (c) `"D"` biçimli olmayan eski anahtarlı isteğin parmak izi FU01 öncesi koddan sabitlenmiş vektöre eşit; (c2) `"D"` biçimli eski anahtar vektörleri: küçük harfli eski anahtarın parmak izi sabit vektöre eşit; büyük harfli eski anahtarla FU01 öncesi yazılmış kaydın aynı anahtarla tekrarı 400 `IDEMPOTENCY_KEY_REUSED` (§8.3'te yazılı kabul edilen fark; gerçek veride yokluğu AC-20 (f)). Test `RecallCrmLegacyTests.Legacy_records_unchanged`. Sabotaj S-5: kanonikleştirmeyi (küçük harf) `"D"` biçimli olmayan anahtarlara da uygula → (b) ve (c) kırmızı.
- [ ] AC-6 (zorunlu) — Yeniden izlemede ve revizyonda kararlılık (§8.5), her biri ayrı fixture; (a)…(d) `Draft` vakada, hiç bildirim / beyan / iade bağı yokken (ön koşul testte doğrulanır: üç koleksiyonda vaka için 0 belge): (a) FU01 öncesi (`null`) `CUST-100/ADDR-2` satırı, MVP-6 artık aynı sevkiyat için `customerRef = A` döner → `submitScope` sonrası anahtar `CUST-100/ADDR-2` ve kaynak `null` kalır; aynı izde yeni belge düğümü A / `Crm` alır; `getCaseCustomers` iki satır, eski satırın `customer.state = Legacy`; (a2) aynısı `null` kaynaklı `DOC:` satırıyla → anahtar ve `null` kalır; (a3) silinmiş `Manual` satırın belgesi izle geri gelir, MVP-6 `customerRef = A` döner → satır geri yüklenir, anahtarı ve `Manual` kaynağı kalır; (a4) FU01 sonrası MVP-6 kesintisinde yazılmış `Document` satırı, sonraki izde `customerRef = A` → anahtar A, kaynak `Crm`; (a5) FU01 sonrası `ShipTo` satırı (3.4.0 yanıtıyla yazılmış), sonraki izde `customerRef = A` → anahtar A, kaynak `Crm`; (b) saklı `Crm` satırı, yeniden izlemede sevkiyat `Unavailable` → anahtar A ve `Crm` kalır; (c) `null` kaynaklı `DOC:` satırı, yeni iz shipTo döner → anahtar shipTo olur, kaynak `null` kalır; (c2) `null` kaynaklı shipTo satırı, yeni iz aynı shipTo → anahtar aynı, kaynak `null`; (c3) FU01 sonrası `Document` satırı, yeni iz shipTo (customerRef yok) → anahtar shipTo, kaynak `ShipTo`; (d) saklı `Crm` A, yeni iz `Crm` C → anahtar C, kaynak `Crm`, günlükte `recall.scope.customer_ref_changed` satırı düğüm anahtarıyla, A ve C metni günlükte yok; (e) `Active` vaka, kayıtlarla (A'ya ve `CUST-100/ADDR-2`'ye birer bildirim), MVP-6 artık `CUST-100/ADDR-2` sevkiyatı için de `customerRef = A` döner, izde bir yeni sevkiyat bulgusu (`customerRef = A`) var ve vakada FU01 sonrası bir `Document` satırı var → `reviseScope` (bulgu eklenerek) → r+1'de A satırı `Crm`, `CUST-100/ADDR-2` satırı `null`, `Document` satırı `Document` olarak kopyalanır, hiçbiri yükseltilmez; delta düğümü A / `Crm` alır; revizyon onaylandıktan sonra r+1 üzerinde A büyük harfle `recordNotification` → 201, saklanan değer küçük harf. Test `RecallCrmReconcileTests.*`. Sabotaj (her biri ayrı): S-6a `Reconcile`'da anahtarı bugünkü gibi koşulsuz üzerine yaz (`:141`) → (a), (a2), (a3), (b) kırmızı; S-6b `null` kaynağı da FU01 sonrası gibi yükselt → (a), (a2) kırmızı, (a4) yeşil; S-6c FU01 sonrası `ShipTo` / `Document`'i eski say (r2 kuralı) → (a4), (a5) kırmızı; S-6d anahtar değişince `null` kaynağa yeni izin kaynağını yaz → (c) kırmızı; S-6e `Crm` → `Crm` farkında saklı anahtarı koru (r2 kuralı) → (d) kırmızı; S-6f `ReviseRecallScopeHandler` kopyasından `CustomerRefSource`'u çıkar → (e) kırmızı (A satırı `Legacy`, büyük harfli bildirim 404).
- [ ] AC-7 (zorunlu) — Çözüldü: CRM 200 `{accountId: A, code, name, status: "inactive", isBillable: false, contractVersion: "crm-account-lookup/1.0"}` → satır `customer = {source: Crm, state: Resolved, name, code}`, `label`, `shipTo`, `shipmentNumber` yok. Test `RecallCustomerDirectoryTests.Resolved_ignores_billability`. Sabotaj S-7: çözümü `isBillable = true` koşuluna bağla → `Unresolved`, kırmızı.
- [ ] AC-8 (zorunlu) — Çözülemedi, her biri ayrı fixture: 400, 401, 403, 500, 503, zaman aşımı, ulaşılamıyor, `accountId` farklı, `name` boş, `code` yok, `contractVersion = crm-account-lookup/2.0`, ayrıştırılamayan gövde → `state = Unresolved`, `name` / `code` yok. Test `RecallCustomerDirectoryTests.Unresolved_*`. Sabotaj S-8a: 403'ü `NotFound` say → kırmızı; S-8b: `accountId` karşılaştırmasını kaldır → kırmızı.
- [ ] AC-9 (zorunlu) — Bulunamadı: CRM 404 → `state = NotFound`. Test `RecallCustomerDirectoryTests.NotFound_is_404_only`. Sabotaj S-9: 404'ü `Unresolved`'a eşle → kırmızı.
- [ ] AC-10 (zorunlu) — GUID gösterilmez (sunucu): A'lı vakada `getCaseCustomers`, `getCaseEffectiveness`, `getCaseScope`, `getCaseScopeDiff`, `runTrace` yanıtlarında A metni yalnız `customerRef` adlı alanlarda geçer — `label` ve `customer.*` alanlarında (`shipTo`, `shipmentNumber` dahil) geçmez; `exportTrace` CSV'si, kanıt paketi CSV ve PDF metni A içermez (üç durumda: Resolved, Unresolved, NotFound); (d) izinde A bulunan tatbikatın `getDrillRun` yanıtında A yalnız `customers` anahtar dizisinde geçer (§8.7, §8.8 tatbikat istisnası). Test `RecallCrmNoGuidDisplayTests.*` (tek JSON tarayıcı yardımcı, izin verilen yollar adıyla). Sabotaj (her biri ayrı): S-10a `CaseDetailReadHandlers` etiketinde CRM satırı için `?? g.Key` → kırmızı; S-10b `RecallEvidence` müşteri satırına ham anahtar → kırmızı; S-10c iz dışa aktarımında "place" = `n.CustomerRef` (bugünkü `:213`) → kırmızı; S-10d ayırıcının sevkiyat numarasını `SourceDocumentId`'den al → `shipmentNumber`'da GUID, kırmızı.
- [ ] AC-11 (zorunlu) — Kiracı izolasyonu: (a) T2 vakası, MVP-6 ikizi `customerRef = A` (T1 hesabı) döner → CRM ikizi T2 token'ıyla 404 → `NotFound`; yanıtta T1 adı / kodu yok; (b) T1 kullanıcısı T2 vakasının müşterilerini okur → 404 (bugünkü); (c) CRM isteği çağıranın `Authorization` ve `X-Correlation-Id` başlığını taşır, kiracı başlığı ve `X-Legal-Entity-Id` göndermez. Test `RecallCrmTenantTests.*`. Sabotaj S-11: CRM `HttpClient` kaydından `.AddHttpMessageHandler<ForwardCallerHeadersHandler>()`'ı kaldır → (c) kırmızı (ikiz 401 → (a)'nın beklenen `NotFound`'u da `Unresolved` olur).
- [ ] AC-12 (zorunlu) — Süre bütçesi ve eşzamanlılık (§8.6): (a) CRM ikizi A için 5 sn bekler → satır `Unresolved`, `getCaseCustomers` 2,5 sn içinde döner; (b) 9 farklı hesap, her biri 1,5 sn → ikizde aynı anda en çok 8 istek; (c) 17 farklı hesaplı vaka, CRM ikizi hiç yanıt vermez, MVP-6 ikizi anında yanıtlar → 17 satır `Unresolved`, `getCaseCustomers` 2,5 sn içinde döner; (d) üç hesap: A 0,1 sn'de yanıtlar, C ve D hiç yanıt vermez → A `Resolved`, C ve D `Unresolved`, yanıt 2,5 sn içinde (kısmi sonuç); (e) tek hesaplı vaka, MVP-6 ikizi 1,5 sn, CRM ikizi hiç yanıt vermez → `getCaseCustomers` 2,5 sn içinde döner (iki okuma eşzamanlı). Süreler testte `Stopwatch` ile ölçülür. Test `RecallCustomerDirectoryTests.Budget_*`. Sabotaj (her biri ayrı): S-12a `PerReadTimeout` 10 sn ve `TotalBudget` 10 sn → (a) kırmızı; S-12b `MaxParallel` kapısını kaldır → (b) kırmızı; S-12c `TotalBudget`'ı kaldır (yalnız okuma başına sınır) → (c) yaklaşık 6 sn, kırmızı; S-12d bütçe dolunca tüm sözlüğü `Unresolved` döndür → (d) kırmızı; S-12e CRM çözümünü MVP-6 okumasından sonra başlat → (e) yaklaşık 3,5 sn, kırmızı.
- [ ] AC-13 (zorunlu) — Önbellek yok, istek içi tekilleştirme: A'lı üç sevkiyat satırı → bir `getCaseCustomers` isteğinde CRM'e 1 çağrı; art arda iki istek → toplam 2 çağrı. Test `RecallCustomerDirectoryTests.No_cache_dedup_per_request`. Sabotaj S-13a: `Distinct`'i kaldır → 3 çağrı, kırmızı; S-13b: statik sözlükte sonuç sakla → ikinci istek 0 çağrı, kırmızı.
- [ ] AC-14 (zorunlu) — Günlükte kişisel veri yok: AC-7 / AC-8 / AC-12 / AC-25 koşularının günlük yakalamasında ad ve kod hiçbir satırda geçmez; başarısızlıkta `recall.customer.lookup_unavailable` satırı `AccountId` ve neden (bütçe dolunca `Budget`) taşır; geçersiz `customerRef` ve `"D"` biçimli `shipToReference` değeri günlüğe yazılmaz (§8.2). Test `RecallCrmLoggingTests.*`. Sabotaj S-14: geçersiz sözleşme yanıtında gövdeyi günlüğe yaz → kırmızı.
- [ ] AC-15 (zorunlu) — Mühürler: FU01 öncesi kapsam fixture'ının rs-scope-v1 özeti sabit vektöre bayt eşit; aynı kapsam CRM adı değişse de aynı özeti verir; `CustomerRefSource` kanonik metinde yok; rt-v1 özeti değişmez. Test `RecallCrmSealTests.*`. Sabotaj S-15: `RecallScopeRules.Canonical`'a `customerRefSource` yaz → vektör kırmızı.
- [ ] AC-16 (zorunlu) — RECALL 1.10.0: `recall.openapi.yaml` `version: 1.10.0`; değişiklik notunun ilk satırı `1.10.0 — additive + one input restriction (MOD-0177-FU01` ile başlar ve Q-6 `addScopeItems` daraltmasını, yeni `RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE` (503) kodunu ve `DrillRun.customers` adlandırmasını anar; `CustomerDisplay` şeması ve beş isteğe bağlı `customer` alanı; kod üst düzey kayıtta ve `recordNotification` / `recordConsumption` / `linkReturn` listelerinde; dondurulmuş 1.9.0 fixture'ı (`recall-v1.9.0.openapi.yaml`, LF sha256 `7d97b13d…930c`) ile canlı dosya arasındaki fark tam olarak bu listedir ve tek girdi daraltması `addScopeItems` açıklamasıdır; AC-1…AC-13, AC-20 ve AC-25 yanıtları şemaya karşı doğrulanır (`ContractSchemaValidator`); `RecallContract120Tests` … `RecallContract180Tests` değiştirilmeden yeşil, `RecallContract190Tests` yalnız "after" dosyası fixture'a çevrilerek ve `RecallRulesAndShapeTests` yalnız sürüm satırı ve 1.10.0 belirteçleriyle değişip yeşil. Test `RecallContract1100Tests.*`. Sabotaj (her biri ayrı): S-16a sunucu `customer.state` alanını yazmasın → şema doğrulaması kırmızı; S-16b değişiklik notunu "additive only" diye başlat → kırmızı; S-16c kodu `recordConsumption` listesinden çıkar → kırmızı.
- [ ] AC-17 (zorunlu) — **Kabul kapısı**, SHIPMENT 3.5.0 hizası: `shipment-bundle.openapi.yaml` `getShipment` 200 şemasında `ShipmentBundleHttpReader.CustomerRefField` adlı, `type: string`, `format: uuid` özellik var ve `info.version` ≥ 3.5.0. Test `ShipmentBundle350AlignmentTests.CustomerRef_field_matches`; D-1 kapanana dek `[Fact(Skip = "MOD-0177-FU01 D-1: SHIPMENT-BUNDLE 3.5.0 not frozen")]` ile adlı gerekçeyle atlanır (CT tam paket koşusunda kırmızı değil, atlanmış görünür); DL-1 kabul commit'i Skip'i kaldırır ve test yeşil olmadan DL-1 kabul edilmez; kabul kaydı Skip'in kaldırıldığı commit'i yazar. Sabotaj S-17 (Skip kaldırıldıktan sonra): sabitin değerini değiştir → kırmızı.
- [ ] AC-18 (zorunlu) — Yetki: CRM ikizi 403 (anahtar atanmamış kullanıcı) → recall okuma uçları 200, satır `Unresolved`; aynı kullanıcının `Crm` satırına `recordNotification` → 503 `RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE`, yazım 0; eski anahtarlı satıra → 201; recall okuma izni olmayan kullanıcı → 403 (bugünkü); `AuthorizationGuardTests` değiştirilmeden yeşil (yeni eylem yok, her eylem `[HasPermission]`). Test `RecallCrmAuthorizationTests.*`. Sabotaj S-18: CRM 403'ünü okuma ucuna taşı (uç 503) → kırmızı.
- [ ] AC-19 (zorunlu) — 7 dil: §11 tablosundaki her anahtar kendi ailesinde (`Recall.{en,tr,fr,es,zh,ar,ru}.resx`; ortaklar ayrıca `RecallEvidenceText_{7}.resx`) dolu, `zh` / `ar` / `ru` İngilizce kopya değil; `Recall.cs` anahtar listesinde; `EvidenceTextParityTests` ve Recall resx paritesi yeşil. Sabotaj S-19: `Recall.ar.resx`'ten `Recall.Customer.LegacyBadge`'ı sil → kırmızı.
- [ ] AC-20 (zorunlu) — Elle kapsam satırında CRM müşterisi seçilemez (§8.2; Q-6 = HAYIR). `Draft` vakada `addScopeItems`, her biri ayrı fixture: (a) Shipment `customerRef = "CUST-300/ADDR-1"` → 200; satır `CustomerRefSource = Manual`; `getCaseScope` ve `getCaseCustomers` satırında `customer = {source: Manual, state: NoCrm, label: "CUST-300/ADDR-1"}`, CRM ikizine 0 çağrı; (b) `customerRef = A` (küçük harf) → 400 `INVALID_REQUEST`; (c) A büyük harfle → 400; (d) `"  " + A + " "` (kırpılınca 36 karakterlik GUID) → 400; (e) iki maddelik toplu istek, ilki geçerli (`CUST-301/ADDR-1`), ikincisi A → 400 ve vaka için `recall_scope_items`, vaka olayı ve denetim satırı sayıları istek öncesine eşit (ilk madde de yazılmaz); (e2) `"{" + A + "}"` (B biçimi) ve tiresiz A (N biçimi) → 200, `Manual`, `NoCrm` (yasak yalnız `"D"` biçimine bağlı); (f) **veri sayımı** (salt okuma operasyon sorgusu, ürün kodu değil): `Kind = Shipment`, `CustomerRefSource` `Crm` olmayan (`null` dahil), her revizyon ve silinmiş satırlar dahil kapsam satırlarında `IsDShaped(customerRef)` olanların kiracı başına sayısı (değer yazılmaz) — beklenen 0; (1) DL-1 kabul ortamında kabul koşusunda, (2) her hedef ortamda FU01 kod geçişinin hemen öncesinde ve (3) hemen sonrasında koşulur, (2) ve (3) sıfır ve eşit olmalı; sayımı MVP-1 CT koşar ve kabul / geçiş kaydına yazar, geçişi yapan kişi geçiş kaydında iki sonucu imzalar; sıfır değilse kabul ya da geçiş durur, satırlar CT'ye gider (O-2). Test `RecallCrmManualScopeTests.*`. Sabotaj (her biri ayrı): S-20a GUID kontrolünü kaldır → (b), (c) kırmızı; S-20b `IsDShaped`'in 36 karakter denetimini kırpmadan önceki ham girdiye uygula → (d) 200, kırmızı; S-20c kontrolü yalnız küçük harfe bağla (`Guid.TryParseExact` yerine kanonik metin karşılaştırması) → (c) kırmızı; S-20d reddetmek yerine GUID'li maddeyi sessizce atla (öteki maddeler yazılır, 200) → (e) kırmızı; S-20e `Guid.TryParse` (her biçim) kullan → (e2) kırmızı; S-20f satırı `CustomerRefSource` yazmadan kur → (a) kırmızı (`source = Unknown`). (f) üretim kodu değildir, sabotajı yoktur; kanıtı sorgu + sonuçtur.
- [ ] AC-25 (zorunlu) — Çözülemeyen müşteriye yazım yok, sunucu (sahip kararı F-1; §8.4): `Active` vaka; A (`Resolved`), C ve D (CRM ikizi 503), N (404) `Crm` satırları; C'nin sevkiyatı `shipToReference = "CUST-200/ADDR-1"`, D'ninki `"CUST-201/ADDR-1"`. (a) `getCaseCustomers`: C satırı `customer = {source: Crm, state: Unresolved, shipTo: "CUST-200/ADDR-1", shipmentNumber: <C'nin hareket numarası>}`, D satırı kendi değerleriyle; iki satırın ayırıcısı farklı, hiçbirinde GUID yok; N satırı `state: NotFound` ve ayırıcıyla; (b) C ile `recordNotification`, `recordConsumption`, `linkReturn` → her biri 503 `RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE`, yanıt 2,5 sn içinde, yanıt gövdesinde C yok; üç koleksiyon, vaka olayı ve denetim satırı sayıları istek öncesine eşit; (c) A ile üçü → 201; N ile üçü → 201 (Q-7 varsayımı); (d) eski anahtarlı satırla üçü → 201 ve CRM ikizine 0 çağrı; (e) CRM açıkken C ile yazılmış bildirimin aynı anahtar ve aynı gövdeyle tekrarı CRM kapalıyken → 200, aynı `notificationId`. Test `RecallCrmUnresolvedWriteTests.*`. Sabotaj (her biri ayrı): S-25a çözüm denetimini kaldır → (b) 201, kırmızı; S-25b `NotFound`'u da reddet → (c) kırmızı; S-25c CRM'i her komutta çağır (eski anahtar dahil) → (d) çağrı 1, kırmızı; S-25d çözüm denetimini tekrar denetiminden önceye al → (e) 503, kırmızı; S-25e 503 yerine 409 `RECALL_CONFLICT` dön → (b) kırmızı.
- [ ] AC-26 (zorunlu) — Kanıt paketi anlık görüntüsü (CT kararı; §8.10): karışık vaka (A `Resolved`, C `Unresolved`, `null` kaynaklı `CUST-100/ADDR-2`, `Manual` `CUST-300/ADDR-1`): (a) `createEvidenceExport` CSV ve PDF → 201 (Unresolved varken reddedilmez); müşteri bölümünde A hücresi "Example Hastanesi · ACC-2026-000042", C hücresi `Recall.Customer.UnresolvedWith` metni C'nin hareket numarasıyla, eski satır "CUST-100/ADDR-2 (Eski kayıt)", elle satır "CUST-300/ADDR-1 (CRM kimliği yok)" (dil `tr`); özet bölümünde `customerNames` satırı; A ve C GUID'i dosyada yok; (b) saklı dosyanın sha256'sı dönen baytların hash'ine eşit (mevcut kural); (c) CRM ikizinde A'nın adı değiştirilir → aynı dışa aktarımın `getEvidenceFile`'ı aynı baytları ve hash'i döner, yeni dışa aktarım yeni adı taşır; (d) yalnız CRM kimliksiz satırları olan vakada müşteri ve özet bölümü FU01 öncesi koddan sabitlenmiş vektöre bayt eşit ve `RecallEvidenceTests` değiştirilmeden yeşil; (e) dışa aktarım günlüğü satırı ve `recall.evidence.exported` denetim satırı adı ve kodu taşımaz. Test `RecallCrmEvidenceTests.*`. Sabotaj (her biri ayrı): S-26a `Unresolved` varken dışa aktarımı reddet → (a) kırmızı; S-26b `Unresolved` hücresini boş yaz → (a) kırmızı; S-26c işareti yalnız CRM kimliksiz vakaya da uygula → (d) kırmızı; S-26d `getEvidenceFile`'da dosyayı yeniden üret → (c) kırmızı.

**DL-2**

- [ ] AC-21 (zorunlu) — Müşteriler sekmesi: `Resolved` satır "{ad} · {kod}", altında anahtar satırı yok; `Unresolved` "Çözülemedi" ve ayırıcı, `NotFound` "Bulunamadı" ve ayırıcı; sayfanın metin içeriğinde ve `title` özniteliklerinde A geçmez (`data-notify` / `data-consume` değerinde geçebilir). Test `recall-fu01.test.mjs` "customers tab never prints the account id". Sabotaj S-21: `case-detail.js:248`'deki tek aralıklı `customerRef` alt satırını CRM satırında bırak → kırmızı.
- [ ] AC-22 (zorunlu) — Diğer görünümler: kapsam sekmesi, kapsam farkı listesi (`getCaseScopeDiff` `added`), iade bağları, etkinlik tablosu, üç pencere ve seçim listesinin `option` metni, iz ekranı — A metin olarak geçmez, ad + kod ya da durum metni görünür; tatbikat ekranı (`drill-view.js`) `customers` alanını hiçbir yerde çizmez. Test `recall-fu01.test.mjs` "no view prints the account id". Sabotaj (her biri ayrı): S-22a `trace-view.js:32` `n.customerRef`'e geri dönsün → kırmızı; S-22b etkinlik tablosu (`:287`) `r.customerRef` → kırmızı; S-22c kapsam farkı listesi (`case-detail.js:165`) `n.customerRef` → kırmızı; S-22d `drill-view.js` koşu kartına `run.customers` listesini bas → kırmızı.
- [ ] AC-23 (zorunlu) — Rozetler (§8.8): AC-6 (a) verisiyle ekran iki satır çizer; eski satırda "Eski kayıt" rozeti ve `Recall.Customer.LegacyHint` ipucu, CRM satırında rozet yok; satırlar ad eşleşse de birleşmez; FU01 sonrası `Document` satırında (AC-6 (e) r+1 verisi) "CRM kimliği yok" rozeti ve `Recall.Customer.NoCrmHint`; AC-20 (a)'nın `Manual` satırında "CRM kimliği yok" rozeti ve `Recall.Customer.ManualHint`. Test `recall-fu01.test.mjs` "legacy and no-crm rows stay separate with badge". Sabotaj (her biri ayrı): S-23a satırları `customer.name`'e göre birleştir → kırmızı; S-23b rozeti yalnız `state = Legacy`'ye bağla → `NoCrm` satırları rozetsiz, kırmızı; S-23c `Manual` satırına `LegacyHint` bas → kırmızı; S-23d FU01 sonrası `Document` satırına `LegacyHint` bas → kırmızı.
- [ ] AC-24 (zorunlu) — Sabit metin yok: dil `ar` iken üç durum metni, iki rozet ve `Recall.Customer.CrmUnavailable` `Recall.ar.resx` değerleriyle çizilir. Test `recall-fu01.test.mjs` "texts come from l10n". Sabotaj S-24: `case-detail.js`'e "Çözülemedi" sabit yaz → kırmızı.
- [ ] AC-27 (zorunlu) — Çözülemeyen müşteriye ekranda komut yok (sahip kararı F-1; §8.8): AC-25 verisiyle müşteriler sekmesinde C ve D satırında bildirim ve tüketim düğmesi çizilmez, yerine `Recall.Customer.CrmUnavailable` metni; A ve N satırında düğmeler var; iade bağlama penceresinin seçim listesinde C ve D seçenek değil, pencerede `Recall.Customer.CrmUnavailable` görünür; yalnız C'nin olduğu vakada pencere alanları ve bağlama düğmesi çizilmez; sunucunun 503 yanıtı `Recall.Errors.RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE` metniyle gösterilir. Test `recall-fu01.test.mjs` "unresolved customer offers no command". Sabotaj (her biri ayrı): S-27a `Unresolved` satırda düğmeleri çiz → kırmızı; S-27b `Unresolved` müşteriyi seçim listesinde bırak → kırmızı; S-27c `Recall.Errors.RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE`'ı `Recall.cs` listesinden çıkar → 503 ham kodla görünür, kırmızı.

## 17. Test Expectations

- Build: InventoryService ve Web çözümü uyarı politikası değişmeden derlenir.
- **Tam paket:** servis dokunulduğu için kabul koşusu filtresiz Inventory test paketi + `Diten.Web.Tests` (CT kuralı); temel kırmızılar aynı
  gün `main` ile karşılaştırılır. Inventory testleri kendi Mongo replica set'ini başlatır (27017 kullanılmaz). AC-17 D-1 kapanana dek adlı
  gerekçeyle **atlanmış** görünür, kırmızı değildir (F-9); atlanan başka FU01 testi yoktur.
- Sözleşme: RECALL 1.10.0 şema doğrulaması ve 1.9.0 fixture farkı (AC-16); SHIPMENT 3.5.0 hizası (AC-17, kapı).
- İzolasyon: AC-11; mevcut `RecallTenantBoundaryTests` değiştirilmeden yeşil.
- Denetim: yeni olay yok; mevcut `recall.customer.notified` / `recall.consumption.declared` / iade bağlama olaylarının ayrıntısı CRM
  anahtarında kanonik GUID taşır (AC-4). Q-6 reddinde (`addScopeItems` 400) ve çözüm denetimi reddinde (503) vaka olayı ve denetim satırı
  yazılmaz (AC-20 (e), AC-25 (b)); başarılı `addScopeItems` ve `reviseScope` denetim satırları değişmez.
- Veri sayımı: AC-20 (f) salt-okuma sayımının sorgusu ve üç sonucu DL-1 kabul kaydına ve geçiş kaydına yazılır.
- Smoke (E5, FU01 kapanışı — DL kabulü değil): MOD-0149 DL-1 ve MVP-6 3.5.0 yayındayken bir vakada müşteri adı ve kodu görünür, GUID görünmez.

### 17.1 Sabotaj tablosu

Her AC'nin sabotajı metnindedir (S-1…S-27, alt harfli olanlar ayrı koşu). Sabotajı olmayan tek alt ölçüt AC-20 (f)'dir: üretim kodu değil,
salt-okuma veri sayımıdır (kanıtı sorgu + sonuç). AC-17'nin sabotajı (S-17) Skip kaldırıldıktan sonra, 3.5.0 FROZEN olunca koşulur. r2'nin
S-20b'si ("kontrolü kırpmadan önce yap") kırmızıya dönmediği için (.NET `Guid.TryParseExact` kırpar) kaldırıldı; yerine kuralın kendi 36
karakter denetimini hedefleyen S-20b ve S-3d geldi (F-5).

## 18. Ready-for-dev Checklist

- [x] Golden Reference / Backend File Convention ebeveyn düzeniyle (§10)
- [x] Frontmatter zorunlu alanlar dolu; ekranlar ebeveynin üç sayfası (§9)
- [x] Validation Rules (§12), Failure Path ≥ 4 (§13), Authorization (§14), Gateway kararı (§15)
- [x] AC'lerin hepsi üretim koduna karşı ve adlı sabotajlı (§16, 27 AC, her biri tek satır; tek istisna AC-20 (f) veri sayımı, §17.1)
- [x] Audited Events (§21) — yeni yazma komutu yok
- [x] Teslim dilimleri (§22) ve sayfa yardımı kararı (§23)
- [x] Sahip kararları Q-5 = HAYIR, Q-6 = HAYIR, F-1 (çözülemeyen müşteri) (Beste, 2026-11-03) — §8.2, §8.4, §8.5, §8.8, AC-6, AC-20, AC-25, AC-27
- [x] CT kararları CT-1 (mühre girmez), CT-2 (RECALL 1.10.0), CT-3 (kanıt anlık görüntüsü), CT-4 (D-3 / D-4 DL-1 kabul kapısı), CT-5 (Q-5 "eski" tanımı) (MVP-1 CT, 2026-11-03) — §19.6
- [x] Keşif bulgularının (gateway crm 5061 / recall 5067) mükerrer sayılmaması CT'ce kabul edildi (§19.1)
- [x] Registry satırı (CT, `8ba30d12b`)
- [x] Bağımsız inceleme 01 tur 1 (a66dd353) bulguları kapatıldı (§19.7)
- [ ] CT-APR-02 bağımsız inceleme (r3, yazandan ayrı oturum) + CT-APR-01 onay satırı (yüksek risk)
- [ ] **Ready-for-dev önkoşulu (sahip yanıtı, AC metnini değiştirir):** Q-7 (Bulunamadı satırında komutlar açık mı) ve Q-8 (Q-5 "eski" tanımının
  FU01 öncesi anahtarlara daraltılmasının teyidi) (§19.4)
- [ ] Açık, **DL-1 kabul kapısı** (kodlamayı engellemez — §7, §22): D-1 (Q-2), D-2 (Q-3), D-3 (Q-1), D-4 (Q-4), AC-20 (f) veri sayımı.
  D-5 CRM sahibinin adımı (kapı değil)
- [ ] `status: ready-for-dev` — CT kararıyla

## 19. Implementation Notes

### 19.1 Kimlik ve keşif kanıtı

- Ön kontrol: çıkış 0 (yukarıdaki künye notu). Araç `verify_module_id.py` sha256 `ae87a566…b51a`; depo HEAD `8b5205226`; origin/main `66864f1ab`
  (son fetch 2026-11-02T21:18, uzak durum doğrulanmadı). r2: registry satırıyla (`8ba30d12b`) aynı komut yeniden çıkış 0; keşif yeniden
  koşulmadı (registry satırı bu paketin kendisidir; diğer kaynaklar `8b5205226`'dan beri değişmedi — §künye ölçüm tabanı). r3 kimliğe dokunmaz.
- Keşif `--discover "recall customer crm"`: çıkış 3, rapor sha256 `d6262ba44790f4f0bb6ae4754eb0f163843546e106425a3f3da5a33dcfbd2b15`; 493 ref,
  9 görev kaynağı. Bulgular ve kararlar:

| Bulgu | Karar | Gerekçe |
|---|---|---|
| gateway `'crm'` → 5061 | mükerrer değil — **üretici-tüketici** | CRM-ACCOUNT-LOOKUP'ın üreticisi CrmService; bu paket tüketicidir (§7 D-2) |
| gateway `'recall'` → 5067 | mükerrer değil — **ebeveynin kendi rotası** | `/api/recall*` MOD-0177'nindir (ocelot `:1106`, `:1124`) |

- Blueprint, SoR, registry, paket ve görev eşleşmesi 0. **Kısmi inceleme sınırları** (raporun EKSİK satırları): bu makinede olmayan ya da
  son fetch'ten sonra itilen dallar, commit edilmemiş işler, kayıtsız görev kaynakları ve paketlerin iş kapsamı (yalnız ad / kimlik eşleşti)
  görülmedi.

### 19.2 Ölçülen gerçekler (`8b5205226` / `8ba30d12b` / `97cf7d170`, salt okuma)

- Anahtar üretimi: `RecallTraceEngine.cs:331` (`"DOC:" + document`), `:375` (`shipToReference`, yalnız okuma `Ok` ise);
  `RecallTraceService.cs:93` (`n.CustomerRef ?? "DOC:" + document`). `TraceModel.cs:97` sırayı belgeler.
- Okuyucu: `IShipmentReader` `Ports/IRecallTraceSource.cs:86-89` (ayrı dosya değil); `ShipmentBundleHttpReader.cs:19-22` (`MaxParallel = 8`,
  `PerReadTimeout = 2 sn`), `:54` (GUID değilse NotFound), `:66-70` (yalnız `status`, `actualDeliverAt`, `shipToReference` okunur), `:83`
  (başarısızlık günlüğü), **önbellek yok**, **toplam bütçe yok** (`:36-49` her okuma kendi 2 sn'siyle). DI `RecallServiceCollectionExtensions.cs:48-57`
  (Gateway taban adresi, `ForwardCallerHeadersHandler`).
- Uzunluk: `RecallContract.MaxCustomerRefLength = 200` (`RecallContract.cs:20`); kullanım `CaseCommandHandlers.cs:369`, `R3Handlers.cs:383`.
- Eşleşmelerin **hepsi ordinal / harfe duyarlı**: `RecordHandlers.cs:58`, `:155`, `:163`, `:169-170`; `R3Handlers.cs:417`;
  `CaseDetailReadHandlers.cs:75` (gruplama), `:78`, `:90`; `RecallEvidence.cs:283-286`; `RecallReads.cs:80`, `:137-138`; etkinlik
  `RecallRules.cs:342-343`.
- Parmak izleri ham (kırpılmış) `customerRef` ile ve **vaka / kapsam okunmadan önce**: `RecordHandlers.cs:32-42` (`:34`), `:125-136` (`:127`);
  `R3Handlers.cs:388-392`. `RecallSupport.Trimmed` = `string.Trim` (`RecallSupport.cs:134`).
- Müşteri satırı etiketi: `CaseDetailReadHandlers.cs:80-83` — shipTo, yoksa `DOC:` için belge numarası, **yoksa anahtarın kendisi** (CRM
  anahtarında GUID'e düşerdi → §8.7). Satır başına canlı `ShipToReference` (`:72`) ve hareket numaraları (`:68`) aynı işleyicide.
- Yeniden izleme: `RecallTraceService.Reconcile` `:111`, anahtar koşulsuz üzerine yazılır `:141`; tek çağıran `CaseCommandHandlers.cs:553` (`submitScope`).
  Silinmiş satırın geri yüklenmesi `:128-136` (`Origin = Trace`).
- Mühürler: rs-scope-v1 `RecallRules.cs:243`, `customerRef` kanonik metinde `:305`; rt-v1 müşteri referansını içermez (`TraceRules.cs:108-111`).
- Gösterim yerleri: `case-detail.js:135`, `:165`, `:244-245` (düğmeler), `:248` (anahtar tek aralıklı alt satırda), `:259`, `:287`, `:363`, `:380`, `:405`;
  `trace-view.js:32`; iz dışa aktarımı `RecallTraceService.cs:213`; kanıt paketi `RecallEvidence.cs:283-295`, `:428-440`.
  Tatbikat `Customers` listesi `DrillRunHandlers.cs:284` → `DrillSupport.cs:56` → `RecallDrillRunDto.Customers` (`DrillModels.cs:56`); `DrillRun`
  şeması `additionalProperties: true` (`recall.openapi.yaml:1688-1691`); `drill-view.js` `customers`'ı çizmiyor (yalnız adım adı `:16`).
- Sözleşmeler: RECALL `1.9.0` FROZEN (`recall.openapi.yaml:14-15`), `customerRef` şemaları `:1198`, `:1291`, `:1308`, `:1386`, `:1398`,
  `:1533-1534` (`label`), `:1566`; üst düzey hata kaydı `:962`; SHIPMENT-BUNDLE `3.4.0` FROZEN (`shipment-bundle.openapi.yaml:15`),
  `shipToReference` `:3208`, `customerRef` / `partyId` **yok**. CRM taslağı: U-1 200 alanları `accountId, code, name, status, isBillable,
  contractVersion`; 404 üç durumda aynı gövde; izin `crm.account.lookup-validation`; kiracı başlıkla verilmez (§1); `x-consumers` bugün yalnız MOD-0120.
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
- **r3 ölçümleri (`97cf7d170`):**
  - Kanıt saklama: `CreateRecallEvidenceExportHandler` (`EvidenceHandlers.cs:68-156`) üretilen baytları hash'ler ve saklar (`Sha256`
    `RecallEvidenceEntities.cs:46`, `Content` `:69`); önizleme aynı kurucuyu kullanır (`:56`); kurucu MVP-6'yı okumaz, ledger hareketlerini okur
    (`RecallEvidence.cs:427`); müşteri bölümü sütunları `customer, notifiedOn, channel, note` (`:312`), kimlik sütunu yok. Hücreler paketin
    dilinde, özet alanı sözcük anahtarıdır (`RecallEvidence.cs:18-26`). `RecallEvidenceTests.cs:144` müşteri satırında saklı teslim yeri metnini
    birebir arar.
  - Sözleşme testleri: yalnız `RecallContract190Tests` canlı dosyayı okur (`:39` `after = Load(ContractSchemas.RecallContractPath)`, `:41`
    `("1.8.0", "1.9.0")`, `:44` ilk not satırı `1.9.0 —`); emsal `RecallContract180Tests.cs:38` "after" olarak 1.8.0 fixture'ını okur;
    `RecallRulesAndShapeTests.cs:196` sürümü `1.9.0` diye sabitler, `:222` 1.9.0 belirteçleri; `:285-290` her `RecallErrorCodes` sabitinin üst
    düzey kayıtta olmasını ister. Dondurulmuş 1.9.0 = `6cd16ab00` blob'u, LF sha256 `7d97b13d…930c` (HEAD ile aynı). Web testlerinde 1.9.0
    yalnız yorumlarda geçer (sürüm iddiası yok).
  - Hata kodu emsali: 503 bağımlılık kodları `InventoryErrorCodes.cs:42` (`LEGAL_ENTITY_MASTER_UNAVAILABLE`), `QuarantineContract.cs:76`
    (`QUARANTINE_WORKFLOW_UNAVAILABLE`); recall sözleşmesinde bugün 503 yok.
  - Yetki: `ForwardCallerHeadersHandler.cs:21-35` `Authorization` ve `X-Correlation-Id` iletir, kiracı başlığı eklemez;
    `DefaultRolePermissionTemplate.cs:207-211` Görüntüleyici yalnız `read` eylemli kiracı anahtarlarını alır.
  - Mongo: `InventoryBsonMappings.cs:36-43` alan düzeyinde `IgnoreIfNull` sözleşmesi yok.
  - Sayfa yardımı: müşteriler adımı `Help.RECALLCASEDETAILS.Step4` (`PageHelpResource.en.resx:1672-1673`).
  - .NET `Guid.TryParseExact` baştaki / sondaki boşluğu kırpar — çalışma zamanı bilgisi, bu makinede koşularak doğrulanmadı; F-5'in çözümü
    buna dayanmaz (kuralın kendi uzunluk denetimi ölçülür).

### 19.3 Varsayımlar

- ASSUMPTION-FU01-1: `customerRef` sevkiyat kökünde (`getShipment` 200) tek değerdir; satır düzeyinde değildir (Q-2 ile teyit).
- ASSUMPTION-FU01-2: MVP-6 yalnız kendi kiracısının hesabını yazar; aksi veri hatası CRM 404 ile "Bulunamadı" olur, sızmaz (AC-11).
- ASSUMPTION-FU01-3: Geri çağırma bildirimi müşterinin CRM durumundan bağımsızdır (pasif / arşivli müşteri de çözülür ve gösterilir).
- ASSUMPTION-FU01-4 (Q-7'ye bağlı): `NotFound` (CRM'in kesin "yok" yanıtı) satırında kayıt komutları açıktır; satır ayırıcıyla ayırt edilir.

### 19.4 Açık sorular

| # | Soru | Sahip | Tarih |
|---|---|---|---|
| Q-1 | CRM çağrı yolu: kullanıcı token'ı Gateway üzerinden mi, servis kimliği mi? (servis kimliğinde kiracı nasıl taşınır) — **DL-1 kabul kapısı** (§7 D-3) | CRM / commercial-suite (Beste) | soruldu 2026-11-03; yanıt TBD |
| Q-2 | SHIPMENT 3.5.0 alanının kesin adı, tipi (`string`, `format: uuid`?), yeri (kök), küçük harf garantisi ve `shipToReference`'ın `"D"` biçimli GUID olmayacağı garantisi | MVP-6 CT | TBD (3.5.0 taslağıyla teyit) |
| Q-3 | MOD-0149 DL-1 (U-1 ucu) teslim tarihi ve sözleşmenin dondurulma tarihi | CRM / commercial-suite (Beste) | TBD |
| Q-4 | `crm.account.lookup-validation` hangi geri çağırma rollerine atanır (Q-1 = kullanıcı token'ı ise) — **DL-1 kabul kapısı** (§7 D-4) | Auth (Ali) | TBD |
| Q-7 | `NotFound` ("Bulunamadı") satırında bildirim / beyan / iade bağlama açık mı? r3 varsayımı **açık**: CRM kesin yanıt vermiştir, ad bir daha çözülmeyecektir; kapatmak silinmiş CRM hesabının müşterisine bildirimi hiç kaydedilemez kılar; satır ayırıcıyla ayırt edilir. Kapalı olursa AC-25 (c), AC-27 ve S-25b değişir — **ready-for-dev önkoşulu** | sahip (Beste) | sorulacak 2026-11-03 |
| Q-8 | Q-5'in "eski" tanımının FU01 öncesi (`CustomerRefSource = null`) anahtarlara daraltılması (§8.5; F-7, CT yönlendirmesi) sahip kararı Q-5'in kapsamını netleştirir — teyit? Hayır olursa AC-6 (a4), (a5), (c3), AC-23 ve S-6b, S-6c değişir — **ready-for-dev önkoşulu** | sahip (Beste) | sorulacak 2026-11-03 |

Q-5 ve Q-6 r2'de kapandı — §19.5 madde 8, 9.

### 19.5 Verilen kararlar (sahip — Beste / MVP-1 CT, 2026-11-03)

1. `customerRef` okuma ve sıra (customerRef → shipToReference → DOC), harf duyarsız karşılaştırma — §8.2, §8.3.
2. Ad ve kod CRM-ACCOUNT-LOOKUP 1.0.0 U-1'den; zaman aşımı ve önbellek mevcut okuyucu deseniyle (okuma başına 2 sn, önbelleksiz) — §8.6;
   r3'te istek başına toplam 2 sn bütçe eklendi (F-3, yazar kararı — inceleme konusu).
3. GUID gösterilmez; "Çözülemedi" / "Bulunamadı" — §8.8.
4. **SB-19:** eski anahtarlı satırlar yeni kimliğe otomatik bağlanmaz; rozetle ayrı satır — **kabul edilen sınır** (§8.5).
5. Kayıtlar yeni anahtarı kabul eder; eski kayıtlar değişmez — §8.4.
6. MOD-0177 CRM tüketicisi olarak yazılır (sözleşme CRM'in); izin ataması Auth işi — §7 D-4, D-5.
7. Yeni metinler 7 dilde — §11.
8. **Q-5 = HAYIR** (Beste, 2026-11-03): hiç müşteri kaydı olmayan vakada da eski anahtar CRM kimliğine yükseltilmez; kural kayıt varlığına
   bakmaz, revizyon kopyası için de geçerlidir — §8.5, AC-6, AC-23. "Eski"nin kapsamı r3'te FU01 öncesi anahtarlara daraltıldı (CT-5; teyit Q-8).
9. **Q-6 = HAYIR** (Beste, 2026-11-03): elle eklenen kapsam satırında CRM müşterisi seçilemez; satır `Manual` kaynaklı serbest metindir,
   `"D"` biçimli GUID metni 400 `INVALID_REQUEST` ile reddedilir (uygulama biçimi paket yazarının tasarımıdır — inceleme konusu), ekranda
   "CRM kimliği yok" rozeti ve `ManualHint` araç ipucu — §8.2, §8.8, AC-20, AC-23. FU-0177-FU01-SEL kapandı (§20).
10. **F-1 sahip kararı** (Beste, 2026-11-03): ad çözülemezse satırda sevkiyatın teslim yeri metni ve sevkiyat numarası görünür; ad çözülene
    kadar bildirim, tüketim beyanı ve iade bağlama kapalı ("CRM'e ulaşılamıyor, sonra tekrar deneyin", 7 dil); GUID hiç görünmez. Sunucu da
    reddeder: 503 `RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE` (kod seçimi yazarın, gerekçe §8.4) — §8.4, §8.8, AC-25, AC-27. "Sevkiyat numarası"
    olarak ledger hareket numarası alındı (her yerde mevcut ve GUID değil; MVP-6 sevkiyat kimliği GUID'dir) — §8.8.

### 19.6 CT kararları (MVP-1 CT, 2026-11-03 — verildi)

| # | Karar | Sonuç |
|---|---|---|
| CT-1 | `CustomerRefSource` rs-scope-v1 mührüne girer mi? | **Hayır** — biçim adı ve FU01 öncesi mühürler değişmez (§4.1, §8.5, AC-15) |
| CT-2 | Sözleşme numarası | RECALL **1.10.0** — `97cf7d170`'de sözleşme hâlâ `1.9.0` (§7 D-6) |
| CT-3 | Kanıt dışa aktarımı ve ad | Ad dışa aktarım anının anlık görüntüsüdür; §2 "ad saklanmaz" kuralı kayıt alanlarıyla sınırlanır; kesintide çözülemeyen satır "Çözülemedi" işaretiyle girer (§2, §8.10, AC-26) |
| CT-4 | D-4 (izin) ve Q-1 (çağrı yolu) | **DL-1 kabul kapısı** (§7, §22) |
| CT-5 | Q-5 "eski" kapsamı (F-7) | FU01 öncesi (`null` kaynak) anahtarlarla sınırlı; FU01 sonrası geçici `Document` / `ShipTo` satırı `Draft`'ta `Crm`'e geçebilir (§8.5) — sahip teyidi Q-8 |
| — | Bulgular a66dd353 | hepsi kabul; r3'te tek geçiş (S-101) |
| — | `--discover` bulguları (gateway crm 5061 / recall 5067, üretici-tüketici) | mükerrer sayılmaz — CT kabul etti (§19.1) |
| — | Registry satırı | CT ekledi, `8ba30d12b` |

### 19.7 Bağımsız inceleme 01 — kapanış tablosu (tur 1 → r3)

Bulgu dosyası `docs/records/reviews/MOD-0177-FU01/2026-11-03-a66dd353.md` (`2194c5ce6`; sahip ve CT kararları `97cf7d170`; dosya özeti
`9eb17822…cbd1fa9`, `git show 97cf7d170:<yol> | sha256sum`); hedef r2 pack sha256 `a66dd353…42665a67`, kaynak `942c73153`; UYGUN DEĞİL,
1 ENGEL, 7 ORTA, 6 DÜŞÜK. Kaynak `942c73153` → `97cf7d170` arasında Recall kodu, Web ve sözleşmeler değişmedi (`git diff --stat` boş). Sahip
kararları Q-5, Q-6, CT-1, CT-2 yeniden açılmadı. Her düzeltme kodla ölçüldü (§19.2 r3 ölçümleri). Ek düzeltme (`pack-review-common-findings`
§4): r2'de kabul aracı (`acceptance_record.py` `parse_pack`) AC-17'yi okumuyordu (`(zorunlu, **kabul kapısı**)` biçimi) ve çok satırlı
ölçütlerin yalnız ilk satırını alıyordu; r3'te her AC tek satır ve AC-17 `(zorunlu)` biçiminde.

| Bulgu | Düzeltme | Bölümler |
|---|---|---|
| F-1@a66dd353 (ENGEL) | Sahip kararı uygulandı: `Unresolved` / `NotFound` satırında ayırıcı (`shipTo` + ledger hareket numarası; `SourceDocumentId` değil, GUID biçimli değer gösterilmez); `Unresolved` satırda bildirim / beyan / iade bağlama ekranda kapalı ve sunucuda 503 `RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE`, yazım 0, tekrar denetimi önce (kod gerekçesi: bağımlılık kesintisi, emsal 503'ler); kanıt dışa aktarımı adı anlık görüntü olarak taşır, `Unresolved` satır işaretli girer, dışa aktarım reddedilmez (CT-3); §2 kuralı kayıt alanlarıyla sınırlandı; D-4 ve Q-1 DL-1 kabul kapısı (CT-4); `NotFound` komutları Q-7'ye bağlandı | §1, §2, §3, §7, §8.4, §8.6, §8.7, §8.8, §8.9, §8.10, §11, §13, §14, §19.4; AC-10, AC-18, AC-25, AC-26, AC-27 |
| F-2@a66dd353 | Sıra seçildi: parmak izi ve tekrar denetimi bugünkü yerinde (kapsamdan önce); parmak izinde `"D"` biçimli giriş koşulsuz kanonik, eşleşmede `Crm` kaynak koşulu; `"D"` biçimli eski anahtarın parmak izi farkı yazıldı ve AC-5 (c2) vektörüyle ölçülür, gerçek veride yokluğu AC-20 (f) sayımıyla (her revizyon ve silinmiş satır dahil) | §8.3; AC-5, AC-20 |
| F-3@a66dd353 | `TotalBudget = 2 sn` (tek bağlı iptal belirteci), başlamamış okuma başlamaz, kısmi sonuç kuralı; `getCaseCustomers`'ta CRM çözümü MVP-6 okumasıyla eşzamanlı; AC-12'ye 17 hesaplı yanıtsız CRM, kısmi sonuç ve eşzamanlılık fixture'ları, sabotajlar S-12c…S-12e | §8.6, §13; AC-12, AC-14; O-3 |
| F-4@a66dd353 | 1.9.0 fixture'ı (`6cd16ab00` blob, LF sha256 `7d97b13d…930c`), `RecallContract190Tests` "after" dosyasının fixture'a çevrilmesi, `RecallRulesAndShapeTests` `:196` / `:222` güncellemesi Repo Scope'ta; AC-16 metni "değiştirilmeden" yalnız 1.2.0…1.8.0 testleri için | §5, §6; AC-16 |
| F-5@a66dd353 | `IsDShaped` kuralına kırpılmış değerde 36 karakter denetimi eklendi; S-20b bu denetimi ham girdiye uygulayan gerçek sabotaj (kırmızı: (d) 200); aynı denetim komut girişi için AC-3 (d) + S-3d | §8.3, §17.1; AC-3, AC-20 |
| F-6@a66dd353 | `Draft`'ta `Crm` A → `Crm` B: anahtar B'ye güncellenir (korunacak kayıt yok; onaya güncel müşteri gider); AC-6 (d) ve S-6e | §8.5, §8.9; AC-6 |
| F-7@a66dd353 | "Eski" = yalnız FU01 öncesi (`null`) anahtar (CT-5, gerekçeli; sahip teyidi Q-8); FU01 sonrası `ShipTo` / `Document` `Draft`'ta `Crm`'e geçer; `Active`'de hiçbir kopya yükseltilmez; ayrı durum `NoCrm`, rozet "CRM kimliği yok" ve `NoCrmHint` (7 dil); AC-6 (a4), (a5), (c3), AC-23 (S-23d) | §4.1, §8.5, §8.7, §8.8, §11, §19.4; AC-6, AC-20, AC-23 |
| F-8@a66dd353 | D-3 (Q-1) ve D-4 (Q-4) DL-1 kabul kapısı; "yalnız DI değişir, AC-11 aynen geçer" iddiası kaldırıldı; servis kimliğinin kiracı taşıma (CRM taslağı §1) ve yetki sonuçları yazıldı — seçilirse paket revizyonu | §7, §14, §18, §19.4, §22 |
| F-9@a66dd353 | AC-17 D-1 kapanana dek adlı Skip gerekçesiyle atlanır; DL-1 kabul commit'i Skip'i kaldırır; tam paket koşusunda kırmızı değil | §16, §17, §17.1; AC-17 |
| F-10@a66dd353 | `DrillRun.customers` 1.10.0'da anahtar dizisi olarak adlandırıldı; §8.8'de açık tatbikat istisnası; AC-10 (d) sunucu, AC-22 `drill-view.js` (S-22d) | §5, §8.7, §8.8; AC-10, AC-22 |
| F-11@a66dd353 | Anahtar listesi aile bazında (ortak / yalnız Web); CSV / PDF işareti `LegacyMarked` / `NoCrmMarked` yalnız `Crm` satırı olan vakada (FU01 öncesi kanıt çıktısı aynı kalır); `PageHelpResource` 7 dil DL-2 Repo Scope'ta | §5, §8.8, §8.10, §11, §23; AC-19, AC-26 |
| F-12@a66dd353 | AC-20 (f) sayımı: kabul ortamı + her hedef ortamda geçişten hemen önce ve hemen sonra, kiracı başına, sahibi MVP-1 CT (geçişi yapan imzalar); sıra 2'de `"D"` biçimli `shipToReference` bekçisi (AC-2 (h), S-2c); Q-2 / D-1 teyidine `shipToReference` garantisi | §7, §8.2, §8.3, §12, §19.4; AC-2, AC-20 |
| F-13@a66dd353 | Değişiklik notu "additive + one input restriction"; 1.10.0 testi 1.9.0 fixture'ına karşı tek girdi daraltmasını ve eklemeleri tam liste olarak ölçer (S-16b) | §8.2, §8.7; AC-16 |
| F-14@a66dd353 | `null` kaynak yapışkan: eski → eski geçişte anahtar bugünkü gibi güncellenir, kaynak `null` kalır (aynı değerde de); `Draft` yeniden yazımında bayt eşitliği beklenmez, ne beklendiği yazıldı; AC-6 (c), (c2), S-6d | §4.1, §8.5; AC-6 |

## 20. Follow-up Items

| ID | Konu | Sahip |
|---|---|---|
| O-1 | **Gözlem (ebeveyn, bu FU'nun kapsamı dışı):** bugün yeniden izlemede MVP-6 o an erişilemezse saklı shipTo anahtarı `DOC:` anahtarına düşer (`RecallTraceService.cs:141`); o müşterinin kayıtları yetim kalmaz (Draft'ta kayıt yok) ama etiket değişir. FU01 yalnız CRM anahtarını korur (§8.5). Eski → eski düşüşün düzeltilmesi ayrı karar | MOD-0177 / CT |
| FU-0177-CUST-01 | Ebeveyn §20 satırı bu paketle karşılanır (MVP-6 party id yerine CRM `Account.Id` + U-1) — ebeveyn satırının güncellenmesi CT adımı | CT |
| O-2 | **Gözlem (veri):** FU01 öncesi `"D"` GUID biçimli eski anahtar aynı vakadaki `Crm` satırıyla çakışabilir ve parmak izi değişir (§8.3). Ölçüm AC-20 (f) sayımı; sıfır değilse DL-1 kabul edilmez ya da geçiş durur, satırların işlenişi CT kararı (göç yok) | CT |
| O-3 | **Gözlem (ebeveyn):** `getCaseCustomers`'ın MVP-6 okuması toplam bütçesizdir (tavan(N/8) × 2 sn, `ShipmentBundleHttpReader.cs:36-49`); FU01 CRM payını ≤ 2 sn'de tutar ve eşzamanlı koşar, MVP-6 payını değiştirmez | MOD-0177 / CT |
| O-4 | **Ölçülmedi:** Platform denetim günlüğü görüntüleyicisinin denetim ayrıntısındaki `customerRef`'i (CRM anahtarında GUID) kullanıcıya gösterip göstermediği. Gösteriyorsa §8.8 kuralı dışında bir yüzeydir; Platform sahibine sorulur | CT / Platform |
| FU-0177-FU01-SEL | Elle eklenen kapsam satırında CRM müşteri seçici — **kapandı, açılmaz**: Q-6 = HAYIR (Beste, 2026-11-03). Yeniden istenirse yeni sahip kararı ve yeni FU gerekir | — |
| FU-0177-FU01-REBIND | Eski anahtarlı kayıtların CRM kimliğine **elle**, denetimli bağlanması (SB-19'un otomatik yasağı korunarak) — ürün kararı | Ürün / CT |

## 21. Audited Events

Yeni yazma komutu ve yeni denetim olayı yok. Mevcut olaylar (ebeveyn §8.12): `recall.customer.notified`, `recall.consumption.declared`,
iade bağlama — ayrıntıdaki `customerRef` CRM anahtarında kanonik GUID'dir (kayıtta kalır; ad girmez). Davranışı değişen mevcut yazma komutları:
`addScopeItems` (`RecallContract.AuditScopeItemsAdded`, yol ebeveynin) — başarılı istekte denetim satırı değişmez, Q-6 reddinde (400) hiç
yazılmaz (AC-20 (e)); `recordNotification`, `recordConsumption`, `linkReturn` — çözüm denetimi reddinde (503) hiç yazılmaz (AC-25 (b));
`submitScope` ve `reviseScope` — denetim satırları değişmez (`CustomerRefSource` denetim ayrıntısına girmez); `createEvidenceExport` —
`recall.evidence.exported` satırı değişmez, ad ona girmez (AC-26 (e)). CRM okuması AUD-001 okuma olayı üretmez (CRM taslağı §1).

## 22. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | arka uç: customerRef okuma + anahtar sırası + ad çözümü + çözüm denetimi + kayıtlar + kanıt anlık görüntüsü + RECALL 1.10.0 + 7 dil metinleri | Owned Objects, Entity Fields, Repo Scope, Dependencies, Müşteri anahtarı sırası, Anahtar biçimi ve tekrar parmak izi, Kayıtlar, Yeniden izlemede anahtar kararlılığı (SB-19), Ad çözümü (CRM-ACCOUNT-LOOKUP U-1), Sözleşme — RECALL 1.10.0, Gösterim kuralı, Kanıt paketi ve ad anlık görüntüsü, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Audited Events | E4 | plan satırı yok | MVP-1 |
| DL-2 | ekran: ad + kod, durum metinleri ve ayırıcı, kapalı komutlar, rozetler | Gösterim kuralı, Frontend File Contract, Layout & Shell Contract, Page Help (UXH-001) | E4 | plan satırı yok | MVP-1 |

Kabul ölçütleri: DL-1 → AC-1…AC-20, AC-25, AC-26; DL-2 → AC-21…AC-24, AC-27. **DL-1 kabul kapısı:** D-1 (SHIPMENT 3.5.0 FROZEN, AC-17 Skip'siz
yeşil, Q-2 teyidi), D-2 (CRM U-1 şekli teyitli ya da sözleşme FROZEN), D-3 (Q-1 = kullanıcı token'ı; aksi paket revizyonu), D-4 (Q-4 yanıtlı ve
kabul ortamında rol ataması yapılmış) ve AC-20 (f) veri sayımı = 0 (kabul ortamı; O-2). Canlıya geçişte AC-20 (f) her hedef ortamda geçişten
hemen önce ve hemen sonra yinelenir (sahibi MVP-1 CT). D-1…D-4 kapanmadan DL-1 ikizlerle kodlanabilir, kabul edilemez. DL-2 DL-1'e dayanır.
FU01'in kapanışı ayrıca E5 smoke ister (§17; MOD-0149 DL-1 ve MVP-6 3.5.0 yayında). 7 dil metinleri parite bekçisi gereği DL-1'dedir (§11);
"Gösterim kuralı" iki dilime bölünür: DL-1 sunucu çıktıları (API, CSV, PDF), DL-2 ekran. Ready-for-dev önkoşulu Q-7 ve Q-8'dir (§18) — dilim
kapısı değildir.

## 23. Page Help (UXH-001)

Yeni sayfa yok. Vaka detayı "?" panelindeki müşteriler adımı (`Help.RECALLCASEDETAILS.Step4`, `PageHelpResource.{7}.resx`) iki cümle alır:
"Eski kayıt" / "CRM kimliği yok" rozetleri ve CRM'e ulaşılamadığında bildirim, tüketim beyanı ve iade bağlamanın kapalı olduğu (DL-2; yeni tur yok).
