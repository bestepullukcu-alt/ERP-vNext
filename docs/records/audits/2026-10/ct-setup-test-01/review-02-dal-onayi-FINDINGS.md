# Bağımsız inceleme — "dal onayı" (S-115…S-122) değişiklik paketi, tur 2

İnceleyen: geçmişsiz alt ajan (Claude Fable 5.1), tur 1'den bağımsız. Tarih: 2026-11-01. Kapsam: yalnız inceleme paketi
(`DESIGN.md`, `ROUND1-FINDINGS.md`, `package.diff`, `fixes-since-round1.diff`, değişmiş tam `pack_approval.py` /
`prompt_gate.py` / `acceptance_record.py` / `approvals-append-only.yml`) ve salt okunur depo
(`/Users/alitufanoglu/ERP-vNext/.claude/worktrees/speed-01` @ `507d0d5a4`, çalışma ağacı temiz). Depoya yazılmadı, git yazma
komutu koşulmadı, GitHub'a sorulmadı, `cd` kullanılmadı.

Koşulan ölçümler (kayıtlar `scratchpad/review-02/`):
- `python3 …/speed-01/scripts/pack_approval.py --self-test`: **247/247 PASS**, çıkış 0, 4 dk 39 sn (`selftest-pack_approval.log`).
- `python3 …/speed-01/scripts/acceptance_record.py --self-test`: **187/187 PASS**, çıkış 0, 23 sn (`selftest-acceptance_record.log`).
- Kendi deneylerim `exp/exp2.py` ve `exp/exp3.py` (geçici git depoları; scratchpad'deki `pack_approval.py` kopyası, CLI deneyleri
  için kopya geçici deponun `scripts/` dizinine konuldu; merge'li senaryolarda `_PR_SOURCE = _test_pr`): E1–E6, sonuçlar
  `exp/exp2.log`, `exp/exp3.log`. İş akışı YAML'ı `ruby -ryaml` ile ayrıştırıldı (PyYAML yok).

Satır numaraları: `pack_approval.py` / `prompt_gate.py` / `acceptance_record.py` için scratchpad'deki tam dosyalar (worktree ile
aynı sürüm); kart (`docs/guides/operations/control-tower-operating-card.md`), SOP, CONTRACT ve POLICY-BRANCH-START için
worktree dosyası.

---

## 1. Tur 1 kapanış tablosu

