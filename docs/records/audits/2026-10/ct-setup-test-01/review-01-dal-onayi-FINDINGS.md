# Bağımsız inceleme — "dal onayı" (S-115…S-120) değişiklik paketi

İnceleyen: geçmişsiz alt ajan (Claude Fable 5.1). Tarih: 2026-11-01. Kapsam: yalnız inceleme paketi
(`DESIGN.md`, `package.diff`, değişmiş tam `pack_approval.py` / `prompt_gate.py` / `acceptance_record.py`) ve salt okunur
depo (`.claude/worktrees/speed-01`: kart, SOP, GIT-002, `approvals/*.tsv`, CI iş akışı). Depoya yazılmadı, git yazma
komutu koşulmadı, GitHub'a sorulmadı.

Koşulan ölçümler:
- `python3 scripts/pack_approval.py --self-test` (worktree kopyası): **233/233 PASS**, çıkış 0
  (`scratchpad/review-01/selftest-pack_approval.log`).
- Kendi deneylerim `scratchpad/review-01/exp.py` (geçici git depoları; scratchpad'deki `pack_approval.py` kopyası,
  `_PR_SOURCE = _test_pr`): E1–E7, E10 — sonuçları ilgili bulguda "kanıt" olarak verilmiştir.

Satır numaraları: `pack_approval.py` / `prompt_gate.py` / `acceptance_record.py` için scratchpad'deki tam dosyalar;
kart ve GIT-002 için worktree'deki (diff sonrası) dosya; diff için `package.diff` satırı.

---

## Soruların kısa cevapları

**(a) Deferred modda sahip onayı olmadan hangi satır dağıtım alır?** Herhangi bir `approve` satırı: `author` = CT'nin
hesabı, `approver` = main'deki listede `high = evet` olan ve yazanın kişisi olmayan **herhangi bir** login (Beste, gmg-10,
cihan…), üstüne `reviewer_session ≠ author_session` yazılmış bir inceleme satırı. Araç `approver`'ı yalnız listeye ve
kişi anahtarına bakarak doğrular (`pack_approval.py:442-451`); `requested_by` ve oturum alanı **onay satırında yoktur**
(`APPROVAL_COLUMNS`, `pack_approval.py:77-78`) — tasarımın ve SOP'un dayandığı bu alanlar yalnız inceleme satırındadır.
Sahip sözünün tek yeri sohbettir (F-4).

**(b) Satır main'e girince sıkı okuma nerede yanlış geçer?**
- İki hesap, farklı K anahtarı: geçer (E6; liste doğruysa tasarım gereği; `approvers.tsv`'de gmg-10 = K1, GMG-ERP = K3 —
  aynı insansa yanlış geçiş, F-6).
- `high = hayır` birleştiren: ENGEL (öz test 10; `pack_approval.py:914-915`).
- Yazarın başka hesabı birleştiriyor, `author` hücresi dürüst: ENGEL (öz test 9/9b). `author` hücresi **sahte**: geçer
  (E7, F-5) — denetim beyan hücresine dayanır.
- Squash / rebase merge: satır commit'i main'in birinci ebeveyn çizgisine düşer → "doğrudan ana çizgiye yazılmış" ENGEL
  (`pack_approval.py:953-955`): yanlış geçiş değil ama modülün bütün satırları ve kabul kayıtları **geriye dönük**
  geçersizleşir; kart §6 merge yöntemini yazmıyor (F-7).
- Foxtrot / iç içe merge: değişmeyen koruma (`pack_approval.py:960-973`) — ENGEL.
- `approvers.tsv` dalda değiştirilmiş: deferred main'in listesi (`:428`), sıkı `merge^1` listesi (`:436, :441, :901`);
  öz test 1c kırmızı. Doğru.

**(c) Yalnız-ekleme dal–main arasında:** `prefix_problem` (`:935-945`) main'in satırlarını dalın öneki olarak ister;
silip yeniden ekleme (E2) ve yer değiştirme (E3) dalın kendi geçmişinde `_append_only` yürüyüşüyle yakalanır → ENGEL.
Açık: geçmişi **yeniden yazarak** (rebase/amend) yalnız dalda var olan olumsuz satır izsiz silinebilir (F-9).

