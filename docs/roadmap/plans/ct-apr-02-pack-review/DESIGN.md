# CT-APR-02 — onaydan önce bağımsız inceleme ve dilimli uygulama (tasarım, r3)

> Durum: **r3 — uygulandı** (`scripts/pack_approval.py`, `scripts/prompt_gate.py`, SOP §20.4,
> `execution/approvals/POLICY-REVIEW-START.md`). r2'den farklar §6'da. Taban: `origin/main@4728cb804`. Bağlam: yöneticinin
> 2026-10-20 isteği; Codex S2 görüşü (KOŞULLU) ve tasarım görüşü (S1/S4/S5 UYGUN DEĞİL, S2/S3 KOŞULLU). Üst sözleşme:
> `docs/roadmap/plans/ct-dev-readiness-approval-01/CONTRACT.md` (CT-APR-01).

## 1. İstek

1. Module Pack ya da Delivery Capability Pack (DCP) yazıldıktan sonra, onaya gitmeden önce bağımsız bir inceleme
   yapılır; inceleyeni (Codex ya da Claude) geliştirici seçer.
2. Bir Module Pack ya da DCP tek seferde, bütün olarak uygulanmaz; iş paketleri (WP) dilim dilim ilerler.

## 2. Ölçülen durum (`4728cb804`)

- `scripts/pack_approval.py` yalnız `execution/domains/<domain>/module-packs/<dosya>.md` yolunu kabul eder (`PACK_PATH`).
  DCP'ler `execution/portfolio/delivery-capability-packs/DCP-*.md` altındadır; kimlikleri YAML ön bilgisindedir.
- Onay satırları `execution/approvals/approvals.tsv`'dedir; klasör CODEOWNERS'tadır.
- `scripts/prompt_gate.py` §17.1 meta alanlarının hiçbirini denetlemez.
- **Paketlerde kararlı kabul kriteri kimliği yok:** 171 Module Pack'in 145'inde `AC-…` kimliği geçmiyor; `## Acceptance
  Criteria` başlığı 26 pakette var ve maddeler numarasız onay kutularıdır. Dilim, AC kimliğiyle çözümlenemez.

## 3. Kural

### 3.1 İnceleme ne zaman zorunlu (beyaz liste, fail-closed)

Kural başlangıç commit'inden sonra eklenen her `approve` satırı inceleme ister; **tek istisna**, onaylı önceki sürüme
göre değişen bütün satırların açık bir muafiyet listesinde olmasıdır:
- `@künye`'de `owner` ya da `reviewer` alanının `TBD` / boştan bir değere doldurulması (var olan sahibin değişmesi
  muaf değildir);
- tarih alanları (`updated`, `last_reviewed`);
- yalnız boşluk değişikliği.

Satır sayısı tetikleyici değildir; değişikliği küçük onaylara bölmek muafiyet kazandırmaz. Zorunluluk ve derinlik
hesaplanan ile beyan edilen riskin yükseğine göre belirlenir. Kural başlangıcından önceki onaylar ve değişmemiş paketler
etkilenmez.

### 3.2 İnceleme kaydı

