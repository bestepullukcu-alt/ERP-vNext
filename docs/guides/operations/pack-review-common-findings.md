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
- Durum tablosu ile kabul ölçütü aynı (durum, olay) çifti için farklı sonuç veriyor; bir tablo satırına istisna
  eklenince ("niyet iptal ise …") o durumu anan her AC ve başarısızlık yolu da güncellenmeli. (MOD-0280-FU02
  2026-10-29-878a24f0)
- Denetim yükü kuralı ile denetim tablosu çelişiyor: kayıt "yalnız şu alanlar" derken başka satır o listede olmayan
  alanları zorunlu kılıyor; iş alanları ile teknik zarf (kimlik, durum kodu) ayrı tanımlanmalı. (MOD-0280-FU02
  2026-10-29-ea61e8c6 F-01, 2026-10-29-47df0fc4 F-09, 2026-10-29-436bc38b F-18)
- Bir kural (ör. "karar varsa iptal reddedilir") genel yazılmış, oysa yalnız bir yol için geçerli; başka bir AC'nin izin
  verdiği yolu (ör. onaylı izni iptal) kapatıyor. (MOD-0280-FU02 2026-10-29-436bc38b F-19)
- Bir taban kuralı (ör. hak + devir ≥ kullanılan + ayrılan) ile AC farklı eşik kullanıyor; AC'de sayısal bir örnek
  yazılmalı. (MOD-0280-FU02 2026-10-29-47df0fc4 F-14)
- Hazırlık listesi bir koşulu ready-for-dev önkoşulu sayarken dilim tablosu aynı koşulu yalnız bir dilimin kabul kapısı
  sayıyor. (MOD-0280-FU02 2026-10-29-ea61e8c6 F-06, 2026-10-29-47df0fc4 F-15)

## 2. Kodla tutmayan iddia

- İzin anahtarı ailesi, sınıf, metot ya da uç nokta adı gerçek koddakiyle aynı değil; "var" denen metot yok ya da
  farklı davranıyor. Her ad `grep` ile kodda ölçülür; ölçülemiyorsa "yazılacak" denir. (MOD-0230 2026-10-21-f1b56737, 2026-10-21-b48500cf)
- Yetkilendirilen yollar (Repo Scope) ile işin dokunacağı yollar tutmuyor. (MOD-0230 2026-10-21-f1b56737)
- Bağımlı modülün davranışı hakkında bir sınır iddiası ("X değişince reddeder", "Y'yi garanti eder") o modülün kodunda
  ölçülmemiş; iddia dosya ve satırla yazılır, ölçülemiyorsa "bilinmiyor" denir. (MOD-0280-FU02 2026-10-29-948f931b F-21,
  2026-10-29-47df0fc4 F-16)
- Yeniden deneme ya da arka plan işi, bağımlı modülün kullanıcı kimliği istediği bir komutu çağırıyor; işin hangi
  kimlikle çalışacağı yazılmamış. (MOD-0280-FU02 2026-10-29-47df0fc4 F-11)
- Bir yetki "satır kapsamı" (atama, sahiplik) ile sağlanmış gösteriliyor; eylem için izin anahtarı ayrıca adlandırılmalı,
  okuma için ayrı anahtar olmalı. (MOD-0280-FU02 2026-10-29-ea61e8c6 F-03, 2026-10-29-47df0fc4 F-13)

## 3. Ölçülemeyen kabul ölçütü

- "Kayıt tamamlanınca", "gerektiğinde" gibi tetikleyiciler; ne zaman ve hangi olayla olduğu yazmalı. (MOD-0230 2026-10-22-31f02845)
- Özel ve sınır değerlerin kuralı yok ya da iki anlamlı: "bilinmiyor" değeri, liste geldikten sonraki doğrulama,
  sayfa boyutu üst sınırı ve sayfa eşlemesi. (MOD-0230 2026-10-22-23c49893 F-2/F-3, 2026-10-27-a51f2349)
- Birlikte çalışması gereken iki davranış (ör. bağı kaldırma + otomatik eşleştirme) için ortak kabul ölçütü yok.
  (MOD-0230 2026-10-22-94cbb2d5)
- Denetim (audit) tablosunda zorunlu okuma denetimi eksik: hassas bir kaydın tekil açılması da denetlenir
  (`audit-trail-standard` §7). (MOD-0230 2026-10-21-b48500cf)
- Bir alan "sabit kod listesi" ya da "kapalı enum" diye anılıyor ama değerleri pakette sayılmamış; doğrulama yalnız
  alanın dolu olmasını istiyor. Değerler, üyelik doğrulaması ve geçersiz kodun yanıtı yazılmalı; lookup kararı ile alan
  tanımı aynı listeyi göstermeli. (MOD-0280-FU02 2026-10-29-775ce53c F-76)
- Bir sayı (eşleşmeyen kayıt, hata) raporlanıyor ama hangi kaydın düzeltileceğini gösteren okuma yüzeyi yok.
  (MOD-0280-FU02 2026-10-29-ea61e8c6 F-07)
- Bir dilimin gerekli kanıt seviyesi işin riskinden düşük (ör. izin anahtarı ve gateway rotası yalnız smoke ile).
  (MOD-0280-FU02 2026-10-29-ea61e8c6 F-04)

## 5. Dış sistemle yarış, kesinti ve bırakma

- Başka bir modüle yapılan çağrının başarısız yanıtı ya da süreli kilidin dolması "o iş olmadı" kanıtı sayılıyor; yanıt
  kaybolmuş, iş tamamlanmış olabilir. Bırakma (gün iadesi, yeni gönderim) yalnız karşı sistemin yeniden okunan son
  durumuyla yapılır. (MOD-0280-FU02 2026-10-29-ea61e8c6 F-02, 2026-10-29-47df0fc4 F-10, 2026-10-29-436bc38b F-17,
  2026-10-29-5fb4d1bb F-20)
- Birden çok kayda dokunan yazım için işlem bütünlüğü ya da kalıcı telafi tanımlı değil; "her hata geri alınır" iddiası
  kullanılan işlem aracının belirsiz commit sonucunu hesaba katmıyor. (MOD-0280-FU02 2026-10-29-ea61e8c6 F-02, 2026-10-29-47df0fc4 F-16)
- Bir işaret (ör. "yıl devredildi") ile değiştirdiği değerler aynı işlemde yazılmıyor; yarıda kalan koşu tekrarlanınca
  eksik ya da çift iş yapar. (MOD-0280-FU02 2026-10-29-47df0fc4 F-12)
- Değer azaltılınca (hak, kota) mevcut ayrılmış ve kullanılmış miktarla uzlaştırma kuralı yok. (MOD-0280-FU02
  2026-10-29-ea61e8c6 F-05)

## 4. Araç biçimi

- Kabul ölçütü tablosunun devam satırları ya da hücre içi `|` karakteri kabul aracı tarafından okunamıyor; göndermeden
  önce `python3 scripts/acceptance_record.py draft` koşulur ve her ölçüt satırı görünür. (MOD-0230 2026-10-23-96424971)