**(d) Kota yolu fail-closed mı?** Evet. `GitHubQuota` yalnız "rate limit" + (403|exceeded) metninde (`:799-806`);
`evaluate` çıkış 2 (`:1440-1443`, E4d). Kapılarda: `check_dispatch_review` onu `Unreadable` olarak yakalar → S-5 ENGEL
"ölçülemedi — … kota" (`prompt_gate.py:906-907`); kabulde "incelemeler ölçülemedi — kota" (`acceptance_record.py:409-410`)
ya da "onaylı değil (CT-APR-01: sonuç: ölçülemedi: kota…)" (`:393`). Kota dışı GitHub hataları çıkış 1 ("sorulamadı" →
güvenilmez / insan adımı yok). "Onaylı" dönen hata yolu bulamadım. Yalnız metin kusuru (F-13).

**(e) `base_for` / `judge` memo / `today_problems` hangi ref'i okur?** `ctx.rows` daldaki `approvals.tsv` (`:333`; main'in
bu paket satırları önek olmalı, `:1406-1410`); önceki satırlar da `ctx.deferred` ile yargılanır (`:351`); `today_problems`
"bugün"ü deferred'da `ctx.commit`'ten okur (`:494-495`). Tutarlı. **Tutarsızlık** üretici tarafta: `row` ve `card` komutları
her zaman sıkı `Ctx` kurar (`:1456`, `:3388`) → aynı dalda ikinci onay satırının kart özeti `check --deferred` ile
tutmaz (F-3, E1b).

**(f) Mesaj metinleri:** `prompt_gate` "dalda ya da main'de geçerli inceleme yok" (`:902`) = kart §3 = SOP §20.4/8;
`acceptance_record` "dalda ya da main'de onaylı değil … `check <paket> --deferred`" (`:393-395`) = kart §1. Uyumlu.
Kart §1 "`--ref <CT HEAD>`" derken kabul kaydı satırları kaydın kendi commit'inden okur (`at_rows = rec`, `:380`) —
küçük fark (F-10 ile ilgili).

**(g) Kart §7 / GIT-002 vs «onayla»:** çelişir. GIT-002 §5: "PR açmak ve PR birleştirmek bu izne girmez; **her biri
ayrıca onay ister**", push "yalnızca açık kullanıcı onayından sonra"; §4 commit aynı. Kart §7 tablosu (satır 731) ve §6
(680-691) "modül sonu PR'ı için açık onay modülün «onayla» sözüdür" der. `.antigravity/rules` kartın üstündedir (AGENTS.md
§1; kartın kendi başlığı "yeni authority yaratmaz"). Paket GIT-002'yi değiştirmiyor → **GIT-002 kazanır**, kartın
cümlesi bağlayıcı değildir (F-2).

---

## Bulgular

### F-1 · ENGEL · Kart §7a/7 kendi içinde çelişiyor; "PR'da paket başına tek onay satırı" modül PR'ı modeline uymuyor
- Dosya: `docs/guides/operations/control-tower-operating-card.md:846-849` (diff 414-418).
- Senaryo: CT §7a/7'yi okur. 846: "«onaylayan merge eder» cümlesi S-115 ile kalktı … onaylayan olması gerekmez". 849 (diff'te
  değişmeyen bağlam satırı): "Onay satırının onaylayanı PR'ı kendisi merge eder; satırı yazan merge ederse onay sayılmaz."
  İki hüküm aynı maddede. Ayrıca 846 "Bir PR'da bir paket için tek onay satırı … ara sürümler onaylanmaz" — oysa S-117/S-53
  (kart "İşçi sohbetleri" 9) her paket revizyonu için yeni «onayla» ve yeni satır ister ve bu satırlar iki hafta boyunca
  dalda birikip **aynı modül PR'ıyla** girer: modül PR'ı aynı pakete N satır taşıyacaktır. 847-848'deki "ikincinin onay
  kartı main'de başka tabana göre hesaplanır ve tutmaz" gerekçesi de F-3 düzeltilince ortadan kalkar (sıkı okuma merge'den
  sonra her iki satır için aynı tabanı bulur).
