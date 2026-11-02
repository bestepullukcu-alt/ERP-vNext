# Planlama hizalaması — açık kararlar

- **Durum:** sahip cevabı bekliyor. Her kararı tek tek cevaplayın: «evet» ya da «hayır» (ya da diğer seçeneğin adı).
- Önerilen seçenek her zaman önce yazılıdır. Bir cevap yalnız kendi satırındaki işi yetkilendirir; kod, commit, PR ya da
  dağıtım izni değildir (PR için ayrıca «PR aç»).
- Teknik ayrıntı ve ölçümler: aynı klasördeki öneri belgesi.

---

### K-1 · Kural dosyalarındaki değişiklikler tek PR'da onaylanıyor mu?

Altı ajan kuralı dosyasında (paket standardı, paket yazarı ajanı, orkestratör, çok modüllü paket standardı, başka modül
kuralı, başvuru belgesi) ve kule çalışma yönteminde/kartında yapılan değişiklikler.

- **Önerilen — evet:** değişiklikler bu dalda kalır, ileride sizin «PR aç» sözünüzle tek PR'a girer. Kural yalnız ana dala
  girince geçerli olur.
- **Hayır:** hangi dosyanın çıkarılacağını söylersiniz; o dosya eski metnine döner.
- *Evet neyi yetkilendirir:* yalnız bu metinlerin PR'a girmesini. Hiçbir paketi, kuyruğu ya da kodu değiştirmez.

### K-2 · Süreç modelleme ve iş kırılım motoru modülleri için yeni bir iş kuyruğu açılsın mı?

Bugün toplantılar kuyruğu yalnız toplantı modülünü kapsıyor ve bu iki modülü açıkça dışarıda bırakıyor; iki modülün
paketi geliştirmeye hazır görünüyor ama hiçbir kuyrukta değil.

- **Önerilen — evet:** kule hazırlığa başlar: önce paket yazarı iki pakete teslim dilimlerini ekler, sonra bağımsız
  inceleme ve sizin onayınız, sonra kuyruk dosyası ve kayıt satırı. Her adımda sizden yalnız zorunlu söz istenir.
- **Hayır:** iki modül raporlarda "kuyruk yok" diye görünmeye devam eder.
- *Evet neyi yetkilendirir:* hazırlık işini (paket dilimi yazımı ve incelemesi). Paket onayı, kuyruk onayı ve kod ayrı
  sözlerdir.

### K-3 · Bu iki modül tek kuyrukta mı olsun?

- **Önerilen — tek kuyruk, tek kule:** süreç modelleme modülü iş kırılım motoruna doğrudan bağımlı (ana katalogda ön koşul)
  ve ikisi aynı çok modüllü pakette arka arkaya duruyor; tek kule sıralamayı tek elde tutar.
- **İki ayrı kuyruk:** paralel çalışma mümkün, ama ikinci kule birincinin bitmesini bekler.
- **Toplantılar kulesine eklemek:** var olan kayıt satırını değiştirmek demektir; o kulenin onaylı kapsamı yeniden onay
  ister. Önerilmez.
- *Evet (tek kuyruk) neyi yetkilendirir:* önerilen kayıt satırı biçimini kullanmayı. Kule adı ilk kuyruk onayında sorulur.

### K-4 · Plan dosyasının dilim ve sahiplik satırları depoya alınsın mı?

Plan bugün depo dışında; kurallarımız depo dışı kaynağı kapı yapmıyor. 1 Kasım CEO kararı da plan kararlarının evini ayrı
tutuyor ve bu depoya commit istemiyor.

- **Önerilen — hayır, şimdilik:** kule plan eşleşmesini yalnız ölçer ve farkı size sorar; dağıtımı plan farkı yüzünden
  durdurmaz (uygulanan metin budur).
- **Evet:** plan tarafı bir dışa aktarma dosyasını depoya koyar, eşleşme kapı olur. Bu, CEO kararıyla çeliştiği için önce
  CEO onayı gerekir.
- *Hayır neyi yetkilendirir:* uygulanan "koşullu" metni olduğu gibi bırakmayı.

### K-5 · Elimizdeki plan sürümü isteğin dayandığı sürümden eski; sahiplik nasıl yazılsın?

