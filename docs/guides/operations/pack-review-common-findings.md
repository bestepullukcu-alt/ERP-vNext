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
- Kabul ölçütü bugünkü sözleşmeden farklı bir cevap istiyor (ör. "bulunamadı" hatası) ama kod başka bir cevap veriyor
  (ör. boş liste) ve paket bunu bir değişiklik olarak yazmıyor. Her beklenen cevap bugünkü kodla karşılaştırılır; farklıysa
  "değişir" diye ve izinli yoluyla yazılır. (MOD-0023 2026-10-29-3e802d2f F-1)
- Mevcut bir test, iddia edilen durumu zorlamadığı halde kanıt diye gösteriliyor (ör. yarış testi, istekleri gerçekten
  aynı noktada tutmuyor). Testin tam olarak neyi zorladığı kodda okunur; zorlamıyorsa "destekleyici" denir ve ölçüt
  yeni testle yazılır. (MOD-0023 2026-10-30-5512cbca F-3)

## 3. Ölçülemeyen kabul ölçütü

- "Kayıt tamamlanınca", "gerektiğinde" gibi tetikleyiciler; ne zaman ve hangi olayla olduğu yazmalı. (MOD-0230 2026-10-22-31f02845)
- Özel ve sınır değerlerin kuralı yok ya da iki anlamlı: "bilinmiyor" değeri, liste geldikten sonraki doğrulama,
  sayfa boyutu üst sınırı ve sayfa eşlemesi. (MOD-0230 2026-10-22-23c49893 F-2/F-3, 2026-10-27-a51f2349)
- Birlikte çalışması gereken iki davranış (ör. bağı kaldırma + otomatik eşleştirme) için ortak kabul ölçütü yok.
  (MOD-0230 2026-10-22-94cbb2d5)
- Denetim (audit) tablosunda zorunlu okuma denetimi eksik: hassas bir kaydın tekil açılması da denetlenir
  (`audit-trail-standard` §7). (MOD-0230 2026-10-21-b48500cf)
- Eşzamanlılık ve tekrar ölçütleri zamanlamadan bağımsızmış gibi tek cevapla yazılmış. Her durum ayrı yazılır (ilk
  okumada bulunan tekrar, geç gelen, işlem içi örtüşme), her biri için testin onu nasıl zorladığı ve kaç kayıt yazıldığı
  belirtilir. (MOD-0023 2026-10-29-3e802d2f F-2)
- Bir dalın ardından gelen denemenin beklentisi, o dalın kayıt yazıp yazmadığına bakılmadan yazılmış (hiç yazılmamış
  denemeden sonraki istek "tekrar" değil, ilk yazmadır). (MOD-0023 2026-10-30-5512cbca F-2)
- Eşitlik ya da "aynı istek mi" kuralı, birleştirilmiş ya da kayıplı bir metin üzerinden kuruluyor; iki farklı istek aynı
  metni üretebiliyor. Karşılaştırılan her alan ayrı saklanır ve ayrı karşılaştırılır; eski kayıtlar için davranış
  yazılır. (MOD-0023 2026-10-30-5512cbca F-1)

## 4. Araç biçimi

- Kabul ölçütü tablosunun devam satırları ya da hücre içi `|` karakteri kabul aracı tarafından okunamıyor; göndermeden
  önce `python3 scripts/acceptance_record.py draft` koşulur ve her ölçüt satırı görünür. (MOD-0230 2026-10-23-96424971)

## 5. İnceleme girdisi (CT)

- İncelemeye giden ZIP'te paketin dayandığı kaynak dosyalar eksik (entity, mevcut testler, tüketici istemcileri); inceleyen
  iddiaları ölçemiyor. CT paketteki her dosya atfını ZIP'le karşılaştırır. (MOD-0023 2026-10-29-3e802d2f F-3)
