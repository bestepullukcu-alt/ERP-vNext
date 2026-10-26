# PVG-MOD0230-DL2A-KAPSAM-01 — DL2A kapsamına tek bir test sahtesi imzası eklendi

> Karar sahibi: PVG Control Tower. Sahip onayı: "1. evet" (`dl2a-duzelt`), 2026-10-23, PVG Control Tower sohbeti.
> Modül: MOD-0230 Case Intake & Triage. İş paketi: WP-0230-DL2A, PR #215.

## Durum

- WP-0230-DL3A (#213) 2026-10-23'te `main`'e girdi; merge commit `50600c5b3`.
- #213, `services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/PvgServiceApiHostTests.cs` dosyasına `TestIntakeDraftStore` adlı bir test sahtesi ekledi. Bu sınıf `IPvgIntakeDraftStore.ReplaceAsync`'in eski imzasını uyguluyor.
- WP-0230-DL2A ise bu imzayı değiştirdi: yeni bir `long? expectedVersion` parametresi ekledi ve dönüş tipini `PvgIntakeDraftReplaceResult` yaptı.
- İki iş ayrı ayrı yeşildi, ama birleşince derlenmiyor. #215'in CI'ındaki `pvg-api` kontrolü kırmızı: `CS0535`, `PvgServiceApiHostTests.cs(1483,49)`.

## Kök neden

Dağıtım planındaki DP-1 kararı (`pvg-mod0230-dagitim-plani-01.md`) DL2A ile DL3A'nın yollarını ayrık saydı. Bu kontrol yalnız dosya yollarına baktı. Paylaşılan bir arayüzü başka bir test projesinde uygulayan sınıflar hesaba katılmadı.

**Ders:** Arayüz imzasını değiştiren bir iş paketinde, o arayüzün bütün uygulamaları depo genelinde aranır (`git grep ": IArayüz"`). Bulunan dosyalar ya izinli yollara eklenir ya da sıralama bağımlılığı olarak yazılır.

## Karar

1. WP-0230-DL2A'nın izinli yollarına tek bir değişiklik eklenir: `PvgServiceApiHostTests.cs` içindeki `TestIntakeDraftStore.ReplaceAsync` metodunun imzası yeni arayüze uydurulur.
   - Gövde değişmez; yine `NotSupportedException` fırlatır.
   - Dosyanın başka hiçbir satırı değişmez.
2. Düzeltme #215'in dalına yapılır: önce `main` birleştirilir, sonra ayrı bir commit atılır, ardından push edilir. Main'e doğrudan yazılmaz.
3. Doğrulama:
   - Api süiti 58/58, düz ve `Development` ile.
   - RegPvBase 97/97.
   - `known_failures` için `pvg-api` ve `pvg-regpv-base` çıkış 0.
   - CI'da `pvg-api` yeşil.
   - CT diff'i kontrol eder: yalnız bu metot değişmiş olmalı.

## Gerileme değerlendirmesi

- Davranış değişmez: sahte sınıfın metodu hiçbir testte çağrılmıyor ve yine hata fırlatıyor.
- Hiçbir test zayıflamaz.
- Düzeltme yapılmazsa #215 birleştiğinde `main` derlenmez. Bu yüzden düzeltme birleştirmeden önce gelir.

## Sonraki dağıtımlar için

DL2B ve DL3B prompt'larında arayüz imzası değişirse, uygulamaların aranması DoR kontrolüne eklenir.
