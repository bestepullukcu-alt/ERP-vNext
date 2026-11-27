---
id: MOD-0189
name: MRP & Replenishment
domain: supply-chain-execution
service: Diten.PlanningService
service_port: 5068
shell: tenant
golden_reference: none
entity_base: EntityBase
status: ready-for-dev
approval_status: pending
revision: "r2 (2026-11-25) — Demand v2 bağlı koşu (bound-run) sözleşmesi 1.0.0 FROZEN (execution/domains/supply-chain-execution/work-packs/WP-MOD0189-BOUND-RUN-CONTRACT.md; sahip kararları K-1…K-12, Beste, docs/records/decisions/2026-11/mod0188-demand-v2-field-names-owner-decisions-01.md; commit 66b165806) pakete işlendi: §4 bağ kaydı alanları, §11 ekran ve Gateway dilim önerisi (DL-R1, DL-E1, DL-E2 önerileri; X-1…X-3), §12 Demand revision bağı ve warehouseId UUID sınır kuralı, §13 bağlı koşu hata yolları ve A-1…A-3, §14 mrp.runs.create / mrp.runs.cancel ve mrp.runs.read düzeltmesi, §15 servis içi bağ okuma (K-7), §16 AC-BR01…AC-BR12, §17 testler ve sabotajlar, §18 r2 kapıları, §20 seçenek tablosu, §21 run denetim eylem adları, §22 Değişiklik günlüğü (yeni). r1 metni silinmedi; r1 cümlelerinin r2'de nasıl okunacağı r2 işaretli paragraflardadır. r1 (HEAD d4377ac56'daki metin, LF sha256 0551b33a…) — Ali onaylı dar dilim ve PR #160 takip dilimi (§18); revizyon etiketi r2 ile geriye dönük verildi."
status_note: "ready-for-dev yalnız r1 kapsamıdır (§18 dar dilim ve PR #160 takip dilimi). r2 eklemeleri taslaktır: bağımsız inceleme (CT-APR-02) ve sürüme bağlı onay satırı (CT-APR-01, onaylayıcı Beste) yok; r2 için kod izni yok."
owner: supply-chain-execution / control-tower (human owner TBD)
branch: feature/sce/mod-0189-mrp-core
started: TBD
target: TBD
---

# MOD-0189 — MRP & Replenishment

> DCP-002 kimlik kontrolü Blueprint 8.1 `Blueprint_Data` ve registry'de `MOD-0189 / MRP & Replenishment` için geçti. `ready-for-dev` §18'deki izole dilimi ve PR #160 icin onayli canli MDM/Published Demand/Inventory takip dilimini kapsar; tam modul kabulunu, dis aktarimi veya Gateway/UI isini kapsamaz. MVP-4 kapsamı MOD-0188, MOD-0189 ve MOD-0191'dir; MOD-0190/0192 MVP-6'ya aittir.

> **r2 (2026-11-25, taslak):** Bağlı koşu sözleşmesi `execution/domains/supply-chain-execution/work-packs/WP-MOD0189-BOUND-RUN-CONTRACT.md` 1.0.0 FROZEN (K-1…K-12) bu paketin Demand v2 tüketici sınırında alan adı, tip, durum, izin, denetim ve sayfa kaynağıdır. Sözleşme kod izni değildir; Demand v2 üretici API'si (`2.0.0-draft.1`) DRAFT kalır. r2 maddeleri bağımsız inceleme (CT-APR-02) ve onay satırı (CT-APR-01) olmadan kod kapısı açmaz.

## 1. Module Summary

Ali'nin servis kararı `Diten.PlanningService` (5068); ayrı servis açılmaz. MOD-0188 PlanningService main'dedir. §18'deki onayli takip diliminde canli MDM, Published Demand ve Inventory okumalari acilir; tam modul kabulu acik kalir.

MRP, onaylı talebi stok ve doğrulanmış tedarik girdilerine karşı netleyerek açıklanabilir net ihtiyaç ve ikmal önerisi üretir. Hedef, `MRP Monitor`, istisna kuyruğu ve kanıt paketidir. İlk sürüm SKU × Warehouse × ISO haftası ve lot-for-lot hesabıdır; MOQ/koli yuvarlaması ve sipariş tetikleyicisi açık kalır.

## 2. Ownership and Boundaries

MOD-0189 `MRPRun`, `NetRequirement`, `ReplenishmentProposal` ve MRP istisnalarının SoR'udur. Satın alma siparişi, satın alma talebi ve onay akışı MOD-0141'in; stok gerçeği MOD-0173'ün; BOM kompozisyonu MOD-0193'ün; brüt talep revision'ı MOD-0188'in; safety-stock politikası MOD-0191'in SoR'udur. MOD-0189 başka modülün veritabanına doğrudan erişmez ve PO oluşturmaz. Onaylı requirement için MOD-0141'e yalnız kararlaştırılmış handoff yapar.

## 3. Owned Objects

| Nesne | Taslak sorumluluk | Açık karar |
|---|---|---|
| MRPRun | Seçilmiş dönem, girdi revision/sürüm referansları, durum, kapsam, başlatan ve kanıt. | Run yaşam döngüsü, durdurma ve tekrar anahtarı. |
| NetRequirement | SKU × Warehouse × zaman kovası için brüt ihtiyaç, uygun stok, açık tedarik, safety stock ve net sonuç izleri. | Netting sırası, birim, kovalar ve kısmi verinin anlamı. |
| ReplenishmentProposal | Net ihtiyaçtan türeyen idempotent, gerekçeli öneri; PO değil. | Lot/lead-time, onay ve MOD-0141 handoff alındısı. |
| MRPException | Eksik, çelişkili, geçersiz veya güncelliğini yitirmiş girdinin görünür kaydı. | Şiddet, çözüm ve run'a etkisi. |
| PlanningLeadTime | SKU × Warehouse için gün cinsinden tarihli ve sürümlü planlama parametresi; planlamacı hazırlar, bağımsız kişi onaylar. | Kalıcı yazma/audit yolu ve gelecekteki yetkili tedarikçi kaydına geçiş sözleşmesi. |

## 4. Entity Fields

`TenantId`, `LegalEntityId`, `IsDeleted`, `DeletedAt`, oluşturma/güncelleme izi ve teknik sürüm repo standardına göre zorunludur. İş alanları adaydır: `runId`, `planningPeriodKey`, `demandRevisionId`, `demandStateVersion`, `inventoryAsOf`, `bomVersionId`, `sourceVersion`, `skuId`, `warehouseId`, `baseUomId`, `grossQuantity`, `availableQuantity`, `openSupplyQuantity`, `safetyStockQuantity`, `netQuantity`, `proposalId`, `idempotencyKey`, `evidenceChecksum`. Lead time parametresinde `leadTimeDays`, geçerlilik aralığı, policy revision, hazırlayan, bağımsız onaylayan ve onay izi gerekir. Alan adları ve kalıcı şema ayrıca incelenir.

