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
- Veritabanı kısıtının ya da indeksinin (tekil, seyrek, kısmi) kapsamı gerçek davranışla ölçülmemiş: sürücünün yazdığı
  `null` "alan yok" sayılmaz; "yalnız değeri olanlar" için gerçek değere bakan bir süzgeç gerekir. (MOD-0357
  2026-10-29-5383fd43 F-33)
- Bir kayda (denetim satırı, olay) yazılacak kimlik ya da değer, onu üreten adımdan önce sabitleniyor; sonradan bulunan
  değer (tekrar çağrısı, yeni kayıt) oraya yazılamaz. (MOD-0357 2026-10-29-0e730d97 F-55)
- Kabul ölçütü bugünkü sözleşmeden farklı bir cevap istiyor (ör. "bulunamadı" hatası) ama kod başka bir cevap veriyor
  (ör. boş liste) ve paket bunu bir değişiklik olarak yazmıyor. Her beklenen cevap bugünkü kodla karşılaştırılır; farklıysa
  "değişir" diye ve izinli yoluyla yazılır. (MOD-0023 2026-10-29-3e802d2f F-1)
- Mevcut bir test, iddia edilen durumu zorlamadığı halde kanıt diye gösteriliyor (ör. yarış testi, istekleri gerçekten
  aynı noktada tutmuyor). Testin tam olarak neyi zorladığı kodda okunur; zorlamıyorsa "destekleyici" denir ve ölçüt
  yeni testle yazılır. (MOD-0023 2026-10-30-5512cbca F-3)
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
  (`audit-trail-standard` §7). (MOD-0230 2026-10-21-b48500cf; MOD-0357 2026-10-29-5383fd43 F-37)
- Eşzamanlılık ve tekrar ölçütleri zamanlamadan bağımsızmış gibi tek cevapla yazılmış. Her durum ayrı yazılır (ilk
  okumada bulunan tekrar, geç gelen, işlem içi örtüşme), her biri için testin onu nasıl zorladığı ve kaç kayıt yazıldığı
  belirtilir. (MOD-0023 2026-10-29-3e802d2f F-2)
- Bir dalın ardından gelen denemenin beklentisi, o dalın kayıt yazıp yazmadığına bakılmadan yazılmış (hiç yazılmamış
  denemeden sonraki istek "tekrar" değil, ilk yazmadır). (MOD-0023 2026-10-30-5512cbca F-2)
- Eşitlik ya da "aynı istek mi" kuralı, birleştirilmiş ya da kayıplı bir metin üzerinden kuruluyor; iki farklı istek aynı
  metni üretebiliyor. Karşılaştırılan her alan ayrı saklanır ve ayrı karşılaştırılır; eski kayıtlar için davranış
  yazılır. (MOD-0023 2026-10-30-5512cbca F-1)
- Bir alan "sabit kod listesi" ya da "kapalı enum" diye anılıyor ama değerleri pakette sayılmamış; doğrulama yalnız
  alanın dolu olmasını istiyor. Değerler, üyelik doğrulaması ve geçersiz kodun yanıtı yazılmalı; lookup kararı ile alan
  tanımı aynı listeyi göstermeli. (MOD-0280-FU02 2026-10-29-775ce53c F-76)
- Bir sayı (eşleşmeyen kayıt, hata) raporlanıyor ama hangi kaydın düzeltileceğini gösteren okuma yüzeyi yok.
  (MOD-0280-FU02 2026-10-29-ea61e8c6 F-07)
- Bir dilimin gerekli kanıt seviyesi işin riskinden düşük (ör. izin anahtarı ve gateway rotası yalnız smoke ile).
  (MOD-0280-FU02 2026-10-29-ea61e8c6 F-04)

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

- Durum × komut tablosu yok: her durumda her komutun sonucu yazılmamış; deneme, kira ve yeniden gönderim akışları koda
  bırakılmış. (MOD-0183 2026-10-23-febf6e35 F-1; MOD-0186 2026-10-26-5c7be454 F-1; 3 ENGEL kod incelemesinde çıktı, MVP-6
  değerlendirmesi 2026-10-30)
- Paket, sözleşme ve ekran aynı alanı farklı anlatıyor; onaydan önce üçü karşılaştırılmalı. (`toLocationId`, MVP-6
  değerlendirmesi 2026-10-30)

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

## 6. İnceleme girdisi (CT)

- İncelemeye giden ZIP'te paketin dayandığı kaynak dosyalar eksik (entity, mevcut testler, tüketici istemcileri); inceleyen
  iddiaları ölçemiyor. CT paketteki her dosya atfını ZIP'le karşılaştırır. (MOD-0023 2026-10-29-3e802d2f F-3)
