# MVP6 — Auth ve Shipment integration efor güncellemesi R4

**Cut-off:** 2026-09-24. **Verdict:** Update-03’ün 103 satırlık rubric’i ve toplam tahmin paydası korunarak fresh Auth-chain kapanışı ilerlemeye işlendi; iki Shipment rework kalemi mevcut rezervler içinde O/M/P olarak tahmin edildi.

## Portföy sonucu

- Delivered most-likely: **1372 → 1388 saat** (`+16`).
- Remaining most-likely: **1432 → 1416 saat** (`-16`).
- Total most-likely: **2804 saat**, değişmedi.
- Efor ağırlıklı tamamlanma: **%48,9 → %49,5**.
- O/M/P toplam payda değişmedi. Bu bir efor/takvim/full-finish yüzdesi değildir.

Fresh Auth-chain PASS’i yalnız mevcut Shared Test/VER rezervini delivered duruma taşır. Update-03’te daha önce teslimata geçirilen NumericDate implementation eforu tekrar sayılmaz. Shipment rework’ün uygulama ve bağımsız doğrulama tahminleri mevcut 0183 Integration/Test-VER rezervlerine alt tahsistir; portföy toplamına eklenmez.

## On teslimat başlığı

| Başlık | Durum | Delivered / remaining / total ML saat | Tamamlanma | Yeni girdinin etkisi |
|---|---|---:|---:|---|
| 0183 Shipment/POD | PARTIAL — rework tahminlendi | 204 / 90 / 294 | **%69,4** | Disposable hazırlık tamam; exact source closure ve gateway successor gerçek hedefe uygulanmadı, bağımsız VER yapılmadı. |
| 0184 Carrier | PARTIAL — değişmedi | 160 / 48 / 208 | **%76,9** | Shared Auth kanıtı Carrier altında tekrar sayılmadı; Carrier real-Auth UI E2E ayrı kapı. |
| 0185 Loads | PARTIAL — değişmedi | 136 / 134 / 270 | **%50,4** | Yeni kanıt yok. |
| 0186 Returns | PARTIAL — değişmedi | 172 / 106 / 278 | **%61,9** | Yeni kanıt yok. |
| 0187 Claims | PARTIAL — değişmedi | 200 / 114 / 314 | **%63,7** | Yeni kanıt yok. |
| 0190 S&OP | PARTIAL — değişmedi | 148 / 136 / 284 | **%52,1** | Yeni kanıt yok. |
| 0192 Capacity | PARTIAL — değişmedi | 220 / 144 / 364 | **%60,4** | Yeni kanıt yok. |
| 0147 Supplier Performance | PRE-DEV / PARTIAL | 20 / 248 / 268 | **%7,5** | Yeni kanıt yok. |
| 0148 Supplier Portal | PRE-DEV / PARTIAL | 20 / 232 / 252 | **%7,9** | Yeni kanıt yok. |
| Ortak işler | PARTIAL — ilerledi | 108 / 164 / 272 | **%39,7** | Fresh login, refresh, LE re-resolution, scope ayrımları ve refusal/timeout S2 rezervini kapattı. |

## Modül ve kategori yüzdeleri

| Kapsam | Pack/tasarım | Contract | Backend | Frontend | Entegrasyon | Test/VER | Genel |
|---|---:|---:|---:|---:|---:|---:|---:|
| 0183 Shipment/POD | 66,7% | 72,7% | 80,0% | 100,0% | 33,3% | 47,1% | **69,4%** |
| 0184 Carrier | 80,0% | 80,0% | 92,3% | 76,9% | 66,7% | 60,0% | **76,9%** |
| 0185 Loads | 72,7% | 66,7% | 80,0% | 0,0% | 20,0% | 57,1% | **50,4%** |
| 0186 Returns | 76,9% | 83,3% | 90,0% | 0,0% | 50,0% | 66,7% | **61,9%** |
| 0187 Claims | 76,9% | 85,7% | 91,7% | 0,0% | 50,0% | 70,6% | **63,7%** |
| 0190 S&OP | 71,4% | 83,3% | 84,2% | 0,0% | 27,3% | 57,1% | **52,1%** |
| 0192 Capacity | 75,0% | 85,7% | 89,7% | 0,0% | 38,5% | 66,7% | **60,4%** |
| 0147 Supplier Performance | 42,9% | 33,3% | 0,0% | 0,0% | 0,0% | 0,0% | **7,5%** |
| 0148 Supplier Portal | 42,9% | 33,3% | 0,0% | 0,0% | 0,0% | 0,0% | **7,9%** |
| Ortak işler | N/A | 0,0% | 100,0% | 0,0% | 0,0% | **42,9%** | **39,7%** |
| MVP6 toplam | 66,7% | 60,8% | 72,4% | 20,3% | 25,2% | **50,0%** | **49,5%** |

