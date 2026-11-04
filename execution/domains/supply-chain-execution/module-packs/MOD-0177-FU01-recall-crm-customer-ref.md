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
status: ready-for-dev
approval_status: pending
revision: "r5 (2026-11-04) — bağımsız inceleme 01 tur 3 (hedef r4 807c8dc2, KOŞULLU, 0 ENGEL, 2 ORTA, 1 DÜŞÜK) F-1…F-3 ve CT kararı (MVP-1 CT, 2026-11-04) tek geçişte kapatıldı (§19.9; S-101); yeniden inceleme (CT-APR-02) ve onay satırı (CT-APR-01) ister. Önceki: r4 (807c8dc2) — tur 2 (fbafd3af) kapanışı (§19.8); r3 (fbafd3af) — tur 1 (a66dd353) kapanışı (§19.7)."
risk: HIGH
risk_reason: "GxP geri çağırma kaydı: müşteri anahtarı bildirim, tüketim beyanı, iade bağlama ve etkinlik hesabının birleştirme anahtarıdır — yanlış anahtar kaydı yetim bırakır ve etkinliği yanlış gösterir; servisler arası okuma (CRM 5061) ile kiracı izolasyonu ve kişisel veri sınırı; kapsam mührü (rs-scope-v1) customerRef'i içerir; kanıt paketinin saklı baytları müşteri adını dışa aktarım anının anlık görüntüsü olarak taşır; sözleşme değişikliği (RECALL 1.10.0) ve dondurulmamış iki üretici sözleşmesine bağımlılık (SHIPMENT-BUNDLE 3.5.0, CRM-ACCOUNT-LOOKUP 1.0.0)"
status_note: "ready-for-dev r5 (2026-11-04) — künye S-49 gereği ready-for-dev (onay satırı ready-for-dev künyeli sürüme yazılır); approval_status pending: kod CT-APR-02 yeniden inceleme (r5) ve CT-APR-01 onay satırından sonra yazılır. ready-for-dev geliştirme başlangıcı ya da kabul değildir; DL-1 kabul kapıları açık kalır. Bağımsız inceleme 01 tur 3'ün (807c8dc2, KOŞULLU) üç bulgusu CT kararıyla (MVP-1 CT, 2026-11-04) tek geçişte kapatıldı (S-101, §19.9): F-1 — Export kipine devre kesici (ordinal sırayla 8'lik dalgalar; bir dalganın bütün okumaları zaman aşımı / 5xx / ulaşılamaz ise kalan hesaplar çağrılmadan Unresolved; sağlıklı gecikmeli CRM'de hepsi çözülür), Q-9 ölçümü kanıt olarak yazıldı (Ocelot 24.1.0 tanımsız rota zaman aşımı 90 sn, Web HttpClient 100 sn, kesicisiz eşik ~361 yanıtsız hesap) (§8.6, AC-14, AC-29); F-2 — recall.evidence.export ve recall.trace.export AC-30 rol kontrolüne girdi; vakada / izde Forbidden müşteri varken previewEvidence, createEvidenceExport ve exportTrace 403 RECALL_CUSTOMER_DIRECTORY_FORBIDDEN ile reddedilir, izin eksikliği kalıcı GxP dosyasına girmez (§8.6, §8.7, §8.10, §14, AC-16, AC-18, AC-26, AC-27, AC-29, AC-30; yeni metin Recall.Customer.ExportForbidden 7 dil); F-3 — müşteri sayfalarının ordinal anahtar sırası (Crm müşterilerinde fiilen GUID sırası, adla sıralama ve arama yok) kabul edilen sınır (§8.8, §20 FU-0177-FU01-FIND). Q-9 kapandı. Kimlik ön kontrolü 8ba30d12b'de çıkış 0 (değişmedi). Açık: Q-1…Q-4, D-1…D-5; DL-1 kabul kapısı D-1, D-2, D-3, D-4, AC-20 (f) veri sayımı ve AC-30 rol kontrolü (§22). 31 AC (DL-1 AC-1…AC-20, AC-25, AC-26, AC-28, AC-29, AC-30; DL-2 AC-21…AC-24, AC-27, AC-31)."
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
> services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization` boş — satır numaraları üç tabanda aynıdır. **r4 ölçümleri
> `a4bb047d9`'dandır**; `git diff --stat 8fa9ba40f a4bb047d9` yalnız tur 2 bulgu dosyasını, `execution/approvals/reviews.tsv`'yi ve Recall
> dışı bir PSS taslağını (`docs/analysis/contracts/drafts/record-code-allocate-v1.0-proposal.md`, 1 satır) gösterir — Recall kodu, Web,
> Web testleri ve `recall.openapi.yaml` aynıdır; satır numaraları tur 2 bulgu dosyasının ölçüm tabanıyla aynıdır. **r5 ölçümleri
> `76f93c261`'dendir**; `git diff --stat 9686cafd9 76f93c261 -- services/Diten.InventoryService frontend/Diten.Web frontend/Diten.Web.Tests
> docs/analysis/contracts gateway services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization` boş (aradaki commit'ler yalnız
> inceleme dosyaları, `reviews.tsv` ve Recall dışı iki kayıt) — satır numaraları tur 3 bulgu dosyasının ölçüm tabanıyla (`9686cafd9`) aynıdır.

## 1. Module Summary

Recall'da müşteri bugün opak bir metinle tanınır: sevkiyat okunduysa MVP-6'nın `shipToReference` değeri, okunamadıysa ya da belge MOD-0183
dışındaysa `"DOC:" + sourceDocumentId` (`RecallTraceEngine.cs:331`, `:375`; `RecallTraceService.cs:93`). Ekranda bu metin ya da belge numarası
görünür; ad yoktur (ebeveyn §8.6: "No customer master link in MVP-1").

Bu FU üç şey yapar:

1. **Anahtar (DL-1):** SHIPMENT-BUNDLE 3.5.0'da MVP-6'nın ekleyeceği `customerRef` (MOD-0149 `Account.Id`) doluysa müşteri anahtarı odur;
   boşsa bugünkü sıra korunur. CRM anahtarı kanonik küçük harf GUID ("D") olarak saklanır; giriş büyük/küçük harf duyarsız eşleşir.
2. **Ad (DL-1):** CRM anahtarlı müşterinin adı ve kodu okuma anında CRM-ACCOUNT-LOOKUP 1.0.0 U-1'den çözülür (önbelleksiz). Ekran okumaları
   adı yalnız **gösterilen müşteri sayfası** (25 müşteri) için çözer — sayfa başına toplam 3 sn; kanıt dışa aktarımı, önizlemesi ve iz CSV'si
   bütün müşterileri toplam tavansız çözer (CT kararı, 2026-11-04; §8.6), **devre kesiciyle** (8'lik dalgalar; bir dalganın bütün okumaları
   zaman aşımı / 5xx / ulaşılamaz ise kalan hesaplar çağrılmadan "Çözülemedi" — CT kararı F-1@807c8dc2). Çözülemezse "Çözülemedi", çağıranın
   CRM izni yoksa "İzin eksik", hesap yoksa / silinmişse / başka kiracıdaysa "Bulunamadı"; bu üç durumda satır, sevkiyatın teslim yeri metni ve
   sevkiyat hareket numarasıyla ayırt edilir. **GUID hiçbir zaman gösterilmez** — kayıtta ve API'nin anahtar alanında kalır. **Ad çözülemeyen
   ("Çözülemedi") ya da izni eksik ("İzin eksik") müşteriye bildirim, tüketim beyanı ve iade bağlama yapılamaz** — ekranda kapalı, sunucuda
   503 / 403 (sahip kararı F-1, CT kararı F-2@fbafd3af). **İzni eksik kullanıcının dışa aktarımı yapılmaz:** vakada ya da izde "İzin eksik"
   müşteri varken kanıt önizlemesi, kanıt dışa aktarımı ve iz CSV'si 403 ile reddedilir — izin eksikliği kalıcı GxP dosyasına girmez (CT kararı
   F-2@807c8dc2).
3. **Ekran (DL-2):** müşteri görünen her yerde ad + kod; FU01 öncesi anahtarlı satırlar "Eski kayıt", FU01 sonrası CRM kimliksiz satırlar
   "CRM kimliği yok" rozetiyle ayrı satır (SB-19).

Bildirim, tüketim beyanı ve iade bağlama yeni anahtarı kabul eder; eski kayıtlar değişmez (yeniden yazma, göç, otomatik bağlama yok).
FU01 öncesi yazılmış anahtar hiçbir vakada CRM kimliğine yükseltilmez — vakada hiç müşteri kaydı olmasa da (Q-5 = HAYIR; kapsamı §8.5).
Elle eklenen kapsam satırında CRM müşterisi seçilemez; GUID biçimli serbest metin reddedilir (Q-6 = HAYIR). Yeni izin anahtarı ve yeni Gateway
rotası yoktur; yeni hata kodu iki tanedir (`RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE` 503 — üç kayıt komutu; `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN`
403 — üç kayıt komutu ve üç dışa aktarım ucu).

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
  Saklı dosyaya "İzin eksik" girmez: dışa aktaranın CRM izni eksikse dışa aktarım reddedilir (CT kararı F-2@807c8dc2, §8.10).

## 3. Owned Objects

Yeni koleksiyon yok. Değişen nesneler:

| Nesne | Koleksiyon | Değişiklik |
|---|---|---|
| `RecallScopeItem` | `recall_scope_items` | eklenen alan `CustomerRefSource` (§4.1) |
| `TraceNode` (bellek içi) / `TraceShipmentInfo` | — | eklenen alanlar (§4.2) |
| `RecallCustomerNotification`, `RecallConsumptionDeclaration`, `RecallReturnLink` | mevcut | **alan eklenmez**; `CustomerRef` CRM anahtarında kanonik GUID taşır |
| `RecallEvidenceExport` / `RecallEvidenceFile` | mevcut | **alan eklenmez**; saklı baytlar adı anlık görüntü olarak taşıyabilir (§2, §8.10) |
| `RecallErrorCodes` (`RecallRules.cs:11`) | — | eklenen kodlar `RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE` (503) ve `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN` (403) (§8.4) |
| `RecallCaseEvent` (`ReturnLinked` olayı) | mevcut | **alan eklenmez**; `Crm` satırına bağlanan iadenin olay metni müşteri anahtarını taşımaz (§8.4) |
| RECALL yanıt şemaları | `recall.openapi.yaml` | eklemeli `customer` nesnesi, `customerPage` sorgu parametresi, `DrillRun.customers` adlandırması, iki yeni hata kodu (üç kayıt komutunda ikisi, üç dışa aktarım ucunda `…_FORBIDDEN`) (§8.7) |

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
  `R3Handlers.cs` (yalnız `LinkRecallReturnHandler` — eşleşme, çözüm denetimi ve `:438` olay metni — ve `ReviseRecallScopeHandler` kopyası `:527-533`),
  `CaseCommandHandlers.cs` (yalnız `AddRecallScopeItemsHandler` Shipment dalı `:361-382` ve satır kurulumu `:403-427` — Q-6),
  `TraceHandlers.cs` (yalnız `ExportRecallTraceHandler` dili, ad çözümü ve `Forbidden` reddi — reddi denetim satırından (`:103-107`) önce —
  ve `DiffRecallScopeHandler` müşteri sayfası, §8.6, §8.10),
  `EvidenceHandlers.cs` (gerekirse yalnız müşteri çözümünün kurucuya geçirilmesi; `Forbidden` reddi kurucunun bugünkü başarısızlık dönüşüyle
  gelir — `BuildAsync` `(document, code, error, status)`, `RecallEvidence.cs:161`; işleyicilerde `:56-58`, `:97-99` — saklama akışı `:100-156`
  değişmez)
- `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/Recall/Queries/RecallQueries.cs` (`:31`, `:45`, `:50`, `:53` —
  isteğe bağlı `CustomerPage`), `Commands/RecallCommands.cs` (`:169` `RunRecallTraceCommand` ve `:183` `DiffRecallScopeCommand` — `CustomerPage`;
  `:176` `ExportRecallTraceCommand` — `Language`)
- `services/Diten.InventoryService/src/Diten.InventoryService.Api/Controllers/Recall/RecallController.cs` (yalnız altı okuma ucunda
  `customerPage` bağlama ve `exportTrace`'e `EvidenceLanguage()` (`:218`) geçirilmesi)
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
  `ShipmentReaderTests.cs` eki; CRM test ikizi `RecallTestInfrastructure.cs` yanında; `RecallTestInfrastructure.cs`
  `RecallContractValidatingHandler.Operations` listesine (`:141-191`) bugün olmayan `GET /api/recall/cases/{id}/customers` ve
  `GET /api/recall/cases/{id}/effectiveness` satırları (ölçüldü: listede R7a okumaları yok) — AC-16'nın şema doğrulaması bu iki uçta da koşsun
- **Sözleşme testleri (F-4@a66dd353, F-3@fbafd3af):** `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Contract/Fixtures/recall-v1.9.0.openapi.yaml`
  (yeni — dondurulmuş 1.9.0 dosyası, git blob `6cd16ab00:docs/analysis/contracts/recall.openapi.yaml`, LF sha256
  `7d97b13de27e662cc7f2775ca7fac3458d62bb749ff12e0d370fa768a47f930c`); `Recall/RecallContract190Tests.cs` (yalnız "after" dosyası canlı
  sözleşmeden fixture'a çevrilir, `:40` — emsal `RecallContract180Tests.cs:38` `after = RecallContract190Tests.FixtureV180Path`);
  `Recall/RecallRulesAndShapeTests.cs` (yalnız `:196` sürüm satırı `1.10.0` ve 1.10.0 değişiklik notu belirteçleri, `:222` deseninde).
  Canlı sözleşmeyi okuyan öteki testler **değişmeden** yeşil kalmalıdır (§6 listesi): `RecallESignTests.cs:128` / `:583-588`
  (`x-signable-actions` — FU01 dokunmaz), `RecallRulesAndShapeTests.cs:285-305` (her `RecallErrorCodes` sabiti üst düzey kayıtta — iki yeni
  kod 1.10.0'da kayda girer)

**DL-2 (ekran):**
- `frontend/Diten.Web/wwwroot/assets/js/Recall/case-detail.js`, `trace-view.js`, `trace.js` (müşteri sayfası düğmeleri; `Forbidden` düğüm
  varken CSV bağlantısı yerine `Recall.Customer.ExportForbidden`, `:95-97`, §8.8), `recall.l10n.js`. `evidence.js` **değişmez**: önizleme
  ve dışa aktarım hatası bugün de `ui.errorText(reply)` ile kodun metnini gösterir (`evidence.js:107`, `:129`; `recall-ui.js:49-55`)
- `frontend/Diten.Web/Controllers/RecallController.cs` (yalnız `CaseData` sorgu iletimi `:247` — bugün yalnız `timeline` için `page` /
  `pageSize` iletir; FU01 `customers`, `scope`, `diff`, `returns`, `effectiveness` parçaları için `customerPage`'i iletir — ve `TraceData`
  `:109-134` için aynısı; `SendFileAsync` (`:842-870`) `Accept-Language`'ı iletir — bugün yalnız `SendAsync` iletir (`:818`), bu yüzden
  `exportTrace` dosyası servise dilsiz gider ve servis `en`'e düşer (`EvidenceLanguage()`, servis `RecallController.cs:218-222`)). Hata
  yanıtları değişmez: `Failed` → `Refuse(status, code)` (`:933-955`) servisin durumunu ve kodunu korur, iletiyi `Recall.Errors.{code}` ile
  verir (ölçüldü — 503 ve 403 gövdeleri ekrana kodla ulaşır)
- `frontend/Diten.Web/Resources/PageHelpResource.{en,tr,fr,es,zh,ar,ru}.resx` (yalnız `Help.RECALLCASEDETAILS.Step4`, §23)
- `frontend/Diten.Web.Tests/JavaScript/Node/recall-fu01.test.mjs` (yeni), `frontend/Diten.Web.Tests/Recall/` (gerekirse; sahte ağ geçidi
  yanıtları `RecallContractFakes.Bound` ile canlı sözleşmeye bağlıdır — DL-2 önkoşulu §22)

`drill-view.js` değişmez (`customers` alanını bugün çizmiyor, ölçüldü; AC-22 bunu sabitler).

## 6. Protected Paths

`.antigravity/**`, `gateway/Diten.ApiGateway/ocelot.json` (yalnız integration-agent), `_Layout.cshtml`, Archive;
`services/Diten.CrmService/**` (commercial-suite), `services/Diten.SupplyChainService/**` (MVP-6), Auth servisi;
`docs/analysis/contracts/shipment-bundle.openapi.yaml` (MVP-6 tek yazıcı), `docs/analysis/contracts/drafts/crm-account-lookup-validation-v1.0-proposal.md`
(CRM tek yazıcı). MOD-0173 / 0174 / 0175 / 0176 dosyalarına dokunulmaz. Mevcut `RecallContract120Tests` … `RecallContract180Tests`
değişmez — bunlar yalnız dondurulmuş fixture'ları okur; sürüm testleri arasında canlı dosyayı okuyan tek test `RecallContract190Tests`'tir
(`:40`). **Canlı `recall.openapi.yaml`'ı okuyan öteki testler (F-3@fbafd3af, `a4bb047d9`'da ölçüldü — r3'teki "yalnız RecallContract190Tests"
cümlesi yanlıştı):**

| Okuyan | Yer | Okuduğu | FU01 etkisi |
|---|---|---|---|
| `RecallContractTests` | `RecallRulesAndShapeTests.cs:177-181` (`:179` dosya), `:196` sürüm, `:200-223` not belirteçleri, `:285-305` hata kodları | sürüm, değişiklik notu, uç / izin listeleri, `x-error-codes` | `:196` ve 1.10.0 belirteçleri değişir (Repo Scope); iki yeni kod üst düzey kayda girmezse `:285-290` kırmızı |
| `RecallContractYaml` | `RecallESignTests.cs:128`, `:583-588` | `x-signable-actions` | değişmez (FU01 imza eylemi ve imza alanı eklemez — `RecallSignableSubjects.cs:60-95`'te müşteri anahtarı yok) |
| `RecallContractValidatingHandler` | `RecallTestInfrastructure.cs:132-192`, şemalar `ContractSchemas.cs:147` (`ValidateRecall`, `:152`) | 2xx gövdeler adlı şemaya, öteki durumlar `Error` şemasına | 1.10.0 şemaları (`customer`) canlı dosyada olmadan DL-1 yanıtları kırmızı; R7a okumaları listeye eklenir (Repo Scope) |
| Web `ContractRoutes` + `RecallContractFakes.Bound` | `frontend/Diten.Web.Tests/Contracts/ContractRoutes.cs:29`, `:73-75`, `:187`; `Recall/RecallContractFakes.cs:25-40` | **yalnız 2xx JSON** gövdeler (kapalı nesneler); 2xx dışı yanıtlar denetlenmez (`RecallContractFakes.cs:28`, `ContractRoutes.cs:75`) | DL-2 sahteleri `customer` nesnesi taşıyan 2xx gövdesi döndüğünde canlı sözleşmede 1.10.0 şemaları gerekir — **DL-2 önkoşulu** (§22). Sahtelerin 503 / 403 gövdeleri şemaya karşı denetlenmez; bu gövdeler AC-27'de sözleşmenin `Error` biçimiyle (`error.code`) yazılır |

## 7. Dependencies

| # | Bağımlılık | Durum (2026-11-03) | Bu pakete etkisi |
|---|---|---|---|
| D-1 | **SHIPMENT-BUNDLE 3.5.0** — `getShipment` 200 yanıtında isteğe bağlı `customerRef` (MOD-0149 `Account.Id`, GUID "D", küçük harf; yalnız ekleme) | yok; bugün **3.4.0** FROZEN (`shipment-bundle.openapi.yaml:15`), `customerRef` / `partyId` geçmiyor. Alan adı ve tipi **kesin değil — MVP-6 taslağıyla teyit edilecek** (Q-2) | **DL-1 kabul kapısı:** 3.5.0 FROZEN, alan teyitli ve `shipToReference`'ın `"D"` biçimli GUID olmayacağı MVP-6'ca yazılı (Q-2, F-12). Okuyucu alan adını tek sabitte tutar (§8.2); ad değişirse tek satır |
| D-2 | **CRM-ACCOUNT-LOOKUP 1.0.0 U-1** `GET /api/crm/accounts/{accountId:guid}/lookup-validation` | taslak (`drafts/crm-account-lookup-validation-v1.0-proposal.md`); **uç kodda yok** (MOD-0149 DL-1) | DL-1 E4 sözleşme metninden kurulan ikizle; **DL-1 kabul kapısı:** sözleşme FROZEN ya da U-1 şekli (alanlar, 404 kuralı, `contractVersion`) CRM sahibince teyitli (teyit edilen taslağın sha256'sı kabul kaydına). Uçtan uca E5: MOD-0149 DL-1 yayında |
| D-3 | Çağrı yolu: kullanıcı token'ı Gateway üzerinden mi, servis kimliği mi | **CRM'e soruldu — açık** (Q-1) | **DL-1 kabul kapısı (CT kararı, 2026-11-03; F-8).** Kod varsayılan kullanıcı token'ıyla (`ForwardCallerHeadersHandler`, mevcut okuyucu deseni) yazılır ve kabul edilir. Servis kimliği seçilirse bu **yalnız DI değişikliği değildir**: CRM taslağı §1 kiracının başlık, parametre ya da gövdeyle verilmesini yasaklar (servis kimliğinde kiracı token'dan çözülemez) ve CRM izni kullanıcıdan bağımsızlaşır (her recall okuyucusu adı görür; AC-18'in anlamı değişir) — paket revizyonu (yeni AC'ler, CRM sözleşme değişikliği) gerekir, DL-1 kabul edilmez |
| D-4 | `crm.account.lookup-validation` izninin geri çağırma rollerine atanması | Auth işi (MOD-0018, **Ali**) — XMC-001; Q-1 = kullanıcı token'ıysa gerekir. Ölçüldü: varsayılan Görüntüleyici rolü yalnız `read` eylemli anahtarları alır (`DefaultRolePermissionTemplate.cs:207-211`); `lookup-validation` eylemi kendiliğinden verilmez | **DL-1 kabul kapısı (CT kararı, 2026-11-03; F-8):** Q-4 yanıtlı ve kabul ortamında atama yapılmış. Atanmamış kullanıcıda CRM 401 / 403 → **"İzin eksik"** (`Forbidden`, F-2@fbafd3af); okumalar çalışır (AC-18) ama o kullanıcı CRM anahtarlı müşteriye bildirim / beyan / iade bağı **kaydedemez** (403 `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN`, §8.4) ve CRM anahtarlı müşterisi olan vakanın kanıt paketini ve böyle bir düğümü olan iz CSV'sini **dışa aktaramaz** (aynı 403, §8.10; F-2@807c8dc2) — bu yüzden kapı. **Canlı geçiş kapısı (CT kararı, 2026-11-04):** ebeveynin rol şablonu yalnız yeni kiracılara uygulanır (ebeveyn §14.3 ROLE1), mevcut kiracıların rolleri kiracıya özgüdür; bu yüzden **her hedef ortamda, kiracı başına** rol ataması FU01 kod geçişinden önce kontrol edilir (AC-30) — eksik atamalı kiracı varsa o ortamın geçişi durur |
| D-5 | CRM sözleşmesinin tüketici satırı | CRM sahibinin adımı | önerilen satır: `x-consumers: … MOD-0177 Recall Readiness (U-1 — müşteri adı / kodu gösterimi ve kayıt öncesi çözüm denetimi; ekran okumasında istek başına en çok 25 hesap, dışa aktarımda vakanın bütün hesapları 8'lik dalgalarla — bir dalganın bütün okumaları zaman aşımı / 5xx ise kalanı çağrılmaz —, en çok 8 eşzamanlı; uygunluk kararı vermez, isBillable kullanmaz)` |
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
| `declareConsumption` (`DeclareRecallConsumptionHandler`, `RecordHandlers.cs:123-221`) | `:155`, `:163`, `:169-170` ordinal | §8.3 eşleşmesi; aynı müşteri / SKU toplamları kanonik anahtarla; çözüm denetimi |
| `linkReturn` (`R3Handlers.cs:378-450`) | `ToHashSet(StringComparer.Ordinal)` (`:417`) | §8.3 eşleşmesi; `customers` kümesi kaynağa göre kurulur; çözüm denetimi; `Crm` eşleşmesinde olay metni anahtarsız (aşağıda) |

(r3'te bu satır `recordConsumption` diye yazılmıştı; sözleşmedeki ve koddaki ad `declareConsumption`'dır — `recall.openapi.yaml:484`; r4 her
yerde düzeltildi.)

Kapsamda olmayan anahtar bugünkü gibi 404 `RECALL_UNKNOWN_CUSTOMER` (`RecordHandlers.cs:103-105`), yazım 0. **Eski kayıtlar değişmez:**
göç yok, açılışta düzeltme yok, okuma yolunda yeniden yazma yok. Yeni kayıt alanı yok.

**Çözüm denetimi — ad çözülmeden yazım yok (sahip kararı, Beste, 2026-11-03; F-1; Q-7 = açık, Beste, 2026-11-04).** Ekran bu üç işlemi ad
çözülene kadar kapatır (§8.8); sunucu da reddeder — ekran tek başına bekçi değildir:
- **Yer — yazımdan önceki son denetim (F-6@fbafd3af):** çözüm denetimi işleyicinin bugünkü **bütün** denetimlerinden sonra, yazım işleminden
  hemen önce koşar. Bugünkü sıra korunur ve sona bir adım eklenir:
  - `recordNotification`: kırpma ve parmak izi (`:32-36`) → tekrar (`:37-42`) → giriş (`:44-45`, 400) → vaka ve durum (`:47-52`, 404 / 409)
    → tarih (`:55`, 400) → kapsam eşleşmesi (`:57-58`, 404) → `supersedesId` (`:60-64`, 400) → **çözüm denetimi** → yazım (`:81-91`).
  - `declareConsumption`: kırpma ve parmak izi (`:125-130`) → tekrar (`:131-136`) → giriş (`:138-143`, 400) → vaka ve durum (`:145-150`) →
    tarih (`:153`) → kapsam eşleşmesi (`:155-156`, 404) → birim (`:158-160`, 400) → `supersedesId` (`:162-166`, 400) → miktar (`:168-179`,
    422) → **çözüm denetimi** → yazım (`:199-212`).
  - `linkReturn`: giriş (`:380-386`, 400) → parmak izi ve tekrar (`:388-392`) → vaka ve durum (`:394-396`) → hareket ve uygunluk
    (`:398-414`, 422 / 409) → kapsam eşleşmesi (`:416-418`, 404) → **çözüm denetimi** → yazım (`:433-443`).
  Sonuç: CRM kapalıyken ya da izin eksikken **geçersiz** bir istek bugünkü 400 / 404 / 409 / 422'sini alır ve CRM çağrılmaz; 503 / 403 yalnız
  aksi hâlde yazılacak bir isteğe döner. Gerekçe: aynı geçersiz isteğin cevabı CRM'in durumuna göre değişmez (istemci hatasını düzeltebilir), CRM
  yalnız yazılacak istek için çağrılır. Bedeli: denetimler ile yazım arasındaki pencere CRM okuması kadar (≤ 2 sn) uzar; bugünkü kod da bu
  denetimleri işlem dışında yapar ve işlem içinde yinelemez (ör. miktar `:168-179`, yazım `:199-212`) — FU01 yeni bir yarış türü açmaz,
  var olanın penceresini uzatır (O-6, §20).
- **Çözüm:** **eşleşen satır `Crm` kaynaklıysa** `IRecallCustomerDirectory.ResolveAsync([hesap], Page)` (§8.6, tek hesap). `Unresolved` →
  **503 `RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE`**; `Forbidden` → **403 `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN`** (F-2@fbafd3af); ikisinde de
  yazım 0 (kayıt, vaka olayı, denetim satırı yok). `Resolved` ve `NotFound` → bugünkü akış (**Q-7 = açık**, sahip, Beste, 2026-11-04: CRM
  kesin "yok" demiştir, ad bir daha çözülmeyecektir; kapatmak silinmiş CRM hesabının müşterisine bildirimi hiç kaydedilemez kılardı). Eşleşen
  satır `Crm` değilse CRM çağrılmaz (eski anahtarın davranışı ve süresi değişmez).
- Tekrar denetimi çözüm denetiminden **önce** kalır: daha önce başarıyla yazılmış bir isteğin tekrarı CRM kapalıyken de 200 ve aynı kaydı döner
  (ilk çağrı denetimi geçmiştir; tekrar yeni yazım yapmaz).
- **İade bağının olay metni (r4 ek düzeltme, §19.8):** `linkReturn` bugün vaka olayının metnine müşteri anahtarını yazar
  (`R3Handlers.cs:438` `$"{t.Number ?? t.TransactionId} → {customer}"`); bu metin zaman çizelgesinde (`getCaseTimeline` `text`,
  `case-detail.js` `drawTimeline` `e.text`) ve kanıt paketinin zaman çizelgesi bölümünde (`RecallEvidence.cs:302-303` `e.Text`) basılır — `Crm`
  anahtarında GUID olurdu. FU01: eşleşen satır `Crm` kaynaklıysa metin yalnız `{t.Number ?? t.TransactionId}`'dir; müşteri anahtarı olayın
  `refs.customerRef` alanında kalır (bugünkü gibi, `:428-432`). `Crm` olmayan eşleşmede ve FU01 öncesi olaylarda metin değişmez (olaylar
  yalnız eklenir, eski olay yeniden yazılmaz).
- **Kod seçimi — 503, 409 değil:** neden kaynağın durumu değil, bağımlı servisin şu anki yanıtıdır; istemci isteği değiştirerek çözemez, yalnız
  sonra yeniden dener ("CRM'e ulaşılamıyor, sonra tekrar deneyin"). Depodaki emsal bağımlılık kesintisi kodları da 503'tür
  (`QUARANTINE_WORKFLOW_UNAVAILABLE`, `LEGAL_ENTITY_MASTER_UNAVAILABLE` — `InventoryErrorCodes.cs:42`, `QuarantineContract.cs:76`). 409 bu
  sözleşmede kaynak çakışmasına ayrılmıştır (`RECALL_CONFLICT`, `RECALL_RETURN_ALREADY_LINKED`). Yanıt ayrıntısı `customerRef` taşımaz
  (GUID istemciye hata gövdesinde de dönmez; istemci gönderdiği değeri zaten bilir).
- **İzin eksik — 403, 503 değil (CT kararı, MVP-1 CT, 2026-11-04; F-2@fbafd3af):** CRM 401 / 403 artık `Unresolved` değil ayrı sonuç
  `Forbidden`'dır (§8.6). Yazım reddi **403 `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN`**. Gerekçe: neden geçici bir kesinti değil, **çağıranın**
  kalıcı yetki durumudur — yeniden denemek izni getirmez; 503 istemciye "sonra yeniden dene" der ve `recall-ui.js:52` kod anahtarı yoksa 5xx'i
  `Recall.Errors.SERVICE_UNAVAILABLE` ("… ulaşılamıyor, biraz sonra yeniden deneyin") metnine düşürür — yanlış yönlendirir. 403 bu sözleşmede
  kodlu yetki reddi için zaten kullanılır (`CROSS_LE_FORBIDDEN`, `recall.openapi.yaml:993` `shared`; `Forbidden` yanıtı üç uçta tanımlı,
  `:479`, `:510`, `:557`, ve test doğrulayıcısının izinli durum listesinde, `RecallTestInfrastructure.cs:159`, `:160`, `:174`); kod anahtarı yoksa bile
  ekran 403'ü `Recall.Errors.FORBIDDEN` ("Bu işlem için yetkiniz yok.") ile gösterir (`recall-ui.js:53`), yani "yeniden dene" demez. Ayrı kod,
  recall'un kendi 403'ünden (eksik recall izni) ayırt etmek içindir: kullanıcı ve yöneticisi eksik iznin CRM'in olduğunu görür. Yanıt
  ayrıntısı `customerRef` taşımaz.
- İzni atanmamış kullanıcı CRM anahtarlı müşteriye kayıt yapamaz ve böyle müşterisi olan vakayı / izi dışa aktaramaz (§8.10) — D-4 bu
  yüzden DL-1 kabul kapısı, AC-30 canlı geçiş kapısıdır (§7).

### 8.5 Yeniden izlemede anahtar kararlılığı (SB-19)

`submitScope` yeniden izler ve `RecallTraceService.Reconcile` saklı kapsam satırının anahtarını **koşulsuz** üzerine yazar
(`RecallTraceService.cs:141` `s.CustomerRef = t.CustomerRef`; tek çağıran `CaseCommandHandlers.cs:553`). `submitScope` yalnız `Draft`'ta
koşar (`RecallCaseRules.CanSubmit`, `RecallRules.cs:173`), müşteri kayıtları yalnız `Active` / `EffectivenessCheck`'te yazılır
(`AcceptsRecords` = `IsUnderRecall`, `RecallRules.cs:169`, `:197` — `RecordHandlers.cs:49`, `:147`; iade bağı `R3Handlers.cs:396`; geri dönüş
yalnız `PendingScopeApproval → Draft`, `:175`): yeniden izlenen vakada müşteri kaydı **hiç yoktur**.

**Q-5 = HAYIR (sahip, Beste, 2026-11-03):** hiç müşteri kaydı (bildirim / beyan / iade bağı) olmayan vakada da **eski** anahtar CRM kimliğine
**yükseltilmez**; kural kayıt varlığına bakmaz.

**"Eski" tanımı — yalnız FU01 öncesi anahtarlar (CT yönlendirmesi, MVP-1 CT, 2026-11-03; F-7; sahip teyidi Q-8 = EVET, Beste, 2026-11-04):** eski = `CustomerRefSource
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

**Port:** `IRecallCustomerDirectory.ResolveAsync(IReadOnlyCollection<Guid> accountIds, RecallLookupMode mode, CancellationToken)` →
`IReadOnlyDictionary<Guid, RecallCustomerName>`; `RecallCustomerName = (Outcome: Resolved | NotFound | Forbidden | Unresolved, Code?, Name?)`;
`RecallLookupMode = Page | Export` (bütçe, aşağıda). Sözlük istenen her hesabı içerir (yanıtsız kalan `Unresolved`). `Page` kipinde
`accountIds` en çok `RecallCustomerDisplay.PageSize` (25) hesaptır; fazlası programlama hatasıdır (`ArgumentOutOfRangeException`, CRM
çağrılmaz) — pencereyi çağıran keser (aşağıda "Müşteri sayfası").

**Bağdaştırıcı `CrmAccountLookupHttpReader`** — `ShipmentBundleHttpReader` deseni (`:19-22`):
- `GET api/crm/accounts/{id:D}/lookup-validation`, Gateway taban adresiyle (`RecallServiceCollectionExtensions.cs:48-56`); Gateway rotası
  `/api/crm/accounts/{everything}` → 5061 mevcut (ocelot `:2979`).
- Kimlik: çağıranın token'ı ve korelasyon kimliği (`ForwardCallerHeadersHandler`, `ForwardCallerHeadersHandler.cs:21-35` — `Authorization` ve
  `X-Correlation-Id`; CRM taslağı §7 korelasyon beklentisini karşılar). Kiracı yalnız token'dan (CRM taslağı §1); kiracı başlığı ve
  `X-Legal-Entity-Id` gönderilmez (SHIPMENT okuyucusunun `:60` şirket başlığı burada yoktur — CRM hesabı kiracı geneldir).
- **Süre — iki kip (CT kararı, MVP-1 CT, 2026-11-04; F-1@fbafd3af).** Her iki kipte `MaxParallel = 8` eşzamanlı ve okuma başına
  `PerReadTimeout = 2 sn`. r3'ün çağrı başına `TotalBudget = 2 sn` kuralı kaldırıldı: sağlıklı ama gecikmeli CRM'de büyük vakanın
  müşterilerinin bir kısmını kalıcı olarak `Unresolved` bırakıyordu (8 × 2 sn / gecikme — 200 ms'de ≈ 80 hesap).
  - **`Page` (ekran okumaları ve kayıt komutunun tek hesabı):** çağrı en çok 25 hesap alır ve **sayfa için toplam tavan `PageBudget = 3 sn`**:
    `ResolveAsync` tek bir bağlı iptal belirteci açar (`CancelAfter(PageBudget)`); her okuma bu belirteçle ve kendi 2 sn sınırıyla koşar. Tavan
    dolunca süren okumalar iptal edilir, başlamamış okumalar **hiç başlamaz**; ikisi de `Unresolved` olur. **Kısmi sonuç kuralı:** tavan içinde
    yanıtlanan hesaplar kendi sonucunu taşır, kalanlar `Unresolved`; çağrı hiçbir zaman hata fırlatmaz.
    **Değerin gerekçesi:** sayfa boyu 25 = sözleşmenin varsayılan sayfa boyu (`PageSize` `default: 25`, `recall.openapi.yaml:1012`); 8
    eşzamanlıyla tavan(25/8) = 4 dalga. Sağlıklı CRM = Gateway dahil okuma başına ≤ 500 ms (ASSUMPTION-FU01-5, §19.3 — CRM taslağında süre
    hedefi yok, ölçülmedi; teyit Q-3 sahibinde). Sağlıklı CRM'de sayfa süresi ≤ 4 × 0,5 = 2 sn < 3 sn (CT ölçütü "sayfa boyu × gecikme / 8 <
    tavan": 25 × 0,5 / 8 ≈ 1,6 sn < 3 sn); 1 sn pay, tek bir yavaş okumanın (≤ 2 sn) bir yuvayı tutup kalan 24 hesabın 7 yuvada sürmesini de
    karşılar (tavan(24/7) × 0,5 = 2 sn). Yanıtsız CRM'de ekran okumasına katkı ≤ 3 sn + paydır, müşteri sayısından bağımsızdır.
  - **`Export` (kanıt paketi önizleme ve dışa aktarımı, iz CSV'si):** **toplam tavan yoktur**, her hesap okunmaya çalışılır (okuma başına
    2 sn, 8 eşzamanlı) — **devre kesiciyle** (CT kararı, MVP-1 CT, 2026-11-04; F-1@807c8dc2):
    - **Dalga:** hesaplar kanonik `"D"` metninin ordinal sırasıyla dizilir ve 8'erli dalgalara bölünür (son dalga 8'den küçük olabilir). Bir
      dalganın okumaları birlikte başlar; sonraki dalga, öncekinin bütün okumaları sonuçlanınca başlar.
    - **Kesici sınıfı:** okuma başına 2 sn zaman aşımı, 5xx ve ulaşılamıyor (`HttpRequestException`). CRM'in verdiği her başka yanıt —
      200 (geçerli ya da geçersiz gövde), 400, 401 / 403, 404 — kesici sınıfı **değildir** (CRM ayaktadır).
    - **Kesici:** bir dalganın **bütün** okumaları kesici sınıfındaysa kalan dalgaların hesapları **çağrılmadan** `Unresolved` olur ve her biri
      için günlüğe `recall.customer.lookup_unavailable {AccountId}: Breaker` yazılır. Dalgada tek bir kesici dışı yanıt varsa kesici açılmaz.
      Kesici yalnız `Export` kipinde vardır; `Page` kipini sayfa tavanı sınırlar.
    - **Kural CT kararının uygulanışıdır:** CT kararı ilk dalgayı adlandırır ("ilk 8'lik dalganın hepsi …"); paket aynı denetimi **her
      dalgada** yapar (yazar kararı — inceleme konusu). Gerekçe: CRM dışa aktarımın ortasında düşerse yalnız ilk dalgaya bakan kesici açılmaz
      ve kalan dalgalar 2'şer sn sürer (400 hesapta, ikinci dalgadan sonra düşen CRM'le ≈ 48 × 2 = 96 sn > 90 sn — aşağıda Q-9 ölçümü);
      her dalgada denetim kesintiyi en çok bir dalga (≤ 2 sn) gecikmeyle yakalar. Sağlıklı CRM'de iki biçim aynıdır (hiçbir dalga baştan sona
      kesici sınıfında değildir). CT yalnız ilk dalgayı isterse değişiklik tek koşuldur; AC-29 (f) ve S-29g bu farkı ölçer.
    - Çözülemeyen hesap (kesiciyle atlanan dahil) dosyaya `Unresolved` işaretiyle girer (§8.8, §8.10); `Unresolved` yüzünden dışa aktarım
      reddedilmez. `Forbidden` kesici sınıfı değildir; vakada / izde `Forbidden` hesap varsa dışa aktarım ve önizleme 403 ile reddedilir
      (§8.10). Kesici açıldıktan sonra çağrılmayan hesapların gerçek durumu bilinmez (izin eksik de olabilirdi); dosyaya `Unresolved` girer.
    - **Süre:** sağlıklı CRM'de ≈ dalga sayısı × dalganın en yavaş okuması (200 hesap, 200 ms → ≈ 25 × 0,2 = 5 sn; 400 hesap → ≈ 10 sn).
      Baştan yanıtsız CRM'de ≈ 2 sn + pay (ilk dalga), hesap sayısından bağımsız; ortada düşen CRM'de düştüğü ana kadarki süre + ≤ 2 sn.
    - Önizleme de bu kiptedir: önizleme ve dışa aktarım aynı kurucuyu kullanır (`EvidenceHandlers.cs:56`, `:97-98`) ve kullanıcının önizlemede
      gördüğü belge indireceği belgedir; sayfa kipi önizlemeyi dosyadan farklı kılardı (tur 3 bunu bulgu saymadı; kesici yanıtsız CRM'de
      önizlemenin bekleyişini ≈ 2 sn'ye indirir — Q-9 (b) kapandı, §19.4).
    - **Uçtan uca zaman sınırı — ölçüm (Q-9 (a); inceleme 01 tur 3, `9686cafd9`; r5'te `76f93c261`'de yeniden okundu):** Gateway Ocelot
      24.1.0 (`gateway/Diten.ApiGateway/Diten.ApiGateway.csproj:14`); `ocelot.json`'da hiçbir rotada `Timeout`, `QoSOptions` ya da
      `TimeoutValue` yok (`grep -c` 0), `GlobalConfiguration` yalnız `BaseUrl` (`:6493-6495`); Gateway `Program.cs`'te zaman aşımı ayarı yok.
      Ocelot belgesine göre rota zaman aşımı tanımsızsa `DownstreamRoute.DefaultTimeoutSeconds` = `DefTimeout` = **90 sn** uygulanır
      (`~/.nuget/packages/ocelot/24.1.0/lib/net8.0/Ocelot.xml:253-256`). Web Recall vekili varsayılan `HttpClient`'ı kullanır
      (`RecallController.cs:823` `SendAsync`, `:861` `SendFileAsync`; kayıt `Program.cs:130`, `:134-135`) — zaman aşımı ayarlanmamış, .NET
      varsayılanı **100 sn**; `ShipmentBundleTimeoutHandler` yalnız `/api/shipment-bundle/` yollarına dokunur (`Program.cs:575`). Zaman
      aşımında Web 503 `SERVICE_UNAVAILABLE` döner (`RecallController.cs:834-837`). Recall JS'inde `fetch` zaman sınırı yok. Sonuç: dış sınır
      Web → Gateway → Inventory hattının **90 sn**'sidir. **Kesicisiz** yanıtsız CRM'de süre ≈ tavan(N/8) × 2 sn → kesilme eşiği ≈ N ≥ 361
      hesap (ölçülmeyen öteki okumalar — imzalar, ledger, karantina — eşiği biraz düşürür); eşiğin üstünde dışa aktarım ve önizleme genel bir
      503 ile biterdi ve "kesintide de işaretli dosya" kuralı (§8.10) sağlanmazdı — kesici bu yüzden vardır. **Ölçülmeyenler:** Ocelot'un 90
      sn'de döndürdüğü kod (503 / 504) ve Inventory işleyicisinin iptali alıp almadığı koşularak ölçülmedi (Web ikisini de kodsuz 5xx →
      `SERVICE_UNAVAILABLE` olarak gösterir); canlı / kabul ortamının ters vekil (IIS, nginx) sınırları depoda yok — 90 sn'den kısaysa eşik
      düşer. `ocelot.json`'a rota zaman aşımı yazmak integration-agent'ın işidir; bu paket önermez (§15).
    - **Kalan sınır (kabul edilen, O-7, §20):** kesici yalnız bütün dalganın düştüğü kesintiyi yakalar. (i) Her dalgada en az bir okumanın
      zaman aşımına uğradığı **kısmi** kesintide her dalga ≈ 2 sn sürer, kesici açılmaz → ≈ 361 hesap üstünde dışa aktarım yine Gateway'de
      kesilir; (ii) sağlıklı sınırdaki (500 ms, ASSUMPTION-FU01-5) CRM'de tavan(N/8) × 0,5 sn < 90 sn → ≈ 1 440 hesaba kadar.
- **Müşteri sayfası (ekran okumaları, F-1@fbafd3af):** müşteri taşıyan her ekran okuması — `getCaseCustomers`, `getCaseScope`,
  `getCaseScopeDiff`, `getCaseReturns`, `getCaseEffectiveness`, `runTrace` — bütün satırlarını bugünkü gibi döner, ama adı yalnız **istenen
  müşteri sayfasının** `Crm` müşterileri için çözer. Tanım (tek kural, `RecallCustomerDisplay.Page`):
  - Yanıtın **müşteri anahtarı kümesi** = yanıtta `customer` taşıyan satırların farklı `customerRef` değerleri (bütün kaynaklar), ordinal
    (UTF-16 kod birimi) sırasıyla. `getCaseCustomers`'ta bu, satırların bugünkü sırasıdır (`CaseDetailReadHandlers.cs:75` `OrderBy(g => g.Key,
    StringComparer.Ordinal)`); tarayıcıda JS `Array.prototype.sort()` varsayılanı aynı sırayı verir.
  - Sayfa k = kümenin `[(k − 1) × 25, k × 25)` dilimi; k, isteğe bağlı sorgu parametresi `customerPage`'dir (tamsayı, varsayılan 1). Ad yalnız
    dilimdeki `Crm` anahtarları için çözülür (`Page` kipi, tek çağrı, en çok 25 hesap). Dilim dışındaki `Crm` satırı `customer.state =
    Deferred` taşır (ad, kod ve ayırıcı yok; CRM çağrılmaz). CRM kimliksiz satırlar (`Legacy`, `NoCrm`) sayfadan etkilenmez.
  - `customerPage` < 1 ya da tamsayı değil → 400 `INVALID_REQUEST`; küme boyunu aşan sayfa → boş dilim, CRM'e 0 çağrı, bütün `Crm`
    satırları `Deferred`, 200.
  - Ekran, sayfayı gösterdiği müşterilere göre seçer (§8.8): müşteriler sekmesi ve iade bağlama seçim listesi yalnız sayfanın müşterilerini
    çizer; öteki tablolar ve iz ekranı bütün satırları çizer ve küme 25'i aşınca bir müşteri sayfası seçicisi gösterir.
- **MVP-6 okumasıyla eşzamanlılık:** `getCaseCustomers` CRM anahtarlarını saklı kapsam satırlarından alır (canlı MVP-6 okumasını beklemez);
  CRM çözümü ve MVP-6 sevkiyat okuması (`CaseDetailReadHandlers.cs:47-50`) **aynı anda** başlatılır, sonuç ikisi bitince birleştirilir. FU01'in
  bu uca eklediği süre ≤ 3 sn + paydır; MVP-6 okumasının kendi süresi (ebeveyn kuralı, tavan(N/8) × 2 sn) FU01 kapsamı dışındadır (O-3, §20).
  `runTrace` / `getCaseScopeDiff`'te anahtar canlı izden doğduğu için çözüm izden sonra koşar (iz süresi + ≤ 3 sn).
- İstek içinde tekilleştirme (aynı GUID bir kez), **önbellek yok** (mevcut okuyucu da önbelleksizdir; CRM taslağı §7 "yanıtlar güncel durumdur").

**Sınıflandırma** (CRM taslağı §2, §5, §7 ile hizalı; recall uygunluk kararı vermez, `isBillable` / `status` kullanılmaz — pasif müşteriye
de geri çağırma bildirimi gerekir):

| CRM yanıtı | Sonuç | Gösterim |
|---|---|---|
| 200; `data.accountId` istenen GUID (Guid eşitliği); `code` ve `name` boş değil; `contractVersion` `crm-account-lookup/1.` ile başlar | `Resolved` | ad + kod |
| 404 (yok / silinmiş / başka kiracı — CRM'de üçü aynı gövde) | `NotFound` | "Bulunamadı" + ayırıcı (§8.8); kayıt komutları açık (Q-7) |
| 401 / 403 (çağıranın `crm.account.lookup-validation` izni yok ya da token CRM'ce kabul edilmiyor) | `Forbidden` (F-2@fbafd3af) | ekranda "İzin eksik" + ayırıcı (§8.8); kayıt komutları kapalı, sunucu 403 (§8.4); dışa aktarım ve önizleme 403 (§8.10, F-2@807c8dc2); ekran yeniden denemeyi önermez |
| 200 ama yukarıdaki koşullardan biri tutmuyor; gövde ayrıştırılamıyor; 400; 5xx; zaman aşımı; sayfa tavanı doldu; devre kesici açık (yalnız `Export`); ulaşılamıyor | `Unresolved` | "Çözülemedi" + ayırıcı (§8.8); kayıt komutları kapalı, sunucu 503 (§8.4); dosyaya işaretli girer (§8.10) |

`Deferred` bir CRM yanıtı değildir: dilim dışındaki `Crm` satırı için CRM hiç çağrılmaz (yukarıda "Müşteri sayfası"). Hiçbir sonuç bir
**ekran okumasını** başarısız kılmaz: müşteri satırları ve etkinlik her durumda döner. Kayıt komutlarının `Unresolved` ve `Forbidden` reddi
§8.4'te, üç dışa aktarım ucunun (`previewEvidence`, `createEvidenceExport`, `exportTrace`) `Forbidden` reddi §8.10'dadır; `Unresolved` hiçbir
dışa aktarımı reddettirmez.

**Günlük:** başarısızlıkta `recall.customer.lookup_unavailable {AccountId}: {Reason}` (Reason = durum kodu — 401 / 403 dahil —, istisna türü, sayfa tavanı dolunca `Budget`, devre kesiciyle atlanan hesapta `Breaker`) —
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
    state:  { type: string, enum: [Resolved, Unresolved, NotFound, Forbidden, Deferred, Legacy, NoCrm] }   # Legacy ⇔ source = Unknown; NoCrm ⇔ source ∈ {ShipTo, Document, Manual}; Forbidden / Deferred yalnız source = Crm
    name:   { type: string }   # yalnız state = Resolved
    code:   { type: string }   # yalnız state = Resolved
    label:  { type: string }   # yalnız state ∈ {Legacy, NoCrm} — bugünkü etiket (shipTo / belge no / serbest metin)
    shipTo: { type: string }   # yalnız state ∈ {Unresolved, NotFound, Forbidden} — sevkiyatın canlı teslim yeri metni; okunamadıysa ya da "D" biçimliyse yok
    shipmentNumber: { type: string }   # yalnız state ∈ {Unresolved, NotFound, Forbidden} — sevkiyat hareket numarası (ledger TransactionNumber); yoksa yok
# Deferred: ad bu müşteri sayfasında istenmedi (§8.6) — name, code, shipTo, shipmentNumber yok
CustomerPage: { name: customerPage, in: query, schema: { type: integer, minimum: 1, default: 1 } }   # components/parameters
ServiceUnavailable: { description: A dependency is unreachable (see x-error-codes), content: { application/json: { schema: { $ref: '#/components/schemas/Error' } } } }   # components/responses
```

İsteğe bağlı `customer: CustomerDisplay` eklenir: `CustomerRow` (`:1530`), `EffectivenessRow` (`:1198`, toplam satırında yok), `ReturnLink`
(`:1558`), `ScopeItem` (`:1308`, yalnız `kind = Shipment`), trace düğümü (`:1466`, yalnız `Shipment`). `customerRef` alanları ve tipleri
**değişmez** (anahtar; komutlar onu gönderir). `CustomerRow.label` CRM satırında **yoktur** (bugün `?? g.Key` ile anahtara düşebilir,
`CaseDetailReadHandlers.cs:80-83`). Trace düğümü şeması `getCaseScopeDiff` yanıtında da kullanılır (`ScopeDiff.added`, `:1499-1504`) — ayrı
alan gerekmez. Komut gövdeleri (`:467-470`, `:496-499`, `:548-552`) değişmez; açıklamalarına §8.3 harf kuralı ve §8.4 çözüm denetimi eklenir.

**Müşteri sayfası parametresi (F-1@fbafd3af):** `customerPage` (`components/parameters/CustomerPage`, yukarıda) altı ekran okumasına
isteğe bağlı eklenir: `getCaseCustomers` (`:443`), `getCaseScope` (`:173`), `getCaseScopeDiff` (`:184`), `getCaseReturns` (`:516`),
`getCaseEffectiveness` (`:563`), `runTrace` (`:82`). Bugün 400 tanımı olmayan beş ucun (`runTrace` dışındakiler) `x-error-codes`
listesine `INVALID_REQUEST` ve yanıtlarına `'400': BadRequest` eklenir (`runTrace` ikisini zaten taşır, `:85`, `:92`). Parametre
verilmeyen istek 1. sayfayı ister; satırların kendisi (sayı, sıra, alanlar) parametreden etkilenmez. Açıklamaya §8.6 kuralı (25'lik dilim,
ordinal anahtar sırası, `Deferred`) yazılır. Dışa aktarım uçları (`previewEvidence`, `createEvidenceExport`, `exportTrace`) parametre
almaz — bütün müşteriler çözülür (§8.6 `Export`, devre kesiciyle; açıklamalarına kesici kuralı bir cümleyle yazılır).

**Yeni hata kodları (F-1@a66dd353, F-2@fbafd3af):** üst düzey `x-error-codes` kaydına (`recall.openapi.yaml:962`;
`RecallRulesAndShapeTests.cs:285-290` her `RecallErrorCodes` sabitinin kayıtta olmasını ister) `RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE: 503`
ve `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN: 403`; ikisi de `recordNotification` (`:458`), `declareConsumption` (`:487`) ve `linkReturn`
(`:539`) `x-error-codes` listelerine eklenir. Bu üç uca `'503': ServiceUnavailable` yanıtı eklenir (yeni `components/responses` girdisi —
sözleşmede bugün hiç 503 yok, ölçüldü); `'403': Forbidden` üçünde zaten tanımlı (`:479`, `:510`, `:557`), `Forbidden` açıklaması değişmez. İki
kod yalnız `Crm` kaynaklı satırla eşleşen ve öteki bütün denetimleri geçen girişte doğar (§8.4) — FU01 öncesi hiçbir girişin cevabı değişmez.

**Dışa aktarım reddi (F-2@807c8dc2):** `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN` ayrıca `exportTrace` (`:100`), `previewEvidence` (`:813`) ve
`createEvidenceExport` (`:838`) `x-error-codes` listelerine eklenir; `'403': Forbidden` üçünde zaten tanımlı (`:108`, `:822`, `:858`) — yeni
yanıt girdisi gerekmez. Açıklamalarına bir cümle: "403 RECALL_CUSTOMER_DIRECTORY_FORBIDDEN when the caller may not read a CRM customer of the
subject (CRM 401 / 403) — no file, no log row, no audit row". `RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE` bu uçlara **eklenmez** (kesinti dışa
aktarımı reddettirmez, §8.10). Kod yalnız `Crm` kaynaklı müşterisi olan konuda doğar (tatbikat ve dönem kanıtında CRM çağrılmaz,
`RecallEvidence.cs:382`, `:408`) — FU01 öncesi hiçbir girişin cevabı değişmez; değişiklik notu bunu "additive" sayar ve üç ucu adıyla anar.

**`DrillRun.customers` adlandırması (F-10):** tatbikat yanıtı bugün `customers` dizisini `additionalProperties` üzerinden taşır
(`RecallDrillRunDto.Customers`, `DrillModels.cs:56`; dolduran `DrillSupport.cs:56`, kaynak `DrillRunHandlers.cs:284`; şema `:1688-1691`).
1.10.0'da isteğe bağlı olarak adlandırılır: `customers: { type: array, items: { type: string }, description: "customer keys (customerRef
values) of the drill trace — keys, never display text; a CRM key is a GUID" }`. Gösterim istisnası §8.8'dedir.

**Tek girdi daraltması (Q-6):** `addScopeItems` (`:196`) açıklamasına ve değişiklik notuna "one input restriction: a Shipment item whose
trimmed customerRef is a 36-character GUID in the 'D' form is refused with 400 INVALID_REQUEST (no CRM customer is chosen by hand;
MOD-0177-FU01 Q-6)" eklenir; `x-error-codes` değişmez (`INVALID_REQUEST` zaten listede). Şema (`ScopeItemInput`) değişmez.

### 8.8 Gösterim kuralı

- **CRM anahtarlı müşteri (ekran, CSV, PDF):** `Resolved` → "{ad} · {kod}"; `Unresolved` → "Çözülemedi"; `Forbidden` (yalnız ekran ve
  API — dışa aktarımda olmaz, dışa aktarım reddedilir, §8.10) → "İzin eksik"; `NotFound` → "Bulunamadı"; `Deferred` (yalnız ekran ve API —
  dışa aktarımda olmaz) → "Ad bu sayfada değil" (`Recall.Customer.Deferred`).
  `Unresolved`, `Forbidden` ve `NotFound` satırı **ayırıcıyla** gösterilir (sahip kararı, F-1): `shipTo` ve `shipmentNumber` (§8.7) — ikisi de
  varsa "{teslim yeri} · {sevkiyat no}", biri varsa o, hiçbiri yoksa yalnız durum metni. `Deferred` satırı ayırıcı taşımaz; bu satırlar
  yalnız komut verilmeyen tablolarda ve iz ekranında görünür, oralarda satırın kendi belge / hareket sütunu vardır (aşağıda "Görünen yerler").
  **Ayırıcının kaynağı — tek kural `RecallCustomerDisplay.Distinguisher` (F-4@fbafd3af):** müşterinin satırları (`NodeKey` ordinal sırasıyla)
  ve her satırın arkasındaki satış hareketleri; hareketler satıra **`getCaseCustomers`'ın bugünkü eşleşme kuralıyla** bağlanır — aynı lot ve
  `DocumentId ?? "TXN-" + TransactionId` = satırın `SourceDocumentId`'si (`CaseDetailReadHandlers.cs:42-44`; FU01 bu kuralı
  `RecallReads.MovementsBehind` adıyla ortak yardımcıya taşır, davranış aynı) — ve bir satırın hareketleri (`PostingDate`, `TransactionId`
  ordinal) sırasıyla okunur. `shipmentNumber` = bu sırayla ilk boş olmayan hareket `Number`'ı; `shipTo` = `NodeKey` sırasıyla ilk boş olmayan
  canlı `ShipToReference` (okuma `Ok` ise, `CaseDetailReadHandlers.cs:72`). Kaynaklar: `getCaseCustomers` hareketleri zaten okur (`:41`,
  `SalesIssuesOfLotsAsync`); trace düğümünde düğümün kendi sevkiyatı (`TraceNode.Shipment.ShipToReference`, `Movement.TransactionNumber`);
  kanıt paketinde §8.10. `SourceDocumentId` (MVP-6 sevkiyat GUID'i) **ayırıcı olarak kullanılmaz**. Ayırıcı değerlerinden `IsDShaped`
  olan gösterilmez.
- **GUID metin, `title`, araç ipucu, CSV / PDF hücresi olarak hiçbir yerde görünmez**; yalnız komut gönderen öğenin değerinde (`data-*` /
  `option.value`), API'nin `customerRef` alanlarında ve `DrillRun.customers` anahtar dizisinde taşınır. **Tatbikat istisnası (F-10):**
  `DrillRun.customers` bir anahtar listesidir, ekranda çizilmez (`drill-view.js` bugün çizmiyor — ölçüldü; AC-22 sabitler) ve kanıt paketine
  girmez (tatbikatta müşteri bölümü "notApplicableDrill", `RecallEvidence.cs:382`). Tatbikatın "CustomerList" adımı yalnız zamanlayıcıyı
  durdurur; liste insan için ayrıca sunulmaz (değişmez).
- **Kayıt komutları (F-1, F-2@fbafd3af):** `Unresolved` satırda bildirim ve tüketim beyanı düğmeleri **çizilmez** (`case-detail.js:244-245`);
  yerine `Recall.Customer.CrmUnavailable` ("CRM'e ulaşılamıyor, sonra tekrar deneyin") yazılır. `Forbidden` satırda da düğmeler çizilmez;
  yerine `Recall.Customer.CrmForbidden` ("CRM müşteri adlarını görme izniniz yok; yöneticinize başvurun.") yazılır — **yeniden deneme
  önerilmez**. İade bağlama seçim listesinde (`:405`) `Unresolved` ve `Forbidden` müşteriler **seçenek olmaz**; dışarıda kalan varsa pencerede
  ilgili metin (`CrmUnavailable` ve / veya `CrmForbidden`) görünür; hiç seçenek kalmadıysa alanlar ve bağlama düğmesi çizilmez (`:407-409`
  deseni). Sunucunun 503'ü `Recall.Errors.RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE`, 403'ü `Recall.Errors.RECALL_CUSTOMER_DIRECTORY_FORBIDDEN`
  ile çizilir (`recall-ui.js:51` kod eşlemesi; anahtar yoksa 5xx `SERVICE_UNAVAILABLE`'a, 403 `FORBIDDEN`'a düşer, `:52-53` — AC-27 bu
  düşüşü ayırt eder). `Resolved` ve `NotFound` satırda düğmeler bugünkü gibidir (**Q-7 = açık**, Beste, 2026-11-04).
- **Dışa aktarım ekranda (F-2@807c8dc2, DL-2):** kanıt ekranı önizleme ve dışa aktarımın 403 yanıtını bugünkü yoldan gösterir —
  `ui.errorText(reply)` (`evidence.js:107`, `:129`) → `Recall.Errors.RECALL_CUSTOMER_DIRECTORY_FORBIDDEN` (`recall-ui.js:51`); `evidence.js`
  değişmez. İz ekranında CSV bir bağlantıdır (`trace.js:95-97`, `<a href="/Recall/export/trace?…">`); Web vekili ret gövdesini JSON olarak
  döner (`RecallController.cs:144` `Refuse`), tarayıcı bunu sayfa olarak açar. Bu yüzden iz yanıtının çizilen düğümlerinden biri
  `customer.state = Forbidden` ise CSV bağlantısı **çizilmez**, yerine `Recall.Customer.ExportForbidden` ("CRM müşteri adlarını görme izniniz
  yok; dışa aktarım yapılamaz. Yöneticinize başvurun.") yazılır. Sınır: `Forbidden` çağırana özgüdür, ama ad yalnız gösterilen müşteri
  sayfasında çözülür — sayfada hiç `Crm` düğümü yoksa ekran eksik izni önceden göremez, bağlantı çizilir ve 403 gövdesi tarayıcıda JSON
  olarak açılır (dosya bağlantılarının bugünkü hata davranışı, 503 için de aynı; ebeveynin konusu, O-8, §20).
- **Müşteri sayfası ekranda (F-1@fbafd3af, §8.6):** müşteriler sekmesi (`drawCustomers`, `case-detail.js:229-252`) yalnız seçili müşteri
  sayfasının satırlarını çizer (25 müşteri; özet satırı `Recall.Cust.Summary` bugünkü gibi bütün satırları sayar, `:231-232`) ve küme 25'i
  aşınca altında "Müşteriler {0}–{1} / {2}" (`Recall.Customer.NamesPage`) ile önceki / sonraki düğmeleri (`Recall.Customer.PagePrev` /
  `PageNext`) gösterir; sayfa değişince `customers` parçası `customerPage` ile yeniden okunur. İade bağlama seçim listesi (`openLink`, `:400-412`)
  aynı sayfanın müşterilerini listeler ve pencerede aynı önceki / sonraki düğmelerini taşır. Kapsam sekmesi, kapsam farkı listesi, iade bağları,
  etkinlik tablosu ve iz ekranı bütün satırları bugünkü gibi çizer; küme 25'i aşınca tablonun üstünde aynı sayfa seçicisi görünür ve seçilen
  sayfanın `Crm` müşterileri ad + kodla, öteki `Crm` satırları `Recall.Customer.Deferred` ile görünür. Bu tablolardan komut verilmez.
- **Kabul edilen sınır — müşteri sayfalarının sırası ve bulunması (F-3@807c8dc2; CT kararı, MVP-1 CT, 2026-11-04):** sayfa dilimi yanıtın
  müşteri anahtarı kümesinin **ordinal** sırasıyla kesilir (§8.6; `CaseDetailReadHandlers.cs:75`). `Crm` anahtarları küçük harfli GUID'dir;
  bu yüzden `Crm` müşterileri kendi aralarında fiilen **GUID sırasıyla** — kullanıcı için anlamsız, rastgele görünen bir sırayla — dizilir
  ve CRM kimliksiz anahtarlar (ör. `CUST-…`, `DOC:…`, `warehouse-outbound:…`) metinlerinin ordinal yerine göre araya girer (rakamlar <
  büyük harfler < küçük harfler). Ad yalnız sayfa içinde çözüldüğünden **adla sıralama ve adla arama yoktur**; müşteriler sekmesi ve iade
  bağlama seçim listesi 25'er müşteri gösterir. Sonuç: 200 müşterili vakada belirli bir müşteriye bildirim girmek ya da iade bağlamak için 8
  sayfaya kadar gezinmek gerekebilir ve her sayfa değişimi bir CRM turu ister (`Page` kipi, ≤ 3 sn). Bu, sayfaya bağlı çözümün (CT-6) doğal
  sonucudur ve DL-1 / DL-2'de değiştirilmez; sayfa içinde de satırlar anahtar sırasıyla kalır (AC-28 (b), (f) ve AC-31 (a) bu sırayı sabitler).
  İyileştirme ayrı takiptir (FU-0177-FU01-FIND, §20).
- **CRM kimliksiz satırlar (rozet ve ipucu, F-7):**
  `Legacy` (kaynak `Unknown`, FU01 öncesi) → bugünkü etiket + "Eski kayıt" rozeti (`Recall.Customer.LegacyBadge`), ipucu
  `Recall.Customer.LegacyHint` ("CRM müşteri kimliğinden önce kaydedildi; yeni kimliğe otomatik bağlanmaz.").
  `NoCrm`, kaynak `ShipTo` / `Document` (FU01 sonrası) → etiket + "CRM kimliği yok" rozeti (`Recall.Customer.NoCrmBadge`), ipucu
  `Recall.Customer.NoCrmHint` ("Sevkiyat okunduğunda CRM müşteri kimliği yoktu ya da okunamadı.").
  `NoCrm`, kaynak `Manual` → etiket + "CRM kimliği yok" rozeti, ipucu `Recall.Customer.ManualHint` ("Elle girildi; CRM müşteri kimliğine
  bağlanmaz.", Q-6). Hiçbiri CRM satırıyla **birleştirilmez**. "GUID gösterilmez" kuralı `Crm` kaynaklı anahtarı bağlar; CRM kimliksiz satırın
  etiketi saklı metnin kendisidir (kullanıcının ya da MVP-6'nın metni, bugünkü gibi) — AC-10 / AC-22 fixture'larında böyle satır A metnini
  taşımaz.
- **CSV / PDF işareti (F-11):** kanıt paketinde CRM kimliksiz satırın hücresi, **vakada en az bir `Crm` satırı varsa** "{etiket} (Eski
  kayıt)" (`Recall.Customer.LegacyMarked`) ya da "{etiket} (CRM kimliği yok)" (`Recall.Customer.NoCrmMarked`) olur. Yalnız CRM kimliksiz
  satırları olan vakada hücre bugünkü gibi saklı anahtardır (işaretsiz). Gerekçe: işaret yalnız iki türün aynı dosyada yan yana durduğu
  durumda ayırt etmeye yarar; FU01 öncesi vakaların kanıt dosyası bugünkü çıktıyla aynı kalır (mevcut `RecallEvidenceTests.cs:144`
  `4_customers` satırında saklı teslim yeri metnini birebir arar).
- **İz dışa aktarımı (`exportTrace`) işareti (F-5@fbafd3af):** `exportTrace` bir lot izidir, vakası yoktur (`ExportRecallTraceHandler`,
  `TraceHandlers.cs:93-109`; CSV `RecallTraceService.Csv`, `:205-223`); kural **izin kendi düğümlerine** bağlıdır: izde **en az bir `Crm`
  kaynaklı Shipment düğümü varsa** "Depo / Müşteri" hücresi (`:213` `place`) `Crm` düğümünde §8.8 metnidir (`Resolved` "{ad} · {kod}";
  `Unresolved` / `NotFound` `…With` metni, ayırıcı düğümün kendi `ShipToReference`'ı (okuma `Ok`, `IsDShaped` değil) ·
  `Movement.TransactionNumber`), CRM kimliksiz Shipment düğümünde "{anahtar} (CRM kimliği yok)" (`Recall.Customer.NoCrmMarked`). İzde
  `Forbidden` düğüm varsa CSV üretilmez: 403 `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN` (§8.10). Canlı iz
  `Legacy` üretmez (her anahtarlı Shipment düğümü §8.2'de bir kaynakla doğar), bu yüzden iz CSV'sinde "Eski kayıt" işareti yoktur. İzde hiç
  `Crm` düğümü yoksa CSV FU01 öncesi koddan sabitlenmiş vektöre **bayt eşittir** (anahtarsız düğümün hücresi bugünkü gibi boş). Sözcüklerin
  dili `Accept-Language`'tır (`EvidenceLanguage()`, servis `RecallController.cs:218-222` — `ExportRecallTraceCommand` `Language` alır; Web
  bu başlığı dosya isteğinde de iletir, Repo Scope DL-2). Ad çözümü `Export` kipindedir (§8.6). CSV başlığı ve düğüm türü bugün sabit metindir
  (`:210`, `:216`) — ebeveynin konusu, FU01 değiştirmez (O-5, §20).
- Görünen yerler (ölçüldü): vaka detayı kapsam sekmesi (`case-detail.js:135`), kapsam farkı listesi (`:165`, `getCaseScopeDiff`
  `added` düğümleri — bugün `n.customerRef`'i basar), müşteriler sekmesi (`:248` — bugün anahtarı tek aralıklı alt satırda basar), iade bağları
  (`:259`), etkinlik tablosu (`:287`), bildirim / tüketim / iade bağlama pencereleri (`:363`, `:380`, `:405`), iz ekranı (`trace-view.js:32`),
  iz dışa aktarımı (`RecallTraceService.cs:213` "place"), kanıt paketi müşteri, iade ve iz bölümleri (`RecallEvidence.cs:283-295`, `:428-440`),
  zaman çizelgesi ve kanıt paketinin zaman çizelgesi bölümü (iade bağı olayının metni — `R3Handlers.cs:438`, `RecallEvidence.cs:302-303`;
  §8.4 kuralıyla `Crm` eşleşmesinde anahtarsız).
  Ayırıcı zorunluluğu komut seçilen yerlerde (müşteriler sekmesi, üç pencere, seçim listesi — hepsi `getCaseCustomers`'tan beslenir), iz
  ekranında ve kanıt paketindedir; kapsam, etkinlik ve iade bağı tablolarında satırın kendi belge / hareket sütunu bugün de görünür ve bu
  tablolardan komut verilmez.
- Kişisel veri: tarayıcıya müşteri hakkında yalnız ad ve kod gider; adres, iletişim, durum, faturalanabilirlik gitmez. `shipTo` MVP-6'nın
  bugün de gösterilen teslim yeri metnidir (`CaseCustomerShipment.shipToReference`), yeni kişisel veri değildir.

### 8.9 Durum ve komut tablosu

Yeni vaka durumu ve yeni komut yok. Okuma uçları (`getCaseCustomers`, `getCaseEffectiveness`, `getCaseScope`, `getCaseScopeDiff`,
`getCaseReturns`, `runTrace`) `customer` nesnesini ve isteğe bağlı `customerPage` parametresini ekler (§8.6 `Page`); `exportTrace` ve kanıt
paketi önizleme / dışa aktarma müşteri hücresini §8.8 metniyle yazar (§8.6 `Export`); izinleri değişmez.

| Komut | Durum | FU01 etkisi |
|---|---|---|
| `addScopeItems` (Shipment) | `Draft` | `Manual` kaynağı yazılır; `IsDShaped` `customerRef` → 400 `INVALID_REQUEST`, yazım 0 (Q-6) |
| `submitScope` | `Draft` | §8.5 tablosu (Q-5: FU01 öncesi anahtar yükseltilmez; FU01 sonrası `ShipTo` / `Document` `Crm`'e geçebilir; `Crm` → `Crm` güncellenir) |
| `reviseScope` | `Active` | kopya `CustomerRefSource`'u taşır; hiçbir kopya yükseltilmez (§8.5) |
| `recordNotification`, `declareConsumption`, `linkReturn` | `Active` / `EffectivenessCheck` | §8.3 eşleşmesi (§8.4); bütün mevcut denetimlerden sonra `Crm` satırı `Unresolved` → 503 `RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE`, `Forbidden` → 403 `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN`, yazım 0; `NotFound` → yazılır (Q-7); `linkReturn` `Crm` eşleşmesinde olay metni anahtarsız |
| `createEvidenceExport` | — | ad anlık görüntüsü (`Export` kipi, toplam tavansız, devre kesiciyle); `Unresolved` satır işaretli girer, dışa aktarım reddedilmez; `Forbidden` müşteri varken 403 `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN`, dışa aktarım günlüğü satırı, dosya ve denetim satırı yazılmaz; aynı anahtarla daha önce saklanmış dışa aktarımın tekrarı 200 (§8.10) |
| `previewEvidence`, `exportTrace` (okuma / dosya) | — | `Export` kipi, devre kesiciyle; `Forbidden` müşteri / düğüm varken 403 `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN`; `exportTrace` reddinde `recall.trace.run` (`exportTrace` bağlamı) denetim satırı yazılmaz (§8.10) |
| diğer komutlar | — | değişmez |

### 8.10 Kanıt paketi ve ad anlık görüntüsü

CT kararı CT-3 (MVP-1 CT, 2026-11-03; F-1); bütçe CT-6 (2026-11-04); devre kesici ve izin reddi CT-9, CT-10 (2026-11-04).

- `previewEvidence` ve `createEvidenceExport` aynı kurucuyu kullanır (`EvidenceHandlers.cs:56`, `:97-98`); kurucu vakanın **bütün** `Crm`
  anahtarlarını `IRecallCustomerDirectory` ile **bir kez**, `Export` kipinde (toplam tavansız, devre kesiciyle, §8.6) çözer. Müşteri hücresi
  (müşteriler bölümü `RecallEvidence.cs:283-288`, iade satırı `:295-296`, iz bölümü "place" `:440-442`) §8.8 metnidir: `Resolved` → "{ad} ·
  {kod}"; `Unresolved` → "Çözülemedi ({ayırıcı})" (`Recall.Customer.UnresolvedWith`); `NotFound` → "Bulunamadı ({ayırıcı})"
  (`Recall.Customer.NotFoundWith`); `Forbidden` dosyaya girmez (aşağıda "İzin eksik — ret"). Bir müşterinin metni dosyanın her bölümünde aynıdır (müşteri
  düzeyinde bir kez kurulur). Etkinlik tabloları müşteri sütunu taşımaz (`:351-353`, SKU / birim toplamları) — değişmez.
- **Kanıt paketinde ayırıcının kaynağı (F-4@fbafd3af):** r3'ün "kurucu ledger hareketlerini zaten okur, `:427`" iddiası yanlıştı — `:427`
  okuması yalnız iz bölümündedir (`TraceSectionAsync`) ve mühürlü anlık görüntü yoksa hiç yapılmaz (`:423` `snapshot is null` → okumadan döner);
  müşteri bölümü (`:281-288`) ve iade satırı hareket okumaz. FU01: vakada `Resolved` olmayan (`Unresolved` / `NotFound`; `Forbidden` varsa
  kurucu bu adıma gelmeden reddeder) en az bir `Crm` müşterisi varsa `CaseAsync` müşteri bölümü için **ayrı** bir hareket okuması yapar: `_ledger.SalesIssuesOfLotsAsync` (kurucunun
  bugünkü `IRecallTraceSource` bağımlılığı, `RecallEvidence.cs:130`; port `IRecallTraceSource.cs:59`) o müşterilerin kapsam satırlarının
  lotlarıyla; satır ↔ hareket eşleşmesi ve sıra §8.8'deki tek kuraldır (`RecallReads.MovementsBehind`, `getCaseCustomers` ile aynı). Ayırıcı =
  bu sırayla ilk boş olmayan hareket numarası; kurucu MVP-6'yı okumaz, bu yüzden teslim yeri metni dosyada yoktur; numara yoksa yalnız durum
  metni ("Çözülemedi"). Mühürsüz vakada (ör. `Draft`) de aynı okuma yapılır. Bütün `Crm` müşterileri `Resolved` ise ya da vakada `Crm` satırı
  yoksa bu okuma yapılmaz. Müşteri bölümüne kimlik sütunu (`customerRef`) **eklenmez**.
- **Anlık görüntü:** `createEvidenceExport` üretilen baytları saklar ve hash'ler (`EvidenceHandlers.cs:101-156`); dosya, dışa aktarım
  anındaki CRM adını taşır, CRM'de ad sonradan değişse de saklı dosya ve hash değişmez (`getEvidenceFile` saklı baytları döner). Vakada en az
  bir `Crm` satırı varsa özet bölümüne bir satır eklenir: alan `customerNames` (`Recall.Ev.W.customerNames`, "Müşteri adları"), değer
  `Recall.Ev.W.customerNamesSnapshot` ("CRM'den, dışa aktarım anında"). Yalnız CRM kimliksiz satırları olan vakada özet ve müşteri bölümü
  bugünkü çıktıyla aynıdır.
- **Kesinti:** dışa aktarım anında çözülemeyen satır dosyaya "Çözülemedi" işaretiyle girer; dışa aktarım **reddedilmez** (§8.6 `Export`:
  zaman aşımı olan okuma da, devre kesiciyle çağrılmayan hesap da `Unresolved`'dır). Kullanıcı CRM düzelince yeni bir dışa aktarım alabilir;
  eski dosya kanıt olarak kalır.
- **İzin eksik — ret (CT kararı, MVP-1 CT, 2026-11-04; F-2@807c8dc2):** konunun `Crm` müşterilerinden (izde `Crm` düğümlerinden) en az biri
  `Forbidden` ise `previewEvidence`, `createEvidenceExport` ve `exportTrace` **403 `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN`** ile reddedilir;
  `Forbidden` + `Unresolved` karışık konuda da 403 (izin çağırana özgüdür, kesinti değildir). Gerekçe: r4'te izni eksik kullanıcının dosyası
  bütün `Crm` müşterilerini "İzin eksik" diye **kalıcı** olarak saklıyordu; aynı vakanın GxP dosyası dışa aktaranın kim olduğuna göre
  değişiyordu ve dışa aktarım rolleri (`recall.evidence.export`, `recall.trace.export`; `Infrastructure/Authorization/RecallPermissions.cs:47`, `:15`) kayıt komutlarını
  taşımadığı için AC-30 kontrolünün dışındaydı. Çağıran izni düzeltebilir; 403 bunu söyler (§8.4 gerekçesiyle aynı), dosya üretilmez.
  - **Yer:** ret, işleyicinin bugünkü bütün denetimlerinden **sonra**, yazımdan **önce**: `createEvidenceExport` biçim (`EvidenceHandlers.cs:85`)
    → parmak izi ve tekrar (`:87-94` — aynı anahtarla daha önce saklanmış dışa aktarım 200 ve aynı dosyayı döner, CRM çağrılmaz) → kurucunun
    konu doğrulaması (`RecallEvidence.cs:167` ve sonrası, 400 / 404) → ad çözümü → **ret** (kurucunun başarısızlık dönüşüyle, `:99`) → saklama
    (`:100-156`) değişmez. `previewEvidence` aynı kurucuyla (`:56-58`). `exportTrace`: biçim ve parse (`TraceHandlers.cs:95-99`) → iz
    (`:101-102`, 404) → ad çözümü → **ret** → denetim satırı (`:103-107`) → CSV. Reddedilen istekte `recall_evidence_exports`, dosya,
    `recall.evidence.exported` ve `recall.trace.run` denetim satırı **yazılmaz**.
  - Tatbikat ve dönem kanıtı CRM çağırmaz (`RecallEvidence.cs:382` `notApplicableDrill`, `:408` `notApplicablePeriod`; tatbikatın iz
    bölümü kapsam satırı almaz, `:380` `TraceSectionAsync(snapshot, [], …)`, "place" depo / yer olur, `:439-441`) — reddedilmez.
  - Devre kesici açılınca çağrılmayan hesapların izni bilinmez; o dosya `Unresolved` işaretiyle üretilir (§8.6).
- **Saklı dosyaya erişim (yazar kararı — inceleme konusu):** `getEvidenceFile` ve dışa aktarım günlüğü (`recall.evidence.export`) CRM
  izni sormaz; saklı dosya, dışa aktarım anında CRM'i okuyabilen kullanıcının ürettiği GxP kaydıdır (CT-3 anlık görüntüsü) ve erişimi
  recall izniyle yönetilir. CRM izni yalnız **canlı** ad okumasını ve yeni dosya üretimini bağlar. Aynı kural aynı anahtarla tekrar için de
  geçerlidir (tekrar yeni dosya üretmez).
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
| yalnız Web | `Recall.Customer.CrmForbidden` | "CRM müşteri adlarını görme izniniz yok; yöneticinize başvurun." |
| yalnız Web | `Recall.Errors.RECALL_CUSTOMER_DIRECTORY_FORBIDDEN` | "CRM müşteri adlarını görme izniniz yok; yöneticinize başvurun." |
| yalnız Web | `Recall.Customer.Deferred` | "Ad bu sayfada değil" |
| yalnız Web | `Recall.Customer.NamesPage` | "Müşteriler {0}–{1} / {2}" |
| yalnız Web | `Recall.Customer.PagePrev` | "Önceki" |
| yalnız Web | `Recall.Customer.PageNext` | "Sonraki" |
| yalnız Web (r5: ortaktan taşındı) | `Recall.Customer.Forbidden` | "İzin eksik" |
| yalnız Web (r5: ortaktan taşındı) | `Recall.Customer.ForbiddenWith` | "İzin eksik ({0})" |
| yalnız Web (r5, F-2@807c8dc2) | `Recall.Customer.ExportForbidden` | "CRM müşteri adlarını görme izniniz yok; dışa aktarım yapılamaz. Yöneticinize başvurun." |

- **r5 (F-2@807c8dc2):** `Forbidden` artık hiçbir dosyaya (kanıt CSV / PDF, iz CSV'si) girmez — dışa aktarım reddedilir (§8.10); bu yüzden
  `Recall.Customer.Forbidden` ve `…ForbiddenWith` servis ailesinden çıkar, yalnız Web'de (ekran) kalır. Dışa aktarım reddinin hata metni
  ayrı anahtar istemez: kod eşlemesi aynı koddur (`Recall.Errors.RECALL_CUSTOMER_DIRECTORY_FORBIDDEN`, nedeni ve çözümü söyler, yeniden
  deneme önermez); `Recall.Customer.ExportForbidden` yalnız iz ekranında CSV bağlantısının yerine çizilir (§8.8).
- Servis ailesi kanıt paketi PDF / CSV sözcüklerini taşır; **parite bekçisi** (`EvidenceTextParityTests`) ortak anahtarın Web ailesinde aynı
  değerle bulunmasını ister → ortak anahtarlar **DL-1'de** iki ailede birlikte eklenir; DL-2 yalnız kullanır. Yalnız Web anahtarları da DL-1'de
  eklenir (Repo Scope DL-1), DL-2 kullanır.
- Hardcoded metin yok; `recall.l10n.js` / `Recall.cs` anahtar listesine eklenir.
- İki hata metni (`Recall.Errors.RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE`, `…_FORBIDDEN`) her dilde düştükleri genel metinlerden
  (`Recall.Errors.SERVICE_UNAVAILABLE`, `Recall.Errors.FORBIDDEN` — tr "Geri çağırma servisine ulaşılamıyor. Biraz sonra yeniden deneyin." /
  "Bu işlem için yetkiniz yok.", `Recall.tr.resx`) **farklı** olmalıdır; AC-27'nin S-27c / S-27d sabotajları bu farka dayanır (AC-19 bunu 7
  dilde sınar). `…_FORBIDDEN` metni yeniden denemeyi önermez.

## 12. Validation Rules

| Girdi | Kural | Sonuç |
|---|---|---|
| SHIPMENT `customerRef` | string, `Guid.TryParseExact(…, "D")`, `≠ Guid.Empty` | aksi hâlde yok sayılır, sıra 2 (§8.2) |
| SHIPMENT `shipToReference` | `IsDShaped` değil (F-12) | `IsDShaped` ise anahtar yapılmaz, sıra 3 (§8.2) |
| komut `customerRef` | 1–200 karakter (bugünkü); `TryCanonical` başarılıysa parmak izi kanonik, `Crm` satırıyla eşleşirse kayıt kanonik (§8.3) | kapsamda yoksa 404 `RECALL_UNKNOWN_CUSTOMER`; öteki bütün denetimlerden sonra `Crm` satırı `Unresolved` ise 503 `RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE`, `Forbidden` ise 403 `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN` (§8.4) |
| okuma `customerPage` (altı ekran okuması) | isteğe bağlı tamsayı ≥ 1, varsayılan 1 (§8.6) | < 1 ya da tamsayı değil → 400 `INVALID_REQUEST`; küme boyunu aşan sayfa → 200, bütün `Crm` satırları `Deferred`, CRM'e 0 çağrı |
| `addScopeItems` Shipment `customerRef` | 1–200 karakter (bugünkü); `IsDShaped` olmamalı (Q-6, §8.2) | `IsDShaped` ise 400 `INVALID_REQUEST`, tüm istek reddedilir, yazım 0 |
| CRM 200 gövdesi | §8.6 tablo | `Unresolved` |
| dışa aktarım konusu (`previewEvidence`, `createEvidenceExport` vakası; `exportTrace` izi) | `Crm` müşterilerinden / düğümlerinden hiçbiri `Forbidden` olmamalı (§8.10) | aksi hâlde 403 `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN`, yazım 0 (dışa aktarım günlüğü, dosya, denetim satırı); `Unresolved` reddettirmez |

## 13. Failure Path to Verify

1. MVP-6 3.4.0 (alan yok) ya da `customerRef` boş / geçersiz → sıra 2 / 3, bugünkü sonuç bayt eşit (AC-2).
2. CRM yanıt vermiyor (zaman aşımı) → satır "Çözülemedi"; ekran okumasında CRM'in istek süresine katkısı ≤ 3 sn + pay, müşteri sayısından
   bağımsız (AC-12); dışa aktarımda toplam tavan yok, satırlar işaretli girer; devre kesici ilk yanıtsız dalgadan sonra kalan hesapları
   çağırmaz — 400 hesapta süre ≈ 2 sn + pay, Gateway'in 90 sn sınırının çok altında (AC-29 (d)).
3. Yetkisiz: kullanıcıda `crm.account.lookup-validation` yok → CRM 401 / 403 → "İzin eksik" (yeniden deneme önerilmez), recall ekran
   okumaları 200, `Crm` satırına kayıt 403 `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN` (AC-18, AC-27); aynı kullanıcının kanıt önizlemesi, kanıt
   dışa aktarımı ve iz CSV'si aynı 403, dosya ve denetim satırı yok (AC-26 (h), (i), AC-29 (i)); canlı geçişten önce her kiracıda kayıt ve
   dışa aktarım rollerinin ataması kontrol edilir (AC-30); recall okuma izni yok → bugünkü 403.
4. Başka kiracının hesabı (MVP-6 veri hatası) → CRM 404 → "Bulunamadı", ad sızmaz (AC-11).
5. Tekrar: aynı idempotency anahtarı, GUID farklı harfle → 200 tekrar (AC-3); CRM kapalıyken daha önce yazılmış isteğin tekrarı → 200 (AC-25).
6. Yeniden izleme sırasında MVP-6 erişilemez → saklı CRM anahtarı korunur (AC-6 (b)).
7. Kayıtsız `Draft` vakada yeniden izleme MVP-6'dan `customerRef` alır → FU01 öncesi anahtar ve kaynak kalır (Q-5; AC-6 (a)); FU01 sonrası
   geçici `Document` satırı `Crm`'e geçer (AC-6 (a4)).
8. Elle kapsam satırında GUID biçimli `customerRef` (CRM müşterisi seçme denemesi) → 400 `INVALID_REQUEST`, yazım 0 (Q-6; AC-20).
9. Revizyon kopyası kaynağı düşürür → `Crm` satırı eski görünür ve büyük harfli GUID'le kayıt 404 olur — engellenir (AC-6 (e)).
10. CRM kesintisinde aynı vakada iki çözülemeyen müşteri → satırlar ayırıcıyla ayrılır, bildirim / beyan / iade bağı ekranda kapalı, sunucuda
    503, yazım 0 (AC-25, AC-27).
11. CRM kesintisinde kanıt dışa aktarımı → dosya üretilir, çözülemeyen satır işaretli; CRM düzelince saklı dosya değişmez (AC-26). İzni
    eksik kullanıcının dışa aktarımı → dosya üretilmez, 403 (AC-26 (h)); "İzin eksik" hiçbir saklı dosyaya girmez.
12. Sağlıklı ama gecikmeli CRM (200 ms), 200 hesaplı vaka → ekranda her müşteri sayfası tam çözülür (25 `Resolved`, öteki satırlar
    `Deferred`, hiç `Unresolved` yok); kanıt dışa aktarımında 200 hesabın hepsi çözülür (AC-28, AC-29, AC-31).
13. CRM kapalıyken ya da izin eksikken geçersiz kayıt isteği (yanlış `supersedesId`, aşan miktar, uygun olmayan iade) → bugünkü 400 / 422,
    CRM'e 0 çağrı — 503 / 403 değil (AC-25).
14. `Crm` müşterisine bağlanan iadenin zaman çizelgesi metni → GUID yok (AC-10).
15. CRM dışa aktarımın ortasında düşer (ilk iki dalga yanıtlanır, sonrası yanıtsız) → üçüncü dalga zaman aşımıyla biter, kalan hesaplar
    çağrılmaz, dosya 201 ve işaretli (AC-29 (f)); her dalgada bir okuma zaman aşımına uğrarsa kesici açılmaz (AC-29 (g); kalan sınır O-7).
16. Sağlıklı CRM'de ilk dalganın bütün hesapları 404 → kesici açılmaz, kalan hesaplar okunur (AC-29 (h)).

## 14. Authorization Convention

Yeni recall izin anahtarı yok; okuma uçları mevcut `[HasPermission]` anahtarlarıyla (ebeveyn §14.2). CRM çağrısı CRM'in
`crm.account.lookup-validation` anahtarını ister (CRM taslağı §6, K-1); `crm.account.read` gerekmez ve verilmez. Q-1 = kullanıcı token'ı ise
anahtarın geri çağırma rollerine (ebeveyn §14.3) atanması Auth işidir (D-4, Ali) ve **DL-1 kabul kapısıdır** (CT kararı, 2026-11-03):
atanmamışken ekran okumaları "İzin eksik" (`Forbidden`) ile çalışır, ama `Crm` satırına kayıt **403 `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN`** ile
reddedilir (§8.4; F-2@fbafd3af — r3'te bu durum "Çözülemedi" ve 503'tü, kullanıcıya "sonra tekrar deneyin" diyordu) ve `Crm` müşterisi olan
konunun kanıt önizlemesi / dışa aktarımı ile iz CSV'si aynı 403'le reddedilir (§8.10; F-2@807c8dc2). Varsayılan Görüntüleyici
rolü bu anahtarı kendiliğinden almaz (`DefaultRolePermissionTemplate.cs:207-211`, yalnız `read` eylemi). Q-1 = servis kimliği ise yetki modeli
değişir; §7 D-3.

**Canlı geçiş kontrolü (CT kararı, MVP-1 CT, 2026-11-04; AC-30):** ebeveyn rol şablonu yalnız yeni kiracılara uygulanır (ebeveyn §14.3,
ROLE1); mevcut kiracıların rolleri kiracıya özgüdür ve kabul ortamındaki atama onları kapsamaz. Bu yüzden **her hedef ortamda, FU01 kod
geçişinin hemen öncesinde, kiracı başına** salt-okuma bir kontrol koşulur: kiracıda `recall.notifications.create`, `recall.consumptions.create`,
`recall.returns.link`, `recall.evidence.export` ya da `recall.trace.export` anahtarlarından (`Infrastructure/Authorization/RecallPermissions.cs:15`, `:35-39`, `:47`) en az
birini taşıyan her rol (ve Q-4 yanıtının adlandırdığı başka roller) `crm.account.lookup-validation`'ı da taşımalıdır. Dışa aktarım rolleri
r5'te eklendi (F-2@807c8dc2): kalite ve denetim rolleri kayıt komutlarını taşımayabilir; atamaları eksikse dışa aktarımları 403 alır (§8.10). Sonuç kiracı başına "eksik atamalı rol sayısı" (rol adları geçiş kaydına, kişi adı yazılmaz) — beklenen 0. Sorgunun biçimi Auth
sahibinin (Ali) rol-izin verisi üzerindedir (XMC-001: recall Auth verisine yazmaz; sorgu Q-4 yanıtıyla birlikte verilir). Kontrolü MVP-1 CT
koşar; geçişi yapan kişi geçiş kaydında sonucu imzalar (AC-20 (f) ile aynı kayıt); bir kiracıda sonuç 0 değilse o ortamın geçişi durur, eksik
atama Auth sahibine gider. Geçişten sonra rol değişirse ekran "İzin eksik" metniyle nedeni gösterir (§8.8).

## 15. Gateway / API Routing Decision

Yeni rota yok. Recall: `/api/recall`, `/api/recall/{everything}` → 5067 (ocelot `:1106`, `:1124`). CRM: `/api/crm/accounts/{everything}` →
5061 (`:2979`); U-1 bu joker rotayla taşınır (CRM taslağı §8, K-4). `ocelot.json` değişmez; teyit integration-agent'ındır (yalnız not).

## 16. Acceptance Criteria

Ortak fixture: kiracı T1 / şirket LE1, ikinci kiracı T2; MVP-6 ikizi (`ShipmentBundleDouble`, mevcut — `customerRef` alanı eklenir) ve
**CRM ikizi** (yeni; kiracıyı yalnız gelen token'dan çözer, çağrı sayar, gecikme / durum kodu hesap başına programlanır). Hesaplar: A = `3c9a1f2e-7b4d-4e61-9a2c-5d8e0f1a2b3c`
(T1, "Example Hastanesi", `ACC-2026-000042`), B (T2), C ve D (T1, ikizde programlanan durumla), N (T1, ikizde 404). **Büyük vaka fixture'ı
V200** — sayfa ve dışa aktarım bütçesi ölçütleri için: T1'de `Active` vaka, 200 farklı `Crm` hesabı (A dahil; her biri tek sevkiyat satırlı, hareket numarası
`GIS-0001`…`GIS-0200`), CRM ikizi her hesabı programlanan gecikmeyle (varsayılan 200 ms) `Resolved` döner. **V400** — devre kesici ölçütleri
için V200'ün 400 hesaplı eşi (`GIS-0001`…`GIS-0400`; CRM ikizi hesap başına ya da ordinal sıra konumuna göre programlanır). Test sınıflarının adları **önerilir**,
kabulde adlarıyla kayda geçer. Her ölçüt tek satırdır (kabul aracı biçimi). Her sabotaj **üretim kodunda** tek tek uygulanır ve geri alınır;
kabul kaydına "kırmızı → geri al → yeşil, diff boş" yazılır.

**DL-1**

- [ ] AC-1 (zorunlu) — Sıra 1: sevkiyat 200 `customerRef = A`, `shipToReference = "CUST-100/ADDR-2"` → `runTrace` Shipment düğümü `customerRef = A`, kapsam satırı `CustomerRef = A`, `CustomerRefSource = Crm`. Test `RecallCrmCustomerKeyTests.CustomerRef_wins_over_shipTo`. Sabotaj S-1: `RecallTraceEngine.ShipmentsAsync`'te shipTo dalını `CustomerAccountId`'den önce değerlendir → kırmızı.
- [ ] AC-2 (zorunlu) — Sıra 2 / 3, her biri ayrı fixture: (a) alan yok (3.4.0 yanıtı); (b) `""`; (c) `"not-a-guid"`; (d) `Guid.Empty`; (e) sayı → anahtar `shipToReference`; (f) shipTo da boş → `DOC:<belge>`; (g) okuma 403 / 404 / zaman aşımı → `DOC:<belge>`; (h) `customerRef` yok ve `shipToReference` `"D"` biçimli bir GUID → anahtar `DOC:<belge>`, kaynak `Document`, günlükte `recall.shipment.ship_to_guid_shaped` satırı değersiz. (a)'da `runTrace` yanıtı FU01 öncesi koddan sabitlenmiş vektöre (`customer` alanı hariç) bayt eşit. Test `RecallCrmCustomerKeyTests.Fallback_order_*`. Sabotaj S-2a: okuyucuda GUID ayrıştırmasını kaldır, boş olmayan her string'i hesap say → (c) kırmızı; S-2b: `Guid.Empty` kontrolünü kaldır → (d) kırmızı; S-2c: sıra 2'nin `IsDShaped` bekçisini kaldır → (h) kırmızı.
- [ ] AC-3 (zorunlu) — Büyük/küçük harf ve kırpma: S1 `customerRef = A` büyük harfle, S2 küçük harfle → `getCaseCustomers` **tek** satır, anahtar küçük harf; (b) `recordNotification` `customerRef = A` büyük harfle → 201, saklanan değer küçük harf; (c) aynı idempotency anahtarıyla küçük harfle tekrar → 200, aynı `notificationId`; (d) `declareConsumption` `customerRef = "  " + A büyük harf + " "` → 201, saklanan değer kanonik A. Test `RecallCrmCustomerKeyTests.Case_insensitive_key`. Sabotaj S-3a: okuyucuda kanonikleştirmeyi kaldır (ham metni sakla) → iki satır, kırmızı; S-3b: komut eşleşmesini ham girişle ordinal yap → (b) 404, kırmızı; S-3c: parmak izini ham girişten hesapla → (c) 400 `IDEMPOTENCY_KEY_REUSED`, kırmızı; S-3d: `TryCanonical`'ın 36 karakter denetimini kırpmadan önceki ham değere uygula → (d) 404, kırmızı.
- [ ] AC-4 (zorunlu) — Kayıtlar yeni anahtarı kabul eder: onaylı kapsamda A (CRM ikizi `Resolved`) ile `recordNotification`, `declareConsumption`, `linkReturn` → 201; `getCaseEffectiveness` (A, SKU, birim) satırında `returned`, `consumed` sayılır; vaka olayı ve denetim satırının `customerRef` ayrıntısı kanonik A; kapsamda olmayan GUID → 404 `RECALL_UNKNOWN_CUSTOMER`, üç koleksiyonda yazım 0. Test `RecallCrmRecordTests.Records_accept_crm_key`. Sabotaj S-4: `LinkRecallReturnHandler` müşteri kümesini yalnız `Crm` olmayan satırlardan kur → `linkReturn` 404, kırmızı.
- [ ] AC-5 (zorunlu) — Eski kayıtlar değişmez, `Active` vakada: FU01 öncesi biçimde tohumlanmış vaka (kaynaksız kapsam satırları; `CUST-100/ADDR-2`, `warehouse-outbound:…:ship-to`, `DOC:…` anahtarlı bildirim, beyan ve iade bağı). (a) FU01 okumaları ve başka müşteriye yeni CRM kaydı sonrası eski belgeler bayt eşit; (b) `CUST-100/ADDR-2` ile bildirim → 201, değer aynen, CRM ikizine 0 çağrı; `cust-100/addr-2` → 404 (eski anahtar harfe duyarlı kalır); (c) `"D"` biçimli olmayan eski anahtarlı isteğin parmak izi FU01 öncesi koddan sabitlenmiş vektöre eşit; (c2) `"D"` biçimli eski anahtar vektörleri: küçük harfli eski anahtarın parmak izi sabit vektöre eşit; büyük harfli eski anahtarla FU01 öncesi yazılmış kaydın aynı anahtarla tekrarı 400 `IDEMPOTENCY_KEY_REUSED` (§8.3'te yazılı kabul edilen fark; gerçek veride yokluğu AC-20 (f)). Test `RecallCrmLegacyTests.Legacy_records_unchanged`. Sabotaj S-5: kanonikleştirmeyi (küçük harf) `"D"` biçimli olmayan anahtarlara da uygula → (b) ve (c) kırmızı.
- [ ] AC-6 (zorunlu) — Yeniden izlemede ve revizyonda kararlılık (§8.5), her biri ayrı fixture; (a)…(d) `Draft` vakada, hiç bildirim / beyan / iade bağı yokken (ön koşul testte doğrulanır: üç koleksiyonda vaka için 0 belge): (a) FU01 öncesi (`null`) `CUST-100/ADDR-2` satırı, MVP-6 artık aynı sevkiyat için `customerRef = A` döner → `submitScope` sonrası anahtar `CUST-100/ADDR-2` ve kaynak `null` kalır; aynı izde yeni belge düğümü A / `Crm` alır; `getCaseCustomers` iki satır, eski satırın `customer.state = Legacy`; (a2) aynısı `null` kaynaklı `DOC:` satırıyla → anahtar ve `null` kalır; (a3) silinmiş `Manual` satırın belgesi izle geri gelir, MVP-6 `customerRef = A` döner → satır geri yüklenir, anahtarı ve `Manual` kaynağı kalır; (a4) FU01 sonrası MVP-6 kesintisinde yazılmış `Document` satırı, sonraki izde `customerRef = A` → anahtar A, kaynak `Crm`; (a5) FU01 sonrası `ShipTo` satırı (3.4.0 yanıtıyla yazılmış), sonraki izde `customerRef = A` → anahtar A, kaynak `Crm`; (b) saklı `Crm` satırı, yeniden izlemede sevkiyat `Unavailable` → anahtar A ve `Crm` kalır; (c) `null` kaynaklı `DOC:` satırı, yeni iz shipTo döner → anahtar shipTo olur, kaynak `null` kalır; (c2) `null` kaynaklı shipTo satırı, yeni iz aynı shipTo → anahtar aynı, kaynak `null`; (c3) FU01 sonrası `Document` satırı, yeni iz shipTo (customerRef yok) → anahtar shipTo, kaynak `ShipTo`; (d) saklı `Crm` A, yeni iz `Crm` C → anahtar C, kaynak `Crm`, günlükte `recall.scope.customer_ref_changed` satırı düğüm anahtarıyla, A ve C metni günlükte yok; (e) `Active` vaka, kayıtlarla (A'ya ve `CUST-100/ADDR-2`'ye birer bildirim), MVP-6 artık `CUST-100/ADDR-2` sevkiyatı için de `customerRef = A` döner, izde bir yeni sevkiyat bulgusu (`customerRef = A`) var ve vakada FU01 sonrası bir `Document` satırı var → `reviseScope` (bulgu eklenerek) → r+1'de A satırı `Crm`, `CUST-100/ADDR-2` satırı `null`, `Document` satırı `Document` olarak kopyalanır, hiçbiri yükseltilmez; delta düğümü A / `Crm` alır; revizyon onaylandıktan sonra r+1 üzerinde A büyük harfle `recordNotification` → 201, saklanan değer küçük harf. Test `RecallCrmReconcileTests.*`. Sabotaj (her biri ayrı): S-6a `Reconcile`'da anahtarı bugünkü gibi koşulsuz üzerine yaz (`:141`) → (a), (a2), (a3), (b) kırmızı; S-6b `null` kaynağı da FU01 sonrası gibi yükselt → (a), (a2) kırmızı, (a4) yeşil; S-6c FU01 sonrası `ShipTo` / `Document`'i eski say (r2 kuralı) → (a4), (a5) kırmızı; S-6d anahtar değişince `null` kaynağa yeni izin kaynağını yaz → (c) kırmızı; S-6e `Crm` → `Crm` farkında saklı anahtarı koru (r2 kuralı) → (d) kırmızı; S-6f `ReviseRecallScopeHandler` kopyasından `CustomerRefSource`'u çıkar → (e) kırmızı (A satırı `Legacy`, büyük harfli bildirim 404).
- [ ] AC-7 (zorunlu) — Çözüldü: CRM 200 `{accountId: A, code, name, status: "inactive", isBillable: false, contractVersion: "crm-account-lookup/1.0"}` → satır `customer = {source: Crm, state: Resolved, name, code}`, `label`, `shipTo`, `shipmentNumber` yok. Test `RecallCustomerDirectoryTests.Resolved_ignores_billability`. Sabotaj S-7: çözümü `isBillable = true` koşuluna bağla → `Unresolved`, kırmızı.
- [ ] AC-8 (zorunlu) — Çözülemedi, her biri ayrı fixture: 400, 500, 503, zaman aşımı, ulaşılamıyor, `accountId` farklı, `name` boş, `code` yok, `contractVersion = crm-account-lookup/2.0`, ayrıştırılamayan gövde → `state = Unresolved`, `name` / `code` yok (401 / 403 `Unresolved` değil `Forbidden`'dır — AC-18). Test `RecallCustomerDirectoryTests.Unresolved_*`. Sabotaj S-8a: 5xx'i `NotFound` say → kırmızı; S-8b: `accountId` karşılaştırmasını kaldır → kırmızı.
- [ ] AC-9 (zorunlu) — Bulunamadı: CRM 404 → `state = NotFound`. Test `RecallCustomerDirectoryTests.NotFound_is_404_only`. Sabotaj S-9: 404'ü `Unresolved`'a eşle → kırmızı.
- [ ] AC-10 (zorunlu) — GUID gösterilmez (sunucu): A'lı vakada `getCaseCustomers`, `getCaseEffectiveness`, `getCaseScope`, `getCaseScopeDiff`, `getCaseReturns`, `getCaseTimeline`, `runTrace` yanıtlarında A metni yalnız `customerRef` adlı alanlarda (`refs.customerRef` dahil) geçer — `label`, `text` ve `customer.*` alanlarında (`shipTo`, `shipmentNumber` dahil) geçmez; `exportTrace` CSV'si, kanıt paketi CSV ve PDF metni A içermez (Resolved, Unresolved, NotFound; Forbidden'da üç dışa aktarım ucunun 403 gövdesi A içermez; ekran okumalarında Resolved, Unresolved, Forbidden, NotFound ve Deferred); (d) izinde A bulunan tatbikatın `getDrillRun` yanıtında A yalnız `customers` anahtar dizisinde geçer (§8.7, §8.8 tatbikat istisnası); (e) A'ya bağlanan iadenin olayı: `getCaseTimeline` `text` = hareket numarası (müşteri soneki yok), A yalnız `refs.customerRef`'te; kanıt paketinin zaman çizelgesi bölümünde A yok; `CUST-100/ADDR-2`'ye (CRM dışı) bağlanan iadenin metni bugünkü `"{no} → CUST-100/ADDR-2"` (§8.4); (f) iz CSV'si işareti (F-5@fbafd3af, §8.8): izde A (`Resolved`), C (`Unresolved`, `shipToReference = "CUST-200/ADDR-1"`) ve yalnız teslim yerli bir düğüm (`CUST-400/ADDR-1`, kaynak `ShipTo`) → `exportTrace` CSV'sinde (`Accept-Language: tr`) A hücresi "Example Hastanesi · ACC-2026-000042", C hücresi "Çözülemedi (CUST-200/ADDR-1 · <C'nin hareket numarası>)", teslim yerli düğüm "CUST-400/ADDR-1 (CRM kimliği yok)"; hiç `Crm` düğümü olmayan izin CSV'si FU01 öncesi koddan sabitlenmiş vektöre bayt eşit. Test `RecallCrmNoGuidDisplayTests.*` (tek JSON tarayıcı yardımcı, izin verilen yollar adıyla). Sabotaj (her biri ayrı): S-10a `CaseDetailReadHandlers` etiketinde CRM satırı için `?? g.Key` → kırmızı; S-10b `RecallEvidence` müşteri satırına ham anahtar → kırmızı; S-10c iz dışa aktarımında "place" = `n.CustomerRef` (bugünkü `:213`) → kırmızı; S-10d ayırıcının sevkiyat numarasını `SourceDocumentId`'den al → `shipmentNumber`'da GUID, kırmızı; S-10e `linkReturn` olay metninde `Crm` eşleşmesinde de `→ {customer}` sonekini bırak (bugünkü `:438`) → (e) kırmızı; S-10f iz CSV'si işaretini `Crm` düğümü olmayan ize de uygula → (f) bayt eşitliği kırmızı.
- [ ] AC-11 (zorunlu) — Kiracı izolasyonu: (a) T2 vakası, MVP-6 ikizi `customerRef = A` (T1 hesabı) döner → CRM ikizi T2 token'ıyla 404 → `NotFound`; yanıtta T1 adı / kodu yok; (b) T1 kullanıcısı T2 vakasının müşterilerini okur → 404 (bugünkü); (c) CRM isteği çağıranın `Authorization` ve `X-Correlation-Id` başlığını taşır, kiracı başlığı ve `X-Legal-Entity-Id` göndermez. Test `RecallCrmTenantTests.*`. Sabotaj S-11: CRM `HttpClient` kaydından `.AddHttpMessageHandler<ForwardCallerHeadersHandler>()`'ı kaldır → (c) kırmızı (ikiz 401 → (a)'nın beklenen `NotFound`'u da `Forbidden` olur).
- [ ] AC-12 (zorunlu) — Yanıtsız CRM'de sayfa tavanı ve eşzamanlılık, `Page` kipi (§8.6): (a) CRM ikizi A için 5 sn bekler → satır `Unresolved`, `getCaseCustomers` 2,5 sn içinde döner (okuma başına 2 sn); (b) 9 farklı hesap, her biri 1,5 sn → ikizde aynı anda en çok 8 istek; (c) 17 farklı hesaplı vaka (tek müşteri sayfası), CRM ikizi hiç yanıt vermez, MVP-6 ikizi anında yanıtlar → 17 satır `Unresolved`, `getCaseCustomers` 3,5 sn içinde döner (sayfa tavanı 3 sn); (d) üç hesap: A 0,1 sn'de yanıtlar, C ve D hiç yanıt vermez → A `Resolved`, C ve D `Unresolved`, yanıt 2,5 sn içinde (kısmi sonuç); (e) tek hesaplı vaka, MVP-6 ikizi 1,5 sn, CRM ikizi hiç yanıt vermez → `getCaseCustomers` 2,5 sn içinde döner (iki okuma eşzamanlı). Süreler testte `Stopwatch` ile ölçülür. Test `RecallCustomerDirectoryTests.Budget_*`. Sabotaj (her biri ayrı): S-12a `PerReadTimeout` 10 sn ve `PageBudget` 10 sn → (a) kırmızı; S-12b `MaxParallel` kapısını kaldır → (b) kırmızı; S-12c `PageBudget`'ı kaldır (yalnız okuma başına sınır) → (c) yaklaşık 6 sn, kırmızı; S-12d tavan dolunca tüm sözlüğü `Unresolved` döndür → (d) kırmızı; S-12e CRM çözümünü MVP-6 okumasından sonra başlat → (e) yaklaşık 3,5 sn, kırmızı.
- [ ] AC-13 (zorunlu) — Önbellek yok, istek içi tekilleştirme: A'lı üç sevkiyat satırı → bir `getCaseCustomers` isteğinde CRM'e 1 çağrı; art arda iki istek → toplam 2 çağrı. Test `RecallCustomerDirectoryTests.No_cache_dedup_per_request`. Sabotaj S-13a: `Distinct`'i kaldır → 3 çağrı, kırmızı; S-13b: statik sözlükte sonuç sakla → ikinci istek 0 çağrı, kırmızı.
- [ ] AC-14 (zorunlu) — Günlükte kişisel veri yok: AC-7 / AC-8 / AC-12 / AC-18 / AC-25 / AC-28 / AC-29 koşularının günlük yakalamasında ad ve kod hiçbir satırda geçmez; başarısızlıkta `recall.customer.lookup_unavailable` satırı `AccountId` ve neden (401 / 403 dahil durum kodu, sayfa tavanı dolunca `Budget`, devre kesiciyle atlanan hesapta `Breaker` — AC-29 (d) koşusunda 392 satır `Breaker`) taşır; geçersiz `customerRef` ve `"D"` biçimli `shipToReference` değeri günlüğe yazılmaz (§8.2). Test `RecallCrmLoggingTests.*`. Sabotaj S-14: geçersiz sözleşme yanıtında gövdeyi günlüğe yaz → kırmızı.
- [ ] AC-15 (zorunlu) — Mühürler: FU01 öncesi kapsam fixture'ının rs-scope-v1 özeti sabit vektöre bayt eşit; aynı kapsam CRM adı değişse de aynı özeti verir; `CustomerRefSource` kanonik metinde yok; rt-v1 özeti değişmez. Test `RecallCrmSealTests.*`. Sabotaj S-15: `RecallScopeRules.Canonical`'a `customerRefSource` yaz → vektör kırmızı.
- [ ] AC-16 (zorunlu) — RECALL 1.10.0: `recall.openapi.yaml` `version: 1.10.0`; değişiklik notunun ilk satırı `1.10.0 — additive + one input restriction (MOD-0177-FU01` ile başlar ve Q-6 `addScopeItems` daraltmasını, iki yeni kodu (`RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE` 503, `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN` 403), `customerPage` parametresini ve `DrillRun.customers` adlandırmasını anar; `CustomerDisplay` şeması (yedi `state` değeri) ve beş isteğe bağlı `customer` alanı; `components/parameters/CustomerPage` ve altı okumada başvurusu; beş okumada `INVALID_REQUEST` ve `'400'`; `components/responses/ServiceUnavailable` ve üç komutta `'503'`; iki kod üst düzey kayıtta ve `recordNotification` / `declareConsumption` / `linkReturn` listelerinde; `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN` ayrıca `exportTrace`, `previewEvidence`, `createEvidenceExport` listelerinde (`…_UNAVAILABLE` bu üçünde yok) ve üç ucun açıklamasında ret cümlesi ile devre kesici cümlesi; dondurulmuş 1.9.0 fixture'ı (`recall-v1.9.0.openapi.yaml`, LF sha256 `7d97b13d…930c`) ile canlı dosya arasındaki fark tam olarak bu listedir ve tek girdi daraltması `addScopeItems` açıklamasıdır; AC-1…AC-13, AC-18, AC-20, AC-25, AC-26, AC-28 ve AC-29 yanıtları `RecallContractValidatingHandler` (`RecallTestInfrastructure.cs:132`, şemalar `ContractSchemas.ValidateRecall` `:152`) ile canlı sözleşmeye karşı doğrulanır ve handler'ın `Validated` sayacında `GET /api/recall/cases/{id}/customers` ile `…/effectiveness` sıfırdan büyüktür (Repo Scope: iki uç listeye eklenir); `RecallContract120Tests` … `RecallContract180Tests` ve `RecallESignTests` değiştirilmeden yeşil, `RecallContract190Tests` yalnız "after" dosyası fixture'a çevrilerek (`:40`) ve `RecallRulesAndShapeTests` yalnız sürüm satırı ve 1.10.0 belirteçleriyle değişip yeşil. Test `RecallContract1100Tests.*`. Sabotaj (her biri ayrı): S-16a sunucu `customer.state` alanını yazmasın → şema doğrulaması kırmızı; S-16b değişiklik notunu "additive only" diye başlat → kırmızı; S-16c `RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE`'ı `declareConsumption` listesinden çıkar → kırmızı; S-16d `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN`'ı üst düzey kayıttan çıkar → `RecallRulesAndShapeTests.cs:285-290` ve 1.10.0 fark testi kırmızı; S-16e sunucu `customer.state = "Pending"` yazsın (şemada olmayan değer) → `customers` yanıtının şema doğrulaması kırmızı; S-16f `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN`'ı `createEvidenceExport` listesinden çıkar → 1.10.0 fark testi kırmızı.
- [ ] AC-17 (zorunlu) — **Kabul kapısı**, SHIPMENT 3.5.0 hizası: `shipment-bundle.openapi.yaml` `getShipment` 200 şemasında `ShipmentBundleHttpReader.CustomerRefField` adlı, `type: string`, `format: uuid` özellik var ve `info.version` ≥ 3.5.0. Test `ShipmentBundle350AlignmentTests.CustomerRef_field_matches`; D-1 kapanana dek `[Fact(Skip = "MOD-0177-FU01 D-1: SHIPMENT-BUNDLE 3.5.0 not frozen")]` ile adlı gerekçeyle atlanır (CT tam paket koşusunda kırmızı değil, atlanmış görünür); DL-1 kabul commit'i Skip'i kaldırır ve test yeşil olmadan DL-1 kabul edilmez; kabul kaydı Skip'in kaldırıldığı commit'i yazar. Sabotaj S-17 (Skip kaldırıldıktan sonra): sabitin değerini değiştir → kırmızı.
- [ ] AC-18 (zorunlu) — Yetki ve izin eksik (CT kararı F-2@fbafd3af, F-2@807c8dc2; §8.4, §8.6, §8.10): her biri ayrı fixture, CRM ikizi C için 403, D için 401 döner (anahtar atanmamış kullanıcı) → recall ekran okuma uçları 200; C ve D satırı `customer = {source: Crm, state: Forbidden, shipTo, shipmentNumber}`, `name` / `code` yok; aynı kullanıcının C ile `recordNotification`, `declareConsumption`, `linkReturn` → her biri 403 `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN`, yanıt gövdesinde C yok, üç koleksiyon, vaka olayı ve denetim satırı sayıları istek öncesine eşit; D ile `recordNotification` → aynı 403; eski anahtarlı satıra → 201 ve CRM'e 0 çağrı; aynı kullanıcının `previewEvidence`, `createEvidenceExport` ve C düğümünü içeren iz için `exportTrace` → her biri 403 `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN`, dosya ve denetim satırı yok (ayrıntı ve sabotaj AC-26 (h), (i), AC-29 (i)); recall okuma izni olmayan kullanıcı → 403 (bugünkü; kod `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN` değil); `AuthorizationGuardTests` değiştirilmeden yeşil (yeni eylem yok, her eylem `[HasPermission]`). Test `RecallCrmAuthorizationTests.*`. Sabotaj (her biri ayrı): S-18a CRM 403'ünü okuma ucuna taşı (okuma 403) → kırmızı; S-18b 401 / 403'ü `Unresolved`'a eşle (r3 kuralı) → `state = Unresolved` ve kayıt 503, kırmızı; S-18c `Forbidden` reddinde 503 `RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE` dön → kırmızı; S-18d yalnız 403'ü `Forbidden` say → D `Unresolved`, kırmızı.
- [ ] AC-19 (zorunlu) — 7 dil: §11 tablosundaki her anahtar kendi ailesinde (`Recall.{en,tr,fr,es,zh,ar,ru}.resx`; ortaklar ayrıca `RecallEvidenceText_{7}.resx`) dolu, `zh` / `ar` / `ru` İngilizce kopya değil; `Recall.cs` anahtar listesinde; her dilde `Recall.Errors.RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE` değeri `Recall.Errors.SERVICE_UNAVAILABLE` değerinden, `Recall.Errors.RECALL_CUSTOMER_DIRECTORY_FORBIDDEN` değeri `Recall.Errors.FORBIDDEN` değerinden farklı (§11); `EvidenceTextParityTests` ve Recall resx paritesi yeşil. Sabotaj S-19a: `Recall.ar.resx`'ten `Recall.Customer.LegacyBadge`'ı sil → kırmızı; S-19b: `Recall.fr.resx`'te `Recall.Errors.RECALL_CUSTOMER_DIRECTORY_FORBIDDEN` değerini `Recall.Errors.FORBIDDEN` değeriyle aynı yap → kırmızı; S-19c: `Recall.zh.resx`'te `Recall.Customer.ExportForbidden` değerini İngilizce kopya yap → kırmızı.
- [ ] AC-20 (zorunlu) — Elle kapsam satırında CRM müşterisi seçilemez (§8.2; Q-6 = HAYIR). `Draft` vakada `addScopeItems`, her biri ayrı fixture: (a) Shipment `customerRef = "CUST-300/ADDR-1"` → 200; satır `CustomerRefSource = Manual`; `getCaseScope` ve `getCaseCustomers` satırında `customer = {source: Manual, state: NoCrm, label: "CUST-300/ADDR-1"}`, CRM ikizine 0 çağrı; (b) `customerRef = A` (küçük harf) → 400 `INVALID_REQUEST`; (c) A büyük harfle → 400; (d) `"  " + A + " "` (kırpılınca 36 karakterlik GUID) → 400; (e) iki maddelik toplu istek, ilki geçerli (`CUST-301/ADDR-1`), ikincisi A → 400 ve vaka için `recall_scope_items`, vaka olayı ve denetim satırı sayıları istek öncesine eşit (ilk madde de yazılmaz); (e2) `"{" + A + "}"` (B biçimi) ve tiresiz A (N biçimi) → 200, `Manual`, `NoCrm` (yasak yalnız `"D"` biçimine bağlı); (f) **veri sayımı** (salt okuma operasyon sorgusu, ürün kodu değil): `Kind = Shipment`, `CustomerRefSource` `Crm` olmayan (`null` dahil), her revizyon ve silinmiş satırlar dahil kapsam satırlarında `IsDShaped(customerRef)` olanların kiracı başına sayısı (değer yazılmaz) — beklenen 0; (1) DL-1 kabul ortamında kabul koşusunda, (2) her hedef ortamda FU01 kod geçişinin hemen öncesinde ve (3) hemen sonrasında koşulur, (2) ve (3) sıfır ve eşit olmalı; sayımı MVP-1 CT koşar ve kabul / geçiş kaydına yazar, geçişi yapan kişi geçiş kaydında iki sonucu imzalar; sıfır değilse kabul ya da geçiş durur, satırlar CT'ye gider (O-2). Test `RecallCrmManualScopeTests.*`. Sabotaj (her biri ayrı): S-20a GUID kontrolünü kaldır → (b), (c) kırmızı; S-20b `IsDShaped`'in 36 karakter denetimini kırpmadan önceki ham girdiye uygula → (d) 200, kırmızı; S-20c kontrolü yalnız küçük harfe bağla (`Guid.TryParseExact` yerine kanonik metin karşılaştırması) → (c) kırmızı; S-20d reddetmek yerine GUID'li maddeyi sessizce atla (öteki maddeler yazılır, 200) → (e) kırmızı; S-20e `Guid.TryParse` (her biçim) kullan → (e2) kırmızı; S-20f satırı `CustomerRefSource` yazmadan kur → (a) kırmızı (`source = Unknown`). (f) üretim kodu değildir, sabotajı yoktur; kanıtı sorgu + sonuçtur.
- [ ] AC-25 (zorunlu) — Çözülemeyen müşteriye yazım yok ve çözüm denetiminin yeri, sunucu (sahip kararı F-1; F-6@fbafd3af; §8.4): `Active` vaka; A (`Resolved`), C ve D (CRM ikizi 503), N (404) `Crm` satırları; C'nin sevkiyatı `shipToReference = "CUST-200/ADDR-1"`, D'ninki `"CUST-201/ADDR-1"`. (a) `getCaseCustomers`: C satırı `customer = {source: Crm, state: Unresolved, shipTo: "CUST-200/ADDR-1", shipmentNumber: <C'nin hareket numarası>}`, D satırı kendi değerleriyle; iki satırın ayırıcısı farklı, hiçbirinde GUID yok; N satırı `state: NotFound` ve ayırıcıyla; (b) C ile `recordNotification`, `declareConsumption`, `linkReturn` → her biri 503 `RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE`, yanıt 2,5 sn içinde, yanıt gövdesinde C yok; üç koleksiyon, vaka olayı ve denetim satırı sayıları istek öncesine eşit; (c) A ile üçü → 201; N ile üçü → 201 (Q-7 = açık, Beste, 2026-11-04); (d) eski anahtarlı satırla üçü → 201 ve CRM ikizine 0 çağrı; (e) CRM açıkken C ile yazılmış bildirimin aynı anahtar ve aynı gövdeyle tekrarı CRM kapalıyken → 200, aynı `notificationId`; (f) CRM ikizi C için 503 iken geçersiz istekler, her biri ayrı: `recordNotification` C ve vakada olmayan `supersedesId` → 400 `INVALID_REQUEST`; `declareConsumption` C ve sevk edilenden fazla miktar → 422 `RECALL_QUANTITY_EXCEEDS_SHIPPED`; `linkReturn` C ve `CUSTOMER_RETURN` olmayan hareket → 422 `RECALL_RETURN_NOT_ELIGIBLE`; üçünde CRM ikizine 0 çağrı ve yazım 0; (g) aynı üç geçersiz istek C için CRM 403 iken → aynı 400 / 422 / 422, CRM'e 0 çağrı. Test `RecallCrmUnresolvedWriteTests.*`. Sabotaj (her biri ayrı): S-25a çözüm denetimini kaldır → (b) 201, kırmızı; S-25b `NotFound`'u da reddet → (c) kırmızı; S-25c CRM'i her komutta çağır (eski anahtar dahil) → (d) çağrı 1, kırmızı; S-25d çözüm denetimini tekrar denetiminden önceye al → (e) 503, kırmızı; S-25e 503 yerine 409 `RECALL_CONFLICT` dön → (b) kırmızı; S-25f çözüm denetimini kapsam eşleşmesinin hemen arkasına al (r3 sırası) → (f) 400 / 422 yerine 503 ve CRM'e 1 çağrı, kırmızı.
- [ ] AC-26 (zorunlu) — Kanıt paketi anlık görüntüsü ve izin reddi (CT kararı; F-2@807c8dc2; §8.10): karışık vaka (A `Resolved`, C `Unresolved`, `null` kaynaklı `CUST-100/ADDR-2`, `Manual` `CUST-300/ADDR-1`): (a) `createEvidenceExport` CSV ve PDF → 201 (Unresolved varken reddedilmez); müşteri bölümünde A hücresi "Example Hastanesi · ACC-2026-000042", C hücresi `Recall.Customer.UnresolvedWith` metni C'nin hareket numarasıyla, eski satır "CUST-100/ADDR-2 (Eski kayıt)", elle satır "CUST-300/ADDR-1 (CRM kimliği yok)" (dil `tr`); özet bölümünde `customerNames` satırı; A ve C GUID'i dosyada yok; (b) saklı dosyanın sha256'sı dönen baytların hash'ine eşit (mevcut kural); (c) CRM ikizinde A'nın adı değiştirilir → aynı dışa aktarımın `getEvidenceFile`'ı aynı baytları ve hash'i döner, yeni dışa aktarım yeni adı taşır; (d) yalnız CRM kimliksiz satırları olan vakada müşteri ve özet bölümü FU01 öncesi koddan sabitlenmiş vektöre bayt eşit ve `RecallEvidenceTests` değiştirilmeden yeşil; (e) dışa aktarım günlüğü satırı ve `recall.evidence.exported` denetim satırı adı ve kodu taşımaz; (f) **mühürsüz vaka** (F-4@fbafd3af): anlık görüntüsü olmayan `Draft` vaka (iz bölümü `notSealed`), C `Unresolved`, C'nin sevkiyat satırının arkasında (aynı lot, `DocumentId` = satırın `SourceDocumentId`'si) `GIS-0042` numaralı satış hareketi → müşteri bölümünde C hücresi "Çözülemedi (GIS-0042)" (`tr`); ledger ikizinde müşteri bölümü için bir `SalesIssuesOfLotsAsync` çağrısı; (g) bütün `Crm` müşterileri `Resolved` olan vakada müşteri bölümü için `SalesIssuesOfLotsAsync` çağrılmaz; (h) **izin eksik — ret** (CT kararı F-2@807c8dc2; §8.10): A `Resolved`, C için CRM ikizi 403 (`Forbidden`) olan `Active` vaka → `createEvidenceExport` CSV ve PDF, her biri ayrı → 403 `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN`, yanıt gövdesinde A ve C yok; `recall_evidence_exports`, dosya koleksiyonu ve `recall.evidence.exported` denetim satırı sayıları istek öncesine eşit; C 403 iken vakadaki D için CRM ikizi 503 (`Unresolved`) dönse de yine 403; aynı kullanıcı aynı idempotency anahtarıyla, C'nin izni varken saklanmış bir dışa aktarımı tekrarlarsa → 200, aynı `exportId` ve sha256, CRM'e 0 çağrı; aynı kullanıcının tatbikat (`runId`) ve dönem (`from` / `to`) kanıtı → 201, CRM'e 0 çağrı; geçersiz istek (biçim `Xml`) → bugünkü 400, CRM'e 0 çağrı; (i) aynı vakada `previewEvidence` → 403 `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN`, gövdede A ve C yok. Test `RecallCrmEvidenceTests.*`. Sabotaj (her biri ayrı): S-26a `Unresolved` varken dışa aktarımı reddet → (a) kırmızı; S-26b `Unresolved` hücresini boş yaz → (a) kırmızı; S-26c işareti yalnız CRM kimliksiz vakaya da uygula → (d) kırmızı; S-26d `getEvidenceFile`'da dosyayı yeniden üret → (c) kırmızı; S-26e ayırıcıyı iz bölümünün `TransactionsOfLotsAsync` okumasından al (r3 metni) → (f) "Çözülemedi" numarasız, kırmızı; S-26f satır ↔ hareket eşleşmesinde `DocumentId ?? "TXN-" + TransactionId` yerine `TransactionId`'yi karşılaştır → (f) numarasız, kırmızı; S-26g kurucuda `Forbidden`'ı r4 kuralıyla işaretli hücre yaz (`ForbiddenWith`, ret yok) → (h) 201 ve dosya yazılır, kırmızı; S-26h reddi `createEvidenceExport`'ta saklama işleminden sonra yap → (h) sayılar artar, kırmızı; S-26i reddi yalnız `createEvidenceExport`'a koy (kurucu değil) → (i) önizleme 200, kırmızı; S-26j reddi tekrar denetiminden önceye al → (h) tekrar 403, kırmızı.
- [ ] AC-28 (zorunlu) — Sayfa bütçesi, sağlıklı ama gecikmeli CRM (CT kararı F-1@fbafd3af; §8.6): V200 fixture'ı, CRM ikizi her hesapta 200 ms: (a) `getCaseCustomers` (parametresiz) → 200 satır; anahtar sırasıyla ilk 25 `Crm` satırı `Resolved` (ad + kod), kalan 175 `Deferred` (`name`, `code`, `shipTo`, `shipmentNumber` yok), hiç `Unresolved` yok; CRM ikizine bu istekte tam 25 çağrı; yanıt 3 sn içinde; (b) `customerPage = 2 … 8` sırayla → her yanıtta yalnız kendi 25'lik dilimi `Resolved`, sekiz yanıtın birleşiminde 200 hesabın her biri tam bir kez `Resolved`; (c) `customerPage = 9` → 200, CRM'e 0 çağrı, 200 satır `Deferred`; `customerPage = 0` ve `customerPage = x` → 400 `INVALID_REQUEST`; (d) aynı pencere kuralı öteki beş okumada, her biri ayrı fixture: `runTrace` (V200 kök lotu), `getCaseScope`, `getCaseEffectiveness`, `getCaseReturns` (V200'ün 30 müşterisine birer iade bağı), `getCaseScopeDiff` (canlı izde V200 dışı 30 `Crm` hesaplı yeni bulgu) — `customerPage = 2` ile her birinde yalnız o yanıtın anahtar kümesinin (ordinal) 26.–50. `Crm` müşterileri `Resolved`, öteki `Crm` satırları `Deferred`, CRM çağrısı ≤ 25; (e) CRM ikizi her hesapta 500 ms (sağlıklı sınır, ASSUMPTION-FU01-5) → `getCaseCustomers` 1. sayfanın 25 hesabının hepsi `Resolved`, yanıt 3 sn içinde; (f) iki farklı `customerPage` ile alınan `getCaseCustomers` yanıtları `customer` alanları dışında bayt eşit (satır sayısı, sırası ve öteki alanlar sayfadan bağımsız). Test `RecallCustomerPageTests.*`. Sabotaj (her biri ayrı): S-28a pencereyi kaldır ve r3 bütçesini geri koy (bütün hesaplar tek çağrıda, toplam 2 sn) → (a) yaklaşık 80 `Resolved`, kalanı `Unresolved`, 200 çağrı, kırmızı; S-28b sayfa boyunu 50 yap → (a) 50 çağrı, kırmızı; S-28c dilimi ordinal anahtar sırası yerine yanıtın satır sırasıyla kes → (d) `runTrace` dilimi farklı, kırmızı; S-28d `PageBudget`'ı 1 sn yap → (e) bir kısmı `Unresolved`, kırmızı.
- [ ] AC-29 (zorunlu) — Dışa aktarım bütçesi (toplam tavansız), devre kesici ve iz reddi (CT kararı F-1@fbafd3af, F-1@807c8dc2, F-2@807c8dc2; §8.6 `Export`, §8.10): (a) V200, CRM ikizi 200 ms → `createEvidenceExport` (CSV) 201, müşteri bölümündeki 200 hücrenin hepsi "{ad} · {kod}", hiç `Recall.Customer.UnresolvedWith` yok, CRM ikizine 200 çağrı, aynı anda en çok 8; `previewEvidence` aynı vakada aynı 200 metni; `exportTrace` (V200 kök lotu) 200 Shipment hücresinin hepsi ad + kod; (b) 17 hesaplı vaka, CRM ikizi hiç yanıt vermez → `createEvidenceExport` 201, 17 hücre `Recall.Customer.UnresolvedWith` (ayırıcılı), CRM'e 8 çağrı, süre ≤ 3 sn; (c) 9 hesap: 8'i 0,1 sn'de yanıtlar, biri hiç yanıt vermez → 8 hücre ad + kod, 1 hücre `UnresolvedWith`, dışa aktarım 201; (d) **devre kesici, yanıtsız CRM** (Q-9 ölçümü: Gateway 90 sn): V400, CRM ikizi hiç yanıt vermez → her biri ayrı: `createEvidenceExport` (CSV) 201 ve 400 hücre `UnresolvedWith`; `previewEvidence` 200 ve aynı 400 metin; `exportTrace` (V400 kök lotu) 200 ve 400 Shipment hücresi `UnresolvedWith` — her birinde CRM ikizine tam 8 çağrı (ordinal sırayla ilk 8 hesap), günlükte 392 `Breaker` satırı, süre ≤ 4 sn (testin kendi zaman sınırı 10 sn); (e) V400, CRM ikizi 200 ms (sağlıklı, gecikmeli) → `createEvidenceExport` 201, 400 hücrenin hepsi ad + kod, 400 çağrı, hiç `Breaker` satırı yok, süre ≤ 15 sn (≈ 50 dalga × 0,2 sn); (f) V400, ordinal sırayla ilk 16 hesap (iki dalga) 0,1 sn'de yanıtlar, sonrası hiç yanıt vermez → 16 hücre ad + kod, 384 `UnresolvedWith`, CRM'e 24 çağrı, süre ≤ 4 sn; (g) 40 hesaplı vaka, her dalganın ordinal sırayla son hesabı hiç yanıt vermez, öteki yedisi 0,1 sn → 35 hücre ad + kod, 5 `UnresolvedWith`, 40 çağrı (kesici açılmaz), süre ≤ 12 sn; (h) 16 hesaplı vaka, ilk dalganın 8 hesabı 404, ikinci dalganın 8 hesabı `Resolved` → 8 `Recall.Customer.NotFoundWith`, 8 ad + kod, 16 çağrı; (i) **iz reddi**: izde A (`Resolved`) ve C (CRM ikizi 403) olan kök lot → `exportTrace` 403 `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN`, gövdede A ve C yok, `recall.trace.run` denetim satırı sayısı istek öncesine eşit; aynı kullanıcının hiç `Crm` düğümü olmayan izi → 200 ve AC-10 (f) bayt eşitliği. Test `RecallCustomerExportBudgetTests.*`. Sabotaj (her biri ayrı): S-29a dışa aktarıma müşteri sayfası penceresini uygula (yalnız ilk 25 hesap) → (a) kırmızı; S-29b dışa aktarıma 3 sn toplam tavan koy → (a) yaklaşık 5 sn gerekir, bir kısmı `Unresolved`, kırmızı; S-29c önizlemeyi `Page` kipine bağla → (a) önizleme metni dışa aktarımdan farklı, kırmızı; S-29d `exportTrace`'i `Page` kipine bağla → (a) iz CSV'sinde 175 hücre çözülmemiş, kırmızı; S-29e **devre kesiciyi kaldır** → (d) süre tavan(400/8) × 2 sn = 100 sn > Gateway 90 sn, test 10 sn sınırında kesilir, kırmızı; (b) ≈ 6 sn, kırmızı; S-29f kesiciyi dalgadaki tek zaman aşımında aç → (g) ilk dalgadan sonra kalanlar atlanır (8 çağrı, 7 ad + kod), kırmızı; S-29g kesiciyi yalnız ilk dalgaya uygula (§8.6 yazar kararı) → (f) ≈ 48 × 2 sn = 96 sn, test sınırında kesilir, kırmızı; S-29h 404'ü kesici sınıfına kat → (h) ikinci dalga çağrılmaz, kırmızı; S-29i `exportTrace`'te `Forbidden` reddini kaldır (r4 kuralı, işaretli hücre) → (i) 200, kırmızı; S-29j `exportTrace` reddini denetim satırından sonra yap → (i) denetim sayısı artar, kırmızı.
- [ ] AC-30 (zorunlu) — Canlı geçiş öncesi rol kontrolü (CT kararı F-2@fbafd3af, F-2@807c8dc2; §14, §7 D-4): salt-okuma operasyon kontrolü, ürün kodu değil — kiracıda `recall.notifications.create`, `recall.consumptions.create`, `recall.returns.link`, `recall.evidence.export` ya da `recall.trace.export` taşıyan (ve Q-4 yanıtının adlandırdığı) her rolde `crm.account.lookup-validation` var mı; sonuç kayıt rolleri ve dışa aktarım rolleri için ayrı sayılır (ikisi de beklenen 0); sonuç kiracı başına eksik atamalı rol sayısı (rol adları kayda, kişi adı yok) — beklenen 0; (1) DL-1 kabul ortamında kabul koşusunda (D-4 kapısıyla) ve (2) her hedef ortamda FU01 kod geçişinin hemen öncesinde koşulur; sorgu ve sonuç kabul / geçiş kaydına yazılır (AC-20 (f) ile aynı kayıt), sorgunun biçimi Auth sahibinin (Ali) Q-4 yanıtıyla verdiği biçimdir; kontrolü MVP-1 CT koşar, geçişi yapan kişi geçiş kaydında sonucu imzalar; bir kiracıda sonuç 0 değilse o ortamın geçişi durur ve eksik atama Auth sahibine gider. Üretim kodu değildir, sabotajı yoktur; kanıtı sorgu + sonuçtur.

**DL-2**

- [ ] AC-21 (zorunlu) — Müşteriler sekmesi: `Resolved` satır "{ad} · {kod}", altında anahtar satırı yok; `Unresolved` "Çözülemedi" ve ayırıcı, `Forbidden` "İzin eksik" ve ayırıcı, `NotFound` "Bulunamadı" ve ayırıcı; sayfanın metin içeriğinde ve `title` özniteliklerinde A geçmez (`data-notify` / `data-consume` değerinde geçebilir). Test `recall-fu01.test.mjs` "customers tab never prints the account id". Sabotaj S-21: `case-detail.js:248`'deki tek aralıklı `customerRef` alt satırını CRM satırında bırak → kırmızı.
- [ ] AC-22 (zorunlu) — Diğer görünümler: kapsam sekmesi, kapsam farkı listesi (`getCaseScopeDiff` `added`), iade bağları, etkinlik tablosu, üç pencere ve seçim listesinin `option` metni, iz ekranı — A metin olarak geçmez, ad + kod ya da durum metni görünür; tatbikat ekranı (`drill-view.js`) `customers` alanını hiçbir yerde çizmez. Test `recall-fu01.test.mjs` "no view prints the account id". Sabotaj (her biri ayrı): S-22a `trace-view.js:32` `n.customerRef`'e geri dönsün → kırmızı; S-22b etkinlik tablosu (`:287`) `r.customerRef` → kırmızı; S-22c kapsam farkı listesi (`case-detail.js:165`) `n.customerRef` → kırmızı; S-22d `drill-view.js` koşu kartına `run.customers` listesini bas → kırmızı.
- [ ] AC-23 (zorunlu) — Rozetler (§8.8): AC-6 (a) verisiyle ekran iki satır çizer; eski satırda "Eski kayıt" rozeti ve `Recall.Customer.LegacyHint` ipucu, CRM satırında rozet yok; satırlar ad eşleşse de birleşmez; FU01 sonrası `Document` satırında (AC-6 (e) r+1 verisi) "CRM kimliği yok" rozeti ve `Recall.Customer.NoCrmHint`; AC-20 (a)'nın `Manual` satırında "CRM kimliği yok" rozeti ve `Recall.Customer.ManualHint`. Test `recall-fu01.test.mjs` "legacy and no-crm rows stay separate with badge". Sabotaj (her biri ayrı): S-23a satırları `customer.name`'e göre birleştir → kırmızı; S-23b rozeti yalnız `state = Legacy`'ye bağla → `NoCrm` satırları rozetsiz, kırmızı; S-23c `Manual` satırına `LegacyHint` bas → kırmızı; S-23d FU01 sonrası `Document` satırına `LegacyHint` bas → kırmızı.
- [ ] AC-24 (zorunlu) — Sabit metin yok: dil `ar` iken beş durum metni (`Unresolved`, `Forbidden`, `NotFound`, `Deferred` ve ayırıcılı biçimler), iki rozet, `Recall.Customer.CrmUnavailable`, `Recall.Customer.CrmForbidden`, `Recall.Customer.ExportForbidden` ve sayfa seçicisi metinleri (`NamesPage`, `PagePrev`, `PageNext`) `Recall.ar.resx` değerleriyle çizilir. Test `recall-fu01.test.mjs` "texts come from l10n". Sabotaj (her biri ayrı): S-24a `case-detail.js`'e "Çözülemedi" sabit yaz → kırmızı; S-24b `trace-view.js`'e "Ad bu sayfada değil" sabit yaz → kırmızı.
- [ ] AC-27 (zorunlu) — Çözülemeyen ya da izni eksik müşteriye ekranda komut yok (sahip kararı F-1; F-2@fbafd3af, F-6@fbafd3af; §8.8): AC-25 verisiyle müşteriler sekmesinde C ve D satırında bildirim ve tüketim düğmesi çizilmez, yerine `Recall.Customer.CrmUnavailable` metni; A ve N satırında düğmeler var; iade bağlama penceresinin seçim listesinde C ve D seçenek değil, pencerede `Recall.Customer.CrmUnavailable` görünür; yalnız C'nin olduğu vakada pencere alanları ve bağlama düğmesi çizilmez; AC-18 verisiyle (C `Forbidden`) C satırında düğme yok, yerine `Recall.Customer.CrmForbidden` metni (`CrmUnavailable` değil), seçim listesinde C yok; sunucunun 503 yanıtı (`{error: {code: "RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE"}}`) ve 403 yanıtı (`{error: {code: "RECALL_CUSTOMER_DIRECTORY_FORBIDDEN"}}`) — sahte gövdeler sözleşmenin `Error` biçiminde, `RecallContractFakes.Bound` 2xx dışını denetlemez (§6) — pencerenin hata kutusunda gösterilir ve test gösterilen metnin ilgili `Recall.Errors.*` anahtarının değerine **eşit** ve `Recall.Errors.SERVICE_UNAVAILABLE` / `Recall.Errors.FORBIDDEN` değerinden **farklı** olduğunu sınar; **dışa aktarım ekranda (F-2@807c8dc2, §8.8):** kanıt ekranında önizleme ve dışa aktarımın 403 yanıtı (`{error: {code: "RECALL_CUSTOMER_DIRECTORY_FORBIDDEN"}}`) `rc-error` kutusunda `Recall.Errors.RECALL_CUSTOMER_DIRECTORY_FORBIDDEN` değeriyle görünür, indirme başlamaz; iz ekranında çizilen düğümlerden biri `customer.state = Forbidden` iken `rc-trace-csv` bağlantısı yoktur ve yerinde `Recall.Customer.ExportForbidden` metni vardır, `Forbidden` düğümü olmayan, bir `Unresolved` düğümü olan izde bağlantı bugünkü gibidir. Test `recall-fu01.test.mjs` "unresolved or forbidden customer offers no command". Sabotaj (her biri ayrı): S-27a `Unresolved` satırda düğmeleri çiz → kırmızı; S-27b `Unresolved` müşteriyi seçim listesinde bırak → kırmızı; S-27c `Recall.Errors.RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE`'ı `Recall.cs` listesinden çıkar → `recall-ui.js:51` eşlemesi düşer, ekran 5xx için `Recall.Errors.SERVICE_UNAVAILABLE` metnini gösterir (`:52`), kırmızı; S-27d `Recall.Errors.RECALL_CUSTOMER_DIRECTORY_FORBIDDEN`'ı `Recall.cs` listesinden çıkar → ekran `Recall.Errors.FORBIDDEN` metnini gösterir (`:53`), kırmızı; S-27e `Forbidden` satıra `CrmUnavailable` bas → kırmızı; S-27f `trace.js` `Forbidden` düğüm varken de CSV bağlantısını çizsin → kırmızı; S-27g `trace.js` bağlantıyı `Unresolved` düğümde de kaldırsın → `Forbidden`'sız izde bağlantı yok, kırmızı.
- [ ] AC-31 (zorunlu) — Müşteri sayfası ekranda (CT kararı F-1@fbafd3af; §8.8): V200 verisiyle (sahte ağ geçidi, 1.10.0 şemalı 2xx gövdeleri): (a) müşteriler sekmesi anahtar sırasıyla ilk 25 müşteri satırını çizer, özet bütün 200'ü sayar, altında "Müşteriler 1–25 / 200" ve sonraki düğmesi; sonraki → Web'e giden istek `part=customers&customerPage=2`, sekme 26.–50. müşterileri çizer; sekmede hiçbir satırda `Recall.Customer.Deferred` görünmez; (b) iade bağlama penceresinin seçim listesi sayfanın 25 müşterisini listeler, pencerede önceki / sonraki düğmeleri; (c) iz ekranı ve etkinlik tablosu 200 satırı çizer, üstte sayfa seçicisi; 1. sayfada 25 `Crm` satırı ad + kodla, 175'i `Recall.Customer.Deferred` metniyle; hiçbir yerde A metni yok; (d) 25 ya da daha az müşterili vakada sayfa seçicisi çizilmez; (e) Web vekili `RecallController.CaseData` `customerPage`'i `customers`, `scope`, `diff`, `returns`, `effectiveness` parçalarında servise iletir, `TraceData` iz okumasında iletir, `ExportTrace` dosya isteği `Accept-Language`'ı iletir. Test `recall-fu01.test.mjs` "customer pages" ve `RecallCustomerPageWebTests.*`. Sabotaj (her biri ayrı): S-31a müşteriler sekmesi bütün 200 satırı çizsin → `Deferred` satırları görünür, kırmızı; S-31b sayfa değişiminde `customerPage`'i göndermesin → 2. sayfa `Deferred`, kırmızı; S-31c Web vekili `customerPage`'i iletmesin → (e) kırmızı; S-31d `SendFileAsync` `Accept-Language`'ı iletmesin → (e) kırmızı.

## 17. Test Expectations

- Build: InventoryService ve Web çözümü uyarı politikası değişmeden derlenir.
- **Tam paket:** servis dokunulduğu için kabul koşusu filtresiz Inventory test paketi + `Diten.Web.Tests` (CT kuralı); temel kırmızılar aynı
  gün `main` ile karşılaştırılır. Inventory testleri kendi Mongo replica set'ini başlatır (27017 kullanılmaz). AC-17 D-1 kapanana dek adlı
  gerekçeyle **atlanmış** görünür, kırmızı değildir (F-9); atlanan başka FU01 testi yoktur.
- Sözleşme: RECALL 1.10.0 şema doğrulaması ve 1.9.0 fixture farkı (AC-16); SHIPMENT 3.5.0 hizası (AC-17, kapı). Canlı sözleşmeyi okuyan
  öteki testler (§6 tablosu: `RecallContractTests`, `RecallContractYaml`, `RecallContractValidatingHandler`, Web `ContractRoutes` /
  `RecallContractFakes`) tam paket koşusunda yeşil olmalıdır; Web sahtelerinin 2xx gövdeleri 1.10.0 şemalarına bağlıdır (DL-2 önkoşulu, §22).
- Süre ölçütleri (AC-12, AC-25, AC-28, AC-29) `Stopwatch` ile ve CRM ikizinin programlanan gecikmesiyle ölçülür; gerçek CRM / Gateway
  gecikmesi ölçülmez (ASSUMPTION-FU01-5). Gateway'in 90 sn sınırı testte kurulmaz: AC-29 (d) / (f) kendi 10 sn zaman sınırıyla koşar,
  kesicisiz hesap (100 sn, 96 sn) sınırın üstündedir — kanıt Q-9 ölçümüdür (§8.6). AC-29 (e) ve (g) yavaş testlerdir (≈ 10 sn).
- İzolasyon: AC-11; mevcut `RecallTenantBoundaryTests` değiştirilmeden yeşil.
- Denetim: yeni olay yok; mevcut `recall.customer.notified` / `recall.consumption.declared` / iade bağlama olaylarının ayrıntısı CRM
  anahtarında kanonik GUID taşır (AC-4). Q-6 reddinde (`addScopeItems` 400) ve çözüm denetimi reddinde (503 / 403) vaka olayı ve denetim satırı
  yazılmaz (AC-20 (e), AC-25 (b), AC-18); dışa aktarımın izin reddinde (403) dışa aktarım günlüğü satırı, dosya, `recall.evidence.exported`
  ve `recall.trace.run` (`exportTrace`) denetim satırı yazılmaz (AC-26 (h), AC-29 (i)); başarılı `addScopeItems` ve `reviseScope` denetim
  satırları değişmez; `Crm` müşterisine bağlanan iadenin olay metni anahtarsızdır (AC-10 (e)).
- Veri sayımı ve rol kontrolü: AC-20 (f) salt-okuma sayımının sorgusu ve üç sonucu, AC-30 rol kontrolünün sorgusu ve iki sonucu DL-1 kabul
  kaydına ve geçiş kaydına yazılır.
- Smoke (E5, FU01 kapanışı — DL kabulü değil): MOD-0149 DL-1 ve MVP-6 3.5.0 yayındayken bir vakada müşteri adı ve kodu görünür, GUID görünmez.

### 17.1 Sabotaj tablosu

Her AC'nin sabotajı metnindedir (S-1…S-31, alt harfli olanlar ayrı koşu). Sabotajı olmayan iki ölçüt vardır: AC-20 (f) (salt-okuma veri
sayımı) ve AC-30 (salt-okuma rol kontrolü); ikisi de üretim kodu değildir, kanıtları sorgu + sonuçtur. AC-17'nin sabotajı (S-17) Skip
kaldırıldıktan sonra, 3.5.0 FROZEN olunca koşulur. r2'nin S-20b'si ("kontrolü kırpmadan önce yap") kırmızıya dönmediği için (.NET
`Guid.TryParseExact` kırpar) kaldırıldı; yerine kuralın kendi 36 karakter denetimini hedefleyen S-20b ve S-3d geldi (F-5@a66dd353). r4:
r3'ün S-27c beklentisi ("503 ham kodla görünür") koda uymuyordu — anahtar yoksa `recall-ui.js:52` 5xx'i `Recall.Errors.SERVICE_UNAVAILABLE`
metnine düşürür, ham kod görünmez; yalnız "bir hata metni göründü" diyen test sabotajda yeşil kalırdı. Yeni S-27c / S-27d, gösterilen metnin
ilgili anahtarın değerine eşit ve genel metinden farklı olduğunu sınar (F-6@fbafd3af). r3'ün S-8a'sı (403'ü `NotFound` say) 403 artık
`Forbidden` olduğu için S-18b / S-18d'ye taşındı; S-8a 5xx'i hedefler. r3'ün S-18'i S-18a oldu. r5: devre kesici S-29e…S-29h (S-29e CT
ölçütü: kesici kaldırılınca 400 hesapta 100 sn > 90 sn), dışa aktarım reddi her yol için ayrı — `createEvidenceExport` S-26g, S-26h, S-26j,
`previewEvidence` S-26i, `exportTrace` S-29i, S-29j —, sözleşme S-16f, ekran S-27f, S-27g, metin S-19c (F-1@807c8dc2, F-2@807c8dc2).

## 18. Ready-for-dev Checklist

- [x] Golden Reference / Backend File Convention ebeveyn düzeniyle (§10)
- [x] Frontmatter zorunlu alanlar dolu; ekranlar ebeveynin üç sayfası (§9)
- [x] Validation Rules (§12), Failure Path ≥ 4 (§13), Authorization (§14), Gateway kararı (§15)
- [x] AC'lerin hepsi üretim koduna karşı ve adlı sabotajlı (§16, 31 AC, her biri tek satır; istisnalar AC-20 (f) veri sayımı ve AC-30 rol kontrolü, §17.1)
- [x] Audited Events (§21) — yeni yazma komutu yok
- [x] Teslim dilimleri (§22) ve sayfa yardımı kararı (§23)
- [x] Sahip kararları Q-5 = HAYIR, Q-6 = HAYIR, F-1 (çözülemeyen müşteri) (Beste, 2026-11-03) — §8.2, §8.4, §8.5, §8.8, AC-6, AC-20, AC-25, AC-27
- [x] Sahip kararları Q-7 = açık (Bulunamadı satırında işlemler açık), Q-8 = EVET ("eski" yalnız FU01 öncesi, `null` kaynaklı anahtar) (Beste, 2026-11-04) — §8.4, §8.5, §8.8, AC-6, AC-23, AC-25, AC-27; §19.5 madde 11, 12
- [x] CT kararları CT-1 (mühre girmez), CT-2 (RECALL 1.10.0), CT-3 (kanıt anlık görüntüsü), CT-4 (D-3 / D-4 DL-1 kabul kapısı), CT-5 (Q-5 "eski" tanımı) (MVP-1 CT, 2026-11-03) — §19.6
- [x] CT kararları CT-6 (ad çözümü müşteri sayfasına bağlı; dışa aktarım tavansız), CT-7 (CRM 401 / 403 ayrı durum, 403 kodu, geçiş öncesi rol kontrolü), CT-8 (F-3…F-6) (MVP-1 CT, 2026-11-04) — §19.6
- [x] Keşif bulgularının (gateway crm 5061 / recall 5067) mükerrer sayılmaması CT'ce kabul edildi (§19.1)
- [x] Registry satırı (CT, `8ba30d12b`)
- [x] Bağımsız inceleme 01 tur 1 (a66dd353) bulguları kapatıldı (§19.7)
- [x] Bağımsız inceleme 01 tur 2 (fbafd3af) bulguları kapatıldı (§19.8)
- [x] Bağımsız inceleme 01 tur 3 (807c8dc2) bulguları kapatıldı (§19.9); CT kararları CT-9 (devre kesici), CT-10 (dışa aktarım reddi ve dışa aktarım rolleri), CT-11 (müşteri sayfası sırası kabul edilen sınır) (MVP-1 CT, 2026-11-04) — §19.6
- [x] Q-9 kapandı — (a) ölçümle (Gateway 90 sn, Web 100 sn), (b) önizleme `Export` kipinde (§8.6, §19.4)
- [x] `status: ready-for-dev` (r5) — S-49: onay satırı ready-for-dev künyeli sürüme yazılır; `approval_status: pending`
- [ ] CT-APR-02 bağımsız inceleme (r5, yazandan ayrı oturum) + CT-APR-01 onay satırı (yüksek risk) — kod bunlardan sonra
- [ ] Açık, **DL-1 kabul kapısı** (kodlamayı engellemez — §7, §22): D-1 (Q-2), D-2 (Q-3), D-3 (Q-1), D-4 (Q-4), AC-20 (f) veri sayımı,
  AC-30 rol kontrolü. D-5 CRM sahibinin adımı (kapı değil). ready-for-dev bu kapıları kapatmaz; kapılar DL-1 kabulündedir

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
    (`RecallEvidence.cs:427` — **r4'te düzeltildi:** yalnız iz bölümünde ve yalnız mühürlü anlık görüntü varken, §8.10, §19.8); müşteri bölümü sütunları `customer, notifiedOn, channel, note` (`:312`), kimlik sütunu yok. Hücreler paketin
    dilinde, özet alanı sözcük anahtarıdır (`RecallEvidence.cs:18-26`). `RecallEvidenceTests.cs:144` müşteri satırında saklı teslim yeri metnini
    birebir arar.
  - Sözleşme testleri (**r4'te düzeltildi:** canlı dosyayı başka testler de okur, satır `:40` — §6 tablosu, §19.8): yalnız `RecallContract190Tests` canlı dosyayı okur (`:39` `after = Load(ContractSchemas.RecallContractPath)`, `:41`
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
- **r4 ölçümleri (`a4bb047d9`, salt okuma; kod tabanı `8fa9ba40f` ile aynı):**
  - Ad yerleri: komut adı `declareConsumption` (`recall.openapi.yaml:484`; Web `RecallController.cs:356` `DeclareConsumption`); kodda ve
    sözleşmede `recordConsumption` yok (`grep` 0). İşleyiciler `RecordRecallNotificationHandler` (`RecordHandlers.cs:30-101`),
    `DeclareRecallConsumptionHandler` (`:123-221`), `LinkRecallReturnHandler` (`R3Handlers.cs:378-450`); denetim sırası §8.4'teki satırlarla.
  - Denetimler ile yazım arası: miktar denetimi işlem dışında (`RecordHandlers.cs:168-179`), yazım `InTransactionAsync` içinde (`:199-212`),
    işlem içinde yeniden denetim yok (O-6).
  - İade bağı olay metni `R3Handlers.cs:438` `$"{t.Number ?? t.TransactionId} → {customer}"`; `TimelineEntry` `text` ve `refs` taşır
    (`recall.openapi.yaml:1341-1351`); ekran `drawTimeline` `e.text`'i basar (`case-detail.js:293-300`); kanıt paketi zaman çizelgesi
    `e.Text` (`RecallEvidence.cs:302-303`).
  - Kanıt kurucusu: `IRecallTraceSource _ledger` (`RecallEvidence.cs:130`), `TransactionsOfLotsAsync` yalnız `TraceSectionAsync`'te (`:427`),
    `snapshot is null` → okumadan döner (`:423`); `SalesIssuesOfLotsAsync` portta var (`IRecallTraceSource.cs:59`), `getCaseCustomers` kullanır
    (`CaseDetailReadHandlers.cs:41`); satır ↔ hareket kuralı `:42-44`. Etkinlik tabloları SKU / birim toplamıdır, müşteri sütunu yok (`:351-353`).
  - İz CSV'si: `RecallTraceService.Csv` (`:205-223`), başlık ve düğüm türü sabit metin (`:210`, `:216`), "place" `:213`; dili yok;
    `ExportRecallTraceHandler` (`TraceHandlers.cs:93-109`); servis `EvidenceLanguage()` `Accept-Language`'tan (`RecallController.cs:218-222`,
    7 dilden biri, yoksa `en`); Web `SendAsync` `Accept-Language` iletir (`RecallController.cs:818`), `SendFileAsync` iletmez (`:842-877`).
  - Hata metni eşlemesi `recall-ui.js:49-55`: kod anahtarı varsa onu (`:51`), yoksa 0 / 5xx → `SERVICE_UNAVAILABLE` (`:52`), 401 / 403 →
    `FORBIDDEN` (`:53`); `has` = anahtarın sayfa sözlüğünde olması (`:14`). Web vekili `Failed` → `Refuse(status, code)` (`:933-955`) durumu ve
    kodu korur. tr değerleri: `Recall.Errors.SERVICE_UNAVAILABLE` "Geri çağırma servisine ulaşılamıyor. Biraz sonra yeniden deneyin.",
    `Recall.Errors.FORBIDDEN` "Bu işlem için yetkiniz yok." (`Recall.tr.resx`).
  - Sözleşme: hiçbir uçta `'503'` yok, `components/responses`'ta `ServiceUnavailable` yok (`:1790-1795`); `'403': Forbidden` üç komutta
    (`:479`, `:510`, `:557`); `CROSS_LE_FORBIDDEN` 403 paylaşılan kod (`:993`); `PageSize` varsayılan 25, en çok 200 (`:1011-1012`);
    `getCaseCustomers` sayfasız dizi döner (`:443-453`), müşteri taşıyan öteki okumalar da sayfasız (yalnız `getCaseTimeline` sayfalı, `:640-650`).
  - Test altyapısı: `RecallContractValidatingHandler.Operations` (`RecallTestInfrastructure.cs:141-192`) R7a okumalarını (`customers`,
    `effectiveness`) içermez; üç komutta 403 ve 503 izinli (`:159`, `:160`, `:174`); 2xx dışı gövde `Error` şemasına doğrulanır. Web
    `ContractRoutes.Check` 2xx dışında boş döner (`ContractRoutes.cs:75`), yalnız 2xx yanıt şemalarını yükler (`:187`); `RecallContractFakes.Bound`
    2xx dışını geçirir (`RecallContractFakes.cs:28`).
  - Ekran: müşteriler sekmesi bütün satırları çizer, sayfa yok (`case-detail.js:229-252`); iade bağlama seçim listesi bütün müşterilerden
    (`:400-412`); iz ekranı bütün düğümleri ağaç ve tablo olarak çizer (`trace-view.js:44-60`). Web `CaseData` yalnız `timeline` için `page` /
    `pageSize` iletir (`RecallController.cs:247`).
  - Gateway: recall rotaları `ocelot.json:1097-1131`; dosyada `QoSOptions` / `TimeoutValue` yok (`grep` 0); Ocelot 24.1.0
    (`Diten.ApiGateway.csproj`). Varsayılan zaman sınırı bu makinede ölçülmedi.
  - Rol şablonu yalnız yeni kiracılara (ebeveyn §14.3 başlığı, ROLE1).
- **r5 ölçümleri (`76f93c261`, salt okuma; kod tabanı `9686cafd9` ile aynı — künye notu):**
  - Zaman sınırları (Q-9): Ocelot `24.1.0` (`Diten.ApiGateway.csproj:14`); `ocelot.json` 6 496 satır, `Timeout` / `QoSOptions` geçmez
    (`grep -c -i -E "Timeout|QoSOptions"` 0), `GlobalConfiguration` `:6493-6495` yalnız `BaseUrl`; `Ocelot.xml:253-256`
    `DefaultTimeoutSeconds` = `DefTimeout` (90 sn), `:296-302` / `:495-501` rota / genel `Timeout` tanımsızsa varsayılan geçerli. Web:
    `Program.cs:130` `AddHttpClient()`, `:134-135` varsayılan istemciye yalnız `ShipmentBundleTimeoutHandler` (`:575` yalnız
    `/api/shipment-bundle/`); Recall vekili `_httpClientFactory.CreateClient()` (`RecallController.cs:823`, `:861`), zaman aşımında
    `Reply.Error(503, "SERVICE_UNAVAILABLE")` (`:837`) ve dosyada `(503, …, "SERVICE_UNAVAILABLE", …)` (`:872-875`).
  - Okuyucu deseni: `ShipmentBundleHttpReader` bütün okumaları birlikte başlatıp `SemaphoreSlim(8)` ile sınırlar (`:34-49`), dalga ve kesici
    yok; 5xx → `Unavailable` (`:76-77`), zaman aşımı / `HttpRequestException` → `Unavailable` (`:80-85`). FU01'in CRM okuyucusu `Export` kipinde dalgayı ve kesiciyi
    kendisi kurar (yeni kod, §8.6).
  - İzin anahtarları: `Infrastructure/Authorization/RecallPermissions.cs` — `TraceExport` `:15`, `NotificationsCreate` `:35`,
    `ConsumptionsCreate` `:37`, `ReturnsLink` `:39`, `EvidenceExport` `:47`. Servis uçları: `exportTrace` `[HasPermission(TraceExport)]`
    (`Api/Controllers/Recall/RecallController.cs:128-130`), `previewEvidence` / `createEvidenceExport` / `getEvidenceFile`
    `[HasPermission(EvidenceExport)]` (`:174-176`, `:186-188`, `:202-204`).
  - Dışa aktarım işleyicileri: `CreateRecallEvidenceExportHandler` biçim `:85`, parmak izi + tekrar `:87-94`, kurucu `:97-99` (başarısızlık
    `Response.Fail(code, error, status)`), saklama + denetim `:100-156`; `PreviewRecallEvidenceHandler` `:56-58`; kurucu
    `BuildAsync` `(Document, Code, Error, Status)` (`RecallEvidence.cs:161`), konu doğrulaması `:167`; tatbikat müşteri bölümü
    `notApplicableDrill` `:382`, iz bölümü kapsam satırsız `:380`; dönem `notApplicablePeriod` `:408`. `ExportRecallTraceHandler` biçim
    `:95`, parse `:96-99`, iz `:101-102`, denetim satırı (`recall.trace.run`, bağlam `exportTrace`) `:103-107`, CSV `:108`
    (`TraceHandlers.cs`).
  - Sözleşme: `exportTrace` `x-error-codes` `:100`, `'403'` `:108`; `previewEvidence` `:813`, `:822`; `createEvidenceExport` `:838`, `:858`
    (`recall.openapi.yaml`). Test doğrulayıcısı üç uçta 403'e zaten izin verir (`RecallTestInfrastructure.cs:167`, `:188`, `:190`).
  - Web: kanıt ekranı önizleme / dışa aktarım hatası `ui.errorText(reply)` (`evidence.js:107`, `:129`); iz CSV'si bir bağlantı
    (`trace.js:95-97`); Web `ExportTrace` başarısızlıkta `Refuse(status, code)` JSON gövdesi (`RecallController.cs:144`, `:946-954`).
  - Sıra: `getCaseCustomers` gruplaması `OrderBy(g => g.Key, StringComparer.Ordinal)` (`CaseDetailReadHandlers.cs:75`); `Crm` anahtarı
    küçük harfli `"D"` GUID (§8.3) — sıra fiilen GUID sırasıdır (F-3@807c8dc2).

### 19.3 Varsayımlar

- ASSUMPTION-FU01-1: `customerRef` sevkiyat kökünde (`getShipment` 200) tek değerdir; satır düzeyinde değildir (Q-2 ile teyit).
- ASSUMPTION-FU01-2: MVP-6 yalnız kendi kiracısının hesabını yazar; aksi veri hatası CRM 404 ile "Bulunamadı" olur, sızmaz (AC-11).
- ASSUMPTION-FU01-3: Geri çağırma bildirimi müşterinin CRM durumundan bağımsızdır (pasif / arşivli müşteri de çözülür ve gösterilir).
- ~~ASSUMPTION-FU01-4~~ → **karar Q-7 = açık** (Beste, 2026-11-04; §19.5 madde 11): `NotFound` satırında kayıt komutları açıktır; satır ayırıcıyla ayırt edilir.
- ASSUMPTION-FU01-5: Sağlıklı CRM = Gateway dahil okuma başına ≤ 500 ms. `PageBudget = 3 sn` bu varsayıma dayanır (§8.6); CRM taslağında süre
  hedefi yok, gerçek gecikme ölçülmedi. Teyit Q-3 sahibinde (CRM); daha yüksek bir sağlıklı gecikme yazılırsa sayfa boyu ya da tavan
  yeniden hesaplanır (CT ölçütü: sayfa boyu × gecikme / 8 < tavan).

### 19.4 Açık sorular

| # | Soru | Sahip | Tarih |
|---|---|---|---|
| Q-1 | CRM çağrı yolu: kullanıcı token'ı Gateway üzerinden mi, servis kimliği mi? (servis kimliğinde kiracı nasıl taşınır) — **DL-1 kabul kapısı** (§7 D-3) | CRM / commercial-suite (Beste) | soruldu 2026-11-03; yanıt TBD |
| Q-2 | SHIPMENT 3.5.0 alanının kesin adı, tipi (`string`, `format: uuid`?), yeri (kök), küçük harf garantisi ve `shipToReference`'ın `"D"` biçimli GUID olmayacağı garantisi | MVP-6 CT | TBD (3.5.0 taslağıyla teyit) |
| Q-3 | MOD-0149 DL-1 (U-1 ucu) teslim tarihi ve sözleşmenin dondurulma tarihi | CRM / commercial-suite (Beste) | TBD |
| Q-4 | `crm.account.lookup-validation` hangi geri çağırma rollerine atanır (Q-1 = kullanıcı token'ı ise) — **DL-1 kabul kapısı** (§7 D-4) | Auth (Ali) | TBD |
| ~~Q-9~~ | **Kapandı (r5).** (a) Ölçüldü (inceleme 01 tur 3, CT kararı F-1@807c8dc2): Gateway tanımsız rota zaman aşımında 90 sn keser, Web `HttpClient` 100 sn; kesicisiz yanıtsız CRM'de eşik ≈ 361 hesap → devre kesici (CT-9, §8.6, AC-29 (d)). (b) Önizleme `Export` kipinde kalır: tur 3 bunu bulgu saymadı (önizleme dosyanın aynısıdır), CT kararı yeniden açmadı; kesici yanıtsız CRM'de bekleyişi ≈ 2 sn'ye indirir (§8.6). Kalan sınır O-7 | MVP-1 CT | kapandı 2026-11-04 |

Q-5 ve Q-6 r2'de, Q-7 ve Q-8 r4'te, Q-9 r5'te kapandı — §19.5 madde 8, 9, 11, 12; §19.6 CT-9.

### 19.5 Verilen kararlar (sahip — Beste / MVP-1 CT, 2026-11-03 ve 2026-11-04)

1. `customerRef` okuma ve sıra (customerRef → shipToReference → DOC), harf duyarsız karşılaştırma — §8.2, §8.3.
2. Ad ve kod CRM-ACCOUNT-LOOKUP 1.0.0 U-1'den; zaman aşımı ve önbellek mevcut okuyucu deseniyle (okuma başına 2 sn, önbelleksiz) — §8.6;
   r3'te istek başına toplam 2 sn bütçe eklendi (F-3, yazar kararı); r4'te bu bütçe CT-6 ile müşteri sayfasına bağlandı (sayfa 25, tavan
   3 sn) ve dışa aktarımda kaldırıldı; r5'te dışa aktarıma devre kesici eklendi (CT-9).
3. GUID gösterilmez; "Çözülemedi" / "Bulunamadı" — §8.8.
4. **SB-19:** eski anahtarlı satırlar yeni kimliğe otomatik bağlanmaz; rozetle ayrı satır — **kabul edilen sınır** (§8.5).
5. Kayıtlar yeni anahtarı kabul eder; eski kayıtlar değişmez — §8.4.
6. MOD-0177 CRM tüketicisi olarak yazılır (sözleşme CRM'in); izin ataması Auth işi — §7 D-4, D-5.
7. Yeni metinler 7 dilde — §11.
8. **Q-5 = HAYIR** (Beste, 2026-11-03): hiç müşteri kaydı olmayan vakada da eski anahtar CRM kimliğine yükseltilmez; kural kayıt varlığına
   bakmaz, revizyon kopyası için de geçerlidir — §8.5, AC-6, AC-23. "Eski"nin kapsamı r3'te FU01 öncesi anahtarlara daraltıldı (CT-5; teyit Q-8 = EVET, madde 12).
9. **Q-6 = HAYIR** (Beste, 2026-11-03): elle eklenen kapsam satırında CRM müşterisi seçilemez; satır `Manual` kaynaklı serbest metindir,
   `"D"` biçimli GUID metni 400 `INVALID_REQUEST` ile reddedilir (uygulama biçimi paket yazarının tasarımıdır — inceleme konusu), ekranda
   "CRM kimliği yok" rozeti ve `ManualHint` araç ipucu — §8.2, §8.8, AC-20, AC-23. FU-0177-FU01-SEL kapandı (§20).
10. **F-1 sahip kararı** (Beste, 2026-11-03): ad çözülemezse satırda sevkiyatın teslim yeri metni ve sevkiyat numarası görünür; ad çözülene
    kadar bildirim, tüketim beyanı ve iade bağlama kapalı ("CRM'e ulaşılamıyor, sonra tekrar deneyin", 7 dil); GUID hiç görünmez. Sunucu da
    reddeder: 503 `RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE` (kod seçimi yazarın, gerekçe §8.4) — §8.4, §8.8, AC-25, AC-27. "Sevkiyat numarası"
    olarak ledger hareket numarası alındı (her yerde mevcut ve GUID değil; MVP-6 sevkiyat kimliği GUID'dir) — §8.8.
11. **Q-7 = açık** (Beste, 2026-11-04): "Bulunamadı" (`NotFound`) satırında bildirim, tüketim beyanı ve iade bağlama açıktır — §8.4, §8.8,
    AC-25 (c), AC-27; ASSUMPTION-FU01-4 karara dönüştü.
12. **Q-8 = EVET** (Beste, 2026-11-04): "eski" yalnız FU01 öncesi (`CustomerRefSource = null`) anahtardır; FU01 sonrası `ShipTo` / `Document`
    satırı eski değildir ve `Draft`'ta `Crm`'e geçebilir — §8.5, AC-6 (a4), (a5), (c3), AC-23 (S-23d). CT-5 sahip teyidini aldı.

### 19.6 CT kararları (MVP-1 CT, 2026-11-03 ve 2026-11-04 — verildi)

| # | Karar | Sonuç |
|---|---|---|
| CT-1 | `CustomerRefSource` rs-scope-v1 mührüne girer mi? | **Hayır** — biçim adı ve FU01 öncesi mühürler değişmez (§4.1, §8.5, AC-15) |
| CT-2 | Sözleşme numarası | RECALL **1.10.0** — `97cf7d170`'de sözleşme hâlâ `1.9.0` (§7 D-6) |
| CT-3 | Kanıt dışa aktarımı ve ad | Ad dışa aktarım anının anlık görüntüsüdür; §2 "ad saklanmaz" kuralı kayıt alanlarıyla sınırlanır; kesintide çözülemeyen satır "Çözülemedi" işaretiyle girer (§2, §8.10, AC-26) |
| CT-4 | D-4 (izin) ve Q-1 (çağrı yolu) | **DL-1 kabul kapısı** (§7, §22) |
| CT-5 | Q-5 "eski" kapsamı (F-7) | FU01 öncesi (`null` kaynak) anahtarlarla sınırlı; FU01 sonrası geçici `Document` / `ShipTo` satırı `Draft`'ta `Crm`'e geçebilir (§8.5) — sahip teyidi Q-8 = EVET (2026-11-04) |
| — | Bulgular a66dd353 | hepsi kabul; r3'te tek geçiş (S-101) |
| CT-6 | F-1@fbafd3af — ad çözümünün bütçesi (2026-11-04) | Toplam bütçe ekranda gösterilen sayfaya bağlanır: ad yalnız görünen sayfanın müşterileri için çözülür (okuma başına 2 sn, en çok 8 eşzamanlı, sayfa için toplam tavan; değer gerekçeli). Sağlıklı CRM'de sayfa her zaman çözülebilecek büyüklükte (sayfa boyu × gecikme / 8 < tavan). Kanıt dışa aktarımında toplam tavan yok (okuma başına 2 sn, 8 eşzamanlı); zaman aşımı olan satır işaretli girer. Yeni AC: gecikmeli ama sağlıklı CRM + çok hesaplı vaka, sayfa ve dışa aktarım ayrı, sabotajlı. **Uygulama:** sayfa 25 müşteri, tavan 3 sn (§8.6), `customerPage`, `Deferred` (§8.7, §8.8); AC-12, AC-28, AC-29, AC-31 |
| CT-7 | F-2@fbafd3af — izin eksikliği (2026-11-04) | CRM 401 / 403 ayrı durumdur ("izin eksik" metni, yeniden deneme önerilmez, 7 dil); yazım reddi kodu buna göre (gerekçeli); her hedef ortamda kiracı başına rol ataması geçiş öncesi kontrol edilir (AC-20 (f) eşi). **Uygulama:** `Forbidden`, 403 `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN` (§8.4, §8.6, §11); AC-18, AC-27, AC-30 |
| CT-8 | F-3…F-6@fbafd3af (2026-11-04) | Hepsi kabul: canlı sözleşmeyi okuyan testler Repo Scope'ta ve 503 / 403 şeması DL-2 önkoşulu; kanıt paketinde ayırıcının kaynağı ve sevkiyat ↔ hareket eşleşme kuralı; `exportTrace` için CSV işaret kuralı; S-27c'nin `recall-ui.js:51-52` ile hizası; §8.4'te çözüm denetiminin 422 / `supersedesId` denetimlerine göre yeri (§19.8) |
| — | Bulgular fbafd3af | hepsi kabul; r4'te tek geçiş (S-101) |
| CT-9 | F-1@807c8dc2 — dışa aktarımın uçtan uca zaman sınırı (2026-11-04) | Export kipine devre kesici: ilk 8'lik dalganın hepsi zaman aşımı / 5xx / ulaşılamaz ise kalan hesaplar çağrılmadan `Unresolved`; sağlıklı gecikmeli CRM'de hepsi çözülür; Q-9 ölçümü pakete kanıt; AC-29'a 400 hesaplı yanıtsız CRM ölçütü ve sabotajı (kesici kaldırılınca 90 sn aşılır). **Uygulama:** ordinal sırayla 8'lik dalgalar, kesici sınıfı ve günlük `Breaker` (§8.6); denetim her dalgada (yazar kararı — inceleme konusu, gerekçe §8.6; CT yalnız ilk dalgayı isterse tek koşul); AC-14, AC-29 (b), (d)…(h); Q-9 kapandı |
| CT-10 | F-2@807c8dc2 — dışa aktarım rolleri ve izin eksikliği (2026-11-04) | `recall.evidence.export` ve `recall.trace.export` AC-30 rol kontrolüne girer; `Forbidden` müşteri varken dışa aktarım (kanıt ve iz) 403 `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN` ile reddedilir — izin eksikliği kalıcı GxP dosyasına girmez; AC-26 / AC-29'a ölçüt + sabotaj; 7 dil metni. **Uygulama:** `previewEvidence` de reddedilir (aynı kurucu; yazar kararı — inceleme konusu, gerekçe: önizleme dosyanın aynısıdır); ret bütün denetimlerden sonra, yazımdan önce, tekrar önce (§8.10); sözleşmede üç ucun `x-error-codes`'u (§8.7); ekranda kanıt ekranı hata metni, iz ekranında `Recall.Customer.ExportForbidden` (§8.8, §11); AC-16, AC-18, AC-26 (h), (i), AC-27, AC-29 (i), AC-30 |
| CT-11 | F-3@807c8dc2 — müşteri sayfalarının sırası (2026-11-04) | Ordinal anahtar sırası (`Crm` müşterilerinde fiilen GUID sırası) kabul edilen sınır; §8.8 ve §20'ye yazılır, adla sıralama / arama ayrı takip. **Uygulama:** §8.8 "Kabul edilen sınır", §20 FU-0177-FU01-FIND |
| — | Bulgular 807c8dc2 | hepsi kabul; r5'te tek geçiş (S-101) |
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

### 19.8 Bağımsız inceleme 01 tur 2 — kapanış tablosu (r3 → r4)

Bulgu dosyası `docs/records/reviews/MOD-0177-FU01/2026-11-04-fbafd3af.md` (`a5cf9d514`; dosya özeti `7a5c093a…a701718`,
`git show a4bb047d9:<yol> | sha256sum`); hedef r3 pack sha256 `fbafd3af…7e412f`, kaynak `8fa9ba40f`; UYGUN DEĞİL, 1 ENGEL, 1 ORTA, 4 DÜŞÜK; CT
kararı (MVP-1 CT, 2026-11-04): hepsi kabul, r4'te tek geçiş. `8fa9ba40f` → `a4bb047d9` arasında Recall kodu, Web, Web testleri ve
`recall.openapi.yaml` değişmedi (künye notu). Tur 1 kapanışları ve tur 1'de sayılan sahip / CT kararları yeniden açılmadı; Q-7 ve Q-8 sahip
kararları işlendi (§19.5 madde 11, 12). Her düzeltme kodla ölçüldü (§19.2 r4 ölçümleri).

| Bulgu | Düzeltme | Bölümler |
|---|---|---|
| F-1@fbafd3af (ENGEL) | CT-6 uygulandı: r3'ün çağrı başına `TotalBudget = 2 sn`'si kaldırıldı. İki kip: `Page` — ekran okumaları adı yalnız istenen müşteri sayfasının (ordinal anahtar sırasıyla 25'lik dilim, `customerPage`) `Crm` müşterileri için çözer, okuma başına 2 sn, 8 eşzamanlı, sayfa tavanı 3 sn (gerekçe: varsayılan sayfa boyu 25, 4 dalga, sağlıklı CRM ≤ 500 ms → ≤ 2 sn; ASSUMPTION-FU01-5); dilim dışı `Crm` satırı `Deferred`. `Export` — kanıt önizleme / dışa aktarımı ve iz CSV'si toplam tavansız, zaman aşımı olan satır işaretli. Ekran müşteriler sekmesini ve iade seçim listesini sayfalar, öteki tablolarda sayfa seçicisi. Sözleşmede `customerPage`, `Deferred`. AC-12 tavan değerleri; yeni AC-28 (200 hesap, 200 ms, sayfa; sabotaj r3 bütçesi), AC-29 (200 hesap, dışa aktarım; sabotaj sayfa kipi / 3 sn tavan), AC-31 (ekran sayfası). Açık: Q-9 (dışa aktarımın uçtan uca zaman sınırı, önizlemenin kipi) | §1, §5, §7 D-5, §8.6, §8.7, §8.8, §8.9, §8.10, §11, §12, §13, §17, §19.3, §19.4; AC-12, AC-14, AC-16, AC-24, AC-28, AC-29, AC-31 |
| F-2@fbafd3af (ORTA) | CT-7 uygulandı: CRM 401 / 403 ayrı sonuç `Forbidden` ("İzin eksik", ayırıcılı; ekranda `CrmForbidden` — yeniden deneme önerilmez; 7 dil); yazım reddi 403 `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN` (gerekçe: çağıranın kalıcı yetki durumu; 503 ve `recall-ui.js:52` düşüşü "yeniden dene" der; 403 kodlu ret emsali `CROSS_LE_FORBIDDEN`; kod, recall'un kendi 403'ünden ayırt eder); kanıt dosyasında `ForbiddenWith`; canlı geçiş kapısı: her hedef ortamda kiracı başına rol kontrolü (AC-30, AC-20 (f) eşi, imzalı); AC-18 yeniden yazıldı (401 ve 403 ayrı fixture), AC-27'ye ekran metni ölçütü | §1, §3, §7 D-4, §8.4, §8.6, §8.7, §8.8, §8.10, §11, §13, §14, §21; AC-8, AC-18, AC-19, AC-21, AC-24, AC-27, AC-30 |
| F-3@fbafd3af | §6 cümlesi düzeltildi ve canlı sözleşmeyi okuyan testler tablolandı (`RecallContractTests` `:179`, `RecallContractYaml` `RecallESignTests.cs:128` / `:583-588`, `RecallContractValidatingHandler` + `ContractSchemas.cs:147`, Web `ContractRoutes.cs:29` / `RecallContractFakes.cs:25-40`); ölçüldü: Web sahteleri yalnız 2xx gövdeleri denetler (`RecallContractFakes.cs:28`, `ContractRoutes.cs:75`), bu yüzden DL-2 önkoşulu 1.10.0'ın 2xx şemaları (`customer`) ve DL-1'in 503 / 403 tanımlarıdır (§22); `RecallContractValidatingHandler` listesinde R7a okumaları yoktu — Repo Scope'a eklendi; `RecallContract190Tests` satırı `:39` → `:40` | §5, §6, §17, §22; AC-16, AC-27 |
| F-4@fbafd3af | r3 iddiası (`:427`) düzeltildi: kanıt kurucusu müşteri bölümü için ayrı `SalesIssuesOfLotsAsync` okuması yapar (yalnız `Resolved` olmayan `Crm` müşterisi varsa; mühürsüz vakada da); satır ↔ hareket eşleşmesi `getCaseCustomers`'ın kuralı (`CaseDetailReadHandlers.cs:42-44`, `RecallReads.MovementsBehind`) ve sıra (`NodeKey`, sonra `PostingDate`, `TransactionId`) tek kural `RecallCustomerDisplay.Distinguisher`; AC-26 (f) mühürsüz vaka, (g) gereksiz okuma yok, S-26e, S-26f | §8.8, §8.10, §19.2; AC-26 |
| F-5@fbafd3af | `exportTrace` için kural izin kendi düğümlerine bağlandı (izde en az bir `Crm` Shipment düğümü varsa işaret; canlı iz `Legacy` üretmez; `Crm` düğümü yoksa bayt eşit); dil `Accept-Language` (komut `Language` alır; Web dosya isteği başlığı iletir); `Export` kipi; AC-10 (f), S-10f; O-5 (CSV başlığı sabit metin, ebeveyn) | §5, §8.8, §20; AC-10, AC-29, AC-31 |
| F-6@fbafd3af | (a) S-27c beklentisi koda hizalandı: anahtar yoksa `recall-ui.js:52` `SERVICE_UNAVAILABLE` metnini gösterir; ölçüt gösterilen metnin ilgili anahtarın değerine eşit ve genel metinden farklı olmasıdır (S-27c, S-27d; AC-19 7 dilde farkı sınar, S-19b). (b) §8.4'te çözüm denetimi her işleyicinin **bütün** mevcut denetimlerinden sonra, yazımdan hemen önce (satır satır sıra); CRM kapalıyken ya da izin eksikken geçersiz istek bugünkü 400 / 422'sini alır, CRM'e 0 çağrı; AC-25 (f), (g), S-25f; pencerenin uzaması O-6 | §8.4, §8.8, §11, §17.1, §20; AC-19, AC-25, AC-27 |
| Ek (pack-review-common-findings §2) | (1) `recordConsumption` kodda ve sözleşmede yok — her yerde `declareConsumption`; (2) AC-16'daki `ContractSchemaValidator` sınıfı yok — `RecallContractValidatingHandler` / `ContractSchemas.ValidateRecall`; (3) GUID sızıntısı: `linkReturn` olay metni müşteri anahtarını taşıyordu (`R3Handlers.cs:438`) ve zaman çizelgesinde / kanıt paketinde basılıyordu — `Crm` eşleşmesinde metin anahtarsız (§8.4, AC-10 (e), S-10e); (4) kanıt paketinde "etkinlik tabloları" müşteri hücresi taşımaz (`RecallEvidence.cs:351-353`) — §8.10 düzeltildi; (5) Web `SendFileAsync` `Accept-Language` iletmiyordu — iz CSV'sinin dili için DL-2 Repo Scope | §3, §5, §8.4, §8.8, §8.10, §19.2; AC-3, AC-4, AC-10, AC-16, AC-25, AC-31 |
| Ek (pack-review-common-findings §1, §4) | `status_note`, §18, §22 Q-7 / Q-8'i artık önkoşul saymaz (tutarlılık); her AC tek satır (§4); kabul aracı 31 AC'yi okur (§19.8 sonu) | künye, §18, §22 |

### 19.9 Bağımsız inceleme 01 tur 3 — kapanış tablosu (r4 → r5)

Bulgu dosyası `docs/records/reviews/MOD-0177-FU01/2026-11-04-807c8dc2.md` (`28af31ae9`; dosya özeti `1c62aa4d…1d5e`,
`git show 76f93c261:<yol> | sha256sum`; inceleme satırı `execution/approvals/reviews.tsv`, `76f93c261`); hedef r4 pack sha256
`807c8dc2…d3f1d4`, kaynak `9686cafd9`; KOŞULLU, 0 ENGEL, 2 ORTA, 1 DÜŞÜK; CT kararı (MVP-1 CT, 2026-11-04): üçü de kabul, r5'te tek geçiş.
`9686cafd9` → `76f93c261` arasında Recall kodu, Web, Web testleri, sözleşmeler ve Gateway değişmedi (künye notu). Tur 1 ve tur 2'de sayılan
sahip ve CT kararları ve tur 2'nin CT kararı (sayfaya bağlı çözüm, 401 / 403 ayrı durum, kiracı başına rol kontrolü) yeniden açılmadı. Her
düzeltme kodla ölçüldü (§19.2 r5 ölçümleri).

| Bulgu | Düzeltme | Bölümler |
|---|---|---|
| F-1@807c8dc2 (ORTA) | CT-9 uygulandı: `Export` kipine devre kesici — hesaplar ordinal sırayla 8'lik dalgalara bölünür, bir dalganın bütün okumaları kesici sınıfındaysa (zaman aşımı, 5xx, ulaşılamıyor; 200 / 400 / 401 / 403 / 404 değil) kalan hesaplar çağrılmadan `Unresolved`, günlükte `Breaker`; sağlıklı gecikmeli CRM'de hepsi çözülür (AC-29 (e)). CT'nin "ilk dalga" kuralı her dalgaya uygulandı (yazar kararı — inceleme konusu: ortada düşen CRM'de 400 hesap ≈ 96 sn olurdu). Q-9 ölçümü kanıt olarak §8.6'da (Ocelot 24.1.0 `DefTimeout` 90 sn, Web `HttpClient` 100 sn, kesicisiz eşik ≈ 361 hesap; ölçülmeyenler adıyla). AC-29 (b) süresi 7 sn → 3 sn; yeni ölçütler (d) 400 hesap yanıtsız CRM üç uçta (8 çağrı, ≤ 4 sn), (e) 400 hesap sağlıklı, (f) ortada düşen CRM, (g) kısmi kesinti, (h) 404 dalgası; sabotajlar S-29e (CT ölçütü: kesici kaldırılınca 100 sn > 90 sn), S-29f…S-29h; AC-14 `Breaker`. Q-9 kapandı; kalan sınır O-7 | künye, §1, §7 D-5, §8.6, §8.7, §13, §17, §17.1, §19.2, §19.4, §19.6, §20; AC-14, AC-29 |
| F-2@807c8dc2 (ORTA) | CT-10 uygulandı: AC-30 rol listesine `recall.evidence.export` ve `recall.trace.export` (sonuç kayıt ve dışa aktarım rolleri için ayrı); konuda `Forbidden` müşteri / düğüm varken `createEvidenceExport`, `previewEvidence` (yazar kararı — aynı kurucu) ve `exportTrace` 403 `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN`, dosya / günlük / denetim satırı yok; ret bütün denetimlerden sonra, yazımdan önce, tekrar denetiminden sonra (saklanmış dışa aktarımın tekrarı 200); tatbikat ve dönem kanıtı CRM çağırmaz, etkilenmez; sözleşmede üç ucun `x-error-codes`'u (403 yanıtı zaten tanımlı); `Forbidden` dosyalardan çıktı (`Forbidden` / `ForbiddenWith` yalnız Web ailesine taşındı); ekranda kanıt ekranı mevcut hata yolu, iz ekranında CSV bağlantısı yerine `Recall.Customer.ExportForbidden` (7 dil); saklı dosyaya erişimin CRM izni sormadığı açık yazıldı (yazar kararı — inceleme konusu). AC-26 (h), (i) + S-26g…S-26j; AC-29 (i) + S-29i, S-29j; AC-16 + S-16f; AC-18; AC-24; AC-27 + S-27f, S-27g; AC-19 + S-19c; AC-30 | künye, §1, §2, §3, §5, §7 D-4, §8.4, §8.6, §8.7, §8.8, §8.9, §8.10, §11, §12, §13, §14, §17, §21, §22; AC-10, AC-16, AC-18, AC-19, AC-24, AC-26, AC-27, AC-29, AC-30 |
| F-3@807c8dc2 (DÜŞÜK) | CT-11 uygulandı: müşteri sayfalarının ordinal anahtar sırası (`Crm` müşterilerinde fiilen GUID sırası; adla sıralama ve arama yok; 200 müşteride 8 sayfaya kadar gezinme, her sayfa bir CRM turu) kabul edilen sınır olarak §8.8'de; iyileştirme FU-0177-FU01-FIND (sevkiyat numarası / teslim yeri ile süzme, CRM toplu sorgusu gelince adla sıralama) | §8.8, §20 |
| Ek (pack-review-common-findings §1–§3a) | Künye `status: ready-for-dev` (S-49), `approval_status: pending`, `revision` alanı; §18 ve §22 ready-for-dev'i DL-1 kabul kapılarından ayırır (§1 "hazırlık listesi ile dilim tablosu"); dışa aktarım reddi üç yolun her biri için ayrı fixture ve sabotaj (§3 "bir koruma birden çok yolu kapsıyor"); tekrar erken dönüşünün izin reddinden önce kaldığı ve saklı dosya erişiminin kuralı yazıldı (§3a "tekrar erken dönüşü"); her ad ve satır kodda ölçüldü (§2, §19.2 r5); her AC tek satır, kabul aracı 31 AC'yi okur (§4) | künye, §8.10, §16, §18, §19.2, §22 |

## 20. Follow-up Items

| ID | Konu | Sahip |
|---|---|---|
| O-1 | **Gözlem (ebeveyn, bu FU'nun kapsamı dışı):** bugün yeniden izlemede MVP-6 o an erişilemezse saklı shipTo anahtarı `DOC:` anahtarına düşer (`RecallTraceService.cs:141`); o müşterinin kayıtları yetim kalmaz (Draft'ta kayıt yok) ama etiket değişir. FU01 yalnız CRM anahtarını korur (§8.5). Eski → eski düşüşün düzeltilmesi ayrı karar | MOD-0177 / CT |
| FU-0177-CUST-01 | Ebeveyn §20 satırı bu paketle karşılanır (MVP-6 party id yerine CRM `Account.Id` + U-1) — ebeveyn satırının güncellenmesi CT adımı | CT |
| O-2 | **Gözlem (veri):** FU01 öncesi `"D"` GUID biçimli eski anahtar aynı vakadaki `Crm` satırıyla çakışabilir ve parmak izi değişir (§8.3). Ölçüm AC-20 (f) sayımı; sıfır değilse DL-1 kabul edilmez ya da geçiş durur, satırların işlenişi CT kararı (göç yok) | CT |
| O-3 | **Gözlem (ebeveyn):** `getCaseCustomers`'ın MVP-6 okuması toplam bütçesizdir (tavan(N/8) × 2 sn, `ShipmentBundleHttpReader.cs:36-49`); FU01 CRM payını ekran okumasında ≤ 3 sn'de tutar ve eşzamanlı koşar, MVP-6 payını değiştirmez (MVP-6 okuması sayfalanmaz — bütün satırların teslim durumu bugünkü gibi okunur) | MOD-0177 / CT |
| O-5 | **Gözlem (ebeveyn):** iz CSV'sinin başlığı ve düğüm türü sabit metindir (`RecallTraceService.cs:210` Türkçe başlık, `:216` `Kind.ToString()`); FU01'in yeni hücre sözcükleri `Accept-Language` ile yerelleşir, başlık değişmez — başlığın yerelleştirilmesi ebeveynin konusu | MOD-0177 / CT |
| O-6 | **Gözlem (ebeveyn):** kayıt işleyicileri denetimleri işlem dışında yapar ve işlem içinde yinelemez (ör. miktar `RecordHandlers.cs:168-179`, yazım `:199-212`); FU01'in çözüm denetimi bu pencereyi CRM okuması kadar (≤ 2 sn) uzatır, yeni yarış türü açmaz. Eşzamanlı iki beyanın toplamı aşması bugün de mümkündür; düzeltilmesi ayrı karar | MOD-0177 / CT |
| O-4 | **Ölçülmedi:** Platform denetim günlüğü görüntüleyicisinin denetim ayrıntısındaki `customerRef`'i (CRM anahtarında GUID) kullanıcıya gösterip göstermediği. Gösteriyorsa §8.8 kuralı dışında bir yüzeydir; Platform sahibine sorulur | CT / Platform |
| FU-0177-FU01-SEL | Elle eklenen kapsam satırında CRM müşteri seçici — **kapandı, açılmaz**: Q-6 = HAYIR (Beste, 2026-11-03). Yeniden istenirse yeni sahip kararı ve yeni FU gerekir | — |
| FU-0177-FU01-REBIND | Eski anahtarlı kayıtların CRM kimliğine **elle**, denetimli bağlanması (SB-19'un otomatik yasağı korunarak) — ürün kararı | Ürün / CT |
| FU-0177-FU01-FIND | **Kabul edilen sınırın iyileştirmesi (F-3@807c8dc2, CT-11):** müşteri sayfaları ordinal anahtar sırasındadır (`Crm` müşterilerinde fiilen GUID sırası), adla sıralama ve arama yoktur (§8.8). Seçenekler: müşteriler sekmesinde ve iade bağlama seçim listesinde sevkiyat hareket numarası ya da teslim yeri metniyle süzme (CRM'siz, sunucu tarafında, sayfa kuralından önce); CRM toplu sorgusu (birden çok hesabı tek istekte döndüren uç — CRM sözleşmesinde bugün yok) gelince adla sıralama ve arama. Plan satırı yok; açılırsa yeni FU paketi | MOD-0177 / CT; CRM toplu sorgusu MOD-0149 |
| O-7 | **Kalan sınır (F-1@807c8dc2, CT-9):** devre kesici yalnız bir dalganın tamamı düşünce açılır. Her dalgada en az bir okumanın zaman aşımına uğradığı kısmi kesintide her dalga ≈ 2 sn sürer (≈ 361 hesap üstünde Gateway'in 90 sn'si yine keser, genel 503); sağlıklı sınırdaki (500 ms) CRM'de ≈ 1 440 hesaba kadar sınırın altında kalınır. Ölçülmeyenler: Ocelot'un 90 sn'de döndürdüğü kod, Inventory işleyicisinin iptali alıp almadığı, canlı ters vekil sınırları (§8.6). Gateway rotasına zaman aşımı yazmak integration-agent'ın kararıdır | CT / integration-agent |
| O-8 | **Gözlem (ebeveyn):** Recall'un dosya bağlantıları (`/Recall/export/trace`, `/Recall/export/evidence/{id}`) başarısızlıkta Web vekilinin JSON ret gövdesini döner (`RecallController.cs:144`, `:706`, `Refuse` `:946`); tarayıcı bunu sayfa olarak açar (503 dahil, bugünkü davranış). FU01 iz ekranında `Forbidden` görünür düğümde bağlantıyı kaldırır (§8.8), öteki durumları değiştirmez | MOD-0177 / CT |

## 21. Audited Events

Yeni yazma komutu ve yeni denetim olayı yok. Mevcut olaylar (ebeveyn §8.12): `recall.customer.notified`, `recall.consumption.declared`,
iade bağlama — ayrıntıdaki `customerRef` CRM anahtarında kanonik GUID'dir (kayıtta kalır; ad girmez). Davranışı değişen mevcut yazma komutları:
`addScopeItems` (`RecallContract.AuditScopeItemsAdded`, yol ebeveynin) — başarılı istekte denetim satırı değişmez, Q-6 reddinde (400) hiç
yazılmaz (AC-20 (e)); `recordNotification`, `declareConsumption`, `linkReturn` — çözüm denetimi reddinde (503 / 403) hiç yazılmaz (AC-25 (b),
AC-18); `linkReturn`'ün `Crm` eşleşmesinde vaka olayının metni anahtarsızdır, denetim satırının `customerRef` ayrıntısı değişmez (§8.4);
`submitScope` ve `reviseScope` — denetim satırları değişmez (`CustomerRefSource` denetim ayrıntısına girmez); `createEvidenceExport` —
`recall.evidence.exported` satırı değişmez, ad ona girmez (AC-26 (e)); izin reddinde (403 `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN`) dışa
aktarım günlüğü satırı, dosya ve denetim satırı hiç yazılmaz (AC-26 (h)); `exportTrace` (yazma komutu değil, ama `recall.trace.run`
denetim satırı yazar, `TraceHandlers.cs:103-107`) — başarılı istekte satır değişmez, izin reddinde hiç yazılmaz (AC-29 (i)). Ret, FU01'in
kayıt komutlarındaki 503 / 403 reddiyle aynı kuraldadır (yazım 0, §8.4) — istisna sınıfı değil, yazılmayan işlem. CRM okuması AUD-001 okuma olayı üretmez (CRM
taslağı §1).

## 22. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | arka uç: customerRef okuma + anahtar sırası + ad çözümü (müşteri sayfası ve dışa aktarım kipleri, devre kesici) + çözüm denetimi (503 / 403) + kayıtlar + kanıt anlık görüntüsü ve dışa aktarımın izin reddi (403) + RECALL 1.10.0 + 7 dil metinleri | Owned Objects, Entity Fields, Repo Scope, Dependencies, Müşteri anahtarı sırası, Anahtar biçimi ve tekrar parmak izi, Kayıtlar, Yeniden izlemede anahtar kararlılığı (SB-19), Ad çözümü (CRM-ACCOUNT-LOOKUP U-1), Sözleşme — RECALL 1.10.0, Gösterim kuralı, Kanıt paketi ve ad anlık görüntüsü, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Audited Events | E4 | plan satırı yok | MVP-1 |
| DL-2 | ekran: ad + kod, durum metinleri ve ayırıcı, kapalı komutlar, rozetler, müşteri sayfası, iz ekranında izin eksikken CSV bağlantısı yerine metin, Web vekilinin sorgu ve dil iletimi | Gösterim kuralı, Frontend File Contract, Layout & Shell Contract, Page Help (UXH-001) | E4 | plan satırı yok | MVP-1 |

Kabul ölçütleri: DL-1 → AC-1…AC-20, AC-25, AC-26, AC-28, AC-29, AC-30; DL-2 → AC-21…AC-24, AC-27, AC-31. **DL-1 kabul kapısı:** D-1 (SHIPMENT
3.5.0 FROZEN, AC-17 Skip'siz yeşil, Q-2 teyidi), D-2 (CRM U-1 şekli teyitli ya da sözleşme FROZEN), D-3 (Q-1 = kullanıcı token'ı; aksi paket
revizyonu), D-4 (Q-4 yanıtlı ve kabul ortamında rol ataması yapılmış; AC-30 (1) = 0), AC-20 (f) veri sayımı = 0 (kabul ortamı; O-2).
**Canlı geçiş kapısı** (her hedef ortam, sahibi MVP-1 CT): AC-20 (f) geçişten hemen önce ve hemen sonra, AC-30 (2) geçişten hemen önce, kiracı
başına — sıfır değilse o ortamın geçişi durur. D-1…D-4 kapanmadan DL-1 ikizlerle kodlanabilir, kabul edilemez. **DL-2 önkoşulu (F-3@fbafd3af):**
DL-1 kabul edilmiş ve RECALL 1.10.0 canlı dosyada — Web sahteleri 2xx gövdeleri canlı sözleşmeye bağlar (`RecallContractFakes.Bound`, kapalı
nesneler), `customer` taşıyan bir gövde 1.9.0'da sözleşme ihlali olur; 503 / 403 tanımları (`ServiceUnavailable`, iki kod) DL-1'de AC-16 ile
gelir, Web sahteleri 2xx dışı gövdeyi denetlemediği için AC-27 bu gövdeleri sözleşmenin `Error` biçiminde yazar. DL-2 DL-1'e dayanır.
FU01'in kapanışı ayrıca E5 smoke ister (§17; MOD-0149 DL-1 ve MVP-6 3.5.0 yayında). 7 dil metinleri parite bekçisi gereği DL-1'dedir (§11);
"Gösterim kuralı" iki dilime bölünür: DL-1 sunucu çıktıları (API, CSV, PDF), DL-2 ekran. Q-7 ve Q-8 r4'te, Q-9 r5'te kapandı (§19.4,
§19.5). **Künye `status: ready-for-dev` (r5) bu kapıları kapatmaz:** S-49 gereği onay satırı ready-for-dev künyeli sürüme yazılır; kod
CT-APR-02 yeniden inceleme (r5) ve CT-APR-01 onay satırından sonra yazılır, DL-1 yukarıdaki kapılar kapanmadan kabul edilmez.

## 23. Page Help (UXH-001)

Yeni sayfa yok. Vaka detayı "?" panelindeki müşteriler adımı (`Help.RECALLCASEDETAILS.Step4`, `PageHelpResource.{7}.resx`) üç cümle alır:
"Eski kayıt" / "CRM kimliği yok" rozetleri; CRM'e ulaşılamadığında ya da CRM izni eksikken ("İzin eksik") bildirim, tüketim beyanı ve iade
bağlamanın kapalı olduğu; müşteri adlarının 25'lik sayfalarla gösterildiği (DL-2; yeni tur yok). Kanıt ve iz ekranlarının yardım metni
değişmez (r5): izin eksikken dışa aktarım reddinin metni (`Recall.Errors.RECALL_CUSTOMER_DIRECTORY_FORBIDDEN`, iz ekranında
`Recall.Customer.ExportForbidden`) nedeni ve çözümü kendisi söyler.
