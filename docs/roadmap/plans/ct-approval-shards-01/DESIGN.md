# Onay satırlarını paket başına dosyaya bölme — tasarım taslağı

Durum: **taslak, uygulanmadı.** Sahip 2026-10-31'de yalnız tasarımı istedi ("Evet, tasarımı çıkar"). Kod, kural ve onay dosyası
değişmedi. Uygulama, pushtan önce ayrı sahip onayı ister; Codex incelemesinden geçer.

## Sorun (ölçüldü, `origin/main` 2026-10-31)

- Şu an çakışan dört PR'ın (#308, #312, #315, #317) dördünde de çakışan yalnız `approvals.tsv` ve `reviews.tsv`.
  `scripts/merge_append_only.py` ile dördü de temiz birleşiyor; GitHub bu sürücüyü çalıştırmıyor.
- Son 10 günde iki tarafın içeriği birleştirilen 80 birleştirmenin 62'si bu dosyalara dokunuyor.

## Karar: satır başına değil, paket başına dosya

Yönetici önerisi satır başına dosyaydı. Paket başına dosya öneriyorum, çünkü sıra anlam taşıyor:

- `pack_approval.py` başlığı: "the last row of a pack decides, a revoke is a row". Satır başına dosyada sıra kaybolur; yeniden
  üretmek yeni bir güvenlik yüzeyi açar (tarih alanı yazar tarafından yazılabilir).
- Sıra yalnız **aynı paket içinde** anlamlı (`approval_rows` satırları pakete süzüyor). Paket başına dosyada bu sıra dosyanın
  kendisidir.
- Satır numarası, `git blame` ile satırı ekleyen commit (`row_commit`) ve yalnız ekleme denetimi (`append_only_problems`) dosya
  başına aynı mantıkla kalır.
- Aynı pakete aynı anda iki PR satır eklerse çakışma görünür kalır. Bu istenen davranıştır: iki onay kararı yarışıyordur.

### Etki ölçümü

Son 10 günde bu dosyalara satır ekleyen 33 birleşmiş PR'da, açık kaldığı süreler örtüşen ve aynı dosyaya yazan **37 çift** var.
Aynı paketin aynı dosyasına yazan örtüşen çift **0**. Örtüşme PR'ın açılış-birleşme aralığıyla ölçüldü, satırın eklendiği anla
değil; yani gerçek çakışma sayısı 37'den az olabilir. Sıfır, geçmiş 10 günün ölçümüdür, gelecek garantisi değildir.

## Yapı

```
execution/approvals/approvals.tsv            donuk (yalnız geçmiş)
execution/approvals/reviews.tsv              donuk (yalnız geçmiş)
execution/approvals/approvals/<parça>.tsv    yeni onay satırları
execution/approvals/reviews/<parça>.tsv      yeni inceleme satırları
execution/approvals/append-only-exceptions.tsv   değişmez (nadir, sürücü kalır)
```

- `<parça>`: satırın hedef yolundan (`pack` / `target`) deterministik üretilir. DCP hedefleri de aynı kuralla parçalanır.
  Ad çıkarımı tek fonksiyonda olur.
- Okuma sırası: önce donuk dosya, sonra parça. Aynı paketin eski satırı her zaman yeni satırdan önce gelir.

## Korunması gereken denetimler

| Risk | Denetim | Sabotaj testi |
|---|---|---|
| Satır yanlış parçada: iptal görünmez olur, onay geçer | Her parçadaki her satırın hedefi parça adıyla eşleşmeli; yoksa okunamaz (fail-closed) | Başka paketin iptalini yanlış parçaya yaz → RET |
| Parça dosyası silindi ya da yeniden adlandırıldı | Tüm geçmişte parça kümesi yalnız büyür; silinen/taşınan dosya yalnız ekleme ihlalidir | Parçayı sil, yeniden adlandır → kırmızı |
| Donuk dosyaya yeni satır | Geçiş commit'inden sonra değişiklik reddedilir | Donuk dosyaya satır ekle → kırmızı |
| Bir parçada bozuk satır | Bugünkü gibi tüm okuma durur | Bozuk satır → RET |
| Satırı ekleyen ya da birleştiren kişi | `row_commit`, `human_step` parça yoluyla aynı sonucu vermeli | Yazarın kendi PR'ını birleştirmesi → RET |
| Eski istisna satırları | `exception_why` eski yollar için çalışmaya devam eder | Mevcut istisna hâlâ tanınır |
| Workflow tetiklenmesi | `approvals-append-only.yml` `execution/approvals/**` zaten kapsıyor; PR'daki kırmızı kontrol edilir | Parça değişikliği workflow'u koşturur |

## Değişecek yerler

- `scripts/pack_approval.py`: `approval_rows`, `review_rows`, `make_row`/yazma yardımcıları, `append_only_problems`, `history`.
  Testten önceki kısımda 18 atıf, öz testlerde 94.
- `scripts/prompt_gate.py`: 11 + 23 atıf; "reviews.tsv satır N" adlandırması parça yolu + satır biçimine geçer.
- `scripts/acceptance_record.py`: 0 + 5 atıf, yalnız öz test.
- `scripts/merge_append_only.py` ve `.gitattributes`: parçalar için de sürücü (aynı paket satırları eklenirse yerelde çözer).
- `control-tower-operating-card.md`, `control-tower-sop.md`, `.antigravity` altındaki 4 belge: dosya yolu atıfları.
  **`.antigravity/**` sahip onayı ister.** Anlamı değişen kural yoktur; dosya yolu değişir.

## Geçiş (tek seferlik)

1. Açık onay PR'ları birleşir ya da sahibi onlara geçişten sonra rebase edeceğini bilir.
2. Tek PR: araçlar iki yeri de okur, eski dosyalar dondurulur, yeni satır parçaya yazılır. Eski satır taşınmaz.
3. Geçişten sonra eski dosyaya satır ekleyen açık PR kırmızı olur; o PR yalnız kendi satırını parçaya taşır.
4. Geri dönüş: geçiş PR'ı geri alınırsa parçalardaki satırlar görünmez olur. Bu yüzden geri dönüş, yalnız parçaya henüz satır
   yazılmadıysa güvenlidir; satır yazıldıktan sonra ileri düzeltme yapılır.

## Bilinmeyenler (uygulamadan önce ölçülecek)

- `history` bugün ana dalda yaklaşık 68 saniye sürdü. Parça sayısı arttıkça süre ölçülecek; süre belirgin artarsa önbellek gerekir.
- GitHub'ın "dismiss stale approvals" davranışı: çakışma kalkınca Update branch ihtiyacı azalır, ama onay düşmesinin kaç tanesinin
  bu nedenden geldiği ölçülmedi.
- CRLF kullanan bilgisayarlarda parça dosyası biçimi (`.gitattributes`).
- Öz testlerin (yaklaşık 120 atıf) ne kadarının ortak yardımcıyla sadeleşeceği; iş büyüklüğü bu yüzden kesin değil.

## Ne çözmez

- Diğer ortak dosyalar (kuyruk, kart, sık bulgu listesi) kalır; son 10 günün 80 içerik birleştirmesinin 18'i bu gruptan.
- Ana daldaki kırmızı testler ayrı bir sorundur.
- Kural, onaylayıcı ya da inceleme şartı değişmez.
