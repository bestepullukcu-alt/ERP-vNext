---
id: MOD-0178-FU01
name: Outbound Shipment Form v2
parent: MOD-0178
parent_name: Putaway / Picking / Packing
domain: supply-chain-execution
service: Diten.InventoryService (5067, Warehouse in-process) + frontend/Diten.Web
port: 5067
shell: tenant
golden_reference: compact
data_mode: server
screens:
  - module: OutboundShipments
    data_mode: server
entity_base: EntityBase
status: draft
approval_status: pending
revision: "r1 (2026-11-13) — ilk taslak. Kaynak: E3 canlı turu (docs/records/audits/2026-11/mod-0178-e3-live-tour-2026-11-13.md), sahip kararları D-1…D-5 ve CRM sahibinin cevabı C-1…C-5 (Beste, 2026-11-13; docs/records/decisions/2026-11/mod0178-fu01-outbound-form-v2-owner-decisions-01.md). Mockup turu yok (D-5)."
risk: MEDIUM
risk_reason: "Ekran değişikliği tahsis önizlemesini ve FEFO sapma gerekçesini taşır (yanlış lot / eksik gerekçe stok çıkışını etkiler); DL-2 iki donmuş sözleşmeye eklemeli üye yazar (WMS-BUNDLE, WAREHOUSE-OUTBOUND) ve MVP-6 (MOD-0183) bu yüzeyi tüketir; DL-2 / DL-3 servisler arası CRM okuması (kullanıcı token'ı, kiracı izolasyonu, kişisel veri sınırı) ve dondurulmuş CRM-ACCOUNT-LOOKUP 1.0'a eklenecek bir sürüme bağlıdır."
status_note: "draft r1 (2026-11-13) — approval_status pending: bağımsız inceleme (CT-APR-02) ve onay satırı (CT-APR-01, onaylayan Ali) yok; kod yazılmaz. Mockup turu yok, mevcut Golden Reference DataTable + offcanvas desenleri esas alınır (sahip kararı D-5, 2026-11-13). Sıra: WP-0178-E3-FIX-01 önce gider; FU01 DL-1 FIX-01'in CT kabulünden sonra başlar (§8.1). DL-3 CRM-ACCOUNT-LOOKUP 1.1.0'ın (yeni uç GET /api/crm/accounts/{id}/ship-to, anahtar crm.account-ship-to.read — CRM cevabı C-1…C-5, 2026-11-13; tarih TBD, sahibi Beste — CRM) donmasına ve Auth rol eşlemesine (Q-5, Ali) kapılı; elle alıcı girişi her dilimde çalışır. Posta kodu sahip kararı bekliyor (Q-10). Registry satırı yok (CT ekler; FU-0178-FU01-REG). branch / target: TBD — CT hattı kararı."
owner: supply-chain-execution / merkez (Beste) — MVP-5
approver: Ali (GitHub GMG-ERP)
branch: TBD
started: 2026-11-13
target: TBD
form_field_count: 12
mockup: "yok — sahip kararı D-5 (2026-11-13); Golden Reference Compact _Form + Slim _CreateEditOffcanvas + DataTable v2 desenleri"
dependencies: [MOD-0178 (ebeveyn; S2 outbound ekranı ve servis), WP-0178-E3-FIX-01 (önce gider; sayı biçimleyici, terimler, önizleme lotu), WMS-BUNDLE-v1.1.0 (owned → sonraki eklemeli sürüm, DL-2), WAREHOUSE-OUTBOUND-v1.0.0 (owned; 1.1.0 Item adımı bekliyor → sonraki eklemeli sürüm, DL-2), CRM-ACCOUNT-LOOKUP-v1.0.0 (MOD-0149; U-1 doğrulama DL-2, U-3 arama DL-3) → v1.1.0 ship-to ucu (K-9; CRM cevabı C-1…C-5; DL-3 kapısı = 1.1 donması, sahibi Beste — CRM, tarih TBD), Reference Data (ülke / şehir / ilçe kodlarının görünen adı; DL-3, Q-11), MOD-0018 (Auth — crm.account.lookup-validation ve crm.account-ship-to.read anahtarlarının depo rollerine eşlemesi; DL-2 / DL-3 kabul kapısı, Ali), MOD-0183 (MVP-6 — WAREHOUSE-OUTBOUND tüketicisi; XMC-001 bilgi + soru), MOD-0168 (Order Capture — planlı, kod yok; bağlama FU-0178-FU01-ORDER), DEV-0000, DEV-0001]
---

# MOD-0178-FU01 — Outbound Shipment Form v2 (Çıkış sevkiyatı formu v2)

> **Taslak (r1, 2026-11-13).** `status: draft`, `approval_status: pending`. **Paket onaylanmadan geliştirme başlamaz:** kod yalnız
> `approved` / `ready-for-dev` künye + bağımsız inceleme kaydı (CT-APR-02) + onay satırı (CT-APR-01, onaylayan **Ali**) ile yazılır
> (AGENTS.md Çekirdek madde 7). Onay satırları bu dosyada değil, `execution/approvals/approvals.tsv`'ye yazılır.
>
> **Yetki:** bu paket > `execution/domains/supply-chain-execution/domain-config.md` > `AGENTS.md` > `.antigravity/`. Ebeveyn
> `MOD-0178-putaway-picking-packing.md` (r29 onay bekliyor, r27 onaylı) bu paketin **tabanıdır**: §8.17 (outbound oluşturma ve tahsis),
> §8.18 (durum makinesi), §11.5 (S2 dosyaları), §12 / §13 (doğrulama, hata yolları) ve §21 (denetim) aynen geçerlidir; bu paket yalnız
> aşağıda adı geçen yerleri değiştirir ya da ekler. Çelişkide ebeveynin **davranış** kuralları kazanır; bu paket ekran düzenini ve
> `customerRef` eklemesini tanımlar.
>
> **Ölçüm tabanı:** dosya ve satır atıfları bu oturumda `8913e60f0`'da (CT dalı, worktree `wms-ct-01`) okundu; kimlik ön kontrolü
> `6235794ab`'de koşuldu (aradaki iki commit yalnız MOD-0180 / MOD-0182 paket ve karar dosyalarıdır). **Çıkarım** ve **öneri** olarak
> işaretlenmeyen her cümle bir kaynağa dayanır; bilinmeyen değer uydurulmadı (TBD + Q-).

## 0. Kimlik ön kontrolü ve keşif

**DCP-002 ön kontrolü (fail-closed) — bu oturumda koşuldu:**

| Komut | Sonuç |
|---|---|
| `PYTHONUTF8=1 python .antigravity/scripts/verify_module_id.py . --check-id MOD-0178-FU01 --name "Outbound Shipment Form v2" --parent MOD-0178` | `OK  MOD-0178-FU01: proven against Blueprint/registry.` — çıkış **0** |
| `PYTHONUTF8=1 python .antigravity/scripts/verify_module_id.py . --discover "outbound form"` | çıkış **0**, "incelenen kaynaklarda eşleşme bulunamadı" (blueprint 0 · sor 0 · registry 0 · paket 0 · gateway 0 · görev 0); rapor sha256 `2d18a952b58c5af48b37f37cd6f0ab916bfa2db53380682c6d7c13642880043c`; araç sha256 `ae87a566…b51a`; depo HEAD `6235794ab`; 567 ref |

**Mükerrer kontrolü:** `MOD-0178-FU01` kimliği HEAD'de `execution/` ve `docs/` altında hiç geçmiyordu (`git grep "0178-FU01"` boş) ve bütün
ref'lerde hiç eklenmemişti (`git log --all -S "MOD-0178-FU01" -- execution docs` boş). Keşfin kendi sınırları: commit edilmemiş ya da başka
makinedeki işler, son fetch'ten sonra itilen dallar ve kayıtlı olmayan görev kaynakları görülmedi (keşif raporunun EKSİK satırları).

**Ebeveyn / FU kararı:** yeni modül değildir. İş MOD-0178'in S2 outbound ekranının ve outbound kaydının genişlemesidir (Blueprint'te ayrı
yetenek yok) → **MOD-0178'in FU çocuğu**. Registry'de FU satırı yoktur; satırı CT ekler (FU-0178-FU01-REG — `execution/registries/**`
bu paketin yazma alanı değildir). Emsal: MOD-0177-FU01 registry satırını CT ekledi.

## 1. Module Summary

E3 canlı turunda (2026-11-13) çıkış sevkiyatı formu uçtan uca çalıştı, ama form satırları serbest düzenli kartlar olarak tutuyor
(`frontend/Diten.Web/wwwroot/assets/js/Warehouse/OutboundShipments/form.js:72-88`), alıcıyı yalnız elle aldırıyor
(`frontend/Diten.Web/Views/Warehouse/OutboundShipments/_Form.cshtml:97-129`) ve "Tahsisi önizle" seçilen lotu yok sayıyor: önizleme her
satırı `lotId: ''` ile gönderiyor (`form.js:198`; WP-0178-E3-FIX-01 madde 4).

Bu FU formu üç adımda yeniler:

1. **DL-1 — satırlar DataTable'da, ekleme / düzenleme yan panelde (D-1, D-4).** Satır listesi Golden Reference DataTable v2 deseninde
   (istemci modu, en çok 100 satır); satır ekleme, düzenleme ve lot değişimi Golden Slim `_CreateEditOffcanvas.cshtml` deseninde bir yan
   panelde. Önizleme hem panelde (tek satır) hem tabloda (bütün satırlar) görünür ve **seçilen lotu gösterir**. Yalnız Web; servis ve
   sözleşme değişmez.
2. **DL-2 — outbound kaydında CRM müşteri kimliği (D-2, sunucu + sözleşme).** Outbound isteğe bağlı `customerRef` (MOD-0149
   `Account.Id`) taşır; varsa InventoryService kaydetmeden önce CRM-ACCOUNT-LOOKUP U-1 ile doğrular. Adres her zaman **o anki kopya**dır
   (`shipTo`); CRM'den sonradan değişmez. WMS-BUNDLE ve WAREHOUSE-OUTBOUND'a eklemeli üye.
3. **DL-3 — alıcıyı CRM'den seçme (D-2, Web).** Alıcı kartında CRM müşteri seçici: ad / kodla arama, seçince ad ve adres alanları
   CRM-ACCOUNT-LOOKUP 1.1.0'ın yeni `GET /api/crm/accounts/{id}/ship-to` ucundan doldurulur (CRM cevabı C-1…C-5); ülke / şehir / ilçe
   **kod** olarak gelir, görünen ad Reference Data'dan çözülür ve kopyaya kod + ad yazılır. CRM 1.1 donmadan bu dilim başlamaz.
   **Elle giriş her dilimde çalışır** — CRM kapalıyken de.

Sipariş referansı v1'de elle kalır (D-3); MOD-0168 Order Capture gelince "siparişten seç, yoksa elle gir" bağlaması ayrı FU'dur
(FU-0178-FU01-ORDER). **Mockup turu yoktur** (D-5): ekran düzeni Golden Reference'ın mevcut desenlerinden türetilir.

Hedef kullanıcı: sevkiyat planlayıcısı / depo sorumlusu (`ShippingPlanner`, `WarehouseSupervisor` rolleri — ebeveyn §14.3). Kiracı
modülüdür (`_LayoutTenantShell`, 7 dil).

## 2. Ownership and Boundaries

| Konu | Sahip | Bu paketin payı |
|---|---|---|
| Outbound ekranı (Create / Edit / Details), satır paneli | MOD-0178 (bu FU) | yazar |
| `OutboundShipment` kaydı, tahsis, önizleme kuralları | MOD-0178 (ebeveyn §8.17) | DL-2'de yalnız `customerRef` üyesini ekler; tahsis / FEFO / önizleme kuralına dokunmaz |
| WMS-BUNDLE, WAREHOUSE-OUTBOUND | MOD-0178 (CT tek yazıcı adımı) | eklemeli üyeleri tanımlar; dosyaları CT adımı yazar (§8.7) |
| CRM hesabı, CRM-ACCOUNT-LOOKUP sözleşmesi (1.1 ship-to ucu dahil) | MOD-0149 (commercial-suite, CrmService 5061; sahibi Beste — CRM) | yalnız tüketici tarafı (CRM cevabı C-1…C-5); sözleşmeye ve CRM manifestine yazmaz |
| Ülke / şehir / ilçe kodlarının görünen adı (Reference Data) | Reference Data sahibi (Q-11) | yalnız okur |
| `crm.account.lookup-validation` ve `crm.account-ship-to.read` anahtarlarının depo rollerine eşlemesi | Auth (MOD-0018, Ali) | bağımlılık (XMC-001); dokunmaz |
| Sevkiyat (Shipment) ve `customerRef` eşlemesi | MVP-6 (MOD-0183, Natig) | bilgi + soru (Q-6); MVP-6 dosyalarına yazmaz |
| Sipariş (Order) | MOD-0168 (commercial-suite, planlı) | yok — FU-0178-FU01-ORDER |
| Gateway rotaları | integration-agent | değişiklik yok (§15) |

