@[.antigravity/agents/devops-agent.md]
WP: PSS-UI-FU01-01 · Prompt PSS-UI-FU01-01-P v1.0
Repository: /Users/alitufanoglu/ERP-vNext · Branch: feature/pss/pss-ui-fu01-01 · Expected HEAD: c412e40efe70c61f3ff74e8d0de07b15f91042d0 · Worktree: /Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-fu01-01
Risk: HIGH · §9.1 sinyalleri: yok
Görev sınıfı: ürün · Paket: execution/domains/platform-shared-services/module-packs/MOD-0286-FU01-page-js-reference-mapping.md
İzinli yollar: scripts/uic_changed_pages.py, tests/ui-compliance-baseline.tsv, docs/records/audits/2026-11/mod-0286-fu01-01/fu01-kanit-01/**, docs/records/audits/2026-11/pss-ui-fu01-01-rapor-01/**
Korunan yollar: execution/**, .antigravity/**, .github/**, AGENTS.md, CLAUDE.md, services/**, frontend/**, gateway/**, scripts/run_record.py, scripts/acceptance_record.py, scripts/pack_approval.py, scripts/prompt_gate.py, scripts/ct_queue.py, scripts/run_phase1_gates.sh, tests/known-failures.tsv, docs/records/audits/2026-11/mod-0286-fu01-01/uic-fu01-kanit-01.py, docs/records/audits/2026-11/mod-0286-fu01-01/oracle-sabotaj-01.txt, docs/records/audits/2026-10/mod-0286-fu01-01/**, docs/records/decisions/**, docs/records/reviews/**, docs/reference/**, docs/roadmap/backlog/product-backlog.md
Kapsanan dilim: tamamı

Önce oku (sırayla):
1. execution/domains/platform-shared-services/module-packs/MOD-0286-FU01-page-js-reference-mapping.md
2. AGENTS.md
3. execution/domains/platform-shared-services/domain-config.md
4. .antigravity/rules/git-safety.md
5. .antigravity/rules/cross-module-change-consent.md
6. docs/records/decisions/2026-11/pss-mod0286-fu01-satir-sahipleri-01.md
7. docs/records/decisions/2026-11/pss-mod0286-fu01-satir-sahipleri-01.tsv
8. docs/records/audits/2026-11/mod-0286-fu01-01/uic-fu01-kanit-01.py
9. docs/records/audits/2026-10/mod-0286-fu01-01/uic-fu01-oracle-02.py
10. scripts/uic_changed_pages.py
11. tests/ui-compliance-baseline.tsv
12. scripts/run_record.py
13. execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md

Sözleşme yüzeyleri:
- API: N/A — paket "Gateway / API Routing Decision": uç nokta yok, Gateway değişikliği yok (paket satır 183)
- İzin: N/A — paket "Authorization Convention": uç nokta, sayfa ve izin yok (paket satır 175)
- Test: «The selector holds the three strings in one module-level dict literal named exactly `FU01_REF_PATTERNS`» (execution/domains/platform-shared-services/module-packs/MOD-0286-FU01-page-js-reference-mapping.md:69), «Worker writes only: `scripts/uic_changed_pages.py` (with `--self-test`), `tests/ui-compliance-baseline.tsv`.» (execution/domains/platform-shared-services/module-packs/MOD-0286-FU01-page-js-reference-mapping.md:128)
- Sabotaj: «`alert('x');` appended to `Platform/PersonReferences/person-reference-picker.js`» (execution/domains/platform-shared-services/module-packs/MOD-0286-FU01-page-js-reference-mapping.md:211)

Keşif: inceleme gerekli · rapor docs/records/audits/2026-11/mod-0286-fu01-01/kesif-01.txt@c412e40ef · sha256 71c31bf6e057d232c4cda41d19dbb20aa7ba94322fedde4d88a3d381cee8b3f5
Keşif kararı 1: FU — kayıt defterindeki MOD-0286-FU01 satırı bu işin kimliğidir, MOD-0286'nın Follow-up satırı; yeni modül açılmaz
Keşif kararı 2: FU — execution/domains/platform-shared-services/module-packs/MOD-0286-FU01-page-js-reference-mapping.md bu işin onaylı yetki paketidir (rev 04, d970554b…, PR #328); iş paketi değiştirmez
Keşif kararı 3: yeni — 'hr-compliance' Gateway rotası sorgudaki 'compliance' sözcüğüyle eşleşti; insan kaynakları servisidir, bu işin Gateway ve uç nokta değişikliği yoktur, ilgisi yok
Keşif kararı 4: parent — PSS-UI-P1-04 üst paketin DL-3 ilk teslimidir ve kabul edildi; bu iş onun seçicisini ve tabanını genişletir, kabulünü yeniden açmaz
Keşif kararı 5: parent — PSS-UI-P1-05 üst paketin DL-3 tek seviye sayfa teslimidir ve kabul edildi; 106 dosyanın FU-18 satırı oradan gelir, bu iş o satırı kapatır

Hedef ajan giriş alanları (§17.4 tablosunda devops-agent satırı yok; Control Tower'ın verdiği alanlar):
- İş türü: depo aracı (Python standart kütüphane + git) ve tek bir TSV veri dosyası. Docker, Gateway, Ocelot, servis, port, MongoDB yok; `.github/workflows/**` yazılmaz.
- Model · Effort (istenen): Sonnet 5.5 · high (karmaşık uygulama). Kullanılanı raporda yaz.
- Komut sözleşmesi değişmez: dört kip `--base <ref>`, `--self-test`, `--check-baseline`, `--draft-baseline`.
- Karar sabiti: `FU-19` satırlarının `reason` hücresi birebir `sahip kararı fu01-satir-sahipleri (2026-11-01)`; taslak kipi bunu tek sabitten yazar (paket bölüm 6, `SINGLE_REASON` gibi). Taslakta `owner` = `belirsiz — sahip belirleyecek`; tabana yazılırken karar kaydının TSV'sinden doldurulur; sahip tablosu seçiciye gömülmez.

NE: Paket rev 04'ün tek dilimini tek commit dizisinde teslim et: `scripts/uic_changed_pages.py`'de başvuru okuma (bölüm 1-5, 7, 9), `FU01_REF_PATTERNS`, taban okuyucu (bölüm 8), taslak kipi (bölüm 6), `--self-test` vakaları FU01-01...67 ve aşağıdaki üç ek vaka; `tests/ui-compliance-baseline.tsv`'ye `FU-19` satırları. Ardından Control Tower'ın kanıt betiğini bir kez kayıtlı koş ve yapılandırılmış rapor yaz. CI koşusu, PR ve kabul senin değildir.
NEDEN: Sahibin isteği "kalan kör nokta": otomatik UI kontrolü 106 sayfa JS dosyasında sayfa kontrolü yapmıyor, yeni ihlal kırmızı olmuyor. Paket bu dosyaları görünümlerdeki `<script src>` başvurusuyla sayfalara bağlar; eski borç 22 satırla tabana girer ki eski kırmızı kimseyi durdurmasın (sahip kararı `fu01-satir-sahipleri`). Okuyucu, eşleme ve satırlar aynı PR'da olmak zorunda (paket satır 93). Kanıt betiği bugün `zorunlu geçti 1 / 13` veriyor (ölçüldü); geçen AC-5 bir gerileme korumasıdır.
NASIL:
- Ön kontrol (worktree kökünde): `git rev-parse HEAD` = c412e40efe70c61f3ff74e8d0de07b15f91042d0, `git branch --show-current` = feature/pss/pss-ui-fu01-01, `git status --porcelain --untracked-files=all` boş, paket sha256 = d970554beef0b5e111918647effd1503c98dfb1a5ded1c84a3614ffda15eae31. main'i birleştirme, dal değiştirme.
- Yük kuralı: `uptime` ile 1 dakikalık yükü ölç; `--draft-baseline`, `--self-test` ve kanıt betiği koşularından önce yük 12'nin üstündeyse 15 dakikaya kadar bekle, düşmezse DUR ve raporla. Süreç öldürme.
- Adım 1 — seçici: paket "Owned Objects" bölüm 1-9, Validation Rules ve Failure Path metni harfiyen; satır metinleri bayt bayt. Paketin satır numaraları `0504c96ea` içindir; kodu metinle bul.
  a. `FU01_REF_PATTERNS` tip açıklamasız düz atama olur (`FU01_REF_PATTERNS = {...}`; `FU01_REF_PATTERNS: dict = ...` değil: bağımsız ölçüm yalnız düz atamayı okur). Değerler paket bloğuna bayt bayt eşit; başka kopya yok; her koşu buradan derler.
  b. Paketin yazmadığı iki durumda karar (paket satır 67 "A reference brings in nothing else" gereği): görünüme HEAD'de olmayan bir J etiketi eklenirse yalnız `başvurulan JS dosyası yok P J` basılır, `başvuru eklendi` basılmaz; J sayfanın kendi klasörüne (sınıf dışı K) taşınırsa K için `başvuru eklendi` basılmaz, K kendi klasör kontrolüyle okunur, P'nin J satırları `başvuru kalktı P J` ve `kapandı` ile kapanır.
  c. Yalnız kayıt dosyasıyla seçilen sayfada da tarama satırları basılır (paket bölüm 9 "selects a page").
  d. `--self-test` ek vakalar (FU01-67'den sonra): `FU01-68` satırın dosyası sayfanın görünümünde yalnız `@* *@` içindeki etiketle geçiyor: T-6 ile çıkış 2 (ham metin araması yetmez); `FU01-69` b'nin ilk durumu; `FU01-70` b'nin ikinci durumu. Her vaka geçici depoda gerçek koşu ve sonuç denetimidir; sabit doğru dönen vaka yok.
  e. `self_test` içinde AC-10'un adlandırdığı satırlar dışında mevcut vaka silinmez ya da zayıflatılmaz. Seçici depoda dosya yazmaz; denemeleri `PYTHONDONTWRITEBYTECODE=1` ile koş.
- Adım 2 — taban: yük kuralından sonra `python3 scripts/uic_changed_pages.py --draft-baseline` çıktısından yalnız `fu` = `FU-19` satırlarını al; `owner` hücresini `docs/records/decisions/2026-11/pss-mod0286-fu01-satir-sahipleri-01.tsv`'den doldur; dosyanın SONUNA ekle. Mevcut satırları silme, değiştirme, sıralama (`git diff --numstat` silinen 0). Elle satır ya da sayı değiştirme yok. Taslakta `FU-19` dışı satırların tabandan farkı senin işin değildir: raporda say, düzeltme.
- Adım 3 — commit: yalnız açık yollarla stage (`git add scripts/uic_changed_pages.py tests/ui-compliance-baseline.tsv`; `git add -A` ve `git add .` yok), mesaj `feat(pss): MOD-0286-FU01 DL-1 — sayfa JS başvuru eşlemesi, okuyucu ve FU-19 satırları (PSS-UI-FU01-01)`. Push yok. Deneme koşusu bir hata gösterirse düzeltme için en çok üç ek commit serbesttir; her biri yalnız bu iki dosya.
- Adım 4 — deneme koşusu (kayıtsız): depo dışı boş dizin aç ve depo kökünden `python3 -B docs/records/audits/2026-11/mod-0286-fu01-01/uic-fu01-kanit-01.py <boş dizin> <son commit tam sha> --base c412e40efe70c61f3ff74e8d0de07b15f91042d0 --decision fu01-satir-sahipleri --owners docs/records/decisions/2026-11/pss-mod0286-fu01-satir-sahipleri-01.tsv`. Beklenen: `zorunlu geçti 12 / 13`, tek `kaldı` AC-12 (CI kaydı henüz yok, kule PR'dan sonra verir); bu yüzden çıkış 1 beklenir. Başka bir `kaldı` varsa nedenini çıktı dizinindeki ara dosyalardan bul; seçici ya da tabandaki hatayı düzelt, Adım 3'teki ek commit ile yeniden dene; kanıt betiğinde ya da bağımsız ölçümde hata görürsen onları değiştirme: DUR ve satırıyla raporla. Betik yeşil yolda hiç koşmadı (işçi çıktısı yoktu); betik hatası bulmak olası bir sonuçtur, kendin düzeltme.
- Adım 5 — kayıtlı koşu (temiz ağaçta, son commit'ten sonra): `python3 scripts/run_record.py run --out docs/records/audits/2026-11/mod-0286-fu01-01/fu01-kanit-01 -- python3 -B docs/records/audits/2026-11/mod-0286-fu01-01/uic-fu01-kanit-01.py docs/records/audits/2026-11/mod-0286-fu01-01/fu01-kanit-01 <son commit tam sha> --base c412e40efe70c61f3ff74e8d0de07b15f91042d0 --decision fu01-satir-sahipleri --owners docs/records/decisions/2026-11/pss-mod0286-fu01-satir-sahipleri-01.tsv`; ardından `python3 scripts/run_record.py verify docs/records/audits/2026-11/mod-0286-fu01-01/fu01-kanit-01/run-record-v1.json` çıkış 0. Koşu dizinini commit etme; Control Tower kendi commit'inde kaydeder. Geçici klonlar 1,2 GB'tır: tam koşu en çok üç kez (deneme + kayıtlı dahil, gerekirse `--klon-var` ile aynı klon); silme.
- Adım 6 — rapor: `docs/records/audits/2026-11/pss-ui-fu01-01-rapor-01/RAPOR.md` (commit etme).
- Kanıt düzeyi: E2 (self-test + kayıtlı sabotaj koşuları).
YAPMA:
- Paket dosyasını değiştirme: onay satırı sha256 d970554b…'e bağlı. Pakette bulduğun hata yalnız rapora "paket revizyonu girdisi" olarak yazılır. Bilinen açık uçlar (desenler paket bayt bayt olduğu için dokunulmaz, raporda yeniden keşif sayılmaz): `~/assets/js/@@x.js`, `data-a="a>b"` öznitelikli etiket, `src=" @x"`, `asp-fallback-src`, öneksiz `assets/js/...`, büyük harfli yol, seçilmeyen sayfanın değişken yolu.
- `.github/**`, `.antigravity/**`, `frontend/**`, `services/**`, `gateway/**`, `execution/**`, öteki `scripts/**`, `tests/known-failures.tsv`, `docs/**` (yalnız İzinli iki kayıt dizini hariç): dokunma. Bir sayfanın kırmızısını düzeltme, JS taşıma (XMC-001; her satırın sahibi var).
- Kanıt betiğini ve bağımsız ölçümü değiştirme, kopyalayıp yeniden yazma, başka bir betikle koşma.
- CI kipinin bütün sayfaları koşmasına izin veren bir yol, bayrak ya da değer yazma; görünüm zincirini (partial) izleme.
- Ağ, servis, .NET derlemesi, tam test süiti yok; seçici yalnız Python standart kütüphanesi ve git kullanır.
- Deneme koşuları depo dışında; depoda kalıcı geçici dosya bırakma. run_record reddederse `git clean`, `git reset`, `git stash`, `git checkout` ile ağacı zorla temizleme; dur ve raporla.
- main'e commit / push, PR, dal değiştirme, main'i birleştirme yok. Dalı push etme. Kişi adı, e-posta, gizli bilgi yazma. `rm` yok.
DOĞRULA: (worktree kökünde; "son commit" = Adım 3'ün son commit'i)
1. Kapsam: `git diff --name-only c412e40efe70c61f3ff74e8d0de07b15f91042d0 <son commit>` yalnız `scripts/uic_changed_pages.py` ve `tests/ui-compliance-baseline.tsv`; `git log --oneline c412e40efe70c61f3ff74e8d0de07b15f91042d0..<son commit>` en çok dört commit; paket sha256 başta ve sonda d970554b….
2. Kipler: kipsiz çıkış 2; `--self-test` çıkış 0 (toplam, FU01-01...70 `PASS` sayısı raporda); `--check-baseline` çıkış 0 ve CI başına satır / eşleşme toplamı; `--base HEAD` çıkış 0 ve tek satır `süre: … s`; `python3 -B docs/records/audits/2026-10/mod-0286-fu01-01/uic-fu01-oracle-02.py secici <son commit>` `aynı`, çıkış 0.
3. Taban: `FU-19` satır sayısı (`oracle satirlar c412e40efe70c61f3ff74e8d0de07b15f91042d0`; 2026-11-01 ölçümü 22 satır, 92 eşleşme); `awk -F'\t' '$6 == "FU-19"' tests/ui-compliance-baseline.tsv` içinde `taban-01`, `tek-seviye-borc-01`, `belirsiz` yok; silinen satır 0; öteki `fu` satır sayıları değişmedi.
4. Kayıtlı koşu: `run_record.py verify` çıkış 0; kayıttaki commit = son commit; ham çıkış 1 yalnız AC-12 yüzünden; `stdout.txt`'de `zorunlu geçti 12 / 13` ve `AC-12 kaldı`.
5. `git diff --check` temiz; sonda `git status --porcelain --untracked-files=all` yalnız `fu01-kanit-01/` ve `pss-ui-fu01-01-rapor-01/` altındaki yeni dosyalar.
Durma koşulları:
- HEAD, dal ya da başlangıç temizliği tutmuyor; paket sha256 d970554b… değil → DUR.
- Taslakta karar tablosunda olmayan bir sayfa ya da `oracle satirlar`'dan farklı `FU-19` satırı var → tabanı commit etmeden farkı incele; seçici hatasıysa düzelt, tablo eksiği ya da ölçüm hatasıysa DUR ve raporla; sahip uydurma.
- Paket metni bir noktada iki farklı davranışa izin veriyor ya da komut sözleşmesiyle çelişiyor (NASIL 1b ve 1c dışında) → seçme, DUR ve satırıyla raporla.
- Bir kontrol `.antigravity/**`, `frontend/**` ya da başka bir yasak yolda değişiklik gerektiriyor → DUR, sor (XMC-001).
- Dört commit içinde `12 / 13` olmuyor ya da kanıt betiğinde hata görüyorsun → betiği değiştirmeden DUR ve raporla.
- Yük 15 dakika boyunca 12'nin üstünde → DUR ve raporla.
- main'e commit / push, PR ya da dal değişikliği gerekirse → DUR.
Rapor formatı: SOP §22 yapılandırılmış rapor (Agent Verdict · Branch / HEAD · Worktree status · Changed files · Golden/Contract flow · Sub-flows · Failure paths · Tests · Persistence evidence · Security/RBAC/Tenant evidence · Audit/Evidence · Observability · Migration/Rollback · Decisions · Blockers · Known gaps · Out-of-scope changes), hem son mesajda hem docs/records/audits/2026-11/pss-ui-fu01-01-rapor-01/RAPOR.md dosyasında. Zorunlu içerik: (1) commit'ler ve değişen dosyalar; (2) dört kipin çıkışları ve yeni self-test vakaları; (3) taban: `FU-19` satır sayısı, CI başına toplam, taslakta `FU-19` dışı satırların tabandan farkı (sayı); (4) deneme ve kayıtlı koşu: ham çıkış, `zorunlu geçti n / 13`, her `AC-n` satırı; (5) paket revizyonu girdileri ve kanıt betiği hataları; (6) ölçülmeyenler ve nedenleri; (7) kanıt koşularının bıraktığı geçici klonların yolları (silme; temizlik Control Tower'ın); (8) model ve effort. Senin PASS'in kapanış değildir: doğrulama senden farklı bir ajanın, kabul Control Tower'ın tek kabul kaydınındır.