| F-n | Durum | Kanıt |
|---|---|---|
| F-1 ENGEL (kart §7a/7 çelişkisi, "tek onay satırı") | **kısmen** | §7a/7 yeniden yazıldı (kart 856-861: birden çok revizyon satırı, son satır karar verir, `row --deferred`, birleştiren = listeli/high/≠yazar, açan = yazar; "onaylayanı kendisi merge eder" cümlesi silindi). **Kalan iki kopya** aynı §7a'da: kart 840 "Onay ya da inceleme satırı taşıyan PR bu toplu birleştirmeye girmez: onu satırdaki **onaylayan kişi kendisi birleştirir (§7a/7)**" ve 845-846 (Yedek onaylayıcı S-101) "**Onay satırını satırdaki onaylayan birleştirir**: K3 birleştirecekse satır baştan onun adıyla ve onun kendi sözüyle yazılır". İkisi de §7a/7'nin yeni metniyle ve araçla (S-41 kalktı, `pr_person_check` `approver=None`) çelişir → G-1. SOP, CONTRACT, SKILL, şablon ve DESIGN'da başka kopya yok (`grep` boş; CONTRACT §7/3 ve SOP 70 doğru). |
| F-2 ENGEL («onayla» = push + PR, GIT-002 çelişkisi) | **kapandı** | `git-safety.md` değişmedi (diff'te hunk yok; worktree dosyası §4-§5 S-99 metniyle aynı). Kart artık her yerde ikinci sözü ister: "Ne değişti" S-117 (24-25 "ikinci ve son söz «PR aç» (push ve PR; GIT-002 §4–5 değişmedi)"), "İşçi sohbetleri" 9 (435-437: «PR aç» zorunlu durak listesinde; "PR aç" ara sözler listesinden çıkarıldı), §6 (691-698: hazırlık sözsüz, "Sizden istenen: «PR aç»", söz yalnız o sha için), §7 tablosu (741: "açık onay sahibin «PR aç» sözüdür … GIT-002 değişmedi"), §7b (773-774). DESIGN §5 düzeltme paragrafı (36-39) aynı. Çelişki kalmadı: kart artık GIT-002'nin istediği açık onayı tanımlıyor, yeni yetki yaratmıyor. Küçük not G-13. |
| F-3 ENGEL (`row`/`card` sıkı Ctx) | **kapandı** | `make_row(..., deferred)` → `Ctx(root, commit, path, deferred)` (1507-1527); CLI `card --deferred` (3587), `row --deferred` (3596-3597), `card` komutu `Ctx(…, a.deferred)` (3642), `row` komutu `make_row(…, a.deferred)` (3653). Kart ENGEL mesajı deferred'da `row --deferred`'ı adlandırır (476-480). Öz test 233-236 (F-3 1 / sabotaj / 2a / 2b). **Kendi deneyim (CLI):** E1a v1 `row --deferred` → `check --deferred` çıkış 0; E1b `card` ≠ `card --deferred` bekleyen tabanda (a1e18ec8… / 497ecad5…); E1c v2 `row --deferred` (LOW) → `check --deferred` çıkış 0 ve "insan adımı: beklemede (taban e6393a5b…)"; E1d aynı v2 düz `row` ile → çıkış 1 "kart … aynı değil … `row --deferred` ile üretilmeli (S-115 F-3)"; E1e `row --deferred` notsuz → çıkış 2 ret. Merge sonrası sıkı okuma aynı tabanı bulur (öz test 2a; E3d). |
| F-4 ÖNEMLİ (`requested_by` yok; bedel yanlış tarif) | **kapandı (tasarlanan sınırda)** | Metinler düzeltildi: DESIGN 45-47, kart 529-531, SOP 1429-1434, CONTRACT §7/4. Araç: `OWNER_WORD` (98), `judge` bekleyen HIGH satırda `under_branch_rule` ise not şart (441-444), `make_row(deferred)` notsuz ret (1518-1519). Öz test 229-232; E1e; E5 regex ("onaylandı", "onaylamadı" eşleşmez). Sahte not bedeli DESIGN 45-48'de yazılı — G-14 (NOT). Yalnız bekleyen **HIGH** satırda ölçülür; LOW satır (geçerli taban üstünde) notsuz da geçer — LOW'un bugünkü sınırı, yeni boşluk değil. |
| F-5 ÖNEMLİ (birleştiren ≠ yazar yalnız beyan) | **kapandı; eski satırları düşürmüyor; silinirse fail-closed** | `pr_person_check(opener_key)` (944-947), onay satırı için `judge` 452-455 (`author_key` kişi anahtarı → `opener_key`), inceleme satırı için `review_problems` 1183, 1202-1204 (`r["author"]` K… ile `po` karşılaştırılır — kişi anahtarı eşlemesi doğru, G-3'e bakın). Kapsam: `branch_start` (1294-1304) + `under_branch_rule` (1307-1308): `strictly_before(added, bstart)` → POLICY-BRANCH-START commit'inin **atası** olan satırlar eski kuralda kalır. Öz test 205 (legacy: üçüncü kişi açmış, notsuz → sıkı çıkış 0), 237-238 (E7 sahte hücre → ENGEL; inceleme satırı açan ≠ yazar → ENGEL). **Kendi deneyim:** E4a politika öncesi çatallanmış dalda politika sonrası satır → kural altında (CT-APR-02 okuması, doğru); E4b dalda dosya silinmiş → kural main'in başlangıcından yine uygulanır (ayrıca bayraklanmaz, G-8); E4c/E4d main'de silinmiş → her okuma çıkış 2 "kural silinerek kapatılamaz". E6: yazar hücresi K4, PR'ı K2 açmış, K1 birleştirmiş → ENGEL (hem onay hem inceleme satırı). Bugünkü main satırları: dal origin/main'e satır eklemiyor (`git diff origin/main...HEAD -- execution/approvals/` yalnız POLICY dosyası); main'deki bütün satırlar politika commit'inin atası olacak → düşmez. **Açık (devreden) onay PR'ları için G-3.** |
| F-6 ÖNEMLİ (gmg-10 K1 / GMG-ERP K3 aynı insan mı) | **açık — sahip kararı (kayıtlı)** | DESIGN 100-101, S-122. `approvers.tsv` değişmedi. F-5 ile etkisi büyüdü → G-3. |
| F-7 ÖNEMLİ (merge yöntemi) | **kapandı** | Kart §6 699-701: "PR yalnız merge commit ile birleşir (squash ya da rebase … geriye dönük geçersiz kılar; S-82 kalıbı, inceleme F-7) — Beste'ye giden mesaj bunu bir satırla söyler". GitHub ayarı yine ölçülmedi (gh yasak) — sahipten doğrulama isteği DESIGN'da yok (NOT). |
| F-8 ÖNEMLİ (insan adımını ölçen yok) | **kapandı — yeni boşluk G-2** | `approvals-append-only.yml` 16-22 `push: branches: [main]` + `paths`; `append-only` işi `if: pull_request` (30); yeni `strict-check-on-main` işi `if: push` (44-80): `fetch-depth: 0`, `origin/main` fetch (`MAIN_REFS` 76 ile uyumlu), `GH_TOKEN: github.token`, `before = github.event.before` (boş / 0000… / yoksa `HEAD^1`), dokunulan paketler `git diff -U0 before HEAD -- approvals.tsv reviews.tsv | + satırlar | 1. sütun | ^execution/`, paket başına `check <p> --ref HEAD` (sıkı), kırmızıda `rc=1`. YAML geçerli (ruby: on = [pull_request, push], iki iş, if koşulları doğru). **Boş liste:** satır eklemeyen push'ta `packs` boş → "sıkı okuma gerekmez", çıkış 0 (E2d; kural PR'ının kendisi de satır eklemiyor → yeşil). Paket çıkarımı doğru (E2b/E2c' başlık satırı ve inceleme satırı dâhil doğru paketi buldu). `set -euo pipefail` ile `|| true` boş grep'i kurtarıyor. Kota: iş akışı belirteci kendi kotasını kullanır (depo başına 1000/sa), ortak kullanıcı kotasına dokunmaz (G-12). |
| F-9 ÖNEMLİ (dalda geçmiş yeniden yazma) | **açık — sahip kararı (kayıtlı)** | DESIGN 101-102. Araçta değişiklik yok. |
| F-10 ÖNEMLİ (kabul kaydı yeniden ölçülemiyor) | **kısmen** | `acceptance_record.py` 383-389: `anchor = merge-base(at_rows, main)`, `review_negatives(..., anchor)` + `anchor != main` ise bugünkü main'in sıkı olumsuzları eklenir; `evaluate(..., anchor)` (397) → `prefix_problem(…, anchor or ctx.main, REVIEWS)` (1471); `prompt_gate.review_negatives(anchor)` (605-610). Öz test (acceptance) 115-116: başka paketin satırı main'e girince kayıt geçer; bugünkü revoke durdurur. **Kendi deneyim E3b:** aynı paketin **sonraki revizyon satırı** modül PR'ıyla main'e girince eski DL-1 kaydı yine ölçülemiyor (K4 onay öneki `ctx.main`'e göre, 1461-1465: "bu ref, main'deki bu paketin onay / iptal satırlarını aynı sırayla içermiyor"). S-115 öncesi okuma da aynı durumda düşüyordu (`package.diff` 798-799: "bu sürüm main'deki paket sürümü değil") → gerileme değil, tur 1'in "her zaman yeniden ölçebiliyordu" iddiası o hâl için de doğru değildi → G-4 (DÜŞÜK). |
| F-11 ÖNEMLİ (yeniden sıralama merge'i GitHub'a sorulur, kota) | **kapandı — iddia doğru** | `_append_only` 737-746: `violated(alt)` False (main öneki + dal satırları sırayla) ve `violated(parents[0])` True ise yalnız **main'in atası olan** merge için `why = None` (GitHub); main'de olmayan merge → `continue`. **Satır kaybeden merge:** `violated(alt)` True → `why = ""` → GitHub sorulmadan problem (734-735, 751; `append_only_problems` 543-549 yalnız `why is None`'da sorar). Öz test 146 (erişilemeyen GitHub ile main dışı merge → yalnız ekleme), 239 (E4 yeniden sıralama, 422 veren kaynakla çıkış 0), 240 (dalın kendi satırını kaybeden merge → ENGEL "yalnız ekleme değil", GitHub sorulmadı). Foxtrot (main'e girmiş merge) eskisi gibi sorulur (öz test 148-151). POLICY-BRANCH-START'ın "satır kaybeden merge bugünkü gibi sorulur" cümlesi yanlış ifade (sorulmaz, reddedilir) → G-6. |
| F-12 DÜŞÜK (bekleyen taban izi) | **kapandı** | `evaluate` 1493-1498 `base_index` ile kuyruk notu; E1c: "insan adımı: beklemede (taban e6393a5b, dal onayı S-115)"; öz test 233. |
| F-13 DÜŞÜK (kota mesajı) | **kapandı** | `acceptance_record.py` 399-403 `code_a == 2` ayrı satır "onayı ölçülemedi — … onay dosyaları bozuk sayılmaz"; CONTRACT §7/5 "çıkış 2 … dağıtım kapısı S-5 ENGEL 'ölçülemedi — kota'". |
| F-14 DÜŞÜK («onayla» tek söz mü) | **kapandı** | Kart 585-588: "«onayla» tek sözdür (inceleme F-14): LOW'da son bakış yok; HIGH'da son bakış arka planda koşar, temiz çıkarsa ikinci söz gerekmez; ENGEL çıkarsa … yeni sürüm için yeniden «onayla»". |
| F-15 DÜŞÜK (legacy satırda reviews geçmişi yürünmüyor) | **kapanmadı — kayıtsız** | `today_problems` 505-506 ve `evaluate` 1472-1474 değişmedi; DESIGN "düzeltildi" (92-99) ya da "kalan" (100-102) listesinde yok → G-9. |
| F-16 NOT (yeniden oynatma kanıtı) | **kapanmadı — nedeni kayıtlı** | Pakette yok; ledger S-121: tam yeniden oynatma kotayı 1822→0 indirdi, durduruldu, "hedefli ölçüme geçildi"; sayı DESIGN 126-127'nin istediği gibi kayda girmedi → G-10. |
| F-17 NOT (DESIGN.md diff'te yok) | **kapandı** | `git diff --stat origin/main...HEAD` dalda `docs/roadmap/plans/ct-branch-approval-01/DESIGN.md` (+134) → kural PR'ına girer. |
| F-18 NOT (kırılgan eşleşme) | **kapandı** | `acceptance_record.py` 1620, 1688, 1727 tam ifade "dalda ya da main'de onaylı değil". |
| F-19 NOT (SKILL.md `.antigravity/**`) | **açık — PR açılınca ölçülür** | PR yok; dalda ayrıca `.antigravity/rules/permission-key-standard.md` (+1, `leave.*`) var → G-11. |

---

## 2. Yeni bulgular

### G-1 · ÖNEMLİ · F-1'in iki kopyası kart §7a/3'te kaldı: "onaylayan kendisi birleştirir"
- Dosya: kart `:840` ("… onu satırdaki onaylayan kişi kendisi birleştirir (§7a/7)"), `:845-846` ("Onay satırını satırdaki
  onaylayan birleştirir: K3 birleştirecekse satır baştan onun adıyla ve onun kendi sözüyle yazılır").
- Senaryo: Beste ya da CT §7a/3'ü okur, §7a/7'ye yönlendirilir ve tam tersini bulur (856-861: "onaylayan olması gerekmez");
  kart yine kendi içinde çelişir — tur 1 F-1'in ölçütüyle aynı sınıf, yalnız yeri değişti. Araç tarafı tutarlı (S-41
  `pr_person_check`'ten kalktı), bu yüzden ENGEL değil; ama 846'daki "satır baştan onun adıyla yazılır" bir CT'yi gereksiz
  yeni satır üretmeye (yeni «onayla» sözü, F-3 zinciri) yöneltebilir.
- Kanıt: `grep -n "onaylayan kişi kendisi birleştirir\|satırdaki onaylayan birleştirir"` → 840, 845.
- Öneri: 840'ta "onu satırdaki onaylayan kişi kendisi birleştirir (§7a/7)" → "onu §7a/7'deki ikinci kişi birleştirir";
  845-846'daki iki cümleyi sil ("PR'ı açan kendi PR'ını birleştiremez; K3 hesabıyla açılan PR K2'de kalır" kalır — F-5 ile uyumlu).

### G-2 · ÖNEMLİ · F-8 CI işi meşru durumlarda main'i kırmızıya çeviriyor: iptal satırı, onaysız revizyonun inceleme satırı, ölçülemeyen okuma
- Dosya: `approvals-append-only.yml:64-80` (`check <p> --ref HEAD` çıkışı ≠ 0 → KIRMIZI); `pack_approval.py:1477-1486`
  (`onay yok`, `son satır iptal`, `paket onaydan sonra değişti` hepsi çıkış 1), `:1499-1504` (kota / okunamadı çıkış 2).
- Senaryo 1 (kartın tek geri alma yolu): kart §7 tablosu "yalnız sona `revoke` satırı eklenir" — iptal satırı PR ile main'e
  girer → iş `check` koşar → "son satır iptal — onay yok" çıkış 1 → **main kırmızı**; düzelme yolu yeni onay satırı = yeni PR.
  Senaryo 2 (sahip uzakta, dalga sonu): paket v2 incelendi (UYGUN), «onayla» henüz yok; dalga PR'ı v2 + inceleme satırını
  getirir → "paket onaydan sonra değişti — yeniden onay gerekli" çıkış 1 → kırmızı. Senaryo 3: kota ya da okunamayan dosya
  (çıkış 2) da kırmızı, mesaj "satır sıkı okumada geçmiyor; iptal satırı + yeniden onay" yanlış adres. Kırmızı main kart
  §7b (d) ara PR'ını tetikler; kuleler ya gereksiz PR açar ya da kırmızı main'e alışır — ikincisi F-8'in amacını boşaltır.
- Kanıt: E2b (iptal satırı merge → sıkı çıkış 1 "son satır iptal", iş akışı paket listesi o paketi buluyor); E2c' (geçerli
  v1 onayı main'de, v2 + UYGUN inceleme merge → çıkış 1 "paket onaydan sonra değişti (1 satır)"); iş akışı `rc=1`
  koşulsuz.
- Öneri: işin amacı insan adımıdır — yalnız o sınıfı kırmızı say: (a) `check` çıktısında "insan adımı yok", "onaylayıcı
  listesinde yok", "yüksek riskli işi onaylayamaz", "açan satırın yazarı değil", "yalnız ekleme değil" geçiyorsa kırmızı;
  "son satır iptal", "onay yok", "paket onaydan sonra değişti", "inceleme yok" yalnız bilgi satırı; (b) çıkış 2 için ayrı
  mesaj ("ölçülemedi — kota; main bozuk değil") ve kırmızı kalsın mı sahip kararı; (c) ya da paket listesini yalnız
  `action == approve` satırı getiren ve o satırın `pack_sha256`'sı HEAD'deki paketle aynı olan paketlerle sınırla. Öz
  teste "iptal satırı merge → CI yeşil / sahte birleştiren → CI kırmızı" çifti.

### G-3 · ÖNEMLİ · F-5 "açan = yazar" kişi anahtarına bağlı; `--author` hücresine hangi hesabın yazılacağı tanımsız — bugünkü iki sahip hesabı (K1 / K3) ile modül PR'ı ve devreden onay PR'ları kırmızı olabilir
- Dosya: `pack_approval.py:452-455` (`author_key = person_of(before, row["author"])`), `:944-947`, `:1202-1204`
  (inceleme satırı `author` = K…); kart `:70` (`--author <hesap>` "paketi yazanın onaylayıcı listesindeki hesabı"), `:71`
  (`--author <K…>`), `:858` (`row … --deferred`, hesap belirtilmez); `approvers.tsv` (gmg-10 = K1, GMG-ERP = K3);
  POLICY-BRANCH-START "Eski satırlar: bu commit'ten **önce main'e girmiş** satırlar geriye dönük düşmez".
- Senaryo A (yeni modül): CT satırı `--author GMG-ERP` (K3) yazar (bugün 20 satır böyle; 14 satır `gmg-10`, K1); modül PR'ını
  sahip `gh` oturumundaki hesapla açar; hesap gmg-10 ise açan K1 ≠ yazar K3 → sıkı okuma HIGH onay satırı **ve** her inceleme
  satırı (author K1 130 / K3 44 satır) için "açan satırın yazarı değil" → modülün bütün onay ve inceleme satırları düşer →
  CI (F-8) kırmızı, kabul kayıtları yeniden ölçümde düşer (F-3 2b zinciri). Yanlış geçiş değil, geniş etkili yanlış ret;
  düzeltmesi iptal + yeni satırlar + yeni PR. Senaryo B (geçiş): kural PR'ı main'e girdiğinde **açık** olan eski yol onay
  PR'larının satır commit'leri politika commit'inin atası değil → `under_branch_rule` True → bu satırlar da F-5 ile ölçülür;
  DESIGN 133 "açık onay PR'ları eski yolla biter" demesine rağmen açan ≠ yazar ise satır main'e girdiği anda geçersiz ve o
  modülün dağıtım yolu kapanır (deferred okumada main'deki satır sıkı yoldan geçer, 445-457).
- Kanıt: E6 (yazar hücresi K4, açan K2, birleştiren K1 → çıkış 1, hem "insan adımı yok" hem "inceleme satır 2 …"); öz test
  237-238 aynı mekanizma. Hangi hesabın PR açtığı depodan ölçülemedi (kayıtlarda "açan …" izi yok; `gh` yasak).
- Öneri: (1) F-6'nın cevabı önce: aynı insansa `approvers.tsv`'de gmg-10 ve GMG-ERP aynı K (listede değişiklik, main'e
  PR ile) — o zaman açan/yazar hesap farkı kişi düzeyinde kaybolur ve G-3 düşer; (2) ne olursa olsun kart §1 ve §7a/7'ye
  tek cümle: "`--author` (onay satırı) ve `--author <K…>` (inceleme satırı) = modül PR'ını açacak `gh` hesabı ve onun
  kişisi (`gh auth status`)"; (3) geçiş: kural PR'ı birleşmeden önce açık onay PR'ları birleşir ya da POLICY-BRANCH-START
  "kural PR'ından önce **açılmış** PR'ların satırları eski kuralda" istisnasını araçla ölçülebilir biçimde (ör. satır
  commit'i politika commit'inden önce tarihli **ve** `merge^1` politika commit'inin atası değil) tanımlar — basit yol (1)+(3a).

### G-4 · DÜŞÜK · F-10 yarım: aynı paketin sonraki onay satırı main'e girince eski dilim kaydı yine ölçülemiyor (gerileme değil)
- Dosya: `pack_approval.py:1459-1465` (K4 onay öneki her zaman `ctx.main`'e göre; `anchor` yalnız `REVIEWS` önekini
  taşır, `:1471`); `acceptance_record.py:383-389`; CONTRACT §7/7 "kabul kaydının önek denetimi … merge-base'ine göre".
- Senaryo: DL-1 kaydı c1'de (satırlar [A1]); modül içinde v2 → [A1, A2]; modül PR'ı birleşir; DL-1 kaydı yeniden koşulur →
  "bu ref, main'deki bu paketin onay / iptal satırlarını aynı sırayla içermiyor" → "onay dosyası güvenilmez". S-115 öncesi
  okuma da düşüyordu (eski kod `package.diff:798-799`), yani yeni değil; ama CONTRACT §7/7'nin "yeniden ölçüm" vaadi yalnız
  başka paketlerin satırları için doğru.
- Kanıt: E3a (c1 kabul anında çıkış 0) → E3b (merge sonrası çıkış 1, "aynı sırayla içermiyor"); E3c (son dilim kaydı geçer).
- Öneri: K4 önekini de `anchor`'a göre ölç (`approval_rows(root, anchor, path)` önek + bugünkü main'de **iptal** varsa ayrıca
  dur); CONTRACT §7/7'ye sınırı yaz.

### G-5 · DÜŞÜK · Kart §0 hızlı başvuru ve §1.1/3 hâlâ `row` / `card` komutlarını `--deferred`siz veriyor
- Dosya: kart `:70` (`row <paket> --author … --risk HIGH --reason …` — `--deferred --note` yok; `card <paket>`), `:583`
  ("Araç paketin riskini HIGH ölçüyorsa (`pack_approval.py card` …)").
- Senaryo: CT §0 tablosundaki komutla satır üretir → dalda bekleyen taban varken `check --deferred` ENGEL (fail-closed, E1d);
  §1.1/3'teki `card` bekleyen taban üstündeki revizyonu her zaman HIGH "ilk onay" gösterir → bağımsız son bakış her
  revizyonda koşar (muhafazakâr, yanlış geçiş yok; E1b iki kart farklı).
- Öneri: 70 ve 583'e `--deferred` (ve 70'e `--note "onayla · …"`).

### G-6 · NOT · POLICY-BRANCH-START "satır kaybeden merge bugünkü gibi sorulur" yanlış ifade
- Satır kaybı git ile kanıtlanır ve GitHub sorulmadan reddedilir (`:734-735, :751`); GitHub yalnız main'e girmiş kayıpsız
  yeniden sıralama merge'i ve S-91 istisna satırı için sorulur. "sorulur" → "GitHub'a sorulmadan reddedilir" yazılmalı.

### G-7 · NOT · `OWNER_WORD` büyük/küçük harfe duyarlı
- `pack_approval.py:98`; E5: "Onayla · CT" ve "ONAYLA" eşleşmez → bekleyen HIGH satır ENGEL (fail-closed). Kart küçük harf
  yazıyor; isteğe bağlı `re.IGNORECASE` ya da kartta "küçük harf" notu.

### G-8 · NOT · POLICY-BRANCH-START'ın dalda silinmesi bayraklanmıyor (kural yine uygulanır), main'de silinmesi her okumayı çıkış 2 yapar
- `Ctx` `:333` `branch_start(main) or branch_start(commit)`: main'de varsa dal okunmaz (E4b: kural main'in başlangıcıyla
  uygulandı, ENGEL "sahip sözü yok"; silme ayrıca bayraklanmadı). Main'de silinince E4c/E4d çıkış 2 "kural silinerek
  kapatılamaz". POLICY-START için `evaluate :1442-1443` aynı sınıfı çıkış 1 veriyor; fark yalnız kod (1/2). Fail-closed; not.

### G-9 · DÜŞÜK · F-15 ne düzeltildi ne "kalan" listesinde
- DESIGN 92-102 F-15'i anmıyor; kod aynı (`today_problems :505-506`, `evaluate :1472-1474`). Kapılar `review_negatives`
  `trail` ile kapatıyor (prompt_gate 611-614); yalnız elle `check --deferred` eksik. DESIGN "kalan" listesine yaz ya da
  deferred'da `append_only_problems(root, commit, REVIEWS, ctx.main)` koşulsuz (F-11 sonrası dal içi maliyet düştü).

### G-10 · NOT · Yeniden oynatma (F-16) hâlâ yok; nedeni S-121'de (kota)
- DESIGN 126-127 "sayısı kayda yazılır" — kayıt yok. CI işi (F-8) main'e girdikten sonra ilk push'larda sıkı okumanın
  bugünkü satırlarda ne verdiği zaten ölçülecek; kural PR'ına "yeniden oynatma: kota nedeniyle yapılmadı, CI ilk koşular
  ölçer" cümlesi yeter.

### G-11 · NOT · Kural dalı `.antigravity/rules/permission-key-standard.md`'ye de dokunuyor (`leave.*` satırı)
- `git diff --stat origin/main...HEAD`: `.antigravity/rules/permission-key-standard.md | 1 +` (MOD-0280-FU02 `leave.*`
  ad alanı). S-115 tasarımıyla ilgisiz; çekirdek kural 6 gereği PR açıklaması bunu da ilk satırda adlandırmalı (F-19 ile
  birlikte: SKILL.md + bu satır).

### G-12 · NOT · CI kotası ve çıkış 2
- `strict-check-on-main` `GH_TOKEN: github.token` → depo başına ayrı 1000 istek/saat; ortak kullanıcı kotasına dokunmaz
  (iyi). Modül PR'ı main'e girince dal içindeki yeniden sıralama merge'leri main'in atası olur → her `append_only_problems`
  yürüyüşü merge başına `_pr_merge_why` sorar (önbelleksiz, Codex F-3) → paket başına onlarca çağrı; sınırın altında.
  Çıkış 2 (kota) kırmızı sayılır ve mesaj "iptal satırı + yeniden onay" der (G-2 c).

### G-13 · NOT · «PR aç» tek sözle push + PR; istek satırı ikisini adlandırmalı; SKILL.md eski örnek
- Kart §6 691-698 istek "dal adı, sha, dilimler, sayılar — Sizden istenen: «PR aç»" — push'u açıkça yazmıyor; GIT-002 §5
  push ve PR'ı ayrı sayar. Tek sözün bilgilendirilmiş olması için istek satırına "push (sha) + PR" eklenmeli.
  `.antigravity/skills/control-tower/SKILL.md:90` örneği "evet: satırı yazar, commit, push, PR; merge sizde" eski onay
  PR'ı akışını anlatıyor; S-115 sonrası satır için push/PR yok — örnek güncellenebilir (skill değişikliği = sahip onayı).

### G-14 · NOT · Sahte `onayla` notu: kabul edilen bedel; araç başka ne ölçebilirdi
- DESIGN 45-48 bedeli yazıyor. Ölçülebilir ek izler (karar sahibin): (a) not hücresine sahibin kule sohbeti oturum
  kimliği + saat; kule takip sayfası (CT-TRK-01) aynı dizeyi "Karar" satırı olarak taşır → sahip iki kaynağı
  karşılaştırabilir; (b) satırı ekleyen commit'in `GIT_AUTHOR_EMAIL`'i `approvers.tsv`'deki hesabın e-postasına bağlanır
  (yeni sütun gerekmez: ayrı eşleme dosyası) → en azından "kim yazdı" git'ten türer; (c) ileride: sahibin kendi hesabıyla
  attığı tek satırlık "söz" commit'i (`Signed-off-by`), CT'nin commit'inden ayrı. Hiçbiri şimdi şart değil.

### G-15 · NOT · Deferred taban zinciri (`base_index`) ölçüldü: yanlış geçiş yok
- Bekleyen v1 düşerse (listede yok / `high = hayır` / notsuz) `base_index` onu atlar → v2'nin kartı başka tabana göre
  hesaplanır → "kart aynı değil" ENGEL (öz test 236 "2b"; E3 zinciri). Kart farkı ancak aynı risk ve aynı gerekçe listesi
  üretilirse kaybolur; o durumda v2 zaten geçerli tabana göre doğru sınıflanmıştır. Yeni boşluk bulunamadı.

---

## 3. Sonuç

Tur 1'in üç ENGEL'inden F-2 ve F-3 kapandı (kart–GIT-002 tutarlı; `row --deferred` CLI ve öz testlerle doğrulandı); F-1
kısmen (§7a/7 düzeldi, §7a/3'te iki eski cümle kaldı — G-1). Araç düzeltmeleri (F-4, F-5, F-10, F-11, F-12, F-13) kodda
tutarlı ve öz testleri yeşil (247/247, 187/187); F-5'in kapsamı doğru (eski main satırları düşmez; silme fail-closed).
Düzeltmelerin açtığı yeni boşluklar yanlış geçiş değil, geniş etkili yanlış ret ve süreç gürültüsü: F-8 CI işi iptal /
onaysız revizyon / kota durumlarında main'i kırmızıya çeviriyor (G-2); F-5 "açan = yazar" kuralı iki sahip hesabı (K1/K3)
ve devreden onay PR'larıyla çarpışabilir (G-3, F-6'nın cevabına bağlı). Bunlar kural PR'ından önce kartta (G-1, G-3/2)
ve CI'da (G-2) küçük düzeltmelerle kapanır; G-3/1 ve /3 sahip kararı ister.

`SONUÇ: KOŞULLU` · `açık ENGEL: 0` · koşullar: G-1 (iki cümle), G-2 (CI işinin kırmızı ölçütü), G-3 (yazar hesabı kuralı
+ F-6 cevabı + geçiş sırası).