**Sınır kuralları**
- InventoryService CRM veritabanını okumaz; CRM'i yalnız Gateway (5000) üzerinden, **oturumdaki kullanıcının token'ıyla** çağırır
  (CRM-ACCOUNT-LOOKUP §5a çağrı yolu; emsal `services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/Recall/CrmAccountLookupHttpReader.cs`).
- Tarayıcı CRM'i ya da Gateway'i doğrudan çağırmaz; Web aynı-origin MVC proxy'si sunucu tarafında Gateway'e gider (ebeveyn §11.5 deseni).
- CRM'den gelen ad / adres **yalnız formun ön doldurmasıdır**; kayda giden, kullanıcının onayladığı `shipTo` kopyasıdır. Sunucu `shipTo`'yu
  CRM'le karşılaştırmaz, CRM'den doldurmaz.
- Bu FU tahsis algoritmasını, FEFO kuralını, durum makinesini, izin anahtarlarını ve kayıt kodlarını değiştirmez.

## 3. Owned Objects

Yeni koleksiyon, yeni komut, yeni izin anahtarı yok. Değişen nesneler:

| Nesne | Yer | Değişiklik | Dilim |
|---|---|---|---|
| Outbound Create / Edit ekranı | `Views/Warehouse/OutboundShipments/_Form.cshtml`, `form.js`, `rules.js` | satır tablosu + satır paneli + önizleme gösterimi | DL-1 |
| `OutboundShipment` | `wms_outbound_shipments` | eklenen alan `CustomerRef` (§4) | DL-2 |
| `CreateOutboundShipmentCommand`, `UpdateOutboundShipmentCommand`, `PreviewOutboundAllocationQuery` | InventoryService | gövdeye `customerRef`; güncellemeye `removeCustomerRef` (§8.5) | DL-2 |
| `WarehouseErrorCodes` | InventoryService | eklenen kodlar `WMS_CUSTOMER_NOT_FOUND`, `WMS_CUSTOMER_NOT_ACTIVE` (Q-2 = A ise), `WMS_CUSTOMER_DIRECTORY_FORBIDDEN`, `WMS_CUSTOMER_DIRECTORY_UNAVAILABLE` | DL-2 |
| WMS-BUNDLE, WAREHOUSE-OUTBOUND | `docs/analysis/contracts/` | eklemeli `customerRef` (§8.7) | DL-2 (CT adımı) |
| `OutboundAddress` (`shipTo` kopyası) | `wms_outbound_shipments` | eklenen isteğe bağlı kod / ad üyeleri (§4.1) | DL-2 |
| Alıcı kartı (CRM seçici) + Web proxy eylemleri `customers`, `customer-ship-to` | `_Form.cshtml`, `form.js`, `OutboundShipmentsController.cs` (Web) | seçici, ön doldurma (kod → ad), bağlantıyı kaldırma | DL-3 |
| Details özet kartı | `Details.cshtml`, `details.js` | "CRM müşterisi" rozeti (GUID gösterilmez) | DL-3 |

## 4. Entity Fields

### 4.1 `OutboundShipment` eklenen alan (DL-2)

| Alan | Tip | Kural |
|---|---|---|
| `CustomerRef` | `Guid?` | MOD-0149 CRM `Account.Id`. `null` = alıcı elle girildi **ya da** kayıt FU01'den önce yazıldı (ayırt edilmez; geri doldurma yok). Sözleşmede kanonik küçük harf "D" GUID; girişte büyük / küçük harf duyarsız (emsal RECALL 1.10.0). Yazıldığı anda CRM U-1 ile doğrulanmıştır (§8.5); sonradan CRM'de silinse / pasifleşse de kayıt değişmez. Yerel zaman çizelgesinin `Data`'sına **değer yazılmaz**, yalnız `customerRefChanged` / `customerRefEntered` işareti (ebeveyn kuralı: `ShipTo` değeri `Data`'da tutulmaz — `services/Diten.InventoryService/src/Diten.InventoryService.Domain/Warehouse/Entities/OutboundShipmentEntities.cs:277`). |

### 4.1a `OutboundAddress` (`ShipTo`) eklenen üyeler (DL-2; CRM cevabı C-2)

`ShipTo` (`OutboundAddress`: `Name`, `Country`, `City`, `PostalCode`, `Line1`, `Line2` — `OutboundShipmentEntities.cs:13-28`) CRM'den
seçilen alıcıda da kullanıcının kaydettiği **kopyadır** (D-2). CRM 1.1 ship-to ucu ülke / şehir / ilçeyi **kod** döndürür, görünen adı WMS
Reference Data'dan çözer ve kopyaya **kod + ad** yazar (C-2). Bugünkü üyeler anlamını korur; eklenenler isteğe bağlıdır:

| Üye | Tip | Kural |
|---|---|---|
| `Country` (var) | string | donmuş `Address.country` = "lowercase country code (Territory ile uyumlu)" (`docs/analysis/contracts/warehouse-outbound.openapi.yaml:81`) — CRM `CountryRef` kodu buraya küçük harfle yazılır |
| `CountryName` (yeni) | string? ≤ 200 | ülkenin görünen adı, kayıt anındaki kullanıcı dilinde (ad dile bağlıdır; kopya o anın adıdır) |
| `City` (var) | string? | şehrin görünen adı (kayıt anındaki dil); elle girişte serbest metin |
| `CityRef` (yeni) | string? ≤ 100 | CRM `CityRef` kodu; yalnız CRM'den seçilen alıcıda dolu |
| `DistrictRef` (yeni) | string? ≤ 100 | CRM `DistrictRef` kodu; yalnız CRM'den seçilen alıcıda dolu |
| `District` (yeni) | string? ≤ 200 | ilçenin görünen adı (kayıt anındaki dil) |
| `PostalCode` (var) | string? | CRM'de posta kodu **yok** (C-2) → CRM'den seçilen alıcıda boş gelir, kullanıcı elle ekleyebilir (Q-10 sahip kararı bekliyor) |
| `Line1` (var) | string? | CRM `AddressLine` |

Kod kümesi CRM 1.1 sözleşmesinde yazılacaktır (C-2); sunucu kodları Reference Data'ya karşı doğrulamaz (öneri — kopyanın kaynağı
kullanıcının onayladığı formdur; Q-11). Elle girişte kod üyeleri boş kalır. Yerel olay `Data`'sına adres değeri yazılmaz (ebeveyn kuralı).

### 4.2 İndeks

Yeni indeks yok. `CustomerRef` ile liste süzgeci bu FU'da yoktur (FU-0178-FU01-FILTER).

## 5. Repo Scope

**DL-1 (yalnız Web):**
- `frontend/Diten.Web/Views/Warehouse/OutboundShipments/_Form.cshtml` — "Satırlar" kartı: tablo + araç çubuğu; yan panel partial'ı.
- `frontend/Diten.Web/Views/Warehouse/OutboundShipments/_LineOffcanvas.cshtml` (**yeni**) — satır paneli (Golden Slim `_CreateEditOffcanvas.cshtml` deseni).
- `frontend/Diten.Web/Views/Warehouse/OutboundShipments/Create.cshtml`, `Edit.cshtml` — yalnız partial / betik bağlantıları (Layout değişmez).
- `frontend/Diten.Web/wwwroot/assets/js/Warehouse/OutboundShipments/form.js` — satır modeli (bellekte dizi), tablo, panel, önizleme.
- `frontend/Diten.Web/wwwroot/assets/js/Warehouse/OutboundShipments/rules.js` — saf kurallar (satır doğrulama, `lotNeed`, önizleme eşleme, 100 sınırı).
- `frontend/Diten.Web/wwwroot/assets/js/Warehouse/OutboundShipments/index.l10n.js`, `_IndexL10n.cshtml`, `Resources/Views/Warehouse/OutboundShipments/OutboundShipmentsIndex.{en,tr,fr,es,zh,ar,ru}.resx` — yeni anahtarlar.
- `frontend/Diten.Web/wwwroot/assets/css/Warehouse/warehouse.css` — gerekirse panel / rozet sınıfları (yeni CSS dosyası yok — ebeveyn §11.5).
- `frontend/Diten.Web/tests/**` — DL-1 testleri (vitest; §17).

**DL-2 (servis + sözleşme):**
- `services/Diten.InventoryService/src/Diten.InventoryService.Domain/Warehouse/Entities/OutboundShipmentEntities.cs` — `CustomerRef`; `OutboundAddress` kod / ad üyeleri (§4.1a).
- `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/OutboundShipments/**` — `OutboundShipmentModels.cs` (giriş / görünüm), `Services/OutboundAllocationPlanner.cs` (yalnız `ShapeError` — `customerRef` biçimi; plan değişmez), `Handlers/CommandHandlers/CreateOutboundShipmentHandler.cs`, `UpdateOutboundShipmentHandler.cs` (CRM doğrulaması, olay işareti), `Services/OutboundShipmentReads.cs` (görünüm + donmuş GET üyesi).
- `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/Warehouse/Ports/IWarehouseCustomerDirectory.cs` (**yeni** port) ve `services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/Warehouse/WarehouseCustomerDirectory.cs` (**yeni**; U-1 okuyucusu — `Recall/CrmAccountLookupHttpReader.cs` deseni; Recall dosyası değişmez, ortak kod çıkarılırsa XMC-001 sorusu §19.5 Q-9).
- `services/Diten.InventoryService/src/Diten.InventoryService.Api/Warehouse/WarehouseServiceCollectionExtensions.cs` — port kaydı.
- `WarehouseErrorCodes` (dört kod) ve hata kataloğu testi.
- `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Warehouse/**` — DL-2 testleri.
- `frontend/Diten.Web/Models/Warehouse/OutboundDtos.cs`, `frontend/Diten.Web/Controllers/OutboundShipmentsController.cs` — proxy gövdesine `customerRef` / `removeCustomerRef` geçişi (seçici yok).
- `docs/analysis/contracts/wms-bundle.openapi.yaml`, `docs/analysis/contracts/warehouse-outbound.openapi.yaml` — **yalnız CT'nin tek yazıcı adımıyla** (§8.7); geliştirici bu dosyaları yazmaz.

**DL-3 (Web seçici):**
- `frontend/Diten.Web/Controllers/OutboundShipmentsController.cs` — `GET /Warehouse/OutboundShipments/customers?q=` (U-3) ve `GET /Warehouse/OutboundShipments/customer-ship-to/{accountId}` (CRM 1.1 `GET /api/crm/accounts/{id}/ship-to` + Reference Data ile kod → ad çözümü, kullanıcı dilinde).
- `_Form.cshtml`, `form.js`, `rules.js`, `Details.cshtml`, `details.js` (yalnız özet kartı rozeti), resx / l10n (7 dil), testler.

## 6. Protected Paths

- `gateway/Diten.ApiGateway/ocelot.json` — değişmez (§15); yalnız `integration-agent`.
- `services/Diten.CrmService/**`, `docs/analysis/contracts/crm-account-lookup.md`, `docs/analysis/contracts/drafts/crm-account-lookup-validation-v1.0-proposal.md` — CRM sahibinin (1.1 ship-to ucu dahil — C-1…C-5).
- `services/Diten.InventoryService/src/**/Recall/**` — MOD-0177'nin; okunur, değiştirilmez.
- MVP-6 dosyaları (`services/Diten.SupplyChainService/**`, `shipment-bundle.openapi.yaml`) — MOD-0183'ün.
- `services/Diten.AuthService/**` — rol eşlemesi Ali'nin WP'si (Q-5).
- `frontend/Diten.Web/wwwroot/assets/js/Warehouse/OutboundShipments/details.js` modal kodu — WP-0178-E3-FIX-01 madde 1'in; FU01 yalnız DL-3 özet rozetine dokunur.
- `.antigravity/**`, `execution/registries/**`, `execution/approvals/**` — CT / kullanıcı onayı.
- `docs/analysis/contracts/wms-bundle.openapi.yaml`, `warehouse-outbound.openapi.yaml` — yalnız CT tek yazıcı adımı.
- `_Layout.cshtml`, archive — donuk.

## 7. Dependencies

