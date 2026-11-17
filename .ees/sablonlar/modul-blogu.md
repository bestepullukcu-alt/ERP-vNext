# Şablon — sahibe rapor (hat → kişi; her biten adımdan ve her dilimden sonra)

Kim yazar: hat (hat kartı H27). Ne zaman: planın her biten adımından ve her biten dilimden sonra (dilim = modülün onaylı paket
planındaki bir iş parçası; adım = o planın bir adımı). Kule tek bir modül için sahibe yazarken aynı biçimi kullanır
(`ozet-satiri.md`). Kural: kısa, sade Türkçe; kod ya da kısaltma tek başına yazılmaz, ilk geçtiği yerde anlamı söylenir
(örnek: "birleştirme isteği (PR)", "otomatik kontroller (CI)"); kişiden aynı anda tek şey istenir.

Süre, token ve dokunuş sayıları elle yazılmaz (çekirdek kartın Ç29'u). `Bu adım` satırındaki sayılar
`scripts/ees_run_record.py` aracından gelir; satır aracın özet bölümündeki "Rapor satırı"dır, aynen alınır. Çubuk için adım
sayısı plandan gelir (araç planı bilmez) ve `--adim` ile verilir; araç çubuk satırını da yazar:

```
python3 scripts/ees_run_record.py --transcript <bu oturumun dökümü (.jsonl)> --since <adımın başladığı an, UTC> --adim <biten>/<toplam>
```

Döküm `~/.claude/projects/<proje klasörü>/<oturum kimliği>.jsonl` dosyasıdır (Windows: `%USERPROFILE%\.claude\projects\...`);
hattın ve kulenin dökümleri ayrı verilir (`--transcript` birden çok kez yazılabilir). Araç yoksa ya da dökümü bulamazsa satır
"Bu adım: ölçülemedi (<neden>)" yazılır; sayı uydurulmaz. Aracın çıktısında kişinin her mesajıyla başlayan çalışma parçasına
"tur" denir; rapordaki "adım" planın adımıdır, ikisi karıştırılmaz.

## Biçim

```
**<Modül adı> (MOD-nnnn) · <dilim ya da adım>**
Ne için: <bu işin kullanıcıya faydası, tek cümle>
İlerleme: ▓▓▓▓░░░░ 4/8 adım · tahmini bitiş: <gün ya da "bugün">
## Biten
<biten işler, sade cümlelerle>
Bu adım: <iş süresi> iş + <bekleme süresi> bekleme · <token> · <dokunuş sayısı> dokunuş
Uyarı: <araçtan gelen uyarı satırları>
## Çalışan
<şu an kimin ne yaptığı; yoksa "Şu an çalışan yok.">
## Sırada
<bir sonraki adım, tek cümle>
## Sizden istenen
<tek şey, tek bağlantı; yoksa "Şu an sizden bir şey gerekmiyor.">
```

- **Modül adı (MOD-nnnn):** sade ad ve modül kimliği; kimliğin yanında adı her zaman yazılır. Başlık dilimi sayısız adlandırır
  (örnek: "onay ekranı"); tek sayaç çubuktur.
- **Ne için:** teknik değil, kullanıcıya faydası ("Müdür, ekibinin haftalık saatlerini tek ekranda onaylar").
- **İlerleme:** `İlerleme: ▓▓▓▓░░░░ 4/8 adım` kısmı araçtan gelir (`--adim 4/8`; çubuk 8 kutu, dolu kutu = yuvarlanmış
  8 × biten / toplam); adım sayısı plandan gelir. "Tahmini bitiş" ölçülmez, raporu yazanın tahminidir ve öyle etiketlenir
  ("tahmini"); bitmiş adımların ölçülen süresine dayanır, ölçü yoksa "bilinmiyor, ilk adımdan sonra söylenir" yazılır.
- **Bu adım:** iş süresi = beklemesiz çalışma; bekleme süresi = izin, uzun komut ya da kişi beklemesi; token = o adımda işlenen
  toplam (ana sohbet ve alt ajanlar; önbellekten okunan kısım dahil, bu yüzden büyük çıkar); dokunuş = o adımda kişinin yazdığı
  mesaj sayısı.
- **Uyarı:** isteğe bağlı satır; aracın "Uyarılar" bölümündeki satırlar aynen kopyalanır, araç "Uyarı yok." diyorsa satır hiç yazılmaz.
- **Sizden istenen:** tek şey (kişiden aynı anda birden çok şey istenmez). Onay ise "kabulü onaylıyorum" gibi yazılacak cümle
  ve bağlantı verilir.

İsteyen için raporun altına, ayrı bir "Ayrıntı" bloğu eklenebilir (kişi okumak zorunda değildir). Terimler bloğun içinde bir kez
açılır: birleştirme isteği (PR), otomatik kontroller (CI), denetim sonucu (UYGUN = sorun yok, KOŞULLU = şartla uygun, UYGUN DEĞİL).

```
Ayrıntı:
Birleştirme isteği (PR): <bağlantı> · otomatik kontroller (CI) ilk denemede <yeşil | kırmızı>
Testler: kabul ölçütü <bağlı>/<toplam> teste bağlı · test <geçen>/<toplam>
Belgeler: <var>/<zorunlu> (modül sonunda tamamlanacaklar: <liste> | yok)
Ölçüler: <geçen> iş günü (hedef ≤5) · <n> dokunuş (hedef ≤3) · <token> (hepsi araçtan)
Denetim sonucu: <UYGUN = sorun yok | KOŞULLU = şartla uygun | UYGUN DEĞİL> · tur <1|2> · açık bulgu: <sayı ve önem> | yok
```

Örnek (uydurma değerler):

```
**Haftalık zaman çizelgesi (MOD-0280) · onay ekranı**
Ne için: Müdür, ekibinin haftalık çalışma saatlerini tek ekranda görüp onaylar.
İlerleme: ▓▓▓▓░░░░ 4/8 adım · tahmini bitiş: Cuma
## Biten
Onay ekranı yazıldı, testleri geçti ve değişiklik gözden geçirilmek üzere açıldı (birleştirme isteği, PR).
Bu adım: 38 dk iş + 2 sa 10 dk bekleme · 1,8 milyon token · 1 dokunuş
## Çalışan
Bağımsız denetçi değişikliği okuyor.
## Sırada
Denetim sonucuna göre düzeltme, sonra raporlama ekranı.
## Sizden istenen
Şu an sizden bir şey gerekmiyor.
```
