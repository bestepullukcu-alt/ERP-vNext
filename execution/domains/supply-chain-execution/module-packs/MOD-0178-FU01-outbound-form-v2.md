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
revision: "r3 (2026-11-13) — r2 bağımsız incelemesi (docs/records/reviews/MOD-0178-FU01/2026-11-13-3ce263bb.md, KOŞULLU; F-1…F-11) kapatıldı: U-1 kararı CRM-ACCOUNT-LOOKUP §4 gibi isBillable ile, §5 tutarlılık kuralları 503, F-UNK / F-C1 / F-C2 / F-C4 / F-V / F-403 fikstürleri adlı sabotajla (F-1); CRM 401 / 403 sözleşmeye hizalandı → 503, WMS_CUSTOMER_DIRECTORY_FORBIDDEN kalktı, kod sayısı üç (F-2); MOD-0178'in CRM-ACCOUNT-LOOKUP §5a tüketici satırı CRM sahibine XMC-001 isteği, DL-2 / DL-3 kapısı (F-3); C-6…C-11 karar dosyasının 'CRM güncel önerisi' ekine (commit b8becef7d) bağlandı ve numaralar o eke hizalandı (F-4); CRM seçici yalnız crm.account.lookup-validation, adres seçici yalnız crm.account-ship-to.read ile çizilir (UAS-001, F-5); Q-12 uyarısı Edit'te yönlendirmeden önce onaylanan premium bildirimle (F-6); güncellemede önce yerel kontroller sonra CRM (F-7); customerRef değişmediyse U-1 atlanır (F-8); günlük kuralı, KVKK yorumu ve SameAs (F-9); FIX-01'li tabana atıf tazeliği, Recall 2 sn (F-10); Details ilçe / ülke adı ve eski serbest metin ülke (F-11); CT gözlemi: FEFO sapma gerekçesi Details'te ayrı sütun (DL-1). AC numaraları değişmedi; yeni sabotajlar §19.7. r2 (2026-11-13) — sahip cevapları Q-2, Q-3, Q-4, Q-9, Q-10 ('hepsi öneri gibi', Beste; karar dosyası eki '2026-11-13: sahip cevapları', commit a7c1d32b8), CRM sahibinin güncel 1.1 önerisi (ship-to cevabı ilk günden adres listesi addresses[] + defaultAddressId; ana adreste posta kodu alanı CRM'de ekleniyor; Beste — CRM, 2026-11-13, koordinatör özeti — taslak dosyası bu depoda yok) ve CT bulgusu (MVP-6 bugün CRM'in genel hesap uçlarını sözleşmesiz okuyor) işlendi; outbound'a customerAddressId (DL-2), adres seçimi ve boş liste kuralı (DL-3); Q-12 sahip kararı (Beste, 'evet', commit ad1100158): seçimden sonra pasifleşen account'ta sevkiyat sürer, Edit kaydı ve 'sevke hazır'da Web proxy ship-to'dan durumu okuyup uyarır, akışı durdurmaz (DL-3); yeni açık soru Q-13 (CRM — ship-to cevabında account durumu). AC numaraları değişmedi; yeni vakalar mevcut AC'lere, yeni sabotajlar S-FU01-14b, 14c, 20b, 25b, 26d, 26e, 26f, 29b, 29c. Kapanış tablosu §19.6. r1 (2026-11-13) — ilk taslak. Kaynak: E3 canlı turu (docs/records/audits/2026-11/mod-0178-e3-live-tour-2026-11-13.md), sahip kararları D-1…D-5 ve CRM sahibinin cevabı C-1…C-5 (Beste, 2026-11-13; docs/records/decisions/2026-11/mod0178-fu01-outbound-form-v2-owner-decisions-01.md). Mockup turu yok (D-5)."
risk: MEDIUM
risk_reason: "Ekran değişikliği tahsis önizlemesini ve FEFO sapma gerekçesini taşır (yanlış lot / eksik gerekçe stok çıkışını etkiler); DL-2 iki donmuş sözleşmeye eklemeli üye yazar (WMS-BUNDLE, WAREHOUSE-OUTBOUND) ve MVP-6 (MOD-0183) bu yüzeyi tüketir; DL-2 / DL-3 servisler arası CRM okuması (kullanıcı token'ı, kiracı izolasyonu, kişisel veri sınırı) ve dondurulmuş CRM-ACCOUNT-LOOKUP 1.0'a eklenecek bir sürüme bağlıdır."
status_note: "draft r3 (2026-11-13) — approval_status pending: r3'ün bağımsız incelemesi (CT-APR-02) ve onay satırı (CT-APR-01, onaylayan Ali) yok; kod yazılmaz. r2 incelemesi (KOŞULLU, F-1…F-11) r3'te kapatıldı (§19.7). Mockup turu yok, mevcut Golden Reference DataTable + offcanvas desenleri esas alınır (sahip kararı D-5, 2026-11-13). WP-0178-E3-FIX-01 uygulandı ve CT VERIFIED (49fd410b7; docs/records/audits/2026-11/mod-0178-e3-fix01-ct-acceptance-2026-11-13.md) — DL-1'in FIX-01 kapısı kapandı, atıflar FIX-01'li tabanda yeniden ölçüldü (§8.1). DL-3 CRM-ACCOUNT-LOOKUP 1.1.0'ın (yeni uç GET /api/crm/accounts/{id}/ship-to, cevap addresses[] + defaultAddressId, anahtar crm.account-ship-to.read — CRM cevabı C-1…C-5 ve güncel önerisi C-6…C-11, karar dosyası eki, commit b8becef7d; tahmin: yalnız ana adresle uç ~2 hafta, çoklu adres +2–3 hafta, sözleşme biçimi değişmez; sahibi Beste — CRM) donmasına, MOD-0178'in CRM-ACCOUNT-LOOKUP §5a tüketici satırına (XMC-CRM-01, Beste — CRM; DL-2 kabul ve DL-3 başlama kapısı) ve Auth rol eşlemesine (Q-5, Ali) kapılı; elle alıcı girişi her dilimde çalışır. Sahip soruları Q-2, Q-3, Q-4, Q-9, Q-10, Q-12 kararlaştı (a7c1d32b8, ad1100158). Açık: Q-5, Q-7, Q-8 (Ali), Q-6 (MVP-6, Natig — XMC-001), Q-11 (CT ölçümü), Q-13 (Beste — CRM: ship-to cevabında account durumu). Registry satırı yok (CT ekler; FU-0178-FU01-REG). branch / target: TBD — CT hattı kararı."
owner: supply-chain-execution / merkez (Beste) — MVP-5
approver: Ali (GitHub GMG-ERP)
branch: TBD
started: 2026-11-13
target: TBD
form_field_count: 13
mockup: "yok — sahip kararı D-5 (2026-11-13); Golden Reference Compact _Form + Slim _CreateEditOffcanvas + DataTable v2 desenleri"
dependencies: [MOD-0178 (ebeveyn; S2 outbound ekranı ve servis), WP-0178-E3-FIX-01 (uygulandı, CT VERIFIED 49fd410b7; sayı biçimleyici, terimler, önizleme lotu — FU01 tabanı), WMS-BUNDLE-v1.1.0 (owned → sonraki eklemeli sürüm, DL-2), WAREHOUSE-OUTBOUND-v1.0.0 (owned; 1.1.0 Item adımı bekliyor → sonraki eklemeli sürüm, DL-2), CRM-ACCOUNT-LOOKUP-v1.0.0 (MOD-0149; U-1 doğrulama DL-2, U-3 arama DL-3; §5a tüketici satırı XMC-CRM-01) → v1.1.0 ship-to ucu (K-9; CRM cevabı C-1…C-5 ve güncel önerisi C-6…C-11 — adres listesi; DL-3 kapısı = 1.1 donması, sahibi Beste — CRM, tahmin ~2 hafta), Reference Data (ülke / şehir / ilçe kodlarının görünen adı; DL-3, Q-11), MOD-0018 (Auth — crm.account.lookup-validation ve crm.account-ship-to.read anahtarlarının depo rollerine eşlemesi; DL-2 / DL-3 kabul kapısı, Ali), MOD-0183 (MVP-6 — WAREHOUSE-OUTBOUND tüketicisi; XMC-001 bilgi + soru), MOD-0168 (Order Capture — planlı, kod yok; bağlama FU-0178-FU01-ORDER), DEV-0000, DEV-0001]
---

# MOD-0178-FU01 — Outbound Shipment Form v2 (Çıkış sevkiyatı formu v2)

> **r3 (2026-11-13):** r2'nin ilk bağımsız incelemesi (`docs/records/reviews/MOD-0178-FU01/2026-11-13-3ce263bb.md`, KOŞULLU; 5 orta,
> 6 düşük) kapatıldı. (1) U-1 kararı CRM-ACCOUNT-LOOKUP §4 gibi `isBillable` ile verilir; §5'in tutarlılık kuralları (çelişki, `accountId`
> uyuşmazlığı, `contractVersion`) 503; sözleşme fikstürleri AC-16 / AC-17'de adlı sabotajla (F-1). (2) CRM 401 / 403 sözleşmenin §5 / §6
> kuralına hizalandı: tüketicide 503 `WMS_CUSTOMER_DIRECTORY_UNAVAILABLE`; `WMS_CUSTOMER_DIRECTORY_FORBIDDEN` kalktı, yeni kod sayısı üç (F-2).
> (3) MOD-0178'in §5a tüketici satırı CRM sahibine XMC-001 isteği (XMC-CRM-01), DL-2 kabul / DL-3 başlama kapısı (F-3). (4) §19.1'deki
> C-6…C-11 karar dosyasının "CRM güncel önerisi" ekine (commit `b8becef7d`) bağlandı, numaralar o eke hizalandı (F-4). (5) UAS-001: CRM
> seçici yalnız `crm.account.lookup-validation`, adres seçici yalnız `crm.account-ship-to.read` ile çizilir; yoksa hiç çizilmez, elle
> giriş açık (F-5). (6) Q-12 uyarısı Edit'te yönlendirmeden **önce** onaylanan premium bildirimle gösterilir (F-6). (7) Güncellemede önce
> yerel kontroller, sonra CRM (F-7). (8) `customerRef` değişmediyse U-1 atlanır (F-8). (9) Günlük kuralı, KVKK yorumu, `SameAs` (F-9).
> (10) Atıflar FIX-01'li tabanda (`49fd410b7`, CT VERIFIED) yeniden ölçüldü; Recall okuma sınırı 2 sn (F-10). (11) Details'te ilçe ve
> ülke adı; eski serbest metin ülkeli outbound'un Edit'te gösterimi (F-11). (12) CT turu gözlemi (FIX-01 kabul kaydı): Details'te FEFO
> sapma gerekçesi "Rezervasyon" sütunundan ayrı sütuna alınır (DL-1). AC numaraları değişmedi; kapanış tablosu §19.7. Künye `draft` /
> `pending`: r3'ün kendi CT-APR-02 incelemesi ve CT-APR-01 onay satırı gerekir.
>
> **r2 (2026-11-13):** üç girdi işlendi. (1) Sahip cevapları Q-2, Q-3, Q-4, Q-9, Q-10 (Beste, "hepsi öneri gibi";
> `docs/records/decisions/2026-11/mod0178-fu01-outbound-form-v2-owner-decisions-01.md` eki "2026-11-13: sahip cevapları", commit `a7c1d32b8`):
> yalnız `active` account seçilir ve sunucu `WMS_CUSTOMER_NOT_ACTIVE` ile reddeder; CRM'den gelen adres düzenlenebilir, kopya düzenlenmiş
> değeri tutar, account kimliği kalır; alan adı `customerRef` = `Account.Id`; Recall okuyucusuyla kod paylaşılmaz; posta kodu CRM'den
> doluysa kopyaya yazılır, boşsa elle eklenir. (2) CRM sahibinin güncel 1.1 önerisi (Beste — CRM, 2026-11-13; koordinatör özeti — taslak
> `docs/analysis/contracts/drafts/crm-account-lookup-v1.1-ship-to-proposal.md` dal `docs/crm/crm-account-lookup-v1.1-ship-to`'dadır ve bu
> depoda okunamadı): ship-to cevabı ilk günden **adres listesi** (`addresses[]` + `defaultAddressId`); ilk sürümde tek eleman (ana adres,
> `addressId = "primary"`); ana adrese posta kodu alanını CRM ekliyor. Buna göre outbound'a `customerAddressId` (DL-2), adres seçimi ve boş
> liste kuralı (DL-3) eklendi; FU-0178-FU01-MULTIADDR ve FU-0178-FU01-POSTAL gereksizleşti. (3) CT bulgusu: MVP-6 bugün müşteri seçicisini
> CRM'in genel hesap uçlarından sözleşmesiz okuyor; FU01 bunu yapmaz (§8.7, Q-6). (4) Q-12 sahip kararı (Beste, "evet", commit `ad1100158`):
> seçimden sonra pasifleşen account'ta sevkiyat sürer; Edit kaydı ve "sevke hazır"da Web proxy durumu `ship-to`'dan okuyup uyarır, akışı
> durdurmaz (DL-3, §8.4). Yeni açık soru Q-13 (CRM — `ship-to` cevabında account durumu). AC numaraları değişmedi; kapanış tablosu §19.6. Künye `draft` / `pending`: r2'nin kendi CT-APR-02 incelemesi ve CT-APR-01 onay satırı gerekir.
>
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
> **Ölçüm tabanı:** r1 / r2 dosya ve satır atıfları `8913e60f0`'da (CT dalı, worktree `wms-ct-01`) okundu; kimlik ön kontrolü
> `6235794ab`'de koşuldu (aradaki iki commit yalnız MOD-0180 / MOD-0182 paket ve karar dosyalarıdır). **r3:** Web atıfları (`form.js`,
> `details.js`, `_Form.cshtml`, `Details.cshtml`) FIX-01'li tabanda, `b8becef7d`'de (FIX-01 `49fd410b7` bu zincirin atası) yeniden
> ölçüldü; InventoryService, Web `OutboundShipmentsController.cs`, `SupplyChainShipmentsController.cs`, `crm-account-lookup.md` ve
> `wms-bundle.openapi.yaml` `8913e60f0`'dan beri değişmedi (`git diff --stat 8913e60f0 b8becef7d` bu yollarda boş). **Çıkarım** ve **öneri** olarak
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
(`frontend/Diten.Web/wwwroot/assets/js/Warehouse/OutboundShipments/form.js:72-91`, `lineRow` `:75-91`), alıcıyı yalnız elle aldırıyor
(`frontend/Diten.Web/Views/Warehouse/OutboundShipments/_Form.cshtml:98-130`) ve "Tahsisi önizle" seçilen lotu yok sayıyordu (önizleme her
satırı `lotId: ''` ile gönderiyordu; WP-0178-E3-FIX-01 madde 4). **FIX-01 (CT VERIFIED, `49fd410b7`) bunu bugünkü formda düzeltti:**
önizleme gövdesi satırı `rules.lineBody` ile, `lotOverride`'lı gönderir (`form.js:223-226`), lot değişimi önizlemeyi yeniden koşar
(`form.js:104-105`) ve sapma notu çizilir (`form.js:181-185`). FU01 bu davranışı yeniden yazmaz, yan panele ve tabloya taşır (D-4). CT'nin
FIX-01 turunda ek gözlem: Details'te FEFO sapma gerekçesi "Rezervasyon" sütununda "Tutuluyor" altında görünüyor
(`details.js:63-64`; kabul kaydı "Düşük gözlemler") — DL-1 bunu ayrı sütuna alır (§8.2).

Bu FU formu üç adımda yeniler:

1. **DL-1 — satırlar DataTable'da, ekleme / düzenleme yan panelde (D-1, D-4).** Satır listesi Golden Reference DataTable v2 deseninde
   (istemci modu, en çok 100 satır); satır ekleme, düzenleme ve lot değişimi Golden Slim `_CreateEditOffcanvas.cshtml` deseninde bir yan
   panelde. Önizleme hem panelde (tek satır) hem tabloda (bütün satırlar) görünür ve **seçilen lotu gösterir**. Details tahsis tablosunda
   FEFO sapma gerekçesi ayrı sütundur. Yalnız Web; servis ve sözleşme değişmez.
2. **DL-2 — outbound kaydında CRM müşteri kimliği (D-2, sunucu + sözleşme).** Outbound isteğe bağlı `customerRef` (MOD-0149
   `Account.Id` — Q-4) ve CRM adresinden gelen kopyada isteğe bağlı `customerAddressId` (CRM adres kimliği, ör. `"primary"`) taşır;
   `customerRef` varsa InventoryService kaydetmeden önce CRM-ACCOUNT-LOOKUP U-1 ile doğrular (yalnız `active` — Q-2; karar sözleşmenin
   `isBillable` alanıyla, §8.5). Adres her zaman
   **o anki kopya**dır (`shipTo`); kullanıcı düzenleyebilir, kopya düzenlenmiş değeri tutar, account kimliği kalır (Q-3); CRM'den sonradan
   değişmez. WMS-BUNDLE ve WAREHOUSE-OUTBOUND'a eklemeli üye.