**r2 — bağ kaydı (bound-run 1.0.0 §2, FROZEN):** Run–talep bağının iş alanları artık aday değildir; adı ve tipi sözleşmeyle sabittir: `runId`, `tenantId`, `legalEntityId`, `planningCycleId` (UUID); `planningPeriodKey` (`yyyy-MM-dd`); `demandRevisionId` (UUID; üreticide `revisionId`); `demandStateVersion` (64 bit tamsayı; üreticide `stateVersion`); `demandContentVersion` (K-2); `demandIntegrity { definitionId, digest }` (K-1; `definitionId` ∈ `MOD0188-CANONICAL-1/SHA-256`, `MOD0188-CANONICAL-2/SHA-256`); `demandExpectedRowCount` (K-2); `skuId` ve `warehouseId` (ikisi de UUID; K-3); `baseUomId`; `boundAt`, `startedAt` (UTC, `Utc` eki yok; K-4); `boundBy`, `bindingAuditId` (UUID; ad / e-posta yok; K-5); `runState`, `runVersion`, `stopReason` (K-6); `idempotencyKey`, `requestHash` (K-11). r1 aday listesindeki `demandRevisionId`, `demandStateVersion`, `skuId`, `warehouseId`, `baseUomId` ve `idempotencyKey` bu adlarla aynıdır; `warehouseId`'nin tipi UUID olarak kesinleşti. `demandChecksum`, `boundAtUtc` ve `startedAtUtc` adları kullanılmaz. Bağ kaydı dışındaki run kanıt ve öneri alanları (`inventoryAsOf`, `leadTimeRevisionId`, `leadTimeDays`, `safetyPolicyRevisionId`, `bomVersionId`, `sourceVersion`, `evidenceChecksum`, `proposalId`) ve `NetRequirement` miktar alanları r1'deki gibi adaydır; kalıcı şemaları ayrıca incelenir. Ölçülen başlangıç (HEAD `d4377ac56`): kodda kalıcı run kaydı yoktur; `MrpPublishedDemand` (`MrpLiveSources.cs`) özeti yalnız metin olarak taşır, `definitionId` eklenmesi MOD-0189 kod dilimindedir (K-1). Açık soru (Beste): `runVersion` `EntityBase`'in teknik `Version` alanıyla aynı alan mı, ayrı iş alanı mı? MOD-0188 emsali ayrı alandır (`stateVersion` ≠ `Version`).

## 5. Repo Scope

Bu dilimin yazma kapsamı bu pack, main'e merge edilmiş `services/Diten.PlanningService/` içindeki MOD-0189 `Features/Mrp` kodu ve testleri, PlanningService API bagimlilik/izin/manifest kayitlarindaki yalniz MOD-0189 satirlari, kendi servisinin audit defterindeki MOD-0189 satirlari ve `tests/architecture/TenantArchitecture.ArchitectureTests/AuditTrailStandardTests.cs` icindeki yalniz MOD-0189 c-yolu izi sabitlemesidir. Servis portu 5068'dir. MOD-0188'in mevcut Published okuma tipleri degistirilmez; ayni servis icinde butunlugu dogrulanmis snapshot okumasindan yararlanilir. Gateway, UI ve merkezî contract degisiklikleri ayri sahiplik ve onay gerektirir.

## 6. Protected Paths

`AGENTS.md`, `.antigravity/**`, domain config, DCP-009, Gateway `ocelot.json`, Platform/MDM/Auth, MOD-0188/MOD-0191 servisleri, MOD-0190/0192 ve MVP-3 kodu bu pack'in yazma kapsamı dışındadır. FROZEN sözleşme dosyaları tüketici tarafından değiştirilmez.

## 7. Dependencies

| Girdi / sahip | Bugünkü sözleşme kanıtı | MRP için eksik karar veya sınır |
|---|---|---|
| Talep / MOD-0188 | PlanningService main'de; owned Demand v2 revision/status sözleşmesi DRAFT. Demand v1 FROZEN ama bu akışın girdisi değil. | Takip dilimi ayni servis icindeki butunlugu dogrulanmis guncel Published snapshot'i okur; v1'e sessiz dusus yoktur. DRAFT v2'nin dis servis tuketimi dondurulana kadar kapali; dort yetkili durum kontrolu ve `Invalidated` uzlastirmasi tam modul icin acik. |
| Stok / MOD-0173 | `inventory-bundle.openapi.yaml` v1.2.1 FROZEN, `GET /availability`; `available`, `reserved`, `byStatus`, `baseUomId`, `asOf`. | Anlık görünüm gelecekteki planlanmış giriş veya tarihsel stok yokluğu kanıtı değildir. Kova/asOf tutarlılığı ve kalite/bloke stok kuralı kararlaştırılmalı. |
| Ürün / MOD-0290 | `product-master-bundle.v1.0.openapi.yaml` FROZEN; v1.1 dosyası DRAFT. | Item/SKU ve temel UoM dönüşümü yalnız onaylı sürümden; taslak alanlara canlı bağımlılık yok. |
| Açık PO / MOD-0141 | Main ProcurementService `requisition-po.openapi.yaml` v1.1.0 FROZEN list/get PO; `poId`, `poLineId`, durum, satır miktarı ve UoM. `needBy` yalnız requisition satırında; runtime `PoLine` da teslim tarihi veya açık bakiye tutmaz. | Üreticiden sürümlü açık satır görünümü (kalan miktar, beklenen teslim tarihi, asOf) istenir. Tüketicide `PO - GRN` hesaplanmaz. Görünüm gelene kadar açık sipariş terimi uygulanmaz; her plan görünür `DEGRADED: açık siparişler hesaba katılmadı` işareti taşır. |
| Mal kabul / MOD-0142 | `grn-event.openapi.yaml` v1.0.0 FROZEN; GRN, `poId/poLineId` ve alınan miktarı taşır, stok hareketini MOD-0173'e post eder. | PO açık bakiyesinin yetkili hesabı ve çift sayımı önleyen ilişki ayrıca kararlaştırılmalı; GRN tek başına gelecekteki tedarik değildir. |
| BOM / MOD-0193 | `bom.openapi.yaml` v1.2.0 FROZEN, `current` / `explode` ve `BomVersionEffective` / `BomVersionSuperseded` olayları. | Olay yalnız kontrol işaretidir; güncel/yürürlükteki BOM ve patlatma her zaman BOM servisinden ayrıca doğrulanır. Çok seviyeli patlatma, alternatif, fire/verim, geçerlilik tarihi, döngü ve sürüm sabitleme kuralları açık. Manufacturing yolu dışında BOM zorunlu değil. |
| Safety stock / MOD-0191 | DRAFT v1.1 yürürlükteki onaylı miktar okuması; her ISO haftası başlangıcında `asOf` ile sorgulanır. | Her hafta ayrı miktar, politika revizyonu ve kayıt sürümü taşınır. Kayıtsız hafta yalnız o hafta için safety-stock DEGRADED olur; servis/yetki hatası tüm hesabı kapatır. |

## 8. Runtime Constraints

Tenant × LegalEntity ve SKU × Warehouse kapsamı sunucuda doğrulanır. Geçici şirket kuralı tenant'ın MDM'de aktif şirketidir; MDM doğrulaması fail-closed çalışır, kullanıcı-şirket pozisyon yetkisi merkezi "yetkili şirketlerim" ucu gelene kadar açık maddedir. Claim veya form tek başına kapsam kanıtı değildir. Her run girdilerinin sürüm/asOf bağını korur; eksik veya bozuk girdi sıfıra çevrilmez. Açık PO görünümü yokluğu ayrıca DEGRADED olarak kaydedilir, gerçek sıfır tedarik diye sunulmaz. Her sorgu tenant filtresi ve soft-delete kuralı taşır. Kısmi snapshot'tan güvenilir öneri üretilmez. Repo kuralları beş katman, CQRS, Mongo, Response<T> ve dört pipeline behavior için geçerlidir; servis 5068 PlanningService olarak seçilmiştir, bu canlı tüketim izni değildir.

## 9. Layout & Shell Contract

İleride tenant çalışma alanı `_LayoutTenantShell.cshtml` kullanır; yetkisiz kullanıcıya sayfa iskeleti çizilmez. Bu taslak ekran açmaz. MRP Monitor/Exception Queue/Evidence Pack için form alan sayısı ve DataTable gereksinimi ayrıca belirlenecektir; `golden_reference: none` yalnız bugün UI sözleşmesinin kararsızlığını belirtir.

## 10. Backend File Convention

