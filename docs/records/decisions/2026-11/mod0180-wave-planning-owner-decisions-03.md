# MOD-0180 Dalga Planlama — sahip kararları 03 (2026-11-12)

Kaynak: bağımsız inceleme `docs/records/reviews/MOD-0180/2026-11-12-c7c774c6.md` (r1, KOŞULLU) ve paketin Q-1…Q-22'si. Sahip: Beste —
"Y-1…Y-4 için önerdiğin gibi, Q'lar için Q-6 ayar izni, diğerleri öneri gibi" (MVP-5 WMS CT oturumu `ca7382cf`, 2026-11-12).

| # | Konu | Karar |
|---|---|---|
| Y-1 (F-1) | Ertelenmiş sipariş varken depoda dalga yönetimini kapatmak | **Reddedilir (409)**; önce bekleyen siparişler dalgaya alınır ya da geri bırakılır |
| Y-2 (F-2) | Dalgadan çıkarılan / 0178'de iptal edilen sipariş | **"Kısmen tamamlandı" sayılır** — D-9 bu ekle genişler |
| Y-3 (F-11) | "En kısa yol" (D-2) | v1'de **S-biçimli belirlenimci yaklaşık yöntem** kabul (kesin optimum iddia edilmez; AC-25 39 / 66 ölçüsü) |
| Y-4 (F-10) | `.antigravity` satırları (PKS-001 `warehouse.waves.*` oyması, RCS-001 `WAV`) | **Paket onayından önce** yazılır; CT satırları hazırlar, `.antigravity/**` değişikliği sahip onayıyla |
| Q-6 | Depo dalga ayarı (WaveManaged) izni | **B — ayrı ayar izni** `warehouse.waves.settings.configure` (Tier-3) |
| Q-1…Q-5, Q-7…Q-16, Q-18, Q-19, Q-21, Q-22 | paket önerileri | **Öneri gibi (A)** |
| Q-17 | Hedef tarih | sahipten bekleniyor |
| Q-20 | Rol şablonu | §14 önerisi; karar Ali (Auth) |

## Ek — Y-1 netleştirmesi (r2 incelemesi F-2; Beste "öneriler tamam", 2026-11-12)
- **Y-1 = A:** Depoda dalga yönetimi kapatılırken bekleyen (`Deferred`) siparişler **reddedilmez ve iptal edilmez**; kapatma anında her
  bekleyen siparişin toplama görevleri dalga yokmuş gibi hemen açılır (MOD-0178 normal akışı). r2'deki 409 `WAVE_SETTINGS_DEFERRED_ORDERS`
  kuralı bu kararla değişir. Sahip ayrıca: MOD-0178'e düşülecek not (X-16) r29'u MOD-0178 r28 ile aynı onay turunda Ali'ye götürmek; PKS-001
  :92 istisna cümlesi `.antigravity` kapısı onayına dahil.
- **Q-17 = 24 Kasım 2026** (Beste, 2026-11-12). **"Hemen" teyidi** (r3 incelemesi (c) 3; Beste "evet hemen sayılır", 2026-11-13): kapatma
  isteğinde ilk tur (en çok 200 sipariş / 2.000 görev), kalanı en geç bir bakım turu (60 sn) içinde açılır — Y-1'in "hemen"i budur.
- **2026-11-13 — Q-23 = A (evet)** (r5 incelemesi `2026-11-13-0517db83` F-1; kaynak: sahip sohbet cevabı, Beste 2026-11-13): bir depoda
  10.200'den fazla bekleyen sipariş varsa fazlası sonraki bakım turunda açılır; bu "hemen" sayılır. İlk tur ≤ 200 sipariş / 2.000 görev ve
  bakım turunda **depo başına** tur başına en çok 50 parti sınırı kabul (MOD-0180 r6 §8.19, §19.4 Q-23).
- **2026-11-13 — X-16 bekçisi onaylandı** (MOD-0178 r29 incelemesi F-1; kaynak: sahip sohbet cevabı, Beste 2026-11-13): MOD-0178'in
  `warehouse.waves.*` izinlerini kullanmadığını ölçen test (AC-5 alt ad alanı sahipliği vakası, sabotajlar S-5c / S-5d) MOD-0180'in ilk
  dilimine (DL-1) konur — **sahip onayladı 2026-11-13** (MOD-0180 r6 §14, §16 AC-5, §18, §24).
- **2026-11-13 — X-14 Gateway uç sayısı (XMC-001 sahip onayı)** (MOD-0180 r7 yazar raporu; kaynak: sahip sohbet cevabı, Beste 2026-11-13,
  "tamam" — MOD-0181'deki "70'ten 73'e çıkar" kararıyla aynı yol): `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` :97
  `api/warehouse` uç sayısını 73'e sabitler; MOD-0180'in ilk Gateway dilimi kendi uçlarını eklerken bu sayıyı yeni toplama günceller.
  Rota (`ocelot.json`) ve bu test değişikliği integration-agent'ındır. MOD-0180 X-14 ve AC-55 buna göre yazılır.
- **2026-11-13 — X-14 kapsam teyidi** (MOD-0180 r8 incelemesi `2026-11-13-e2666ea4` F-2; kaynak: sahip sohbet cevabı, Beste 2026-11-13,
  "evet"): yukarıdaki X-14 onayı uç ekleyen **her** MOD-0180 dilimini kapsar — DL-1, DL-2, DL-3 ve DL-5 her biri
  `Mod0178WarehouseRouteTests.cs` uç sayısı onayını (bugün `Assert.Equal(73, endpoints.Count)`) dilim başında ölçülen sayı + kendi eklediği
  uç sayısıyla, uçlarıyla aynı commit'te günceller (beklenen 73 → 87 → 89 → 94 → 96). Her artışın önce / sonra değeri dilimin kabul kaydına
  yazılır; ayrı sahip onayı gerekmez.
