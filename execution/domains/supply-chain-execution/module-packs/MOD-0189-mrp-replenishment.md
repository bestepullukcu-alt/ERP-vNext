---
id: MOD-0189
name: MRP & Replenishment
domain: supply-chain-execution
service: Diten.PlanningService
service_port: 5068
shell: tenant
golden_reference: none
entity_base: EntityBase
status: draft
owner: supply-chain-execution / control-tower (human owner TBD)
branch: feature/sce/mod-0189-mrp-replenishment
started: TBD
target: TBD
---

# MOD-0189 — MRP & Replenishment

> DCP-002 kimlik kontrolü Blueprint 8.1 `Blueprint_Data` ve registry'de `MOD-0189 / MRP & Replenishment` için geçti. Bu belge karar taslağıdır: servis/port yerleşimi dışında kod, izin, rota veya canlı tüketim yetkisi vermez. MVP-4 kapsamı MOD-0188, MOD-0189 ve MOD-0191'dir; MOD-0190/0192 MVP-6'ya aittir.

## 1. Module Summary

Ali'nin servis kararı `Diten.PlanningService` (5068); ayrı servis açılmaz. Pack `draft` kalır. MOD-0188 PlanningService main'e merge edilmeden MOD-0189 kodu başlamaz; MOD-0188 feature dalından kod devralınmaz.

MRP, onaylı talebi stok ve doğrulanmış tedarik girdilerine karşı netleyerek açıklanabilir net ihtiyaç ve ikmal önerisi üretir. Hedef, `MRP Monitor`, istisna kuyruğu ve kanıt paketidir. Ürün kararı olmadan netting formülü, zaman kovası, lot büyüklüğü veya sipariş tetikleyicisi kodlanmaz.

## 2. Ownership and Boundaries

MOD-0189 `MRPRun`, `NetRequirement`, `ReplenishmentProposal` ve MRP istisnalarının SoR'udur. Satın alma siparişi, satın alma talebi ve onay akışı MOD-0141'in; stok gerçeği MOD-0173'ün; BOM kompozisyonu MOD-0193'ün; brüt talep revision'ı MOD-0188'in; safety-stock politikası MOD-0191'in SoR'udur. MOD-0189 başka modülün veritabanına doğrudan erişmez ve PO oluşturmaz. Onaylı requirement için MOD-0141'e yalnız kararlaştırılmış handoff yapar.

## 3. Owned Objects

| Nesne | Taslak sorumluluk | Açık karar |
|---|---|---|
| MRPRun | Seçilmiş dönem, girdi revision/sürüm referansları, durum, kapsam, başlatan ve kanıt. | Run yaşam döngüsü, durdurma ve tekrar anahtarı. |
| NetRequirement | SKU × Warehouse × zaman kovası için brüt ihtiyaç, uygun stok, açık tedarik, safety stock ve net sonuç izleri. | Netting sırası, birim, kovalar ve kısmi verinin anlamı. |
| ReplenishmentProposal | Net ihtiyaçtan türeyen idempotent, gerekçeli öneri; PO değil. | Lot/lead-time, onay ve MOD-0141 handoff alındısı. |
| MRPException | Eksik, çelişkili, geçersiz veya güncelliğini yitirmiş girdinin görünür kaydı. | Şiddet, çözüm ve run'a etkisi. |

## 4. Entity Fields

`TenantId`, `LegalEntityId`, `IsDeleted`, `DeletedAt`, oluşturma/güncelleme izi ve teknik sürüm repo standardına göre zorunludur. İş alanları adaydır: `runId`, `planningPeriodKey`, `demandRevisionId`, `demandStateVersion`, `inventoryAsOf`, `bomVersionId`, `sourceVersion`, `skuId`, `warehouseId`, `baseUomId`, `grossQuantity`, `availableQuantity`, `openSupplyQuantity`, `safetyStockQuantity`, `netQuantity`, `proposalId`, `idempotencyKey`, `evidenceChecksum`. Alan adları ve hesap semantiği CT incelemesinden önce runtime sözleşmesi değildir.