Servis kararı sonrası feature klasörleri Command, Query, Handler ve Validator için ayrı tutulur. MOD-0188 veya MOD-0141 iç modelini referans almak yerine yayımlanmış HTTP/olay sözleşmelerine uygun tüketici adaptörleri kullanılır. İdempotency, sürüm ve kanıt doğrulaması katman sınırlarında test edilir. Bu bölüm dosya oluşturma izni değildir.

## 11. Frontend File Contract

UI ve Gateway ayrı onay konusudur. Frontend doğrudan servis portuna istek atmaz; rota yalnız integration-agent tarafından açılır. Yedi tenant dili (en, tr, fr, es, zh, ar, ru), erişilebilir yetkisiz yüzey ve varsa DataTable v2 profili ekran kararıyla birlikte tanımlanır.

**r2 — ekran ve Gateway dilim önerisi (öneri; sahip kararı değil):** Ölçüm (HEAD `d4377ac56`): MOD-0189'un HTTP uçları PlanningService'te `api/v1/mrp/calculations` (GET, `mrp.runs.read`) ve `api/v1/mrp/lead-times` (dört POST komutu) olarak vardır (`services/Diten.PlanningService/src/Diten.PlanningService.Api/Features/Mrp/MrpCalculationController.cs:9`, `services/Diten.PlanningService/src/Diten.PlanningService.Api/Features/Mrp/LeadTimeController.cs:18`). `gateway/Diten.ApiGateway/ocelot.json` içinde `mrp` geçen rota yoktur; `frontend/Diten.Web` altında MRP görünümü, denetleyicisi ya da JS dosyası yoktur. Bu yüzden r1'in onaylı lead time komutlarını planlamacı ve bağımsız onaylayıcı bugün yalnız doğrudan API ile kullanabilir; r1 ekran açmadı (§9).