- Kanıt: worktree dosyası satır 846-849 (`grep -n` çıktısı).
- Düzeltme: §7a/7'yi yeniden yaz: "Modül PR'ı aynı paketin birden çok onay satırını (her revizyon için bir) taşıyabilir;
  satırlar dalda sırayla yazılır; birleştiren §7b'deki ikinci kişidir." 849'daki cümleyi sil.

### F-2 · ENGEL · Kart §6/§7 "«onayla» = push + PR onayı" GIT-002 §4-§5 ile çelişiyor; üst kural kazanır
- Dosya: kart `:731` (§7 tablosu), `:680-691` (§6 modül PR'ı); `.antigravity/rules/git-safety.md` §4 ("Commit yalnızca açık
  kullanıcı onayından sonra"), §5 ("Push yalnızca açık kullanıcı onayından sonra … PR açmak ve PR birleştirmek bu izne
  girmez; her biri ayrıca onay ister").
- Senaryo: CT, kart §6'ya dayanarak «onayla» sözüyle modül sonunda dalı push eder ve PR açar → GIT-002 §5'i ihlal eder;
  GIT-002 `.antigravity/` katmanındadır ve AGENTS.md §1'e göre kartı ezer. S-99 istisnası GIT-002'ye **yazılmıştı** (§4, §5
  satırları); S-117 istisnası yazılmamış. Paket GIT-002'yi değiştirmiyor; `.antigravity/**` değişikliği ayrıca sahip onayı
  ister (çekirdek kural 6).
- Kanıt: `git-safety.md` §5 metni; diff'te `.antigravity/rules/` için hiç hunk yok (yalnız `skills/control-tower/SKILL.md`).
- Düzeltme (seçenek): (1) GIT-002 §4/§5'e S-117 istisna satırı ekle ("modül ya da dalga sonu push'u ve PR'ı için açık onay,
  o paket sürümü için verilen «onayla» sözüdür; sha ile, taramadan sonra; main, force, merge dışında") — sahip onayıyla,
  aynı kural PR'ında; ya da (2) karttan bu cümleyi çıkar, "PR aç" sözünü zorunlu durak olarak bırak. AGENTS.md çekirdek 8
  ile de hizala.

### F-3 · ENGEL · Aynı dalda ikinci onay satırı (paket revizyonu) `check --deferred`'dan geçemiyor: `row`/`card` sıkı Ctx kuruyor
- Dosya: `pack_approval.py:1448-1463` (`make_row` → `Ctx(root, commit, path)` sıkı, `:1456`), `:3382-3399` (`card` ve `row`
  komutlarında `--deferred` yok), `:340-354` (`base_for` önceki satırı `judge` ile yargılar), `:419-430`.
- Senaryo: dalda v1 için HIGH onay satırı (beklemede). Paket v2'ye revize edilir, incelenir, CT `row` ile v2 satırını üretir
  → `make_row` sıkı Ctx → `base_for` v1 satırını "insan adımı yok" diye reddeder → taban yok → hesaplanan risk HIGH "ilk
  onay" → kart özeti bu kartla yazılır. `check --deferred` → v1 beklemede geçer → taban v1 → risk LOW "1 değişen satır" →
  **kart özeti farklı** → "onay kartının özeti paketin şimdiki kartıyla aynı değil" ENGEL → dağıtım kapısı
  (`prompt_gate.py:938`) ve kabul (`acceptance_record.py:389`) reddeder. Tasarımın "paket yeni revizyona girerse yeni
  «onayla»" (kart "İşçi sohbetleri" 9) akışı çalışmaz; yeni modüllerde ilk satır her zaman daldadır, yani ilk revizyonda
  patlar.
- Kanıt: E1a (v1 deferred çıkış 0) → E1b (`row` ile v2, `check --deferred` çıkış 1, "kart … aynı değil") → E1d (aynı satır
  deferred Ctx ile üretilince çıkış 0). "yanlış geçiş" değil yanlış ret; ama tasarlanan yol kapalı → dağıtımdan önce
  düzeltilmeli.
- Düzeltme: `row` ve `card`'a `--deferred` (ya da `make_row(..., deferred)`; CT için varsayılan deferred); kart §1'e "satır
  `row --deferred` ile üretilir"; sıkı/deferred üretim farkını öz teste ekle (sabotaj: strict üretilen satır deferred'da
  "kart" ENGEL'i vermeli — bugün veriyor; yeşil: deferred üretilen satır geçmeli). Merge'den sonra iki okuma aynı tabanı
  bulduğundan geriye uyumludur.

### F-4 · ÖNEMLİ · "Kabul edilen bedel" var olmayan bir güvenceye dayanıyor: onay satırında `requested_by` / oturum alanı yok
- Dosya: `DESIGN.md:42-44`; SOP §20.4/5 (diff 472-475); CONTRACT §7/4 (diff 975-977); `pack_approval.py:77-78`.
- Senaryo: metinler "sahibin «onayla» sözü … satırın `requested_by` / oturum alanlarındadır" der. `APPROVAL_COLUMNS` =
  pack, pack_sha256, action, risk, risk_reason, author, approver, approved_at, card_sha256, note. Bu alanlar yalnız
  `REVIEW_COLUMNS`'da (`:97-98`). Onay satırının sahip sözüne izi yoktur; (a)'daki gibi CT istediği `approver`'ı yazar.
  Sahip "bedel"i yanlış tarifle kabul etmiş.
- Kanıt: satır 77-78 ve gerçek `approvals.tsv` başlığı (worktree).
- Düzeltme: metni düzelt ("tek dayanak kule sohbetindeki söz"). İsteğe bağlı iz: deferred okumada `note` hücresinde
  `sahip-sözü=<sahip oturum kimliği>@<tarih>` zorunlu kıl (`WHOLE_PACK` gibi bir desen; şema değişmez — başlık değişikliği
  yalnız-ekleme geçmişini kırar, yapma).

### F-5 · ÖNEMLİ · S-41 kalkınca "birleştiren ≠ satırı yazan" yalnız beyan edilen `author` hücresine dayanıyor; sahte hücreyle yazarın diğer hesabı birleştirip geçiyor
- Dosya: `pack_approval.py:436-438` (`author_key = person_of(before, row["author"])`), `:912-913`, `:755-762`
  (`other_merger` git adı/e-postası — `GIT_AUTHOR_*` ile serbestçe yazılır).
- Senaryo: satırı dev (K4) yazar, `author` hücresine `lead2` (K2) yazar; PR'ı üçüncü kişi (lead, K1 — dalın son commit'i)
  açar; dev-alt (K4, dev'in diğer hesabı, `high = evet`) birleştirir. Denetimler: açan K1 ≠ birleştiren K4 ✓; birleştiren
  `high` ✓; `author_key` K2 ≠ K4 ✓; `other_merger` "dev" ≠ "dev-alt" ✓; onaylayan lead K1 ≠ K2 ✓ → **"sonuç: onaylı — HIGH"**.
  Eski kuralda (S-41) birleştiren = onaylayan (lead) zorunluydu; dev'in hiçbir hesabı birleştiremezdi. Yeni kuralda
  "ikinci insan" GitHub'da ölçülen tek kişi birleştirendir ve onun yazar olmadığı beyana bağlıdır.
- Kanıt: E7 — çıkış 0, "insan adımı: … PR #0: açan lead, merge eden dev-alt (GitHub)".
- Düzeltme: `author_key`'i GitHub'dan türet: `pulls/{n}/commits` → onay/inceleme satırını ekleyen commit'in `author.login`
  → `person_of(merge^1 listesi)`; birleştiren bu kişi olamaz (hücre yalnız bilgi). Alternatif: satır commit'inin git
  e-postasını `approvers.tsv`'ye bağlayan sütun. Öz test: "hücre sahte, GitHub yazarı = birleştiren → ENGEL".

### F-6 · ÖNEMLİ · İki hesap – iki kişi anahtarı: `gmg-10 = K1`, `GMG-ERP = K3`; aynı insansa modül PR'ı onaylayan hiç devreye girmeden geçer (soru)
- Dosya: `execution/approvals/approvers.tsv` (worktree) satır 2 ve 5; `pack_approval.py:901-908`.
- Senaryo: CT (GMG-ERP, K3) satırı yazar (`author = GMG-ERP`, `approver = bestepullukcu-alt`), modül PR'ını GMG-ERP açar,
  **gmg-10** (K1, CEO, `high = evet`) birleştirir: açan K3 ≠ birleştiren K1, yazar K3 ≠ K1, high ✓ → sıkı okuma geçer;
  Beste'nin hiçbir eylemi yok. Eski S-41 bunu engelliyordu (birleştiren = onaylayan). Tasarım "listedeki başka bir kişi"
  der; "kişi" tanımı listedeki K anahtarıdır — iki hesap aynı insansa K anahtarı yanlış demektir.
- Kanıt: E6 (gmgerp K13 açar, gmg K11 birleştirir, onaylayan beste K12 — çıkış 0). Gerçek hesapların kime ait olduğunu
  depodan ölçemedim; kullanıcı e-postası `grandmedical.eu`, "GMG" her iki login'de.
- Düzeltme: sahip cevaplasın: gmg-10 ve GMG-ERP aynı insan mı? Evet ise `approvers.tsv`'de aynı K anahtarı (main'e PR
  ile; liste değişikliği) — bu paketin yürürlüğünden **önce**. Hayır ise bulgu NOT'a düşer.