| Bağımlılık | Tür | Durum (ölçüldü) | Kullanım |
|---|---|---|---|
| WP-0178-E3-FIX-01 | DL-1 **başlama kapısı** | WP yazıldı, kod yok (`execution/domains/supply-chain-execution/work-packs/mvp5-wms/WP-0178-E3-FIX-01-ui-l10n.md`) | aynı dosyalar (`form.js`, `_Form.cshtml`, resx); sayı biçimleyicisi (madde 2) ve "Çıkış sevkiyatı" terimleri (madde 3) FU01'de yeniden kullanılır; madde 4'ün en dar düzeltmesi FU01'den önce canlıya gider (§8.1) |
| MOD-0178 S2 servis (önizleme, oluşturma, düzenleme) | HARD | main'de (ebeveyn DL-4…DL-6, PR #397) | `POST /outbound-shipments/allocation-preview` gövdesi oluşturma gövdesidir ve `lotOverride` taşır (`docs/analysis/contracts/wms-bundle.openapi.yaml:1990-2001`, satır şeması `:1953-1977`); servis önizlemede lot değişimini uygular ve sapmayı reddetmeden bildirir (`services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/OutboundShipments/Services/OutboundAllocationPlanner.cs:233-236`, `:431-458`) — DL-1 için **sözleşme değişikliği gerekmez** |
| CRM-ACCOUNT-LOOKUP 1.0.0 U-1 | DL-2 HARD | FROZEN, main'de (`docs/analysis/contracts/crm-account-lookup.md`, `x-implemented` 38f7ea08d) | kayıtta `customerRef` doğrulaması |
| CRM-ACCOUNT-LOOKUP 1.0.0 U-3 | DL-3 HARD | FROZEN, main'de | seçici araması (ad / kod) |
| **CRM-ACCOUNT-LOOKUP 1.1.0 — `GET /api/crm/accounts/{id}/ship-to`** | **DL-3 başlama kapısı = 1.1 donması** | **depoda yok** — bu depodaki metin 1.0: U-3 öğeleri adres taşımaz (`crm-account-lookup.md:150`), U-1 yalnız doğrulama alanları (`services/Diten.CrmService/src/Diten.CrmService.Application/Features/Account/AccountModels.cs:86-94`), K-9 açık (`crm-account-lookup.md:351`); hesapta adres var (`services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/Account.cs:27-30`: `CountryRef`, `CityRef`, `DistrictRef`, `AddressLine`; posta kodu yok). **CRM sahibinin cevabı (C-1…C-5, Beste, CRM oturumu, 2026-11-13):** yalnız ekleme; yeni uç; kiracı sınırı; 404 (yok / silinmiş / başka kiracı aynı cevap); `tenant_id`'siz token → 400; 5xx → 503; adres U-1 / U-3'e **eklenmez**; ülke / şehir / ilçe kod döner; v1 tek adres; anahtar `crm.account-ship-to.read`; sıra 1.1 taslağı → inceleme → kod → donma; **tarih TBD** | sahibi Beste (CRM) — CRM başka bir bilgisayarda geliştiriliyor |
| Reference Data (ülke / şehir / ilçe kod → görünen ad) | DL-3 HARD | kaynak ve uç bu pakette ölçülmedi (Q-11) | ön doldurmada kodların kullanıcının dilindeki adı |
| MOD-0018 Auth rol eşlemesi | DL-2 / DL-3 kabul kapısı | yok | `crm.account.lookup-validation` (K-1, `crm-account-lookup.md:346`; U-1 doğrulama + U-3 arama) ve `crm.account-ship-to.read` (C-4; anahtarı CRM manifestine CRM ekler) → `ShippingPlanner`, `WarehouseSupervisor` (Q-5, Ali) |
| WAREHOUSE-OUTBOUND / WMS-BUNDLE sonraki eklemeli sürüm | DL-2 yayın kapısı | WAREHOUSE-OUTBOUND 1.0.0 FROZEN (`docs/analysis/contracts/warehouse-outbound.openapi.yaml:12-13`); WMS-BUNDLE 1.1.0 (`wms-bundle.openapi.yaml:64`); Item adımı (WAREHOUSE-OUTBOUND 1.1.0 + WMS-BUNDLE 1.2.0) MOD-0183 r10 onayını bekliyor (ebeveyn §19.7) | §8.7; Q-7 |
| MOD-0183 (MVP-6) | XMC-001 bilgi + soru | r9 taslak; okuma şeması açık, eklenen alanı anlık görüntüye yazar (`execution/domains/supply-chain-execution/module-packs/MOD-0183-shipment-tracking-pod.md:256-262`); intake `customerRef` yazmıyor (`MOD-0183-shipment-tracking-pod.md:934`) | Q-6 (Natig) |
| MOD-0168 Order Capture | yok (FU) | registry `reserved / planned` (`execution/registries/module-id-registry.md:301`); paket ve kod yok | FU-0178-FU01-ORDER |

## 8. Runtime Constraints

### 8.1 Sıra ve FIX-01 ile çakışmama

- **WP-0178-E3-FIX-01 önce gider.** FU01 DL-1, FIX-01'in CT kabulü ve CT hattına birleşmesinden **sonra** başlar; FU01 dalı FIX-01'li tabandan
  açılır. FIX-01'in dört maddesi FU01'de **yeniden yazılmaz**:
  - madde 1 (onay modalları, `details.js`) — FU01 dokunmaz;
  - madde 2 (kültüre göre sayı biçimi) — FU01 tablo ve panelde **aynı biçimleyiciyi** kullanır; yeni biçimleyici yazmaz;
  - madde 3 ("Outbound" → "Çıkış sevkiyatı") — FU01'in yeni anahtarları FIX-01'in terimini kullanır;
  - madde 4 (önizleme seçilen lotu yok sayıyor) — FIX-01 bugünkü formda düzeltir; FU01 aynı davranışı yan panele ve tabloya taşır (AC-5, AC-6).
- FIX-01 kabul edilmeden FU01 kodu başlarsa iki iş aynı satırları (`form.js:193-205`, `_Form.cshtml:72-94`) değiştirir — bu paket bunu
  yasaklar (§24 DL-1 kapısı).

### 8.2 Satır tablosu

Dilim DL-1; sahip kararı D-1.

- Create sayfasının "Satırlar" kartı bugünkü kart listesinin (`_Form.cshtml:82-91`) yerine bir DataTable taşır:
  `<table id="outbound-lines-table" data-dt-standard="v2" data-dt-data-mode="client">` — **istemci modu**, kaynak sayfanın bellekteki satır
  dizisi (kaydedilmemiş satırlar sunucuda yoktur). Üst sınır **100 satır** (servis sınırı `maxItems: 100`, `wms-bundle.openapi.yaml:1989`);
  101. satır eklenemez ("Satır ekle" pasif + açıklama).
- **Sütunlar:** sıra (sipariş satırı no), ürün (ad — kod), SKU, miktar + birim, lot (seçilen lot ya da "FEFO" + önerilen lot no), alınabilir,
  durum rozeti, satır menüsü. Kimlik (GUID) gösterilmez.