| Öneri | Kapsam | Sahip | Ön koşul / bağımlılık |
|---|---|---|---|
| DL-R1 (öneri) | Bound-run kod dilimi: kalıcı run + bağ + işlem-içi denetim, `runState` geçişleri, idempotency, `mrp.runs.create` / `mrp.runs.cancel` kaydı, `mrp.runs.read` açıklaması, servis içi tarihsel okuma (AC-BR01…AC-BR12) | MOD-0189 (MOD-0188 üretici tarafı: MOD-0188 §15, §17 r2) | r2 onayı (CT-APR-02, CT-APR-01); A-1…A-4 teknik tasarım; A-8 gerçek Mongo testleri |
| DL-E1 (öneri) | MRP Monitor: run listesi ve run ayrıntısı (bağ alanları, `runState`, `stopReason`, DEGRADED nedenleri, kanıt özeti); run başlatma (`mrp.runs.create`) ve gerekçeli iptal (`mrp.runs.cancel`) | MOD-0189 | DL-R1 ve kalıcı run okuma ucu; mockup brief; `golden_reference` / `data_mode` / `form_field_count` kararı; X-1, X-3 |
| DL-E2 (öneri) | Lead time çalışma alanı: taslak, yeni sürüm, incelemeye gönderme, bağımsız onay / gerekçeli ret (r1'in onaylı dört komutu) | MOD-0189 | Mockup brief; Slim / Compact alan sayımı; bugün lead time liste / okuma ucu yok, eklenmesi ayrı karar; X-1, X-3 |
| X-1 | Gateway rotası: upstream `/api/v1/mrp` ve `/api/v1/mrp/{everything}` → downstream PlanningService (5068) aynı yollar; yöntemler `GET`, `POST`, `OPTIONS`; Bearer | integration-agent | DL-E1 / DL-E2 kabulü; `ocelot.json`'u yalnız integration-agent değiştirir. Bağ okuma için rota açılmaz (K-7, §15 r2) |
| X-2 | PKS-001 §4 tablosuna `mrp.*` → `Diten.PlanningService` / MOD-0189 satırı (`demand.*` → MOD-0188 için de) | `.antigravity` sahibi (kullanıcı onayı) | DL-R1 kabulü; bugün §4'te iki ad alanı da yok (ölçüldü) |
| X-3 | Tenant navigasyonu, sayfa / izin manifesti ve 7 dilde nav anahtarları | integration sahibi + l10n ajanı | DL-E1 / DL-E2 |

Rol izin köprüsü: `create` ve `cancel` fiilleri `perm-label.js` ailelerinde ve RoleAssignments `_IndexL10n` sözlüğünde vardır (`frontend/Diten.Web/wwwroot/assets/js/Governance/RoleAssignments/perm-label.js:51`, `frontend/Diten.Web/wwwroot/assets/js/Governance/RoleAssignments/perm-label.js:55`, `frontend/Diten.Web/Views/Governance/RoleAssignments/_IndexL10n.cshtml:167`, `frontend/Diten.Web/Views/Governance/RoleAssignments/_IndexL10n.cshtml:196`); yeni fiil köprüsü gerekmez. Dilim adları öneridir: bu pakette `Teslim dilimleri` tablosu yoktur; tablonun eklenip eklenmeyeceği ve numaralandırma açık soru (Beste).

## 12. Validation Rules

Ali kararı: her SKU × Warehouse × ISO haftası için girdi sürüm/asOf kesiti kaydedilir. Hesap izi
`netRequirement = max(0, grossDemand + approvedSafetyStock - availableStock - openOrderQuantity)` biçimindedir.
Stok anlık görüntüsü yayımlanmış talebin ilk haftasından sonraysa MRP, görüntünün UTC tarihini
içeren ISO haftasından başlar; önceki haftalar hesaplanmaz. Görüntü tüm 52 haftalık ufkun
sonrasındaysa plan üretilmez. Talep revision kimliği ve checksum'ı değişmeden korunur.
Güvenlik stoğu yalnız MOD-0191'in onaylı politika sürümünden gelir. İlk sürüm lot-for-lot'tur;
MOQ/koli yuvarlaması yapılmaz. MOD-0141 açık satır görünümü yokken `openOrderQuantity` güvenilir
bir sıfır olarak yazılmaz: terim hesaptan çıkarılır ve her plan
`DEGRADED: açık siparişler hesaba katılmadı` işaretini taşır. Haftalık tahmini fazla stok sonraki
ISO haftasına devreder; negatif öneri üretilmez. Lead time, MOD-0189'un SKU × Warehouse için gün
cinsinden tarihli ve sürümlü planlama parametresidir; planlamacı girer, hazırlayandan bağımsız
inceleyen onaylar. Sourcing teklifindeki nullable `leadTimeDays` yetkili kaynak değildir.
İleride satın alma tarafında yetkili tedarikçi × SKU kaydı gelirse tercih sırası ayrıca onaylanır.
Kova içi tarih tahsisi ve önerinin gerçekleşmiş tedarik sayılacağı an teknik tasarımda açık tutulur.

Ali'nin haftalık safety-stock kararı: MOD-0191 v1.1 yürürlükteki onaylı miktar ucu
her ISO haftasının başlangıç tarihinde okunur. Hafta sonucu miktar, temel birim,
politika revizyonu ve kayıt sürümünü ayrı taşır; tek plan-geneli revizyon yetkili
kanıt sayılmaz. Kayıt bulunmayan hafta DEGRADED işaretlenir ve onaylı sıfır gibi
sunulmaz. Üreticiye erişilemiyorsa veya birim uyuşmuyorsa plan üretilmez. Açık PO
görünümü gelene kadar her planın açık sipariş DEGRADED nedeni ayrıca sürer.

| Alan / kanıt | Önerilen doğrulama | Açık ürün kararı |
|---|---|---|
| Tenant/LegalEntity | Sunucu kaynaklı yetkili kapsam; her girdi aynı kapsamda. | Platform sözleşmesi ve iptal testi. |
| Demand revision | Aynı `revisionId`, yetkili durum ve checksum; kısmi sayfa yok. | Dört kontrol noktası ve tarihsel bağ şeması. |
| Item/SKU/UoM/Warehouse | FROZEN üreticilerde mevcut ve aynı ölçü biriminde. | Dönüşüm, boyut ve lokasyon kapsamı. |
| Inventory/PO/BOM asOf | Run sınırında sürümlü, tekrarlanabilir kesit. | Gecikme toleransı ve MOD-0141 açık satır görünümü. |
| Miktar ve zaman | Decimal, ISO haftası, lot-for-lot, `max(0, …)` ve fazla stok devri. | Kova içi tarih tahsisi ve ileride MOQ/koli yuvarlaması. |

**r2 — Demand revision bağı (bound-run 1.0.0):** Yukarıdaki "Demand revision" satırı ve "Talep revision kimliği ve checksum'ı değişmeden korunur" cümlesi r2'de bağ kaydı alanlarıyla okunur: `demandRevisionId`, `demandStateVersion`, `demandContentVersion`, `demandIntegrity { definitionId, digest }` ve `demandExpectedRowCount`.

| Alan / kanıt (r2) | Kural | Hata davranışı |
|---|---|---|
| Kaynak durum | Bağ yalnız `Published` revizyona kurulur; `Superseded` / `Invalidated` revizyona yeni bağ yok | Ret, sıfır yazım |
| `demandIntegrity` | `definitionId` iki kanonik tanımdan biri; `digest` 64 büyük harf onaltılık; özet üreticinin kanonik tanımı ve yazdığı baytlarla doğrulanır | Uyuşmazlıkta bağ yok; bağlı run `Stopped` (`IntegrityFailed`) |
| `demandExpectedRowCount` | Tam anlık görüntü ve tarihsel sayfaların toplamı bu sayıyla tutmalı | Kısmi sayfa / kısmi hesap yok |
| `warehouseId` (K-3) | Üreticinin metin değeri sınırda UUID'ye çözülür; eşleştirme UUID değeriyle yapılır | Çözülemezse kapalı hata: bağ kurulmaz, sayfa verilmez |
| `idempotencyKey` + `requestHash` (K-11) | Tenant × LegalEntity içinde aynı anahtar + aynı özet aynı run'ı döndürür | Aynı anahtar + farklı özet: çatışma, yazım yok; alan kümesi A-4 |
| `stopReason` | Yalnız `Stopped` durumunda zorunlu: `DemandInvalidated`, `ScopeRevoked`, `IntegrityFailed`, `Cancelled` | `Cancelled` gerekçesiz reddedilir |

Hata gövdesi kodları ve HTTP durumları (çatışma için 409 mu, durum kaynağı kesintisinde 503 mü) sözleşmede yazılı değildir; açık soru (Beste).

## 13. Failure Path to Verify

Yanlış tenant/şirket/seri için kayıt veya run görünmez; eksik işlem izni reddedilir. Yetki, Demand durumu, stok asOf veya BOM sürümü doğrulanamıyorsa run/proposal güvenilir ilan edilmez. Açık PO görünümü yoksa kullanıcıdan gizlenmeyen DEGRADED plan hesaplanır, incelenir ve öneri üretebilir; açık tedarik sıfır diye doğrulanmış sayılmaz. MOD-0141'e otomatik aktarım yasaktır. Bağımsız onaylayıcı yalnız "açık siparişler kontrol edildi" gerekçesini yazarak manuel aktarabilir; gerekçe zorunlu audit kaydında tutulur. Görünüm varken bozuk veya çelişkili bakiye sessizce DEGRADED'a düşmez, kapalı hata/istisna üretir. Aynı istek aynı girdilerle tek run/öneri etkisi üretir; aynı anahtar farklı içerikle çatışır. Audit veya zorunlu kanıt başarısızlığı yarım sonuç bırakmaz. Olay kaybında düzenli yetkili durum uzlaştırması gerekir; aralık ve kullanıcıya görünen askı durumu açık karardır.

**r2 — bağlı koşu hata yolları:** r1'in "aralık ve kullanıcıya görünen askı durumu" maddesi bound-run 1.0.0'ın A-1 (uzlaştırma sıklığı), A-2 (`Suspended` azami bekleme) ve A-3 (`ResultUntrusted` görünürlük süresi) açık noktalarına bağlanır; sayılar teknik tasarımda ölçülerek gelir, varsayılmaz (K-12). Doğrulanacak ek yollar: (1) bağ, ilk run ve denetim tek Mongo işlemidir; denetim yazılamazsa run ve bağ yoktur; (2) durum okunamazsa `Running → Suspended`; yeniden okunduğunda yalnız aynı `demandRevisionId`, uygun `demandStateVersion` ve aynı özetle `Running`; (3) `Invalidated` devam eden run'ı `Stopped` (`DemandInvalidated`), tamamlanmış run'ı `ResultUntrusted` yapar, uygulanmamış öneriler bloklanır; (4) kapsam iptali `Stopped` (`ScopeRevoked`); (5) özet bozulması `Stopped` (`IntegrityFailed`); (6) uç durumdan (`Stopped`, `ResultUntrusted`) geçiş reddedilir; (7) başka run'ın, imzası bozuk ya da 30 dakikası dolmuş cursor reddedilir; (8) `Superseded` tek başına run'ı durdurmaz; (9) UUID'ye çözülemeyen `warehouseId` kapalı hatadır.

## 14. Authorization Convention

Taslak izin ayrımı: `mrp.runs.read`, `mrp.runs.create`, `mrp.proposals.read`, `mrp.proposals.approve`, `mrp.evidence.read`. Bunlar kayıtlı runtime izinleri değildir. Hazırlayan/onaylayan ayrımı, Platform aktör–LegalEntity ataması, SKU × Warehouse kapsamı ve iptal semantiği Security/CT onayı ister. MDM yalnız referans doğrular.

Ali'nin onayladığı dar lead-time komut izinleri: `mrp.lead-times.create` (ilk taslak),
`mrp.lead-times.update` (yeni sürüm), `mrp.lead-times.submit` (incelemeye gönderme) ve
`mrp.lead-times.review` (bağımsız onay veya gerekçeli ret). Her komut Tenant × aktif
LegalEntity × SKU × Warehouse kapsamında sunucuda doğrulanır; MDM referans doğrulaması
erişilemezse işlem kapalı kalır. MDM doğrulaması kullanıcının o şirkete yetkisi olduğunun
kanıtı değildir; Platform `yetkili şirketlerim` bağı merkezi ekip gelene kadar açıktır.

**r2 — izinler (bound-run 1.0.0 §4, K-8):** `mrp.runs.create` (kalıcı run + bağ oluşturma) ve `mrp.runs.cancel` (kullanıcı iptali, `stopReason` = `Cancelled`, gerekçe zorunlu) yeni anahtarlardır; runtime kaydı DL-R1 kod dilimindedir (§11 r2). Yukarıdaki "Bunlar kayıtlı runtime izinleri değildir" cümlesi `mrp.runs.read` için ölçümle düzeltilir: anahtar bugün kayıtlıdır (`services/Diten.PlanningService/src/Diten.PlanningService.Api/ModuleRegistration/MrpManifestProvider.cs:24`, `services/Diten.PlanningService/src/Diten.PlanningService.Api/Features/DemandPlanning/DemandPlanningServiceCollectionExtensions.cs:36`) ve anlık hesap ucunu korur (`services/Diten.PlanningService/src/Diten.PlanningService.Api/Features/Mrp/MrpCalculationController.cs:13`). Açıklaması bugün "Calculate scoped MRP plan"dır; DL-R1'de "View MRP runs" anlamına düzeltilir. `mrp.proposals.read`, `mrp.proposals.approve` ve `mrp.evidence.read` taslak kalır, kayıtlı değildir ve bu sözleşmenin konusu değildir. Tarihsel talep okuması için yeni izin yoktur: `demand.plans.consume` + sunucunun okuduğu kalıcı bağ. `Running → Suspended`, `Suspended → Running`, `Running → Completed`, sistem nedenli `Stopped` ve `ResultUntrusted` sistem geçişleridir, izin istemez. Yetkisiz kullanıcı 403, kapsam dışı run boş 404. Açık soru (Beste): kalıcı run gelince anlık hesap ucu `GET /api/v1/mrp/calculations` `mrp.runs.read` altında mı kalır, kaldırılır mı, yoksa `mrp.runs.create`'e mi taşınır?

## 15. Gateway / API Routing Decision

Servis `Diten.PlanningService`, port 5068 olarak kararlaştırıldı; yeni servis açılmaz. `PLAN-MRP-BUNDLE` producer sözleşmesi, Gateway rotası ve merkezî kayıt ayrı review/freeze gerektirir. PR #160 takip diliminde servis portunda dogrudan HTTP ile MDM aktif sirket ve Inventory availability okunur; Demand ayni servisin dogrulanmis Published snapshot'idir. Demand v2 DRAFT dis tuketimi ve MOD-0141 aktarimi acilmaz.

**r2 — bağ okuma servis içi (K-7):** Run–revizyon bağı ve `Superseded` tarihsel okuma PlanningService içindeki sunucu arayüzüdür; bunun için HTTP ucu ve Gateway rotası açılmaz, `ocelot.json` ve Gateway testleri bu nedenle değişmez, integration-agent işi doğmaz. Ölçüm: Gateway'de `/api/v2/demand/{everything}` → PlanningService genel rotası vardır (`gateway/Diten.ApiGateway/ocelot.json:11-17`); `/api/v2/demand` altına eklenecek her HTTP ucu kendiliğinden dışarı açılır. Bu yüzden bağlı tarihsel okuma HTTP ucu olarak eklenmez; AC-BR10 bunu test eder. MRP'nin kendi kullanıcı uçları için rota önerisi §11 r2 X-1'dedir ve bağ okumasını kapsamaz. Bound-run sözleşmesi OpenAPI değildir; `PLAN-MRP-BUNDLE` üretici sözleşmesi ayrı kalır.

## 16. Acceptance Criteria

- [ ] MRP run aynı onaylı Demand revision ve aynı sürümlü stok/PO/BOM girdileriyle yeniden hesaplandığında aynı net ihtiyaç ve kanıt özetini verir.
- [ ] Manufacturing yolunda effective BOM sürümü sabitlenir; component ihtiyaçları belgelenmiş netting kuralına göre hesaplanır. BOM olmayan ticari yolda patlatma yapılmaz.
- [ ] Uygun stok ve doğrulanmış açık tedarik çift sayılmadan, safety stock/lead-time kuralıyla açıklanabilir net ihtiyaç üretilir.
- [ ] Açık PO görünümü yokken her plan DEGRADED işaretlidir; açık tedarik sıfır diye sertifikalanmaz. Görünüm varken bozuk veri ayrı kapalı hatadır.
- [ ] DEGRADED plan incelenir ve öneri üretir; otomatik MOD-0141 devri reddedilir. Bağımsız onaylayıcının gerekçeli manuel devri audit kaydıyla birlikte doğrulanır.
- [ ] Tarihli/sürümlü SKU × Warehouse lead time parametresini hazırlayan kendi revision'ını onaylayamaz; eski/çakışan sürüm reddedilir.
- [ ] Fazla tahmini stok sonraki ISO haftasına devreder; net ihtiyaç ve öneri negatif olmaz.
- [ ] Aynı girdi ve idempotency anahtarı ikinci run/öneri oluşturmaz; farklı içerikli tekrar çatışır.
- [ ] Sadece onaylı requirement, MOD-0141'e sözleşmeli handoff alındısıyla iletilir; MOD-0189 PO oluşturmaz.
- [ ] Demand `Superseded` veya `Invalidated`, kayıp/geç olay ve durum sorgusu kesintisi hallerinde kararlaştırılmış dört tüketici kontrolü ve uzlaştırma güvenli sonuç verir.
- [ ] Tenant/LegalEntity/SKU × Warehouse izolasyonu, izin, görev ayrılığı, audit arızası ve yeniden başlatma gerçek kaynakla test edilir.

Bu maddeler önerilen kabul kapsamıdır; Ali/CT incelemesi olmadan tamamlanmış sayılmaz.

**r2 — bağlı koşu kabul ölçütleri (bound-run 1.0.0; taslak, r2 onaylanmadan kabul kapısı değildir):**

- [ ] **AC-BR01** Kalıcı bağ kaydı sözleşme §2'deki alanların tamamını adı ve tipiyle taşır (`warehouseId` UUID, `demandIntegrity { definitionId, digest }`, `boundAt` / `startedAt` UTC); `demandChecksum`, `boundAtUtc`, `startedAtUtc` adında alan yoktur; bağ alanları ve `demandRevisionId` oluşturulduktan sonra hiçbir komut ya da geçişle değişmez.
- [ ] **AC-BR02** Bağ, ilk run yazısı ve `RunCreated` denetim kaydı (`auditId` = `bindingAuditId`) tek Mongo işleminde yazılır; denetim yazımı başarısızsa run, bağ ve iz yoktur.
- [ ] **AC-BR03** Bağ yalnız `Published`, `demandIntegrity`'si doğrulanmış ve satır sayısı `demandExpectedRowCount` ile tutan revizyona kurulur; `Superseded` / `Invalidated` revizyona, bilinmeyen `definitionId`'ye ya da özet uyuşmazlığına yeni bağ sıfır yazımla reddedilir.
- [ ] **AC-BR04** Üreticinin `warehouseId` metni UUID'ye çözülemezse bağ kurulmaz ve tarihsel sayfa verilmez; eşleştirme UUID değeriyle yapılır (aynı UUID'nin farklı harf büyüklüğündeki metni eşleşir); özet doğrulamasında depo kimliği yeniden biçimlendirilmez.
- [ ] **AC-BR05** `runState` yalnız sözleşme §3 geçiş tablosundaki geçişleri yapar; her geçiş `runVersion`'ı tam bir artırır ve tek denetim kaydı yazar (`RunSuspended`, `RunResumed`, `RunCompleted`, `RunStopped`, `RunResultUntrusted`); uç durumlardan (`Stopped`, `ResultUntrusted`) geçiş reddedilir.
- [ ] **AC-BR06** Kullanıcı iptali yalnız `mrp.runs.cancel` ve zorunlu gerekçeyle `Running` / `Suspended` → `Stopped` (`Cancelled`) yapar; izinsiz istek 403, kapsam dışı run 404, gerekçesiz istek ret, üçünde de sıfır geçiş; sistem geçişleri izin istemez.
- [ ] **AC-BR07** Run oluşturma `mrp.runs.create` ister (yoksa 403). Aynı Tenant × LegalEntity içinde aynı `idempotencyKey` + aynı `requestHash` ilk run'ı döndürür, ikinci run / bağ / denetim kaydı oluşmaz; aynı anahtar + farklı `requestHash` çatışmadır ve yazım yoktur.
- [ ] **AC-BR08** `Superseded` tarihsel okuma yalnız `demand.plans.consume` + sunucunun okuduğu kalıcı bağla ve yalnız run'ın kendi serisi (`skuId` × `warehouseId`) için verilir; satır sayfası en çok 500 (varsayılan 200), denetim sayfası en çok 200 (varsayılan 100), cursor ömrü 30 dk; cursor `runId`'ye bağlıdır ve başka run'da reddedilir; satır toplamı `demandExpectedRowCount` ile tutmazsa kısmi sayfa yoktur.
- [ ] **AC-BR09** Dört kontrol noktasında yetkili durum okunamazsa run `Suspended` olur; yeniden okunduğunda yalnız aynı revizyon, uygun `demandStateVersion` ve aynı özetle `Running`'e döner; `Invalidated` devam eden run'ı `Stopped` (`DemandInvalidated`), tamamlanmış run'ı `ResultUntrusted` yapar ve uygulanmamış öneriler bloklanır; `Superseded` run'ı durdurmaz. Kayıp, geç ya da yinelenmiş olay kararı değiştirmez; uzlaştırma yetkili durum sorgusundan karar verir.
- [ ] **AC-BR10** Bağ okuma için HTTP ucu ve Gateway rotası yoktur (K-7): Gateway `/api/v2/demand/{everything}` üzerinden `runId` ya da başka bir parametreyle `Superseded` içerik alınamaz; `ocelot.json` bu dilimde değişmez.
- [ ] **AC-BR11** Denetim kaydı sözleşme §5 alanlarını taşır (`actorId` UUID, `correlationId`, `runVersionBefore` / `runVersionAfter`, bağ kanıtı); talep satırı, miktar, kişi adı ya da e-posta yazılmaz.
- [ ] **AC-BR12** `mrp.runs.create` ve `mrp.runs.cancel` manifestte ve runtime izin listesinde kayıtlıdır; `mrp.runs.read` açıklaması "View MRP runs"tır; her run ucu `[HasPermission]` taşır.