Elimizdeki sürümde proje portföyü, iş kırılım motoru, süreç modelleme ve toplantı modülleri araştırma–ruhsat bloğunun;
yönetim bloğu bunları yalnız "kullanıyor". İstek ise bunların yönetim bloğuna geçtiğini söylüyor.

- **Önerilen — güncel plan gelene kadar kayıtlara "eski sürüm: araştırma–ruhsat bloğu sahip; yeni sürüm doğrulanmadı"
  yazılsın ve güncel plan dosyası planlama tarafından istensin.**
- **Hayır — isteği doğru kabul et:** kayıtlara yönetim bloğu sahip diye yazılır; kanıtı olmayan bir sahiplik kaydı olur.
- *Evet neyi yetkilendirir:* yeni kayıt satırlarına bu notu yazmayı ve planlama tarafından güncel dosyayı istemeyi
  (mesajı siz gönderirsiniz).

### K-6 · Orkestratörün "çekirdek dilim" koşulu neyi beklesin?

- **Önerilen — çekirdek dilimin kabul edilmiş olması:** onay bütün paketin bir sürümüne verilir, dilime değil; dilim
  kabul edilir. Çekirdek dilim kabul edilmeden ona dayanan dilim başlatılmaz (uygulanan metin).
- **Yalnız paketin onaylı sürümünde çekirdek dilimin bulunması:** daha gevşek; dilimler aynı anda başlayabilir.
- *Evet (önerilen) neyi yetkilendirir:* uygulanan metni bırakmayı. Toplantı modülünün bugünkü işini etkilemez (kural
  geriye dönük değil).

### K-7 · Yeni dilim kuralları yalnız bundan sonra yazılan dilim tablolarına mı uygulansın?

- **Önerilen — evet, geriye dönük değil:** toplantı modülünde ilk dilim "düzeltmeler"dir ve iş 13. dilimden yürüyor;
  geriye dönük uygulama yürüyen işi durdururdu.
- **Hayır:** var olan bütün paketler yeniden yazılır, yeniden incelenir ve yeniden onaylanır.
- *Evet neyi yetkilendirir:* uygulanan metni bırakmayı.

### K-8 · "Büyük artış" ne zaman ayrı takip paketi olsun?

- **Önerilen — pakete planın yeni bir teslim aşaması ya da yeni bir yetenek kimliği gelince:** ölçülebilir ve plandan
  okunur (uygulanan metin).
- **Dilim sayısı bir eşiği aşınca (örneğin 10):** kolay sayılır ama plana bağlı değil.
- *Evet (önerilen) neyi yetkilendirir:* uygulanan metni bırakmayı.

### K-9 · Ana proje sözleşmesine bu kurala tek cümlelik atıf eklensin mi?

Sözleşme boyut sınırına yakın (uyarı eşiğinin hemen altında); cümle eklemek uyarı eşiğini aşar.

- **Önerilen — hayır, şimdilik:** kural, sözleşmenin zaten yönlendirdiği paket standardında duruyor.
- **Evet:** atıf eklenir ve boyut uyarısı kabul edilir (sınır aşılmaz).
- *Hayır neyi yetkilendirir:* sözleşmeye dokunmamayı.

### K-10 · Kalite yönetim sistemi taslağı ile bugünkü geliştirme kuralları arasındaki çelişki kalite birimine gitsin mi?

Taslak, kabulün yalnız adı belirli bir kişi onaylayınca sayılmasını ve yapay zekâ incelemesinin kalite birimi
değerlendirmesi ve CEO imzası olmadan geçerli olmamasını söylüyor; bugünkü kurallarımız dal onayıyla, alt ajan
incelemesiyle ve kulenin kendi kabul kaydıyla yürüyor.

- **Önerilen — evet, yalnız rapor olarak:** çelişki tablosu kalite birimine ve CEO'ya iletilir; bizim kurallarımız
  karar gelene kadar değişmez.
- **Hayır:** çelişki yalnız bu belgede kalır.
- *Evet neyi yetkilendirir:* raporun iletilmesini (mesajı siz gönderirsiniz). Hiçbir kuralı değiştirmez.