## 5. Repo Scope

Bu taslağın tek yazma kapsamı bu pack dosyasıdır. Gelecek backend yeri main'e merge edilmiş `services/Diten.PlanningService/`, port 5068'dir. MOD-0188 dalındaki kod veya iç tipler bugünkü geliştirme bağımlılığı olamaz. UI, Gateway ve merkezî contract değişiklikleri ayrı sahiplik ve onay gerektirir.

## 6. Protected Paths

`AGENTS.md`, `.antigravity/**`, domain config, DCP-009, Gateway `ocelot.json`, Platform/MDM/Auth, MOD-0188/MOD-0191 servisleri, MOD-0190/0192 ve MVP-3 kodu bu pack'in yazma kapsamı dışındadır. FROZEN sözleşme dosyaları tüketici tarafından değiştirilmez.

## 7. Dependencies

| Girdi / sahip | Bugünkü sözleşme kanıtı | MRP için eksik karar veya sınır |
|---|---|---|
| Talep / MOD-0188 | `docs/analysis/contracts/demand.openapi.yaml` v1.0.0 FROZEN, `/plan` ve `/forecast`; MOD-0188 dalında Demand v2 revision/status DRAFT. | Bağlı immutable revision, dört yetkili durum kontrolü, snapshot bütünlüğü, `Superseded` tarihsel okuma kanıtı ve `Invalidated` uzlaştırması v2 onayı olmadan canlı bağlanmaz. v1'e sessiz düşüş yok. |
| Stok / MOD-0173 | `inventory-bundle.openapi.yaml` v1.2.1 FROZEN, `GET /availability`; `available`, `reserved`, `byStatus`, `baseUomId`, `asOf`. | Anlık görünüm gelecekteki planlanmış giriş veya tarihsel stok yokluğu kanıtı değildir. Kova/asOf tutarlılığı ve kalite/bloke stok kuralı kararlaştırılmalı. |
| Ürün / MOD-0290 | `product-master-bundle.v1.0.openapi.yaml` FROZEN; v1.1 dosyası DRAFT. | Item/SKU ve temel UoM dönüşümü yalnız onaylı sürümden; taslak alanlara canlı bağımlılık yok. |
| Açık PO / MOD-0141 | Main ProcurementService `requisition-po.openapi.yaml` v1.1.0 FROZEN list/get PO; `poId`, `poLineId`, durum, satır miktarı ve UoM. `needBy` yalnız requisition satırında; runtime `PoLine` da teslim tarihi veya açık bakiye tutmaz. | PO satırında kalan açık miktar ve beklenen teslim tarihi yok. İptal/ters kayıt, GRN düşümü ve asOf kesiti üreticiyle sözleşmelendirilmeli. |
| Mal kabul / MOD-0142 | `grn-event.openapi.yaml` v1.0.0 FROZEN; GRN, `poId/poLineId` ve alınan miktarı taşır, stok hareketini MOD-0173'e post eder. | PO açık bakiyesinin yetkili hesabı ve çift sayımı önleyen ilişki ayrıca kararlaştırılmalı; GRN tek başına gelecekteki tedarik değildir. |
| BOM / MOD-0193 | `bom.openapi.yaml` v1.0.0 FROZEN, effective BOM ve `/explode` component ihtiyacı ile `bomVersionId`. | Çok seviyeli patlatma, alternatif, fire/verim, geçerlilik tarihi, döngü ve sürüm sabitleme kuralları açık. Manufacturing yolu dışında BOM zorunlu değil. |
| Safety stock / MOD-0191 | MOD-0191 ayrı dalda izole Yöntem A ve revision çekirdeği; main'de onaylı canlı politika tüketim sözleşmesi yok. | Hangi etkin policy sürümü ve hangi tarihli miktar netting'e girer kararlaştırılmalı; fixture canlı policy değildir. |

## 8. Runtime Constraints

