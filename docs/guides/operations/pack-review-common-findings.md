# Paket incelemesinde sık bulgular — paket yazarı için kontrol listesi (S-93)

Bu liste **paket yazarı** içindir: paketi incelemeye göndermeden önce her maddeyi kendi paketinde arayın. Ön inceleyen
alt ajana "en azından bunlara bak; yalnız bunlara bakma" satırıyla verilir (S-99, kart §1.1/4); Codex'e **verilmez**
(resmî inceleme ipucusuz kalır). Yeni madde yalnız Codex'in ENGEL ya da ORTA bulgusundan, **tür
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
- Veritabanı kısıtının ya da indeksinin (tekil, seyrek, kısmi) kapsamı gerçek davranışla ölçülmemiş: sürücünün yazdığı
  `null` "alan yok" sayılmaz; "yalnız değeri olanlar" için gerçek değere bakan bir süzgeç gerekir. (MOD-0357
  2026-10-29-5383fd43 F-33)
- Bir kayda (denetim satırı, olay) yazılacak kimlik ya da değer, onu üreten adımdan önce sabitleniyor; sonradan bulunan
  değer (tekrar çağrısı, yeni kayıt) oraya yazılamaz. (MOD-0357 2026-10-29-0e730d97 F-55)

## 3. Ölçülemeyen kabul ölçütü

- "Kayıt tamamlanınca", "gerektiğinde" gibi tetikleyiciler; ne zaman ve hangi olayla olduğu yazmalı. (MOD-0230 2026-10-22-31f02845)
- Özel ve sınır değerlerin kuralı yok ya da iki anlamlı: "bilinmiyor" değeri, liste geldikten sonraki doğrulama,
  sayfa boyutu üst sınırı ve sayfa eşlemesi. (MOD-0230 2026-10-22-23c49893 F-2/F-3, 2026-10-27-a51f2349)
- Birlikte çalışması gereken iki davranış (ör. bağı kaldırma + otomatik eşleştirme) için ortak kabul ölçütü yok.
  (MOD-0230 2026-10-22-94cbb2d5)
- Denetim (audit) tablosunda zorunlu okuma denetimi eksik: hassas bir kaydın tekil açılması da denetlenir
  (`audit-trail-standard` §7). (MOD-0230 2026-10-21-b48500cf; MOD-0357 2026-10-29-5383fd43 F-37)

## 3a. Eşzamanlılık, tekrar ve yarıda kalan iş

- Bir değişmez ("hiç geri inmez", "düzenleyen hep katılımcı") yalnız bazı yazma yollarında korunuyor ya da değişebilen
  bir kayıttan türetiliyor: onu bozabilecek her yol (kaynağın geri dönmesi, devir, rol değişimi) sayılmalı. (MOD-0357
  2026-10-29-5383fd43 F-34, F-36)
- Erişim denetimi, kararın verildiği andaki güncel duruma göre yapılmıyor: tekrar (idempotency) erken dönüşü ilk çağrının
  denetimini atlıyor ya da işlemden önceki denetim işlem içinde her denemede yinelenmiyor. (MOD-0357 2026-10-29-5383fd43
  F-35; 2026-10-29-ee542bf9 F-66)
- Eşzamanlı yazımı kaçıran kapı ya da toplu düzeltme: iki yol ortak bir belgeye (sürüm, sayaç) yazmıyorsa çakışma
  görünmez; anlık görüntüden sonra gelen ekleme ve üst kaydın sürümünü artırmayan alt yazım kaçar. (MOD-0357
  2026-10-29-0e730d97 F-54, F-57)
- Çok adımlı yazımda "tamamlandı" işareti (bağ, durum) ilk yazılıyor: yarıda kalan iş kalıcı eksik kalır. İşaret en son
  yazılır; kesinti testi her ara noktada koşar. (MOD-0357 2026-10-29-0e730d97 F-56; 2026-10-29-ee542bf9 F-65, F-67)

## 4. Araç biçimi

- Kabul ölçütü tablosunun devam satırları ya da hücre içi `|` karakteri kabul aracı tarafından okunamıyor; göndermeden
  önce `python3 scripts/acceptance_record.py draft` koşulur ve her ölçüt satırı görünür. (MOD-0230 2026-10-23-96424971)
