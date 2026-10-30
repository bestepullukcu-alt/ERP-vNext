# PVG üçüncü dalga — bağımsız doğrulama: WP-0230-DL3C

> Control Tower kaydı, 2026-10-27. Bu kayıt kuyrukta `DISPATCHED → VERIFIED` geçişinin kanıtıdır.
> Bu kayıt entegrasyon ya da kabul kaydı değildir. İş, sahibin "tek-pr" kararıyla (2026-10-27) ayrı bir PR açılmadan CT dalına birleştirildi. Üçüncü dalganın tek PR'ı ile `main`'e girecek. `INTEGRATED` geçişi o PR birleştikten sonra yazılır.
> Önceki adımlar:
> - DoR: `docs/records/audits/2026-10/pvg-wave3-dor-02/DOR.md`
> - Dağıtım kapısı raporu: `docs/records/audits/2026-10/pvg-wave3-dispatch-01/PQG-WP-0230-DL3C-v1.0.txt`

## Teslim (kart S-63)

| | |
|---|---|
| İşçi dalı | `feature/pvg/wp-0230-dl3c-r1` |
| İş commit'i | `53247c436` |
| `origin/main` birleştirmesi | `d9821dcc7` (dal birinci ebeveyn; ikinci ebeveyn `500c122ba`, #250 dahil; çakışma yok) |
| CT dalına birleştirme | sahip "tek-pr"; CT dalında merge commit, dal birinci ebeveyn |

## Bağımsız ölçüm: KABUL EDİLEBİLİR

Ölçümü yazardan ayrı bir CT ölçücüsü yaptı. Ölçüm, `d9821dcc7` commit'inin ayrı bir kopyası üzerinde yapıldı.

**Kapsam.** Üç dosya değişti ve üçü de izinli yollarda:
- `PvgServiceApiHost.cs`
- yeni `PvgServiceHostEnvironmentTests.cs`
- `PvgServiceApiHostTests.cs` (yalnız M1)

`Program.cs` farkı boş. `git diff --check` temiz.

**Gizli bilgi taraması (S-27).** E-posta, şifre, anahtar ya da token yok.

**Testler.** Ölçüm sırasında 1 dakikalık yük 3,0–6,4 arasındaydı.

| Koşu | Sonuç |
|---|---|
| Api: varsayılan, `Development`, `ASPNETCORE_URLS=http://127.0.0.1:5999`, `LC_ALL=tr_TR.UTF-8` | 228/228 (dördünde de) |
| Api: boş `ASPNETCORE_ENVIRONMENT`, `dotnet vstest` ile | 228/228 |
| Api: boş `ASPNETCORE_ENVIRONMENT`, `dotnet test --no-build` ile | 228/228 |
| RegPvBase | 209/209 |
| `known_failures` | çıkış 0 |

**Sabotaj.** Her sabotaj adı verilen bir testi kırmızıya çevirdi:

| Sabotaj | Kırmızı olan test |
|---|---|
| Yardımcı eski hâline döndü | H1; boş ortamlı koşuda ayrıca M1 |
| `From`'daki boş ad koşulu kaldırıldı | H3 |
| `IsNullOrEmpty` kullanıldı | H1, H2, H3 boşluk satırları |
| `CurrentCultureIgnoreCase` kullanıldı | H5 (tr-TR) |
| URL sabit yazıldı | H7; `ASPNETCORE_URLS` koşusunda ayrıca H6 |
| URL yapılandırmadan okundu | H7 |

İşçi kendi 12 sabotajını da yaptı; hepsi kırmızı verdi.

**Ölçülen ayrıntı.** `ASPNETCORE_ENVIRONMENT= dotnet test` komutu önce derleme yaparsa boş değeri test sürecine iletmiyor; süreç değişkeni hiç tanımlanmamış görüyor. `--no-build` ya da `dotnet vstest` ile boş dize iletiliyor.

**Gerileme yok.**
- Depoda boş `ASPNETCORE_ENVIRONMENT` kuran bir başlatma profili, appsettings dosyası, CI iş akışı ya da test yok.
- CI ortam adını hiç kurmuyor; bu yüzden değişiklikten önce de sonra da Production çalışıyor.
- Değişen tek davranış, açıkça verilen boş ya da yalnız boşluktan oluşan ad. Bu ad artık Production sayılıyor; K3 kararı bunu istiyor.

**Bulgular.**
- DÜŞÜK, kapandı: işçinin PR gövdesi taslağında üç eksik vardı:
  - boş değerin hangi koşulda düştüğü yanlış anlatılmıştı;
  - imza satırı yoktu;
  - M1'in boş-ortam yarısının ölçülmesiyle ilgili bilinen boşluk eksikti.

  Gövdeyi CT yeniden yazdı.
- BİLGİ: `.claude/launch.json` içindeki "web-verify" girişi 5011 portunu kullanıyor. Bu durum bu işten önce de vardı. PVG'nin varsayılan portu OD-PVG-7 kararını bekliyor.

## CT dalında yeniden ölçüm

İş CT dalına birleştirildi. CT dalı `main` `500c122ba`'yı içeriyor ve `services` altında `main`'den yalnız bu üç dosyayla ayrılıyor. Sonuçlar:

| Süit | Sonuç |
|---|---|
| Api | 228/228 |
| RegPvBase | 209/209 |
| `known_failures` | iki süit için de çıkış 0 |

Ölçüm sırasında 1 dakikalık yük 6,0–7,3 arasındaydı.

## Kanıt seviyesi

Bu iş E2 seviyesinde kanıt üretiyor. DL-3 için gerekli seviye E3. Resmî kabul, canlı kapı açılana kadar açık kalır.