Tenant × LegalEntity ve SKU × Warehouse kapsamı sunucuda doğrulanır; claim, form veya MDM lookup kullanıcı yetkisi yerine geçmez. Platform'un yetkili şirketlerim ve seri kapsamı gelene kadar canlı kullanıcı işlemi kabul edilmez. Her run girdilerinin sürüm/asOf bağını korur; eksik veya bozuk girdi sıfıra çevrilmez. Her sorgu tenant filtresi ve soft-delete kuralı taşır. Kısmi snapshot'tan güvenilir öneri üretilmez. Repo kuralları beş katman, CQRS, Mongo, Response<T> ve dört pipeline behavior için geçerlidir; servis 5068 PlanningService olarak seçilmiştir, bu canlı tüketim izni değildir.

## 9. Layout & Shell Contract

İleride tenant çalışma alanı `_LayoutTenantShell.cshtml` kullanır; yetkisiz kullanıcıya sayfa iskeleti çizilmez. Bu taslak ekran açmaz. MRP Monitor/Exception Queue/Evidence Pack için form alan sayısı ve DataTable gereksinimi ayrıca belirlenecektir; `golden_reference: none` yalnız bugün UI sözleşmesinin kararsızlığını belirtir.

## 10. Backend File Convention

Servis kararı sonrası feature klasörleri Command, Query, Handler ve Validator için ayrı tutulur. MOD-0188 veya MOD-0141 iç modelini referans almak yerine yayımlanmış HTTP/olay sözleşmelerine uygun tüketici adaptörleri kullanılır. İdempotency, sürüm ve kanıt doğrulaması katman sınırlarında test edilir. Bu bölüm dosya oluşturma izni değildir.

## 11. Frontend File Contract

UI ve Gateway ayrı onay konusudur. Frontend doğrudan servis portuna istek atmaz; rota yalnız integration-agent tarafından açılır. Yedi tenant dili (en, tr, fr, es, zh, ar, ru), erişilebilir yetkisiz yüzey ve varsa DataTable v2 profili ekran kararıyla birlikte tanımlanır.

## 12. Validation Rules

| Alan / kanıt | Önerilen doğrulama | Açık ürün kararı |
|---|---|---|
| Tenant/LegalEntity | Sunucu kaynaklı yetkili kapsam; her girdi aynı kapsamda. | Platform sözleşmesi ve iptal testi. |
| Demand revision | Aynı `revisionId`, yetkili durum ve checksum; kısmi sayfa yok. | Dört kontrol noktası ve tarihsel bağ şeması. |
| Item/SKU/UoM/Warehouse | FROZEN üreticilerde mevcut ve aynı ölçü biriminde. | Dönüşüm, boyut ve lokasyon kapsamı. |
| Inventory/PO/BOM asOf | Run sınırında sürümlü, tekrarlanabilir kesit. | Gecikme toleransı ve açık PO bakiyesi. |
| Miktar ve zaman | Decimal, açık kova ve negatif/missing ayrımı. | Netting, safety stock, lead time ve rounding. |

## 13. Failure Path to Verify

Yanlış tenant/şirket/seri için kayıt veya run görünmez; eksik işlem izni reddedilir. Yetki, Demand durumu, stok asOf, açık PO bakiyesi veya BOM sürümü doğrulanamıyorsa run/proposal güvenilir ilan edilmez. Aynı istek aynı girdilerle tek run/öneri etkisi üretir; aynı anahtar farklı içerikle çatışır. Audit veya zorunlu kanıt başarısızlığı yarım sonuç bırakmaz. Olay kaybında düzenli yetkili durum uzlaştırması gerekir; aralık ve kullanıcıya görünen askı durumu açık karardır.

## 14. Authorization Convention

Taslak izin ayrımı: `mrp.runs.read`, `mrp.runs.create`, `mrp.proposals.read`, `mrp.proposals.approve`, `mrp.evidence.read`. Bunlar kayıtlı runtime izinleri değildir. Hazırlayan/onaylayan ayrımı, Platform aktör–LegalEntity ataması, SKU × Warehouse kapsamı ve iptal semantiği Security/CT onayı ister. MDM yalnız referans doğrular.

