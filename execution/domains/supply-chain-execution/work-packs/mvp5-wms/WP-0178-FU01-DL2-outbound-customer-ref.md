# WP-0178-FU01-DL2 — Çıkış formu v2 DL-2: outbound kaydında CRM müşteri kimliği (servis + proxy)

Tarih: 2026-11-13 · Veren: CT (merkez, Beste adına) · Sahip kararı: "DL-2'ye başlayabiliriz, Ali sonradan ekler" (Beste, 2026-11-13; Q-5
kabul kapısıdır, başlamayı bekletmez) · Paket: `execution/domains/supply-chain-execution/module-packs/MOD-0178-FU01-outbound-form-v2.md` r4,
LF sha256 `949ab3c9…` (onay satırı Ali / GMG-ERP, 2026-11-13) · DL-1 VERIFIED (`docs/records/audits/2026-11/mvp1-ct-acceptance-batch-2026-11-13.md`).

## 0. Başlamadan
- Dal: tek çalışma dalı `feature/sce/mod-0173-inventory-ledger-valuation`; CT etiketi `ct/line-ff-2026-11-13n`. Etiket ata ise devam; değilse
  `--ff-only`; olmazsa `--no-ff`; çakışmada abort ve CT'ye yaz. Paket sha256'sını ölç (`949ab3c9` ile başlamalı).
- **Eşzamanlılık:** MOD-0182 DL-1 aynı serviste (InventoryService) aynı ana checkout'ta sürüyor. Ana checkout'ta derleme / test yapma;
  ölçümü ayrık worktree'de yap. `WarehouseErrorCodes` ve hata kataloğu testi iki işte de değişebilir: commit'ten hemen önce hattı al,
  çakışırsa ikisini de koru, CT'ye yaz.
- Paketi oku: §1 madde 2, §4 / §4.1a, §5 "DL-2", §8.5 (sunucu tarafında müşteri kimliği), §8.7 (sözleşme — CT adımı), §14, §16
  (AC-14…AC-24), §17 (S-FU01-14…S-FU01-23), §24 DL-2 satırı.

## 1. Kapsam (paket §5 "DL-2" listesi dışına çıkma)
- `OutboundShipment.CustomerRef` (kanonik küçük harf GUID), `CustomerAddressId`; `OutboundAddress` kod / ad üyeleri, KVKK yorumu, `SameAs`.
- Create / Update / Preview gövdelerine `customerRef`, `customerAddressId`; Update'e `removeCustomerRef`.
- Yeni port `IWarehouseCustomerDirectory` + `WarehouseCustomerDirectory` (U-1 okuyucusu; `Recall/CrmAccountLookupHttpReader.cs` **deseni**,
  Q-9: Recall dosyaları okunur ama değişmez, kod paylaşılmaz).
- Üç hata kodu: `WMS_CUSTOMER_NOT_FOUND` (422), `WMS_CUSTOMER_NOT_ACTIVE` (422; yalnız `isBillable` = aktif, Q-2),
  `WMS_CUSTOMER_DIRECTORY_UNAVAILABLE` (503; CRM 400 / 401 / 403 / 5xx ve sözleşme §5 tutarsız gövde).
- Web proxy (`OutboundDtos.cs`, `OutboundShipmentsController.cs`): yalnız gövde geçişi; **seçici yok** (DL-3).
- Sözleşme dosyaları (`wms-bundle.openapi.yaml`, `warehouse-outbound.openapi.yaml`) **yazılmaz** — CT tek yazıcı (AC-21, Q-6 / Q-7 sonrası).

## 2. Kurallar
- `customerRef` yoksa CRM hiç çağrılmaz (AC-18, elle giriş her zaman çalışır); değişmediyse U-1 atlanır (F-8).
- İstek anahtarı CRM'den önce aranır (AC-19); güncellemede önce yerel kontroller, sonra CRM (F-7).
- CRM'e çağıranın `Authorization` başlığıyla, Gateway üzerinden (AC-22); kiracı ve şirket izolasyonu; başka kiracı 404.
- Kişisel veri: ad / adres kopyası günlüğe yazılmaz; günlük kuralı paket §4.1a.
- Korunan yollar ve XMC-001: Recall, CRM, Auth, `ocelot.json`, `.antigravity/**`, sözleşme dosyaları değişmez. Yalnız açık yolla stage;
  hook atlanmaz; push / PR yok.

## 3. Kabul kapıları (başlamayı bekletmez)
- XMC-CRM-01: CRM-ACCOUNT-LOOKUP §5a'da MOD-0178 satırı (en az U-1) — CRM sahibi (Beste) yazar; DL-2 kabulünde CT commit'i atıfla doğrular.
- Q-5 Auth rol eşlemesi (Ali) — sonradan; DL-2 kabulünde eşleme yoksa canlı ölçüm (AC-31) yönetici rolüyle yapılır ve bu kayda geçer.
- AC-21 sözleşme yayını — CT adımı (Q-6 MVP-6, Q-7 sürüm sırası; K-4 eklemeli sürümüyle sıra CT'de).

## 4. Test ve kanıt
- AC-14…AC-20, AC-22…AC-24; sabotajlar S-FU01-14…S-FU01-23 (her biri kırmızı → bayt-eşit geri al → yeşil; `git status` temiz).
- InventoryService takımı **filtresiz** (AC-24): `INVENTORY_TEST_MONGO` boş (takım kendi replica set'ini açar), 27017 yok. Dilim başı ve sonu
  sayıları; taban kırmızılarını adıyla yaz. `ShipmentBundle350AlignmentTests` ve `Mod0178WarehouseRouteTests` yeşil kalmalı.
- Web.Tests: proxy gövde testi (`OutboundFormV2BodyTests` deseni) — `customerRef` / `customerAddressId` / `removeCustomerRef` geçişi.
- `Diten.AuthService.AccountKindAcceptanceHost.Tests` koşulmaz.

## 5. Rapor
Commit sha'ları; AC-14…AC-24 her biri için test adı ve sonuç; sabotaj tablosu; filtresiz takım sayıları (önce / sonra); MOD-0182 ile çakışma
olduysa nasıl çözüldü; açık noktalar (özellikle sözleşme için önerilen eklemeli üye metni — CT yazacak).
