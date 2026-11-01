@[.antigravity/agents/devops-agent.md]
WP: PSS-UI-P1-05 · Prompt PSS-UI-P1-05-P v1.1
Repository: /Users/alitufanoglu/ERP-vNext · Branch: feature/pss/pss-ui-p1-05 · Expected HEAD: 5d2774ae973f65797b0d67e75fee611ee83baeef · Worktree: /Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-p1-05
Risk: HIGH · §9.1 sinyalleri: yok
Görev sınıfı: ürün · Paket: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md
İzinli yollar: scripts/uic_changed_pages.py, tests/ui-compliance-baseline.tsv, docs/records/audits/2026-10/mod-0286-uic-ci-01/p1-05-kanit-01/**, docs/records/audits/2026-10/pss-ui-p1-05-rapor-01/**
Korunan yollar: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md, execution/**, .antigravity/**, .github/**, AGENTS.md, CLAUDE.md, services/**, frontend/**, gateway/**, scripts/run_record.py, scripts/acceptance_record.py, scripts/pack_approval.py, scripts/prompt_gate.py, scripts/ct_queue.py, scripts/run_phase1_gates.sh, tests/known-failures.tsv, docs/records/audits/2026-10/mod-0286-uic-ci-01/uic-tek-seviye-kanit.sh, docs/records/audits/2026-10/mod-0286-uic-ci-01/uic-tek-seviye-kanit-02.sh, docs/records/audits/2026-10/mod-0286-uic-ci-01/uic-tek-seviye-oracle.py, docs/records/audits/2026-10/mod-0286-uic-baseline-01/**, docs/records/decisions/**, docs/records/reviews/**, docs/reference/**, docs/roadmap/backlog/product-backlog.md, frontend/Diten.Web/Views/Shared/_Layout.cshtml
Kapsanan dilim: DL-3

Önce oku (sırayla):
1. execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md
2. AGENTS.md
3. execution/domains/platform-shared-services/domain-config.md
4. .antigravity/rules/git-safety.md
5. .antigravity/rules/cross-module-change-consent.md
6. docs/records/decisions/2026-10/pss-mod0286-tek-seviye-borc-01.md
7. docs/records/decisions/2026-10/pss-mod0286-taban-01.md
8. docs/records/audits/2026-10/mod-0286-uic-ci-01/uic-tek-seviye-kanit-02.sh
9. docs/records/audits/2026-10/mod-0286-uic-ci-01/uic-tek-seviye-oracle.py
10. scripts/uic_changed_pages.py
11. tests/ui-compliance-baseline.tsv
12. scripts/run_record.py
13. docs/guides/operations/control-tower-sop.md

Sözleşme yüzeyleri:
- API: N/A — DL-3 uç nokta kurmaz; paket "Gateway / API Routing Decision" bölümü P1 için Gateway değişikliğinin gereksiz olduğunu yazar (paket satır 798)
- İzin: N/A — paket "Authorization Convention" bölümü P1 için izin anahtarı tanımlamaz; DL-3 salt okunur CI token'ıyla koşar (paket satır 785)
- Test: «exit: 0 = no red outside the baseline; 1 = at least one `YENİ KIRMIZI`; 2 = tool error» (execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md:393), «the CI job never runs that mode» (execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md:460), «land in **one** pull request, none before the others» (execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md:825)
- Sabotaj: «a commit adding `spinner-border` once to `Views/Tasks/Create.cshtml`» (execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md:825), «a commit adding `alert(` once to `wwwroot/assets/js/Recall/cases.js`» (execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md:825)

Keşif: inceleme gerekli · rapor docs/records/audits/2026-10/pss-ui-p1-03-discovery-01/MOD-0286-ui-design-system-page-pattern-governance.txt@b72aff010 · sha256 47c9fc6f8c723666728bcd416385bd9fcabce4f89321e819fe71946c742f39e6
Keşif kararı 1: parent — Blueprint 8.8 satırı MOD-0286 'UI Design System & Page Pattern Governance' bu işin modülüdür; iş onun DL-3 diliminin yeniden açılmasıdır, yeni modül açılmaz
Keşif kararı 2: parent — SoR_Map'te page patterns, wizard templates, component standards sahibi MOD-0286'dır; bu iş bu nesneler için yalnız CI denetiminin kapsamını genişletir; P2 bu paketten çıktı (D-4)
Keşif kararı 3: parent — modül kaydındaki MOD-0286 satırı bu işin kimliğidir; kayda yazılmaz
Keşif kararı 4: parent — MOD-0286 paketi rev 15 (sha256 0c53533e…, main'de onaylı, PR #312 merge 819cc642a, onaylayan bestepullukcu-alt) bu işin yetki paketidir; DL-3 gerekli kanıt seviyesi E2; bu iş paketi değiştirmez
Keşif kararı 5: parent — PSS-UI-P1-01 (SUPERSEDED) DL-1 satırıdır; bu işle ilgisi yok, yeni iş açılmaz
Keşif kararı 6: parent — PSS-UI-P1-02 (SUPERSEDED) DL-2 satırıdır; bu işin denetlediği kontrol listesidir, değiştirilmez
Keşif kararı 7: child — PSS-UI-P1-04 (DL-3 ilk teslim) main'de; PSS-UI-P1-05 onun seçicisini ve tabanını paket AC-21'e göre genişletir

Hedef ajan giriş alanları (§17.4 tablosunda devops-agent satırı yok; Control Tower'ın verdiği alanlar):
- İş türü: depo aracı (Python standart kütüphane + git) ve tek bir TSV veri dosyası. Docker, Gateway, Ocelot, servis, port, MongoDB yok; `.github/workflows/**` yazılmaz.
- Komut sözleşmesi değişmez: dört kip `--base <ref>`, `--self-test`, `--check-baseline`, `--draft-baseline`. Çıktı dili P1-04'teki gibidir (`<DURUM> <page> CI-<n> <instance>[ <count>][ +<d>]`); tek seviye sayfada `<page>` = `{Module}` (`/` yok). Yeni satır yalnız `eşlenmemiş sayfa JS'i, sayfa kontrolü yok (FU-18) <depo köküne göre yol>`.
- Karar sabiti: FU-16 satırlarının `reason` hücresi birebir `sahip kararı tek-seviye-borc-01 (tek seviye sayfalar, 2026-10-31)`. Taslak kipi bunu yazar; `FU-16` satırında `REASON` sabiti kullanılmaz. Taslak kipinde FU-16 `owner` = `belirsiz — sahip belirleyecek` (`UNKNOWN_OWNER`), `AREA_OWNER` tek seviye sayfaya uygulanmaz (paket satır 454-459).
- Sahip tablosu yalnız karar kaydındadır (madde 4); seçiciye gömülmez. Tabana yazılırken karar kaydındaki tablodan doldurulur.

NE: Paket rev 15 AC-21 (a)'nın kod tarafını tek commit dizisinde teslim et: `scripts/uic_changed_pages.py`'de okuyucu (`{Module}` sayfa anahtarı), eşleme (tek seviye sayfa, FU-18 satırı, klasör düzeyi `_ViewStart.cshtml` / `_ViewImports.cshtml`), `evaluate_page` (yalnız doğrudan dosyalar), kaldırılan sayfa ve yeni yüzey (yalnız doğrudan dosyalar), taslak kipi (FU-16, sahip, reason) ve `--self-test` durumları; `tests/ui-compliance-baseline.tsv`'ye FU-16 satırları. Ardından Control Tower'ın kanıt betiğini bir kez kayıtlı koş ve yapılandırılmış rapor yaz. CI koşusu, PR ve kabul senin değildir.
NEDEN: Sahibin isteği "kör nokta" (paket rev 15, main'de onaylı): 162 tek seviye sayfa dosyası hiçbir CI kontrolünden geçmiyordu (`scripts/uic_changed_pages.py` `map_path` hepsini "paylaşılan dosya" sayıyor); yeni bir sayfa ya da eski bir sayfaya eklenen ihlal yeşil geçiyor. Eski sayfaların bugünkü kırmızıları kimseyi durdurmamalı: bu yüzden satırlar okuyucu ve eşlemeyle aynı PR'da girer (sahip kararı `tek-seviye-borc-01`, karar kaydı). Tasarım paket satır 325-351 (bölüm 1), 450-462 (bölüm 2), 485-499 (CI tablosu), 523-609 ("DL-3 tek seviye sayfalar"), 744-757 (Validation Rules); ölçütler AC-21 satır 825; hata durumları satır 777-778. Control Tower kanıt betiği `uic-tek-seviye-kanit-02.sh` bunları tek koşuda ölçer ve bugün 3/10 ile kırmızıdır (CT-2, AC-21c, REG geçer; ölçüldü).
NASIL:
- Ön kontrol (worktree kökünde): `git rev-parse HEAD` = 5d2774ae973f65797b0d67e75fee611ee83baeef, `git branch --show-current` = feature/pss/pss-ui-p1-05, `git status --porcelain --untracked-files=all` boş, paket sha256 = 0c53533e3e2434fbd8abe615f814d037c3c390b06625ee822358871958aed102. main'i birleştirme, dal değiştirme.
- Yük kuralı: `uptime` ile 1 dakikalık yükü ölç; `--draft-baseline`, `--self-test` ve kanıt betiği koşularından önce yük 12'nin üstündeyse 15 dakikaya kadar bekle, düşmezse DUR ve raporla.
- Adım 1 — seçiciyi değiştir (paket metni harfiyen; satır numaraları Expected HEAD'deki kodu gösterir):
  a. Eşleme `map_path` (`:840-849`): paket bölüm 1 (satır 325-351). `Views/{Module}/<dosya>` (klasörde doğrudan, `{Module}` `NOT_PAGE_AREAS` dışında) → sayfa `{Module}`; dosya türü fark etmez. `wwwroot/assets/js/{Module}/<dosya>.js` doğrudan klasörde → `Views/{Module}/` HEAD'de ya da tabanda doğrudan dosya taşıyorsa sayfa `{Module}`. İki seviyeli kural değişmez. FU-18 satırı: `pages/**`; bölüm 1 (2), (2b), (3). Bu dört yol genel paylaşılan satıra düşmez. Kök `Views/_ViewStart.cshtml`, `Views/_ViewImports.cshtml` ve `js/shared` gibi `Views/{X}/` karşılığı olmayan yollar paylaşılan kalır. Klasör düzeyi `_ViewStart.cshtml` / `_ViewImports.cshtml` hem sayfa `{Module}` dosyasıdır hem paylaşılan satırını yazar (satır 346-351).
  b. `evaluate_page` (`:481-485`): anahtar `/` içermiyorsa yalnız `Views/{Module}/` altında doğrudan dosyaları ve `wwwroot/assets/js/{Module}/*.js` doğrudan dosyalarını okur. Çalışan kontroller satır 579-581: CI-1, CI-2, CI-4, CI-5, CI-6, CI-7, CI-8, CI-9, CI-10, CI-11 (yeni yüzey), CI-13 eskisi gibi; CI-3 koşmaz: `Views/{Module}/_DataTable.cshtml` olan sayfada birebir tek satır `ölçülmedi {Module} CI-3 - doğrulayıcı alansız sayfayı desteklemiyor (FU-17)` (paket satır 487; asla kırmızı, asla taban satırı), CI-12, CI-14, CI-15 yok.
  c. Sayfa kaldırıldı ve yeni yüzey (`:801`, `:806`; `:799`'daki `page.split` anahtarı `/` içermeyen sayfada kırılır): tek seviye sayfa için yalnız doğrudan dosyalar okunur; `has_dir` bütün dizini okur ve bu sayfada kullanılmaz (satır 333-336, AC-21 (e2), (f)). İki seviyeli sayfanın dalı değişmez.
  d. Okuyucu `instance_problem` (`:281-325`): sayfa anahtarı `{Module}` kabul edilir; boş ya da bozuk anahtar (ör. `Foo Bar`, `A/B/C`) çıkış 2. Tek seviye anahtarlı CI-3 satırı çıkış 2 (paket satır 487). Tek seviye satırın instance'ı alt klasörde ise çıkış 2 (`Meetings · CI-2 · Views/Meetings/Report/x.cshtml`, `Meetings · CI-1 · Views/Meetings/Report/Index.cshtml:yok:Layout`). `fu` için `FU-16` geçerlidir. İki seviyeli satırların kuralı değişmez.
  e. Taslak kipi (`draft` `:868-891`, `owner_of` `:373-382`): sayfa kümesi 19 tek seviye sayfayı da okur; tek seviye sayfa satırı `FU-16`, `UNKNOWN_OWNER`, Karar sabiti. İki seviyeli satırlar aynen kalır.
  f. `--self-test`: yeni durumların adı şu kimlikleri içerir (kanıt betiği `grep` ile arar; birden çok ise her biri): `AC-21g01` görünüm dosyası doğrudan klasörde; `AC-21g02` JS doğrudan klasörde, doğrudan görünüm dosyası var; `AC-21g03` JS doğrudan klasörde, klasörde doğrudan görünüm yok; `AC-21g04` aynı klasörde tek ve iki seviyeli sayfa; `AC-21g05` `NOT_PAGE_AREAS`; `AC-21g06` `pages/**` FU-18; `AC-21g07` `js/{Module}/{Sub}/` görünüm klasörü yok FU-18; `AC-21g08` yalnız alt klasörü olan alanda JS, doğrudan ve alt klasörde FU-18; `AC-21g09` klasör düzeyi `_ViewStart.cshtml` sayfa dosyası ve paylaşılan satırı; `AC-21g10` `{Module}` anahtarlı taban satırı kabul; `AC-21g11` boş ya da bozuk anahtar çıkış 2; `AC-21g12` alt klasör instance'ı çıkış 2 (CI-2 ve CI-1); `AC-21g13` yalnız alt klasör sayfası olan klasörde tek seviye yeni yüzey (CI-11 koşar); `AC-21g14` alt klasör sayfası kalırken tek seviye sayfa kaldırıldı. Her `AC-21gNN` durumu geçici depoda gerçek koşu ve sonuç denetimi yapar; sabit doğru dönen durum yok. `AC-21g02` ve `AC-21g03`, `Views/{Module}/`'de doğrudan görünümün yalnız tabanda olduğu dalı; `AC-21g09`, `_ViewImports.cshtml`'i de denetler. Mevcut 67 durum geçmeye devam eder.
  g. Seçici depoda dosya yazmaz; denemeleri `PYTHONDONTWRITEBYTECODE=1` ile koş.
- Adım 2 — taban: yük kuralından sonra `python3 scripts/uic_changed_pages.py --draft-baseline` çıktısından yalnız `fu` = `FU-16` satırlarını al; `owner` hücresini karar kaydının tablosundan doldur; dosyanın SONUNA ekle. Mevcut satırları silme, değiştirme, sıralama (`git diff --numstat` silinen 0). Elle satır ya da sayı değiştirme yok. Taslakta FU-16 dışı satırların tabandan farkı senin işin değildir (main ilerledi): raporda say, düzeltme.
- Adım 3 — commit: yalnız açık yollarla stage (`git add scripts/uic_changed_pages.py tests/ui-compliance-baseline.tsv`; `git add -A` ve `git add .` yok), mesaj `feat(pss): MOD-0286 DL-3 — tek seviye sayfalar: okuyucu, eşleme ve FU-16 satırları (PSS-UI-P1-05)`. Push yok. Deneme koşusu bir hata gösterirse düzeltme için en çok iki ek commit serbesttir (toplam en çok üç); her biri yalnız bu iki dosya.
- Adım 4 — deneme koşusu (kayıtsız): depo dışı boş dizin aç (`O=$(mktemp -d)`) ve depo kökünden `sh docs/records/audits/2026-10/mod-0286-uic-ci-01/uic-tek-seviye-kanit-02.sh "$O" "$(git rev-parse HEAD)"`. Beklenen: çıkış 0, `zorunlu geçti 10 / 10`. Bir `kaldı` varsa nedenini `$O`'daki ara dosyalardan bul; seçici ya da tabandaki bir hataysa düzelt, Adım 3'teki ek commit ile yeniden dene; kanıt betiğinde ya da bağımsız ölçümde hata görürsen onları değiştirme: DUR ve satırıyla raporla.
- Adım 5 — kayıtlı koşu (temiz ağaçta, son commit'ten sonra): `python3 scripts/run_record.py run --out docs/records/audits/2026-10/mod-0286-uic-ci-01/p1-05-kanit-01 -- sh docs/records/audits/2026-10/mod-0286-uic-ci-01/uic-tek-seviye-kanit-02.sh docs/records/audits/2026-10/mod-0286-uic-ci-01/p1-05-kanit-01 <son commit'in 40 haneli sha'sı>`; ardından `python3 scripts/run_record.py verify docs/records/audits/2026-10/mod-0286-uic-ci-01/p1-05-kanit-01/run-record-v1.json` çıkış 0. Koşu dizinini commit etme; Control Tower kendi commit'inde kaydeder.
- Adım 6 — rapor: `docs/records/audits/2026-10/pss-ui-p1-05-rapor-01/RAPOR.md` (commit etme).
- Kanıt düzeyi: E2 (self-test + kayıtlı sabotaj koşuları).
YAPMA:
- Paket dosyasını değiştirme: onay satırı sha256 0c53533e…'e bağlı. Pakette bulduğun hata yalnız rapora "paket revizyonu girdisi" olarak yazılır.
- `.github/**`, `.antigravity/**`, `frontend/**`, `services/**`, `gateway/**`, `execution/**`, öteki `scripts/**`, `tests/known-failures.tsv`, `docs/**` (yalnız İzinli iki kayıt dizini hariç): dokunma. Bir sayfanın kırmızısını düzeltme (XMC-001; her satırın sahibi var).
- Kanıt betiğini ve bağımsız ölçümü değiştirme, kopyalayıp yeniden yazma, başka bir betikle koşma.
- `pages/**` için ad tablosu yazma (FU-18 kararı paket satır 562-578).
- CI kipinin bütün sayfaları koşmasına izin veren bir yol yazma; `--draft-baseline` CI komut satırından ulaşılamaz.
- Ağ, servis, .NET derlemesi, tam test süiti yok; seçici yalnız Python standart kütüphanesi ve git kullanır.
- `--self-test` ve deneme koşuları depo dışında; depoda kalıcı geçici dosya bırakma. run_record reddederse `git clean`, `git reset`, `git stash`, `git checkout` ile ağacı zorla temizleme; dur ve raporla.
- main'e commit / push, PR, dal değiştirme, main'i birleştirme yok. Kişi adı, e-posta, gizli bilgi yazma.
DOĞRULA: (worktree kökünde; "son commit" = Adım 3'ün son commit'i)
1. Kapsam: `git diff --name-only 5d2774ae9 <son commit>` yalnız `scripts/uic_changed_pages.py` ve `tests/ui-compliance-baseline.tsv`; `git log --oneline 5d2774ae9..<son commit>` en çok üç commit; paket sha256 başta ve sonda 0c53533e….
2. Kipler: kipsiz çıkış 2; `--self-test` çıkış 0 (toplam ve yeni durum adları raporda); `--check-baseline` çıkış 0 ve CI başına satır / eşleşme toplamı; `--base HEAD` çıkış 0 ve son satır `süre: … s`; kanıt koşusunun `ac21b/stdout.txt` dosyasında FU-17 satırı birebir (raporda alıntı).
3. Taban: FU-16 satır sayısı (paket 134, CT ölçümü 2026-10-31: 134), CI başına satır ve eşleşme (CI-1 81, CI-2 4 / 12, CI-4 2 / 2, CI-6 35 / 73, CI-7 8 / 24, CI-8 1 / 1, CI-9 1, CI-10 2 / 4); `awk -F'\t' '$6 == "FU-16"' tests/ui-compliance-baseline.tsv | grep -c 'taban-01'` = 0; silinen satır 0.
4. Kayıtlı koşu: `run_record.py verify` çıkış 0; kayıttaki commit = son commit; ham çıkış 0; `stdout.txt`'de `zorunlu geçti 10 / 10`; sabotaj koşularının çıkışları ve adlı satırları raporda.
5. `git diff --check` temiz; sonda `git status --porcelain --untracked-files=all` yalnız `p1-05-kanit-01/` ve `pss-ui-p1-05-rapor-01/` altındaki yeni dosyalar.
Durma koşulları:
- HEAD, dal ya da başlangıç temizliği tutmuyor; paket sha256 0c53533e… değil → DUR.
- Paketin DL-3 metni bir noktada iki farklı davranışa izin veriyor ya da komut sözleşmesiyle çelişiyor → seçme, DUR ve satırıyla raporla.
- Bir kontrol `.antigravity/**`, `frontend/**` ya da başka bir yasak yolda değişiklik gerektiriyor → DUR, sor (XMC-001).
- Deneme ya da kayıtlı koşuda üç commit içinde `10 / 10` olmuyor → DUR ve raporla; kanıt betiğinde hata görürsen betiği değiştirmeden DUR.
- Taslağın FU-16 satırları bağımsız ölçümden farklı → tabanı commit etmeden farkı incele; seçici hatasıysa düzelt, ölçüm hatasıysa DUR ve raporla.
- Yük 15 dakika boyunca 12'nin üstünde → DUR ve raporla.
- main'e commit / push, PR ya da dal değişikliği gerekirse → DUR.
Rapor formatı: SOP §22 yapılandırılmış rapor (Agent Verdict · Branch / HEAD · Worktree status · Changed files · Golden/Contract flow · Sub-flows · Failure paths · Tests · Persistence evidence · Security/RBAC/Tenant evidence · Audit/Evidence · Observability · Migration/Rollback · Decisions · Blockers · Known gaps · Out-of-scope changes), hem sohbette hem docs/records/audits/2026-10/pss-ui-p1-05-rapor-01/RAPOR.md dosyasında. Zorunlu içerik: (1) commit'ler ve değişen dosyalar; (2) dört kipin çıkışları ve yeni self-test durumları; (3) taban: FU-16 satır sayısı, CI başına toplam, taslakta FU-16 dışı satırların tabandan farkı (sayı); (4) deneme ve kayıtlı koşu: ham çıkış, `zorunlu geçti n / 10`, her `sonuç=` satırı; (5) paket revizyonu girdileri; (6) ölçülmeyenler ve nedenleri; (7) kanıt koşularının bıraktığı geçici klonların yolları (silme; temizlik Control Tower'ın). Senin PASS'in kapanış değildir: doğrulama senden farklı bir ajanın, kabul Control Tower'ın tek kabul kaydınındır.
