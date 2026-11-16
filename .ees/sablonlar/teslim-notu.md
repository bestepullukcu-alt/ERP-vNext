# Şablon — işçi teslim notu (işçi → hat, 5 satır)

Kim yazar: işçi, işi bitirince ya da durunca (işçi kartı İ10). 2, 3 ve 5. satırları çalıştırma kaydı gelince araç doldurur;
işçi yalnız 1. satırın durumunu ve 4. satırı yazar. Beş satırı geçmez.

```
1. İş <n>/<toplam> "<işin kısa adı>" — bitti | engel | durduruldu
2. <dal> @ <kısa commit> · <dosya sayısı> dosya · +<eklenen>/−<silinen>
3. Kanıt: <koşulan komut> → <sonuç>; canlı deneme: <ne denendi, sonuç> | yapılmadı (neden)
4. Açık nokta: <tek cümle> | yok
5. <süre> dk · ~<token> bin token · <deneme sayısı> deneme · seviye <üst|standart|hafif> · kartlar @<commit>
```

Ayrıntılı kanıt (sabotaj denemeleri, komut çıktıları) bu notta değil, çalıştırma kaydında ve PR'ın test tablosundadır.

Örnek (uydurma değerler, kişi adı yok):

```
1. İş 2/3 "haftalık özet uç noktası" — bitti
2. feature/hcm/mod-0280-fu01-dl2-haftalik @ 3f2a9c1 · 6 dosya · +214/−12
3. Kanıt: dotnet test …HcmService.Tests → 48/48; GET /api/v1/time-entries/weekly kiracı A 200, kiracı B 404
4. Açık nokta: tatil günü kuralı pakette yok; uygulamadım, hatta sordum
5. 41 dk · ~62 bin token · 1 deneme · seviye standart · kartlar @a1b2c3d
```