`execution/approvals/reviews.tsv` (yalnız ekleme; CODEOWNERS'ta):

| sütun | anlamı |
|---|---|
| `target` · `target_sha256` | incelenen belge (kanonik yol) ve sürümü |
| `findings` · `findings_sha256` | bulgu dosyası ve özeti; dosya kontrol edilen ref'te bu özetle bulunmalı |
| `verdict` | `UYGUN` / `KOŞULLU` / `UYGUN DEĞİL` |
| `blockers_open` | açık ENGEL sayısı; araç dosyadan sayar, eşleşmezse geçmez |
| `reviewer` · `model` · `effort` · `reviewer_session` | inceleyen araç, model, effort, oturum kimliği — **beyan** |
| `author` · `author_session` | yazan kişi anahtarı (`K…`) ve oturumu — **beyan** |
| `requested_by` · `reviewed_at` | isteyen hesap, tarih |

Bulgu dosyası `docs/records/reviews/<hedef-kimliği>/<tarih>-<sha8>.md`; her bulgu tek satır:
`F-n | ENGEL / ORTA / DÜŞÜK | bulgu | kapanış | sahip | son tarih`.
- ENGEL kapanışı yalnız `açık` ya da `kabul — onaylayan` olabilir. Pakette yapılan bir düzeltme özeti değiştirir, bu da
  incelemeyi bayatlatır ve yeni inceleme ister; `düzeltildi@commit` kapanış kanıtı sayılmaz.
- `kabul — onaylayan`: riski onay satırındaki onaylayan üstlenir; bu durumda onay **HIGH** sayılır ve CT-APR-01'in yetki
  ve insan adımı denetimi uygulanır (yüksek risk yetkisi, yazandan farklı kişi, başkasının merge'i).
- `KOŞULLU` sonuçta ORTA ve DÜŞÜK koşulların sahibi ve son tarihi yazılır.
- Eksik ya da bozuk bulgu satırı, bilinmeyen seviye ya da kapanış, okunamayan dosya başarı sayılmaz (çıkış 1 ya da 2).

### 3.3 Onaya bağlanma (`pack_approval.py check`)

İnceleme zorunlu bir onay satırı ancak şu durumda geçer:
- aynı `target` + `target_sha256` için en az bir inceleme satırı vardır (paket incelemeden sonra değiştiyse inceleme
  bayattır);
- o sürüm için **bütün** inceleme satırları birlikte değerlendirilir (en kısıtlayıcı birleşim): herhangi birinde
  `UYGUN DEĞİL` varsa ya da herhangi bir bulgu dosyasında kapanışı `açık` ENGEL kalmışsa onay geçmez. Sonradan eklenen
  bir `UYGUN` satırı önceki bir sonucu ezmez; önceki ENGEL ancak `kabul — onaylayan` ile kapanır.
- her bulgu dosyası `findings_sha256` ile aynıdır ve `blockers_open` dosyadan sayılanla eşleşir.

### 3.4 Bağımsızlık

`reviewer`, `reviewer_session`, `author`, `author_session` yalnız beyandır; kayıtta beyan olarak etiketlenir. Yazanın
kendi incelemesini başka araç adıyla yazmasını araç engelleyemez. Kanıt CT-APR-01'in insan adımıdır: kayıt CODEOWNERS
onaylı bir merge ile main'e girer. Bu, insanın kaydı kabul ettiğini gösterir; AI incelemesinin ayrı oturumda yapıldığını
kanıtlamaz. Aynı araçla inceleme yasak değildir (seçim geliştiricinin); kayıtta görünür.

### 3.5 DCP

- `PACK_PATH` iki kökü kabul eder: `execution/domains/<domain>/module-packs/` ve
  `execution/portfolio/delivery-capability-packs/`.
- DCP'de risk bölüm numarasıyla değil, açık bir şemayla hesaplanır: ön bilgi alanları ve tanınan yapısal başlıklar
  (sahiplik, üye modüller, bağımlılıklar, önkoşullar, teslim sırası, mimari kararlar, kapsam, dışlananlar, sözleşmeler,
  kapı ve kabul kriterleri) → HIGH; tanınmayan bir başlık altındaki değişiklik de HIGH. LOW yalnız açıkça sayılan
  bölümlerde (denetim notları, gelecek işler) olur.

### 3.6 Dilimli uygulama

- Kural kapsamındaki her yeni Module Pack ve DCP bir **Teslim dilimleri** tablosu taşır: `DL-1`, `DL-2`, … kimliği,
  kısa ad, kapsadığı bölümler. İnceleme bu tabloyu da değerlendirir. (Bugünkü paketlerde kararlı AC kimliği olmadığı için
  dilim kimliği gereklidir.)
- SOP §17.1'e `Kapsanan dilim` alanı eklenir; WP bir ya da birkaç `DL-n` yazar.
- `prompt_gate.py check`, paketin onayı yeni kurala tabiyse **durdurur**: alan yoksa, dilim kimliği onaylı paket
  sürümünde yoksa ya da bütün dilimler tek WP'de toplanmışsa. Eski kurala tabi (başlangıçtan beri değişmemiş) paketlerde
  yalnız uyarı verir: yürüyen işler bu açık geçiş kuralıyla korunur.
- `tamamı` istisnası yalnız onay satırında yapılandırılmış bir işaretle olur (`note` içinde `whole_pack=<WP-kimliği>`):
  onay o paket sürümüne bağlıdır, onaylayan yetkisi ve insan adımı CT-APR-01'le denetlenir; araç WP kimliğinin prompt'takiyle
  aynı olduğunu da denetler. Herhangi bir karar dosyasının varlığı yetmez.

## 4. Kapsam dışı

- GitHub'da inceleyenin kimliğini ya da AI oturumunun ayrılığını doğrulamak.
- Eski paketlere geriye dönük inceleme ya da dilim tablosu.
- `prompt_gate`'in §17.1'in diğer alanlarını zorunlu denetlemesi.

## 5. Codex görüşüne itirazlar ve yorumlar

- **Sahiplik tetikleyicisi:** `TBD`'den bir değere doldurma muaf tutuldu; var olan sahibin değişmesi tetikler.
- **Dilim çözümlemesi:** AC kimliğiyle çözümlenemez (ölçüm §2); bu yüzden yeni paketlere dilim tablosu şartı eklendi.
- **"Tamamı" kararı:** ayrı bir karar dosyası yerine onay satırının kendisine bağlandı; yetki ve sürüm denetimi mevcut
  araçla yapılır.

## 6. Uygulamada r2'den farklar (r3)

- **UYGUN DEĞİL kesin durdurur:** o sürüm için onaylayan kabulüyle de aşılamaz; paket değişir, yeniden incelenir. Kabul
  (`kabul — onaylayan`) yalnız KOŞULLU sonuçtaki ENGEL içindir; önceki bir dosyadaki ENGEL'e `F-n@<özet8>` ile başvurulur.
- **İnceleme satırı yalnız main'den sayılır:** satır, ekleyenden başka birinin merge ettiği bir merge commit'iyle main'e
  gelmiş olmalıdır. Aksi halde çalışan dal kendi kabul satırını ekleyip dağıtım kapısını geçebilirdi.
- **Eski paketin ilk onayı:** hiç onay almamış bir paket, kuralın başladığı andaki sürümüyle (Module Pack için
  POLICY-START, DCP için POLICY-REVIEW-START) karşılaştırılır. CT-APR-01'in ilk onayı zorunlu HIGH saymasından gelen risk bu
  kararda sayılmaz; böylece eski bir pakette yalnız sahip alanını doldurmak inceleme ve dilim tablosu istemez.
- **Dilim bağı:** paket, kural başlangıcından sonra inceleme isteyen bir onay aldığında bağlanır; sonraki muaf bir yeniden
  onay bağı çözmez.
- **Dilim tablosu onayda da denetlenir:** inceleme isteyen onay, tablo yoksa ya da tek dilimse (`whole_pack` yoksa)
  geçmez. Kod çiti içindeki örnek tablo sayılmaz; DCP tablosu `scripts/ct_route.py`'nin okuyucusuyla okunur.
- **`review-row` komutu:** özetleri ve açık ENGEL sayısını dosyadan hesaplar; elle yazılan sayı yoktur.
- **Kapsam dışı kalanlar:** DCP onayı bugün hiçbir dağıtım kapısına bağlı değildir (WP'nin yetkisi Module Pack'tir);
  `pack_approval.py check <DCP>` el ile ya da CT tarafından koşulur.

## 7. Bağımsız denetim (alt ajan, Opus, 2026-10-20) ve düzeltmeler

Sonuç: UYGUN DEĞİL — 3 BLOCKER, 3 MAJOR, 4 MINOR; hepsi yeniden üretildi ve kapatıldı. Her biri için adı belli bir test
eklendi ve düzeltme bozulunca o test kırmızıya döndü.

| kod | bulgu | düzeltme |
|---|---|---|
| B1 | Hiç geçerli olmamış bir onay satırı karşılaştırma tabanı oluyordu; sonraki satır incelemeyi, HIGH riski ve insan adımını atlıyordu. **CT-APR-01'de de vardı.** | Taban yalnız kendi başına geçen (`judge`) en son onay satırıdır |
| B2 | Yazar `whole_pack`'i kendi LOW satırıyla verebiliyordu | `whole_pack` işareti onayı HIGH yapar (başka yetkili kişi + insan adımı); yalnız geçen son satırdan okunur |
| B3 | Bir merge, onay ya da inceleme dosyasından satır silebiliyordu (varsayılan geçmiş sadeleştirmesi, yalnız ilk üst). **CT-APR-01'de de vardı.** | `--full-history` ve her üst commit: tek üstte önek, merge'de her üstün satırlarını içerme |
| M1 | Kanonik olmayan hedef ya da SHA yazılmış satırlar görünmüyordu | Her satır biçim denetiminden geçer; biri bozuksa dosya okunamaz (çıkış 2) |
| M2 | HTML yorumu ya da alt başlık altındaki dilim satırları sayılıyordu | Dilim tablosu tek, bitişik bir tablodur; bölümde yorum ya da girintili satır varsa tablo yok sayılır |
| M3 | Olumsuz inceleme yalnız denetlenen dalda okunuyordu | İnceleme satırları ve bulgu dosyaları main'den okunur; dal main'deki onay satırlarının hepsini içermelidir |
| m1 | Tarih alanına serbest metin ve girinti değişikliği "muaf" sayılıyordu | Tarih YYYY-AA-GG olmalı; yalnız satır sonu boşluğu muaf; doldurulan sahip değeri kısa ve işaretlemesiz |
| m2 | Benzer harfli ya da küçük harfli bulgu satırı yok sayılıyordu | `|` içeren her satır geçerli bir bulgu satırı olmalı |
| m3 | ENGEL kabulü onaylayana bağlı değildi; UYGUN sonucunda ENGEL olabiliyordu | Kabul eden kişi anahtarı onaylayanınkiyle aynı olmalı; UYGUN sonucunda ENGEL satırı olamaz |
| m4 | `ct_route.py` yüklenemezse iz dökümüyle çıkış 1 | Okunamadı (çıkış 2) |

Performans: değişmeyen commit kimlikleriyle okunan git sonuçları süreç içinde önbelleğe alınır; öz test ~2 dakika.

## 8. Codex kod denetimi (2026-10-20, paket d14eae6a…) ve düzeltmeler

Sonuç: UYGUN DEĞİL (K5 engelleyici). Beşine de katılındı; her biri için adı belli bir test var ve düzeltme bozulunca kırmızı.

| kod | bulgu | düzeltme |
|---|---|---|
| K5 | Merge satır silmeden sırayı değiştirip eski onayı son iptalin arkasına taşıyabiliyordu (A0, R0, R1, A1) | Merge'de ilk üstün satırları önek; diğer üstlerin satırları aynı sırayla (alt dizi) bulunmalı |
| K2 | Onaydan sonra gelen olumlu inceleme eski onayı geçerli yapıyordu | Onayı yalnız onay satırının kendi geçmişindeki incelemeler geçerli kılar; bugünkü kararda main'deki bütün incelemelerin olumsuzları ayrıca uygulanır; tarihsel taban kararı yalnız o anki incelemelerle verilir |
| K4 | Başka bir paketin main'e giren onayı ilgisiz dalları durduruyordu | Yalnız bu paketin main'deki onay / iptal zinciri, dalın zincirinin aynı sıradaki öneki olmalı |
| K1 | Eksik ya da boş hücreli dilim satırı kabul ediliyordu | Her satır başlığın hücre sayısında, boş hücre yok; "bölümler" sütunundaki her ad paketin bir başlığı |
| K3 | Sığ geçmiş ya da `git replace` önbelleği bayatlatabilirdi | `GIT_NO_REPLACE_OBJECTS=1`; sığ depo reddedilir (çıkış 2) |

## 9. Codex ikinci kod denetimi (2026-10-20, paket 6f99565f…) ve düzeltmeler

Sonuç: UYGUN DEĞİL — K1′, K3′, K4′, K5′ UYGUN; K2′ UYGUN DEĞİL; K6 KOŞULLU; K7 UYGUN DEĞİL (yanlış durdurma). Üçüne de
katılındı.

| kod | bulgu | düzeltme |
|---|---|---|
| K2′ | Tarihsel inceleme bulgu dosyasını bugünkü main'den okuyordu; onaydan sonra düzeltilen dosya eski onayı geçiriyordu | `review_problems(..., at)`: inceleme dosyası, yalnız-ekleme geçmişi ve bulgu dosyaları `at` noktasında okunur; tarihsel karar `at` = onay satırının commit'i |
| K7 | Kart üretimi son eski onaya bugünkü olumsuzları uyguluyor, denetim aynı satırı tarihsel taban sayıyordu; üretici ile denetçi farklı taban hesaplıyordu | `judge` yalnız tarihsel; bugünkü olumsuzlar ayrı `today_problems` adımında ve yalnız son satırın kararında; taban seçimi her yerde tarihsel |
| K6 | Aynı commit'teki inceleme + onay | "Aynı sürümün birlikte kabulü" olarak tanımlandı (POLICY-REVIEW-START, SOP §20.4); gerçek zaman sırası kanıtı sayılmaz |

## 10. Codex üçüncü kod denetimi (2026-10-20, paket d1e40539…) ve düzeltmeler

Sonuç: UYGUN DEĞİL — K7′ ve K6′ UYGUN; K2″ ve K8 UYGUN DEĞİL. İkisine de katılındı; ikisi de kalıcı test.

| kod | bulgu | düzeltme |
|---|---|---|
| K2″ | LOW onayın onaylayıcı listesi bugünkü main'den okunuyordu; kişi listeye sonradan eklenince eski onay geçiyordu | `list_point`: liste, satırın main'e girdiği merge'in ilk üstünde (doğrudan yazıldıysa satırın üstünde; main'de değilse dalın main ile son ortak commit'inde) okunur |
| K8 | Geçerli LOW onaydan sonra açılan ENGEL, başkasının kabulüyle yeni onay olmadan kapanıyordu | `today_problems`: onaydan sonra gelen her ENGEL (açık ya da kabul edilmiş) yeni bir onay satırı ister; yeni satırda kabul tarihsel okunur, onay HIGH olur ve onaylayan kabul edenle aynı kişi olmalıdır |

## 11. Codex dördüncü kod denetimi (2026-10-20, paket 2b538b2e…) ve düzeltme

Sonuç: UYGUN DEĞİL — K2‴ UYGUN; K8′ ve K9 aynı kökten UYGUN DEĞİL; yeni MINOR yok. Katılındı.

| kod | bulgu | düzeltme |
|---|---|---|
| K8′ / K9 | Paketi değiştirmeden aynı sürüme eklenen yeni LOW onay "muaf" sayılıyor ve o sürümün UYGUN DEĞİL sonucunu ya da kabul edilmiş ENGEL'in HIGH şartını atlıyordu | Muafiyet yalnız yeni olumlu inceleme şartını kaldırır. Bu sürümde kabul edilmiş ENGEL varsa onay muaf satırda da HIGH sayılır (tarihsel okuma). Sürümün olumsuzları (UYGUN DEĞİL, açık ENGEL) kural kapsamındaki her satıra `today_problems` ile bugün uygulanır |

Bozma notu: muaf satıra tarihsel olumsuzları ayrıca uygulayan bir satır yazılmıştı; bozulduğunda hiçbir test kırmızı vermedi.
İnceleme satırı yalnız main'e girince sayıldığından, bugünkü okuma tarihsel olanı tamamen kapsıyor. Bu yüzden satır
kaldırıldı ("sabotaj kanıtı olmayan guard, guard değildir"). Kalan iki parçanın her biri için ayrı test var (K8′; K9 today).

## 12. Codex beşinci kod denetimi (2026-10-20, paket 44cf288c…) ve düzeltme

Sonuç: UYGUN DEĞİL — K8″ ve K9′ UYGUN; K10 UYGUN DEĞİL; yeni MINOR yok. Katılındı; §11'deki "bugünkü okuma tarihsel
olanı tamamen kapsar" gerekçesi yanlıştı.

| kod | bulgu | düzeltme |
|---|---|---|
| K10 | Açık ENGEL varken aynı sürüme muaf LOW öz onay ekleniyor, ENGEL sonra kabul ediliyor: bugünkü okuma ENGEL'i kapalı görüyor, "sonradan gelen ENGEL" kuralı da tutmuyor (ENGEL onaydan önceydi); yeni HIGH onay olmadan çıkış 0 | Muaf satırda da tarihsel olumsuzlar (`inceleme yok` dışındaki her hata) satırı durdurur; Codex'in sırası kalıcı test; kaldırılıp geri konan satır bozulunca bu test kırmızı |

Ders: bir satırın bozulduğunda hiçbir testin kırılmaması, satırın gereksiz olduğunu değil, ölçen testin eksik olduğunu
gösterebilir. Kaldırmadan önce o satırın yakalayabileceği bir sıra aranmalıdır.
