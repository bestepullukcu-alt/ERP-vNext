# CONTROL TOWER — Daily Operating Card v1.8

> **Purpose:** günlük Control Tower turu için hızlı gate. Yeni authority yaratmaz. Ayrıntı için [CONTROL TOWER SOP](./control-tower-sop.md) (v2.5 r5).

## Bugün hangi komut

> Kısa giriş (CT-FIX-03 U1); kural değildir, ayrıntı aşağıdaki bölümlerdedir. Taslak komutları yalnız taslak üretir:
> çıkış 0 dağıtım izni ya da kabul değildir, `<<DOLDUR` kalan her alan `check`'te ENGEL'dir. Her komut
> `scripts/tests/test_ct_card_commands.py` ile aracın kendi `--help` çıktısına karşı denetlenir.

| Ne zaman | Komut | Çıkış |
|---|---|---|
| WP dağıtılabilir mi — isteğe bağlı erken kontrol (kapı aynı kararı kendisi verir) | `python3 scripts/ct_queue.py require <WP> --queue <kuyruk>` | 0 dağıtılabilir · 1 DUR |
| Yeni iş: önce keşif, raporu commit et (taslaktan önce; yoksa Expected HEAD eskir) | `python3 .antigravity/scripts/verify_module_id.py . --discover "<ad>"` | 0 eşleşme yok · 3 inceleme gerekli · 2 tamamlanamadı |
| Prompt taslağı (keşif commit'inden sonra) | `python3 scripts/prompt_gate.py draft --queue <kuyruk> --wp <WP>` | 0 taslak üretildi — dağıtım izni değil · 2 ölçülemez |
| Paket onayı: kart, onay satırı ve kontrol (kural başlangıcından sonra değişen paket; satırı `execution/approvals/approvals.tsv`'ye siz eklersiniz) | `python3 scripts/pack_approval.py card <paket>` · `python3 scripts/pack_approval.py row <paket> --author <hesap> --approver <hesap> --risk HIGH --reason "<gerekçe>"` · `python3 scripts/pack_approval.py check <paket>` | check: 0 onaylı · 1 onaylı değil · 2 okunamadı |
| Onaydan önce bağımsız inceleme (Module Pack ya da DCP; SOP §20.4; bulgu dosyası önce commit edilir, satırı `execution/approvals/reviews.tsv`'ye siz eklersiniz) | `python3 scripts/pack_approval.py review-row <paket> --findings <dosya> --verdict UYGUN --reviewer codex --model <model> --effort <effort> --reviewer-session <oturum> --author <K…> --author-session <oturum> --requested-by <hesap>` | 0 satır basıldı · 2 reddedildi |
| Dağıtımdan önce (kayıt `main`'de değilse `--prepare`: yalnız hazırlık) | `python3 scripts/prompt_gate.py check <prompt> --queue <kuyruk>` | 0 geçti · 1 ENGEL · 2 tamamlanamadı |
| Kuyruğa yazmadan hemen önce | `python3 scripts/ct_queue.py guard --queue <kuyruk>` | yazma öncesi özet; kilit değildir |
| Başka bir MVP'ye soru | `python3 scripts/ct_route.py --kind delivery --module <MOD>` (sözleşme için `--kind contract`) | 0 tek hedef + gönderilmeyen taslak · 1 hedef yok · 2 ölçülemez |
| Kabul kaydı taslağı (künye; liste için `--part list` ve `--pack` zorunlu) | `python3 scripts/acceptance_record.py draft --part record --queue <kuyruk> --wp <WP> --pack <paket>@<commit>` | 0 taslak üretildi — kabul değil · 2 ölçülemez |
| Kabul kanıtı koşusu (temiz ağaç, yeni dizin) | `python3 scripts/run_record.py run --out docs/records/audits/<…>/<yeni dizin> -- <komut>` | 0 kayıt yazıldı (komutun ham çıkışı kayıtta) · 2 reddedildi, kayıt yok |
| Kabul | `python3 scripts/acceptance_record.py check <kayıt>@<commit> --queue <kuyruk> --wp <WP> --module <MOD>` | 0 ACCEPTED'e izin verir · 1 ENGEL · 2 tamamlanamadı |
| Rapor | `python3 scripts/status_report.py --mode v2 --queue <kuyruk>` | 0 tam · 1 basıldı ama eksik (neden makine bloğunda) · 2 ölçülemez |
| Test koşusundan sonra | `python3 scripts/known_failures.py check <koşu.trx> --suite <süit>` | 0 yeni regresyon yok · 1 regresyon · 2 koşu güvenilmez |

Paket onayı notu: hiç onay satırı olmamış bir paketin ilk onayı HIGH'dır (listedeki, yazardan başka bir kişi onaylar; başka bir kişi birleştirir). Var olan bir sahip değerini değiştirmek (örneğin e-postayı role çevirmek) muaf değişiklik değildir ve bağımsız inceleme ister (SOP §20.4).

## Soru biçimi — her Control Tower, her soru (S-22, sahip kararı 2026-10-21)

Sahibin ya da geliştiricinin karar vermesi gereken her soru, yanıtın **sonunda** ve metnin içinde, şu biçimde yazılır.
**Butonlu soru kutusu (AskUserQuestion) kullanılmaz** (S-35): uygulamanın kendi talimatı kutu kullanılınca soruyu
metinde tekrar etmemeyi istiyor ve bu kartla çelişiyor; soru yalnız metindeki bu biçimle sorulur. "Şimdi siz: … yazın"
satırı soru yerine geçmez: karar gerekiyorsa yanıtın son bloğu ❓ SORU kutusudur.

Markdown alıntı kutusu olarak yazılır (sohbette çerçeveli görünür; terminalde ve Codex'te de okunur):

```text
> ❓ **SORU:** <tek cümle — hangi kararı istiyorum>
> **1) `<cevap kelimesi>` <seçenek> — ÖNERİM**, çünkü <tek cümle neden>
> **2) `<cevap kelimesi>` <seçenek>** — <seçilirse ne olur>
> "`<cevap kelimesi>`" (ya da 1) derseniz: <neye izin verir>; <neye izin vermez (ör. push, PR, merge)>.
```

Görsel kart (HTML) kullanılmaz — her araçta çalışmaz ve her soruda token harcar.

- Bir yanıtta en çok **bir** soru; en çok üç seçenek. Karar gerekmiyorsa soru yazılmaz.
- Önerilen seçenek `ÖNERİM` ile işaretlenir ve gerekçesi tek cümledir; öneri yoksa "önerim yok, çünkü …" yazılır.
- Her seçenek neye izin verdiğini ve vermediğini söyler; sahipten uzun onay metni beklenmez.
- Her seçeneğin bu soruya özgü kısa bir **cevap kelimesi** vardır (`commit`, `düzelt`, `hepsi-evet`); sahip kelimeyi ya da
  numarayı yazabilir. Art arda gelen sorularda aynı numara farklı anlam taşıyabildiği için, yalnız numara gelir ve bu
  önerilen seçenek değilse ya da yazma, silme veya geri alınamaz bir adım başlatıyorsa CT seçeneğin adını tekrar
  edip tek satırla teyit ister: "2 = önce düzelt — bunu mu kastettiniz?" (S-26).
- **Başkasına gidecek metin (S-28):** Codex'e, bir geliştiriciye, başka bir CT'ye ya da bir ekibe gidecek her prompt,
  soru listesi ya da mesaj sohbette **tek bir kopyalanabilir kod bloğunda** (```text … ```) verilir; "dosyadan kopyalayın"
  denmez. Dosyaya da yazılıyorsa orada da aynı kod bloğu içinde durur (başlık ya da `|` satırı markdown olarak
  çözülmez). Ek dosya (ZIP) varsa kod bloğunun hemen altında **mutlak yolu** (`/Users/…` ile başlayan, kısa `.ct-teslim/…` değil)
  ve SHA-256'sı **ayrı bir kod bloğunda** yazılır, kopyalanabilsin (S-36); araç izin veriyorsa dosya Finder'da gösterilir.
- **Uzun prompt işçiye dosyayla (S-52, sahip kararı 2026-10-26):** işçi oturumuna verilen prompt yaklaşık 10 bin
  token'ı aşıyorsa metin yapıştırılmaz; işçiye prompt dosyasının yolu ve SHA-256'sı verilir. İşçi işe başlamadan dosyanın
  özetini hesaplar; tutmazsa hiçbir şey yapmadan durur ve CT'ye bildirir. Böylece okunan metin, prompt kapısından geçen
  metnin birebir aynısıdır. Sahibe gidecek prompt'lar yine kod bloğunda verilir (S-28).
- **Paket onayı değişince prompt'lar (S-53):** bir paketin onayı (yeni revizyon) değişince, eski Expected HEAD'e bağlı
  dağıtılmamış prompt'ları dağıtım kapısı reddeder; bu bilinçli bir kuraldır. CT planına "tabanı yeni main'e al ve
  yeniden inceletmek" adımını koyar.
- **Bulgu dosyasında düz metin (S-59):** inceleme isteği ve bulgu dosyasında kapanmış bulgular düz metin olarak
  "Kapananlar (inceleme NN): F-19, F-20 …" biçiminde yazılır. Bulgu tablosunun dışında satır başında `F-` ve büyük harfle
  `ENGEL` kullanılmaz; ayrıştırıcı böyle bir satırı bozuk bulgu sayar.
- **İnceleme kaydını commit'ten önce dene (S-54):** prompt inceleme kaydı commit'lenmeden önce
  `python3 scripts/prompt_gate.py review-check <prompt> <kayıt dosyası>` ile denenir (her `kontrol` satırının kanıtı kaynak
  paketindeki bir yolu göstermeli). Çıkış 0 dağıtım izni değildir; dağıtım için kayıt commit'lenir ve
  `check --review YOL@COMMIT` koşulur.
- **Prompt'tan önce sade özet (S-45, sahip kararı 2026-10-23):** başkasına gidecek her prompt'tan **önce**, sohbette
  ve kod bloğunun dışında, sahibin dilinde beş satırlık bir özet verilir; teknik kod ve dosya adı kullanılmaz (gerekiyorsa
  ne olduğu yanında yazılır):
  - **Bu ne:** bir cümle — iş isteği mi, geliştirme işi mi, bağımsız inceleme mi.
  - **Kime:** hangi oturuma, kime yapıştırılacak.
  - **Neden:** hangi iş takıldı ya da ne bekleniyor.
  - **Siz ne yapacaksınız:** "yapıştırın" ya da "önce karar verin" (ne kararı).
  - **Başka yol:** varsa kısaca; özellikle iş isteğinde istek sahibinin sorunu kendi tarafında çözüp çözemeyeceği
    (sorunu kim yarattı, daha basit yol var mı, değişiklik kaç işe yarıyor).
  Prompt'un kendisi yine `HEDEF:` satırıyla başlar.
- **Dış istek (S-30):** başka bir ekipten istek gelince CT (1) her isteği sınıflandırır (modül, kimin kararı, mevcut
  paketi etkiliyor mu), (2) her soruya önerilen cevabı **sohbette** gösterir — yalnız itiraz listesi yetmez, (3) isteyene
  gidecek mesajı kod bloğunda hazırlar; doğrulama gereken yerleri (ör. QPPV, başka modül sahibi) işaretler. Mesajı
  kendisi göndermez; kimin ileteceğini sorar. Kuyruğa yazmak ayrı onaydır.
- **Teslim dosyalarının yeri (S-29):** başkasına gidecek ZIP ve benzeri dosyalar oturumun geçici klasörüne değil,
  CT worktree'sinde git'in izlemediği kalıcı bir klasöre yazılır (`.ct-teslim/<iş>/`; worktree'de `.git` bir dosyadır, hariç tutma `$(git rev-parse --git-common-dir)/info/exclude`'a yazılır);
  oturum kapanınca kaybolmaz, depoya da girmez.
- Soru doğrudan izni sorar ("Geçip main'i merge edeyim mi?"). Bir olgu sorusu ("başka oturum açık mı?") izin vermez:
  olgu gerekiyorsa seçenekler olgu ile izni birlikte adlandırır ("Açık oturum yok — geç ve merge et") (S-24).
- Soru teknik terimsiz, düz Türkçe; kod, kimlik ya da yol gerekiyorsa seçeneğin içinde tek parça yazılır.
- Soruda ya da onay istenen satırda geçen her kod, kişi anahtarı, hesap ya da dosya satırı yanında düz cümleyle
  açıklanır: "K3 (siz — GMG-ERP hesabı)", "isteyen GMG-ERP (incelemeyi isteyen sizsiniz)". Değeri CT türettiyse
  nereden türettiğini de yazar. Sahip neyi onayladığını açıklamayı okumadan anlayabilmelidir (S-25).

## Sahibe rapor ve mesaj biçimi — her Control Tower (S-61, sahip kararı 2026-10-26)

Control Tower'ı kullanan kişi geliştirici olmayabilir. Sahibe (ve geliştirici olmayan kullanıcıya) giden **her mesaj**
sade dille yazılır; teknik ayrıntı (test sayıları, sha, commit, dosya yolu) yalnız sahip isterse ya da CT-Setup'a giden
5 satırlık özette yer alır.

- **Kısa haber** (her mesaj): yalnız üç başlık — **Biten · Çalışan · Sizden istenen**. "Sizden istenen" altında **tek iş**
  (hangi sohbete hangi kelime, ya da "bir şey gerekmiyor"); başka kararlar sırası gelince tek tek sorulur. Soru gerekiyorsa
  "Soru biçimi" kutusu bu başlığın içinde kullanılır.
- **Tam rapor** (sahip "rapor" deyince ya da bir kilometre taşında) şu şablondadır:

```text
### <Control Tower> — <modülün sade adı> · <kaçıncı gün>

Katmanlara göre
  Belge        ▓▓▓▓░░░░░░  x / y  <tek satır açıklama>
  Arka uç      …
  API          …
  Gateway      …
  Ekran        …
  Canlı ortam  …
  Kabul edilen: x / y  (<neden henüz değil>)

Parçalar
| Parça | Ne için | Tür | Durum | Kalan prompt (tahmin) | Kimi bekliyor |

Kalan iş: yaklaşık N prompt (tahmin) — X'i şimdi yapılabilir, Y'si bekliyor
  Türlerine göre: …
  Ekranda ne zaman görürsünüz: …

Biten
- <iş>: önce …, şimdi …

Çalışan
- <iş>
  - Ne yapılıyor: …
  - Kullanıcı için: …

Sıradaki
- <iş> — Kullanıcı için: …

Sizden istenen
- <tek iş>
```

Kurallar:
1. **Kod ve kısaltma yok** (WP-…, AC-…, DL…, UIC-…, E3, rev 10, F-22 gibi); gerekiyorsa önce sade anlamı, kod en sonda
   parantez içinde. Her iş "önce / şimdi" ya da "kullanıcı için" cümlesiyle anlatılır; kullanıcı, işten etkilenen kişidir
   (ekranı kullanan ya da geliştirici).
2. **Parçalar** paketin teslim dilimleridir (modülün tamamı); "Tür" Belge · Arka uç · API · Gateway · Ekran · Yapılandırma
   · Kapı'dan biridir (birden çoksa "Arka uç + API"). Bu modülde olmayan katman "—" ile yazılır.
3. **Sayılar ölçülür:** çubuklar ve sayılar kuyruk satırlarından, paketin dilim tablosundan ve GitHub'dan sayılır; elle
   yüzde yazılmaz. Yüzde yalnız neye göre olduğu yazılarak verilir ("iş paketi sayısına göre %30"); iş miktarına göre
   yüzde ancak onaylı efor tabanıyla (REP-001 §7.1).
4. **"Kalan prompt" her zaman "tahmin"dir:** bir dilim ≈ 1–2 geliştirme prompt'u; karar ya da başka dilim bekleyenler
   ayrı sayılır ve "Kimi bekliyor" sütununda kimin kararı beklendiği yazılır (sahip, Beste, başka modülün sahibi).
5. **"Ekranda ne zaman görürsünüz"** her raporda yazılır; ekranı olmayan modülde ne görüleceği söylenir (ör. "her PR'da
   otomatik kontrol").
6. Kayıt ve denetim için teknik tablo (REP-001 §2, `status_report.py`) ayrıca durur; bu biçim onun yerine geçmez, sahibe
   giden anlatımdır.

## 0. Yeni Control Tower açılışı

> Sahip kararı 2026-10-15 (`CT-STEP2-STARTER-KIT-OWNER-DECISION-01`). Yalnız bu karardan sonra açılan Control Tower'lar
> için. Eski Control Tower'lar ve onaylı işleri geriye dönük değişmez, durdurulmaz.

- [ ] **Yer (S-18, 2026-10-21; Codex tasarım görüşü KOŞULLU ve bağımsız inceleme bulguları burada):** geliştirici
  oturumu her zaman ana klasörde açar ve yalnız `/control-tower <modül>` yazar; yeri CT kurar. Hedef doğrulanana kadar
  hiçbir dosya yazılmaz; ana klasördeki değişiklikler taşınmaz, stash yok, silme yok, `--force` ya da reset yok. "İlk
  çağrı salt okunur" kuralının tek istisnası bu adımdaki `git fetch` ve `git worktree add`'dir; commit, push, merge ya
  da dağıtım izni değildir.
  1. **Kuyruğu bul.** `git fetch origin`, sonra kaydı `git show origin/main:execution/delivery/ct-queue-registry.tsv`
     ile oku (ana klasördeki kopya eski olabilir). `<q>` şu sırayla (büyük/küçük harf duyarsız): (i) çağrıdaki ad bir
     `queue_id`'ye eşit; (ii) ad bir `execution/domains/<domain>/` klasörüne eşit ve satırın `path`'i o klasörde; (iii) ad bir
     `MOD-nnnn` ve kayıtlı bir kuyruk dosyasının `module` sütununda geçiyor. `scope` metni eşleşmede kullanılmaz. Sonuç
     0 ya da birden çoksa `queue_id — control_tower` listesini göster ve tek soru sor.
  2. **Yeri bul (salt okunur, dışarıdan).** Geçişten önce hedefe `cd` yapılmaz; okumalar `git -C <wt> …` ve tam yolla
     yapılır — uygulama `cd` ile oturumu oraya taşıyabilir ve 5. adımın sorusu atlanır (S-23). Modülün dalı yalnız `chore/ct/<q>-ct-NN` (NN iki hane). `git worktree list`, yerel ve
     `origin` dalları. Her aday için `origin/main`'e göre ileri/geri sayısı, son commit tarihi ve aynı adlı `origin`
     dalına göre `git rev-list --left-right --count <dal>...origin/<dal>`. Birden çok aday varsa bu bilgilerle listele
     ve tek soru sor; tahminle seçme.
  3. **Tek adaya geç.** Worktree'si varsa oturumu oraya taşı (Claude Code: `EnterWorktree`, `path`). Worktree'si yoksa:
     yerel dal varsa `git worktree add .claude/worktrees/<q>-ct-NN <dal>`; yalnız `origin`'de varsa
     `git worktree add -b <dal> .claude/worktrees/<q>-ct-NN origin/<dal>`; sonra `EnterWorktree path`. `EnterWorktree
     name` kullanılmaz (dalı `chore/ct/…` biçiminde açmaz). Dal aynı adlı `origin` dalının yalnız gerisindeyse ilk soru
     `git merge --ff-only origin/<dal>`; iki yönde ayrışmışsa dur ve sor.
  4. **Hiç yoksa.** Kayıtlı bir kuyruk için hiç `chore/ct/<q>-ct-NN` yoksa (eski adlandırmalı bir CT olabilir) yeni dal
     açmadan önce kuyruk dosyasının son commit'ini (`git log --all -1 --format='%h %an %ci %D' -- <path>`) göster ve tek
     soru sor; kuyruğu başka bir CT yazıyorsa yer kurulmaz, salt okunur devam edilir. Kayıtta hiç satır yoksa (yeni CT)
     kayıtta olmayan bir `<q>` öner (domain kısa kodu) ve `chore/ct/<q>-ct-01` yolunu tek soruyla sun: "evet" yalnız
     worktree açar (`git worktree add -b chore/ct/<q>-ct-01 --no-track .claude/worktrees/<q>-ct-01 origin/main`);
     kuyruk ya da kayıt satırı yazmaz (`--prepare` sınırı).
  5. **Başkasının işi.** Açık bir oturum süreçten ölçülemez (oturum taşınsa da süreç klasörü değişmez). Hedef worktree
     kirliyse ya da son 60 dakikada değişmişse (`git -C <wt> log -g -1 --format=%cr`, `find <wt> -mmin -60 -not -path
     '*/.git*' | head -1`; macOS'ta denendi — `-newermt '-60 minutes'` orada hata verir, S-20) tek soru sor: "Bu worktree'de açık başka bir CT var mı? evet: geçmem /
     hayır: geçerim". `origin/main` gerisindeyse yazma; ilk soru `git merge --no-edit origin/main` (merge commit;
     çakışmada `git merge --abort`).
  6. **Geçemiyorsan devam etme.** Araç oturumu taşıyamıyorsa (Codex) worktree'yi hazırla ve yeni oturumun açılacağı
     klasörü tek satırla söyle; worktree'yi de açamıyorsa komutu tek satır ver. Ana klasörde devam etme. Geçişten sonra
     eski mutlak yolları kullanma; hedefte `AGENTS.md` ve bu kartı yeniden oku.
  İlk yanıtın ilk satırı ölçülmüş durumdur: geçildiyse `Klasör: <hedef> | Dal: … | HEAD: …`; geçilemediyse
  `Klasör: <ana> (hedef: <yol>, geçilmedi) | Dal: … | HEAD: …`.
- [ ] **İş takip sayfası (CT-TRK-01, her CT için zorunlu — S-39):** block'un sayfası yoksa ilk durum değerlendirmesinden ya da ilk paket /
  karar commit'inden sonra CT önerir ve `sayfa-ac` ile açar (SOP §22.2/2a); varsa her kabul, prompt ve karardan sonra
  günceller. Bağlantı SOP §22.2/8'deki yere yazılır. **Sayfa salt okunurdur (S-43):** işaretleme / silme düğmesi yok,
  veri yazma yalnız sayfa sahibine açık; oturum başında ve her main birleştirmesinden sonra CT, merge olmuş / kabul
  edilmiş işleri kanıtıyla kendiliğinden "bitti" yapar — sahip "bitti" demek zorunda değildir (SOP §22.2/4, /7).
- [ ] **Araç bağımlılığı:** `python3 -m pip install -r scripts/requirements-tools.txt`. Kütüphane yoksa keşif
  "tamamlanamadı" der (çıkış 2).
- [ ] **Görev kaynağı:** [`ct-queue-template.tsv`](../../../execution/delivery/ct-queue-template.tsv)'den başlar. Kayıt
  satırı ([`ct-queue-registry.tsv`](../../../execution/delivery/ct-queue-registry.tsv)) `vocabulary` = `sop7`,
  `strict` = `yes` ile, sahip onayıyla `main`'e girer. `python3 scripts/ct_queue.py validate --queue <kuyruk>` 0 hata.
  Kuyruk bir DCP diliminin teslimatıysa `scope` `dcp:<DCP yolu>#<Slice>` ile başlar (CAP-001 §4 sonu); önek yoksa
  `scope` serbest metindir, yönlendirme verisi değildir.
- [ ] **Durumlar:** sözlük `sop7` = SOP §7'nin 13 ana + 6 istisna durumu. İzinli geçişler ve her geçişin kanıtı
  [`ct-sop7-transitions.tsv`](../../../execution/delivery/ct-sop7-transitions.tsv)'de; `öneri` işaretli satırlar SOP'de
  açıkça yazmayan, sahibin onayladığı geçişlerdir. Son durumlar (çıkış yok): ACCEPTED, REWORK, FAILED, SUPERSEDED,
  CANCELLED. Dağıtılabilir durum: yalnız READY.
- [ ] **`module` sütunu:** `MOD-nnnn`, `MOD-nnnn-FUn` ya da `ortak:<MOD listesi | kapsam yolu>`. Kayıt dosyasında
  olmayan kimlik ve kullanımdan kalkmış takma ad reddedilir.
- [ ] **Kalite kapısı (CT-PQG-01, SOP §20.2):** her uygulama promptu dağıtımdan önce
  `python3 scripts/prompt_gate.py check <prompt> --queue <kuyruk>` ile çıkış 0 alır; HIGH için `--review <kayıt>@<commit>`.
  Kapı ve kuyruk kaydı `main`'de değilken yalnız `--prepare` çalışır ve "DAĞITIM İZNİ DEĞİL" der. Elle denetim
  kapının yerine geçmez.

### 0.1 Keşif — her yeni iş için (DISCOVERED → INSPECTION_REQUIRED)

```text
python3 .antigravity/scripts/verify_module_id.py --discover "<ad>" [--resource <gateway kaynağı>] [--sor "<SoR nesnesi>"] > <kanıt yolu>
python3 .antigravity/scripts/verify_module_id.py --check-report <kanıt yolu>
```

- Araç raporu yalnız ekrana yazar; Control Tower onu tek yönlendirmeyle iş paketinin kanıt yoluna kaydeder.
- İlk çağrıda (dosyaya yazılmaz) rapor uzunsa (yüzlerce `ref:` satırı) ekrandan süzülerek okunur:
  `… --discover "<ad>" | grep -v '^ref:'` — `ÖZET`, `kaynak`, `BULGU`, `EKSİK`, `İNCELENEN REF'LER` ve `sonuç`
  satırları okunur; ref satırları yalnız sayılır. Süzme yine kesilirse inceleme "kısmi"dir (S-21).
- Rapor: aracın kimliği (commit + betiğin SHA-256'sı), sorgu, `origin/main` SHA'sı ve son fetch zamanı, incelenen
  kaynakların kimlikleri, eksikler, sonuç ve çıkış kodu.
- Raporun son satırı `rapor-sha256: <hex>`. Değer, bu satır **hariç** önceki bütün satırların `\n` ile birleştirilip
  sonuna bir `\n` eklenmiş UTF-8 içeriğinin SHA-256'sıdır. `--check-report` aynı kuralla yeniden hesaplar: 0 = geçerli,
  2 = bozuk ya da değiştirilmiş.
- Sonuç: 0 = "incelenen kaynaklarda eşleşme bulunamadı" (işin yeni olduğunu kanıtlamaz; rapordaki eksikler geçerlidir),
  3 = "inceleme gerekli", 2 = "tamamlanamadı".
- İş paketi kaydındaki keşif satırı, sabit biçim:

  ```text
  Keşif: <sonuç> · rapor <yol>@<commit> · sha256 <rapor-sha256>
  ```

- **"İnceleme gerekli":** dağıtımdan önce iş paketi kaydına her bulgu için (1) karar — parent, FU, child ya da gerçekten
  yeni; (2) çakışmanın neden yeni işi engellemediği ya da mevcut işin nasıl kullanılacağı yazılır. Çözülmemiş tek bir
  bulgu varken dağıtım yok.
- Keşif çalıştırılmamış ya da "tamamlanamadı" → CT-PQG-01'de engelleyici. Yalnız yeni Control Tower'lar için.

### 0.2 Yeniden işleme

- **Y1. Yeni satır, eski durum korunur.** REWORK, FAILED ve kabulden sonra bulunan hata eski bir duruma dönmez. Yeni
  `<kimlik>-R<n>` satırı READY'de açılır; `depends_on` asıl kimliği içerir, iş metni kök nedeni yazar. Asıl satır ve
  raporu üzerine yazılmaz; kabul edilmiş satır ACCEPTED kalır (SOP §19.2).
- **Y2. Esaslı değişiklik yeni iş paketidir.** Yeni bir kimlik verilir. Asıl satır **sonlanmamışsa** SUPERSEDED olur;
  ACCEPTED ya da başka bir son durumdaysa olduğu gibi kalır ve yeni iş `depends_on` ile ona bağlanır (SOP §19.1, §19.3).
- **Y3. İki tur sınırı.** Aynı kök neden iki ardışık turda kapanmazsa `docs/records/decisions/` altında bir
  ARCHITECTURE_REASSESSMENT_REQUIRED karar/engel kaydı açılır; bu bir kuyruk durumu değildir (SOP §28.2). Kayıt
  çözülmeden yeni `-R` satırı açılmaz ve dağıtılmaz. `-R3` ve sonrası bu kaydı `reassessment_record` sütununda
  gösterir (sütunu olmayan eski katı kuyrukta `record`); kaydın künyesinde tek `status: approved` ya da `decided` gerekir
  ve dosyanın varlığı kararın çözüldüğünü kanıtlamaz. ACCEPTED satırında `record` kabul kaydıdır (SOP §29.5).
- **Y4. `-R` satırı da kapılardan geçer:** dağıtımdan önce DoR (§1) ve CT-PQG-01.

`ct_queue.py validate` katı kuyrukta mekanik olarak denetler: durum adlarının sözlükte olması, son durumdan çıkış
(kuyruk dosyasının git geçmişindeki ardışık sürümler arasında), `-R<n>` satırının var olan asıl kimliğe bağlı olması ve
`depends_on`'da onu göstermesi, `-R3` ve sonrası için karar kaydı, `module` sütunu ve ACCEPTED satırının kabul kaydı
(CT-OPS-03). Atlanmış adım anlık görüntülerden çıkarılamaz.

## 1. START / DoR — dispatch etmeden önce

- [ ] **Identity + owner:** canonical module ID/name ve owner/SoR açık.
- [ ] **Pack authority:** gerekli Module Pack `approved/ready-for-dev`; cross-module ise DCP boundary açık. Künyede `approval_status` koşulluysa yalnız hazırlık; yeni dağıtım için paketin kapsamı ve onayı netleşir; ret ya da bozuk alan DUR (SOP §20.2, Görev sınıfı kuralı).
- [ ] **Contract:** required API/DTO/event/permission/evidence contract mevcut; invention yok.
- [ ] **Dependencies:** `SATISFIED` veya kayıtlı `WAIVED`.
- [ ] **Scope:** allowed paths + protected paths açık.
- [ ] **Git isolation:** repo / branch / expected HEAD / worktree / dirty baseline ölçüldü.
- [ ] **Execution:** Profile A/B/C + Agent Lane ID/Type + risk class açık.
- [ ] **Target agent:** entry point §6'dan seçildi; o ajanın §17.4 zorunlu giriş alanları dolu; paste-ready prompt (§36.1) hazır.
- [ ] **Acceptance:** golden/contract/inspection flow + measurable AC + required evidence level açık.
- [ ] **Görev kaynağı satırı (R8, r3):** WP'nin satırı, dağıtan Control Tower'ın kayıtlı görev kaynağında dağıtımdan önce var (kayıtlı = `execution/delivery/ct-queue-registry.tsv`'de `main`'de geçerli bir satır; SCE / MVP6: `docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv`). Kayıtlı kaynak yoksa: 30 günlük geçiş süresinde yalnız `CT-R8-TRANSITIONAL-DEVIATION-OWNER-DECISION-01` kapsamında (kararı aldığını doğrulamış, onaylı WP / Module Pack, ayrı geçiş referansı); süre bitince yalnız bitişten önce onaylanmış WP'ler, sonra onaylananlar için kayıtlı kaynak ya da sahibin süreli istisnası. Değilse yeni dağıtım yok; çalışan iş durdurulmaz (SOP §0.1 r3, §17.1, §20.1).
- [ ] **Kuyruk kontrolü (gerçek durma):** `python3 scripts/ct_queue.py require <WP-ID> --queue <kuyruk>` — çıkış 1 = DUR, dağıtım yok: satır yok, aynı kimlikle birden çok satır ya da durum dağıtılabilir değil. Katı kuyrukta prompt kapısı (CT-PQG-01) aynı kararı (`require_decision`) kendisi verir; komutu ayrıca koşmak kararı yalnız daha erken gösterir. **Profil değişikliği `main`'e birleştikten sonra yürürlüğe girer:** değişiklik dalda yapılır ve PR ile `main`'e girer; o zamana kadar dalda kuyruğun `strict` / `vocabulary` / `baseline_commit` / `owner_confirmation` / görev dosyası `origin/main`'deki kayıttan farklıysa (`farklı profil`) her tüketici DUR verir (CT-FIX-02 K3, CT-FIX-04). `origin/main`'deki satır sahip teyidi bekliyorsa (`onaysız`) hazırlık sürer; `require --in-force` ve prompt kapısının dağıtım kipi DUR verir. Kuyruklar [`ct-queue-registry.tsv`](../../../execution/delivery/ct-queue-registry.tsv)'de; kayıtlı kuyruğu olmayan Control Tower için dağıtım kendiliğinden serbest değildir. Kuyruğa yazmadan önce `guard` (kilit değildir); dallar arası kimlik çakışması için `writers`.
- [ ] **Başka bir MVP'ye soru (yardımcı; zorunlu kapı değil):** `python3 scripts/ct_route.py --kind delivery|contract --module <MOD>` ya da `--kind delivery --wp <WP-ID> --queue <kuyruk>` (`--in-force`: yalnız `origin/main`'de yürürlükteyse 0). Çıkış 0 = tek hedef ve bir soru taslağı — taslak gönderilmez, bu bir dağıtım izni değildir; 1 = hedef yok ya da belirsiz → koordinatöre (kayıt satırı `origin/main`'dekinden farklıysa iki kipte de 1, CT-FIX-04); 2 = ölçülemez. Sözleşme sorusunda önce gösterilen paket okunur; Control Tower teknik temastır, karar otoritesi değildir. Kişi depoda tutulmaz: isteğe bağlı `--contacts <depo dışı dosya>` ([`ct-contacts-template.tsv`](../../../execution/delivery/ct-contacts-template.tsv) yalnız başlık örneğidir).

**Bir kritik madde FAIL → implementation dispatch yok.**

> **R8 (SOP r3; kararlar `CT-R8-REGISTERED-TASK-SOURCE-OWNER-DECISION-01`, `CT-R8-TRANSITION-PERIOD-OWNER-DECISION-01`):** her Control Tower kendi kayıtlı
> görev kaynağını kullanır; SCE / MVP6 kuyruğuna başkası yazmaz; §36.2 T1–T4 SCE / MVP6'nındır, diğerleri §36.1'i
> kullanır. Yürürlük: r3 `main`'e girdiğinde; 30 günlük geçiş süresi o gün başlar. O zamana kadar v2.5 r2 ve geçiş
> kararı (yalnız onu aldığını doğrulayan Control Tower'lar için).

## 2. PARALLEL / Agent Lane gate

- [ ] Shared contract/migration/permission/global route/shared registration için **single writer**.
- [ ] Parallel Agent Lane'ler ayrı worktree + disjoint scope kullanıyor.
- [ ] `Parallel-safe with` **ölçüldü**, varsayılmadı.
- [ ] Integration order açık.
- [ ] Güvenli paralellik varsa mümkünse **2+ executable WP** hazır.

## 3. DISPATCH PREFLIGHT

```text
WP / Prompt ID + version
PQG: python3 scripts/prompt_gate.py check <prompt> --queue <kuyruk> → çıkış 0 (HIGH: --review; SOP §20.2)
Agent Lane ID / Type
Target agent / entry point
Repository / Branch / HEAD / Worktree
Depends on / Parallel-safe with
Allowed paths / Protected paths
```

Aşağıdakilerde STOP: wrong branch/HEAD, overlapping dirty files, draft pack, missing contract, ownership conflict, unplanned migration, **agent/scope mismatch**.

**S-5 dağıtım kapısı (SOP §20.4 madde 8):** ürün WP'sinin her yeni dağıtımı paket sürümünün main'deki geçerli bağımsız incelemesini ister (katı ya da eski her kuyrukta); kural başladığında yürüyen ve paketi değişmeyen iş korunur, olumsuz inceleme kabulü de durdurur — ENGEL'deki kuralı tanımıyorsanız main'i alın ve yeni Control Tower oturumu açın.

## 4. EVIDENCE LEVEL

| Level | Evidence | Meaning |
|---|---|---|
| E0 | Agent narrative | claim only |
| E1 | Static code/config inspection | readiness |
| E2 | Build/unit/contract tests | implementation confidence |
| E3 | Runtime API/UI smoke | runtime behavior |
| E4 | + persistence/RBAC/tenant/audit/failure paths | state-changing acceptance |
| E5 | + cross-module integrated runtime / restart-compatibility where required | capability/integration acceptance |

**Required level yoksa Agent PASS → CT ACCEPTED değildir.**

## 5. VERIFY

- [ ] Runtime freshness: process start > binary timestamp.
- [ ] Critical fix: mümkünse `fix absent → RED`, `fix present → GREEN`.
- [ ] Producer + consumer birlikte ölçüldü.
- [ ] State-changing ise UI/API + persisted state + audit/observability.
- [ ] RBAC + tenant + concurrency + idempotency applicable kontrolleri.
- [ ] Cross-module ise Integration Gate.
- [ ] Bilinen kırmızılar: koşu TRX ile alınır (`dotnet test … --logger "trx;LogFileName=<dosya>"`) ve `python3 scripts/known_failures.py check <trx> --suite <ad>` ile `tests/known-failures.tsv`'ye karşı okunur. Yalnız tamamlanmış `Failed` satırı bilinen kırmızı olabilir; aynı mesajlı `Timeout`, `Error`, `Aborted` YENİ-AYRINTI'dır (CT-FIX-05). Çıkış 0 = bu karşılaştırmada yeni regresyon yok (testler geçti ya da kabul edildi demek değildir): kayıtlı kırmızılar aynı, ya da KISMEN-DÜZELDİ — kayıtlı öğelerin bir kısmı gitti, yeni öğe yok; kayıt (`tests/known-failures.tsv` + `tests/known-failures-items.json`) elle güncellenir; 1 = YENİ ya da YENİ-AYRINTI → regresyon, STOP; 2 = koşu ya da kayıt güvenilir değil (boş, bozuk, eksik, iptal, hiç test koşmamış, TSV ile JSON uyuşmuyor) → STOP; 3 = konsol çıktısı, ayrıntı doğrulanmadı → kabul için yetmez. Yerel tekrar tanıdan önce `should-rerun --suite <ad>`: ATLA yalnız adı verilen kayıtlı testler içindir — süitin geri kalanı, CI, zorunlu kabul, güvenlik testleri ve sahibin açıkça istediği testler atlanmaz.
- [ ] **Kanıt saklama:** Git'e genel olarak yalnız kapanış koşusunun arındırılmış TRX'i girer; kabul için gereken önce/sonra/gerileme kanıtı istisnadır. Tam sohbet dökümü, geçici klasörler ve tekrar eden ara çıktılar Git'e girmez; önceki kayıtlar silinmez; kanıt `yol@commit` kalır (REP-001). CI artifact'ı süreli olduğu için tek kanıt olamaz. CI (phase1) CT araçlarının öz-testlerini koşar ama `known_failures` karşılaştırması yapmaz: aynı hatalar CI'da da oluşursa CI kırmızı kalır (Linux sonucu ölçülmedi); karşılaştırma yerel kapanış kanıtıdır.

## 6. CLOSE / REPLAN

- [ ] Agent Verdict ayrı, Verification Verdict ayrı, CT Status ayrı.
- [ ] Backlog / Module Pack / Seam / DCP / Delivery Board applicable kayıtları güncellendi.
- [ ] Decision / waiver / intentionally-not-done kaydedildi.
- [ ] **Kabul kaydı (yeni Control Tower, CT-OPS-03):** ACCEPTED'ten önce
  `python3 scripts/acceptance_record.py check <kayıt>@<commit> --queue <kuyruk> --wp <WP> --module <modül>` çıkış 0;
  satırın `record` sütunu bu kaydı gösterir (REP-001 §6.1–6.2, SOP §29.5).
- [ ] **Rapor (yeni Control Tower, CT-OPS-04):** `python3 scripts/status_report.py --mode v2 --queue <kuyruk>`; yüzde
  yalnız onaylı efor tabanından, yoksa "Taban yok — hesaplanamadı" (REP-001 §7.1).
- [ ] Dependency gates yeniden hesaplandı.
- [ ] Newly-unblocked work belirlendi.
- [ ] Güvenli paralellik varsa sonraki **2+ WP** üretildi; yoksa neden yazıldı.
- [ ] PR merge edildikten sonra worktree temizse ve itilmemiş commit'i yoksa `git worktree remove` ile kaldırıldı; değilse kaldırılmadı, sahibe bildirilip soruldu (SOP §16.5).

```text
Commit ≠ Acceptance
Isolated PASS ≠ Integrated PASS
Agent PASS ≠ CT ACCEPTED
Chat ≠ System of Record
Replanning = Closure
```

## 7. Kalıcı onaylar ve açık onay sınırları

> Sahip kararı 2026-10-15 (CT-OPS-01). "Kalıcı" yalnız önceden yetkilendirilmiş kapsam içindir. Bu tablo GIT-002, XMC-001,
> `AGENTS.md` ve SOP'yi değiştirmez; çelişkide onlar kazanır.

| İşlem | Onay | Kaynak |
|---|---|---|
| Salt-okunur inceleme ve ölçüm | Kalıcı — tekrar sorulmaz | Sahip kararı 2026-10-15 |
| REP-001'e uygun rapor üretme | Kalıcı — tekrar sorulmaz | [REP-001](../../../.antigravity/rules/status-reporting-and-evidence.md); sahip kararı |
| Ortak test kilidine uyarak, onaylı kapsamda test çalıştırma | Kalıcı — tekrar sorulmaz | Sahip kararı 2026-10-15 |
| Kendi geçici çalışma dosyalarını kullanma | Kalıcı — tekrar sorulmaz | Sahip kararı 2026-10-15 |
| Açıkça onaylanmış iş paketi için dal ve worktree oluşturma | Kalıcı — tekrar sorulmaz | [GIT-002](../../../.antigravity/rules/git-safety.md) §1; sahip kararı |
| Commit, push, PR ve merge | **Her seferinde açık onay** | [GIT-002](../../../.antigravity/rules/git-safety.md) §4, §5 |
| CT dalının yedek push'u (gün sonu ya da büyük adım sonrası; PR yok, main'e dokunmaz) | **Her seferinde açık onay, tek kelime** — CT kendisi önerir: "Dalı yedekleyeyim mi? `yedek`". Push'tan önce: dal yalnız CT dalı, `git diff origin/<dal>..HEAD` içinde e-posta, şifre ve anahtar taraması; push sonrası uzak uç = yerel HEAD ölçülür | GIT-002 §5; sahip kararı 2026-10-21 ("orta yol", S-27): her commit'te değil — her push CI'ı çalıştırır ve depo herkese açıktır |
| Onay ya da inceleme satırını geri almak (`approvals.tsv`, `reviews.tsv`) | **Yalnız sona `revoke` satırı eklenir** — `git revert`, satır silme ya da düzenleme asla: iki dosya yalnız ekleme kabul eder; geçmişte satır silen tek commit main'e girerse o dosyadaki **bütün** onaylar güvenilmez olur ve geri ekleme düzeltmez (S-34). Satır henüz yalnız yereldeyse ve silindiyse, push'tan önce sahibin onayıyla yerel geçmiş yeniden kurulur | [POLICY-START](../../../execution/approvals/POLICY-START.md); sahip kararı 2026-10-22 |
| Ortak bellek (`~/.claude/projects/<depo>/memory/`) | **Yalnız kendi dosyası** — bütün Claude oturumları aynı bellek klasörünü paylaşır. CT yalnız kendi `project_<kuyruk>_…` dosyasını ekler ya da günceller ve `MEMORY.md` dizinine kendi tek satırını yazar; başka oturumların dosyalarına ve dizin satırlarına dokunmaz, kısaltmaz, birleştirmez. Dizin sınırı aşılırsa sahibe sorar (S-37) | Sahip kararı 2026-10-22 |
| Yıkıcı Git işlemleri | **Her seferinde açık onay** | [GIT-002](../../../.antigravity/rules/git-safety.md) §6 |
| `.antigravity/**` değişiklikleri | **Her seferinde açık onay** | [AGENTS.md](../../../AGENTS.md) §4 |
| Başka modüle müdahale | **Her seferinde açık onay** | [XMC-001](../../../.antigravity/rules/cross-module-change-consent.md) |
| Mimari veya kapsam değişikliği | **Her seferinde açık onay** | SOP §4.1, §19.4 |
| Canlı ortam müdahalesi | **Her seferinde açık onay** | Sahip kararı 2026-10-15 |
| Ortak dosyada tek-yazar anlaşması | **Her seferinde**, etkin Control Tower ile | SOP §16.4, K15 |

### 7a. Paralel Control Tower'lar ve paylaşılan dosyalar (S-38, sahip kararı 2026-10-22)

Aynı depoda birden çok CT ve başka ekipler aynı anda çalışır. Şu dosyalar herkesin ortak kaydıdır:
`execution/approvals/approvals.tsv`, `execution/approvals/reviews.tsv` (ikisi yalnız ekleme kabul eder),
`execution/delivery/ct-queue-registry.tsv`, `execution/registries/module-id-registry.md`, `AGENTS.md` §3 port tablosu.

1. Push'tan önce: CT dalını main ile birleştirir (merge commit) ve kontrolleri birleşmiş hâlde yeniden koşar:
   `pack_approval.py check <paket>`, `ct_queue.py validate --queue <kuyruk>`. **Yön (S-47):** birleştirme dalda
   `git merge origin/main` ile yapılır, dal birinci ebeveyn kalır; GitHub'daki "Update branch" da bu yöndedir. Main'i
   birinci ebeveyn yapan **ters merge yapılmaz**: PR merge'ünde dalın satırları ikinci ebeveyn tarafında kalır ve onay
   aracı onları "iç içe ya da ters merge" diye reddeder (S-40). Dalda koşan `check` bunu "UYARI (S-48)" ile merge'den
   önce söyler. Main'e girmiş bir satır geri alınamaz (dosyalar yalnız ekleme kabul eder). **Geçersiz onay satırı:** aynı
   paket sürümü için doğru yoldan yeni bir onay satırı eklenir; son satır geçerlidir (Codex F-3). **Geçersiz inceleme
   satırı:** o sürümün bütün inceleme satırları sayıldığı için sürüm onaylanamaz; çıkış yolu paketin yeni bir
   revizyonudur.
2. Çakışma çıkarsa: kendi satırlarını ana dalın satırlarının ALTINA taşır; başkasının satırını silmez, değiştirmez,
   sırasını değiştirmez. Emin değilse `git merge --abort` ile geri alır ve sahibe sorar.
3. Bu dosyalara dokunan PR'lar tek tek merge edilir. Merge'den hemen önce PR'da "Update branch" yapılır, kontroller
   yeniden koşar, sonra merge commit ile birleştirilir (SOP §20.4/7).
4. Bir PR merge edilince açık diğer CT dalları bir sonraki push'tan önce main'i alır.
5. Uygulamayı, geliştirme sunucusunu ya da canlı testleri aynı anda yalnız bir CT çalıştırır (aynı portlar ve aynı
   geliştirme veritabanı). Başlamadan önce CT, başka bir CT'nin çalışıp çalışmadığını sorar.
   **Yük altında kırmızı (S-51, sahip kararı 2026-10-26):** aralıklı (zamana bağlı) bir test kırmızısı ölçülürken
   makinenin yükü (`uptime`, çekirdek sayısıyla birlikte) kayda yazılır. Yük çekirdek sayısını aşıyorken çıkan kırmızı,
   yük düştükten sonra bir kez daha koşulmadan "kalıcı" ya da "üründe hata" sayılmaz; iki koşunun sonucu da kayda girer.
6. Kuralları değiştiren bir PR (`.antigravity/`, kart, SOP) merge edilince her CT kartı yeniden okur; skill
   değiştiyse yeni oturum açılır.
7. **Bir PR'da bir paket için tek onay satırı (S-41).** Ara sürümler onaylanmaz; PR'a giren son paket sürümü onaylanır.
   Aynı PR'da aynı pakete iki satır olursa ilk satır merge'le geçerli olur, ikincinin onay kartı main'de başka bir
   tabana göre hesaplanır ve tutmaz; satır merge'den sonra yeniden üretilip yeni bir PR ile alınmak zorunda kalır.
   Onay satırının onaylayanı PR'ı kendisi merge eder; satırı yazan merge ederse onay sayılmaz.

## 8. Domain → Control Tower sahipliği (yalnız kanıttan)

> Kanıt = depo yolu@commit. Kanıt yoksa satır "Belirsiz — sahip ataması bekliyor" yazar; sahip kendiliğinden atanmaz.
> Ölçüm: 11 `execution/domains/*/domain-config.md` dosyasının hiçbiri Control Tower sahibi tanımlamıyor (2026-10-15, `6b8a62d5a`).

| Domain | Control Tower | Kanıt |
|---|---|---|
| supply-chain-execution | MVP6 Control Tower (CT-QUEUE) — **kısmi**: MOD-0147, 0148, 0183–0187, 0190, 0192; domainin diğer modülleri Belirsiz | `docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv@6b8a62d5a` (bu modüllerin satırları; MOD-0188 yalnız başka bir sahibe atıf olarak geçer, 0189 ve 0191 hiç geçmez) · `docs/guides/operations/mvp6-development-process-v1.0.md@6b8a62d5a` · güncel: PR #146 (`3912d568b`) `execution/domains/supply-chain-execution/work-packs/CT-QUEUE.tsv` dosyasını ekledi (MOD-0188, 3 satır) — kayıtlı görev kaynağı değildir; sahipliği doğrulanmadı |
| pharmacovigilance | PVG Control Tower — planda; güncelliği doğrulanmadı | `docs/roadmap/plans/pvg-control-tower-development-plan-2026-08-13.md@6b8a62d5a` |
| platform-shared-services | Backlog'daki "CT" — **kısmi**: Auth, Platform, MOD-0023 onay motoru, MOD-0024 Görev Merkezi, MOD-0027 bildirimler. Etiket yalnız "CT" der, hangi Control Tower olduğunu söylemez; bugünkü sahip, sahip kararı bekliyor | `docs/roadmap/backlog/product-backlog.md@6b8a62d5a` "SAHİP: CT (…)" etiketleri (Auth 13, MOD-0024/Görev Merkezi/WorkCenter 12, MOD-0023 ve onay motoru 10, Platform ve bildirimler 6); paketler `execution/domains/platform-shared-services/module-packs/@6b8a62d5a` |
| master-data-management | Backlog'daki "CT" — **kısmi**: MDM devralma, MOD-0220. Bugünkü sahip, sahip kararı bekliyor | `docs/roadmap/backlog/product-backlog.md@6b8a62d5a` "SAHİP: CT (…)" etiketleri (MDM devralma 10, MDM 4, MOD-0220 2) |
| procurement | Belirsiz — sahip ataması bekliyor | CT-QUEUE'da yalnız MOD-0140 satırı var; domain sahipliği sayılmadı |
| commercial-suite | Belirsiz — sahip ataması bekliyor | — |
| developer-enablement | Belirsiz — sahip ataması bekliyor | — |
| enterprise-strategy-business-performance | Belirsiz — sahip ataması bekliyor | `domain-config.md`'deki "Control Tower Gate 2" bir kapıdır, sahiplik değildir |
| human-capital-management | Belirsiz — sahip ataması bekliyor | — |
| management-governance | Belirsiz — sahip ataması bekliyor | `domain-config.md`'deki "Control Tower Gate 2" bir kapıdır, sahiplik değildir |
| portfolio-delivery | Belirsiz — sahip ataması bekliyor | — |
| *(domain dışı)* altyapı, CI, test altyapısı, ortak ön yüz | Backlog'daki "CT"; bugünkü sahip, sahip kararı bekliyor | `docs/roadmap/backlog/product-backlog.md@6b8a62d5a` "SAHİP: CT (…)" etiketleri (altyapı 12, ortak ön yüz 5, test altyapısı 2) |
