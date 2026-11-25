# WP-0230-DL12-PREP-WEB bağımsız E2 doğrulaması

Karar: **UYGUN — yalnız PREP-WEB transport E2 kapsamı için.** Yeni engelleyici, yüksek veya orta bulgu bulunmadı. Bu görüş CT kabul kaydı, full DL-12/no-loss/complete-detail kabulü, E4, canlı kullanım veya operasyon izni değildir.

2026-11-20. Doğrulayıcı geliştiren ajan değildir; ürün/test/CI/paket/queue/ledger dosyası, commit, push, PR veya merge değiştirmedi. İstenen model/effort gpt-6.2-astra/high; gerçek model/effort bu oturumda doğrulanabilir görünmediğinden **verilemiyor**. Başka alt ajan kullanılmadı.

Kaynak işçi ağacı `C:/Users/epenc/.codex/worktrees/pvg-dl12-prep-web/ERP-vNext-git`, dal `feature/pvg/mod-0230-dl12-prep-web`. İlk ve son gerçek git status boş; ilk/son HEAD `15791333afa89e447210b8d877dc5407693e9b5b`. Baseline `58920519a17e8f4c085738642b33e36689bc8fee`. İlk sandbox status denemesi erişim hatası verdi; dar escalation ile gerçek temiz status ölçüldü. İşçi ağacına generated/bin/obj dahil hiçbir yazım yapılmadı.

AGENTS.md, AGENTS-REFERENCE §5, testing-agent, §6.1 daima kuralları, ortam/config/log/kanıt kuralları, domain-config ve pack'in Edit Keeps Stored Data / connected-test / DL-12 preparation bölümleri okundu. Exact ürün prompt'u `WP-0230-DL12-PREP-WEB-v1.2.md@433aeed1b3a04c3bdc0159fdf7274ea566314b7c`; passed normal dispatch kaydı `dispatch-gate-v1.2.txt@ce02d1828479026aa81e510d9f1e6ae7e43fc15c` çıkış 0. Pack SHA256 `186ad9675e2f5219d240080f93012c54f266561f6ce70451f96aaeddb242aa01`, ready-for-dev / approved; operasyon kapalı. Rev20 sahip ve ROUND-02/ROUND-03 kararları okundu. Yeni kimlik/keşif veya paket yazımı bu doğrulamanın kapsamında değildir.

| Kontrol | Gerçek sonuç / kanıt |
|---|---|
| Değişen yollar | Tam üç izinli yol: module Web test, Web test csproj, phase1 workflow. 420 ekleme / 3 silme; test dosyası gerçek diffi 411 satırlık değişim. Tam diff `worker-exact.diff`. |
| csproj | Tek PVG Api ProjectReference, yalnız `Aliases="pvgapi"`; ek satır çıkarılınca baseline baytları birebir. API host alias ile gerçekten derlendi. |
| Workflow | Yalnız pinned `actions/setup-node@49933ea5288caeca8642d1e84afbd3f7d6820020`, Node 22/npm cache/frontend lock ve `frontend/Diten.Web` içinde `npm ci --no-audit --no-fund`. İki adım çıkarılınca baseline birebir: triggers, permissions, concurrency, diğer adımlar ve gate komutu aynı. `static-scope-audit.json`. |
| Üretim değişmezliği | MVC, _Form Razor, form.js, PVG host, package.json, package-lock Git LF blob hashleri baseline ile aynı. Disk CRLF/LF farkı ayrı ölçüldü. İlk/son dokuz kaynak disk hash'i aynı; `source-before.json`, `source-after.json`. |
| Git whitespace | `git diff --check 58920519… 15791333…` çıkış 0. |
| Araçlar | Portable .NET SDK 8.0.428, Node v22.23.5. 6 fiziksel / 12 mantıksal çekirdek; 16:29:29+04 anında CPU yükü %14. `environment.json`, sürüm logları, `load-during.json`. |
| Bağımsız normal koşu | **62/62 passed, 0 failed, 0 skipped**, çıkış 0. Eski 49 genişletilmiş vaka korunmuş; yeni 13 vaka = iki olumlu, iki token-negatif, dokuz runtime/bridge/receipt/update-negatif. `test-result.json`, `independent.trx`, `independent.log`. |
| Süre | Temiz derleme dahil komut 244.800 saniye; test çıktısı 3 dakika 12 saniye. İşçi 3:24 iddiası tekrar edilmedi; bağımsız süre ayrı ölçüldü. |
| Olumlu transport | İki normal vaka da API detail reads=1, updates=1, store version=1→2; gerçek store reread'de browser-edited narrative ve ürün alanı doğrulandı. |
| Token retleri | MissingToken ve InvalidToken gerçek MVC 400; observer POST=1, Gateway/API update=0, store version=1. Her iki outer vaka GREEN. |
| Runtime negatifleri | MissingNode, MissingJsdom, StartFailure, Timeout, BridgeFailure, EarlyEnd, Malformed, MissingReceipt, SuppressUpdate: her biri aynı connected fixture'da beklenen BrowserFailure, sıfır update ve version=1; dokuz outer guard GREEN. |
| Kapanış | Normal 12 + iki bağımsız mutantın iki Node PID'i = 14 kayıtlı tarayıcı PID'i. Bunlar ve 10 kök/gözlenen süreç PID'inin son sorgusunda **0 canlı eşleşme**. Doğrulayıcı süreç öldürmedi; fixture kendi çocuklarını finally'de reap etti. `independent-cleanup.json`. |

