# MVP-1 / MVP-5 — CT kabul kayıtları (toplu, 2026-11-13)

CT: merkez (Beste adına), oturum ca7382cf. Kaynak: geliştirici oturumlarının raporları (oturum dökümleri), CT'nin kendi koşuları ve PR #424
CI'ı. Her kalemde "Ölçülmeyen" ayrıca yazılı.

## 1. MOD-0181 DL-6 — Gateway kapsama testi · VERIFIED
- Commit `a7ec33562` (`gateway/Diten.ApiGateway.Tests/Mod0181CycleCountRouteTests.cs`, +186; yalnız test). Etiket başı `ct/line-ff-2026-11-13f`.
- Ölçüm (geliştirici): uç listesi koddan (`Controllers.CycleCounting`: CycleCounts 13, CountVariances 9, CountEvidenceExports 3 = 25),
  WMS-COUNT-BUNDLE 1.1.0 FROZEN ile iki yönde eşit; her uç `/api/warehouse/{everything}` → `localhost:5067`; gerçek Gateway üzerinden 25 uç.
- Gateway takımı: önce 371/0, sonra 375/0. Sabotaj (a) bin kopyasında port 5068 → 4 testten 3'ü kırmızı; (b) mutlak şablon → 3'ü kırmızı;
  bayt-eşit geri alındı. `ocelot.json` sha256 öncesi = sonrası.
- CT: Gateway 375/375 MOD-0180 DL-1 kabulünde de ölçüldü (bu test dahil). PR #424 CI.
- Ölçülmeyen: CT sabotajları yeniden koşmadı. Kalan MOD-0181 kapısı: DL-4 ACCEPTED için Auth rol şablonu (Ali, bekletmez).

## 2. MOD-0178-FU01 DL-1 — çıkış formu v2 (Web) · KOŞULLU → canlı tur sonrası VERIFIED
- Commit `32ef19ca7` (feat, 20 Web dosyası), `f4877003c` (test, 4 dosya). Rapor: 14 paket + 7 ek sabotaj tuttu.
- CT tam Web.Tests koşusu (`3e31bfde9`, bu DL-1 dahil): 4543/4546; 3 kırmızı main'de de kırmızı. vitest: 13 kırmızı, tabanla aynı ve main'de de
  aynı (Windows satır sonu / yol; CI Linux'ta yeşil).
- CT kararları: 375 px'te `table-responsive` yatay kayma kabul; Edit'te ürün adı DL-2'de (proxy değişikliği).
- **Canlı turda bulunan hata (CT, 2026-11-13):** `/Warehouse/OutboundShipments/Create` yarıda kesiliyordu (ERR_INCOMPLETE_CHUNKED_ENCODING).
  Neden: `_LineOffcanvas.cshtml:48` `@Localizer["LinePanel.Unit"]` — değer `{0}` içeriyor, argümansız basılınca HTML localizer biçimlendirip
  hata fırlatıyor. Düzeltme `1a9ee5333` (`.Value`, emsal CRM LegalProfiles `_Form.cshtml:103`) + bekçi
  `WarehouseLocalizerPlaceholderGuardTests` (yeşil; `.Value` silinince kırmızı, geri alındı). Birim testler Razor çizmediği için yakalamamıştı.
- Geliştiricinin süreç hatası: dala girmeyen geçici commit'te bir kez `core.hooksPath=/dev/null`; geri alındı, kayda geçti.
- Kalan: canlı tur (iki lotla önizleme; 375 px `scrollWidth` tr / en / ar). Ana checkout Web'i düzeltmeyi alınca yapılır; sonuç bu kayda eklenir.

## 3. MOD-0177-FU01 AC-17 — SHIPMENT_BUNDLE_350 atlamasının kaldırılması · VERIFIED
- Commit `64c3ef26a` (cherry-pick -x 016c892b7, 4 dosya), `481bbea29` (AC-17 r9 testi). Adım 0 `e22a28eb4` (--no-ff CT etiketi).
- `CustomerRef_field_matches` allOf / $ref çözümü, `[string, null]` tam küme, pattern harfiyen. Sabotajlar: pattern sil, a-f → A-F,
  tür tekil, alan adı, RepoPaths filtresi — hepsi kırmızı, geri alındı.
- Inventory filtresiz: taban 2130 geçti / 5 atlandı; dal 2131 / 4 atlandı (yalnız INVENTORY_PERF); kırmızı yok. CI skip denetimi dalda 0.
- CT: `ShipmentBundle350AlignmentTests` CT'de ayrıca yeşil (1/1, 2026-11-13). PR #424 CI inventory-tests.
- Ölçülmeyen: `scripts/tests` 240 failure / 6 error (taban ve dalda aynı sayı; adları karşılaştırılmadı).

## 4. MOD-0262-FU08 DL-1 — yükleme denetim kaydı nesneyi adlandırır · VERIFIED (errata ile)
- Commit `02a1a41cd` (9 dosya, hepsi paket §5 içinde). AC-1…AC-16 test adları raporda; S-1…S-20 kırmızı, geri alındı. AC-16 gerçek
  Mongo: aynı correlation ile 503 sonra 201 → iki kayıt (Failed boş EntityId; Succeeded EntityId = ContentId).
- Platform.Application filtresiz: önce 9304/9323 (19 kırmızı), main 9140/9159 (19), sonra 9319/9339 (20). 19 kırmızı adı adına aynı
  (Mongo ortam); 20. `InternalRecordCodesHttpTests.AC26_…3_s…` yük altında süre testi, tek başına 46/46 yeşil.
- CT kararı: DL-1 yükleme cevabı **200** kalır (FU01 §19, `DocumentRepositoryService.cs:300`); paket AC-2 / S-12'deki "201" errata,
  sonraki revizyonda düzelir. AC-15 geliştirme kaydı: K-7 yolu commit iletisinde (`mod0262-fu01-mod0181-owner-decisions-2026-11-12.md:33`).
- Kalan: AC-29 Auth audience (paket kapısı). PR #424 CI platform-app.

## Karar
MOD-0181 DL-6, MOD-0177-FU01 AC-17, MOD-0262-FU08 DL-1: VERIFIED. MOD-0178-FU01 DL-1: render hatası düzeltildi; canlı tur sonrası VERIFIED.