r1'in "Demand `Superseded` veya `Invalidated` …" maddesi AC-BR03, AC-BR08 ve AC-BR09 ile ölçülür; metni değişmez. Ekran (DL-E1 / DL-E2) kabul ölçütleri mockup brief ve alan sayımından sonra yazılır; bu revizyonda yoktur.

## 17. Test Expectations

Karar sonrası saf netting için sınır/decimal/UoM ve eksik-veri testleri; FROZEN sözleşmelere karşı mock/fixture testleri; idempotency ve yarış için geçici loopback Mongo replica set testleri; tenant/şirket/seri izolasyonu; JWT/HTTP izin hattı; audit ve durum-kaybı sabotajı; yedi dil ve ekran testi gerekir. 27017–27021 test için kullanılmaz. CI başarısı ve canlı üretici kanıtı ayrı tutulur. İzole lead time yazımı için gerçek Mongo'da işlem-içi audit sabotajı gerekir; fixture başarısı canlı üretici kanıtı değildir.

**r2 — bağlı koşu testleri:** Her AC-BR için ayrı test; kritik korumada sabotaj kanıtı (koruma kaldırılır → test kırmızı, geri konur → yeşil). AC-BR01: alan adı / tip yansıma testi ve bağ alanını değiştirmeye çalışan geçişin reddi. AC-BR02: denetim yazımı sabotajında run ve bağ sayısı 0. AC-BR03: `Superseded` ve `Invalidated` bağ denemesi, bilinmeyen `definitionId`, tek baytı bozuk özet, eksik satır. AC-BR04: çözülemeyen ve farklı harf büyüklüğündeki `warehouseId`. AC-BR05: her geçişte `runVersion` +1 ve tek iz; uç durumdan geçiş negatif testi. AC-BR06: izinsiz, gerekçesiz ve kapsam dışı iptal. AC-BR07: aynı anahtar aynı özet, aynı anahtar farklı özet, eşzamanlı iki aynı istek. AC-BR08: 501 satırlık sayfa isteği, başka run'ın cursor'u, sabit saatle 30 dakikası dolmuş cursor, seri dışı okuma. AC-BR09: durum servisi kesintisi, yeniden okuma, `Invalidated` sonrası uzlaştırma, kayıp / yinelenmiş / sırası değişmiş olay. AC-BR10: Gateway üzerinden `/api/v2/demand/...` ile `Superseded` içerik denemesinin içerik vermemesi (HTTP / Gateway testi) ve `ocelot.json` farksızlığı. AC-BR11: denetim belgesinde satır, miktar ve ad alanı olmaması. AC-BR12: manifest ve runtime izin listesi yansıma testi. A-8 gereği Mongo yarış / rollback, yeniden başlatma, kapsam iptali, özet bozulması, olay kaybı ve `runId`–`demandRevisionId` karışması gerçek geçici Mongo replica set ile test edilmeden `Superseded` içerik okuması açılmaz; 27017–27021 kullanılmaz. MOD-0188'in üretici testleri MOD-0188 §17 r2'dedir; ortak test iki tarafın ayrı kanıtıdır.