3. **DL-3 — alıcıyı CRM'den seçme (D-2, Web).** Alıcı kartında CRM müşteri seçici: ad / kodla arama (yalnız `active`), seçince
   CRM-ACCOUNT-LOOKUP 1.1.0'ın yeni `GET /api/crm/accounts/{id}/ship-to` ucu account'un **adres listesini** döner (CRM güncel önerisi:
   `addresses[]` + `defaultAddressId`; ilk sürümde tek eleman, ana adres); kullanıcı adresi seçer (varsayılan önceden seçili), ad ve adres
   alanları doldurulur; ülke / şehir / ilçe **kod** olarak gelir, görünen ad Reference Data'dan çözülür ve kopyaya kod + ad yazılır. Liste
   boşsa bağlantı kurulur, adres elle girilir. CRM 1.1 donmadan bu dilim başlamaz. Seçici yalnız CRM anahtarı olan kullanıcıya çizilir
   (UAS-001, §8.4).
   **Elle giriş her dilimde çalışır** — CRM kapalıyken ve CRM anahtarı yokken de.

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
| CRM hesabı, CRM-ACCOUNT-LOOKUP sözleşmesi (1.1 ship-to ucu dahil) | MOD-0149 (commercial-suite, CrmService 5061; sahibi Beste — CRM) | yalnız tüketici tarafı (CRM cevabı C-1…C-5, güncel öneri C-6…C-11); sözleşmeye ve CRM manifestine yazmaz; §5a tüketici satırını CRM sahibinden ister (XMC-CRM-01, §7) |
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
- **Günlük kuralı (r3, F-9; emsal MVP-6 SB-21 — `MOD-0183-shipment-tracking-pod.md:1009`):** yeni CRM adaptörü (`WarehouseCustomerDirectory`)
  ve Web proxy eylemleri (`customers`, `customer-ship-to`, `edit/{id}` / `release/{id}` Q-12 okuması) CRM'den gelen **hiçbir** değeri
  günlüğe, OTel özniteliğine ya da yerel olay `Data`'sına yazmaz: ad, hesap kodu, adres alanları, posta kodu, `label`, gövde. Başarısızlıkta
  yazılabilenler yalnız kimlikler (isteğin `accountId`'si, outbound kimliği), sonuç sınıfı / HTTP durum kodu ve `X-Correlation-Id`'dir.
  Recall okuyucusu da yalnız kimlik + nedeni yazar (`CrmAccountLookupHttpReader.cs:20`, `:129-133`); FU01 bunu kopyalamaz (Q-9), aynı sınırı uygular.
- Bu FU tahsis algoritmasını, FEFO kuralını, durum makinesini, izin anahtarlarını ve kayıt kodlarını değiştirmez.

## 3. Owned Objects

Yeni koleksiyon, yeni komut, yeni izin anahtarı yok. Değişen nesneler:

| Nesne | Yer | Değişiklik | Dilim |
|---|---|---|---|
| Outbound Create / Edit ekranı | `Views/Warehouse/OutboundShipments/_Form.cshtml`, `form.js`, `rules.js` | satır tablosu + satır paneli + önizleme gösterimi | DL-1 |
| `OutboundShipment` | `wms_outbound_shipments` | eklenen alanlar `CustomerRef`, `CustomerAddressId` (§4) | DL-2 |
| `CreateOutboundShipmentCommand`, `UpdateOutboundShipmentCommand`, `PreviewOutboundAllocationQuery` | InventoryService | gövdeye `customerRef`, `customerAddressId`; güncellemeye `removeCustomerRef` (§8.5) | DL-2 |
| `WarehouseErrorCodes` | InventoryService | eklenen kodlar `WMS_CUSTOMER_NOT_FOUND`, `WMS_CUSTOMER_NOT_ACTIVE` (Q-2 kararı: yalnız `active` = `isBillable`), `WMS_CUSTOMER_DIRECTORY_UNAVAILABLE` (r3: üç kod — CRM 401 / 403 de 503, F-2) | DL-2 |
| WMS-BUNDLE, WAREHOUSE-OUTBOUND | `docs/analysis/contracts/` | eklemeli `customerRef` (§8.7) | DL-2 (CT adımı) |
| `OutboundAddress` (`shipTo` kopyası) | `wms_outbound_shipments` | eklenen isteğe bağlı kod / ad üyeleri (§4.1) | DL-2 |
| Alıcı kartı (CRM seçici + teslimat adresi seçimi) + Web proxy eylemleri `customers`, `customer-ship-to` | `_Form.cshtml`, `form.js`, `OutboundShipmentsController.cs` (Web) | seçici, adres listesinden seçim (varsayılan önceden seçili), ön doldurma (kod → ad), boş liste kuralı, bağlantıyı kaldırma; seçici ve adres seçici yalnız CRM anahtarıyla çizilir (UAS-001, r3) | DL-3 |
| Details tahsis tablosu | `details.js` (`renderLines`, `:58-71`), resx | FEFO sapma gerekçesi ayrı sütun (r3, CT gözlemi; bugün "Rezervasyon" hücresinde — `:63-64`) | DL-1 |
| Details özet kartı | `Details.cshtml`, `details.js` | "CRM müşterisi" rozeti (GUID gösterilmez); ülke adı ve ilçe satırı (r3, F-11); "sevke hazır" cevabındaki pasif account uyarısının gösterimi (Q-12) | DL-3 |
| Web proxy eylemleri `edit/{id}`, `release/{id}` | `OutboundShipmentsController.cs` (Web) | başarılı cevaptan sonra `ship-to` durum okuması + Web cevabına `customerWarning` (Q-12; WMS sözleşmesi değişmez) | DL-3 |

## 4. Entity Fields

### 4.1 `OutboundShipment` eklenen alanlar (DL-2)

| Alan | Tip | Kural |
|---|---|---|
| `CustomerRef` | `Guid?` | MOD-0149 CRM `Account.Id` (Q-4 kararı: ad `customerRef`, değer `Account.Id` — SHIPMENT-BUNDLE 3.5.0 ile aynı). `null` = alıcı elle girildi **ya da** kayıt FU01'den önce yazıldı (ayırt edilmez; geri doldurma yok). Sözleşmede kanonik küçük harf "D" GUID; girişte büyük / küçük harf duyarsız (emsal RECALL 1.10.0). Yazıldığı (atandığı ya da değiştiği) anda CRM U-1 ile doğrulanmıştır (§8.5); sonradan CRM'de silinse / pasifleşse de kayıt değişmez ve değeri değişmeyen güncellemede yeniden doğrulanmaz (r3, F-8). Yerel zaman çizelgesinin `Data`'sına **değer yazılmaz**, yalnız `customerRefChanged` / `customerRefEntered` işareti (ebeveyn kuralı: `ShipTo` değeri `Data`'da tutulmaz — `services/Diten.InventoryService/src/Diten.InventoryService.Domain/Warehouse/Entities/OutboundShipmentEntities.cs:277`). |
| `CustomerAddressId` | `string?` ≤ 100 | r2 — CRM ship-to listesindeki `addressId` (ilk sürümde `"primary"`; CRM güncel önerisi). Kopyanın **kaynağı**dır, adresin kendisi değildir: yalnız `CustomerRef` doluyken yazılır (`CustomerRef`'siz gelirse 400 `INVALID_REQUEST`); kullanıcı CRM listesinden bir adres seçip kaydettiyse dolu, liste boşsa ya da adres elle girildiyse boş. Kullanıcı seçilen adresi düzenlerse kimlik **kalır** (Q-3: kopya düzenlenmiş değeri tutar, bağlantı kalır). Sunucu kimliği CRM'e karşı doğrulamaz (öneri — kayıt CRM ship-to anahtarını gerektirmez; kopyanın kaynağı kullanıcının onayladığı formdur). Yerel olay `Data`'sına değer yazılmaz. |

### 4.1a `OutboundAddress` (`ShipTo`) eklenen üyeler (DL-2; CRM cevabı C-2)

`ShipTo` (`OutboundAddress`: `Name`, `Country`, `City`, `PostalCode`, `Line1`, `Line2` — `OutboundShipmentEntities.cs:13-28`) CRM'den
seçilen alıcıda da kullanıcının kaydettiği **kopyadır** (D-2); kullanıcı CRM'den gelen değerleri düzenleyebilir ve kopya düzenlenmiş değeri
tutar (Q-3). CRM 1.1 ship-to ucu her adres öğesinde ülke / şehir / ilçeyi **kod** döndürür (Reference Data kümeleri `country`, `city`,
`district` — CRM güncel önerisi); görünen ad WMS'te Reference Data'dan çözülür ve kopyaya **kod + ad** yazılır (C-2). Bugünkü üyeler anlamını
korur; eklenenler isteğe bağlıdır:

| Üye | Tip | Kural |
|---|---|---|
| `Country` (var) | string | donmuş `Address.country` = "lowercase country code (Territory ile uyumlu)" (`docs/analysis/contracts/warehouse-outbound.openapi.yaml:81`) — CRM `CountryRef` kodu buraya küçük harfle yazılır |
| `CountryName` (yeni) | string? ≤ 200 | ülkenin görünen adı, kayıt anındaki kullanıcı dilinde (ad dile bağlıdır; kopya o anın adıdır) |
| `City` (var) | string? | şehrin görünen adı (kayıt anındaki dil); elle girişte serbest metin |
| `CityRef` (yeni) | string? ≤ 100 | CRM `CityRef` kodu; yalnız CRM'den seçilen alıcıda dolu |
| `DistrictRef` (yeni) | string? ≤ 100 | CRM `DistrictRef` kodu; yalnız CRM'den seçilen alıcıda dolu |
| `District` (yeni) | string? ≤ 200 | ilçenin görünen adı (kayıt anındaki dil) |
| `PostalCode` (var) | string? | CRM adres öğesinin `postalCode`'u (CRM güncel önerisi: biçim ilk günden var; ana adrese alanı CRM ekliyor, tarih CRM'de — eklenene kadar ana adreste boş gelir). Q-10 kararı: CRM'den gelen değer doluysa kopyaya yazılır, boşsa kullanıcı formda elle ekleyebilir; posta kodsuz kayıt da geçerlidir (donmuş `Address.postalCode` isteğe bağlı) |
| `Line1` (var) | string? | CRM adres öğesinin `addressLine`'ı |

Kod kümesi CRM 1.1 sözleşmesinde yazılacaktır (C-2); sunucu kodları Reference Data'ya karşı doğrulamaz (öneri — kopyanın kaynağı
kullanıcının onayladığı formdur; Q-11). Elle girişte kod üyeleri boş kalır. Yerel olay `Data`'sına adres değeri yazılmaz (ebeveyn kuralı).

**r3 (F-9) — KVKK ve karşılaştırma:** dört yeni üye (`CountryName`, `CityRef`, `District`, `DistrictRef`) `OutboundAddress`'in KVKK
yorumunun kapsamındadır ("never in a log, an OTel attribute or a local event's `Data`" — `OutboundShipmentEntities.cs:11-12`); yorum dört
üyeyi adıyla sayacak biçimde güncellenir. `OutboundAddress.SameAs` (`OutboundShipmentEntities.cs:29-31`) dört üyeyi de karşılaştırır →
yalnız ilçe ya da yalnız kod değişen güncellemede `shipToChanged: "true"` yazılır (`UpdateOutboundShipmentHandler.cs:89`); `Copy()`
`MemberwiseClone` olduğu için ek iş istemez. İstek anahtarı parmak izi (`CreateOutboundShipmentHandler.cs:202`,
`UpdateOutboundShipmentHandler.cs:50`) dört üyeyi, `customerRef`'i ve `customerAddressId`'yi de taşır (aynı anahtar + farklı değer → 400
`IDEMPOTENCY_KEY_REUSED`, AC-19). Web Edit'in değişiklik karşılaştırması (`form.js:267`, bugün altı üye) da dört üyeyi sayar.

### 4.2 İndeks

Yeni indeks yok. `CustomerRef` ile liste süzgeci bu FU'da yoktur (FU-0178-FU01-FILTER). `CustomerAddressId` indekslenmez.

## 5. Repo Scope

**DL-1 (yalnız Web):**
- `frontend/Diten.Web/Views/Warehouse/OutboundShipments/_Form.cshtml` — "Satırlar" kartı: tablo + araç çubuğu; yan panel partial'ı.
- `frontend/Diten.Web/Views/Warehouse/OutboundShipments/_LineOffcanvas.cshtml` (**yeni**) — satır paneli (Golden Slim `_CreateEditOffcanvas.cshtml` deseni).
- `frontend/Diten.Web/Views/Warehouse/OutboundShipments/Create.cshtml`, `Edit.cshtml` — yalnız partial / betik bağlantıları (Layout değişmez).
- `frontend/Diten.Web/wwwroot/assets/js/Warehouse/OutboundShipments/form.js` — satır modeli (bellekte dizi), tablo, panel, önizleme.
- `frontend/Diten.Web/wwwroot/assets/js/Warehouse/OutboundShipments/rules.js` — saf kurallar (satır doğrulama, `lotNeed`, önizleme eşleme, 100 sınırı).
- `frontend/Diten.Web/wwwroot/assets/js/Warehouse/OutboundShipments/details.js` — **yalnız** `renderLines` (`:58-71`): FEFO sapma gerekçesi
  ayrı sütun (r3, CT gözlemi — §8.2); onay akışları (FIX-01 madde 1) değişmez.
- `frontend/Diten.Web/wwwroot/assets/js/Warehouse/OutboundShipments/index.l10n.js`, `_IndexL10n.cshtml`, `Resources/Views/Warehouse/OutboundShipments/OutboundShipmentsIndex.{en,tr,fr,es,zh,ar,ru}.resx` — yeni anahtarlar.
- `frontend/Diten.Web/wwwroot/assets/css/Warehouse/warehouse.css` — gerekirse panel / rozet sınıfları (yeni CSS dosyası yok — ebeveyn §11.5).
- `frontend/Diten.Web/tests/**` — DL-1 testleri (vitest; §17).

**DL-2 (servis + sözleşme):**
- `services/Diten.InventoryService/src/Diten.InventoryService.Domain/Warehouse/Entities/OutboundShipmentEntities.cs` — `CustomerRef`; `OutboundAddress` kod / ad üyeleri, KVKK yorumu ve `SameAs` (§4.1a, r3).
- `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/OutboundShipments/**` — `OutboundShipmentModels.cs` (giriş / görünüm), `Services/OutboundAllocationPlanner.cs` (yalnız `ShapeError` — `customerRef` biçimi; plan değişmez), `Handlers/CommandHandlers/CreateOutboundShipmentHandler.cs`, `UpdateOutboundShipmentHandler.cs` (CRM doğrulaması, olay işareti), `Services/OutboundShipmentReads.cs` (görünüm + donmuş GET üyesi).
- `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/Warehouse/Ports/IWarehouseCustomerDirectory.cs` (**yeni** port) ve `services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/Warehouse/WarehouseCustomerDirectory.cs` (**yeni**; U-1 okuyucusu — `Recall/CrmAccountLookupHttpReader.cs` deseni; Q-9 kararı: Recall okuyucusuyla kod paylaşılmaz, Warehouse kendi okuyucusunu yazar, Recall dosyaları değişmez).
- `services/Diten.InventoryService/src/Diten.InventoryService.Api/Warehouse/WarehouseServiceCollectionExtensions.cs` — port kaydı.
- `WarehouseErrorCodes` (üç kod — r3, F-2) ve hata kataloğu testi.
- `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Warehouse/**` — DL-2 testleri.
- `frontend/Diten.Web/Models/Warehouse/OutboundDtos.cs`, `frontend/Diten.Web/Controllers/OutboundShipmentsController.cs` — proxy gövdesine `customerRef` / `customerAddressId` / `removeCustomerRef` geçişi (seçici yok).
- `docs/analysis/contracts/wms-bundle.openapi.yaml`, `docs/analysis/contracts/warehouse-outbound.openapi.yaml` — **yalnız CT'nin tek yazıcı adımıyla** (§8.7); geliştirici bu dosyaları yazmaz.

**DL-3 (Web seçici):**
- `frontend/Diten.Web/Controllers/OutboundShipmentsController.cs` — `GET /Warehouse/OutboundShipments/customers?q=` (U-3, `billableOnly=true`) ve `GET /Warehouse/OutboundShipments/customer-ship-to/{accountId}` (CRM 1.1 `GET /api/crm/accounts/{id}/ship-to` → adres listesi + `defaultAddressId`; her öğe için Reference Data ile kod → ad çözümü, kullanıcı dilinde). CRM'in genel hesap uçları (`/api/crm/accounts`, `/api/crm/accounts/{id}`, `crm.account.read`) **çağrılmaz** (§8.7 CT bulgusu).
- Aynı denetleyicide `edit/{id}` ve `release/{id}` eylemlerine başarılı cevaptan sonra `ship-to` durum okuması ve `customerWarning` (Q-12, §8.4).
- Aynı denetleyicide `customers` / `customer-ship-to` eylemlerine CRM anahtarı kapısı (`_permissions.Has` — emsal `SupplyChainShipmentsController.cs:212`) ve Create / Edit görünüm modeline izin anlık görüntüsü (r3, F-5, §8.4).
- `_Form.cshtml` (seçicinin izin koşullu çizimi — emsal `Views/SupplyChain/Shipments/_Form.cshtml:12-13`), `form.js`, `rules.js`, `Details.cshtml`, `details.js` (yalnız özet kartı rozeti, ülke adı ve ilçe satırı — F-11, ve "sevke hazır" cevabındaki `customerWarning` gösterimi — Q-12), resx / l10n (7 dil), testler (vitest + `frontend/Diten.Web.Tests` görünüm / proxy testleri).

## 6. Protected Paths

- `gateway/Diten.ApiGateway/ocelot.json` — değişmez (§15); yalnız `integration-agent`.
- `services/Diten.CrmService/**`, `docs/analysis/contracts/crm-account-lookup.md`, `docs/analysis/contracts/drafts/crm-account-lookup-validation-v1.0-proposal.md` — CRM sahibinin (1.1 ship-to ucu dahil — C-1…C-5).
- `services/Diten.InventoryService/src/**/Recall/**` — MOD-0177'nin; okunur, değiştirilmez.
- MVP-6 dosyaları (`services/Diten.SupplyChainService/**`, `shipment-bundle.openapi.yaml`) — MOD-0183'ün.
- `services/Diten.AuthService/**` — rol eşlemesi Ali'nin WP'si (Q-5).
- `frontend/Diten.Web/wwwroot/assets/js/Warehouse/OutboundShipments/details.js` onay akışları (`openCancel`, `sendRelease`, `askReasons`, `openRelease`, `sync` — `:168-245`) — WP-0178-E3-FIX-01 madde 1'in (uygulandı, `49fd410b7`); FU01 yalnız DL-1'de `renderLines` gerekçe sütununa, DL-3'te özet kartına (rozet, ülke adı, ilçe) ve "sevke hazır" başarı dalına uyarı gösterimine (Q-12) dokunur; onay pencerelerinin kendisi değişmez.
- `.antigravity/**`, `execution/registries/**`, `execution/approvals/**` — CT / kullanıcı onayı.
- `docs/analysis/contracts/wms-bundle.openapi.yaml`, `warehouse-outbound.openapi.yaml` — yalnız CT tek yazıcı adımı.
- `_Layout.cshtml`, archive — donuk.

## 7. Dependencies