## Fresh Auth-chain kredisi

Update-03 `S2-REMAINING` satırı O/M/P **9,6/16/32 saat** ile “Shared Auth login/refresh/re-resolution independent completion” teslimatını taşır. `mvp6-carrier-auth-chain-recovery-01` bunu fresh native .NET 8 süreçlerinde tamamladı:

- gerçek Auth-issued login ve refresh;
- refresh-time Legal Entity re-resolution;
- zero/multiple/inactive/revoked scope durumlarında claim omission;
- Platform refusal ve timeout için token üretmeden fail-closed davranış.

Bu yüzden S2 rezervinin tamamı delivered’a taşındı. Önceki NumericDate 16 saatlik implementation kredisi `S1-DELIVERED` içinde kalır ve tekrar sayılmaz.

MFA, forced-password ve daha geniş tenant/actor negatifleri yeni pakette yeniden çalıştırılmadı; `INHERITED / NOT RERUN` olarak kalır. Bu rapor onları fresh PASS yapmaz. Bununla birlikte bunlar Update-03’teki exact S2 kalan teslimatının dışında olduğundan yeni bir rezerv uydurulmadı. CT ileride fresh yeniden koşu isterse bu yeni kapsam kararı olur.

## Shipment rework O/M/P

Bir kişi-günü karşılaştırma varsayımı **8 aktif uzman saati**dir; ölçülmüş süre değildir.

| Teslimat | Uygulama O/M/P | Bağımsız VER O/M/P | Toplam O/M/P saat | Toplam kişi-günü | Durum |
|---|---:|---:|---:|---:|---|
| Exact accepted-source closure | 8/16/28 | 4/8/16 | **12/24/44** | **1,50/3,00/5,50** | Disposable hazırlık tamam; authority, gerçek uygulama ve VER açık. |
| Dar gateway düzeltmesi + regresyon | 4/8/12 | 2/4/8 | **6/12/20** | **0,75/1,50/2,50** | İki dosyalı aday doğrulandı; gerçek uygulama ve bağımsız regresyon açık. |
| Toplam | **12/24/40** | **6/12/24** | **18/36/64** | **2,25/4,50/8,00** | Yeni portföy eforu değildir. |

Bu tahminler 258 dosya veya 22 test üzerinden üretilmedi. Source closure tahmini fail-closed preimage kontrolü, absent-only aktarım, üç successor conflict koruması, üç build, bağımsız yeniden üretim ve recovery sorumluluğuna dayanır. Gateway tahmini iki preimage’ın korunması, CRM/SupplyChain sahiplik ayrımı, dar uygulama, route regresyonu ve başarısız deneme/recovery sınırına dayanır.

## Rezerv örtüşmesi

- `0183-5-REMAINING` Integration rezervi **12/24/40 saat**: iki rework’ün uygulama kısımlarına tam tahsis edildi; yeni satır olarak eklenmedi.
- `0183-6-REMAINING` Test/VER rezervi **20/36/64 saat**: source closure VER `4/8/16` ve gateway regression `2/4/8` olmak üzere toplam `6/12/24` saat alt tahsis edildi.
- Test/VER rezervinin kalan **14/24/40 saati**, composed runtime/browser ve diğer bağımsız acceptance için açık kalır.
- Disposable hazırlık tamamlanmıştır fakat gerçek hedef uygulaması değildir; geçmiş hazırlık saati ölçülmüş gibi kredilendirilmedi.

## Değişim ayrıştırması

| Sınıf | Delivered ML | Remaining ML | Toplam tahmin | Açıklama |
|---|---:|---:|---:|---|
| Tamamlanan iş — Auth chain | +16 | -16 | 0 | Mevcut S2 rezervi delivered’a taşındı. |
| Tamamlanan hazırlık — Shipment | 0 | 0 | 0 | Disposable aday hazır; üretim/integration uygulaması değil. |
| Tahmin düzeltmesi — Shipment rework | 0 | 0 | 0 | İki UNESTIMATED bulgu mevcut rezervlerde O/M/P alt tahsise dönüştü. |
| Kapsam değişimi | 0 | 0 | 0 | Yeni teslimat kapsamı eklenmedi. |

Readiness, release gate, production readiness ve takvim ilerlemesi bu efor metriğinden ayrıdır.