## 18. Ready-for-dev Checklist

- [x] Ali servis ve portu `Diten.PlanningService` / 5068 olarak seçti; ayrı servis yok.
- [x] MOD-0188 PlanningService main'e merge edildi; yalnız aşağıdaki dar MOD-0189 kod kapısı açıldı.
- [x] İzole lead time parametresi PlanningService'te MOD-0189 sahipliğinde kalır; başka modülün iç deposuna yazılmaz.
- [x] Ali, SKU × Warehouse × ISO hafta netting formülü, lot-for-lot, onaylı MOD-0191 policy ve BOM olayının kontrol işareti olmasını kararlaştırdı.
- [ ] MOD-0141 sürümlü açık PO satırı ve beklenen teslim tarihi sözleşmesi; yokluğunda her izole sonuç DEGRADED, hiçbir aktarım bu dilimde açık değil.
- [x] Ali, ilk lead time kaynağını MOD-0189'un tarihli/sürümlü SKU × Warehouse planlama parametresi ve bağımsız onayı olarak seçti.
- [ ] Demand v2 tüketici sınırı (dört durum kontrolü, bağlı run kanıtı, uzlaştırma) onaylandı; DRAFT v2 canlı girdi sayılmadı.
- [x] Ali dört lead-time komut iznini onayladı (`create`, `update`, `submit`, `review`); MDM aktif şirket/SKU/depo referans doğrulaması ara düzendir.
- [ ] Kullanıcı-şirket yetkisi ve seri kapsamı Platform'un yetkili kaynağıyla doğrulandı; merkezi `yetkili şirketlerim` bağı açık.
- [x] Lead time yazma ve yerel audit aynı Mongo işlemindedir; audit arızasında sıfır iş etkisi sabotaj testiyle kanıtlanır. Bu geçici `c` yolu merkezi audit değildir; BL-501 açık borçtur.
- [x] İzole fixture dilimi ve canlı kabul kapısı ayrıldı; Ali dar `ready-for-dev` kapsamını onayladı.
- [ ] Owner ve hedef tarih idari olarak belirlendi; `TBD` değerleri onaylı kararla değiştirildi.

Onaylı dar dilim: test verisi / Prism taklidiyle SKU × Warehouse × ISO haftası netting,
fazla stok devri, lot-for-lot, açık PO olmadan görünür DEGRADED hesap ve öneri;
ayrıca tarihli/sürümlü SKU × Warehouse lead time parametresinin planlamacı tarafından
hazırlanması, bağımsız onayı ve işlem-içi audit ile kalıcı yazımı. Lead time komutlarının
izinleri §14'te onaylandı. Bunun dışında kalıcı MRPRun/öneri yazımı, UI,
canlı Demand v2/PO/BOM tüketimi ve MOD-0141'e otomatik ya da gerekçeli manuel
aktarım kapalıdır. Açık PO üretici sözleşmesi, canlı kaynaklar ve tam modül kabulü ayrı kapıdır.

