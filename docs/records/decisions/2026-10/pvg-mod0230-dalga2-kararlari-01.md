---
status: approved
decided: 2026-10-26
module: MOD-0230
work_packages: WP-0230-DL2B, WP-0230-DL3B
---

# PVG-MOD0230-DALGA2-01 — İkinci dalga kararları (K1–K6)

> - **Karar sahibi:** sahip. Cevabı: "1 hepsi-evet", 2026-10-26, PVG Control Tower sohbeti. Altı öneri, önerildiği biçimiyle kabul edildi.
> - **Kayıt:** PVG Control Tower.
> - **Dayanak incelemeler:**
>   - `docs/records/reviews/MOD-0230/prompts/WP-0230-DL2B-v1.0-inceleme-01.md` (2 engelleyici)
>   - `docs/records/reviews/MOD-0230/prompts/WP-0230-DL3B-v1.0-inceleme-01.md` (1 engelleyici)
> - **Paket:** `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md`. Satır numaraları main @ `05f311784`'teki revizyon 09'a göredir.

| # | Karar | Gerekçe | Gerileme değerlendirmesi |
|---|---|---|---|
| K1 | **Seçenek kümesi.** WP-0230-DL2B'de ürün satırı rolü (AC-21) ve rapor türü (AC-27), kodda tek yerde duran bir PVG kümesi ile denetlenir ve küme dışı değer reddedilir. Ülke listesi, ekrandaki kopyalar ve başka modüllerin listeleri yasak kalır. Paketin "sabit seçenek listesi" yasağı (satır 1827) ve referans veri yolu kararı (satır 1002-1003) bu kümeyi kapsamaz. Küme, referans veri yolu seçilince oraya taşınır. | AC-21 ve AC-27 küme dışı değerin reddini istiyor; bunun için kümenin bir yerde durması gerekiyor. Tek yerde tutmak, taşımayı tek noktaya indiriyor. AC-27'nin beş değeri QPPV onayına kadar "onaylı" sayılmaz. | Gerileme yok. Bugün bu alanlar yok. Taşıma, referans veri yolu kararından sonra ayrı iş olur. |
| K2 | **DL2B'nin DL-2 yolları dışındaki iki dosyası.** Yalnız şu iki yere dokunulabilir: `services/Diten.PvgService/src/Diten.PvgService.Api/PvgCaseIntakeTriageEndpoints.cs` içindeki iki `ToApplicationRequest()` gövdesi (satır 231-245 ve 263-277), ve `services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/PvgServiceApiHostTests.cs` satır 41. Api istek kayıtlarının biçimi değişmez; o iş AC-31 / DL-4'ündür. | Alan değişikliği bu iki yerde derlemeyi kırıyor. Tüketici araması (`git grep` @ `05f311784`) başka bağ bulmadı. Bu, DL2A-KAPSAM-01'deki dersin uygulamasıdır. | API create ve update bugün `409 Blocked` dönüyor. DL2B'den sonra DL-4'e kadar `400` döner, çünkü doğrulama izin kapısından önce koşuyor (G-9; düzeltmesi AC-7 / DL-4). Canlı kapı kapalı ve tümünü reddeden adaptörler yerinde; kullanıcıya etkisi yok. Bu değişiklik DL2B raporunun "Known gaps" bölümüne yazılır. DL-4 bu davranışı resmileştirir. |
| K3 | **Boş ortam adı.** `ASPNETCORE_ENVIRONMENT` boş dize ise ortam "verilmemiş" sayılır: Production, yani üretim benzeri; güvenli tarafta kalınır. Paket yalnız "unset" durumunu tanımlıyor (satır 671); boş değer bu kararla ona eşitlenir. | Boş değerin üretim dışı sayılması fail-closed değil: bugün host boş değerle üretim dışı adaptörlerle açılıyor (DL3A doğrulaması, `PvgServiceApiHost.cs:41`, `:153`). | Host kodundaki düzeltme küçük, ayrı bir DL-3 işi olur; DL3B'nin izinli yolu yalnız test dosyası. DL3B, bu durumu tarayıcıda karar olarak uygular ve host açığını "Known gaps"e yazar. Gerileme yok, çünkü düzeltme davranışı daha güvenli yapar. |
| K4 | **Tarama kapsamı (AC-9).** `docs/records/**` taranmaz. `.env` dosyaları dağıtım dosyası sayılır. Prompt v1.0'daki fail-closed seçimler (değeri belirlenemeyen referans ihlaldir; şablon ortam değeri üretim benzeridir; `UseNonProductionAdapters`'ı üretim benzeri kapsamda `false` olarak bile kurmak ihlaldir) bu kayıtla kabul edilir. | `docs/records/**` değişmez kanıttır (K4). Taranırsa, ileride commit edilen bir kanıt dosyası T1'i kalıcı olarak kırmızıya çevirir ve kayıt düzeltilemez. `.env` dağıtımda ortam taşır. Fail-closed seçimler, inceleyenin "pakete dayanan izinli okuma" dediği seçimlerdir. | Bugün tarama 82 dosya seçiyor ve 0 ihlal buluyor (inceleme 11 ölçümü). Hariç tutma bugünkü sonucu değiştirmez. |
| K5 | **Paralel dağıtım.** WP-0230-DL2B ve WP-0230-DL3B aynı anda dağıtılabilir. | İki inceleyen de çakışma görmedi: DL2B test dosyasında yalnız satır 41'i değiştiriyor, DL3B sınıfın sonuna ekleme yapıyor. Birleştirme sırası: önce hangisi biterse; ikincisi `main`'i birleştirip kontrolleri yeniden koşar (§7a). | Metinsel çakışma yok. İkinci birleşmede derleme ve test kontrolü yeniden yapılır. |
| K6 | **DL3B'nin dağıtım koşulu.** `pvg-mod0230-dl3-bolme-01.md` DB-2'deki "DL3A kabul edildikten sonra" koşulu şöyle değişir: "DL3A `main`'de (#213) ve CT tarafından doğrulandı (`pvg-wave1-verify-01/VERIFY.md`, kuyrukta `INTEGRATED`)". | DL3A'nın resmî kabulü artık E3 canlı kanıtına bağlı (`pvg-mod0230-kanit-seviyesi-01.md`). Eski koşulda kalmak DL3B'yi canlı kapıya kadar durdururdu. DB-2'nin amacı aynı dosyada sırayla çalışmaktı; DL3A kodu `main`'de olduğu için bu amaç karşılanmış durumda. | DL3A'nın kabulü açık kalır; DL3B onu değiştirmez. |

## Uygulama

- DL2B ve DL3B prompt'ları v1.1'de bu kayda atıf yapar. İnceleme bulgularının "diğer" maddeleri de v1.1'de düzeltilir.
- Kuyruk değişmez. Satırlar, DoR kaydıyla `PLANNED → READY` olur.
- DB-2 kaydı değiştirilmez (K4 kuralı). Onun yerine bu kaydın K6 kararı geçerlidir.