Bağımsız koşu işçi HEAD'inin yeni dış repository-shaped `git archive` kopyasında çalıştı: `C:/Users/epenc/AppData/Local/Temp/pvg-web-independent-1795177566591193300/snapshot`. Kaynaklar git blob LF baytlarıdır. İlk/son işçi raw hashleri değişmedi. Node/jsdom önceden kurulu node_modules bu kopyaya kopyalandı; bağımsız doğrulayıcı npm kurulumu yapmadı. İşçinin gerçek `npm ci` ve restore logları incelendi ve kopyalandı (`worker-npm-ci.log`, `worker-restore.log`). Bu bağımsız koşuda npm-ci yeniden kurulmuş iddia edilmiyor. Koşu/mutant kaynakları dış scratch'tadır; ham log/TRX bu izinli CT audit klasörüne doğrudan yazıldı, dış temp'ten sonradan kopyalanmış gibi gösterilmedi.

Restore gerçek `obj/project.assets.json.packageFolders` yolundan ölçülmüş görev NuGet cache'i ile, boş yerel feed ve NuGetAudit=false kullanarak çevrimdışı yapıldı, çıkış 0 / 2.906 saniye. Bu değer yalnız restore invocation içindir; CI dosyasına/global feed'e/config'e yazılmadı. DOTNET_ROOT / DOTNET_CLI_HOME / PATH / ilk çalıştırma / telemetri / certificate değerleri yalnız alt süreç ortamına verildi. Global PATH, feed, profil veya sertifika değiştirilmedi. Gerçek app/dev sunucusu ve 5000/5001/5011/27017 açılmadı; TestServer kullanıldı.

```text
dotnet restore frontend/Diten.Web.Tests/Diten.Web.Tests.csproj --source <fresh-task-temp>/empty-feed --ignore-failed-sources -p:NuGetAudit=false
dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~PvgCaseIntakeTriageWebTests --logger trx;LogFileName=independent.trx --results-directory <CT-audit-directory>
```

Argüman sınırları ve portable executable'ın tam yolu `prerequisites.json` / `test-result.json` içinde gerçek argv dizileriyle kayıtlıdır. Normal koşu `BuildProjectReferences=false` kullanmadı; Web ve PVG gerçek source'dan yeni kopyada derlendi. Mutantlar yalnız yeniden test assembly derlemesinde bu seçeneği kullandı.

Kaynak zinciri bağımsız okundu: `PvgCaseIntakeTriageWebTests.cs@15791333…:857` gerçek aliased PvgApiHost TestServer'ını kurar; :865 gerçek InMemory store'u DI'dan çözer; :869–879 Razor Edit ve antiforgery pair'i alır. :1178 üretim form.js dosyasını JSDOM içinde eval eder, :1193 gerçek submit event'ini dispatch eder; :1172 `Array.from(options.body.entries())` gerçek FormData'nın ad/değer/sıra/repetition/empty/whitespace dizisini gönderir. :1084 receipt'i kontrol eder, :1085–1097 girdileri herhangi bir trim/filter/dedup/token tamiri olmadan sırayla multipart'a ekler, :1104 gerçek MVC HttpClient'a gönderir. Üretim MVC UpdateDraft :144–163 gerçek binding/ToPayload ile PUT üretir; test bridge'i replacement PUT üretmez. ConnectedGateway :962–978 yalnız Gateway route çevirisini yapar ve gerçek API handler'a delege eder; gerçek PVG endpoint UpdateDraftAsync ve application ReplaceAsync/store çalışır. :884–914 gerçek store reread ve version/narrative/product assertion'larıdır. Canned Wire eski bağımsız testlerde kalmıştır; yeni connected path onu kullanmaz.