### F-7 · ÖNEMLİ · Modül PR'ının merge yöntemi yazılmamış; squash/rebase modülün bütün satır ve kabullerini geriye dönük geçersiz kılar
- Dosya: kart `:680-691` (§6 modül PR'ı — "merge commit" yalnız `git merge origin/main` için söyleniyor; PR birleştirme
  yöntemi yok); `pack_approval.py:953-955`; karşılaştır S-82 kayıt PR'ı kuralı kart `:799-803` ("yalnız merge commit …
  squash ya da rebase sonrası … geçersiz").
- Senaryo: Beste modül PR'ını "Squash and merge" ile birleştirir → satır commit'leri main'in birinci ebeveyn çizgisinde
  tek commit → her onay/inceleme satırı "doğrudan ana çizgiye yazılmış — merge ile gelen ikinci kişi adımı yok" →
  modülün dağıtımları ve kabul kayıtları sonradan "onaylı değil" okunur; eski akışta onay PR'ı küçüktü ve hata hemen
  görülürdü, şimdi iki haftalık iş etkilenir. GitHub dal koruması squash'ı kapatıyor mu ölçemedim (`gh api` yasak).
- Kanıt: `main_merge` `:953-955` + öz test 8 (doğrudan main → ENGEL).
- Düzeltme: §6'ya "PR yalnız **merge commit** ile birleştirilir (squash / rebase yok); PR açıklamasının ilk satırı bunu
  söyler"; sahipten GitHub ayarını (yalnız merge commit) doğrulaması istensin.