- **Durum rozeti** (yalnız renge dayanmaz; ikon + metin): `Önizlenmedi` · `Önizleme eski` · `Karşılanır` · `Karşılanamaz` (sunucu
  `failureCode`'u satır altında) · `FEFO sapması — gerekçe gerekli` / `FEFO sapması — gerekçe var` / `FEFO sapması — izin yok (Block)`.
- **Araç çubuğu:** "Satır ekle" (paneli boş açar) ve "Tahsisi önizle" (bütün satırlar, §8.3).
- **Satır menüsü:** Düzenle (paneli o satırla açar), Lot değiştir (paneli lot alanında açar; satır önizlenmemişse önce tek satır önizlemesi),
  Sil (yerel; premium onay `window.showConfirm` — MOD-0013; kayıtlı bir şey silinmez).
- **Boş durum:** "Henüz satır yok — Satır ekle". Yükleniyor durumu: önizleme sırasında tablo iskelet satırı + düğmeler pasif.
- **Edit sayfası:** aynı tablo **salt okunur** — satır menüsü ve araç çubuğu yok; sütunlar kayıttan (lot, tahsis miktarı, rezervasyon durumu).
  Satırlar ve tahsis düzenlemede değişmez (ebeveyn §8.17; bugünkü `LinesLockedHint`).
- 375 px: tablo DataTables duyarlı daraltmayla; yatay sayfa kaydırması yok.

### 8.3 Satır yan paneli ve önizleme

Dilim DL-1; sahip kararları D-1 ve D-4.

- **Panel alanları (6, Golden Slim offcanvas deseni — başlıksız, her alan `.diten-field` + ikon):**
  1. Ürün (select2, `dropdownParent` = panel; kaynak bugünkü `/Warehouse/OutboundShipments/products`)
  2. SKU (ürüne bağlı; ürün değişince SKU ve birim **temizlenir**, select2 yeniden eşitlenir — `frontend-form-template.md` bağımlı select kuralı)
  3. Miktar (+ salt okunur baz birim, bugünkü `/sku-facts`; ondalık giriş kültüre göre — FIX-01 madde 2)
  4. Sipariş satırı no (varsayılan: sıradaki boş numara; satırlar arasında tekil — istemci kontrolü)
  5. Lot (seçenekler son önizlemenin o satır için döndürdüğü serbest `lots` listesi: lot no, FEFO sırası, SKT, alınabilir; ilk seçenek
     "FEFO'ya göre (önerilen: {lot no})"). Liste yalnız uygun lotları taşır; geri çağrılmış / süresi geçmiş / bloke lot seçilemez.
  6. FEFO sapma gerekçesi (yalnız seçilen lot ilk serbest lot değilse görünür; zorunluluk şirketin FEFO kipine göre: `Block` → lot
     değişimi yapılamaz, panel açıklar ve "Satırı kaydet" pasif; `RequireReason` → 10–1000 karakter zorunlu; `Warn` → isteğe bağlı, ≤ 1000).
- **Panel düğmeleri:** "Satırı önizle" (`btn-label-primary`), birincil "Satırı kaydet" (`btn-primary`, `data-dt-primary="enter"` — yerel,
  geri alınabilir; `page-help-and-keyboard.md` §3), "Vazgeç". Panel kaydı yalnız bellekteki diziyi değiştirir; sunucuya yazmaz.
- **Panel önizlemesi (tek satır):** `POST /Warehouse/OutboundShipments/preview` — başlık (depo, Staging) + **yalnız bu satır**; lot seçildiyse
  `lotOverride { lotId, fefoBypassReason? }` ile. Panel gösterir: FEFO önerisi (ilk serbest lot), **seçilen lot ve ondan yapılan tahsis**,
  alınabilir miktar, sapma bloğu (`deviation.firstEligibleLotNumber`, SKT, kip). Etiket: "Tek satır önizlemesi — kayıtta bütün satırlarla
  yeniden hesaplanır" (ebeveyn §8.17 adım 7: önizleme rezervasyon değildir).
- **Tablo önizlemesi (bütün satırlar):** aynı uç, bütün satırlar ve her satırın `lotOverride`'ı ile. Her satırın lot / alınabilir / durum
  sütunu cevaptan dolar; seçilen lotu olan satırda lot sütunu **seçilen lotu** gösterir (D-4).
- **Sunucu reddi:** önizleme dışlanmış bir lot değişiminde bütün isteği 422 ile reddeder (`OutboundAllocationPlanner.cs:436-450`) — ekran
  hatayı `details.lineSeq` ile ilgili satıra bağlar, öbür satırları `Önizlenmedi` bırakır. `WMS_STAGING_NOT_CONFIGURED`,
  `WMS_TARGET_NOT_ALLOWED` gibi başlık hataları Staging alanının altında gösterilir (FIX-01 madde 4 ikinci paragraf).
- **Eskime:** depo, Staging ya da bir satırın ürün / SKU / miktar / lotu değişince ilgili önizleme `Önizleme eski` olur. Seçilmiş lot
  silinmez; yeniden önizlemede lot artık listede yoksa satır `Karşılanamaz` + "Seçilen lot artık uygun değil" — sessiz sıfırlama yok.
- **Kaydet:** istemci kuralları (en az 1 satır, her satır tam, sapma kuralı son önizlemeye göre — bugünkü `rules.lotNeed`) geçerse gövde
  bugünkü sözleşmeyle gider (`CreateOutboundShipmentRequest`, `wms-bundle.openapi.yaml:1978-1989`); sunucu tahsisi yeniden hesaplar ve
  karar odur. `422 WMS_ALLOCATION_FAILED` `details.lines[]` satır rozetlerine eşlenir.

### 8.4 Alıcı seçimi

Dilim DL-3 (CRM müşterisi ya da elle giriş); sahip kararı D-2, CRM cevabı C-1…C-5.

- Alıcı kartının üstünde **"CRM müşterisi"** seçici (select2, en az 2 karakter): Web proxy `GET /Warehouse/OutboundShipments/customers?q=`
  → sunucu tarafında Gateway → CRM U-3 `GET /api/crm/accounts/lookup?search=&page=&pageSize=20&billableOnly=` (kullanıcı token'ı).
  Öğe: ad — kod (+ durum rozeti, Q-2'ye göre).
- Seçince Web proxy `GET /Warehouse/OutboundShipments/customer-ship-to/{accountId}` → sunucu tarafında Gateway → CRM 1.1
  `GET /api/crm/accounts/{id}/ship-to` (kullanıcının token'ı; anahtar `crm.account-ship-to.read` — C-1, C-4). Uç ülke / şehir / ilçeyi
  **kod** döner (C-2); proxy kodların görünen adını **Reference Data**'dan kullanıcının dilinde çözer (Q-11) ve forma kod + ad verir. Form
  doldurur: Alıcı adı (= hesap adı), Ülke (kod + ad), Şehir (ad; kod gizli alanda), İlçe (ad; kod gizli alanda — yeni alan, §4.1a),
  Adres 1 (`AddressLine`). **Posta kodu boş gelir** (account'ta yok — C-2); kullanıcı elle ekleyebilir (Q-10 sahip kararı bekliyor).
  Ad / adres alanları **düzenlenebilir** (Q-3 önerisi A); altta "CRM kaydından dolduruldu ({kod}). Değişiklik yalnız bu sevkiyata yazılır."
  Kullanıcı CRM'den gelen şehir / ilçe adını değiştirirse ilgili kod gönderilmez (ad ile kod çelişmez).
- **v1 tek adres** (account adresi, C-3). Çoklu teslimat adresi CRM'in ileriki 1.x eklemesidir (adres listesi + isteğe bağlı adres kimliği);
  WMS account kimliği + adres kopyası tuttuğu için kırılma olmaz (FU-0178-FU01-MULTIADDR).
- **CRM ship-to cevapları (1.0 kurallarıyla aynı — C-1):** 404 (yok / silinmiş / başka kiracı) → "CRM kaydı bulunamadı — alıcıyı elle girin",
  bağlantı kurulmaz; 401 / 403 → "CRM adres okuma yetkiniz yok"; 400 (`tenant_id`'siz token) ve 5xx / 503 → "CRM'e şu an ulaşılamıyor".
  Reference Data adı çözemezse kod ve "Ad okunamadı" yazılır, kullanıcı adı elle düzeltebilir.
- "Bağlantıyı kaldır": `customerRef` gönderilmez, alanlar olduğu gibi kalır (elle giriş).
- **Elle giriş her zaman çalışır:** seçici kullanılmadan alanlar doğrudan yazılır; CRM çağrısı yapılmaz; kayıt `customerRef`'siz gider.
- **CRM hata durumları (seçicide):** 401 / 403 → "CRM müşteri arama yetkiniz yok — alıcıyı elle girin"; 5xx / zaman aşımı → "CRM'e şu an
  ulaşılamıyor — alıcıyı elle girin". İki durumda da elle alanlar açık kalır. Ship-to okuması 403 / 5xx ile başarısızsa ad (U-3
  satırından) doldurulur, adres alanları boş kalır + uyarı; bağlantı korunur (hesap kimliği U-3'ten geçerli).
- **Ülke alanı elle girişte de kod seçicidir (öneri, DL-3):** donmuş `Address.country` kod beklediği için (§4.1a) ülke Reference Data
  seçicisinden seçilir (kod + ad); şehir / ilçe elle girişte serbest metin kalır (kod yok). DL-3'ten önce (DL-1 / DL-2) ülke bugünkü gibi
  serbest metindir — bu davranış FU01'den önce de böyledir (`_Form.cshtml:106-109`).
- **Kayıtta CRM hatası:** `customerRef`'li kayıt 503 `WMS_CUSTOMER_DIRECTORY_UNAVAILABLE` ya da 403 `WMS_CUSTOMER_DIRECTORY_FORBIDDEN`
  alırsa form uyarıyı ve **"Bağlantıyı kaldırıp elle kaydet"** eylemini gösterir; eylem `customerRef`'siz ve **yeni** istek anahtarıyla
  yeniden gönderir (aynı anahtar farklı gövdeyle 400 olur — ebeveyn §8.1).
- **Edit:** düzenlenebilir durumlarda (Allocated / Picked / Packed) müşteri seçilebilir, değiştirilebilir, bağlantı kaldırılabilir; adres
  kopyası yalnız kullanıcı "CRM'den yeniden al" derse yenilenir.
- **Details:** özet kartında `shipTo` kopyası + `customerRef` varsa "CRM müşterisi" rozeti. Ayrıntıda CRM çağrısı yok, GUID gösterilmez.

### 8.5 Sunucu tarafında müşteri kimliği

Dilim DL-2 (`customerRef`); sahip kararı D-2.

1. **Giriş:** `CreateOutboundShipmentRequest` ve `PreviewOutboundAllocationRequest` isteğe bağlı `customerRef` (Uuid);
   `UpdateOutboundShipmentRequest` isteğe bağlı `customerRef` (ata / değiştir) ve `removeCustomerRef: true` (kaldır). İkisi birlikte → 400
   `INVALID_REQUEST`. JSON `null` yok (WMS-BUNDLE kural 4) — kaldırma bu yüzden ayrı bayraktır.
2. **Önizleme:** `customerRef`'i kabul eder (gövde oluşturma gövdesidir, `additionalProperties: false`) ama **CRM çağırmaz** ve sonucu etkilemez.
3. **Sıra:** istek anahtarı aranır (ebeveyn §8.1, WMS-BUNDLE kural 5) → **sonra** CRM doğrulaması → sonra ebeveyn §8.17 adım 1'in öbür ön
   kontrolleri. Replay (aynı anahtar + aynı gövde) CRM'i çağırmaz.
4. **Doğrulama:** `IWarehouseCustomerDirectory.ValidateAsync(accountId)` → Gateway → U-1 `GET /api/crm/accounts/{accountId}/lookup-validation`,
   çağıranın token'ıyla, önbelleksiz, tek çağrı (zaman sınırı öneri 3 sn — Recall sayfa bütçesi emsali):
   - 200 + `status = active` → geçer;
   - 200 + başka durum → 422 `WMS_CUSTOMER_NOT_ACTIVE` (**Q-2 = A ise**; B ise geçer);
   - 404 → 422 `WMS_CUSTOMER_NOT_FOUND` (yok / silinmiş / başka kiracının — CRM üçünü aynı cevaplar);
   - 401 / 403 → 403 `WMS_CUSTOMER_DIRECTORY_FORBIDDEN` (emsal RECALL 1.10.0 `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN`);
   - diğer her şey (400, 5xx, zaman aşımı, geçersiz gövde, ulaşılamaz) → 503 `WMS_CUSTOMER_DIRECTORY_UNAVAILABLE`.
   Ret yazımsızdır: kayıt, rezervasyon, yerel olay, merkezi denetim yok (0 / 0 / 0).
5. **Yazım:** oluşturmada işlem A'da `CustomerRef` ve `ShipTo`'nun kod / ad üyeleri (§4.1a; `ShipToAddressInput`'a eklemeli `countryName`,
   `cityRef`, `districtRef`, `district`) (ebeveyn §8.17 adım 5); güncellemede başlık yazımında, `Version` koşullu. Yerel olay
   `Created` / `Updated` `Data`'sına yalnız `customerRefEntered` / `customerRefChanged: true` (değer yok).
6. **Okuma:** `OutboundShipmentView.customerRef` (`wms-bundle.openapi.yaml:1757-1771` şemasına eklemeli) ve donmuş `getOutboundShipment` /
   `listOutboundShipments` `OutboundShipment.customerRef` — **yalnız doluysa yazılır**, hiç `null` yazılmaz. Arama listesi
   (`OutboundShipmentListItem`) değişmez.
7. **Eski kayıtlar:** geri doldurma, göç, otomatik eşleme yok.

### 8.6 Sipariş referansı

Sahip kararı D-3.

- v1: `orderRef` bugünkü gibi serbest metin (≤ 100, `_Form.cshtml:64-67`); sözleşme değişmez.
- "Alan hazır" anlamı (öneri): DL-1'de alan tek başına bir `.diten-field` içinde, ileride select2 "seç ya da yaz" (etiket girişi) kontrolüne
  dönüşebilecek biçimde durur; görünen metin sipariş numarasıdır. MOD-0168 gelince FU-0178-FU01-ORDER eklemeli `orderId` (MOD-0168 kimliği)
  ekler, `orderRef` görünen numara olarak kalır. v1'de "siparişten seç" düğmesi, ipucu metni ya da boş seçici **çizilmez** (çalışmayan
  kontrol yok).

### 8.7 Sözleşme değişiklikleri ve MVP-6 etkisi

Dilim DL-2 (yayın: CT sözleşme adımı).

| Sözleşme | Bugün | FU01 eki (yalnız ekleme) | Yazan |
|---|---|---|---|
| WMS-BUNDLE | 1.1.0 (`wms-bundle.openapi.yaml:64`); Item adımı 1.2.0 bekliyor | **1.3.0** (Item adımı önce yazılırsa; değilse Q-7): `CreateOutboundShipmentRequest` / `PreviewOutboundAllocationRequest` / `UpdateOutboundShipmentRequest` `customerRef`; `UpdateOutboundShipmentRequest.removeCustomerRef`; `OutboundShipmentView.customerRef`; üst küme `OutboundShipment.customerRef`; `ShipToAddressInput` / `ShipToAddress` / üst küme `Address`'e isteğe bağlı `countryName`, `cityRef`, `districtRef`, `district` (§4.1a); create / update `x-error-codes`'a dört kod | CT tek yazıcı adımı |
| WAREHOUSE-OUTBOUND | 1.0.0 FROZEN; Item adımı 1.1.0 bekliyor | **1.2.0** (aynı koşul): `OutboundShipment.customerRef` (isteğe bağlı; MOD-0149 `Account.Id`; SHIPMENT-BUNDLE 3.5.0 `customerRef` ile **aynı ad ve anlam** — C-5) ve `Address`'e isteğe bağlı `countryName`, `cityRef`, `districtRef`, `district`. `OutboundShipmentReadyEvent` değişmez | aynı CT adımı (üst küme kuralı: iki dosya birlikte) |
| CRM-ACCOUNT-LOOKUP | 1.0.0 FROZEN | **1.1.0** (CRM cevabı C-1…C-5): yeni uç `GET /api/crm/accounts/{id}/ship-to`, anahtar `crm.account-ship-to.read`, ülke / şehir / ilçe kod, kod kümesi sözleşmede; U-1 / U-3 değişmez. Sıra: taslak → inceleme → kod → donma; tarih TBD | CRM sahibi (Beste — CRM) |

**MVP-6'ya etkisi (XMC-001; Q-6, Natig):**
- MOD-0183 WAREHOUSE-OUTBOUND okuyucusu açık şemayla okur ve üreticinin eklediği her alanı anlık görüntüye **sınıflandırmadan** yazar
  (`MOD-0183-shipment-tracking-pod.md:256-262`) → `customerRef` 1.2.0'dan sonra MVP-6 kaynak anlık görüntülerine girer. Bir kimliktir (ad /
  adres değil), ama MOD-0183'ün "yükümlülük 3" beyaz listesinde sınıflandırılmalıdır.
- MOD-0183 intake'i bugün sevkiyata `customerRef` yazmaz (`MOD-0183-shipment-tracking-pod.md:934`; ebeveyn FU-0178-MVP6). FU01'in alanı bu
  eşlemenin kaynağı olabilir: depodan CRM'le seçilmiş bir outbound, MVP-6 sevkiyatına `customerRef` olarak geçerse MOD-0177-FU01 geri
  çağırma ekranı müşteriyi adıyla gösterir. Eşleme MVP-6'nın kararı ve kodudur; FU01 yalnız alanı sunar.
- **Aynı CRM ucu:** MVP-6 da alıcı adresi için aynı `ship-to` ucunu kullanacaktır (CRM cevabı, 2026-11-13); WMS'te tutulan kimlik
  `Account.Id`'dir ve SHIPMENT-BUNDLE 3.5.0 `customerRef` ile uyumludur (C-5) — bu yüzden depo outbound'undan MVP-6 sevkiyatına eşleme dönüşümsüzdür.
- Adres kod üyeleri (`cityRef`, `districtRef`) ve adlar (`countryName`, `district`) de MVP-6 anlık görüntüsüne girer; ad / adres kişisel
  veri sınıfındadır (`shipTo` gibi) — Q-6'nın sınıflandırma maddesi bunları da kapsar.
- Kırılma yok (çıkarım, şemadan): alanlar isteğe bağlıdır, `null` yazılmaz, elle girilmiş outbound'ların cevabı byte olarak değişmez.

## 9. Layout & Shell Contract

- `shell: tenant` → `Views/Warehouse/OutboundShipments/{Index,Create,Edit,Details}.cshtml` Razor bloğunda **`Layout = "_LayoutTenantShell";`
  AÇIKÇA** yazılır (bugün `Create.cshtml` böyle — ebeveyn §9; partial'lar hariç). `_LineOffcanvas.cshtml` partial'dır, Layout taşımaz.
- **Golden Reference kararı (alan sayımı):**

| Ekran | Kullanıcı alanları | Sayı | Karar |
|---|---|---|---|
| Outbound Create / Edit (başlık) | depo, Staging, sipariş ref., CRM müşterisi (DL-3), alıcı adı, ülke, şehir, ilçe (DL-3), posta kodu, adres 1, adres 2, not | 12 (DL-1 / DL-2'de CRM müşterisi ve ilçe yok → 10) | > 8 → **compact** (değişmez; künye `form_field_count: 12`) |
| Satır paneli (offcanvas) | ürün, SKU, miktar, sipariş satırı no, lot, FEFO gerekçesi | 6 | ≤ 8 → Golden **Slim** `_CreateEditOffcanvas.cshtml` deseni, compact sayfa içinde |

  Sayılmayanlar: Id, şirket seçici (sayfa üstü `_CompanySelector`), salt okunur birim ve önizleme alanları.
- UAS-001: `warehouse.outbound-shipments.create` (Create) / `.update` (Edit) yoksa yalnız erişim açıklaması (bugünkü `_AccessDenied`).
- 375 px: tablo, panel (tam genişlik) ve alıcı kartı ölçülür.

## 10. Backend File Convention

DL-2 ebeveynin mevcut `Features/OutboundShipments/` ağacına ekler (Golden Reference klasör düzeni ebeveynde ölçülmüştür, ebeveyn §10); yeni
komut / sorgu / handler **yoktur**. Yeni dosyalar yalnız port ve adaptördür:

```text
services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/Warehouse/Ports/IWarehouseCustomerDirectory.cs   (yeni)
services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/Warehouse/WarehouseCustomerDirectory.cs                (yeni)
```

Adlandırma ebeveynle aynı (`{Verb}{Module}Command` record, `{Verb}{Module}Handler` sealed class — Command / Query soneki yok). Port,
`IWarehouseAssigneeDirectory` (`…/Features/Warehouse/Ports/IWarehouseAssigneeDirectory.cs`, Auth `lookup-validation`) ile aynı desende:
CRM hatasında istisna atmaz, sonucu `Valid | NotActive | NotFound | Forbidden | Unavailable` olarak döner.

## 11. Frontend File Contract

```text
frontend/Diten.Web/Views/Warehouse/OutboundShipments/          (Compact seti — değişmez; ebeveyn §11.5)
├── Index.cshtml · Create.cshtml · Edit.cshtml · Details.cshtml     (Layout = "_LayoutTenantShell" AÇIKÇA)
├── _Form.cshtml                    (DL-1: satır tablosu kartı; DL-3: CRM seçici)
├── _LineOffcanvas.cshtml           (YENİ, DL-1 — Golden Slim _CreateEditOffcanvas deseni; satır paneli)
├── _ActionDialogs.cshtml · _Filter.cshtml · _DataTable.cshtml · _IndexL10n.cshtml
└── OutboundShipmentsIndex.cs
frontend/Diten.Web/wwwroot/assets/js/Warehouse/OutboundShipments/{form.js, rules.js, details.js (DL-3 rozet), index.l10n.js}
frontend/Diten.Web/Resources/Views/Warehouse/OutboundShipments/OutboundShipmentsIndex.{en,tr,fr,es,zh,ar,ru}.resx
frontend/Diten.Web/Controllers/OutboundShipmentsController.cs   (DL-2 gövde geçişi; DL-3 customers, customer-ship-to)
```

**Bilinçli sapma G-FU01-01 (onayla kabul edilir):** Golden Compact'ta `_CreateEditOffcanvas.cshtml` yasaktır (ana kayıt tam sayfa formdur).
FU01'in paneli ana kaydın değil **alt satırın** formudur (D-1); ana kayıt yine tam sayfa Create / Edit'tir. Ad bu yüzden
`_LineOffcanvas.cshtml`'dir. Satır tablosu istemci modunda ve yerel veriyle çalıştığı için dışa aktarma, yazdırma, toplu silme, süzgeç ve
sayfa başına satır seçimi **yoktur** (kaydedilmemiş satırlar için anlamsız); `verify_datatable_page.py` liste sayfası (`Index`) için koşar,
satır tablosu Index değildir — sapma `--format gaps` çıktısında görünürse bu madde gerekçedir.

**Yeni resx anahtarları (öneri; kesin liste DL kabulünde):** `LinesTable.*` (sütun başlıkları, boş durum), `LineStatus.*` (rozetler),
`LinePanel.*` (başlık "Satır ekle" / "Satırı düzenle", alanlar, düğmeler, tek satır önizlemesi notu), `LineLimitReached`, `LotNoLongerEligible`,
`Customer.*` (DL-3: seçici, ipuçları, CRM hata metinleri, "Bağlantıyı kaldır", "CRM'den yeniden al", "Bağlantıyı kaldırıp elle kaydet",
rozet), `Warehouse.Error.WMS_CUSTOMER_*` (DL-2 kodları). Kiracı modülü → **7 dil**; bir dil eksikse dilim bitmemiştir.

## 12. Validation Rules

| Alan | Kural | Yer |
|---|---|---|
| Satır sayısı | 1–100 | istemci (tablo) + sunucu (ebeveyn §8.17 adım 1) |
| Ürün, SKU | zorunlu; SKU ürünün SKU'su | panel + sunucu |
| Miktar | > 0, kültüre göre ondalık giriş; gönderimde nokta ondalık string | panel (FIX-01 biçimleyici) + sunucu |
| Birim | SKU baz birimi (salt okunur) | sunucu 422 `WMS_UOM_NOT_BASE` |
| Sipariş satırı no | zorunlu, ≤ 100, satırlar arasında tekil | panel (tekillik yalnız istemcide — çıkarım: sunucuda tekillik kuralı ölçülmedi) |
| Lot | son önizlemenin serbest `lots` listesinden ya da boş (FEFO) | panel + sunucu (ebeveyn §8.17 adım 3) |
| FEFO gerekçesi | `Block` → lot değişimi yok; `RequireReason` → 10–1000; `Warn` → isteğe bağlı ≤ 1000; > 1000 her kipte 400 | panel + sunucu |
| `customerRef` | isteğe bağlı GUID; varsa CRM U-1 ile geçerli (§8.5) | sunucu (DL-2) |
| `removeCustomerRef` | yalnız güncellemede; `customerRef` ile birlikte → 400 | sunucu |
| Alıcı adı, ülke | zorunlu (donmuş `Address`) — CRM'den gelse de; ülke kod (DL-3'ten itibaren Reference Data seçicisi) | form + sunucu |
| Şehir, ilçe (DL-3), posta kodu, adres 1-2 | isteğe bağlı, ≤ 200; posta kodu CRM'den gelmez (Q-10) | form |
| `countryName`, `district` | isteğe bağlı, ≤ 200; kayıt anındaki dilde ad | form + sunucu (uzunluk) |
| `cityRef`, `districtRef` | isteğe bağlı, ≤ 100; yalnız CRM'den gelen değer; ad elle değiştirilince gönderilmez | form + sunucu (uzunluk) |
| Sipariş ref. | isteğe bağlı, ≤ 100 | form |
| Not | isteğe bağlı, ≤ 1000 | form |

## 13. Failure Path to Verify

| Senaryo | Beklenen |
|---|---|
| Aynı istek iki kez (çift tık, yeniden gönderim) | aynı anahtar + aynı gövde → 200 replay, ikinci outbound yok; CRM ikinci kez çağrılmaz (AC-19) |
| Eksik alan (satırsız, SKU'suz, alıcı adsız) | istemci engeller; sunucu 400 / 422 — kayıt yok |
| Yetkisiz kullanıcı (`.create` yok) | Create'te yalnız erişim açıklaması; doğrudan POST 403 |
| CRM yetkisi yok (`crm.account.lookup-validation` yok) | seçici "yetkiniz yok — elle girin"; `customerRef`'li kayıt 403 `WMS_CUSTOMER_DIRECTORY_FORBIDDEN`; elle kayıt 201 |
| CRM kapalı | seçici "ulaşılamıyor — elle girin"; `customerRef`'li kayıt 503; elle kayıt 201 (AC-18) |
| Başka kiracının hesap kimliği | CRM 404 → 422 `WMS_CUSTOMER_NOT_FOUND` (AC-22); ship-to ucu da 404 → seçicide "bulunamadı" |
| Ship-to anahtarı yok (`crm.account-ship-to.read`) ama arama anahtarı var | seçim yapılır, ad dolar, adres boş + "adres okuma yetkiniz yok"; kayıt `customerRef`'le gider (U-1 ayrı anahtar) |
| Reference Data kodu çözemez | kod + "Ad okunamadı"; kullanıcı adı elle yazabilir |
| Eşzamanlılık (iki kişi aynı outbound'u düzenler) | eski `If-Match` → 409 `WMS_CONFLICT`; ekranda "Kayıt değişti, yenileyin" |
| Önizlemeden sonra stok değişti | kayıt 422 `WMS_ALLOCATION_FAILED`, satır rozetleri güncellenir; outbound yok |
| Seçilen lot geri çağrıldı (önizleme ile kayıt arasında) | önizleme / kayıt 422 `LOT_UNDER_RECALL`, ilgili satır işaretli |
| `ReadyToShip` sonrası müşteri değiştirme | 409 `WMS_OUTBOUND_IMMUTABLE` |

## 14. Authorization Convention

- **Yeni izin anahtarı yok.** Ekran ve uçlar ebeveynin anahtarlarını kullanır: Create / önizleme / müşteri araması (create sayfasında)
  `warehouse.outbound-shipments.create`; Edit / müşteri araması (edit sayfasında) `warehouse.outbound-shipments.update`; okuma `.read`.
  Web proxy eylemi tek anahtar alıyorsa `customers` / `customer-ship-to` her sayfa için ayrı eylemdir (ölçülecek — DL-3 geliştiricinin ilk
  ölçümü); iki anahtardan birini kabul eden ortak eylem yazılmaz.
- **CRM tarafı:** CRM uçları kendi anahtarlarını çağıranın token'ında arar: U-1 / U-3 `crm.account.lookup-validation` (K-1); ship-to ucu
  **`crm.account-ship-to.read`** (C-4; `crm.account.read` değil; anahtarı CRM manifestine CRM ekler). İki anahtarın `ShippingPlanner` ve
  `WarehouseSupervisor` rol şablonlarına eşlenmesi Auth işidir (MOD-0018, Ali; Q-5). Eşleme yoksa sistem çalışır, yalnız CRM seçimi ve
  `customerRef`'li kayıt 403 olur (elle giriş açık).
- Aktör: `tenant_user`; servis-servis token'ı yok (CRM-ACCOUNT-LOOKUP §5a).
- Permission formatı tenant servis için `warehouse.*` (PKS-001); `Platform.*` / `Modules.*` yok.

## 15. Gateway / API Routing Decision

- **Değişiklik yok; `ocelot.json`'a dokunulmaz.** InventoryService'in CRM U-1 çağrısı mevcut `/api/crm/accounts/{everything}` rotasından geçer
  (CRM-ACCOUNT-LOOKUP §8, K-4 "mevcut joker rota yeterli"; MOD-0177-FU01 aynı yolu kullanıyor). CRM 1.1 ship-to ucu
  `/api/crm/accounts/{id}/ship-to` aynı joker rotanın altındadır (C-1) — yeni Gateway rotası gerekmez (çıkarım; CRM sahibinin 1.1 taslağı teyit eder).
- Warehouse uçları değişmez (`/api/warehouse/outbound-shipments/**`); Web proxy aynı-origin MVC.
- integration-agent işi: yalnız teyit (DL-2 kabulünde Gateway üzerinden U-1 çağrısının canlı ölçümü, AC-31).

## 16. Acceptance Criteria

Her guard maddesi **üretim koduna karşı** ölçülür ve adlı sabotaj kanıtı ister: ilgili üretim satırı geçici bozulur, test kırmızıya döner,
bozma geri alınır (kendi kopyasıyla ölçen test guard sayılmaz). Sabotaj kimlikleri `S-FU01-n` biçimindedir.

**DL-1 — satır tablosu, yan panel, önizleme (Web)**

- [ ] AC-1 (zorunlu) — Create sayfasında satırlar `#outbound-lines-table` tablosundadır (`data-dt-standard="v2"`, `data-dt-data-mode="client"`); bugünkü satır kartı girişleri (`form.js:72-88` `lineRow`) sayfada yoktur; sütunlar §8.2'deki sırada; GUID görünmez. Sabotaj **S-FU01-1:** tablo başlatıcısında `data-dt-data-mode`'u `server` yap → vitest kırmızı.
- [ ] AC-2 (zorunlu) — Satır paneli 6 alanı taşır; her alan `.diten-field` + ikon (`diten-field-icons.test.js` ICON_MAP); birincil düğme `data-dt-primary="enter"`; Enter "Satırı kaydet"e basar. Sabotaj **S-FU01-2:** panelin `data-dt-primary` işaretini kaldır → `diten-enter-primary.test.js` kırmızı.
- [ ] AC-3 (zorunlu) — Panelde ürün değişince SKU ve birim temizlenir ve select2 yeniden eşitlenir; select2 açılır listesi panelin içindedir (`dropdownParent`). Sabotaj **S-FU01-3:** ürün değişim işleyicisinden SKU temizleme satırını kaldır → test kırmızı.
- [ ] AC-4 (zorunlu) — 100 satırda "Satır ekle" pasiftir ve açıklama görünür; 101. satır hiçbir yoldan (panel, klavye) eklenmez. Sabotaj **S-FU01-4:** `rules.js`'teki sınırı kaldır → test kırmızı.
- [ ] AC-5 (zorunlu, D-4) — Panelde lot seçilip "Satırı önizle"ye basılınca istek gövdesi o satırı `lotOverride.lotId` = seçilen lotla taşır; cevabın tahsisi seçilen lottan gösterilir, FEFO önerisi (ilk serbest lot) ayrıca gösterilir; sapma varsa kip ve önerilen lot no görünür. Sabotaj **S-FU01-5:** önizleme gövdesi kurucusunda `lotOverride`'ı boşalt (bugünkü `form.js:198` davranışı) → vitest kırmızı. Canlı kanıt: E3 turundaki iki lotla (AMX-2604 FEFO 1, G1-20261020 FEFO 2) seçilen G1-20261020 önizlemede görünür.
- [ ] AC-6 (zorunlu, D-4) — "Tahsisi önizle" bütün satırları, her birinin `lotOverride`'ıyla tek istekte gönderir; her satırın lot / alınabilir / durum sütunu cevaptan dolar; seçilen lotu olan satırda lot sütunu seçilen lotu gösterir. Sabotaj **S-FU01-6:** cevap eşlemesinde satır eşini sıra yerine hep ilk satıra bağla → test kırmızı.
- [ ] AC-7 (zorunlu) — FEFO kipi istemcide: `Block` → lot değişimli satır kaydedilemez + açıklama; `RequireReason` → < 10 karakter gerekçeyle kaydedilemez; `Warn` → gerekçesiz kaydedilir; sunucunun 422 `FEFO_BYPASS_REASON_REQUIRED` / `FEFO_ORDER_VIOLATION` cevabı `details.lineSeq` ile doğru satıra bağlanır. Sabotaj **S-FU01-7:** `rules.lotNeed`'in `RequireReason` dalını `Optional` döndür → test kırmızı.
- [ ] AC-8 (zorunlu) — Depo / Staging / satır değişince ilgili önizleme `Önizleme eski` olur; eski önizlemeye dayanan lot değişimli satır kaydedilmez ("yeniden önizleyin"); yeniden önizlemede listede olmayan seçili lot sessizce sıfırlanmaz, satır `Karşılanamaz` + açıklama alır. Sabotaj **S-FU01-8:** depo değişim işleyicisinden eskitme çağrısını kaldır → test kırmızı.
- [ ] AC-9 (zorunlu) — Kaydet gövdesi `CreateOutboundShipmentRequest` (WMS-BUNDLE 1.1.0, `additionalProperties: false`) şemasına uyar: satır sırası tablo sırası, `lotOverride` yalnız lot seçilmiş satırda, `null` üye yok. Test: gövde dondurulmuş şemaya karşı doğrulanır. Sabotaj **S-FU01-9:** gövdeye `lotOverride: null` yaz → şema testi kırmızı.
- [ ] AC-10 (zorunlu) — `422 WMS_ALLOCATION_FAILED` `details.lines[] { lineSeq, code, requested, allocatable }` her satırın rozetine ve satır altı metnine eşlenir; outbound oluşmaz, sayfa yönlenmez. Sabotaj **S-FU01-10:** `lineSeq` eşlemesini kaldır → test kırmızı.
- [ ] AC-11 (zorunlu) — Edit sayfasında satır tablosu salt okunurdur: araç çubuğu ve satır menüsü yoktur, sütunlar kayıttan (lot, tahsis, rezervasyon durumu) dolar; başlık düzenleme bugünkü gibi çalışır. Sabotaj **S-FU01-11:** edit modunda satır menüsünü aç → test kırmızı.
- [ ] AC-12 (zorunlu) — Tablo ve panelde miktarlar FIX-01'in biçimleyicisiyle gösterilir (tr: servis "2.000" → "2", "1.500" → "1,5"); FU01 ikinci bir biçimleyici tanımlamaz. Sabotaj **S-FU01-12:** tablo hücresinde biçimleyici yerine ham değeri yaz → test kırmızı.
- [ ] AC-13 (zorunlu) — UAS-001 (Create'te `.create` yoksa yalnız açıklama), `resx-check` kiracı 7 dil temiz, 375 px'te tablo + panel yatay kaydırmasız (canlı ölçüm, `scrollWidth`), sayfa yardımı güncel (§25). Sabotaj **S-FU01-13:** `ar` dosyasından bir `LinePanel.*` anahtarını sil → resx bekçisi kırmızı.

**DL-2 — `customerRef` (servis + sözleşme)**

- [ ] AC-14 (zorunlu) — `customerRef`'li oluşturma: CRM U-1 200 + `active` → 201; `CustomerRef` kanonik küçük harf yazılır; görünüm `customerRef` döner; `shipTo` isteğin kopyasıdır (CRM'den doldurulmaz). Sabotaj **S-FU01-14:** handler'da `CustomerRef` atamasını kaldır → test kırmızı.
- [ ] AC-15 (zorunlu) — CRM 404 → 422 `WMS_CUSTOMER_NOT_FOUND`; kayıt, rezervasyon, yerel olay ve merkezi denetim 0 / 0 / 0 (FU03 rezervasyon portu çağrılmaz). Sabotaj **S-FU01-15:** 404 dalını geçerli say → test kırmızı.
- [ ] AC-16 (zorunlu) — CRM 401 / 403 → 403 `WMS_CUSTOMER_DIRECTORY_FORBIDDEN`; 400 / 5xx / zaman aşımı / geçersiz gövde → 503 `WMS_CUSTOMER_DIRECTORY_UNAVAILABLE`; ikisinde de yazım yok. Sabotaj **S-FU01-16:** adaptörün `Unavailable` sonucunu `Valid`'e eşle → test kırmızı.
- [ ] AC-17 (Q-2 = A ise zorunlu) — 200 + `status ≠ active` → 422 `WMS_CUSTOMER_NOT_ACTIVE`. Sabotaj **S-FU01-17:** durum kontrolünü kaldır → test kırmızı. (Q-2 = B ise madde düşer ve kod eklenmez.)
- [ ] AC-18 (zorunlu, D-2 "elle giriş her zaman") — `customerRef`'siz oluşturma ve güncelleme CRM'i **hiç çağırmaz**: CRM sahtesi her çağrıda istisna atarken 201 / 200. Sabotaj **S-FU01-18:** doğrulamayı `customerRef` koşulundan çıkar (her zaman çağır) → test kırmızı.
- [ ] AC-19 (zorunlu) — İstek anahtarı CRM'den önce aranır: aynı anahtar + aynı gövde → 200 replay ve CRM çağrı sayısı 0; aynı anahtar + farklı `customerRef` → 400 `IDEMPOTENCY_KEY_REUSED`. Sabotaj **S-FU01-19:** CRM doğrulamasını anahtar aramasının önüne al → replay CRM'i çağırır, test kırmızı.
- [ ] AC-20 (zorunlu) — Güncelleme: Allocated / Picked / Packed'te `customerRef` atanır / değişir / `removeCustomerRef` ile kalkar; `ReadyToShip` ve sonrası 409 `WMS_OUTBOUND_IMMUTABLE`; ikisi birlikte 400; yerel olay `Data`'sı yalnız `customerRefChanged: true` taşır, GUID ne `Data`'da ne merkezi denetimde. Sabotaj **S-FU01-20:** olay `Data`'sına GUID değerini yaz → test kırmızı.
- [ ] AC-21 (zorunlu) — Sözleşme: CT adımıyla WMS-BUNDLE ve WAREHOUSE-OUTBOUND yeni sürümleri birlikte yazılır (§8.7); `ShipTo`'nun kod / ad üyeleri (`countryName`, `cityRef`, `districtRef`, `district`) yazılır, okunur ve uzunluk sınırları (§12) 400 verir; donmuş GET'ler `customerRef`'i ve bu üyeleri yalnız doluysa yazar, hiç `null` yazmaz; elle girilmiş outbound'un donmuş GET cevabı önceki sürümle byte-eşittir (fikstür). Sabotaj **S-FU01-21:** boş `customerRef`'i `null` olarak yaz → sözleşme testi (kural 4) kırmızı.
- [ ] AC-22 (zorunlu) — Kiracı izolasyonu ve token: adaptör CRM'i çağıranın `Authorization` başlığıyla çağırır (servis kimliği yok); başka kiracının hesap kimliği CRM'den 404 alır → 422. Sabotaj **S-FU01-22:** adaptörde çağıranın token'ı yerine boş / sabit başlık gönder → başlık testi kırmızı.
- [ ] AC-23 (zorunlu) — Hata kataloğu: dört kod `WarehouseErrorCodes`'ta ve WMS-BUNDLE `x-error-codes`'ta (create, update); katalog testi ikisini karşılaştırır. Sabotaj **S-FU01-23:** bir kodu yalnız koddan sil → katalog testi kırmızı.
- [ ] AC-24 (zorunlu) — InventoryService test paketinin **tamamı** yeşil (filtre yok — CT kuralı); mimari testler (`AuthorizationGuardTests`, `CentralAuditDeclarationTests`) değişmeden yeşil. Guard değil, kanıt maddesidir: sabotaj yerine negatif test kanıtı = paketteki bütün `S-FU01-14…23` sabotajlarının aynı koşuda kırmızı olup geri alınınca bu maddenin yeşile dönmesi.

**DL-3 — CRM müşteri seçici (Web)**

- [ ] AC-25 (zorunlu) — Seçici yalnız aynı-origin proxy'yi çağırır (`/Warehouse/OutboundShipments/customers`); tarayıcıdan 5000 / 5061'e istek yok; proxy Gateway U-3'ü kullanıcı token'ıyla çağırır; en az 2 karakter. Sabotaj **S-FU01-25:** seçici URL'sini Gateway adresine çevir → vitest kırmızı.
- [ ] AC-26 (zorunlu) — Seçim ad + adresi CRM 1.1 `GET /api/crm/accounts/{id}/ship-to`'dan (proxy üzerinden) doldurur: ülke / şehir / ilçe kodu Reference Data'dan kullanıcının dilinde ada çözülür, kayıt gövdesi kod + adı taşır (`country` küçük harf kod, `countryName`, `city` + `cityRef`, `district` + `districtRef`, `line1`); posta kodu boş gelir; alanlar düzenlenebilir (Q-3 = A) ve elle değiştirilen şehir / ilçe adının kodu gönderilmez; ipucu hesap kodunu gösterir; GUID görünmez. Sabotaj **S-FU01-26a:** ön doldurmada `cityRef` eşlemesini kaldır → test kırmızı; **S-FU01-26b:** şehir adı elle değişince kodu temizleyen satırı kaldır → ad / kod çelişkisi testi kırmızı.
- [ ] AC-32 (zorunlu) — Ship-to cevapları 1.0 kurallarıyla eşlenir (C-1): 404 → "bulunamadı", bağlantı yok; 401 / 403 → "adres okuma yetkiniz yok", ad dolar, adres boş, bağlantı korunur; 400 / 5xx / 503 → "ulaşılamıyor"; Reference Data çözemezse kod + "Ad okunamadı". Sabotaj **S-FU01-26c:** 404'te bağlantıyı kur → test kırmızı.
- [ ] AC-27 (zorunlu) — "Bağlantıyı kaldır" sonrası gövdede `customerRef` yoktur, alanlar korunur. Sabotaj **S-FU01-27:** kaldırma işleyicisinde `customerRef`'i temizleme → test kırmızı.
- [ ] AC-28 (zorunlu, D-2) — Seçicide CRM 403 / 5xx → açıklama + elle alanlar açık; elle kayıt 201. Sabotaj **S-FU01-28:** seçici hatasında alıcı alanlarını pasifleştir → test kırmızı.
- [ ] AC-29 (zorunlu) — Kayıtta 503 / 403 → uyarı + "Bağlantıyı kaldırıp elle kaydet"; eylem `customerRef`'siz ve **yeni** istek anahtarıyla gönderir. Sabotaj **S-FU01-29:** eylemde eski anahtarı kullan → test (400 beklentisi) kırmızı.
- [ ] AC-30 (zorunlu) — Details özet kartı `customerRef` varsa "CRM müşterisi" rozetini gösterir; ayrıntıda CRM çağrısı yok, GUID yok. Sabotaj **S-FU01-30:** rozet yerine `customerRef` değerini yaz → test kırmızı.
- [ ] AC-31 (zorunlu) — Canlı E3: Gateway üzerinden gerçek oturumla (a) CRM müşterisi seçilerek, (b) elle girilerek, (c) CRM kapalıyken elle girilerek birer outbound oluşur; 375 px ve tr / en / ar ekran görüntüleri; sahip turu (Enter, "?" metni, "tur yok"). Guard değil, canlı kanıt maddesidir: negatif test = (c) koşusunda CRM kapalıyken `customerRef`'li kaydın 503 aldığının ve elle kaydın 201 aldığının aynı turda görülmesi.

## 17. Test Expectations

- **Web (vitest):** AC-1…AC-12, AC-25…AC-30, AC-32 — `frontend/Diten.Web/tests/` altında, gerçek `form.js` / `rules.js` dosyalarına karşı (jsdom + fetch
  sahtesi); şema testi (AC-9) dondurulmuş `wms-bundle.openapi.yaml`'ı okur. Mevcut bekçiler: `diten-enter-primary.test.js`,
  `diten-field-icons.test.js`, l10n / resx bekçileri. BL-586: offcanvas `data-dt-primary` işareti zorunlu.
- **Servis (xUnit, gerçek replica set):** AC-14…AC-24 — CRM adaptörü sahte HTTP işleyicisiyle (200 active / 200 archived / 404 / 401 / 403 /
  500 / zaman aşımı / geçersiz gövde); CRM çağrı sayacı (AC-18, AC-19). Servise dokunulduğu için **bütün** InventoryService paketi koşar.
  `INVENTORY_TEST_MONGO` yoksa paket kendi replica set'ini başlatır.
- **Sözleşme:** WMS-BUNDLE / WAREHOUSE-OUTBOUND yeni sürümlerine karşı uyum testi (emsal ebeveyn AC-60); elle outbound için önceki sürüm fikstürü
  byte-eşit (AC-21).
- **Liste ekranı protokolü:** `Index.cshtml` / `_DataTable.cshtml` / `_Filter.cshtml` / `index.js`'e dokunulursa
  `verify_datatable_page.py . --area Warehouse --module OutboundShipments --format gaps` koşar ve sapmalar sahibine sorulur (BL-440). FU01 bu
  dosyalara dokunmayı planlamaz.
- **Sabotaj kanıtı:** §16'daki her `S-FU01-n` kabul kaydında komut + kırmızı çıktı + geri alma ile yazılır.
- **Canlı:** AC-5 (iki lot), AC-13 (375 px), AC-31.
- **Build:** `Diten.InventoryService`, `Diten.Web` PASS.

## 18. Ready-for-dev Checklist

**Belge maddeleri (ready-for-dev öncesi)**
- [x] Golden Reference Compact (sayfa) + Slim offcanvas deseni (panel) seçildi; alan sayımı §9
- [x] Frontmatter zorunlu alanları dolu (service, shell, golden_reference, data_mode, screens, entity_base); `branch` / `target` TBD gerekçesi status_note'ta
- [x] Layout & Shell Contract'ta `_LayoutTenantShell` açıkça yazılı (§9)
- [x] Backend File Convention ebeveynle aynı; yeni yalnız port + adaptör (§10)
- [x] Frontend File Contract tam; sapma G-FU01-01 adıyla (§11)
- [x] Validation Rules her alan için (§12)
- [x] Failure Path en az 4 senaryo (§13)
- [x] Authorization Convention: yeni anahtar yok; CRM anahtarları ve rol eşlemesi bağımlılık (§14)
- [x] Gateway kararı: değişiklik yok (§15)
- [x] AC'ler test edilebilir; her guard adlı sabotajlı (§16)
- [x] Audited Events (§21) ve Page Help (§25) yazılı
- [ ] Açık sorular: Q-2, Q-3, Q-4 (sahip), Q-7 (Ali / CT) — paket onayından önce cevap ya da "öneri kabul"; Q-10 (sahip, posta kodu) ve Q-11 (Reference Data) DL-3 kapısıdır; CRM 1.1 donması (C-5), Q-5 (Auth), Q-6 (MVP-6) dilim kapısıdır, onayı engellemez (§24)
- [ ] Bağımsız inceleme (CT-APR-02) ve onay satırı (CT-APR-01, onaylayan Ali)
- [ ] Registry FU satırı (CT; FU-0178-FU01-REG)

**Dilim kapıları (onay sonrası)** — §24.

## 19. Implementation Notes

### 19.1 Sahip kararları (Beste, 2026-11-13) — bağlayıcı

Kayıt: `docs/records/decisions/2026-11/mod0178-fu01-outbound-form-v2-owner-decisions-01.md`.

| # | Karar | Pakette |
|---|---|---|
| D-1 | Satırlar DataTable standardında; satır ekleme ve düzenleme yan panelde (ürün, SKU, miktar, lot değişimi, FEFO sapma gerekçesi); tahsis önizlemesi panelde ya da tabloda görünür | §8.2, §8.3, AC-1…AC-11 |
| D-2 | Alıcı CRM müşteri kaydından adı ve adresiyle seçilir; kayıt yoksa elle girilir; sevkiyatta account kimliği + adresin o anki kopyası tutulur. CRM 1.0 adres vermez → CRM sözleşme eki gerekir (sahibi Beste — CRM); alıcı seçimi dilimi o eke kapılı; elle giriş her zaman çalışır | §8.4, §8.5, §8.7, C-1…C-5, DL-2, DL-3 |
| D-3 | Sipariş referansı şimdilik elle; MOD-0168 gelince "siparişten seç, yoksa elle gir" — v1'de alan hazır, bağlama FU | §8.6, FU-0178-FU01-ORDER |
| D-4 | Önizleme seçilen lotu göstermeli (FIX-01 madde 4) | §8.3, AC-5, AC-6 |
| D-5 | Mockup turu yok; mevcut Golden Reference DataTable + offcanvas desenleri esas alınır | künye, §1, §9, §11 |

**CRM sahibinin cevabı (Beste, CRM oturumu, 2026-11-13; çağıran CT iletti) — DL-3'ün tabanı:**

| # | Cevap | Pakette |
|---|---|---|
| C-1 | CRM-ACCOUNT-LOOKUP 1.1 yalnız ekleme: yeni uç `GET /api/crm/accounts/{id}/ship-to`. Kiracı sınırı; 404 (yok / silinmiş / başka kiracı aynı cevap); `tenant_id`'siz token → 400; 5xx → 503 — 1.0 kurallarıyla aynı. Adres U-1 / U-3'e eklenmez | §7, §8.4, §15, AC-32 |
| C-2 | Ülke / şehir / ilçe **kod** döner (`CountryRef`, `CityRef`, `DistrictRef`); görünen ad WMS'te Reference Data'dan çözülür; anlık kopyaya kod + ad yazılır (ad dile bağlı); kod kümesi sözleşmede yazılacak. Posta kodu account'ta yok; gerekirse ayrı CRM alan işi — v1'de CRM'den seçilen alıcıda posta kodu boş gelir, kullanıcı elle ekleyebilir | §4.1a, §8.4, Q-10, Q-11 |
| C-3 | v1 tek adres (account adresi); çoklu teslimat adresi ileride 1.x eklemesi (adres listesi + isteğe bağlı adres kimliği); WMS account kimliği + kopya tuttuğu için kırılma yok | §8.4, FU-0178-FU01-MULTIADDR |
| C-4 | Ayrı okuma anahtarı `crm.account-ship-to.read` (`crm.account.read` değil); kullanıcının token'ıyla, Gateway üzerinden; anahtarı CRM manifestine CRM ekler, WMS rollerine atama Auth ile | §14, Q-5 |
| C-5 | Tarih TBD (Beste). Sıra: 1.1 sözleşme taslağı → inceleme → kod → donma; alıcı seçim dilimi bu donmaya kapılı; elle giriş her zaman çalışır. MVP-6 da aynı ucu kullanacak; `customerRef` = `Account.Id` — WMS'te tutulan kimlik de `Account.Id` | §8.7, §24 DL-3, Q-4 |

### 19.2 Ölçülen gerçekler (`8913e60f0`, salt okuma)

- Bugünkü form satırları kart olarak çizer (`form.js:72-88`) ve önizlemeye `lotId: ''` gönderir (`form.js:198`); seçilen lot yalnız kayıtta
  gider (`form.js:134-148` → `rules.lineBody`).
- Servis önizlemesi lot değişimini uygular; sapmayı önizlemede reddetmez, `deviation` olarak bildirir (`OutboundAllocationPlanner.cs:233-236`,
  `:452-460`); dışlanmış lotu önizlemede de 422 ile reddeder (`:436-450`). Önizleme işleyicisi `preview: true` ile planlar
  (`services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/OutboundShipments/Handlers/QueryHandlers/OutboundShipmentQueryHandlers.cs:23-31`).
- WMS-BUNDLE önizleme gövdesi = anahtarsız oluşturma gövdesi, satırda `lotOverride` var (`wms-bundle.openapi.yaml:1953-1977`, `:1990-2001`);
  istek şemaları `additionalProperties: false` → `customerRef` sözleşme sürümü olmadan gönderilemez.
- Outbound adresi: `OutboundAddress` altı alan (`OutboundShipmentEntities.cs:13-28`); `OutboundShipment`'ta müşteri kimliği yok
  (`OutboundShipmentEntities.cs:189-207`).
- CRM: U-1 yalnız doğrulama alanları (`AccountModels.cs:86-94`); tam hesap okuması (`AccountDetailDto`, adresli — `AccountModels.cs:46-58`)
  `crm.account.read` ister ve sözleşmesizdir (`crm-account-lookup.md` "Ölçülen kaynaklar"); K-9 açık (`crm-account-lookup.md:351`); MOD-0183 /
  MOD-0186 aday tüketici satırı adres ihtiyacını K-9'a bağlar (`crm-account-lookup.md:259`).
- InventoryService'te CRM U-1 okuyucusu var (Recall): `IRecallCustomerDirectory` (`services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/Recall/Ports/IRecallCustomerDirectory.cs:37-40`),
  adaptör `Infrastructure/Recall/CrmAccountLookupHttpReader.cs`.

### 19.3 Varsayımlar (yalnız yeni paket sürümüyle değişir)

- ASSUMPTION-FU01-01: CRM hesabı kiracı geneldir, şirket (LegalEntity) alanı yoktur (`crm-account-lookup.md` "Ölçülen kaynaklar") → bir
  hesap kiracının her şirketinin outbound'unda seçilebilir.
- ASSUMPTION-FU01-02: Sipariş satırı no tekilliği sunucuda zorlanmaz (ölçülmedi — çıkarım); FU01 istemcide zorlar, sunucu davranışını değiştirmez.
- ASSUMPTION-FU01-03: CRM `CountryRef` kodu donmuş `Address.country`'nin beklediği "lowercase country code (Territory ile uyumlu)" kümesiyle
  aynıdır (C-2: kod kümesi CRM 1.1 sözleşmesinde yazılacak). Değilse DL-3 eşleme tablosu ister — CRM 1.1 taslağı incelenirken teyit edilir.

### 19.4 Bilinçli sapmalar

- G-FU01-01 — §11 (Compact sayfada alt satır paneli; yerel satır tablosunda dışa aktarma / yazdırma / toplu sil / süzgeç yok).

### 19.5 Açık sorular

| # | Soru | Sahip | Öneri | Kapı |
|---|---|---|---|---|
| Q-1 | ~~CRM-ACCOUNT-LOOKUP 1.1.0 adres yüzeyi (K-9)~~ — **cevaplandı** (CRM sahibi Beste, 2026-11-13, C-1…C-5): ayrı uç `GET /api/crm/accounts/{id}/ship-to`, anahtar `crm.account-ship-to.read`, kod döner. Kalan: 1.1 taslağı / inceleme / kod / donma, tarih TBD | Beste (CRM) | — | DL-3 başlama (donma) |
| Q-2 | Hangi CRM durumları seçilebilir? | Beste (sahip) | **A**: yalnız `active` (seçici `billableOnly=true`; sunucu 422 `WMS_CUSTOMER_NOT_ACTIVE`). B: silinmemiş her hesap | paket onayı |
| Q-3 | CRM'den gelen adres bu sevkiyat için düzenlenebilir mi? | Beste (sahip) | **A**: evet, düzenlenebilir; bağlantı korunur, kayıt kopyadır | paket onayı |
| Q-4 | Alan adı `customerRef` mı `accountRef` mı? | Beste (sahip) | `customerRef` — SHIPMENT-BUNDLE 3.5.0 ve RECALL 1.10.0 ile aynı ad ve anlam (MOD-0149 `Account.Id`); MVP-6 eşlemesi birebir olur | paket onayı |
| Q-5 | `crm.account.lookup-validation` (U-1 / U-3) ve `crm.account-ship-to.read` (C-4) `ShippingPlanner` / `WarehouseSupervisor` rol şablonlarına eklenir mi (var olan kiracılarda da, ebeveyn D-3 koşullarıyla)? | **Ali** (Auth, MOD-0018) | evet; ebeveyn AC-76 / MOD-0018-FU31 ile aynı WP'ye ek satır; `crm.account-ship-to.read` CRM manifestine girdikten sonra | DL-2 / DL-3 kabul |
| Q-6 | WAREHOUSE-OUTBOUND eklemeli `customerRef`'i MVP-6 kabul eder mi; yükümlülük 3 beyaz listesinde nasıl sınıflanır; intake sevkiyata `customerRef` olarak eşler mi, ne zaman (FU-0178-MVP6)? | **Natig (MVP-6 CT)** — XMC-001 | kabul; kimlik olarak sınıflanır; eşleme MOD-0183'ün sonraki revizyonunda | DL-2 yayın (CT sözleşme adımı) |
| Q-7 | Sürüm sırası: FU01 eki Item adımından (WAREHOUSE-OUTBOUND 1.1.0 + WMS-BUNDLE 1.2.0) sonra 1.2.0 / 1.3.0 olarak mı yazılır; Item adımı gecikirse FU01 eki önce mi gider, aynı adımda mı birleşir? | **Ali / CT** | Item adımını bekle; MOD-0183 r10 onayı DL-2 bitişinden sonra kalırsa CT iki eki tek adımda yazar (iki dosya, tek sürüm artışı) | DL-2 yayın |
| Q-8 | Golden sapması G-FU01-01 kabul mü? | Ali (onay) | kabul (gerekçe §11) | paket onayı |
| Q-9 | DL-2 CRM adaptörü Recall'ın `CrmAccountLookupHttpReader`'ı ile ortak kod paylaşacak mı (MOD-0177 dosyasına dokunur)? | Beste (MOD-0177 sahibi) — XMC-001 | hayır: Warehouse kendi adaptörünü yazar; ortaklaştırma sonraya (FU-0178-FU01-CRMCLIENT) | DL-2 başlama |
| Q-10 | **Posta kodu:** account'ta posta kodu yok (yalnız ilçe — C-2). v1'de CRM'den seçilen alıcıda posta kodu boş gelir ve kullanıcı elle ekleyebilir; bu yeterli mi, yoksa CRM'de ayrı bir posta kodu alan işi (MOD-0149) açılsın mı? | **Beste (sahip) — karar bekliyor** | v1: boş gelir, elle eklenir (sevkiyat posta kodsuz da kaydedilir — donmuş `Address.postalCode` isteğe bağlı); taşıyıcı (MOD-0184) zorunlu kılarsa CRM alan işi o zaman açılır | DL-3 başlama |
| Q-11 | **Reference Data kaynağı:** ülke / şehir / ilçe kodlarının görünen adı hangi uçtan, hangi dillerde okunur (C-2 "WMS Reference Data")? Kod kümesi CRM 1.1 sözleşmesinde yazılacak; Web'in bugün kullandığı bir ülke / şehir arama ucu bu pakette ölçülmedi | CT (ölçüm) + Reference Data sahibi | DL-3 geliştiricisinin ilk ölçümü; 7 dilde ad yoksa en'e düşülür ve kopyaya o ad yazılır (öneri) | DL-3 başlama |

## 20. Follow-up Items

- **FU-0178-FU01-ORDER:** MOD-0168 Order Capture gelince "siparişten seç, yoksa elle gir" — eklemeli `orderId`, `orderRef` görünen numara kalır;
  satırların siparişten doldurulması ayrı karar (D-3).
- **FU-0178-FU01-REG:** registry'ye `MOD-0178-FU01` satırı (CT).
- **FU-0178-FU01-FILTER:** outbound listesinde müşteriye göre süzgeç (`CustomerRef` indeksi) — talep olursa.
- **FU-0178-FU01-MULTIADDR:** CRM 1.x çoklu teslimat adresi (adres listesi + isteğe bağlı adres kimliği, C-3) gelince seçicide adres
  seçimi ve outbound'a isteğe bağlı adres kimliği (eklemeli); bugünkü kopyalar değişmez.
- **FU-0178-FU01-POSTAL:** Q-10 "CRM alan işi" seçilirse ön doldurmaya posta kodu.
- **FU-0178-FU01-CRMCLIENT:** InventoryService'te iki CRM U-1 okuyucusunun (Recall, Warehouse) ortak istemciye taşınması (XMC-001, Q-9).
- **FU-0178-MVP6 (ebeveyn) notu:** intake `customerRef` eşlemesi FU01 DL-2'den sonra mümkündür (Q-6).

## 21. Audited Events

Servisin denetim altyapısı **bugün var** (ebeveyn §21: `tests/architecture/audit-ledger/Diten.InventoryService.md` `## İzler` →
`inventory-merkezi-iletim | b | işaret | ICentralAuditDeclaration`). **Yeni yazma komutu yoktur**; FU01 iki mevcut komutun gövdesini genişletir.
Merkezi kayıt ad ve gövde taşımaz (ebeveyn §8.8), bu yüzden `customerRef` merkezi kayda girmez.

| Komut | Olay adı (`RequestType` + `Operation`) | Yol | FU01 değişikliği |
|---|---|---|---|
| `CreateOutboundShipmentCommand` | `CreateOutboundShipmentCommand` + `Create` | b (K2) — ebeveyn §21 | yerel `Created` olayı `Data`'sına `customerRefEntered: true` (değer yok); CRM retlerinde 0 / 0 / 0 (AC-15, AC-16) |
| `UpdateOutboundShipmentCommand` | `UpdateOutboundShipmentCommand` + `Update` | b — ebeveyn §21 | yerel `Updated` olayı `Data`'sına `customerRefChanged: true` (atama, değişim, kaldırma); değer yok (AC-20) |
| `PreviewOutboundAllocationQuery` | — | istisna İ-okuma (yazmaz) | yok |
| Web proxy `customers`, `customer-ship-to` | — | istisna İ-okuma (yazmaz; CRM'e salt okuma) | yok |

## 22. Record Codes

Yeni kayıt türü ve kayıt kodu yok. Outbound numarası ebeveynin `{ŞİRKET}-OUT-{000001}` kuralıyla kalır (ebeveyn §22).

## 23. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | çekirdek: satır tablosu + yan panel + önizlemede seçilen lot + Edit salt okunur tablo (yalnız Web; servis ve sözleşme değişmez) | Sıra ve FIX-01 ile çakışmama, Satır tablosu, Satır yan paneli ve önizleme, Sipariş referansı, Layout & Shell Contract, Frontend File Contract, Validation Rules, Failure Path to Verify, Page Help (UXH-001) | E4 | plan kimliği yok | plan satırı yok |
| DL-2 | outbound `customerRef` + CRM U-1 doğrulaması + dört hata kodu + WMS-BUNDLE / WAREHOUSE-OUTBOUND eklemeli sürüm (CT adımı) | Owned Objects, Entity Fields, Sunucu tarafında müşteri kimliği, Sözleşme değişiklikleri ve MVP-6 etkisi, Backend File Convention, Authorization Convention, Gateway / API Routing Decision, Audited Events | E4 | plan kimliği yok | plan satırı yok |
| DL-3 | alıcıyı CRM'den seçme (seçici, ön doldurma, bağlantıyı kaldırma, kayıtta CRM hatası) + Details rozeti + canlı E3 | Alıcı seçimi, Frontend File Contract, Authorization Convention, Page Help (UXH-001) | E4 | plan kimliği yok | plan satırı yok |

## 24. Dilim kapıları ve bağlı kararlar

Paket onaylanmadan (künye `approved` / `ready-for-dev` + CT-APR-02 inceleme kaydı + CT-APR-01 onay satırı, onaylayan Ali) hiçbir dilim başlamaz.

| Dilim | Başlamadan kapanması gereken | Seam'ler (XMC-001) | AC |
|---|---|---|---|
| DL-1 | **WP-0178-E3-FIX-01 CT kabulü ve hatta birleşmesi** (§8.1); Q-8 | yok (yalnız MOD-0178 Web dosyaları) | AC-1…AC-13 |
| DL-2 | Q-2, Q-4, Q-9; DL-1 kabulü gerekmez (paralel yürüyebilir, ama `OutboundDtos.cs` / proxy birleşmesi sırayla) | Recall adaptörü okunur, değişmez (Q-9); MVP-6 bilgi (Q-6) | AC-14…AC-24 |
| DL-2 yayın (CT sözleşme adımı) | Q-6 (MVP-6 cevabı), Q-7 (sürüm sırası) | `docs/analysis/contracts/` (CT tek yazıcı) | AC-21 |
| DL-3 | **CRM-ACCOUNT-LOOKUP 1.1.0 (`ship-to` ucu, `crm.account-ship-to.read`) dondu ve CRM'de uygulandı** (C-5: taslak → inceleme → kod → donma; tarih TBD, Beste — CRM); Reference Data kaynağı (Q-11); DL-1 ve DL-2 kabulü; Q-3, Q-10 | CRM (Beste — CRM; anahtar CRM manifestinde), Auth rol eşlemesi (Q-5, Ali — kabul kapısı) | AC-25…AC-32 |

Sıra: FIX-01 → DL-1 → DL-3; DL-2 DL-1 ile paralel, DL-3'ten önce. CRM eki gecikirse DL-1 ve DL-2 kendi başına canlıya çıkabilir (elle alıcı
girişi tam çalışır; `customerRef` yalnız API'den gelir).

## 25. Page Help (UXH-001)

- Sayfa kodu `OUTBOUNDSHIPMENTS` (Create / Edit aynı kodu kullanır — `Create.cshtml` `ViewData["PageCode"]`). Güncellenecek anahtarlar:
  `Help.OUTBOUNDSHIPMENTS.Purpose`, `.Audience`, `.Step1Title`/`.Step1` … `.Step5` — 1: depo ve (varsa) sevkiyat hazırlık lokasyonunu seç;
  2: "Satır ekle" ile yan panelde ürün, SKU ve miktarı gir; 3: "Tahsisi önizle" ile lotları gör, gerekirse "Lot değiştir" ve gerekçe;
  4: alıcıyı CRM'den seç ya da elle yaz (DL-3'e kadar yalnız "elle yaz"); 5: kaydet — tahsis kayıtta yeniden hesaplanır.
  `.Immediate`: "Önizleme stok ayırmaz; kayıt anında stok değişmişse satır karşılanamaz olarak işaretlenir."
- Kiracı modülü → 7 dil. **Tur yok** (beş akıştan biri değil). Sahip kontrol listesi: Enter panelde "Satırı kaydet"e basar; "?" metni doğru;
  "tur yok".
