# Şablon — sahibe rapor (hat → kişi, her dilim sonunda ve her biten adımdan sonra)

Kim yazar: hat (hat kartı H27). Kule tek bir modül için sahibe yazarken aynı biçimi kullanır (`ozet-satiri.md`). Kural: kısa,
sade Türkçe; kod ya da kısaltma tek başına yazılmaz, ilk geçtiği yerde anlamı söylenir (örnek: "PR — değişikliğin gözden
geçirilmesi için açılan istek", "CI — otomatik kontroller"); kişiden aynı anda tek şey istenir.

Süre, token ve dokunuş sayıları elle yazılmaz (çekirdek kartın Ç29'u). `Bu adım` satırındaki sayılar
`scripts/ees_run_record.py` aracından gelir; satır aracın özet bölümündeki "Rapor satırı"dır, aynen alınır:

```
python3 scripts/ees_run_record.py --transcript <bu oturumun dökümü (.jsonl)> --since <adımın başladığı an, UTC>
```

Döküm `~/.claude/projects/<proje klasörü>/<oturum kimliği>.jsonl` dosyasıdır (Windows: `%USERPROFILE%\.claude\projects\...`);
hattın ve kulenin dökümleri ayrı verilir (`--transcript` birden çok kez yazılabilir). Araç yoksa ya da dökümü bulamazsa satır
"Bu adım: ölçülemedi (<neden>)" yazılır; sayı uydurulmaz.

## Biçim

```
**<Modül adı> (MOD-nnnn) · <dilim ya da adım>**
Ne için: <bu işin kullanıcıya faydası, tek cümle>
İlerleme: ▓▓▓▓░░░░ 4/8 adım · tahmini bitiş: <gün ya da "bugün">
## Biten
<biten işler, sade cümlelerle>
Bu adım: <iş süresi> iş + <bekleme süresi> bekleme · <token> · <dokunuş sayısı> dokunuş
## Çalışan
<şu an kimin ne yaptığı; yoksa "Şu an çalışan yok.">
## Sırada
<bir sonraki adım, tek cümle>
## Sizden istenen
<tek şey, tek bağlantı; yoksa "Şu an sizden bir şey gerekmiyor.">
```

- **Modül adı (MOD-nnnn):** sade ad ve modül kimliği; kimliğin yanında adı her zaman yazılır.
- **Ne için:** teknik değil, kullanıcıya faydası ("Müdür, ekibinin haftalık saatlerini tek ekranda onaylar").
- **İlerleme:** çubuk her zaman 8 kutu; dolu kutu sayısı = biten adım / toplam adım × 8 (yuvarlanır). Adım, hattın dilim
  planındaki adımdır. Tahmini bitiş, bitmiş adımların ölçülen süresinden çıkar; henüz ölçü yoksa "bilinmiyor, ilk adımdan sonra
  söylenir" yazılır, tahmin uydurulmaz.
- **Bu adım:** iş süresi = beklemesiz çalışma; bekleme süresi = izin, uzun komut ya da kişi beklemesi; token = o adımda işlenen
  toplam (ana sohbet ve alt ajanlar; önbellekten okunan kısım dahil); dokunuş = o adımda kişinin yazdığı mesaj sayısı.
- **Sizden istenen:** tek şey (kişiden aynı anda birden çok şey istenmez). Onay ise "kabulü onaylıyorum" gibi yazılacak cümle
  ve bağlantı verilir.

İsteyen için raporun altına, ayrı bir "Ayrıntı" bloğu eklenebilir (kişi okumak zorunda değildir):

```
Ayrıntı:
PR: <bağlantı> · CI ilk denemede <yeşil | kırmızı>
Testler: kabul ölçütü <bağlı>/<toplam> teste bağlı · test <geçen>/<toplam>
Belgeler: <var>/<zorunlu> (modül sonunda tamamlanacaklar: <liste> | yok)
Ölçüler: <geçen> iş günü (hedef ≤5) · <n> dokunuş (hedef ≤3) · <token> (hepsi araçtan)
Denetim: <UYGUN | KOŞULLU | UYGUN DEĞİL> · tur <1|2> · açık bulgu: <sayı ve önem> | yok
```

Örnek (uydurma değerler):

```
**Haftalık zaman çizelgesi (MOD-0280) · dilim 2/4: onay ekranı**
Ne için: Müdür, ekibinin haftalık çalışma saatlerini tek ekranda görüp onaylar.
İlerleme: ▓▓▓▓░░░░ 4/8 adım · tahmini bitiş: Cuma
## Biten
Onay ekranı yazıldı, testleri geçti ve değişiklik gözden geçirilmek üzere açıldı (PR).
Bu adım: 38 dk iş + 2 sa 10 dk bekleme · 1,8 milyon token · 1 dokunuş
## Çalışan
Bağımsız denetçi değişikliği okuyor.
## Sırada
Denetim sonucuna göre düzeltme, sonra dilim 3: raporlama ekranı.
## Sizden istenen
Şu an sizden bir şey gerekmiyor.
```