### F-8 · ÖNEMLİ · "İnsan adımı satır main'e girince ölçülür" — ölçen yok
- Dosya: `.github/workflows/approvals-append-only.yml:29` (yalnız `history`); `pack_approval.py:1126` ("CI's `history` is
  unchanged"); `DESIGN.md:25-26` ("bugünkü ölçüm yer değiştirir, kaybolmaz").
- Senaryo: modül PR'ı `high = hayır` biri ya da yazarın diğer hesabı tarafından birleştirilir. Sıkı okuma bunu yakalar —
  ama kimse koşmazsa. Modül bitmiştir: yeni dağıtım yok, yeni kabul yok; CI yalnız yalnız-ekleme bakar. Bulgu ancak aynı
  paketin bir sonraki revizyonunda (`base_for` eski satırı yargılarken) ya da bir denetimde çıkar — o zaman da kod çoktan
  main'dedir.
- Kanıt: CI grep çıktısı (yalnız `history`); diff'te yeni iş akışı yok.
- Düzeltme: `push: main` tetiklemeli CI işi: merge'ün getirdiği `approvals.tsv` satırlarının paketleri için
  `pack_approval.py check <paket> --ref HEAD` (sıkı) → kırmızı main → kart §7b (d) ara PR'ı (iptal satırı + yeniden
  onay). Öz test: "strict check main'de kırmızı → CI kırmızı".