Ali'nin PR #160 Request Changes karariyla acilan takip dilimi: lead-time komutlari icin gercek JWT izinleri ve tek `ILegalEntityScopeReader` arkasinda MDM aktif sirket listesi/`lookup-validation` (MDM kesintisinde 503); kullanici-sirket atama yetkisi acik borctur. MRP hesabi ayni PlanningService'teki yalniz `Published`, butunlugu dogrulanmis Demand revision satirlarini ve InventoryService'in canli `availability` yanitini tuketir; herhangi biri okunamaz veya kapsam/birim uyusmazsa kapali hata verir. Acik PO yoktur ve her basarili hesap gorunur `DEGRADED` kalir. Test verisi yalniz testtedir. Gercek Mongo + MDM + Inventory HTTP kaniti, audit rollback testi ve main karsilastirmali test kapisi gerekir. Bu ek, tum modulu veya Demand v2 sozlesmesini dondurmaz; MOD-0141 aktarimi, BL-501 ve ekranlar acik kalir.

Ali karari: MOD-0191'in bugunku okuma sozlesmesi onayli safety-stock miktari sunmadigindan bu etki hesapta kullanilmaz; dogrulanmis sifir gibi sunulmaz. Basarili her hesap iki ayri gorunur neden tasir: acik PO hesaba katilmadi ve onayli safety-stock hesaba katilmadi. Yetkili miktar baglanincaya kadar tam netting kabulu aciktir.

**r2 kapıları (bound-run 1.0.0):**

- [x] Bound-run sözleşmesi 1.0.0 FROZEN (2026-11-24, sahip Beste, K-1…K-12; commit `66b165806`). Yukarıdaki "Demand v2 tüketici sınırı" maddesinin bağ kaydı, durum, izin, denetim ve sayfa kısmını karşılar; madde açık kalır, çünkü Demand v2 üretici API'si DRAFT, A-1…A-8 açık ve r2 onaylanmadı.
- [ ] r2 bağımsız incelemesi (CT-APR-02) ve sürüme bağlı onay satırı (CT-APR-01, onaylayıcı Beste; yazar ≠ onaylayıcı).
- [ ] A-1, A-2, A-3 (sıklık / süreler) ve A-4 (`requestHash` alan kümesi) teknik tasarımda ölçülerek yazıldı.
- [ ] A-5: sözleşme §5 denetim alan tablosunun sahip teyidi (Beste).
- [ ] A-8: gerçek geçici Mongo testleri (§17 r2) geçti; geçmeden `Superseded` içerik okuması açılmaz.
- [ ] MOD-0188 üretici tarafı: A-7 (K-9 olay şeması ve outbox çevirisi) ve A-6 kararı (MOD-0188 §15 r2); Demand v2 üretici dondurması ayrı merkez kararıdır.
- [ ] Ekran ve Gateway: DL-E1 / DL-E2 için mockup brief, X-1 (integration-agent), X-2 (PKS-001 §4, kullanıcı onayı), X-3 (§11 r2).

## 19. Implementation Notes

Blueprint 8.1 `Blueprint_Data`, MOD-0189'u W-4 Planning Execution, SoR `MRP runs, replenishment proposals, exceptions`, min contract `PLAN-MRP-BUNDLE` olarak gösterir. `WP-MVP4-planning.md` golden flow'u forecast → gerekiyorsa BOM explode → availability'ye karşı net → safety-stock/lead-time → requirement → proposal → MOD-0141 handoff olarak verir. Bu sıralama otomatik PO onayı değildir. Main'deki FROZEN Demand v1, owned DRAFT v2'nin yerine geçmez.

## 20. Follow-up Items

### Ali kararları ve açık üretici bağımlılıkları

| Kapsam | Karar / davranış | Hazırlık durumu |
|---|---|---|
| ISO hafta netting | SKU × Warehouse; sürümlü kesit; `max(0, brüt talep + onaylı safety stock − kullanılabilir stok − varsa açık sipariş)`; fazla tahmini stok devreder, ilk sürüm lot-for-lot. | Ürün kuralı onaylı; UoM ve kova içi tarih tahsisi teknik tasarımda açık. |
| Demand | Yalnız onaylı ve Published v2 revision; v1 fallback yok. | Ali: v2 şimdi dondurulmaz. Prism taklidi izole geliştirme adayı; canlı tüketici kabulü açık. |
| BOM | v1.2.0 olayları kontrol işareti; `current` / `explode` ile güncel effective sürüm ayrıca doğrulanır. | Tüketici kuralı onaylı; canlı test ve çok seviyeli hesap sınırı açık. |
| Açık PO | Tüketicide `PO - GRN` yapılmaz; görünüm yokken her plan DEGRADED. Hesap/inceleme/öneri serbest; MOD-0141'e otomatik devir yok. Gerekçeli manuel devir kararı yalnız sonraki dilim içindir. | MOD-0141 üretici sözleşmesi açık; canlı kabul engeli. |
| Lead time | MOD-0189 SKU × Warehouse başına gün; tarihli/sürümlü; planlamacı girer, bağımsız kişi onaylar. Sourcing teklif alanı yetkili değildir. | Ürün kararı onaylı; kalıcı komut/audit tasarımı ve gelecekteki satın alma kaynağı geçişi açık. |
| MOQ / koli | İlk sürüm lot-for-lot. | Daha sonraki ürün kararı; ilk sürümü engellemez. |

MOD-0141 sahibine iletilecek açık satır görünümü alan adayları (henüz onaylı şema değildir):

| Alan | Tip | MRP anlamı |
|---|---|---|
| `tenantId`, `legalEntityId` | UUID | Üreticide doğrulanmış kapsam. |
| `poId`, `poLineId` | UUID veya üreticinin sabit opaque kimliği | Tekil sipariş/satır bağı ve tekrar ayıklama. |
| `skuId`, `warehouseId` | UUID / üreticinin kanonik depo kimliği | Netting serisine eşleme; depo eşlemesi kanıtlanamazsa satır kullanılmaz. |
| `baseUomId` | string | Netting temel birimi; dönüşüm referansı yoksa farklı birim toplanmaz. |
| `remainingQuantityBaseUom` | decimal string | İptal, GRN ve ters kayıtlar sonrasında açık kalan yetkili miktar. |
| `expectedDeliveryDate` | date | Açık miktarın beklenen giriş tarihi; bilinmiyorsa satır kovaya tahsis edilmez. |
| `asOfUtc`, `sourceVersion` | date-time / string | Tekrarlanabilir kesit ve değişim denetimi. |
| `lineStatus` | enum | İptal/kapalı/bloke satırın açık tedarik sayılmasını önler; değer kümesi üreticide kararlaştırılır. |

Lead time kaynak kararı: ilk sürümde MOD-0189'un kendi onaylı planlama parametresi tek yetkili
kaynaktır. İleride satın alma tarafının yetkili tedarikçi × SKU kaydı gelirse öncelik ona geçer;
alan/sürüm/effective-date ve mevcut planların geçişi ayrı sözleşme işidir. MOD-0145 Sourcing
teklifindeki nullable `leadTimeDays` ve sessiz sabit varsayılan kullanılmaz.

### Önceki seçeneklerin karar izi