## 15. Gateway / API Routing Decision

Servis `Diten.PlanningService`, port 5068 olarak kararlaştırıldı; yeni servis açılmaz. `PLAN-MRP-BUNDLE` producer sözleşmesi, kesin endpoint/method matrisi, Gateway rotası ve merkezî kayıt ayrı review/freeze gerektirir. Tüketici FROZEN sözleşmelere karşı fixture/mock ile yalnız pack onayı ve MOD-0188 main merge sonrası izole geliştirilebilir; canlı çapraz servis çağrısı bu taslakla açılmaz.

## 16. Acceptance Criteria

- [ ] MRP run aynı onaylı Demand revision ve aynı sürümlü stok/PO/BOM girdileriyle yeniden hesaplandığında aynı net ihtiyaç ve kanıt özetini verir.
- [ ] Manufacturing yolunda effective BOM sürümü sabitlenir; component ihtiyaçları belgelenmiş netting kuralına göre hesaplanır. BOM olmayan ticari yolda patlatma yapılmaz.
- [ ] Uygun stok ve doğrulanmış açık tedarik çift sayılmadan, safety stock/lead-time kuralıyla açıklanabilir net ihtiyaç üretilir.
- [ ] Aynı girdi ve idempotency anahtarı ikinci run/öneri oluşturmaz; farklı içerikli tekrar çatışır.
- [ ] Sadece onaylı requirement, MOD-0141'e sözleşmeli handoff alındısıyla iletilir; MOD-0189 PO oluşturmaz.
- [ ] Demand `Superseded` veya `Invalidated`, kayıp/geç olay ve durum sorgusu kesintisi hallerinde kararlaştırılmış dört tüketici kontrolü ve uzlaştırma güvenli sonuç verir.
- [ ] Tenant/LegalEntity/SKU × Warehouse izolasyonu, izin, görev ayrılığı, audit arızası ve yeniden başlatma gerçek kaynakla test edilir.

Bu maddeler önerilen kabul kapsamıdır; Ali/CT incelemesi olmadan tamamlanmış sayılmaz.

## 17. Test Expectations

Karar sonrası saf netting için sınır/decimal/UoM ve eksik-veri testleri; FROZEN sözleşmelere karşı mock/fixture testleri; idempotency ve yarış için geçici loopback Mongo replica set testleri; tenant/şirket/seri izolasyonu; JWT/HTTP izin hattı; audit ve durum-kaybı sabotajı; yedi dil ve ekran testi gerekir. 27017–27021 test için kullanılmaz. CI başarısı ve canlı üretici kanıtı ayrı tutulur. Bu taslak için yalnız belge ve DCP-002 doğrulaması yapılır.

## 18. Ready-for-dev Checklist

- [x] Ali servis ve portu `Diten.PlanningService` / 5068 olarak seçti; ayrı servis yok.
- [ ] MOD-0188 PlanningService main'e merge edildi; MOD-0189 kod kapısı ayrıca açıldı.
- [ ] Kalıcı kayıt yeri ve servis içi sahiplik sınırı onaylandı.
- [ ] Netting formülü, zaman kovası, BOM yolu, safety stock ve açık PO bakiyesi ürün/sözleşme sahiplerince netleştirildi.
- [ ] Demand v2 tüketici sınırı (dört durum kontrolü, bağlı run kanıtı, uzlaştırma) onaylandı; DRAFT v2 canlı girdi sayılmadı.
- [ ] Platform şirket/seri kapsamı ve izinler ile audit yolu kararlaştırıldı.
- [ ] İzole fixture dilimi ve canlı kabul kapısı ayrıldı; CT/Ali `ready-for-dev` kapsamını açıkça onayladı.
- [ ] Owner ve hedef tarih idari olarak belirlendi; `TBD` değerleri onaylı kararla değiştirildi.

## 19. Implementation Notes