Dört ayrı token koruması vardır: tarayıcı header ve FormData member equality/presence :1089–1092; MVC'ye fiilen girmiş header ve form member equality/presence :902–903 (observer :981–1003). Header tek başına kaybolunca MVC kalan form token'ıyla geçebilse dahi ilgili assertion RED olur. Token/JWT/cookie değeri rapora yazılmadı.

İşçinin yedi dış scratch sabotajı yeniden salt okunur denetlendi. Her saved source normal commit'in yalnız belirtilen mutasyonudur (CRLF normalize edilerek içerik karşılaştırıldı), raw source/TRX/log SHA256'ları metadata ile birebir ve her log gerçek `Diten.Web.Tests -> …dll` derlemesi içeriyor. TRX'lerde birer gerçek failed vaka var; compile failure kanıt sayılmadı. Makine detayı `worker-evidence-audit.json`, kopyalar `worker-sabotages/` altındadır.

| İşçi sabotajı | Gerçek assertion/çalışma zamanı RED nedeni |
|---|---|
| receipt bypass | Outer Assert.Throws: no exception was thrown |
| canned update HTTP200 | BrowserFailure: SuppressUpdate |
| MVC header drop | MVC request token header differs from Razor |
| MVC form member drop | MVC form token member differs from Razor |
| InvalidToken header repair | Assert.Equal: expected 400, actual 200 |
| Browser header drop | Browser request token header differs from Razor |
| Browser form member drop | Browser form token member differs from Razor |

İki kritik mutasyon ayrıca kendi yeni scratch kopyamda yeniden derlenip çalıştırıldı; işçi RED iddiası bu iki testte bağımsız tekrarlandı:

| Bağımsız mutant | Sonuç | Source SHA256 | TRX SHA256 |
|---|---|---|---|
| canned200 | Çıkış 1, 1 failed / 0 skipped, 22.082 s. Gerçek update yok: BrowserFailure SuppressUpdate, update=0/version=1. | `bbf92f8d59b7662b8f5090780c1da38ed9f12d4f1ca10afdc6a058d010f68772` | `c95bc72e915c8fcdcc95c294ba969f4de9a6e81e0cfe0e8c60e78d0e1ece04c2` |
| tokenrepair | Çıkış 1, 1 failed / 0 skipped, 20.204 s. Bozuk browser token header'ının bridge'de tamiri gerçek MVC'yi 200'e geçiriyor; assertion expected400/actual200 ile RED. | `6eb0b4b12430180364870259db981c2fd9d4ebc2331c463ad3fcab10173eac90` | `24038ee101e69006bcde14c30fd27cf20d8f894d598e1701d8f71f76d5fa513e` |

Normal bağımsız TRX SHA256 `8a07396ceb66120190b20d99d9ee7ac0d51b6e6c4af42ab4724bed1e1d184232`; log SHA256 `bed9246947989945a504d0739610b69b68328c211174a5c2ba34feeb66009e39`. Tam mutant argv/log/error/counters dizileri `independent-mutants.json` içindedir. İşçi kanıtındaki normal TRX hash'i transport JSON'u ile aynı; yedi mutant / 13 connected / dokuz runtime-negative dizi sayıları `worker-evidence-summary.json` ile tekrar sayıldı.

Bilinen ana feature eksikleri açık: normal iki vakada **Untouched reporter contact preserved=False; Untouched evidence list preserved=False**. Bu equality'ler PREP içinde sahte doğru beklenen değerlerle sabitlenmemiştir; yalnız gözlem yazılır (:910–914). Complete-detail ve no-loss final acceptance ölçüleri, optional/null/empty/CRLF/duplicate array edge corpus'u, source-reference CR/LF ve izinli başarılı minimal-list sızıntı kontrolü ana WP-0230-DL12'de kalır. Bu rapor bu alanların düzeldiğini veya geçtiğini söylemez; seed'de tek product/evidence olması final array corpus'u kanıtlamaz.

Gerçek GitHub Linux Phase 1, tüm repo mimari testleri, canlı gateway/tarayıcı akışı, Mongo store ve operational/validation koşulları bu WEB E2 doğrulamasında çalıştırılmadı. PREP-WIN bağımsız sonucu bu raporun konusu değildir. Final tek PR Linux CI kapanış kapısı ve ana DL-12/E4 kabul şartları korunur. Doğrulayıcı yalnız bu CT audit dizinini yazdı; işçi commit/HEAD/status ve dokuz disk kaynak hash'i ilk/son aynı kaldı. Kayıtlar CT tarafından henüz commit/push edilmemiş yerel doğrulama artefaktlarıdır.