| Konu / secenek | Artisi | Eksisi | Bagimli modul / sozlesme | Oneri |
|---|---|---|---|---|
| Netting: tek donem toplami | Basit fixture hesabi. | Vadesi sonraki kovada olan tedariki erken sayabilir. | MOD-0188 Demand, MOD-0173 Inventory, MOD-0191 policy. | Yalniz izole karsilastirma. |
| Netting: SKU x Warehouse icin zaman kovasi ve surumlu girdi kesiti | Ihtiyac, stok ve tedarik sirasi aciklanabilir. | Stok devri, negatif sonuc, lead time ve UoM karari gerekir. | MOD-0188 v2, MOD-0173, MOD-0141/0142, MOD-0191; BOM yolunda MOD-0193. | Ali karari: ISO haftasi ve yukaridaki formul. |
| Acik PO: mevcut PO ve GRN'den tuketicide bakiye turetme | Mevcut FROZEN yuzeyler okunabilir. | PO satirinda teslim tarihi ve yetkili acik bakiye yok; ters kayit ve kesit tutarliligi kanitlanamaz. | MOD-0141 `requisition-po` v1.1.0; MOD-0142 `grn-event` v1.0.0. | Canli netting icin kullanma. |
| Acik PO: ureticinin surumlu acik satir gorunumu | Kalan miktar, beklenen tarih ve asOf ureticide dogrulanir. | Yeni/onayli uretici sozlesmesi gerekir. | MOD-0141 PO, MOD-0142 GRN. | Onerilen yon. |
| Demand: FROZEN v1 `/plan` veya `/forecast` | Main'de yayimli. | Degismez revision, durum surumu ve butunluk bagi yok. | MOD-0188 Demand v1. | V2 yerine sessiz kullanim yok. |
| Demand: DRAFT v2 fixture | Izole cekirdek testi mumkun. | Canli veya onayli sozlesme kaniti degil. | MOD-0188 owned v2 DRAFT. | Yalniz pack onayli izole test. |
| Demand: onayli v2 snapshot ve durum sorgusu | Run-revision bagi ve invalidation uzlastirmasi kurulabilir. | Merkezi freeze ve tuketici kabulu bekler. | MOD-0188 / MOD-0189 v2 karari. | Canli tuketim icin onerilen yol. |

MOD-0188 v2 merkezî freeze; MOD-0141 açık PO bakiye/tarih üretici sözleşmesi; MOD-0191 policy tüketici sözleşmesi; Platform yetkili şirketlerim ve seri kapsamı; canlı Inventory/Product/BOM üretici testi; MOD-0141 handoff; Gateway, izin kaydı, UI ve merkezi audit/BL-501 bağlantısı ayrı işlerdir. MOD-0190/0192 MVP-6 işlerine dokunulmaz.

**r2 — seçenek tablosu güncellemesi (bound-run 1.0.0):**

| Konu / seçenek | Artısı | Eksisi | Bağımlı modül / sözleşme | Öneri / karar |
|---|---|---|---|---|
| Demand: bound-run 1.0.0 bağ kaydı + servis içi tarihsel okuma | Run–revizyon bağı, durumlar, izinler ve sayfa sınırları dondu; aynı serviste HTTP / Gateway yok (K-7) | Üretici v2 API'si DRAFT; A-1…A-8 açık; kalıcı run kodu yok | MOD-0188 v2 (DRAFT); `WP-MOD0189-BOUND-RUN-CONTRACT.md` 1.0.0 | Sahip kararı K-1…K-12 ile seçildi; kod DL-R1'de, r2 onayından sonra |
| Demand: bağı HTTP ucu / Gateway üzerinden okumak | Servisler ayrılırsa hazır yüzey | Yeni dış yüzey; istemci beyanının bağ kanıtı sanılma riski | Gateway, integration-agent | K-7 ile seçilmedi |

r1'deki "Demand: onaylı v2 snapshot ve durum sorgusu" satırının "Merkezi freeze" eksisi bağ sözleşmesi için kapandı, Demand v2 üretici API'si için açık kalır. "Ali kararları" tablosundaki Demand satırının "v2 şimdi dondurulmaz" ifadesi üretici API'si içindir; bağ sözleşmesi 1.0.0 FROZEN'dır.

## 21. Audited Events

Dar dilimdeki lead time oluşturma, yeni sürüm, incelemeye gönderme, bağımsız onay ve gerekçeli ret komutları; aktör, Tenant/LegalEntity, SKU × Warehouse, geçerlilik, önce/sonra sürüm ve kararı işlem-içi değiştirilemez yerel izde tutar (`c` yolu, geçici). Audit yazılamazsa parametre/karar yazılmaz; her komut için sabotaj kanıtı gerekir. Bu merkezi audit değildir: `BL-501 bekliyor`, geldiğinde yerel iz kaldırılmaz. Onaylı izin anahtarları §14'tedir; runtime kaydı ve HTTP/UI bu dar dilimin dışında kalır. `RunCreated`, `RunStopped`, `ProposalApproved`, `ProposalHandedOff` bu dilimde yetkili yazma komutu değildir; bunların audit ve canlı sözleşme kararı ayrıca gerekir. MDM iletici kodu kopyalanmaz, BL-501 arayüzü uydurulmaz.

**r2 — run denetim eylemleri (bound-run 1.0.0 §5, K-6):**

| Yazma / geçiş | Olay adı | Yol | Not |
|---|---|---|---|
| Run oluşturma + bağ (`mrp.runs.create`) | `RunCreated` | `c` (yerel, işlem-içi, geçici); `BL-501 bekliyor` | `auditId` = `bindingAuditId`; denetim yazılamazsa run yok |
| Kullanıcı iptali (`mrp.runs.cancel`) | `RunStopped` (`stopReason` = `Cancelled`, gerekçe) | `c`; `BL-501 bekliyor` | Gerekçe zorunlu |
| Sistem: askıya alma / sürdürme | `RunSuspended` / `RunResumed` | `c`; `BL-501 bekliyor` | `actorId` servis kimliği |
| Sistem: tamamlama | `RunCompleted` | `c`; `BL-501 bekliyor` | Kontrol noktası 3 |
| Sistem: durdurma | `RunStopped` (`DemandInvalidated`, `ScopeRevoked`, `IntegrityFailed`) | `c`; `BL-501 bekliyor` | |
| Sistem: güvenilmez sonuç | `RunResultUntrusted` | `c`; `BL-501 bekliyor` | Uygulanmamış öneriler bloklu |

Yukarıdaki "`RunCreated`, `RunStopped` … bu dilimde yetkili yazma komutu değildir" cümlesi r1 dar dilimi için doğrudur. DL-R1 ile `RunCreated` ve kullanıcı iptalindeki `RunStopped` yazma komutu olur; diğer dört ad sistem geçişidir. `ProposalApproved` ve `ProposalHandedOff` bu revizyonun konusu değildir. Her yeni `c` izi `tests/architecture/audit-ledger/Diten.PlanningService.md` defterine ve sabotaj kanıtına bağlanır; merkezi audit gelince yerel iz kaldırılmaz. Denetim alan tablosunun sahip teyidi A-5'tir.

## 22. Değişiklik günlüğü

- **r2 (2026-11-25) — bound-run 1.0.0 uyumu, taslak.** Yazar: module-pack-author ajanı (Claude, WMS CT oturumu); onaylayıcı: Beste (yazar ≠ onaylayıcı). Kaynak: `execution/domains/supply-chain-execution/work-packs/WP-MOD0189-BOUND-RUN-CONTRACT.md` 1.0.0 FROZEN ve `docs/records/decisions/2026-11/mod0188-demand-v2-field-names-owner-decisions-01.md` K-1…K-12 (commit `66b165806`); ölçüm tabanı HEAD `d4377ac56`. Eklenenler: künye `approval_status`, `revision`, `status_note`; başlık notu; §4, §11, §12, §13, §14, §15, §16 (AC-BR01…AC-BR12), §17, §18, §20 ve §21 sonlarına r2 paragrafları; bu bölüm. r1 metni silinmedi, r1 kabul maddelerinin anlamı değişmedi. Kimlik ön kontrolü 2026-11-25'te yeniden koşuldu: OK. Açık sorular: §4 (`runVersion` ↔ teknik `Version`), §11 (dilim tablosu ve numaralar), §12 (hata kodları / HTTP durumları), §14 (anlık hesap ucunun izni), A-1…A-8.
- **r1 (HEAD `d4377ac56`'daki metin, LF sha256 `0551b33af99ea26c59d563dbca23dc0be8b8e2b996bd2f2216da30ef7ffcd511`)** — Ali onaylı dar dilim ve PR #160 takip dilimi (§18). Revizyon etiketi r2 ile geriye dönük verildi.
