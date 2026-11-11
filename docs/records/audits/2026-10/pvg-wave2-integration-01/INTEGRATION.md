# PVG ikinci dalga — doğrulama ve entegrasyon kapısı raporu (WP-0230-DL2B, WP-0230-DL3B)

> Control Tower kaydı, 2026-10-26. Bu kayıt, kuyrukta `DISPATCHED → VERIFIED → INTEGRATION_READY → INTEGRATED` geçişinin kanıtıdır.
> Bu kayıt kabul kaydı değildir. Paket revizyon 10, DL-2 için E4 ve DL-3 için E3 ister. Bu iki iş E2 kanıtı üretir; `ACCEPTED` için CT-OPS-03 künyeli kabul kaydı ve operasyonel runtime kapısı gerekir.
> Önceki adım: `docs/records/audits/2026-10/pvg-wave2-dispatch-01/` (dağıtım kapısı raporları).

## Bağımsız doğrulama (VERIFIED)

Her iki işi yazarından ayrı bir CT ölçücüsü ölçtü. Ölçüm, işçinin çalışma ağacına dokunmadan, ayrı bir kopyada yapıldı.

| WP | Ölçülen hâl | Sonuç | Kanıt özeti |
|---|---|---|---|
| WP-0230-DL2B | taban `830c6c1a5` + işçinin 22 dosyası (blob karşılaştırması 22/22 aynı) | KABUL EDİLEBİLİR | 43 sabotajdan 41'i adı verilen testi kırmızıya çevirdi. K2 geri alınınca derleme kırıldı. tr-TR kültüründe de yeşil. Mimari süitte tabanla aynı 3 kırmızı test var (taban borcu). |
| WP-0230-DL3B | `aafe15a7a` (DL2B dahil) + işçinin test dosyası, birleşik sha256 `9d0d903a315446af8c4f396ca45fdedada0dcc7c99282da6af1d2eb8671db54a` | KABUL EDİLEBİLİR | Api 154/154: R1, R2, R3 ve `LC_ALL=tr_TR.UTF-8`. RegPvBase 204/204. Kültür, Production karşılaştırması, `.git` hariç tutma, yorum ve komut satırı ve `Program` okuması sabotajları adı verilen testleri kırmızıya çevirdi. S-56 için iki tr-TR testi eklendi; N1b ve N8 sabotajlarında ikisi de kırmızı oldu. |

Ölçüm sırasında kalan iki boşluk sonraki işe taşındı. İkisi de `docs/records/decisions/2026-10/pvg-mod0230-dalga3-plani-01.md` D3-6'ya bağlıdır ve WP-0230-DL4A prompt'unda CT eki test olarak yer alır:
- Ürün satırlarının yalnız sırası değiştiğinde `ChangedFields` ölçülmüyor.
- API `ToApplicationRequest` içindeki ürün satırı eşlemesi boşaltılınca Api testleri yeşil kalıyor.

## Entegrasyon gerekliydi

İki iş de PvgService kodu ya da testi değiştirdi. Bu yüzden ikisi de `main`'e PR ile girdi. DL3B, DL2B ile aynı test dosyasını paylaşıyordu. Sahip kararı DALGA2-01 K5'e göre ikinci biten iş origin/main'i birleştirdi ve kontrolleri yeniden koştu. Çakışma çıkmadı.

## Birleşmeler (GitHub)

| WP | PR | Birleşme | Onaylayan / birleştiren | CI |
|---|---|---|---|---|
| WP-0230-DL2B | [#226](https://github.com/bestepullukcu-alt/ERP-vNext/pull/226) | `b6959baff`, 2026-10-26 12:42 UTC | bestepullukcu-alt (APPROVED) | PVG süitleri yeşil. phase1 kırmızı; `main`'de de bilinen, PVG dışı kırmızı. |
| WP-0230-DL3B | [#234](https://github.com/bestepullukcu-alt/ERP-vNext/pull/234) | `6deb54475`, 2026-10-26 18:51 UTC | bestepullukcu-alt (APPROVED) | `pvg-api` ve bütün `service-tests` yeşil. phase1 kırmızı; `main`'deki tedarik zinciri menü anahtarı sorunu, PVG dışı. |

## Entegrasyon kapısı ölçümü: PASS

Ölçüm CT dalında yapıldı. Dal, `main` `1451b2a50` ile birleştirildi. Bu dalda `services`, `gateway` ve `tests` yolları `main` ile aynıdır (`git diff --stat origin/main HEAD -- services gateway tests` boş). Ölçüm sırasında 1 dakikalık yük 4,2–4,6 idi.

| Süit | Sonuç | `known_failures.py check` |
|---|---|---|
| `Diten.PvgService.RegPvBase.Tests` | 204/204 | `pvg-regpv-base`: tüm sayaçlar 0 |
| `Diten.PvgService.Api.Tests` | 154/154 | `pvg-api`: tüm sayaçlar 0 |

Ek kontroller:
- `pack_approval.py check` (MOD-0230) çıkış 0.
- `ct_queue.py validate --queue pvg` 0 hata, 0 uyarı.

## Kabul engeli

İki iş de E2 kanıtı üretir. Paket revizyon 10, DL-2 için E4, DL-3 için E3 ister (`docs/records/decisions/2026-10/pvg-mod0230-kanit-seviyesi-01.md`). Satırlar operasyonel runtime kapısı açılana kadar `INTEGRATED` kalır.