### F-9 · ÖNEMLİ · Dalda geçmiş yeniden yazılarak olumsuz inceleme / iptal satırı izsiz silinebilir
- Dosya: `pack_approval.py:681-724` (`_append_only` yalnız `commit`'in **ataları** üzerinde yürür), `:935-945`
  (`prefix_problem` yalnız main'in satırlarını ister); DESIGN sabotaj 7 yalnız "silinmiş ya da değiştirilmiş" (commit'te).
- Senaryo: dalda UYGUN DEĞİL inceleme satırı (ya da bir `revoke`) yazılır; CT dalı `rebase -i` / `--amend` ile o commit'i
  düşürür; yeni UYGUN inceleme + onay satırı eklenir. Yeni geçmişte düşen commit yoktur → yalnız-ekleme temiz; main'de o
  satır hiç olmadığı için önek denetimi de temiz → deferred okuma geçer. Eski akışta olumsuz satır PR ile main'e girdiği
  için "satır silen tek commit main'e girerse bütün onaylar güvenilmez olur (S-34)" koruması işliyordu; dalda bu koruma
  yoktur. Bu, kartın "Onay ya da inceleme satırını geri almak: yalnız `revoke` eklenir; satır henüz yalnız yereldeyse …
  yerel geçmiş yeniden kurulur" satırının deferred'da **kötüye kullanılabilir** hâlidir.
- Kanıt: kod okuması (`git log --full-history … commit` yalnız erişilebilir geçmişi gezer); deney koşmadım.
- Düzeltme (biri yeter): (1) deferred okumada her onay/inceleme satırının commit'i bir `refs/remotes/origin/*` ref'inden
  erişilebilir olmalı (satır yazılınca push; yeniden yazma = force push = GIT-002 §6 onayı = iz); (2) kuyruk READY
  satırlarının Expected HEAD'i (önceki dağıtımlar) şimdiki ref'in atası olmalı — düşen commit zinciri kırar.
  Öz test: "olumsuz satırlı commit rebase ile düşürüldü → deferred ENGEL".

### F-10 · ÖNEMLİ · Kabul kaydı sabit `rec` commit'inden okuyor + hareketli main'e önek denetimi → main'e herhangi bir inceleme satırı girince eski kabul kayıtları yeniden ölçülemiyor
- Dosya: `acceptance_record.py:380-381` (`at_rows = rec or HEAD or main`; `review_negatives(..., deferred=True)`),
  `prompt_gate.py:628-631` (`prefix_problem(root, commit, main, REVIEWS)` → stop).
- Senaryo: kabul kaydı T'de commit edilir, o anda geçer. Ertesi gün başka kulenin modül PR'ı main'e inceleme satırı
  getirir. Denetçi / Beste / CI `acceptance_record.py check` ile aynı kaydı yeniden koşar: `rec` sabit → main'in satırları
  `rec`'in öneki değil → "inceleme dosyası: bu ref, main'deki … öneki olarak içermiyor — dalı main ile güncelleyin" →
  kayıt **geçmez**, oysa kayıt değişmedi. Eski okuma (main'den) kayıtları her zaman yeniden ölçebiliyordu
  ("kayıt kendi kendini açıklar"). Aynı etki dağıtım kapısında da var ama orada CT dalı güncelleyebilir; kaydın commit'i
  güncellenemez.
- Kanıt: E10 — sabit ref (ct_v2) vs başka paketin satırını alan main → çıkış 1 "önek olarak içermiyor".
- Düzeltme: `rec` için önek denetimini `merge-base(rec, main)` anındaki main'e göre yap (o ana kadar main'in satırları
  `rec`'te olmalı), bugünkü olumsuzları ise main'den ayrıca oku (eski `today` okuması); ya da HEAD `rec`'in soyundansa
  satırları HEAD'den oku.

### F-11 · ÖNEMLİ · Yeniden sıralayan `git merge origin/main` artık her kulede rutin; itilmemiş merge commit'i için GitHub "ölçülemedi" → ENGEL; her okuma GitHub'a sorar → kota
- Dosya: `pack_approval.py:711-718` (`why = None` → GitHub karar verir), `:660-671` (`_pr_merge_why`: sorulamadı →
  problem), `:1411-1419` (yorum: "+53 çağrı ölçüldü"); standing rule "main satırları önce, sonra bizimkiler" (hafıza,
  PVG 10-26).
- Senaryo: A kulesinin dalı [main…, A1]; B'nin modül PR'ı main'e B1 getirir; A `git merge origin/main` yapar, çatışmayı
  kurala göre [main…, B1, A1] çözer. Merge commit'inin birinci ebeveyni (dal) öneki değil → S-42 yolu → GitHub'a "PR merge'ü
  mü?" sorulur → commit henüz itilmediyse 422 → "GitHub'a sorulamadı … ölçülemedi" → "inceleme dosyası güvenilmez" →
  dağıtım durur (E4b). İtilince geçer (E4c) — yani her ana dal birleşmesinden sonra **push** gerekir (GIT-002 §5 söz ya da
  oturum dal izni; S-117 "ara söz istenmez" ile çelişir). Her okuma (dağıtım, kabul, `check`) bu merge'ler için GitHub'a
  yeniden sorar (F-3 Codex: önbellek yok); 8 kule × günde N okuma × birikmiş merge sayısı → kota → bütün kapılar çıkış 2 /
  ENGEL. Mekanizma eski; dal onayı onu sık yola koyuyor.
- Kanıt: E4b (çıkış 1, "sorulamadı"), E4c (geçer), E4d (kota → çıkış 2).
- Düzeltme: fetch sonrası hiçbir `refs/remotes/origin/*`'dan erişilemeyen commit bir GitHub PR merge'ü olamaz (PR merge'ini
  GitHub üretir ve uzak dalda bulunur) — bu durumda GitHub'a sormadan "PR değil" say (yerel kanıt; `_pr_merge_why`'a bir
  ön adım). Kalan sorgular için merge commit'i başına süreç içi önbellek (F-3 Codex kararı yeniden değerlendirilsin:
  itilmiş, sabit bir commit'in PR olmaması zamanla değişmez; yalnız **pozitif** cevap önbellekleniyor, negatif de
  önbelleklenebilir — 40-hex sabit). Kart §7a'ya "ana dal birleşmesinden sonra dağıtımdan önce dal itilir" satırı.

