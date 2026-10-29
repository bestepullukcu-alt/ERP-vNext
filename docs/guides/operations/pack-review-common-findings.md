# Paket incelemesinde sık bulgular — paket yazarı için kontrol listesi (S-93)

Bu liste **paket yazarı** içindir: paketi incelemeye göndermeden önce her maddeyi kendi paketinde arayın. Ön inceleyene
ve Codex'e **verilmez** (bağımsız incelemede ipucu olur). Yeni madde yalnız Codex'in ENGEL ya da ORTA bulgusundan, **tür
olarak** eklenir; kaynak inceleme dosyası parantez içinde yazılır.

## 1. Paket kendisiyle çelişiyor

- Bir satır bir sözleşmeyi, bağımlılığı ya da kanıtı "kapalı / onaylı / var" gösterirken başka bir satır onu boş, eksik ya
  da açık gösteriyor. Her "kapalı" iddiasının kanıt satırı dolu olmalı; aynı konu iki yerde geçiyorsa ikisi aynı şeyi
  söylemeli. (MOD-0230 2026-10-21-f1b56737)
- Ortam iddiaları (yerel / dev / CI, Gateway, UI çalışıyor) paketin farklı bölümlerinde farklı. (MOD-0230 2026-10-21-f1b56737)
- Alan ya da yüzey sayısı başlıktaki hesapla tutmuyor. (MOD-0230 2026-10-22-23c49893)

## 2. Kodla tutmayan iddia

- İzin anahtarı ailesi, sınıf, metot ya da uç nokta adı gerçek koddakiyle aynı değil; "var" denen metot yok ya da
  farklı davranıyor. Her ad `grep` ile kodda ölçülür; ölçülemiyorsa "yazılacak" denir. (MOD-0230 2026-10-21-f1b56737, 2026-10-21-b48500cf)
- Yetkilendirilen yollar (Repo Scope) ile işin dokunacağı yollar tutmuyor. (MOD-0230 2026-10-21-f1b56737)

## 3. Ölçülemeyen kabul ölçütü

- "Kayıt tamamlanınca", "gerektiğinde" gibi tetikleyiciler; ne zaman ve hangi olayla olduğu yazmalı. (MOD-0230 2026-10-22-31f02845)
- Sınır değerler (sayfa boyutu üst sınırı, "bilinmiyor" değeri, boş liste) ve birlikte kabul edilmesi gereken iki
  davranış ayrı ayrı ölçütte. (MOD-0230 2026-10-22-23c49893, 2026-10-22-94cbb2d5, 2026-10-27-a51f2349)
- Denetim (audit) kaydı yalnız bazı yazma komutlarını kapsıyor; her yazma işlemi listelenmeli. (MOD-0230 2026-10-21-b48500cf)

## 4. Araç biçimi

- Kabul ölçütü tablosunun devam satırları ya da hücre içi `|` karakteri kabul aracı tarafından okunamıyor; göndermeden
  önce `python3 scripts/acceptance_record.py draft` koşulur ve her ölçüt satırı görünür. (MOD-0230 2026-10-23-96424971)
