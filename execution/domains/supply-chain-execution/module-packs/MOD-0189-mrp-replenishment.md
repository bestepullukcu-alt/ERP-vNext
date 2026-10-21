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
owner: supply-chain-execution / control-tower (human owner TBD)
branch: feature/sce/mod-0189-mrp-core
started: TBD
target: TBD
---

# MOD-0189 — MRP & Replenishment

> DCP-002 kimlik kontrolü Blueprint 8.1 `Blueprint_Data` ve registry'de `MOD-0189 / MRP & Replenishment` için geçti. `ready-for-dev` §18'deki izole dilimi ve PR #160 icin onayli canli MDM/Published Demand/Inventory takip dilimini kapsar; tam modul kabulunu, dis aktarimi veya Gateway/UI isini kapsamaz. MVP-4 kapsamı MOD-0188, MOD-0189 ve MOD-0191'dir; MOD-0190/0192 MVP-6'ya aittir.

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

## 12. Validation Rules

Ali kararı: her SKU × Warehouse × ISO haftası için girdi sürüm/asOf kesiti kaydedilir. Hesap izi
`netRequirement = max(0, grossDemand + approvedSafetyStock - availableStock - openOrderQuantity)` biçimindedir.
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

## 13. Failure Path to Verify

Yanlış tenant/şirket/seri için kayıt veya run görünmez; eksik işlem izni reddedilir. Yetki, Demand durumu, stok asOf veya BOM sürümü doğrulanamıyorsa run/proposal güvenilir ilan edilmez. Açık PO görünümü yoksa kullanıcıdan gizlenmeyen DEGRADED plan hesaplanır, incelenir ve öneri üretebilir; açık tedarik sıfır diye doğrulanmış sayılmaz. MOD-0141'e otomatik aktarım yasaktır. Bağımsız onaylayıcı yalnız "açık siparişler kontrol edildi" gerekçesini yazarak manuel aktarabilir; gerekçe zorunlu audit kaydında tutulur. Görünüm varken bozuk veya çelişkili bakiye sessizce DEGRADED'a düşmez, kapalı hata/istisna üretir. Aynı istek aynı girdilerle tek run/öneri etkisi üretir; aynı anahtar farklı içerikle çatışır. Audit veya zorunlu kanıt başarısızlığı yarım sonuç bırakmaz. Olay kaybında düzenli yetkili durum uzlaştırması gerekir; aralık ve kullanıcıya görünen askı durumu açık karardır.

## 14. Authorization Convention

Taslak izin ayrımı: `mrp.runs.read`, `mrp.runs.create`, `mrp.proposals.read`, `mrp.proposals.approve`, `mrp.evidence.read`. Bunlar kayıtlı runtime izinleri değildir. Hazırlayan/onaylayan ayrımı, Platform aktör–LegalEntity ataması, SKU × Warehouse kapsamı ve iptal semantiği Security/CT onayı ister. MDM yalnız referans doğrular.

Ali'nin onayladığı dar lead-time komut izinleri: `mrp.lead-times.create` (ilk taslak),
`mrp.lead-times.update` (yeni sürüm), `mrp.lead-times.submit` (incelemeye gönderme) ve
`mrp.lead-times.review` (bağımsız onay veya gerekçeli ret). Her komut Tenant × aktif
LegalEntity × SKU × Warehouse kapsamında sunucuda doğrulanır; MDM referans doğrulaması
erişilemezse işlem kapalı kalır. MDM doğrulaması kullanıcının o şirkete yetkisi olduğunun
kanıtı değildir; Platform `yetkili şirketlerim` bağı merkezi ekip gelene kadar açıktır.

## 15. Gateway / API Routing Decision

Servis `Diten.PlanningService`, port 5068 olarak kararlaştırıldı; yeni servis açılmaz. `PLAN-MRP-BUNDLE` producer sözleşmesi, Gateway rotası ve merkezî kayıt ayrı review/freeze gerektirir. PR #160 takip diliminde servis portunda dogrudan HTTP ile MDM aktif sirket ve Inventory availability okunur; Demand ayni servisin dogrulanmis Published snapshot'idir. Demand v2 DRAFT dis tuketimi ve MOD-0141 aktarimi acilmaz.

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

## 17. Test Expectations

Karar sonrası saf netting için sınır/decimal/UoM ve eksik-veri testleri; FROZEN sözleşmelere karşı mock/fixture testleri; idempotency ve yarış için geçici loopback Mongo replica set testleri; tenant/şirket/seri izolasyonu; JWT/HTTP izin hattı; audit ve durum-kaybı sabotajı; yedi dil ve ekran testi gerekir. 27017–27021 test için kullanılmaz. CI başarısı ve canlı üretici kanıtı ayrı tutulur. İzole lead time yazımı için gerçek Mongo'da işlem-içi audit sabotajı gerekir; fixture başarısı canlı üretici kanıtı değildir.

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

## 21. Audited Events

Dar dilimdeki lead time oluşturma, yeni sürüm, incelemeye gönderme, bağımsız onay ve gerekçeli ret komutları; aktör, Tenant/LegalEntity, SKU × Warehouse, geçerlilik, önce/sonra sürüm ve kararı işlem-içi değiştirilemez yerel izde tutar (`c` yolu, geçici). Audit yazılamazsa parametre/karar yazılmaz; her komut için sabotaj kanıtı gerekir. Bu merkezi audit değildir: `BL-501 bekliyor`, geldiğinde yerel iz kaldırılmaz. Onaylı izin anahtarları §14'tedir; runtime kaydı ve HTTP/UI bu dar dilimin dışında kalır. `RunCreated`, `RunStopped`, `ProposalApproved`, `ProposalHandedOff` bu dilimde yetkili yazma komutu değildir; bunların audit ve canlı sözleşme kararı ayrıca gerekir. MDM iletici kodu kopyalanmaz, BL-501 arayüzü uydurulmaz.