### F-12 · DÜŞÜK · LOW satır, bekleyen HIGH taban üstünde "beklemede" notu olmadan "onaylı — LOW" okunuyor
- Dosya: `pack_approval.py:455-458` (LOW yolu, insan adımı yok), `:1437-1439` (özet yalnız bu satırın "beklemede"sine bakar).
- Senaryo: v1 HIGH dalda beklemede; v2 küçük değişiklik, LOW, `approver = author` (LOW'da serbest) → deferred çıkış 0,
  özet "onaylı — LOW · onaylayan dev", bekleme izi yok (E5). İkinci kişi henüz hiçbir sürüme bakmamıştır; LOW sınıfı
  doğrulanmamış tabana göre hesaplanmıştır (eski akışta taban main'e girmişti).
- Düzeltme: özet satırına taban satırının durumu ("taban v1: insan adımı beklemede") eklensin; kabul kaydı / Biten
  satırı bunu taşısın.

### F-13 · DÜŞÜK · Kota mesajı: kapılarda "ölçülemedi" doğru ama kabulde "onaylı değil (…ölçülemedi: kota…)" öneki yanıltıcı; CONTRACT §7/5 "her okuma çıkış 2" tam değil
- Dosya: `acceptance_record.py:393`; `prompt_gate.py:906-907` (S-5 ENGEL, kapı çıkışı 1); CONTRACT §7/5 (diff 981-983).
- Düzeltme: `code_a == 2` için ayrı satır ("onay ölçülemedi — kota; dosyalar bozuk değil"); CONTRACT'ta "pack_approval
  çıkış 2; kapılar ENGEL 'ölçülemedi — kota' ile durur".

### F-14 · DÜŞÜK · Kart §1.1/3 "LOW'da varsayılan direkt kabul" ile "Direkt kabul: CT «onayla» sözüyle" arasında belirsizlik; inceleme seçimi + «onayla» tek söz mü iki söz mü?
- Dosya: kart `:31`, `:583-584`; "İşçi sohbetleri" 9 ("modül başına tek söz").
- Senaryo: LOW pakette sahip hiçbir şey demeden CT "varsayılan direkt kabul" diye onay satırını yazabilir mi? Metin iki
  türlü okunur. "Bağımsız son bakış" seçildikten sonra temiz sonuçta yine «onayla» gerekir mi? Yazılmamış.
- Düzeltme: "Varsayılan, sahibin düğmede önerilen seçeneğidir; onay satırı her durumda yalnız «onayla» sözüyle yazılır;
  son bakış / Codex sonrası söz yine «onayla»dır" cümlesi.

### F-15 · DÜŞÜK · `check --deferred` tek başına kural öncesi (legacy) satırda `reviews.tsv` geçmişini HEAD'e kadar yürümüyor
- Dosya: `pack_approval.py:484-487` (`today_problems` `[]` döner), `:1416-1419` (ek yürüyüş yalnız bu sürümün satırı
  yoksa). Kapılar `review_negatives` `trail` (`prompt_gate.py:632`) ile kapatır; yalnız elle koşulan `check` eksik.
- Düzeltme: deferred'da `append_only_problems(root, commit, REVIEWS, main)` koşulsuz (maliyet: F-11 ile birlikte düşünülmeli).

### F-16 · NOT · Öz testler yeşil; "yeniden oynatma" kanıtı pakette yok
- `pack_approval.py --self-test` 233/233. DESIGN:104-105 "origin/main'deki bütün onay satırları sıkı okumayla bugünkü
  sonucu birebir vermeli … sayısı kayda yazılır" — paket bu ölçümü içermiyor (GitHub'a sormadan ben de koşamadım).
- Düzeltme: kural PR'ına yeniden oynatma sonucu (satır sayısı, değişen sonuçlar) eklensin.

### F-17 · NOT · `DESIGN.md` diff'te yok; worktree'de var
- `package.diff` dosya listesi: SKILL.md, kart, SOP, şablon html/md, review-request-template, CONTRACT, üç script.
  `docs/roadmap/plans/ct-branch-approval-01/DESIGN.md` worktree'de mevcut ama diff'e girmemiş (izlenmeyen dosya ya da
  önceki commit). Kural PR'ında olduğundan emin olunmalı; kart ve SOP ona atıf veriyor.

### F-18 · NOT · Kırılgan öz test eşleşmesi
- `acceptance_record.py:1608, 1676, 1715`: `"main'de onaylı değil" in e` — yeni metin "dalda ya da main'de onaylı değil"
  alt dize olarak eşleşiyor; metin bir daha değişirse sessizce kırılır. Tam ifadeyle ara.

### F-19 · NOT · SKILL.md değişikliği `.antigravity/**` altında
- `.antigravity/skills/control-tower/SKILL.md` (diff 1-42) çekirdek kural 6 gereği sahip onayı ister; DESIGN sahip
  kararını kaydediyor, PR açıklaması bunu ilk satırda söylemeli (kart §7b/3 istisna PR'ı kuralı).

---

## Sonuç

Araç tarafı (deferred okuma, önek, yalnız-ekleme, kota) kodda tutarlı ve öz testleri yeşil; yanlış geçişlerin çoğu
tasarımın bilerek kabul ettiği "onaylayan alanı doğrulanmaz" bedelinin uzantısı. Dağıtımdan önce düzeltilmesi gerekenler:
kartın kendi içindeki §7a/7 çelişkisi (F-1), GIT-002 ile çelişen «onayla»=push/PR cümlesi (F-2) ve ikinci onay satırının
hiç geçemediği `row`/`check` tutarsızlığı (F-3). F-5/F-6 ise S-41'in kalkmasıyla açılan gerçek "ikinci kişi" boşluğudur;
F-6 sahibin tek cevabıyla kapanır ya da düşer.

`SONUÇ: UYGUN DEĞİL` · `açık ENGEL: 3`
