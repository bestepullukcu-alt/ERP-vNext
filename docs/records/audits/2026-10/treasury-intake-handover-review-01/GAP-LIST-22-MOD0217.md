---
record_id: TREASURY-GAP-LIST-22
kind: sahip denetimi (CT-APR-02 değildir) — MOD-0217 taslak pack'i, bulgular ve kapanışlar
received_at_local: 2026-10-26
received_from: depo sahibi (K1), sohbette yapıştırılan denetim ("KOŞULLU — taslak iyi ilerlemiş, fakat onay öncesi aşağıdaki boşluklar düzeltilmeli")
reviewed_input: ZIP MOD-0217-draft-ac647ac37.zip (manifest 6/6); MOD-0217 pack sha256 5d4ab7a8…; 48 AC tekil; slices() DL-1…DL-4
fixed_in: bu kaydın commit'i (dal chore/ct/treasury-intake-ct-02) — pack Revizyon 2
previous: GAP-LIST-21-PREP.md
---

# Gap listesi 22 ve kapanışları

Denetimin doğruladıkları: ZIP sağlam; AC'ler tekil; dilim tablosu okunuyor; kaynak test sorunları doğru aktarılmış (yeşil kanıt
sayılmıyor; gerçek replica set ve yalıtılmış test veritabanları isteniyor). O-1…O-15'in açık gösterilmesi hata değildir.

| # | Öncelik | Gap (satırlar denetlenen sürüme göre) | Kapanış (Revizyon 2) |
|---|---|---|---|
| 1 | P1 | 448–452: işaretçi yazımı başarısızsa doğrudan `compensate` — yazma olmuş, yalnız yanıt kaybolmuş olabilir | Kesin rollback ile sonucu bilinmeyen commit ayrıldı; belirsizde önce işaretçi okunur, yalnız bağlanmadığı kanıtlanan dosya telafi edilir; okuma yoksa `Unknown`; yanıt kaybı testi + sabotaj |
| 2 | P1 | 459–462: iş akışı sonucu nesneye göre bulunup `Approved` yapılıyor; doğru onay denemesine bağlı değil | Senkron yalnız geçerli denemenin örnek kimliği ve anahtarıyla eşleşen sonucu kabul eder; eski deneme, başka nesne, birden çok eşleşme → değişiklik yok; testler |
| 3 | P1 | 454–458: başlatma başarısızlığında kesin ret / geçici / belirsiz ayrımı yok | Üç sınıflı tablo; kesin ret → güvenli geri dönüş + gerekçe kodu + denetim; geçici / belirsiz → aynı anahtarla uzlaştırma; yeniden başlatmadan önce yetki, şirket, SoD; zaman aşımı ve geçiş çatışması asla kesin ret değil; AC + sabotaj |
| 4 | P2 | 1062–1071: ortak AC'ler (AC-5, AC-11, AC-16) DL-1'de tümüyle kapanıyor | "(bu dilimin eylemleri / … kısmı)" işaretleriyle dilimlere dağıtıldı; son listelendiği dilimde kapanır kuralı |
| 5 | P2 | 259–260, 588, 793–799: imzaya bağlı belgenin geri çekilmesi yasak değil | İmzaya bağlı belge referansı değişmez (geri çekme / değiştirme → 409); son imzada bütün imza referansları aynı işlemde yeniden doğrulanır; yarış testi + sabotaj |
| 6 | P2 | 898–915 ile 1066 çelişkisi (tüm maddeler ready-for-dev öncesi mi, dilim bazında mı) | Tek kural: pack düzeyi kapılar ready-for-dev öncesi; yalnız sonraki dilimin ihtiyacı "— DL-n için" işaretli ve o dilimin önkoşulu (O-2 → DL-2) |
| 7 | küçük | AC-4 "tek niyet" — işlem tablosu 435 iki denetim niyeti | Beklenen niyet / olay sayısı tekilleştirildi |

## 2. tur (2026-10-26) — sahip denetimi "KOŞULLU", 7 bulgunun 6'sı kapalı

Girdi: ZIP `MOD-0217-rev2-35d913e87.zip` (manifest 2/2); 57 AC tekil; `slices()` DL-1…DL-4.

| # | Öncelik | Gap | Kapanış (Revizyon 3) |
|---|---|---|---|
| 8 | P1 | Negatif okuma hâlâ kesin bağsızlık sayılıyor (479, 482–487): çoğunluk okuması geç commit'i dışlamaz | İşaretçinin bulunması bağlı sayılır; bulunmaması **tek başına telafi izni değildir**. Sıra: aynı oturumla commit yeniden denemesi → çoğunluk okuması → `Version = V`'ye koşullu, çoğunluk onaylı **çit yazımı** (`Version = V+1`, `AbandonedContentIds += contentId`); telafi yalnız çit uygulanınca. Çit eşleşmezse yeniden okuma; işaretçi yoksa bilinmiyor, telafi yok. Reconcile aynı kurala bağlandı. AC-58 geç commit testi + sabotaj; `AbandonedContentIds` alanı Entity Fields'a |
| 9 | P2 | İş akışı başlatma sınıfları çakışıyor (502–503, AC-54): genel 5xx / 503 hem geçici hem bilinmiyor | Öncelik sırası: (1) tanınan MOD-0023 kodu → (2) gönderilmedi / 401 → geçici → (3) gönderildi ve kodsuz 5xx / 503 / tanınmayan 4xx / gönderim sonrası zaman aşımı → bilinmiyor. Tablo ve AC-54 bu sıraya göre yeniden yazıldı (tablo testi: aynı yanıt iki sınıfa düşmez) |

Teknik dayanak (denetim): MongoDB `majority` okuması çoğunlukça onaylanmış görünümü döndürür; belirsiz commit'in gerçekleşmediğini
tek başına kanıtlamaz.

Bu sahip denetimi resmî CT-APR-02 kaydı değildir. Belge incelemesidir; build / test / runtime yok.