Blueprint 8.1 `Blueprint_Data`, MOD-0189'u W-4 Planning Execution, SoR `MRP runs, replenishment proposals, exceptions`, min contract `PLAN-MRP-BUNDLE` olarak gösterir. `WP-MVP4-planning.md` golden flow'u forecast → gerekiyorsa BOM explode → availability'ye karşı net → safety-stock/lead-time → requirement → proposal → MOD-0141 handoff olarak verir. Bu sıralama formül, veri tamlığı veya otomatik PO onayı değildir. Main'deki FROZEN Demand v1, MOD-0188 dalındaki DRAFT v2'nin yerine geçmez.

## 20. Follow-up Items

### Karar secenekleri (onay bekliyor)

| Konu / secenek | Artisi | Eksisi | Bagimli modul / sozlesme | Oneri |
|---|---|---|---|---|
| Netting: tek donem toplami | Basit fixture hesabi. | Vadesi sonraki kovada olan tedariki erken sayabilir. | MOD-0188 Demand, MOD-0173 Inventory, MOD-0191 policy. | Yalniz izole karsilastirma. |
| Netting: SKU x Warehouse icin zaman kovasi ve surumlu girdi kesiti | Ihtiyac, stok ve tedarik sirasi aciklanabilir. | Kova, tahsis sirasi, safety stock, lead time, UoM ve yuvarlama karari gerekir. | MOD-0188 v2, MOD-0173, MOD-0141/0142, MOD-0191; BOM yolunda MOD-0193. | Onerilen yon; kesin formul onaylanmadan kodlanmaz. |
| Acik PO: mevcut PO ve GRN'den tuketicide bakiye turetme | Mevcut FROZEN yuzeyler okunabilir. | PO satirinda teslim tarihi ve yetkili acik bakiye yok; ters kayit ve kesit tutarliligi kanitlanamaz. | MOD-0141 `requisition-po` v1.1.0; MOD-0142 `grn-event` v1.0.0. | Canli netting icin kullanma. |
| Acik PO: ureticinin surumlu acik satir gorunumu | Kalan miktar, beklenen tarih ve asOf ureticide dogrulanir. | Yeni/onayli uretici sozlesmesi gerekir. | MOD-0141 PO, MOD-0142 GRN. | Onerilen yon. |
| Demand: FROZEN v1 `/plan` veya `/forecast` | Main'de yayimli. | Degismez revision, durum surumu ve butunluk bagi yok. | MOD-0188 Demand v1. | V2 yerine sessiz kullanim yok. |
| Demand: DRAFT v2 fixture | Izole cekirdek testi mumkun. | Canli veya onayli sozlesme kaniti degil. | MOD-0188 owned v2 DRAFT. | Yalniz pack onayli izole test. |
| Demand: onayli v2 snapshot ve durum sorgusu | Run-revision bagi ve invalidation uzlastirmasi kurulabilir. | Merkezi freeze ve tuketici kabulu bekler. | MOD-0188 / MOD-0189 v2 karari. | Canli tuketim icin onerilen yol. |

MOD-0188 v2 merkezî freeze; MOD-0141 açık PO bakiye/tarih üretici sözleşmesi; MOD-0191 policy tüketici sözleşmesi; Platform yetkili şirketlerim ve seri kapsamı; canlı Inventory/Product/BOM üretici testi; MOD-0141 handoff; Gateway, izin kaydı, UI ve merkezi audit/BL-501 bağlantısı ayrı işlerdir. MOD-0190/0192 MVP-6 işlerine dokunulmaz.

## 21. Audited Events

Yazma komutları henüz taslak niyettir ve bu pack runtime komut açmaz. Olası `RunCreated`, `RunStopped`, `ProposalApproved`, `ProposalHandedOff` için aktör, kapsam, önce/sonra durum, girdi sürümleri, alındı ve korelasyon gerekir. Kesin komut–olay–AUD-001 yolu (a/b/c veya gerekçeli istisna) CT kararı ve ortak iletici takvimiyle ayrıca doldurulmadan hiçbir yazma komutu kodlanmaz; yerel audit koleksiyonu veya uydurma BL-501 arayüzü açılmaz.