| Bağımlılık | Tür | Durum (ölçüldü) | Kullanım |
|---|---|---|---|
| WP-0178-E3-FIX-01 | DL-1 başlama kapısı — **kapandı (r3)** | **uygulandı ve CT VERIFIED** (`49fd410b7`; kabul kaydı `docs/records/audits/2026-11/mod-0178-e3-fix01-ct-acceptance-2026-11-13.md`; WP `execution/domains/supply-chain-execution/work-packs/mvp5-wms/WP-0178-E3-FIX-01-ui-l10n.md`); MVP-1 hat dalında (`feature/sce/mod-0173-inventory-ledger-valuation`) ve bu CT zincirinin HEAD'inde (`b8becef7d`'nin atası). Kabul kaydının düşük gözlemi (Details'te FEFO gerekçesi "Rezervasyon" sütununda) FU01 DL-1'e alındı | aynı dosyalar (`form.js`, `_Form.cshtml`, resx); sayı biçimleyicisi (madde 2) ve "Çıkış sevkiyatı" terimleri (madde 3) FU01'de yeniden kullanılır; madde 4'ün en dar düzeltmesi FU01'den önce canlıya gider (§8.1) |
| MOD-0178 S2 servis (önizleme, oluşturma, düzenleme) | HARD | main'de (ebeveyn DL-4…DL-6, PR #397) | `POST /outbound-shipments/allocation-preview` gövdesi oluşturma gövdesidir ve `lotOverride` taşır (`docs/analysis/contracts/wms-bundle.openapi.yaml:1990-2001`, satır şeması `:1953-1977`); servis önizlemede lot değişimini uygular ve sapmayı reddetmeden bildirir (`services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/OutboundShipments/Services/OutboundAllocationPlanner.cs:233-236`, `:431-458`) — DL-1 için **sözleşme değişikliği gerekmez** |
| CRM-ACCOUNT-LOOKUP 1.0.0 U-1 | DL-2 HARD | FROZEN, main'de (`docs/analysis/contracts/crm-account-lookup.md`, `x-implemented` 38f7ea08d) | kayıtta `customerRef` doğrulaması — sınıflandırma sözleşmenin §4 (`:205-223`, karar `isBillable` ile) ve §5 (`:225-248`, çelişkili / eksik gövde ve 400 / 401 / 403 / 5xx → 503) kurallarıyla, §9 fikstürleriyle (`:311-332`) |
| **XMC-CRM-01 — MOD-0178'in CRM-ACCOUNT-LOOKUP §5a tüketici satırı** (r3, F-3) | **DL-2 kabul kapısı** (U-1) ve **DL-3 başlama kapısı** (U-3 + 1.1 `ship-to`) | §5a tablosunda MOD-0178 yok (`crm-account-lookup.md:250-267`: MOD-0120 tüketici; MOD-0177 ve MOD-0183 / MOD-0186 aday); "Yeni bir tüketici bu tabloya eklenerek sözleşmeye girer (1.x içinde ekleme)" (`:267`). Karar dosyası eki "CRM güncel önerisi" bunu "Açık (CT → CRM)" olarak kaydetti (commit `b8becef7d`). **XMC-001 isteği (Beste — CRM, CRM oturumu):** §5a'ya satır — tüketici `MOD-0178 Putaway / Picking / Packing (MVP-5, Diten.InventoryService + Diten.Web) — MOD-0178-FU01`; uçlar U-1 (outbound kaydında `customerRef` doğrulaması, §5 sınıflandırması), U-3 (alıcı seçici, `billableOnly=true`), 1.1 `GET /api/crm/accounts/{id}/ship-to` (adres listesi; Q-12 durum okuması); amaç "depo çıkış sevkiyatında alıcı seçimi, kayıt öncesi uygunluk, adres kopyası"; çağrı yolu §5a (sunucu tarafı, Gateway, kullanıcı token'ı); izin `crm.account.lookup-validation` + `crm.account-ship-to.read` (rol eşlemesi Q-5). U-1 ve U-3 satırı 1.0 içinde eklenebilir; `ship-to` sütunu 1.1 metniyle birlikte. **FU01 sözleşme dosyasına yazmaz** (§6) | CRM sahibi yazar; CT isteği iletir ve kapıda satırın commit'ini atıfla doğrular |
| CRM-ACCOUNT-LOOKUP 1.0.0 U-3 | DL-3 HARD | FROZEN, main'de | seçici araması (ad / kod) |
| **CRM-ACCOUNT-LOOKUP 1.1.0 — `GET /api/crm/accounts/{id}/ship-to`** | **DL-3 başlama kapısı = 1.1 donması** | **depoda yok** — bu depodaki metin 1.0: U-3 öğeleri adres taşımaz (`crm-account-lookup.md:150`), U-1 yalnız doğrulama alanları (`services/Diten.CrmService/src/Diten.CrmService.Application/Features/Account/AccountModels.cs:86-94`), K-9 açık (`crm-account-lookup.md:351`); hesapta adres var (`services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/Account.cs:27-30`: `CountryRef`, `CityRef`, `DistrictRef`, `AddressLine`; posta kodu yok). **CRM sahibinin cevabı (C-1…C-5, Beste, CRM oturumu, 2026-11-13):** yalnız ekleme; yeni uç; kiracı sınırı; 404 (yok / silinmiş / başka kiracı aynı cevap); `tenant_id`'siz token → 400; 5xx → 503; adres U-1 / U-3'e **eklenmez**; ülke / şehir / ilçe kod döner; anahtar `crm.account-ship-to.read`; sıra 1.1 taslağı → inceleme → kod → donma. **CRM'in güncel önerisi (C-6…C-11; karar dosyası eki "2026-11-13: CRM güncel önerisi (C-6…C-11)", commit `b8becef7d` — CRM oturumunun cevabı, Beste'nin sohbete yapıştırdığı metin; taslak dosyası CRM dalında, bu depoda yok):** cevap ilk günden liste — `addresses[]` (`addressId`, `kind`, `label`, `isDefault`, `countryRef`, `cityRef`, `districtRef`, `postalCode`, `addressLine`) + `defaultAddressId`; ilk sürümde tek eleman (ana adres, `addressId = "primary"`, `postalCode` boş — ana adrese posta kodu alanını CRM ekliyor, tarih CRM'de); ek teslimat adresleri sonra, biçim ve sürüm değişmeden; **tahmin:** yalnız ana adresle uç ~2 hafta, çoklu adres +2–3 hafta | sahibi Beste (CRM) — CRM başka bir bilgisayarda geliştiriliyor |
| Reference Data (ülke / şehir / ilçe kod → görünen ad) | DL-3 HARD | kaynak ve uç bu pakette ölçülmedi (Q-11) | ön doldurmada kodların kullanıcının dilindeki adı |
| MOD-0018 Auth rol eşlemesi | DL-2 / DL-3 kabul kapısı | yok | `crm.account.lookup-validation` (K-1, `crm-account-lookup.md:346`; U-1 doğrulama + U-3 arama) ve `crm.account-ship-to.read` (C-4; anahtarı CRM manifestine CRM ekler) → `ShippingPlanner`, `WarehouseSupervisor` — CRM'den alıcı seçimi için **iki anahtar birden** gerekir (CRM güncel önerisi) (Q-5, Ali) |
| WAREHOUSE-OUTBOUND / WMS-BUNDLE sonraki eklemeli sürüm | DL-2 yayın kapısı | WAREHOUSE-OUTBOUND 1.0.0 FROZEN (`docs/analysis/contracts/warehouse-outbound.openapi.yaml:12-13`); WMS-BUNDLE 1.1.0 (`wms-bundle.openapi.yaml:64`); Item adımı (WAREHOUSE-OUTBOUND 1.1.0 + WMS-BUNDLE 1.2.0) MOD-0183 r10 onayını bekliyor (ebeveyn §19.7) | §8.7; Q-7 |
| MOD-0183 (MVP-6) | XMC-001 bilgi + soru | r9 taslak; okuma şeması açık, eklenen alanı anlık görüntüye yazar (`execution/domains/supply-chain-execution/module-packs/MOD-0183-shipment-tracking-pod.md:256-262`); intake `customerRef` yazmıyor (`MOD-0183-shipment-tracking-pod.md:934`); Web müşteri seçicisi bugün CRM'in genel hesap uçlarını sözleşmesiz okuyor (`frontend/Diten.Web/Controllers/SupplyChainShipmentsController.cs:206-233`, CT bulgusu `a7c1d32b8`, §8.7) | Q-6 (Natig) |
| MOD-0168 Order Capture | yok (FU) | registry `reserved / planned` (`execution/registries/module-id-registry.md:301`); paket ve kod yok | FU-0178-FU01-ORDER |

## 8. Runtime Constraints

### 8.1 Sıra ve FIX-01 ile çakışmama

- **WP-0178-E3-FIX-01 önce gitti (r3: uygulandı, CT VERIFIED `49fd410b7`).** FU01 dalı FIX-01'li tabandan açılır (MVP-1 hat dalı ya da
  FIX-01'i içeren CT zinciri). FIX-01'in dört maddesi FU01'de **yeniden yazılmaz**:
  - madde 1 (onay pencereleri `window.showConfirm`, `details.js:168-245`) — FU01 onay akışlarını değiştirmez; Q-12 uyarısı (DL-3) aynı
    `window.showConfirm`'ü kullanır;
  - madde 2 (kültüre göre sayı biçimi: `rules.quantityText`, `form.js:31-33`, `details.js:26-28`) — FU01 tablo ve panelde **aynı
    biçimleyiciyi** kullanır; yeni biçimleyici yazmaz;
  - madde 3 ("Outbound" → "Çıkış sevkiyatı") — FU01'in yeni anahtarları FIX-01'in terimini kullanır;
  - madde 4 (önizleme seçilen lotu gönderir — `form.js:104-105`, `:181-185`, `:223-226`) — FU01 aynı davranışı yan panele ve tabloya taşır
    (AC-5, AC-6); FIX-01'in testi `frontend/Diten.Web/tests/warehouse-s2-e3-fix-01.test.js` FU01'in DOM'una göre güncellenir, sabotaj b'nin (`lotId: ''`)
    karşılığı S-FU01-5'tir.
- FU01 DL-1, FIX-01'in değiştirdiği satırları (`form.js:187-207` `renderPreview`, `_Form.cshtml:73-95` "Satırlar" kartı) yeniden yazar;
  FIX-01 kabul edildiği için çakışma yoktur. FU01 dalı FIX-01'siz bir tabandan açılmaz (§24 DL-1 kapısı).

### 8.2 Satır tablosu

Dilim DL-1; sahip kararı D-1.

- Create sayfasının "Satırlar" kartı bugünkü kart listesinin (`_Form.cshtml:83-92`) yerine bir DataTable taşır:
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
  Satırlar ve tahsis düzenlemede değişmez (ebeveyn §8.17; bugünkü `LinesLockedHint`). Bugünkü salt okunur liste (`form.js:293-294`) bu
  tabloya döner.
- **FEFO sapma gerekçesi (r3, CT gözlemi — FIX-01 kabul kaydı "Düşük gözlemler"):** gerekçe rezervasyon durumunun altında gösterilmez.
  Create / Edit satır tablosunda gerekçe, lot hücresinin altında **satır altı metin** olarak durur (etiket "FEFO gerekçesi:", `text-break`;
  Create'te panelde girilen, Edit'te tahsisin kayıtlı `fefoBypassReason`'ı). Details tahsis tablosunda (`details.js:58-71`) gerekçe **ayrı
  sütundur** — "FEFO gerekçesi" (`AllocFefoReason`, 7 dil), "Rezervasyon" sütunundan sonra; boşsa "-". "Rezervasyon" sütunu yalnız
  rezervasyon durumunu taşır (bugün gerekçe aynı hücrede, `details.js:63-64`). 375 px'te tablo mevcut `table-responsive` kabıyla kayar,
  sayfa kaymaz.
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
  `WMS_TARGET_NOT_ALLOWED` gibi başlık hataları Staging alanının altında gösterilir (FIX-01 madde 4; uygulandı — `form.js:209-215`).
- **Eskime:** depo, Staging ya da bir satırın ürün / SKU / miktar / lotu değişince ilgili önizleme `Önizleme eski` olur. Seçilmiş lot
  silinmez; yeniden önizlemede lot artık listede yoksa satır `Karşılanamaz` + "Seçilen lot artık uygun değil" — sessiz sıfırlama yok.
- **Kaydet:** istemci kuralları (en az 1 satır, her satır tam, sapma kuralı son önizlemeye göre — bugünkü `rules.lotNeed`) geçerse gövde
  bugünkü sözleşmeyle gider (`CreateOutboundShipmentRequest`, `wms-bundle.openapi.yaml:1978-1989`); sunucu tahsisi yeniden hesaplar ve
  karar odur. `422 WMS_ALLOCATION_FAILED` `details.lines[]` satır rozetlerine eşlenir.

### 8.4 Alıcı seçimi

Dilim DL-3 (CRM müşterisi ya da elle giriş); sahip kararları D-2, Q-2, Q-3, Q-10, Q-12; CRM cevabı C-1…C-5 ve CRM'in güncel önerisi
C-6…C-11 (karar dosyası eki, `b8becef7d`).

- **Çizim kapısı (r3, UAS-001, F-5):** CRM kontrolleri yalnız anahtarı olan kullanıcıya **çizilir**; anahtarı olmayana devre dışı ya da
  açıklamalı kontrol de çizilmez (`unauthorized-surface-standard.md` §6 "Tıklanamayacak buton gösterilmez"). Kaynak, sayfanın sunucu
  tarafındaki izin anlık görüntüsüdür (`Perms.Has` / `_permissions.Has` — MVP-6 emsali görünüm kapısı
  `Views/SupplyChain/Shipments/_Form.cshtml:12-13`, proxy kapısı `SupplyChainShipmentsController.cs:212`; MVP-6 kontrolü devre dışı +
  gerekçeyle çizer, FU01 hiç çizmez — standarda daha dar uyar):

  | Kullanıcının CRM anahtarları | Çizilen | CRM çağrısı |
  |---|---|---|
  | `crm.account.lookup-validation` + `crm.account-ship-to.read` | "CRM müşterisi" seçici, "Teslimat adresi" seçici, "CRM'den yeniden al", "Bağlantıyı kaldır" | U-3, `ship-to` |
  | yalnız `crm.account.lookup-validation` | "CRM müşterisi" seçici, "Bağlantıyı kaldır"; adres seçici ve "CRM'den yeniden al" **yok** | yalnız U-3; seçimde ad U-3 satırından dolar, adres alanları elle, `customerAddressId` gönderilmez; `ship-to` çağrılmaz |
  | `crm.account.lookup-validation` yok | hiçbir CRM kontrolü yok; alıcı kartı bugünkü elle giriş alanlarıdır. Edit'te bağlı outbound'da "CRM müşterisi" rozeti ve "Bağlantıyı kaldır" görünür (kaldırma CRM çağırmaz, `.update` ile WMS'e yazılır) | yok |

  Proxy eylemleri (`customers`, `customer-ship-to`) aynı anahtarı sunucuda yeniden ister, yoksa 403 döner ve CRM'i çağırmaz (savunma
  derinliği; anlık görüntü ile token arasındaki fark için). Q-5 eşlemesi yapılmadıkça sevkiyat rollerinde seçici görünmez; sistem elle girişle çalışır.
- Alıcı kartının üstünde **"CRM müşterisi"** seçici (select2, en az 2 karakter): Web proxy `GET /Warehouse/OutboundShipments/customers?q=`
  → sunucu tarafında Gateway → CRM U-3 `GET /api/crm/accounts/lookup?search=&page=&pageSize=20&billableOnly=true` (kullanıcı token'ı;
  anahtar `crm.account.lookup-validation`). Q-2 kararı: **yalnız `active`** hesaplar seçilebilir — `billableOnly=true` CRM'de yalnız
  `status = "active"` hesapları döndürür (`docs/analysis/contracts/crm-account-lookup.md:136`); seçicideki satır uygunluk kararı değildir
  (sözleşme §5 son paragrafı) — karar kayıtta U-1 ile ve `isBillable` ile verilir (§8.5). Öğe: ad — kod.
- Seçince Web proxy `GET /Warehouse/OutboundShipments/customer-ship-to/{accountId}` → sunucu tarafında Gateway → CRM 1.1
  `GET /api/crm/accounts/{id}/ship-to` (kullanıcının token'ı; anahtar `crm.account-ship-to.read` — C-1, C-4). Cevap **adres listesidir**
  (CRM güncel önerisi): `addresses[]` (`addressId`, `kind`, `label`, `isDefault`, `countryRef`, `cityRef`, `districtRef`, `postalCode`,
  `addressLine`) + `defaultAddressId`; ilk sürümde tek eleman (ana adres, `addressId = "primary"`). Her öğede ülke / şehir / ilçe **kod**dur
  (C-2); proxy kodların görünen adını **Reference Data**'dan kullanıcının dilinde çözer (Q-11) ve forma her öğe için kod + ad verir.
- **Teslimat adresi seçimi (r2):** alıcı kartında "Teslimat adresi" seçicisi listeyi gösterir (etiket `label` ya da tür + adres özeti);
  `defaultAddressId` öğesi **önceden seçilidir** (yoksa ilk öğe). Liste tek elemanlıysa seçici o öğeyle dolu ve salt gösterimdir. Seçilen öğe
  formu doldurur: Alıcı adı (= hesap adı), Ülke (kod + ad), Şehir (ad; kod gizli alanda), İlçe (ad; kod gizli alanda — yeni alan, §4.1a),
  Posta kodu (öğenin `postalCode`'u **doluysa**), Adres 1 (`addressLine`); seçilen öğenin `addressId`'si gizli alanda tutulur ve gövdeye
  `customerAddressId` olarak gider. Başka bir adres seçmek alanları o öğeyle yeniden doldurur (kullanıcının düzenlemeleri o adres için
  geçersiz olur; seçmeden önce premium onay `window.showConfirm` — yalnız alanlar düzenlenmişse).
- **Posta kodu (Q-10 kararı):** CRM'den gelen `postalCode` doluysa kopyaya yazılır; boşsa kullanıcı formda elle ekleyebilir. Ana adreste alan
  CRM eklenene kadar boş gelir (ana adrese posta kodu alanını CRM ekliyor, tarih CRM'de); ek teslimat adreslerinde dolu gelir. Posta kodsuz
  kayıt geçerlidir.
- **Düzenleme (Q-3 kararı):** CRM'den gelen ad / adres alanları **düzenlenebilir**; kopya düzenlenmiş değeri tutar, account kimliği
  (`customerRef`) ve seçilen adresin kimliği (`customerAddressId`) **kalır** — alan düzenlemek bağlantıyı kaldırmaz. Altta "CRM kaydından
  dolduruldu ({kod}). Değişiklik yalnız bu sevkiyata yazılır." Kullanıcı CRM'den gelen şehir / ilçe adını değiştirirse ilgili kod
  gönderilmez (ad ile kod çelişmez).
- **Boş liste (r2, en dar yol):** `addresses[]` boşsa seçim geçerlidir — bağlantı kurulur (`customerRef`), alıcı adı hesap adıyla dolar,
  adres alanları boş ve açık kalır, `customerAddressId` gönderilmez; uyarı "Bu CRM kaydında teslimat adresi yok — adresi elle girin." Kayıt
  engellenmez; alıcı adı ve ülke her zamanki gibi zorunludur (§12).
- **Çoklu adres:** ek teslimat adresleri CRM'de sonra gelir; sözleşme biçimi ve sürümü değişmez (CRM güncel önerisi) → WMS ilk günden
  listeyle çalıştığı için ek iş gerekmez (r1'in FU-0178-FU01-MULTIADDR maddesi kapandı, §20).
- **CRM ship-to cevapları (1.0 kurallarıyla aynı — C-1, C-6):** 404 (yok / silinmiş / başka kiracı) → "CRM kaydı bulunamadı — alıcıyı elle
  girin", bağlantı kurulmaz; 400 (`tenant_id`'siz token), 401 / 403, 5xx / 503, zaman aşımı, okunamayan gövde → "CRM adresi okunamadı —
  adresi elle girin" (r3, F-2: sözleşme §5 / §6 gibi 401 / 403 erişilemiyor sınıfındadır; anahtarı olmayan kullanıcı bu çağrıyı zaten
  yapmaz — çizim kapısı). Reference Data adı çözemezse kod ve "Ad okunamadı" yazılır, kullanıcı adı elle düzeltebilir.
- "Bağlantıyı kaldır": `customerRef` ve `customerAddressId` gönderilmez, alanlar olduğu gibi kalır (elle giriş).
- **Elle giriş her zaman çalışır:** seçici kullanılmadan alanlar doğrudan yazılır; CRM çağrısı yapılmaz; kayıt `customerRef`'siz gider.
- **CRM hata durumları (seçicide):** U-3'ün 400 / 401 / 403 / 5xx / zaman aşımı cevabı → "CRM araması yapılamadı — alıcıyı elle girin"
  (sözleşme §5 son paragrafı: "U-3'ün 400 / 403 / 5xx yanıtı seçicide 'arama yapılamadı' olarak gösterilir"; r3, F-2). Elle alanlar açık
  kalır. Ship-to okuması 404 dışında başarısızsa ad (U-3 satırından) doldurulur, adres alanları boş kalır + uyarı; bağlantı korunur (hesap
  kimliği U-3'ten geçerli; kayıtta U-1 doğrular), `customerAddressId` gönderilmez.
- **Ülke alanı elle girişte de kod seçicidir (öneri, DL-3):** donmuş `Address.country` kod beklediği için (§4.1a) ülke Reference Data
  seçicisinden seçilir (kod + ad); şehir / ilçe elle girişte serbest metin kalır (kod yok). DL-3'ten önce (DL-1 / DL-2) ülke bugünkü gibi
  serbest metindir — bu davranış FU01'den önce de böyledir (`_Form.cshtml:107-110`).
- **Eski serbest metin ülke (r3, F-11):** FU01'den önce (ya da DL-3'ten önce) yazılmış outbound'un `shipTo.country`'si Reference Data kodu
  olmayabilir (ör. "Türkiye"). Edit'te ülke seçicisi kayıtlı değeri **olduğu gibi**, önceden seçili tek ek seçenek olarak gösterir
  (metin = kayıtlı değer + "(kayıtlı değer)" eki); değer silinmez, boş gösterilmez, kullanıcı koda geçmeye zorlanmaz. Kullanıcı ülkeye
  dokunmazsa ve öbür alıcı alanları da değişmezse `shipTo` gövdeye girmez (bugünkü karşılaştırma, `form.js:266-267`); öbür alıcı alanı
  değişirse kayıtlı ülke metni aynen gider (servis ülkeyi yalnız zorunlu + ≤ 200 olarak denetler — `OutboundAllocationPlanner.cs:132-133`).
  Kullanıcı listeden bir kod seçerse kod + `countryName` yazılır. Göç / geri doldurma yok (§8.5 madde 7).
- **Kayıtta CRM hatası:** `customerRef`'li kayıt 503 `WMS_CUSTOMER_DIRECTORY_UNAVAILABLE` alırsa (r3: CRM 401 / 403 de bu koddur, F-2)
  form uyarıyı ve **"Bağlantıyı kaldırıp elle kaydet"** eylemini gösterir; eylem `customerRef`'siz, `customerAddressId`'siz ve **yeni** istek anahtarıyla
  yeniden gönderir (aynı anahtar farklı gövdeyle 400 olur — ebeveyn §8.1).
- **Edit:** düzenlenebilir durumlarda (Allocated / Picked / Packed) müşteri seçilebilir, değiştirilebilir, bağlantı kaldırılabilir; adres
  kopyası yalnız kullanıcı "CRM'den yeniden al" derse (adres listesi yeniden okunur, kayıtlı `customerAddressId` öğesi listede varsa o,
  yoksa varsayılan önceden seçili) yenilenir. **Aynı account'ta adres değiştirme / "CRM'den yeniden al" (r3, F-8):** gövde kayıtlı
  `customerRef`'i aynen ve yeni `customerAddressId`'yi taşır; `customerRef` değişmediği için InventoryService U-1'i **çağırmaz** (§8.5
  madde 4) — seçimden sonra pasifleşen account'ta da adres güncellemesi sürer (Q-12 ile tutarlı: akış durmaz, uyarı Web'de).
- **Seçimden sonra pasifleşen account (Q-12 kararı — Beste, 2026-11-13, "evet"; karar dosyası eki, commit `ad1100158`):** yeni sevkiyatta
  yalnız `active` account seçilir (Q-2). Seçimden **sonra** pasif / askıda / arşiv olan account'ta mevcut sevkiyat **sürer**; WMS **kayıt**
  (Edit kaydı) ve **"sevke hazır"** (Packed → ReadyToShip) adımında durumu `ship-to` ucundan yeniden okur ve kullanıcıyı **uyarır**, akışı
  durdurmaz. Uygulama (DL-3, en dar yol — WMS sözleşmesi ve InventoryService değişmez): Web proxy eylemleri `edit/{id}`
  (`frontend/Diten.Web/Controllers/OutboundShipmentsController.cs:316-318`) ve `release/{id}` (`:332-334`) InventoryService cevabı **başarılı**
  döndükten sonra, outbound'un `customerRef`'i doluysa **ve kullanıcının `crm.account-ship-to.read` anahtarı varsa** (r3: yoksa `ship-to`
  çağrılmaz, uyarı yazılmaz) CRM 1.1 `ship-to`'yu kullanıcının token'ıyla okur; account uygun değilse (karar sözleşme §4 gibi `isBillable`
  ile — Q-13 cevabı `isBillable` taşımazsa `status = "active"` ⇔ `isBillable` değişmez kuralıyla, `crm-account-lookup.md:116`) tarayıcıya
  giden Web cevabına uyarı ekler (Web'in kendi JSON'u — `customerWarning { code: "CustomerNotActive", accountCode }`). Uyarı işlemi geri
  almaz, ret üretmez, ikinci bir WMS çağrısı yapmaz. Ship-to okunamazsa (400 / 401 / 403 / 404 / 5xx / zaman aşımı / çelişkili gövde)
  uyarı yazılmaz, cevap aynen döner (öneri).
  **Gösterim (r3, F-6 — uyarı kaybolmaz):** bugün Edit başarıda toast basıp hemen Details'e yönlendirir (`form.js:271-272`) — toast
  yönlendirmeyle kaybolur. Cevapta `customerWarning` varsa form **yönlendirmeden önce** premium bildirimi açar: `window.showConfirm`
  (MOD-0013; FIX-01 madde 1'in pencereleri gibi), ikon `bx-error` (emsal `details.js:217`), metin "Bu CRM müşterisi artık aktif değil
  ({kod}). Sevkiyat kaydedildi ve sürüyor.", `entityName` outbound numarası, tek anlamlı eylem "Tamam"; onay da kapatma da (`onCancel`)
  **aynı** yönlendirmeyi yapar, zaman aşımıyla kapanmaz. `customerWarning` yoksa akış bugünkü gibidir (toast + yönlendirme). Details'teki
  "sevke hazır" sayfada kalır (`details.js:197-203`: toast + `load()`); orada da aynı bildirim `load()`'dan sonra açılır. Sayfa durumu
  (URL bayrağı, oturum depolaması) kullanılmaz. `customerRef`'in
  kaynağı: başarılı cevabın görünümü (DL-2'den sonra `customerRef` taşır) — yoksa DL-3 geliştiricisinin ilk ölçümü. Durumun `ship-to`
  cevabında taşınması CRM 1.1 taslağına gereksinimdir: koordinatör özetindeki alan listesinde account durumu yok (Q-13).
  Yeni outbound oluşturma ve `customerRef` değiştiren güncelleme Q-2 kuralıyla kalır: InventoryService U-1 ile doğrular, `active` değilse 422 (§8.5).
- **Details:** özet kartında `shipTo` kopyası + `customerRef` varsa "CRM müşterisi" rozeti. Ayrıntıda CRM çağrısı yok, GUID gösterilmez.
  **r3 (F-11):** "Ülke" satırı (`Details.cshtml:93`, `details.js:142`) `countryName` doluysa "ad (kod)" (ör. "Türkiye (tr)"), boşsa
  `country`'yi olduğu gibi gösterir (eski serbest metin dahil); "Şehir" bugünkü gibi `city`; yeni "İlçe" satırı (`FieldDistrict`, 7 dil)
  "Şehir"den sonra `district`'i gösterir, boşsa "-". Kod üyeleri (`cityRef`, `districtRef`) gösterilmez. Details Create / Edit `_Form`'unun
  alanlarıyla eşleşir (`frontend-datatable-template.md` Compact eşliği).

### 8.5 Sunucu tarafında müşteri kimliği

Dilim DL-2 (`customerRef`, `customerAddressId`); sahip kararları D-2, Q-2, Q-3, Q-4.

1. **Giriş:** `CreateOutboundShipmentRequest` ve `PreviewOutboundAllocationRequest` isteğe bağlı `customerRef` (Uuid) ve
   `customerAddressId` (string, 1–100); `UpdateOutboundShipmentRequest` isteğe bağlı `customerRef` + `customerAddressId` (ata / değiştir —
   ikisi bir çifttir: `customerRef` gönderilen güncellemede `customerAddressId` gönderilen değerle değişir, gönderilmezse kalkar) ve
   `removeCustomerRef: true` (kaldır — `customerAddressId`'yi de kaldırır). `customerRef` ile `removeCustomerRef` birlikte → 400
   `INVALID_REQUEST`; `customerAddressId` `customerRef`'siz → 400 `INVALID_REQUEST`. JSON `null` yok (WMS-BUNDLE kural 4) — kaldırma bu
   yüzden ayrı bayraktır. `shipTo` isteğin kopyasıdır — kullanıcının düzenlediği değer olduğu gibi yazılır (Q-3); sunucu CRM'le
   karşılaştırmaz, CRM'den doldurmaz, `customerAddressId`'yi CRM'e karşı doğrulamaz.
2. **Önizleme:** `customerRef`'i ve `customerAddressId`'yi kabul eder (gövde oluşturma gövdesidir, `additionalProperties: false`) ama **CRM çağırmaz** ve sonucu etkilemez.
3. **Sıra (r3, F-7 — önce yerel kontroller, sonra CRM):** gövde biçimi (400 `INVALID_REQUEST` / `VALIDATION_ERROR`, CRM'siz) → istek
   anahtarı aranır (ebeveyn §8.1, WMS-BUNDLE kural 5; replay CRM'i çağırmaz) → **oluşturmada** CRM doğrulaması → ebeveyn §8.17 adım 1'in
   öbür ön kontrolleri; **güncellemede** bugünkü yerel kontrollerin hepsi (outbound görünür mü — 404; durum `ReadyToShip` / `Shipped` →
   409 `WMS_OUTBOUND_IMMUTABLE`, `Cancelled` → 409; `If-Match` bayat → 409 `WMS_CONFLICT` — `UpdateOutboundShipmentHandler.cs:52-65`)
   → **sonra** CRM doğrulaması → yazım. Yerel bir kontrolle reddedilen güncelleme CRM'i hiç çağırmaz (AC-20).
4. **Doğrulama (r3, F-1 — CRM-ACCOUNT-LOOKUP §4 / §5 sınıflandırması):** `IWarehouseCustomerDirectory.ValidateAsync(accountId)` → Gateway →
   U-1 `GET /api/crm/accounts/{accountId}/lookup-validation`, çağıranın token'ıyla, `X-Correlation-Id` ile, önbelleksiz, tek çağrı (zaman
   sınırı öneri 2 sn — Recall okuma başına sınırı emsali, `CrmAccountLookupHttpReader.cs:27`). **Karar `isBillable` iledir; `status` yalnız
   tutarlılık denetimi içindir** (`crm-account-lookup.md:223`). Sınıflandırma sözleşmenin §5 tablosudur (`:225-237`), AR'nin kodları
   yerine WMS kodlarıyla:
   - **uygun** — 200; `data.accountId` istenen kimlik (büyük / küçük harf duyarsız GUID eşitliği); `contractVersion` `crm-account-lookup/1.`
     ile başlar; `isBillable = true`; `status = "active"`; `code` ve `name` dolu → geçer (F-OK);
   - **tanımlı, uygun değil** — 200, tutarlı; `isBillable = false` ve `status ≠ "active"` (kümedeki `inactive`, `draft`, `suspended`,
     `archived` — F-NE-x — ya da kümede olmayan bir kod, ör. `closed` — F-UNK) → 422 `WMS_CUSTOMER_NOT_ACTIVE`;
   - **bulunamadı** — 404 (yok / silinmiş / başka kiracının — CRM üçünü aynı cevaplar; F-404) → 422 `WMS_CUSTOMER_NOT_FOUND`;
   - **geçersiz / çelişkili** — 200 ama `accountId` yok / farklı (F-C2); `isBillable` yok / `null` (F-C3); `isBillable = true` iken
     `status ≠ "active"` (F-C1); `isBillable = false` iken `status = "active"` (F-C4); `status` / `code` / `name` yok ya da boş;
     `contractVersion` yok ya da `1.` değil (F-V); gövde ayrıştırılamıyor / `data` yok → 503 `WMS_CUSTOMER_DIRECTORY_UNAVAILABLE`;
   - **erişilemiyor** — 400, 401 / 403 (anahtar atanmamış dahil — F-403), 5xx (CRM 503 dahil — F-503), zaman aşımı, ulaşılamıyor → 503
     `WMS_CUSTOMER_DIRECTORY_UNAVAILABLE`. **r3 (F-2):** r2'nin 401 / 403 → 403 `WMS_CUSTOMER_DIRECTORY_FORBIDDEN` eşlemesi kalktı;
     sözleşme "Anahtar atanmamış kullanıcı 403 alır (§5: 503)" der (`crm-account-lookup.md:266`, §6 `:279`) ve FU01 §5a tüketicisi olmak
     ister (XMC-CRM-01). RECALL 1.10.0'ın `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN` sapması bilerek izlenmez (Recall bir okuma ekranıdır,
     FU01 bir yazım ön kontrolü; kullanıcıya iki yolda da "Bağlantıyı kaldırıp elle kaydet" gösterilir, ayrı kodun ekranda ayrı sonucu yok).
     Anahtarı olmayan kullanıcı seçiciyi görmez (§8.4 çizim kapısı), bu yüzden 403 yalnız API'den ya da anlık görüntü / token farkından gelir.
   **Q-2 ile ilişki:** Q-2 "yalnız `active`" kararı sözleşmenin değişmez kuralıyla (`isBillable = true` ⇔ `status = "active"`,
   `crm-account-lookup.md:116`) `isBillable = true` demektir; kural CRM'dedir, WMS'te durum tablosu tutulmaz (sözleşme §4). Seçici
   (`billableOnly=true`) ve sunucu aynı kuralı uygular; seçimden sonra uygunluğunu yitiren account Q-12'dir (uyarı, ret değil — §8.4). `status` ve `contractVersion` karşılaştırmaları
   büyük / küçük harfe duyarlıdır (sözleşme §5); `accountId` GUID olarak ayrıştırılıp karşılaştırılır.
   Ret yazımsızdır: kayıt, rezervasyon, yerel olay, merkezi denetim yok (0 / 0 / 0). Doğrulama yalnız `customerRef`'i **atayan ya da
   değiştiren** istekte koşar: oluşturmada `customerRef` varsa; güncellemede gönderilen `customerRef` kayıtlı `CustomerRef`'ten farklıysa
   (GUID eşitliği, harf farkı değişiklik değildir). **r3 (F-8):** gönderilen `customerRef` kayıtlı değere eşitse (aynı account'ta adres
   değiştirme, "CRM'den yeniden al") U-1 **atlanır**, `customerAddressId` ve `shipTo` yazılır — seçimden sonra pasifleşen account'ta akış
   sürer (Q-12 ile tutarlı). `customerRef`'siz güncelleme, `removeCustomerRef` ve "sevke hazır" InventoryService'te CRM'i çağırmaz.
   **Günlük (r3, F-9):** adaptör başarısızlıkta yalnız `accountId`, sonuç sınıfı / HTTP durumu ve `X-Correlation-Id` yazar; CRM gövdesinden
   (ad, kod, durum metni) hiçbir şey yazmaz (§2 günlük kuralı). Seçimden sonra pasifleşen account'un
   uyarısı (Q-12 kararı) Web proxy'sinin işidir, sunucu davranışı ve WMS sözleşmesi değişmez (§8.4).
5. **Yazım:** oluşturmada işlem A'da `CustomerRef`, `CustomerAddressId` ve `ShipTo`'nun kod / ad üyeleri (§4.1a; `ShipToAddressInput`'a eklemeli `countryName`,
   `cityRef`, `districtRef`, `district`) (ebeveyn §8.17 adım 5); güncellemede başlık yazımında, `Version` koşullu. Yerel olay
   `Created` / `Updated` `Data`'sına yalnız `customerRefEntered` / `customerRefChanged: true` (değer yok); `customerRefChanged` kimlik ya da
   `customerAddressId` değişince `true` olur; adres üyelerinin değişimi bugünkü `shipToChanged` işaretidir (`SameAs` dört yeni üyeyi de
   karşılaştırır — §4.1a). İstek anahtarı parmak izi `customerRef`, `customerAddressId`, `removeCustomerRef` ve dört yeni adres üyesini taşır (§4.1a).
6. **Okuma:** `OutboundShipmentView.customerRef` / `.customerAddressId` (`wms-bundle.openapi.yaml:1757-1771` şemasına eklemeli) ve donmuş
   `getOutboundShipment` / `listOutboundShipments` `OutboundShipment.customerRef` / `.customerAddressId` — **yalnız doluysa yazılır**, hiç `null` yazılmaz. Arama listesi
   (`OutboundShipmentListItem`) değişmez.
7. **Eski kayıtlar:** geri doldurma, göç, otomatik eşleme yok.

### 8.6 Sipariş referansı

Sahip kararı D-3.

- v1: `orderRef` bugünkü gibi serbest metin (≤ 100, `_Form.cshtml:65-68`); sözleşme değişmez.
- "Alan hazır" anlamı (öneri): DL-1'de alan tek başına bir `.diten-field` içinde, ileride select2 "seç ya da yaz" (etiket girişi) kontrolüne
  dönüşebilecek biçimde durur; görünen metin sipariş numarasıdır. MOD-0168 gelince FU-0178-FU01-ORDER eklemeli `orderId` (MOD-0168 kimliği)
  ekler, `orderRef` görünen numara olarak kalır. v1'de "siparişten seç" düğmesi, ipucu metni ya da boş seçici **çizilmez** (çalışmayan
  kontrol yok).

### 8.7 Sözleşme değişiklikleri ve MVP-6 etkisi

Dilim DL-2 (yayın: CT sözleşme adımı).

| Sözleşme | Bugün | FU01 eki (yalnız ekleme) | Yazan |
|---|---|---|---|
| WMS-BUNDLE | 1.1.0 (`wms-bundle.openapi.yaml:64`); Item adımı 1.2.0 bekliyor | **1.3.0** (Item adımı önce yazılırsa; değilse Q-7): `CreateOutboundShipmentRequest` / `PreviewOutboundAllocationRequest` / `UpdateOutboundShipmentRequest` `customerRef` ve `customerAddressId`; `UpdateOutboundShipmentRequest.removeCustomerRef`; `OutboundShipmentView.customerRef` / `.customerAddressId`; üst küme `OutboundShipment.customerRef` / `.customerAddressId`; `ShipToAddressInput` / `ShipToAddress` / üst küme `Address`'e isteğe bağlı `countryName`, `cityRef`, `districtRef`, `district` (§4.1a); create / update `x-error-codes`'a üç kod (`WMS_CUSTOMER_NOT_FOUND`, `WMS_CUSTOMER_NOT_ACTIVE`, `WMS_CUSTOMER_DIRECTORY_UNAVAILABLE` — r3) | CT tek yazıcı adımı |
| WAREHOUSE-OUTBOUND | 1.0.0 FROZEN; Item adımı 1.1.0 bekliyor | **1.2.0** (aynı koşul): `OutboundShipment.customerRef` (isteğe bağlı; MOD-0149 `Account.Id`; SHIPMENT-BUNDLE 3.5.0 `customerRef` ile **aynı ad ve anlam** — C-5, Q-4), `OutboundShipment.customerAddressId` (isteğe bağlı; CRM ship-to `addressId` — kopyanın kaynağı) ve `Address`'e isteğe bağlı `countryName`, `cityRef`, `districtRef`, `district`. `OutboundShipmentReadyEvent` değişmez | aynı CT adımı (üst küme kuralı: iki dosya birlikte) |
| CRM-ACCOUNT-LOOKUP | 1.0.0 FROZEN | **1.1.0** (CRM cevabı C-1…C-5 ve güncel önerisi): yeni uç `GET /api/crm/accounts/{id}/ship-to`, anahtar `crm.account-ship-to.read`; cevap `addresses[]` (`addressId`, `kind`, `label`, `isDefault`, `countryRef`, `cityRef`, `districtRef`, `postalCode`, `addressLine`) + `defaultAddressId`, ilk sürümde tek eleman (`"primary"`); ülke / şehir / ilçe kod (Reference Data kümeleri `country`, `city`, `district`), kod kümesi sözleşmede; U-1 / U-3 değişmez. Sıra: taslak → inceleme → kod → donma; tahmin ana adresle ~2 hafta, çoklu adres +2–3 hafta (biçim ve sürüm değişmez) | CRM sahibi (Beste — CRM) |

**MVP-6'ya etkisi (XMC-001; Q-6, Natig):**
- MOD-0183 WAREHOUSE-OUTBOUND okuyucusu açık şemayla okur ve üreticinin eklediği her alanı anlık görüntüye **sınıflandırmadan** yazar
  (`MOD-0183-shipment-tracking-pod.md:256-262`) → `customerRef` 1.2.0'dan sonra MVP-6 kaynak anlık görüntülerine girer. Bir kimliktir (ad /
  adres değil), ama MOD-0183'ün "yükümlülük 3" beyaz listesinde sınıflandırılmalıdır.
- MOD-0183 intake'i bugün sevkiyata `customerRef` yazmaz (`MOD-0183-shipment-tracking-pod.md:934`; ebeveyn FU-0178-MVP6). FU01'in alanı bu
  eşlemenin kaynağı olabilir: depodan CRM'le seçilmiş bir outbound, MVP-6 sevkiyatına `customerRef` olarak geçerse MOD-0177-FU01 geri
  çağırma ekranı müşteriyi adıyla gösterir. Eşleme MVP-6'nın kararı ve kodudur; FU01 yalnız alanı sunar.
- **Aynı CRM ucu:** MVP-6 da alıcı adresi için aynı `ship-to` ucunu kullanacaktır (CRM cevabı, 2026-11-13); WMS'te tutulan kimlik
  `Account.Id`'dir ve SHIPMENT-BUNDLE 3.5.0 `customerRef` ile uyumludur (C-5, Q-4) — bu yüzden depo outbound'undan MVP-6 sevkiyatına eşleme dönüşümsüzdür.
- **CT bulgusu (r2, ölçüldü `a7c1d32b8`):** MVP-6 (MOD-0183) Web müşteri seçicisi bugün CRM'in **genel** hesap uçlarını sözleşmesiz okuyor —
  `GET /api/crm/accounts` (arama) ve `GET /api/crm/accounts/{id}` (seçim sonrası ad + adres), anahtar `crm.account.read`
  (`frontend/Diten.Web/Controllers/SupplyChainShipmentsController.cs:206-233`; anahtar sabiti `:23`, yol sabiti `:24`; kod yorumu "Contractless
  measured use"). **FU01 bunu yapmaz:** arama yalnız CRM-ACCOUNT-LOOKUP U-3 (`crm.account.lookup-validation`), adres yalnız 1.1 `ship-to`
  ucu (`crm.account-ship-to.read`); `crm.account.read` ve genel hesap uçları FU01'in hiçbir dosyasında kullanılmaz. **Not (MVP-6'ya, XMC-001):**
  MVP-6'nın da 1.1 donunca bu uca geçmesi önerilir — iki depo ekranı aynı sözleşmeyi, aynı anahtarı ve aynı adres listesini kullanır,
  sevkiyat rollerine `crm.account.read` verilmesi gerekmez. Karar ve kod MVP-6'nındır; soru Q-6'ya eklendi. FU01 MVP-6 dosyalarına dokunmaz.
- Adres kod üyeleri (`cityRef`, `districtRef`), adlar (`countryName`, `district`) ve `customerAddressId` de MVP-6 anlık görüntüsüne girer;
  ad / adres kişisel veri sınıfındadır (`shipTo` gibi) — Q-6'nın sınıflandırma maddesi bunları da kapsar.
- Kırılma yok (çıkarım, şemadan): alanlar isteğe bağlıdır, `null` yazılmaz, elle girilmiş outbound'ların cevabı byte olarak değişmez.

## 9. Layout & Shell Contract

- `shell: tenant` → `Views/Warehouse/OutboundShipments/{Index,Create,Edit,Details}.cshtml` Razor bloğunda **`Layout = "_LayoutTenantShell";`
  AÇIKÇA** yazılır (bugün `Create.cshtml` böyle — ebeveyn §9; partial'lar hariç). `_LineOffcanvas.cshtml` partial'dır, Layout taşımaz.
- **Golden Reference kararı (alan sayımı):**

| Ekran | Kullanıcı alanları | Sayı | Karar |
|---|---|---|---|
| Outbound Create / Edit (başlık) | depo, Staging, sipariş ref., CRM müşterisi (DL-3), teslimat adresi (DL-3, r2), alıcı adı, ülke, şehir, ilçe (DL-3), posta kodu, adres 1, adres 2, not | 13 (DL-1 / DL-2'de CRM müşterisi, teslimat adresi ve ilçe yok → 10) | > 8 → **compact** (değişmez; künye `form_field_count: 13`) |
| Satır paneli (offcanvas) | ürün, SKU, miktar, sipariş satırı no, lot, FEFO gerekçesi | 6 | ≤ 8 → Golden **Slim** `_CreateEditOffcanvas.cshtml` deseni, compact sayfa içinde |

  Sayılmayanlar: Id, şirket seçici (sayfa üstü `_CompanySelector`), salt okunur birim ve önizleme alanları.
- UAS-001: `warehouse.outbound-shipments.create` (Create) / `.update` (Edit) yoksa yalnız erişim açıklaması (bugünkü `_AccessDenied`).
  Sayfa içinde (r3, F-5): CRM müşteri seçici yalnız `crm.account.lookup-validation`, teslimat adresi seçici ve "CRM'den yeniden al" yalnız
  ek olarak `crm.account-ship-to.read` varsa çizilir; yoksa hiç çizilmez (devre dışı da değil), alıcı kartı elle giriş alanlarıdır (§8.4).
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
CRM hatasında istisna atmaz, sonucu `Valid | NotActive | NotFound | Unavailable` olarak döner (r3: `Forbidden` yok — CRM 401 / 403
`Unavailable`'dır, F-2; sınıflandırma §8.5 madde 4).

## 11. Frontend File Contract

```text
frontend/Diten.Web/Views/Warehouse/OutboundShipments/          (Compact seti — değişmez; ebeveyn §11.5)
├── Index.cshtml · Create.cshtml · Edit.cshtml · Details.cshtml     (Layout = "_LayoutTenantShell" AÇIKÇA)
├── _Form.cshtml                    (DL-1: satır tablosu kartı; DL-3: CRM seçici — izin koşullu çizim)
├── _LineOffcanvas.cshtml           (YENİ, DL-1 — Golden Slim _CreateEditOffcanvas deseni; satır paneli)
├── _ActionDialogs.cshtml · _Filter.cshtml · _DataTable.cshtml · _IndexL10n.cshtml
└── OutboundShipmentsIndex.cs
frontend/Diten.Web/wwwroot/assets/js/Warehouse/OutboundShipments/{form.js, rules.js, details.js (DL-1 gerekçe sütunu; DL-3 rozet, ülke adı, ilçe, Q-12 bildirimi), index.l10n.js}
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
`AllocFefoReason` (DL-1, r3: Details tahsis tablosu "FEFO gerekçesi" sütunu) ve satır altı etiketi "FEFO gerekçesi:",
`Customer.*` (DL-3: seçici, teslimat adresi seçicisi, "Bu CRM kaydında teslimat adresi yok — adresi elle girin", ipuçları, CRM hata metinleri — r3: "CRM araması yapılamadı — alıcıyı elle girin", "CRM adresi okunamadı — adresi elle girin"; "yetkiniz yok" metni yok —, "Bağlantıyı kaldır", "CRM'den yeniden al", "Bağlantıyı kaldırıp elle kaydet",
rozet, Q-12 bildirimi "Bu CRM müşterisi artık aktif değil ({0}). Sevkiyat kaydedildi ve sürüyor.", eski ülke eki "(kayıtlı değer)"), `FieldDistrict` (DL-3, Details ve form),
`Warehouse.Error.WMS_CUSTOMER_*` (DL-2'nin üç kodu). Kiracı modülü → **7 dil**; bir dil eksikse dilim bitmemiştir.

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
| `customerRef` | isteğe bağlı GUID; atanırken / değişirken CRM U-1 ile uygun (`isBillable = true`, tutarlı gövde — §8.5 madde 4, Q-2); değişmeyen değer yeniden doğrulanmaz (r3, F-8) | sunucu (DL-2) |
| `customerAddressId` | isteğe bağlı, 1–100 karakter; yalnız `customerRef` ile (yoksa 400); CRM'e karşı doğrulanmaz | sunucu (DL-2); form yalnız CRM listesinden seçilen öğenin kimliğini gönderir (DL-3) |
| `removeCustomerRef` | yalnız güncellemede; `customerRef` ile birlikte → 400; `customerAddressId`'yi de kaldırır | sunucu |
| Teslimat adresi (DL-3) | CRM listesi doluysa bir öğe seçili (varsayılan önceden); liste boşsa alan gizli, adres elle | form |
| Alıcı adı, ülke | zorunlu (donmuş `Address`) — CRM'den gelse de, düzenlense de (Q-3); ülke kod (DL-3'ten itibaren Reference Data seçicisi); Edit'te kod olmayan eski ülke değeri korunur, koda geçmek zorunlu değildir (r3, F-11, §8.4) | form + sunucu |
| Şehir, ilçe (DL-3), posta kodu, adres 1-2 | isteğe bağlı, ≤ 200; posta kodu CRM öğesinde doluysa ön doldurulur, boşsa elle eklenebilir (Q-10) | form |
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
| CRM yetkisi yok (`crm.account.lookup-validation` yok) | r3 (UAS-001): CRM seçici **çizilmez**, proxy çağrılmaz, elle alanlar açık (AC-28); API'den gelen `customerRef`'li kayıt CRM'den 403 alır → 503 `WMS_CUSTOMER_DIRECTORY_UNAVAILABLE` (sözleşme §5, F-403; AC-16); elle kayıt 201 |
| CRM kapalı | seçici "CRM araması yapılamadı — elle girin"; `customerRef`'li kayıt 503; elle kayıt 201 (AC-18) |
| Başka kiracının hesap kimliği | CRM 404 → 422 `WMS_CUSTOMER_NOT_FOUND` (AC-22); ship-to ucu da 404 → seçicide "bulunamadı" |
| Ship-to anahtarı yok (`crm.account-ship-to.read`) ama arama anahtarı var | r3 (UAS-001): seçici çizilir, teslimat adresi seçici ve "CRM'den yeniden al" çizilmez; seçimde `ship-to` çağrılmaz, ad U-3 satırından dolar, adres elle; kayıt `customerRef`'le, `customerAddressId`'siz gider (U-1 ayrı anahtar) (AC-28) |
| U-1 cevabı çelişkili ya da eksik (`isBillable = true` + `suspended`, `isBillable = false` + `active`, başka `accountId`, `contractVersion` 2.0, `isBillable` yok) | 503 `WMS_CUSTOMER_DIRECTORY_UNAVAILABLE`, yazım 0 / 0 / 0 (sözleşme §5; F-C1, F-C4, F-C2, F-V, F-C3; AC-16) |
| Kümede olmayan durum kodu (`closed`, `isBillable = false`) | 422 `WMS_CUSTOMER_NOT_ACTIVE` (F-UNK; AC-17) |
| Reference Data kodu çözemez | kod + "Ad okunamadı"; kullanıcı adı elle yazabilir |
| CRM ship-to listesi boş (`addresses[]` boş) | bağlantı kurulur, ad dolar, adres alanları boş + "teslimat adresi yok — elle girin"; `customerAddressId` gönderilmez; kayıt engellenmez (AC-26) |
| CRM'den gelen adres düzenlendi (Q-3) | kayıt düzenlenmiş kopyayı yazar; `customerRef` ve `customerAddressId` korunur (AC-14, AC-26) |
| Pasif account (Q-2) | seçicide görünmez (`billableOnly=true`); API'den gelirse 422 `WMS_CUSTOMER_NOT_ACTIVE` (AC-17) |
| Seçimden sonra pasifleşen account (Q-12) | Edit kaydı ve "sevke hazır" başarılı; ekranda uyarı, akış durmaz; ship-to okunamazsa uyarısız (AC-29). r3 (F-6): Edit'te uyarı yönlendirmeden önce onaylanan premium bildirimdir, yönlendirmeyle kaybolmaz |
| Pasifleşen account'ta aynı account içinde adres değiştirme / "CRM'den yeniden al" (r3, F-8) | `customerRef` değişmediği için U-1 çağrılmaz → 200; adres ve `customerAddressId` yazılır; Web uyarısı gösterilir (AC-20, AC-29) |
| Eşzamanlılık (iki kişi aynı outbound'u düzenler) | eski `If-Match` → 409 `WMS_CONFLICT`; ekranda "Kayıt değişti, yenileyin" |
| Önizlemeden sonra stok değişti | kayıt 422 `WMS_ALLOCATION_FAILED`, satır rozetleri güncellenir; outbound yok |
| Seçilen lot geri çağrıldı (önizleme ile kayıt arasında) | önizleme / kayıt 422 `LOT_UNDER_RECALL`, ilgili satır işaretli |
| `ReadyToShip` sonrası müşteri değiştirme | 409 `WMS_OUTBOUND_IMMUTABLE`; r3 (F-7): CRM çağrılmaz (yerel kontroller önce; bayat `If-Match` de CRM'siz 409 `WMS_CONFLICT`) (AC-20) |

## 14. Authorization Convention

- **Yeni izin anahtarı yok.** Ekran ve uçlar ebeveynin anahtarlarını kullanır: Create / önizleme / müşteri araması (create sayfasında)
  `warehouse.outbound-shipments.create`; Edit / müşteri araması (edit sayfasında) `warehouse.outbound-shipments.update`; okuma `.read`.
  Web proxy eylemi tek anahtar alıyorsa `customers` / `customer-ship-to` her sayfa için ayrı eylemdir (ölçülecek — DL-3 geliştiricinin ilk
  ölçümü); iki anahtardan birini kabul eden ortak eylem yazılmaz.
- **CRM tarafı:** CRM uçları kendi anahtarlarını çağıranın token'ında arar: U-1 / U-3 `crm.account.lookup-validation` (K-1); ship-to ucu
  **`crm.account-ship-to.read`** (C-4; `crm.account.read` değil — FU01 `crm.account.read`'i hiçbir yerde istemez, §8.7 CT bulgusu; anahtarı
  CRM manifestine CRM ekler). CRM'den alıcı seçimi için sevkiyat rollerinde **iki anahtar birden** gerekir (CRM güncel önerisi). İki anahtarın
  `ShippingPlanner` ve `WarehouseSupervisor` rol şablonlarına eşlenmesi Auth işidir (MOD-0018, Ali; Q-5). Q-12 durum okuması da
  `crm.account-ship-to.read` ister; anahtar yoksa Web `ship-to`'yu **çağırmaz** ve uyarı yazılmaz (r3), Edit / "sevke hazır" ebeveynin
  anahtarlarıyla aynen çalışır. **Çizim kapısı (r3, UAS-001, F-5):** CRM kontrolleri izin anlık görüntüsüne göre çizilir (§8.4 tablosu);
  proxy eylemleri aynı anahtarı sunucuda yeniden ister (emsal `SupplyChainShipmentsController.cs:212`). Eşleme yoksa sistem çalışır:
  seçici çizilmez, elle giriş açıktır; API'den gelen `customerRef`'li kayıt CRM'den 403 alır ve 503 `WMS_CUSTOMER_DIRECTORY_UNAVAILABLE`
  olur (sözleşme §5 / §6, F-2).
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

- [ ] AC-1 (zorunlu) — Create sayfasında satırlar `#outbound-lines-table` tablosundadır (`data-dt-standard="v2"`, `data-dt-data-mode="client"`); bugünkü satır kartı girişleri (`form.js:75-91` `lineRow`, FIX-01'li taban) sayfada yoktur; sütunlar §8.2'deki sırada; GUID görünmez. Sabotaj **S-FU01-1:** tablo başlatıcısında `data-dt-data-mode`'u `server` yap → vitest kırmızı.
- [ ] AC-2 (zorunlu) — Satır paneli 6 alanı taşır; her alan `.diten-field` + ikon (`diten-field-icons.test.js` ICON_MAP); birincil düğme `data-dt-primary="enter"`; Enter "Satırı kaydet"e basar. Sabotaj **S-FU01-2:** panelin `data-dt-primary` işaretini kaldır → `diten-enter-primary.test.js` kırmızı.
- [ ] AC-3 (zorunlu) — Panelde ürün değişince SKU ve birim temizlenir ve select2 yeniden eşitlenir; select2 açılır listesi panelin içindedir (`dropdownParent`). Sabotaj **S-FU01-3:** ürün değişim işleyicisinden SKU temizleme satırını kaldır → test kırmızı.
- [ ] AC-4 (zorunlu) — 100 satırda "Satır ekle" pasiftir ve açıklama görünür; 101. satır hiçbir yoldan (panel, klavye) eklenmez. Sabotaj **S-FU01-4:** `rules.js`'teki sınırı kaldır → test kırmızı.
- [ ] AC-5 (zorunlu, D-4) — Panelde lot seçilip "Satırı önizle"ye basılınca istek gövdesi o satırı `lotOverride.lotId` = seçilen lotla taşır; cevabın tahsisi seçilen lottan gösterilir, FEFO önerisi (ilk serbest lot) ayrıca gösterilir; sapma varsa kip ve önerilen lot no görünür. Sabotaj **S-FU01-5:** önizleme gövdesi kurucusunda `lotOverride`'ı boşalt (FIX-01 öncesi `lotId: ''` davranışı; FIX-01 CT sabotajı b'nin panel karşılığı) → vitest kırmızı. Canlı kanıt: E3 verisindeki iki lotla (AMX-2604 FEFO 1, G1-20261020 FEFO 2 — kaynak WP-0178-E3-FIX-01 madde 4, `WP-0178-E3-FIX-01-ui-l10n.md:30`, ve FIX-01 CT canlı turu, kabul kaydı "Önizleme" adımı) panelde seçilen G1-20261020 tek satır önizlemesinde ve tabloda görünür.
- [ ] AC-6 (zorunlu, D-4) — "Tahsisi önizle" bütün satırları, her birinin `lotOverride`'ıyla tek istekte gönderir; her satırın lot / alınabilir / durum sütunu cevaptan dolar; seçilen lotu olan satırda lot sütunu seçilen lotu gösterir. Sabotaj **S-FU01-6:** cevap eşlemesinde satır eşini sıra yerine hep ilk satıra bağla → test kırmızı.
- [ ] AC-7 (zorunlu) — FEFO kipi istemcide: `Block` → lot değişimli satır kaydedilemez + açıklama; `RequireReason` → < 10 karakter gerekçeyle kaydedilemez; `Warn` → gerekçesiz kaydedilir; sunucunun 422 `FEFO_BYPASS_REASON_REQUIRED` / `FEFO_ORDER_VIOLATION` cevabı `details.lineSeq` ile doğru satıra bağlanır. Sabotaj **S-FU01-7:** `rules.lotNeed`'in `RequireReason` dalını `Optional` döndür → test kırmızı.
- [ ] AC-8 (zorunlu) — Depo / Staging / satır değişince ilgili önizleme `Önizleme eski` olur; eski önizlemeye dayanan lot değişimli satır kaydedilmez ("yeniden önizleyin"); yeniden önizlemede listede olmayan seçili lot sessizce sıfırlanmaz, satır `Karşılanamaz` + açıklama alır. Sabotaj **S-FU01-8:** depo değişim işleyicisinden eskitme çağrısını kaldır → test kırmızı.
- [ ] AC-9 (zorunlu) — Kaydet gövdesi `CreateOutboundShipmentRequest` (WMS-BUNDLE 1.1.0, `additionalProperties: false`) şemasına uyar: satır sırası tablo sırası, `lotOverride` yalnız lot seçilmiş satırda, `null` üye yok. Test: gövde dondurulmuş şemaya karşı doğrulanır. Sabotaj **S-FU01-9:** gövdeye `lotOverride: null` yaz → şema testi kırmızı.
- [ ] AC-10 (zorunlu) — `422 WMS_ALLOCATION_FAILED` `details.lines[] { lineSeq, code, requested, allocatable }` her satırın rozetine ve satır altı metnine eşlenir; outbound oluşmaz, sayfa yönlenmez. Sabotaj **S-FU01-10:** `lineSeq` eşlemesini kaldır → test kırmızı.
- [ ] AC-11 (zorunlu) — Edit sayfasında satır tablosu salt okunurdur: araç çubuğu ve satır menüsü yoktur, sütunlar kayıttan (lot, tahsis, rezervasyon durumu) dolar; başlık düzenleme bugünkü gibi çalışır. **FEFO gerekçesi vakası (r3, CT gözlemi):** `fefoBypassReason`'lı bir tahsiste Details tahsis tablosunda gerekçe `AllocFefoReason` sütununun hücresindedir, "Rezervasyon" hücresi yalnız durum metnini ("Tutuluyor") taşır; gerekçesiz tahsiste sütun "-"; Edit tablosunda gerekçe lot hücresinin satır altı metnidir. Sabotaj **S-FU01-11:** edit modunda satır menüsünü aç → test kırmızı; **S-FU01-11b:** `details.js` `renderLines`'ta gerekçeyi yine rezervasyon hücresine yaz (bugünkü `:63-64`) → gerekçe vakası kırmızı.
- [ ] AC-12 (zorunlu) — Tablo ve panelde miktarlar FIX-01'in biçimleyicisiyle gösterilir (tr: servis "2.000" → "2", "1.500" → "1,5"); FU01 ikinci bir biçimleyici tanımlamaz. Sabotaj **S-FU01-12:** tablo hücresinde biçimleyici yerine ham değeri yaz → test kırmızı.
- [ ] AC-13 (zorunlu) — UAS-001 (Create'te `.create` yoksa yalnız açıklama), `resx-check` kiracı 7 dil temiz, 375 px'te tablo + panel yatay kaydırmasız (canlı ölçüm, `scrollWidth`), sayfa yardımı güncel (§25). Sabotaj **S-FU01-13:** `ar` dosyasından bir `LinePanel.*` anahtarını sil → resx bekçisi kırmızı.

**DL-2 — `customerRef` (servis + sözleşme)**

- [ ] AC-14 (zorunlu) — `customerRef`'li oluşturma: CRM U-1 200 + `active` → 201; `CustomerRef` kanonik küçük harf yazılır; görünüm `customerRef` döner; `shipTo` isteğin kopyasıdır (CRM'den doldurulmaz). **Q-3 vakası (r2):** istek `shipTo.name` / `line1`'i CRM U-1 cevabındaki addan ve CRM adresinden farklı (kullanıcının düzenlediği) değerlerle gelir → kayıt ve görünüm düzenlenmiş değerleri byte olarak taşır, `CustomerRef` ve `CustomerAddressId` korunur. **`customerAddressId` vakası (r2):** `customerRef` + `customerAddressId = "primary"` → ikisi yazılır ve okunur. Sabotaj **S-FU01-14:** handler'da `CustomerRef` atamasını kaldır → test kırmızı; **S-FU01-14b:** handler'da `ShipTo.Name`'i U-1 cevabının `name`'iyle üzerine yaz → Q-3 vakası kırmızı; **S-FU01-14c:** handler'da `CustomerAddressId` atamasını kaldır → `customerAddressId` vakası kırmızı.
- [ ] AC-15 (zorunlu) — CRM 404 → 422 `WMS_CUSTOMER_NOT_FOUND`; kayıt, rezervasyon, yerel olay ve merkezi denetim 0 / 0 / 0 (FU03 rezervasyon portu çağrılmaz). Sabotaj **S-FU01-15:** 404 dalını geçerli say → test kırmızı.
- [ ] AC-16 (zorunlu; r3 — F-1, F-2) — CRM-ACCOUNT-LOOKUP §5 "geçersiz / çelişkili" ve "erişilemiyor" sınıfları → 503 `WMS_CUSTOMER_DIRECTORY_UNAVAILABLE`, yazım 0 / 0 / 0. Sözleşme fikstürleri (§9, `crm-account-lookup.md:311-332`) adaptörün **üretim** sınıflandırıcısına karşı koşar: F-C1 (`isBillable = true` + `status = suspended`), F-C4 (`isBillable = false` + `status = active`), F-C2 (`accountId` farklı), F-C3 (`isBillable` yok), F-V (`contractVersion = crm-account-lookup/2.0`), F-503 (CRM 503), F-403 (anahtar atanmamış kullanıcı, 403) ve 400 / 401 / 500 / zaman aşımı / ayrıştırılamayan gövde → hepsi 503; hata kataloğunda `WMS_CUSTOMER_DIRECTORY_FORBIDDEN` yoktur. Sabotaj **S-FU01-16:** adaptörün `Unavailable` sonucunu `Valid`'e eşle → test kırmızı; **S-FU01-16b:** sınıflandırıcıdan `isBillable` ↔ `status` tutarlılık denetimini kaldır → F-C1 ve F-C4 kırmızı; **S-FU01-16c:** `data.accountId`'nin istenen kimlikle eşitlik denetimini kaldır → F-C2 kırmızı; **S-FU01-16d:** `contractVersion` önek denetimini kaldır → F-V kırmızı; **S-FU01-16e:** CRM 401 / 403'ü `Unavailable` dışında bir sonuca (r2'nin `Forbidden`'ı / 403) eşle → F-403 kırmızı.
- [ ] AC-17 (zorunlu, Q-2 kararı: yalnız `active`; r3 — F-1: karar `isBillable` ile) — 200, tutarlı, `isBillable = false` + `status ≠ active` → 422 `WMS_CUSTOMER_NOT_ACTIVE`, yazım 0 / 0 / 0: F-NE-x (`inactive`, `draft`, `suspended`, `archived` — dört fikstür) ve **F-UNK** (`status = closed`, kümede yok, `isBillable = false`); F-OK (`active`, `true`) → 201 (seçicinin `billableOnly=true` araması DL-3'te, AC-25). Sabotaj **S-FU01-17:** uygunluk kontrolünü kaldır → test kırmızı; **S-FU01-17b:** kararı `isBillable` yerine bilinen dört pasif kodun listesine bağla (`status ∈ {inactive, draft, suspended, archived}` → NotActive, öbürü → Valid) → F-UNK kırmızı.
- [ ] AC-18 (zorunlu, D-2 "elle giriş her zaman") — `customerRef`'siz oluşturma ve güncelleme CRM'i **hiç çağırmaz**: CRM sahtesi her çağrıda istisna atarken 201 / 200. Sabotaj **S-FU01-18:** doğrulamayı `customerRef` koşulundan çıkar (her zaman çağır) → test kırmızı.
- [ ] AC-19 (zorunlu) — İstek anahtarı CRM'den önce aranır: aynı anahtar + aynı gövde → 200 replay ve CRM çağrı sayısı 0; aynı anahtar + farklı `customerRef` → 400 `IDEMPOTENCY_KEY_REUSED`; **r3 (F-9):** aynı anahtar + yalnız `customerAddressId`'si ya da yalnız `shipTo.district`'i farklı gövde (oluşturma ve güncelleme) → 400 `IDEMPOTENCY_KEY_REUSED`. Sabotaj **S-FU01-19:** CRM doğrulamasını anahtar aramasının önüne al → replay CRM'i çağırır, test kırmızı; **S-FU01-19b:** parmak izinden (`CreateOutboundShipmentHandler.cs:200-203` / `UpdateOutboundShipmentHandler.cs:48-50` eşdeğeri) `customerAddressId`'yi ve dört yeni adres üyesini çıkar → r3 vakası kırmızı (replay sayılır).
- [ ] AC-20 (zorunlu) — Güncelleme: Allocated / Picked / Packed'te `customerRef` atanır / değişir / `removeCustomerRef` ile kalkar; `ReadyToShip` ve sonrası 409 `WMS_OUTBOUND_IMMUTABLE`; ikisi birlikte 400; yerel olay `Data`'sı yalnız `customerRefChanged: true` taşır, GUID ne `Data`'da ne merkezi denetimde. **r2 vakaları:** `removeCustomerRef` `CustomerAddressId`'yi de kaldırır; `customerAddressId`'siz `customerRef` ataması kayıtlı `CustomerAddressId`'yi kaldırır; `customerAddressId` `customerRef`'siz (oluşturma ve güncelleme) → 400 `INVALID_REQUEST`, yazım yok. **r3 vakaları:** (F-7) `ReadyToShip` outbound'a `customerRef` değişikliği → 409 `WMS_OUTBOUND_IMMUTABLE` ve CRM çağrı sayısı 0; bayat `If-Match` + `customerRef` → 409 `WMS_CONFLICT` ve CRM çağrı sayısı 0; (F-8) kayıtlı `customerRef` aynen (harf farkıyla da) + yeni `customerAddressId` + yeni `shipTo`, CRM sahtesi `suspended` dönerken → 200, CRM çağrı sayısı 0, adres ve `customerAddressId` yazılır, `customerRefChanged: true` (değer yok); (F-9) yalnız `shipTo.district` (ya da yalnız `districtRef`) değişen güncelleme → `shipToChanged: "true"`, `Data`'da adres değeri yok. Sabotaj **S-FU01-20:** olay `Data`'sına GUID değerini yaz → test kırmızı; **S-FU01-20b:** `customerAddressId`'nin `customerRef`'siz gelme kontrolünü kaldır → 400 vakası kırmızı; **S-FU01-20c:** güncellemede CRM doğrulamasını durum / `If-Match` kontrollerinin önüne al → F-7 vakası kırmızı (CRM çağrı sayısı 1); **S-FU01-20d:** `OutboundAddress.SameAs`'tan `District` karşılaştırmasını çıkar → F-9 vakası kırmızı; **S-FU01-20e:** "kayıtlı `customerRef`'e eşit" kısa devresini kaldır (her `customerRef`'te U-1) → F-8 vakası kırmızı (422).
- [ ] AC-21 (zorunlu) — Sözleşme: CT adımıyla WMS-BUNDLE ve WAREHOUSE-OUTBOUND yeni sürümleri birlikte yazılır (§8.7); `ShipTo`'nun kod / ad üyeleri (`countryName`, `cityRef`, `districtRef`, `district`) ve `customerAddressId` yazılır, okunur ve uzunluk sınırları (§12) 400 verir; donmuş GET'ler `customerRef`'i, `customerAddressId`'yi ve bu üyeleri yalnız doluysa yazar, hiç `null` yazmaz; elle girilmiş outbound'un donmuş GET cevabı önceki sürümle byte-eşittir (fikstür). Sabotaj **S-FU01-21:** boş `customerRef`'i `null` olarak yaz → sözleşme testi (kural 4) kırmızı.
- [ ] AC-22 (zorunlu) — Kiracı izolasyonu ve token: adaptör CRM'i çağıranın `Authorization` başlığıyla çağırır (servis kimliği yok); başka kiracının hesap kimliği CRM'den 404 alır → 422. **Günlük vakası (r3, F-9):** yakalayan günlükçüyle adaptör 200 (F-OK, F-NE-x), 404, 503 ve geçersiz gövde yollarında koşar → hiçbir günlük satırı CRM cevabındaki `name`, `code` ya da gövde metnini taşımaz; başarısızlık satırı yalnız `accountId`, sonuç sınıfı / durum ve `X-Correlation-Id` taşır; giden istekte `X-Correlation-Id` vardır. Sabotaj **S-FU01-22:** adaptörde çağıranın token'ı yerine boş / sabit başlık gönder → başlık testi kırmızı; **S-FU01-22b:** adaptörün geçersiz gövde günlüğüne CRM cevabının `name`'ini ekle → günlük vakası kırmızı.
- [ ] AC-23 (zorunlu) — Hata kataloğu: üç kod (`WMS_CUSTOMER_NOT_FOUND`, `WMS_CUSTOMER_NOT_ACTIVE`, `WMS_CUSTOMER_DIRECTORY_UNAVAILABLE`; r3 — `WMS_CUSTOMER_DIRECTORY_FORBIDDEN` yok) `WarehouseErrorCodes`'ta ve WMS-BUNDLE `x-error-codes`'ta (create, update); katalog testi ikisini karşılaştırır. Sabotaj **S-FU01-23:** bir kodu yalnız koddan sil → katalog testi kırmızı.
- [ ] AC-24 (zorunlu) — InventoryService test paketinin **tamamı** yeşil (filtre yok — CT kuralı); mimari testler (`AuthorizationGuardTests`, `CentralAuditDeclarationTests`) değişmeden yeşil. Guard değil, kanıt maddesidir: sabotaj yerine negatif test kanıtı = DL-2'nin bütün sabotajlarının (`S-FU01-14`, `S-FU01-14b`, `S-FU01-14c`, `S-FU01-15`, `S-FU01-16`, `S-FU01-16b`, `S-FU01-16c`, `S-FU01-16d`, `S-FU01-16e`, `S-FU01-17`, `S-FU01-17b`, `S-FU01-18`, `S-FU01-19`, `S-FU01-19b`, `S-FU01-20`, `S-FU01-20b`, `S-FU01-20c`, `S-FU01-20d`, `S-FU01-20e`, `S-FU01-21`, `S-FU01-22`, `S-FU01-22b`, `S-FU01-23`) aynı koşuda kırmızı olup geri alınınca bu maddenin yeşile dönmesi.

**DL-3 — CRM müşteri seçici (Web)**

- [ ] AC-25 (zorunlu) — Seçici yalnız aynı-origin proxy'yi çağırır (`/Warehouse/OutboundShipments/customers`); tarayıcıdan 5000 / 5061'e istek yok; proxy Gateway U-3'ü kullanıcı token'ıyla ve `billableOnly=true` ile çağırır (Q-2); proxy CRM'in genel hesap uçlarını (`/api/crm/accounts`, `/api/crm/accounts/{id}`) çağırmaz ve `crm.account.read` istemez (§8.7 CT bulgusu); en az 2 karakter. **Günlük vakası (r3, F-9):** yakalayan günlükçüyle `customers` ve `customer-ship-to` proxy'leri 200 / 404 / 503 yollarında koşar → hiçbir günlük satırı CRM'den gelen ad, kod, adres alanı, posta kodu ya da `label` taşımaz (yalnız kimlik, durum, `X-Correlation-Id`). Sabotaj **S-FU01-25:** seçici URL'sini Gateway adresine çevir → vitest kırmızı; **S-FU01-25b:** proxy'nin U-3 sorgusundan `billableOnly=true`'yu kaldır → proxy testi kırmızı; **S-FU01-25c:** `customer-ship-to` proxy'sine ilk adres öğesinin `addressLine`'ını yazan bir günlük satırı ekle → günlük vakası kırmızı.
- [ ] AC-26 (zorunlu) — Seçim ad + adresi CRM 1.1 `GET /api/crm/accounts/{id}/ship-to`'dan (proxy üzerinden) doldurur: ülke / şehir / ilçe kodu Reference Data'dan kullanıcının dilinde ada çözülür, kayıt gövdesi kod + adı taşır (`country` küçük harf kod, `countryName`, `city` + `cityRef`, `district` + `districtRef`, `line1`); posta kodu öğede doluysa ön doldurulur, boşsa boş kalır ve elle yazılan değer gövdede `postalCode` olarak gider (Q-10); alanlar düzenlenebilir (Q-3) ve elle değiştirilen şehir / ilçe adının kodu gönderilmez; ipucu hesap kodunu gösterir; GUID görünmez. **Adres listesi vakası (r2):** iki öğeli fikstürde (`primary` + `isDefault` olan ikinci öğe) `defaultAddressId` öğesi önceden seçilidir ve formu o doldurur, gövdede `customerAddressId` = seçilen öğenin `addressId`'si; tek öğeli fikstürde (`"primary"`, `postalCode` boş) o öğe seçili. **Boş liste vakası (r2):** `addresses: []` → bağlantı kurulur (gövdede `customerRef`), ad dolar, adres alanları boş ve açık, uyarı görünür, gövdede `customerAddressId` yok, kayıt düğmesi pasif değildir. **Q-3 vakası (r2):** CRM'den doldurulan alıcı adı ve Adres 1 düzenlenip kaydedilince gövde düzenlenmiş değerleri taşır, `customerRef` ve `customerAddressId` gövdede kalır (alan düzenlemek bağlantıyı kaldırmaz). Sabotaj **S-FU01-26a:** ön doldurmada `cityRef` eşlemesini kaldır → test kırmızı; **S-FU01-26b:** şehir adı elle değişince kodu temizleyen satırı kaldır → ad / kod çelişkisi testi kırmızı; **S-FU01-26d:** alıcı alanlarının değişim işleyicisine bağlantıyı (`customerRef` / `customerAddressId`) temizleyen satır ekle → Q-3 vakası kırmızı; **S-FU01-26e:** varsayılan seçimi `defaultAddressId` yerine hep ilk öğeye bağla → adres listesi vakası kırmızı; **S-FU01-26f:** boş listede bağlantıyı kurma (`customerRef`'i temizle) → boş liste vakası kırmızı. **Eski serbest metin ülke vakası (r3, F-11):** `shipTo.country = "Türkiye"` (kod değil) olan outbound'un Edit'inde ülke seçicisi bu değeri önceden seçili gösterir; yalnız not değişince gövdede `shipTo` yoktur; yalnız şehir değişince gövdede `country = "Türkiye"` aynen gider. **S-FU01-26g:** Edit yüklemesinde Reference Data'da olmayan ülke değerini seçicide boşalt → eski ülke vakası kırmızı.
- [ ] AC-32 (zorunlu) — Ship-to cevapları 1.0 kurallarıyla eşlenir (C-1, C-6): 404 → "bulunamadı", bağlantı yok; 400 / 401 / 403 / 5xx / 503 / zaman aşımı → "CRM adresi okunamadı — adresi elle girin", ad dolar, adres boş, bağlantı korunur (r3, F-2: 401 / 403 ayrı "yetkiniz yok" metni almaz); Reference Data çözemezse kod + "Ad okunamadı". Sabotaj **S-FU01-26c:** 404'te bağlantıyı kur → test kırmızı.
- [ ] AC-27 (zorunlu) — "Bağlantıyı kaldır" sonrası gövdede `customerRef` ve `customerAddressId` yoktur, alanlar korunur. Sabotaj **S-FU01-27:** kaldırma işleyicisinde `customerRef`'i temizleme → test kırmızı.
- [ ] AC-28 (zorunlu, D-2; r3 — UAS-001, F-5) — Seçicide CRM 400 / 401 / 403 / 5xx → "CRM araması yapılamadı — alıcıyı elle girin" + elle alanlar açık; elle kayıt 201. **Çizim kapısı vakaları (r3):** (a) `crm.account.lookup-validation` olmayan kullanıcıda Create ve Edit sayfasının HTML'inde CRM müşteri seçici, teslimat adresi seçici ve "CRM'den yeniden al" **yoktur** (devre dışı da değil), `customers` / `customer-ship-to` proxy'lerine istek gitmez, elle alanlar açık, elle kayıt 201; (b) yalnız `crm.account.lookup-validation` olan kullanıcıda seçici vardır, teslimat adresi seçici ve "CRM'den yeniden al" yoktur, seçimde `customer-ship-to` çağrılmaz, gövdede `customerRef` var `customerAddressId` yok; (c) iki anahtarlı kullanıcıda hepsi vardır; (d) anahtarsız doğrudan çağrıda `customers` ve `customer-ship-to` proxy'leri 403 döner ve CRM'i çağırmaz. Sabotaj **S-FU01-28:** seçici hatasında alıcı alanlarını pasifleştir → test kırmızı; **S-FU01-28b:** `_Form.cshtml`'deki `crm.account.lookup-validation` koşulunu kaldır (seçiciyi her zaman çiz) → görünüm testi (a) kırmızı; **S-FU01-28c:** `customer-ship-to` proxy eyleminden `crm.account-ship-to.read` kontrolünü kaldır → proxy testi (d) kırmızı.
- [ ] AC-29 (zorunlu) — Kayıtta 503 (r3: CRM 401 / 403 de 503'tür) → uyarı + "Bağlantıyı kaldırıp elle kaydet"; eylem `customerRef`'siz ve **yeni** istek anahtarıyla gönderir. **Q-12 vakası (r2, sahip kararı `ad1100158`):** `customerRef`'li bir outbound için Web proxy `edit/{id}` ve `release/{id}`, InventoryService sahtesi başarılı dönerken ve CRM `ship-to` sahtesi account durumunu `suspended` (ayrıca `inactive`, `archived`) dönerken → tarayıcıya 2xx + `customerWarning` gider, InventoryService tam bir kez çağrılır, ekran uyarıyı gösterir ve işlemi tamamlanmış sayar (yönlendirme / durum değişimi aynen); `active` → uyarı yok; ship-to 403 / 404 / 503 / zaman aşımı → 2xx, uyarı yok; InventoryService hatası → ship-to hiç çağrılmaz. **Gösterim vakası (r3, F-6):** Edit'te `customerWarning`'li başarılı cevapta `window.showConfirm` sahtesi tam bir kez açılır ve `window.location.href` bildirim **kapanmadan önce** değişmez; onay da kapatma da (`onCancel`) aynı Details adresine yönlendirir; `customerWarning`'siz cevapta bildirim açılmaz, yönlendirme bugünkü gibi hemen olur; Details'te "sevke hazır" başarısında bildirim `load()`'dan sonra açılır. **Anahtar vakası (r3):** `crm.account-ship-to.read` olmayan kullanıcıda `edit/{id}` / `release/{id}` CRM'i çağırmaz (çağrı sayısı 0), cevap `customerWarning`'sizdir. Sabotaj **S-FU01-29:** eylemde eski anahtarı kullan → test (400 beklentisi) kırmızı; **S-FU01-29b:** proxy'de pasif durumda cevabı 409 / 422'ye çevir (akışı durdur) → Q-12 vakası kırmızı; **S-FU01-29c:** `release/{id}` eyleminden durum okumasını kaldır → "sevke hazır" uyarı vakası kırmızı; **S-FU01-29d:** `form.js` Edit başarı dalında `customerWarning` varken bugünkü toast + hemen yönlendirmeyi geri koy (`form.js:271-272`) → gösterim vakası kırmızı; **S-FU01-29e:** Q-12 okumasından `crm.account-ship-to.read` kontrolünü kaldır → anahtar vakası kırmızı.
- [ ] AC-30 (zorunlu) — Details özet kartı `customerRef` varsa "CRM müşterisi" rozetini gösterir; ayrıntıda CRM çağrısı yok, GUID yok. **r3 (F-11):** `countryName = "Türkiye"`, `country = "tr"`, `district = "Kadıköy"` olan outbound'da "Ülke" satırı "Türkiye (tr)", "İlçe" satırı "Kadıköy" gösterir; `countryName`'siz eski outbound'da "Ülke" `country`'yi olduğu gibi gösterir, "İlçe" "-"; `cityRef` / `districtRef` sayfada görünmez. Sabotaj **S-FU01-30:** rozet yerine `customerRef` değerini yaz → test kırmızı; **S-FU01-30b:** "Ülke" satırında `countryName` yerine hep `country`'yi yaz → F-11 vakası kırmızı.
- [ ] AC-31 (zorunlu) — Canlı E3: Gateway üzerinden gerçek oturumla (a) CRM müşterisi seçilerek, (b) elle girilerek, (c) CRM kapalıyken elle girilerek birer outbound oluşur; 375 px ve tr / en / ar ekran görüntüleri; sahip turu (Enter, "?" metni, "tur yok"). Guard değil, canlı kanıt maddesidir: negatif test = (c) koşusunda CRM kapalıyken `customerRef`'li kaydın 503 aldığının ve elle kaydın 201 aldığının aynı turda görülmesi.

## 17. Test Expectations

- **Web (vitest):** AC-1…AC-12, AC-25…AC-30, AC-32 — `frontend/Diten.Web/tests/` altında, gerçek `form.js` / `rules.js` dosyalarına karşı (jsdom + fetch
  sahtesi); şema testi (AC-9) dondurulmuş `wms-bundle.openapi.yaml`'ı okur. DL-3 ship-to fikstürleri (r2): tek öğe (`"primary"`, posta kodu
  boş), iki öğe (varsayılan ikinci, posta kodu dolu), boş liste; proxy testleri (AC-25, AC-26) Web denetleyicisine karşı sahte HTTP işleyicisiyle. Mevcut bekçiler: `diten-enter-primary.test.js`,
  `diten-field-icons.test.js`, l10n / resx bekçileri, FIX-01'in `warehouse-s2-e3-fix-01.test.js`'i (FU01 DOM'una güncellenir, silinmez). BL-586: offcanvas `data-dt-primary` işareti zorunlu.
  r3: `details.js` gerçek dosyasına karşı gerekçe sütunu (AC-11), ülke adı / ilçe (AC-30) ve Q-12 bildirimi (AC-29, `window.showConfirm`
  ve `window.location` sahteleriyle) testleri.
- **Web (xUnit, `frontend/Diten.Web.Tests`, r3):** çizim kapısı (AC-28 a–d) — Create / Edit görünümü üç izin anlık görüntüsüyle çizilir
  (emsal `WarehouseS2ScreensTests`); proxy eylemleri (`customers`, `customer-ship-to`, Q-12 okuması) anahtar yokken 403 / çağrısız; yakalayan
  günlükçüyle günlük vakası (AC-25).
- **Servis (xUnit, gerçek replica set):** AC-14…AC-24 — CRM adaptörü sahte HTTP işleyicisiyle; r3: CRM-ACCOUNT-LOOKUP §9 fikstürleri
  adlarıyla (F-OK, F-NE-x ×4, F-UNK, F-404, F-C1, F-C2, F-C3, F-C4, F-V, F-503, F-403) + 400 / 401 / 500 / zaman aşımı / ayrıştırılamayan
  gövde, üretim adaptörünün sınıflandırıcısına karşı (sözleşme §9: "fikstürler tüketicinin testidir"); CRM çağrı sayacı (AC-18, AC-19,
  AC-20 F-7 / F-8 vakaları); yakalayan günlükçü (AC-22). Servise dokunulduğu için **bütün** InventoryService paketi koşar.
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
- [x] Sahip soruları Q-2, Q-3, Q-4, Q-9, Q-10 (`a7c1d32b8`) ve Q-12 (`ad1100158`) karara bağlandı (Beste, 2026-11-13; §19.5, §19.6)
- [x] r2 bağımsız incelemesinin bulguları F-1…F-11 ve CT turu gözlemi r3'te işlendi (§19.7)
- [x] WP-0178-E3-FIX-01 uygulandı ve CT VERIFIED (`49fd410b7`) — DL-1'in FIX-01 kapısı kapandı
- [ ] Açık sorular: Q-7 ve Q-8 (Ali / CT) — paket onayından önce cevap ya da "öneri kabul"; Q-11 (Reference Data, CT ölçümü) ve Q-13 (CRM, ship-to durumu) DL-3 kapısıdır; CRM 1.1 donması (C-5), XMC-CRM-01 (§5a tüketici satırı, Beste — CRM), Q-5 (Auth, Ali), Q-6 (MVP-6, Natig) dilim kapısıdır, onayı engellemez (§24)
- [ ] r3'ün bağımsız incelemesi (CT-APR-02) ve onay satırı (CT-APR-01, onaylayan Ali)
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
| C-2 | Ülke / şehir / ilçe **kod** döner (`CountryRef`, `CityRef`, `DistrictRef`); görünen ad WMS'te Reference Data'dan çözülür; anlık kopyaya kod + ad yazılır (ad dile bağlı); kod kümesi sözleşmede yazılacak. Posta kodu account'ta yok; gerekirse ayrı CRM alan işi — v1'de CRM'den seçilen alıcıda posta kodu boş gelir, kullanıcı elle ekleyebilir (posta kodu kısmı C-11 ile güncellendi — CRM ana adrese alan ekliyor) | §4.1a, §8.4, Q-10, Q-11 |
| C-3 | ~~v1 tek adres (account adresi); çoklu teslimat adresi ileride 1.x eklemesi~~ — **CRM güncel önerisiyle değişti** (aşağıda C-8) | §8.4 |
| C-4 | Ayrı okuma anahtarı `crm.account-ship-to.read` (`crm.account.read` değil); kullanıcının token'ıyla, Gateway üzerinden; anahtarı CRM manifestine CRM ekler, WMS rollerine atama Auth ile | §14, Q-5 |
| C-5 | Tarih TBD (Beste). Sıra: 1.1 sözleşme taslağı → inceleme → kod → donma; alıcı seçim dilimi bu donmaya kapılı; elle giriş her zaman çalışır. MVP-6 da aynı ucu kullanacak; `customerRef` = `Account.Id` — WMS'te tutulan kimlik de `Account.Id` | §8.7, §24 DL-3, Q-4 |

**CRM sahibinin güncel önerisi C-6…C-11 — kaynak (r3, F-4):** karar dosyası
`docs/records/decisions/2026-11/mod0178-fu01-outbound-form-v2-owner-decisions-01.md`, ek "2026-11-13: CRM güncel önerisi (C-6…C-11)"
(commit `b8becef7d`; CRM oturumunun cevabı, Beste'nin sohbete yapıştırdığı metin). Taslak dosyası
(`docs/analysis/contracts/drafts/crm-account-lookup-v1.1-ship-to-proposal.md`, dal `docs/crm/crm-account-lookup-v1.1-ship-to`) bu depoda
yoktur; PR bağlantısı ayrıca gelecek (karar eki). **Numaralar karar ekiyle aynıdır;** r2'nin kendi numaraları (liste C-6, posta kodu C-8,
CRM'in sorusu C-11) r3'te karar ekine hizalandı — eşleme son sütunda.

| # (karar eki) | Öneri (karar ekinin özeti) | Pakette | r2'deki no |
|---|---|---|---|
| C-6 | Uç: CRM-ACCOUNT-LOOKUP 1.1 yalnız ekleme, `GET /api/crm/accounts/{id}/ship-to`; kiracı sınırı, 404 (yok / silinmiş / başka kiracı aynı cevap), `tenant_id`'siz token 400, 5xx 503 — 1.0 kurallarıyla aynı; adres U-1 / U-3'e eklenmez (C-1'in teyidi) | §7, §8.4, §15, AC-32 | — (r2'de C-1 altında) |
| C-7 | Kodlar: ülke / şehir / ilçe kod döner (Reference Data kümeleri `country`, `city`, `district`); görünen ad kullanıcının dilinde Reference Data'dan; kopyaya kod + ad | §4.1a, §8.4, Q-11 | C-7 |
| C-8 | Adres listesi: `addresses[]` (`addressId`, `kind`, `label`, `isDefault`, `countryRef`, `cityRef`, `districtRef`, `postalCode`, `addressLine`) + `defaultAddressId`; ilk sürümde tek eleman (`addressId = "primary"`); ek teslimat adresleri sonra, biçim ve sürüm değişmez; WMS adres seçtirir (varsayılan önceden seçili), sevkiyatta `customerRef` + `addressId` + kopya tutar; liste boşsa "adres yok" kuralı WMS'in | §4.1, §8.4, §8.5, §8.7, §12, §13; AC-14, AC-20, AC-21, AC-26, AC-27 | C-6 |
| C-9 | İzin: yeni okuma anahtarı `crm.account-ship-to.read` (`crm.account.read` değil); müşteri araması U-3 (`crm.account.lookup-validation`); sevkiyat rollerine iki anahtar birden; çağrı sunucudan, Gateway üzerinden, kullanıcının token'ıyla; anahtar CRM manifestine CRM'ce, WMS rollerine atama Auth ile | §7, §8.4 çizim kapısı, §14, Q-5; AC-28 | C-9 |
| C-10 | Tarih (tahmin): uç (yalnız ana adres) ~2 hafta; çoklu teslimat adresi +2–3 hafta | künye, §7, §8.7, §24 | C-10 |
| C-11 | Posta kodu: hem ek teslimat adreslerinde hem account'un ana adresinde olacak; ana adrese alan eklenene kadar boş gelir, biçim değişmez | §4.1a, §8.4, Q-10, AC-26 | C-8 |
| CRM'in sorusu 5 | Aktif olmayan account — WMS kararı: yeni sevkiyatta yalnız `active` (Q-2); sonradan pasifleşende uyarı, akış sürer (Q-12, `ad1100158`) | §8.4, §8.5, Q-12, Q-13 | C-11 |
| Açık (CT → CRM) | `ship-to` cevabına eklemeli `status` (Q-13) ve MOD-0178'in CRM-ACCOUNT-LOOKUP §5a tüketici tablosuna eklenmesi (r2 incelemesi F-3 → XMC-CRM-01) | §7, §19.5 Q-13, §24 | — |

### 19.2 Ölçülen gerçekler (`8913e60f0`; Web satırları r3'te `b8becef7d`'de, FIX-01'li tabanda yeniden ölçüldü — salt okuma)

- Bugünkü form satırları kart olarak çizer (`form.js:72-91`, `lineRow` `:75-91`). FIX-01'den önce önizlemeye `lotId: ''` gidiyordu
  (`8913e60f0`'da `form.js:198`); FIX-01'den sonra önizleme ve kayıt aynı `rules.lineBody` gövdesini gönderir (`form.js:143`, `:223-226`),
  lot değişimi önizlemeyi yeniden koşar (`:104-105`), seçim yeniden çizimde korunur (`:201-202`).
- Details tahsis tablosu FEFO sapma gerekçesini "Rezervasyon" hücresinde, durum metninin altında çizer (`details.js:63-64`); Edit başarıda
  toast + hemen yönlendirme (`form.js:271-272`); Edit değişiklik karşılaştırması altı adres üyesi (`form.js:267`).
- Güncelleme işleyicisinin sırası: biçim → parmak izi + replay → görünürlük (404) → durum 409 → `Version` 409 (`UpdateOutboundShipmentHandler.cs:36-65`);
  `shipToChanged` `SameAs` ile (`:89`); `OutboundAddress` KVKK yorumu ve `SameAs` (`OutboundShipmentEntities.cs:11-12`, `:29-31`).
- Recall CRM okuyucusu: okuma başına 2 sn, sayfa bütçesi 3 sn (`CrmAccountLookupHttpReader.cs:27-28`); başarısızlıkta `accountId` + neden
  günlüğe yazılır (`:20`, `:129-133`). MVP-6: CRM'den hiçbir şey günlüğe yazılmaz (SB-21, `MOD-0183-shipment-tracking-pod.md:1009`); seçici
  `crm.account.read` yoksa devre dışı + gerekçe (`Views/SupplyChain/Shipments/_Form.cshtml:12-13`), proxy aynı kapıyla 403
  (`SupplyChainShipmentsController.cs:212`, `:227`; incelemenin ":214"ü bir satır kaymış — kapı `:212`).
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
- **Sapma değil (r3, F-2):** CRM 401 / 403 eşlemesi CRM-ACCOUNT-LOOKUP §5 / §6'ya hizalandı (tüketicide 503); RECALL 1.10.0'ın
  `RECALL_CUSTOMER_DIRECTORY_FORBIDDEN` emsali bilerek izlenmedi (§8.5 madde 4). Sözleşmeden ayrılan bir eşleme yoktur.

### 19.5 Sorular — kararlar ve açık kalanlar

Q-2, Q-3, Q-4, Q-9, Q-10 sahip kararıyla kapandı (Beste, 2026-11-13, "hepsi öneri gibi"; karar dosyası
`docs/records/decisions/2026-11/mod0178-fu01-outbound-form-v2-owner-decisions-01.md`, ek "2026-11-13: sahip cevapları", commit `a7c1d32b8`).
Q-12 sahip kararıyla kapandı (Beste, 2026-11-13, "evet"; karar dosyası eki, commit `ad1100158`).
Açık kalanlar: Q-5, Q-7, Q-8 (Ali), Q-6 (MVP-6, Natig — XMC-001), Q-11 (CT ölçümü), Q-13 (yeni — CRM, ship-to cevabında durum).

| # | Soru | Sahip | Öneri / karar | Kapı |
|---|---|---|---|---|
| Q-1 | ~~CRM-ACCOUNT-LOOKUP 1.1.0 adres yüzeyi (K-9)~~ — **cevaplandı** (CRM sahibi Beste, 2026-11-13, C-1…C-5; güncel öneri C-6…C-11, karar eki `b8becef7d`): ayrı uç `GET /api/crm/accounts/{id}/ship-to`, anahtar `crm.account-ship-to.read`, kod döner, cevap adres listesi. Kalan: 1.1 taslağı / inceleme / kod / donma, tahmin ana adresle ~2 hafta | Beste (CRM) | — | DL-3 başlama (donma) |
| Q-2 | ~~Hangi CRM durumları seçilebilir?~~ | Beste (sahip) | **Karar (2026-11-13): yalnız `active`** — seçici `billableOnly=true`; sunucu 422 `WMS_CUSTOMER_NOT_ACTIVE` (§8.4, §8.5; AC-17, AC-25). Seçimden sonra pasifleşen account ayrı soru: Q-12 | kapandı |
| Q-3 | ~~CRM'den gelen adres bu sevkiyat için düzenlenebilir mi?~~ | Beste (sahip) | **Karar (2026-11-13): evet** — kopya düzenlenmiş değeri tutar, account kimliği (`customerRef`) ve seçilen adresin kimliği (`customerAddressId`) kalır (§4.1, §8.4, §8.5; AC-14 + S-FU01-14b, AC-26 + S-FU01-26d) | kapandı |
| Q-4 | ~~Alan adı `customerRef` mı `accountRef` mı?~~ | Beste (sahip) | **Karar (2026-11-13): `customerRef`**, değer MOD-0149 `Account.Id` — SHIPMENT-BUNDLE 3.5.0 ve RECALL 1.10.0 ile aynı ad ve anlam; MVP-6 eşlemesi birebir (§4.1, §8.7) | kapandı |
| Q-5 | `crm.account.lookup-validation` (U-1 / U-3) ve `crm.account-ship-to.read` (C-4) `ShippingPlanner` / `WarehouseSupervisor` rol şablonlarına eklenir mi (var olan kiracılarda da, ebeveyn D-3 koşullarıyla)? CRM'den seçim için iki anahtar birden gerekir (C-9); r3: eşleme yokken seçici çizilmez (§8.4) | **Ali** (Auth, MOD-0018) | evet; ebeveyn AC-76 / MOD-0018-FU31 ile aynı WP'ye ek satır; `crm.account-ship-to.read` CRM manifestine girdikten sonra | DL-2 / DL-3 kabul |
| Q-6 | WAREHOUSE-OUTBOUND eklemeli `customerRef` (ve r2'de `customerAddressId`, adres kod / ad üyeleri) MVP-6 kabul eder mi; yükümlülük 3 beyaz listesinde nasıl sınıflanır; intake sevkiyata `customerRef` olarak eşler mi, ne zaman (FU-0178-MVP6)? **r2 notu (CT bulgusu, `a7c1d32b8`):** MVP-6 Web müşteri seçicisi bugün CRM'in genel hesap uçlarını (`GET /api/crm/accounts`, `GET /api/crm/accounts/{id}`) `crm.account.read` ile sözleşmesiz okuyor (`frontend/Diten.Web/Controllers/SupplyChainShipmentsController.cs:206-233`); FU01 bunu yapmaz. MVP-6 da CRM-ACCOUNT-LOOKUP 1.1 donunca U-3 + `ship-to` ucuna (`crm.account-ship-to.read`) geçer mi, ne zaman? | **Natig (MVP-6 CT)** — XMC-001 | kabul; kimlik olarak sınıflanır; eşleme MOD-0183'ün sonraki revizyonunda; seçici 1.1 donunca U-3 + `ship-to`'ya geçer (iki depo ekranı aynı sözleşme ve anahtar; sevkiyat rollerine `crm.account.read` gerekmez) | DL-2 yayın (CT sözleşme adımı) |
| Q-7 | Sürüm sırası: FU01 eki Item adımından (WAREHOUSE-OUTBOUND 1.1.0 + WMS-BUNDLE 1.2.0) sonra 1.2.0 / 1.3.0 olarak mı yazılır; Item adımı gecikirse FU01 eki önce mi gider, aynı adımda mı birleşir? | **Ali / CT** | Item adımını bekle; MOD-0183 r10 onayı DL-2 bitişinden sonra kalırsa CT iki eki tek adımda yazar (iki dosya, tek sürüm artışı) | DL-2 yayın |
| Q-8 | Golden sapması G-FU01-01 kabul mü? | Ali (onay) | kabul (gerekçe §11) | paket onayı |
| Q-9 | ~~DL-2 CRM adaptörü Recall'ın `CrmAccountLookupHttpReader`'ı ile ortak kod paylaşacak mı?~~ | Beste (MOD-0177 sahibi) — XMC-001 | **Karar (2026-11-13): hayır** — Recall okuyucusuyla kod paylaşılmaz, Warehouse kendi okuyucusunu yazar, Recall dosyaları değişmez; ortaklaştırma ileride ayrı iş (FU-0178-FU01-CRMCLIENT) (§5, §6) | kapandı |
| Q-10 | ~~Posta kodu: CRM'den gelmiyorsa yeterli mi, CRM'de alan işi açılsın mı?~~ | Beste (sahip) | **Karar (2026-11-13), CRM güncellemesiyle (C-11):** CRM'den gelen `postalCode` doluysa kopyaya yazılır, boşsa kullanıcı formda elle ekleyebilir; posta kodsuz kayıt geçerli. CRM ana adrese posta kodu alanı ekliyor (tarih CRM'de); ek teslimat adreslerinde dolu gelir; sözleşme biçimi değişmez. Bilgi: MOD-0182 SSCC etiketinde alıcıdan şehir + ilçe yazılır, posta kodu varsa o da (MOD-0182'nin işi; FU01 yalnız kopyayı sunar) (§4.1a, §8.4, §12; AC-26) | kapandı |
| Q-11 | **Reference Data kaynağı:** ülke / şehir / ilçe kodlarının (kümeler `country`, `city`, `district` — C-7) görünen adı hangi uçtan, hangi dillerde okunur? Web'in bugün kullandığı bir ülke / şehir arama ucu bu pakette ölçülmedi | CT (ölçüm) + Reference Data sahibi | DL-3 geliştiricisinin ilk ölçümü; 7 dilde ad yoksa en'e düşülür ve kopyaya o ad yazılır (öneri) | DL-3 başlama |
| Q-12 | ~~CRM'in sorusu (CRM'in sorusu 5): seçimden sonra pasifleşen account'lu mevcut sevkiyat ne olur?~~ | Beste (sahip) | **Karar (2026-11-13, "evet"; karar dosyası eki, commit `ad1100158`):** yeni sevkiyatta yalnız `active` seçilir (Q-2); seçimden sonra pasif / askıda / arşiv olan account'ta mevcut sevkiyat sürer; WMS kayıt ve "sevke hazır" adımında durumu `ship-to` ucundan yeniden okur, kullanıcıyı uyarır, akışı durdurmaz. Uygulama DL-3 Web proxy'sinde (`edit/{id}`, `release/{id}` — başarılı cevaptan sonra; WMS sözleşmesi değişmez); ship-to okunamazsa uyarısız (öneri) (§8.4, §13, §14, §21; AC-29 + S-FU01-29b, S-FU01-29c) | kapandı |
| Q-13 | **Yeni (r2):** Q-12 kararı durumu `ship-to` ucundan okur, ama koordinatörün ilettiği 1.1 cevap alanları (`addresses[]` + `defaultAddressId`) account durumunu saymıyor. 1.1 `ship-to` cevabı account `status`'unu (U-1 ile aynı `account-status` kümesi) taşıyacak mı? | **Beste (CRM)** | evet — cevaba üst düzey `status` (eklemeli, taslak aşamasında); taşımazsa Web proxy durumu U-1'den okur (aynı kullanıcı token'ı, `crm.account.lookup-validation`) ve davranış aynı kalır. r3 notu (F-1, soru değişmedi): uyarı kararı sözleşme §4 gibi `isBillable` iledir; `ship-to` yalnız `status` taşırsa §2.2'nin değişmez kuralıyla (`status = "active"` ⇔ `isBillable`) okunur | DL-3 başlama |

### 19.6 r1 → r2 kapanış tablosu

> r3 notu (F-4): bu tablodaki C-6…C-11 r2'nin kendi numaralarıdır (liste C-6, posta kodu C-8, CRM'in sorusu C-11); karar eki numaralarıyla
> eşlemesi §19.1'dedir. Tablo tarihî kayıt olarak değiştirilmedi.

| Girdi | Kaynak | Ne yapıldı | Pakette |
|---|---|---|---|
| Q-2 = yalnız `active` | sahip, `a7c1d32b8` | koşullu metin ("Q-2 = A ise") kesinleşti; seçici `billableOnly=true`; AC-17 zorunlu, fikstürler adlandı; AC-25'e `billableOnly` vakası + S-FU01-25b | §3, §8.4, §8.5, §12, §13; AC-17, AC-25 |
| Q-3 = adres düzenlenebilir | sahip, `a7c1d32b8` | kopya düzenlenmiş değeri tutar, `customerRef` / `customerAddressId` kalır; sunucu ve Web vakası + sabotaj | §4.1, §4.1a, §8.4, §8.5, §13; AC-14 + S-FU01-14b, AC-26 + S-FU01-26d |
| Q-4 = `customerRef` = `Account.Id` | sahip, `a7c1d32b8` | alan adı kesinleşti | §1, §4.1, §8.7 |
| Q-9 = kod paylaşımı yok | sahip, `a7c1d32b8` | Repo Scope'tan koşullu XMC-001 cümlesi kalktı; DL-2 kapısından düştü | §5, §24 |
| Q-10 = posta kodu doluysa kopyaya, boşsa elle | sahip, `a7c1d32b8` + CRM güncellemesi C-8 | "account'ta yok / sahip kararı bekliyor" metni düzeldi; CRM ana adrese alan ekliyor (tarih CRM'de); AC-26'ya posta kodu vakası; FU-0178-FU01-POSTAL kapandı | §4.1a, §8.4, §12, §20; AC-26 |
| CRM güncel önerisi — adres listesi (C-6, C-7, C-9, C-10) | Beste — CRM, koordinatör özeti (taslak bu depoda okunamadı) | outbound'a `customerAddressId` (DL-2, sözleşme eki), teslimat adresi seçimi (varsayılan önceden seçili), boş liste kuralı (bağlantı kurulur, adres elle), iki anahtar birden, tahmin; FU-0178-FU01-MULTIADDR kapandı; `form_field_count` 12 → 13 (compact değişmez) | künye, §1, §3, §4.1, §7, §8.4, §8.5, §8.7, §9, §12, §13, §14, §20; AC-14 + S-FU01-14c, AC-20 + S-FU01-20b, AC-21, AC-26 + S-FU01-26e / S-FU01-26f, AC-27 |
| CRM'in sorusu (C-11) → Q-12 kararı | Beste — CRM (soru); sahip "evet", `ad1100158` | seçimden sonra pasifleşen account'ta sevkiyat sürer; Edit kaydı ve "sevke hazır"da Web proxy `ship-to`'dan durumu okur, uyarır, durdurmaz; AC-29'a vaka + S-FU01-29b, S-FU01-29c; ship-to'da durum alanı için Q-13 (CRM) | §3, §5, §6, §8.4, §8.5, §13, §14, §21; AC-29, Q-12, Q-13 |
| CT bulgusu — MVP-6 genel CRM uçları | CT, `SupplyChainShipmentsController.cs:206-233` (`a7c1d32b8`) | FU01'in bunu yapmadığı yazıldı ve AC-25'te ölçülür; MVP-6'nın 1.1 ucuna geçmesi önerisi §8.7'ye ve Q-6'ya eklendi | §5, §7, §8.7, §14, Q-6; AC-25 |

AC numaraları değişmedi (AC-1…AC-32); yeni vakalar mevcut AC'lere, yeni sabotajlar S-FU01-14b, S-FU01-14c, S-FU01-20b, S-FU01-25b,
S-FU01-26d, S-FU01-26e, S-FU01-26f, S-FU01-29b, S-FU01-29c. Açık kalanlar: Q-5, Q-7, Q-8 (Ali), Q-6 (Natig), Q-11 (CT), Q-13 (Beste — CRM).


### 19.7 r2 → r3 kapanış tablosu

Kaynak: r2 bağımsız incelemesi `docs/records/reviews/MOD-0178-FU01/2026-11-13-3ce263bb.md` (hedef r2 LF sha256 `3ce263bb…`, KOŞULLU;
CT-APR-02), karar dosyası eki "CRM güncel önerisi (C-6…C-11)" (`b8becef7d`) ve FIX-01 CT kabul kaydı
`docs/records/audits/2026-11/mod-0178-e3-fix01-ct-acceptance-2026-11-13.md` ("Düşük gözlemler").

| Bulgu | Önem | Ne yapıldı | Pakette | Yeni sabotaj |
|---|---|---|---|---|
| F-1 — U-1 kararı `status` ile; tutarlılık kuralları ve fikstürler eksik | orta | karar `isBillable` ile (sözleşme §4 `:223`); §5 sınıfları WMS kodlarına birebir: çelişki (F-C1, F-C4), `accountId` uyuşmazlığı (F-C2), `isBillable` yok (F-C3), `contractVersion` (F-V), boş `code` / `name` → 503; kümede olmayan kod (F-UNK) → 422; Q-2 ile ilişki yazıldı (Q-2 "yalnız `active`" = `isBillable = true`, değişmez kural `:116`); Q-13 notu | §1, §3, §7, §8.4, §8.5 madde 4, §12, §13, §17, Q-13; AC-16, AC-17 | S-FU01-16b, S-FU01-16c, S-FU01-16d, S-FU01-17b |
| F-2 — CRM 401 / 403 → 403 sözleşmeden ayrılıyor | düşük | **hizalandı** (en dar ve tutarlı yol: sözleşmenin kuralı, F-403 fikstürü, §5a tüketicisi olma isteği): 401 / 403 → 503 `WMS_CUSTOMER_DIRECTORY_UNAVAILABLE`; `WMS_CUSTOMER_DIRECTORY_FORBIDDEN` ve port sonucu `Forbidden` kalktı (kod sayısı üç); seçici ve ship-to metinleri "yapılamadı / okunamadı"; RECALL emsali bilerek izlenmedi (§19.4) | künye, §3, §5, §8.4, §8.5, §8.7, §10, §11, §13, §14, §19.4; AC-16, AC-23, AC-28, AC-29, AC-32 | S-FU01-16e |
| F-3 — §5a tüketici tablosunda MOD-0178 yok | orta | XMC-001 isteği XMC-CRM-01 (Beste — CRM): §5a'ya MOD-0178 satırı (U-1, U-3, 1.1 `ship-to`); DL-2 kabul ve DL-3 başlama kapısı | künye, §2, §7, §18, §24 | — (kapı; belge kanıtı) |
| F-4 — C-6…C-11 kaynaksız | orta | karar dosyası ekine (`b8becef7d`) bağlandı; paketin numaraları ekle hizalandı (r2 C-6 → C-8, C-8 → C-11, C-11 → "CRM'in sorusu 5"; yeni C-6 = uç); eşleme §19.1'de | künye, §7, §8.4, §19.1, §19.5 | — |
| F-5 — UAS-001: seçici yetkisize çiziliyor | orta | izin anlık görüntüsüyle çizim kapısı: seçici yalnız `crm.account.lookup-validation`, adres seçici / "CRM'den yeniden al" yalnız ek `crm.account-ship-to.read` ile; yoksa hiç çizilmez, elle giriş açık; proxy'ler sunucuda aynı kapı (emsal `Views/SupplyChain/Shipments/_Form.cshtml:12-13`, `SupplyChainShipmentsController.cs:212`) | §1, §3, §5, §8.4, §9, §13, §14, §17; AC-28 | S-FU01-28b, S-FU01-28c |
| F-6 — Q-12 uyarısı Edit'te kayboluyor | orta | en dar yol: yönlendirmeden **önce** onaylanan premium bildirim (`window.showConfirm`; onay ve kapatma aynı yönlendirme); URL / oturum durumu yok; Details "sevke hazır"da `load()`'dan sonra aynı bildirim | §8.4, §11, §13; AC-29 | S-FU01-29d |
| F-7 — güncellemede CRM sırası belirsiz | düşük | sıra: biçim → anahtar / replay → görünürlük → durum 409 → `If-Match` 409 → CRM → yazım; yerel retlerde CRM çağrısı 0 | §8.5 madde 3, §13; AC-20 | S-FU01-20c |
| F-8 — aynı account'ta adres değişimi U-1'i yeniden koşuyor | düşük | `customerRef` kayıtlı değere eşitse U-1 atlanır (pasifleşen account'ta akış sürer, Q-12 ile tutarlı) | §4.1, §8.4, §8.5 madde 4, §12, §13; AC-20 | S-FU01-20e |
| F-9 — günlük kuralı, KVKK yorumu, `SameAs` | düşük | günlük kuralı (MVP-6 SB-21 emsali; yalnız kimlik + sonuç + `X-Correlation-Id`); dört yeni adres üyesi KVKK yorumunda, `SameAs`'ta, parmak izinde ve Web karşılaştırmasında | §2, §4.1a, §5, §8.5 madde 4–5; AC-19, AC-20, AC-22, AC-25 | S-FU01-19b, S-FU01-20d, S-FU01-22b, S-FU01-25c |
| F-10 — atıf tazeliği | düşük | FIX-01 "kod yok" → uygulandı / CT VERIFIED (`49fd410b7`); Web atıfları FIX-01'li tabanda yeniden ölçüldü (`form.js`, `details.js`, `_Form.cshtml` +1 satır); G1-20261020 kaynağı FIX-01 madde 4 + CT canlı turu; Recall okuma sınırı 2 sn (`CrmAccountLookupHttpReader.cs:27`); MVP-6 kapı satırı `:212` | künye, ölçüm tabanı, §1, §6, §7, §8.1, §8.2, §8.3, §8.4, §8.5, §8.6, §19.2; AC-1, AC-5 | — |
| F-11 — Details ilçe / ülke adı; eski serbest metin ülke | düşük | Details "Ülke" = "ad (kod)" ya da kayıtlı metin, yeni "İlçe" satırı; Edit'te kod olmayan eski ülke değeri önceden seçili tek ek seçenek, zorunlu dönüşüm yok | §3, §8.4, §11, §12; AC-26, AC-30 | S-FU01-26g, S-FU01-30b |
| CT gözlemi — Details'te FEFO gerekçesi "Rezervasyon" sütununda | — | Details tahsis tablosunda ayrı "FEFO gerekçesi" sütunu; Create / Edit satır tablosunda lot hücresinin satır altı metni (DL-1) | §1, §3, §5, §6, §8.2, §11; AC-11 | S-FU01-11b |

AC numaraları değişmedi (AC-1…AC-32); yeni vakalar mevcut AC'lere. r3'ün yeni sabotajları (18): S-FU01-11b, S-FU01-16b, S-FU01-16c,
S-FU01-16d, S-FU01-16e, S-FU01-17b, S-FU01-19b, S-FU01-20c, S-FU01-20d, S-FU01-20e, S-FU01-22b, S-FU01-25c, S-FU01-26g, S-FU01-28b,
S-FU01-28c, S-FU01-29d, S-FU01-29e, S-FU01-30b — §16 ↔ §24 iki yönde aynı. Açık kalanlar değişmedi: Q-5, Q-7, Q-8 (Ali), Q-6 (MVP-6,
Natig — XMC-001), Q-11 (CT), Q-13 (Beste — CRM); yeni kapı XMC-CRM-01 (Beste — CRM). Sahibe yeni soru açılmadı.

## 20. Follow-up Items

- **FU-0178-FU01-ORDER:** MOD-0168 Order Capture gelince "siparişten seç, yoksa elle gir" — eklemeli `orderId`, `orderRef` görünen numara kalır;
  satırların siparişten doldurulması ayrı karar (D-3).
- **FU-0178-FU01-REG:** registry'ye `MOD-0178-FU01` satırı (CT).
- **FU-0178-FU01-FILTER:** outbound listesinde müşteriye göre süzgeç (`CustomerRef` indeksi) — talep olursa.
- ~~**FU-0178-FU01-MULTIADDR:** CRM 1.x çoklu teslimat adresi gelince seçicide adres seçimi ve outbound'a adres kimliği~~ — **kapandı (r2):**
  CRM ship-to cevabı ilk günden liste (C-8 — karar eki numarası); adres seçimi ve `customerAddressId` DL-2 / DL-3'te. Ek adresler geldiğinde WMS'te iş yok.
- ~~**FU-0178-FU01-POSTAL:** ön doldurmaya posta kodu~~ — **kapandı (r2):** `postalCode` adres öğesinde var (C-8, C-11 — karar eki numaraları); doluysa DL-3 ön
  doldurur (Q-10). Ana adres alanını CRM ekliyor (CRM'in işi, tarih CRM'de).
- **FU-0178-FU01-CRMCLIENT:** InventoryService'te iki CRM U-1 okuyucusunun (Recall, Warehouse) ortak istemciye taşınması — Q-9 kararıyla
  FU01'de yapılmaz; ileride ayrı iş (XMC-001).
- **FU-0178-MVP6 (ebeveyn) notu:** intake `customerRef` eşlemesi FU01 DL-2'den sonra mümkündür; MVP-6 seçicisinin 1.1 `ship-to` ucuna
  geçişi önerisi (Q-6).

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
| Web proxy `edit/{id}`, `release/{id}` — Q-12 durum okuması | — (yazma komutu değil; yazma `UpdateOutboundShipmentCommand` / ebeveynin release komutudur, denetimi ebeveyn §21'deki gibi) | istisna İ-okuma (CRM'e salt okuma, başarılı yazmadan sonra) | uyarı yerel olaya / merkezi kayda yazılmaz |

## 22. Record Codes

Yeni kayıt türü ve kayıt kodu yok. Outbound numarası ebeveynin `{ŞİRKET}-OUT-{000001}` kuralıyla kalır (ebeveyn §22).

## 23. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | çekirdek: satır tablosu + yan panel + önizlemede seçilen lot + Edit salt okunur tablo + Details'te FEFO gerekçesi ayrı sütun (yalnız Web; servis ve sözleşme değişmez) | Sıra ve FIX-01 ile çakışmama, Satır tablosu, Satır yan paneli ve önizleme, Sipariş referansı, Layout & Shell Contract, Frontend File Contract, Validation Rules, Failure Path to Verify, Page Help (UXH-001) | E4 | plan kimliği yok | plan satırı yok |
| DL-2 | outbound `customerRef` + `customerAddressId` + CRM U-1 doğrulaması (yalnız `active` = `isBillable`, sözleşme §5 sınıflandırması) + üç hata kodu + WMS-BUNDLE / WAREHOUSE-OUTBOUND eklemeli sürüm (CT adımı) | Owned Objects, Entity Fields, Sunucu tarafında müşteri kimliği, Sözleşme değişiklikleri ve MVP-6 etkisi, Backend File Convention, Authorization Convention, Gateway / API Routing Decision, Audited Events | E4 | plan kimliği yok | plan satırı yok |
| DL-3 | alıcıyı CRM'den seçme (izin koşullu seçici, teslimat adresi listesi, ön doldurma, boş liste, bağlantıyı kaldırma, kayıtta CRM hatası, seçimden sonra pasifleşen account uyarısı) + Details rozeti, ülke adı, ilçe + canlı E3 | Alıcı seçimi, Frontend File Contract, Authorization Convention, Page Help (UXH-001) | E4 | plan kimliği yok | plan satırı yok |

## 24. Dilim kapıları ve bağlı kararlar

Paket onaylanmadan (künye `approved` / `ready-for-dev` + CT-APR-02 inceleme kaydı + CT-APR-01 onay satırı, onaylayan Ali) hiçbir dilim başlamaz.

| Dilim | Başlamadan kapanması gereken | Seam'ler (XMC-001) | AC | Sabotajlar (§16 ile aynı adlar) |
|---|---|---|---|---|
| DL-1 | ~~WP-0178-E3-FIX-01 CT kabulü ve hatta birleşmesi~~ — **kapandı** (CT VERIFIED `49fd410b7`; FU01 dalı FIX-01'li tabandan, §8.1); Q-8 | yok (yalnız MOD-0178 Web dosyaları) | AC-1…AC-13 | S-FU01-1, S-FU01-2, S-FU01-3, S-FU01-4, S-FU01-5, S-FU01-6, S-FU01-7, S-FU01-8, S-FU01-9, S-FU01-10, S-FU01-11, S-FU01-11b, S-FU01-12, S-FU01-13 |
| DL-2 | Sahip kararları Q-2, Q-4, Q-9 verildi (r2, `a7c1d32b8`) — başlama kapısı yok; DL-1 kabulü gerekmez (paralel yürüyebilir, ama `OutboundDtos.cs` / proxy birleşmesi sırayla). **Kabul kapısı (r3, F-3):** XMC-CRM-01 — CRM-ACCOUNT-LOOKUP §5a'da MOD-0178 satırı en az U-1 ile yazılı (CRM sahibinin commit'i atıfla) | Recall adaptörü okunmaz-değiştirilmez, kod paylaşılmaz (Q-9 kararı); MVP-6 bilgi (Q-6); CRM §5a satırı (XMC-CRM-01, Beste — CRM) | AC-14…AC-24 | S-FU01-14, S-FU01-14b, S-FU01-14c, S-FU01-15, S-FU01-16, S-FU01-16b, S-FU01-16c, S-FU01-16d, S-FU01-16e, S-FU01-17, S-FU01-17b, S-FU01-18, S-FU01-19, S-FU01-19b, S-FU01-20, S-FU01-20b, S-FU01-20c, S-FU01-20d, S-FU01-20e, S-FU01-21, S-FU01-22, S-FU01-22b, S-FU01-23 (AC-24 kanıt maddesi — kendi sabotajı yok) |
| DL-2 yayın (CT sözleşme adımı) | Q-6 (MVP-6 cevabı), Q-7 (sürüm sırası) | `docs/analysis/contracts/` (CT tek yazıcı) | AC-21 | S-FU01-21 |
| DL-3 | **CRM-ACCOUNT-LOOKUP 1.1.0 (`ship-to` ucu, adres listesi, `crm.account-ship-to.read`) dondu ve CRM'de uygulandı** (C-5, C-10: taslak → inceleme → kod → donma; tahmin ana adresle ~2 hafta, Beste — CRM); Reference Data kaynağı (Q-11); `ship-to` cevabında account durumu (Q-13); **XMC-CRM-01 — §5a satırı U-3 ve 1.1 `ship-to` ile** (r3, F-3; 1.1 metniyle birlikte); DL-1 ve DL-2 kabulü. Q-3, Q-10, Q-12 kararlaştı (r2) | CRM (Beste — CRM; anahtar CRM manifestinde; §5a tüketici satırı), Auth rol eşlemesi (Q-5, Ali — kabul kapısı; eşleme yoksa seçici çizilmez) | AC-25…AC-32 | S-FU01-25, S-FU01-25b, S-FU01-25c, S-FU01-26a, S-FU01-26b, S-FU01-26c, S-FU01-26d, S-FU01-26e, S-FU01-26f, S-FU01-26g, S-FU01-27, S-FU01-28, S-FU01-28b, S-FU01-28c, S-FU01-29, S-FU01-29b, S-FU01-29c, S-FU01-29d, S-FU01-29e, S-FU01-30, S-FU01-30b (AC-31 canlı kanıt maddesi — kendi sabotajı yok) |

Sıra: FIX-01 (kabul edildi) → DL-1 → DL-3; DL-2 DL-1 ile paralel, DL-3'ten önce. CRM eki gecikirse DL-1 ve DL-2 kendi başına canlıya çıkabilir (elle alıcı
girişi tam çalışır; `customerRef` yalnız API'den gelir).

## 25. Page Help (UXH-001)

- Sayfa kodu `OUTBOUNDSHIPMENTS` (Create / Edit aynı kodu kullanır — `Create.cshtml` `ViewData["PageCode"]`). Güncellenecek anahtarlar:
  `Help.OUTBOUNDSHIPMENTS.Purpose`, `.Audience`, `.Step1Title`/`.Step1` … `.Step5` — 1: depo ve (varsa) sevkiyat hazırlık lokasyonunu seç;
  2: "Satır ekle" ile yan panelde ürün, SKU ve miktarı gir; 3: "Tahsisi önizle" ile lotları gör, gerekirse "Lot değiştir" ve gerekçe;
  4: alıcıyı elle yaz ya da — CRM yetkin varsa — CRM'den seç ve teslimat adresini onayla (gerekirse düzenle) (DL-3'e kadar yalnız "elle yaz"; r3: metin çizilmeyen bir kontrolü koşulsuz vaat etmez); 5: kaydet — tahsis kayıtta yeniden hesaplanır.
  `.Immediate`: "Önizleme stok ayırmaz; kayıt anında stok değişmişse satır karşılanamaz olarak işaretlenir."
- Kiracı modülü → 7 dil. **Tur yok** (beş akıştan biri değil). Sahip kontrol listesi: Enter panelde "Satırı kaydet"e basar; "?" metni doğru;
  "tur yok".
