@[.antigravity/agents/testing-agent.md]
WP: WP-0230-DL12-PREP-WEB · Prompt PVG-WP-0230-DL12-PREP-WEB-P v1.0
Repository: C:/Users/epenc/Documents/Codex/2026-09-29/https-github-com-bestepullukcu-alt-erp/work/ERP-vNext-git · Branch: feature/pvg/mod-0230-dl12-prep-web · Expected HEAD: 97e0e7f92dd2efb8d8affd94294bddc84ac05a5f · Worktree: C:/Users/epenc/.codex/worktrees/pvg-dl12-prep-web/ERP-vNext-git
Risk: HIGH · §9.1 sinyalleri: sözleşme, uyum
Görev sınıfı: ürün · Paket: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md
İzinli yollar: frontend/Diten.Web.Tests/Controllers/PvgCaseIntakeTriageWebTests.cs, frontend/Diten.Web.Tests/Diten.Web.Tests.csproj, .github/workflows/phase1-gates.yml
Korunan yollar: .antigravity/**, AGENTS.md, execution/**, docs/**, gateway/**, tests/architecture/**, tests/known-failures.tsv, frontend/Diten.Web/Views/Shared/_Layout.cshtml, services/Diten.PvgService/src/**
Kapsanan dilim: DL-12

Önce oku:
1. AGENTS.md
2. .antigravity/agents/testing-agent.md
3. execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md
4. execution/domains/pharmacovigilance/domain-config.md
5. docs/records/decisions/2026-11/pvg-mod0230-rev20-owner-approval-01.md
6. docs/records/audits/2026-11/pvg-rev20-formal-review-01/ROUND-02-DECISION.md
7. .antigravity/rules/multi-tenancy.md
8. .antigravity/rules/security-jwt.md
9. .antigravity/rules/git-safety.md
10. .antigravity/rules/code-style.md
11. .antigravity/rules/docs-organization.md
12. .antigravity/rules/cross-module-change-consent.md
13. .antigravity/rules/dev-runbook.md
14. .antigravity/rules/configuration-safety.md
15. .antigravity/rules/logging-observability.md
16. frontend/Diten.Web.Tests/Controllers/PvgCaseIntakeTriageWebTests.cs
17. frontend/Diten.Web.Tests/Diten.Web.Tests.csproj
18. .github/workflows/phase1-gates.yml
19. frontend/Diten.Web/Controllers/PharmacovigilanceCaseIntakeTriageController.cs
20. frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/_Form.cshtml
21. frontend/Diten.Web/wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/form.js
22. frontend/Diten.Web/package.json
23. frontend/Diten.Web/package-lock.json
24. services/Diten.PvgService/src/Diten.PvgService.Api/PvgServiceApiHost.cs

Sözleşme yüzeyleri:
- API: «input.ToPayload(isUpdate: true)» (frontend/Diten.Web/Controllers/PharmacovigilanceCaseIntakeTriageController.cs:163)
- İzin: N/A — mevcut JWT/RBAC/tenant korunur; üretim izinleri, seed veya grant değişmez.
- Test: «public sealed class PvgCaseIntakeTriageWebTests» (frontend/Diten.Web.Tests/Controllers/PvgCaseIntakeTriageWebTests.cs:41)
- Sabotaj: «sabotaj kanıtı olmayan guard, guard değildir» (AGENTS.md:34)

Keşif: inceleme gerekli · rapor docs/records/audits/2026-11/pvg-rev20-discovery-01/discovery-report.txt@97e0e7f92dd2efb8d8affd94294bddc84ac05a5f · sha256 b062a217bae4665ea21d8f9328c6687d2d6456eb50274f39f9a6400a1d3f1bc3

Keşif kararı 1: parent — Blueprint MOD-0230 Case Intake & Triage bu hazırlıkların mevcut üst modülüdür; yeni kimlik yok.
Keşif kararı 2: parent — Registry aynı MOD-0230 kimliğini doğrular; ayrı modül veya alias açılmaz.
Keşif kararı 3: parent — Mevcut pack aynı yetki belgesidir; eski dallardaki kopyalar yeni iş izni değildir. Son kapsamın resmî inceleme ve HIGH kaydı olmadan worker yok.
Keşif kararı 4: yeni — HR case-management başka servistir ve yalnız ad benzerliğidir; bu PVG işine dahil edilmez, dosyalarına dokunulmaz veya yeni HR modülü oluşturulmaz.
Keşif kararı 5: parent — Mevcut PVG5011 route ailesi bu modüle aittir; route/Gateway değişikliği bu hazırlıkların dışında kalır.
Keşif kararı 6: parent — PVG-M1-01 mevcut INSPECTION_REQUIRED keşif/inceleme kaydıdır; aynı MOD-0230 altında ilerlenir, ayrı yetenek veya mükerrer modül açılmaz.
Keşif kararı 7: parent — PVG-M1-02 mevcut INSPECTED keşif/inceleme kaydıdır; aynı MOD-0230 altında ilerlenir, ayrı yetenek veya mükerrer modül açılmaz.
Keşif kararı 8: parent — Mevcut operasyonel runtime karar kaydı ayrı kapıdır; sınırlı build/test hazırlığı onu kapatmaz ve canlı kullanım izni vermez.
Keşif kararı 9: child — WP-0230-DL2A aynı modülün INTEGRATED mevcut teslimidir; baseline/bağımlılık olarak korunur. Yalnız onaylı DL12 hazırlık yolları yazılır, eski kabul veya canlı kanıt değişmez.
Keşif kararı 10: child — WP-0230-DL2B aynı modülün INTEGRATED mevcut teslimidir; baseline/bağımlılık olarak korunur. Yalnız onaylı DL12 hazırlık yolları yazılır, eski kabul veya canlı kanıt değişmez.
Keşif kararı 11: child — WP-0230-DL3 aynı modülün SUPERSEDED eski teslimidir; yeniden dağıtılmaz. Mevcut düzeltme/ardıl kayıtlar korunur, bu hazırlık onun eski geniş kapsamını üstlenmez.
Keşif kararı 12: child — WP-0230-DL8 aynı modülün ACCEPTED mevcut teslimidir; baseline/bağımlılık olarak korunur. Yalnız onaylı DL12 hazırlık yolları yazılır, eski kabul veya canlı kanıt değişmez.
Keşif kararı 13: child — WP-0230-DL3A aynı modülün ACCEPTED mevcut teslimidir; baseline/bağımlılık olarak korunur. Yalnız onaylı DL12 hazırlık yolları yazılır, eski kabul veya canlı kanıt değişmez.
Keşif kararı 14: child — WP-0230-DL3B aynı modülün ACCEPTED mevcut teslimidir; baseline/bağımlılık olarak korunur. Yalnız onaylı DL12 hazırlık yolları yazılır, eski kabul veya canlı kanıt değişmez.
Keşif kararı 15: child — WP-0230-DL4A aynı modülün INTEGRATED mevcut teslimidir; baseline/bağımlılık olarak korunur. Yalnız onaylı DL12 hazırlık yolları yazılır, eski kabul veya canlı kanıt değişmez.
Keşif kararı 16: child — WP-0230-DL3C aynı modülün ACCEPTED mevcut teslimidir; baseline/bağımlılık olarak korunur. Yalnız onaylı DL12 hazırlık yolları yazılır, eski kabul veya canlı kanıt değişmez.
Keşif kararı 17: child — WP-0230-DL6 aynı modülün INTEGRATED mevcut teslimidir; baseline/bağımlılık olarak korunur. Yalnız onaylı DL12 hazırlık yolları yazılır, eski kabul veya canlı kanıt değişmez.
Keşif kararı 18: child — WP-0230-DL3D aynı modülün ACCEPTED mevcut teslimidir; baseline/bağımlılık olarak korunur. Yalnız onaylı DL12 hazırlık yolları yazılır, eski kabul veya canlı kanıt değişmez.
Keşif kararı 19: child — WP-0230-DL4B aynı modülün INTEGRATED mevcut teslimidir; baseline/bağımlılık olarak korunur. Yalnız onaylı DL12 hazırlık yolları yazılır, eski kabul veya canlı kanıt değişmez.
Keşif kararı 20: child — WP-0230-DL7A aynı modülün INTEGRATED mevcut teslimidir; baseline/bağımlılık olarak korunur. Yalnız onaylı DL12 hazırlık yolları yazılır, eski kabul veya canlı kanıt değişmez.
Keşif kararı 21: child — WP-0230-DL4D aynı modülün ACCEPTED mevcut teslimidir; baseline/bağımlılık olarak korunur. Yalnız onaylı DL12 hazırlık yolları yazılır, eski kabul veya canlı kanıt değişmez.
Keşif kararı 22: child — WP-0230-DL7B aynı modülün REWORK eski teslimidir; yeniden dağıtılmaz. Mevcut düzeltme/ardıl kayıtlar korunur, bu hazırlık onun eski geniş kapsamını üstlenmez.
Keşif kararı 23: child — WP-0230-DL7B-R1 aynı modülün INTEGRATED mevcut teslimidir; baseline/bağımlılık olarak korunur. Yalnız onaylı DL12 hazırlık yolları yazılır, eski kabul veya canlı kanıt değişmez.
Keşif kararı 24: child — WP-0230-DL7C aynı modülün INTEGRATED mevcut teslimidir; baseline/bağımlılık olarak korunur. Yalnız onaylı DL12 hazırlık yolları yazılır, eski kabul veya canlı kanıt değişmez.
Keşif kararı 25: child — WP-0230-DL10 aynı modülün REWORK eski teslimidir; yeniden dağıtılmaz. Mevcut düzeltme/ardıl kayıtlar korunur, bu hazırlık onun eski geniş kapsamını üstlenmez.
Keşif kararı 26: child — WP-0230-DL10-R1 aynı modülün INTEGRATED mevcut teslimidir; baseline/bağımlılık olarak korunur. Yalnız onaylı DL12 hazırlık yolları yazılır, eski kabul veya canlı kanıt değişmez.
Keşif kararı 27: child — WP-0230-DL11 aynı modülün INTEGRATED mevcut teslimidir; baseline/bağımlılık olarak korunur. Yalnız onaylı DL12 hazırlık yolları yazılır, eski kabul veya canlı kanıt değişmez.
Keşif kararı 28: child — WP-0230-DL4E aynı modülün INTEGRATED mevcut teslimidir; baseline/bağımlılık olarak korunur. Yalnız onaylı DL12 hazırlık yolları yazılır, eski kabul veya canlı kanıt değişmez.
Keşif kararı 29: parent — Mevcut parent WP bu iki dar hazırlığın E2 çıktısını bekler; child hazırlıklar parent BLOCKED satırına bağımlı yapılmaz ve parent kabulü ileri taşınmaz.

NE: DL-12 PREP-WEB için gerçek browser-JS → MVC → PVG host → store bağlantısını mevcut C# testinde kur, tek aliased API ProjectReference ve Phase1'ın iki Node/npm önkoşul adımını ekle. Çıktı mevcut üretim davranışında transport feasibility'dir; final lossless feature değildir.

NEDEN: Bağlantısız Wire/canned-success testleri gerçek form dönüşümünü ve kaydı kanıtlamaz. İki PREP E2 olarak doğrulanıp CT'ye entegre olmadan ana DL-12 başlamaz; final Linux CI tek PR kapanışındadır.

NASIL:
- Araçlar yalnız süreç ortamında: DOTNET_ROOT=C:/Users/epenc/AppData/Local/Temp/pvg-ct-tools-20261120/dotnet8; DOTNET_CLI_HOME aynı kök/dotnet-home; DOTNET_MULTILEVEL_LOOKUP=0, DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1, DOTNET_CLI_TELEMETRY_OPTOUT=1. PATH başına bu dotnet8 ve node22/node-v22.23.5-win-x64 ekle. node.exe v22.23.5, dotnet SDK8.0.428; npm gerekirse node.exe ile node_modules/npm/bin/npm-cli.js çağrılır. Global PATH/profile/feed/registry değiştirme. NuGet/npm cache yalnız görev temp kökünde; ağ/yazma için dar escalation gerekebilir. Gerçek model/effort görünmüyorsa verilemiyor yaz.
- Test log/TRX/sabotaj kopyaları worktree DIŞINDA C:/Users/epenc/AppData/Local/Temp/pvg-dl12-prep-kanıt/<iş-id> altında; çıktı adı benzersiz olsun. Sahte sentetik test verisi; token/çerez/gizli bağlantı değeri loglama. Gerçek uygulama/dev servisi açma; 5000/5001/5011/27017 bağlanma; yalnız kendi rastgele loopback test süreçleri ve PID cleanup.
- Güvenlik mevcut DL4B kodundan okunur: doğrulanmış tenant_id=X-Tenant-Id, aktör doğrulanmış principal, pvg.case-intake-triage.*. Paket içindeki eski başlık/JWT notları tarihsel; geriye döndürme. ROUND-02-DECISION üç açık orta bulguyu ana feature'a bırakır.
- Git: yalnız bu işçi dalı; başlangıçtan sonra main/CT merge veya dal değiştirme yok. Bu iş emri yalnız İzinli yollar için yerel commit iznidir. Kontroller yeşil olduktan sonra açık yolları stage et, git diff --cached --name-only VE tam git diff --cached içeriğini denetle, --check temizliğini ölç ve tek yerel commit oluştur. Push/PR/merge/cleanup yok; CT'ye bildirip DUR. Başkasının dosyasını silme/stash/reset/restore yapma.
- Tek csproj eklemesi: ../../services/Diten.PvgService/src/Diten.PvgService.Api/Diten.PvgService.Api.csproj ProjectReference, yalnız pvgapi alias. Mevcut Web Program ile API global Program çakışmasını extern alias pvgapi kullanarak çöz. Paket/framework/solution/üretim DI ekleme yok.
- Node22/jsdom programı mevcut PvgCaseIntakeTriageWebTests.cs içindeki embedded string; başka JS dosyası/runner yok. Aynı mevcut frontend package-lock ve npm ci. Gerçek Razor HTML'i ve gerçek assets/js/Pharmacovigilance/CaseIntakeTriage/form.js dosyasını yükle, üretim init/submit çalıştırıldığını receipt ile ölç.
- İki yönlü bounded stdout/stdin JSON RPC: timeout/exit/cancellation/early-end/malformed output açık fail; süreç finally kendi PID'iyle kapanır. Gerçek JS fetch/FormData çıktısını C# gerçek MVC test host'una yönlendir; MVC kendi model binding/ToPayload, gerçek PVG host/application/izinli test ports/inmemory store ve gerçek reread kullanır. İsim/değer/sıra/duplicate/boşlukları olduğu gibi geçir, handwritten replacement PUT/canned success veya değer onarımı yok. C# yalnız gerçek doğrulanmış cookie/JWT test bağlamını sağlar; browser token veya payload onarmaz.
- Mevcut üretimin kabul ettiği tam sentetik fixture ile gerçek detail→Edit→submit→tek update→reread zincirini ölç. Mevcut üretim veri kaybını final doğru değer diye sabitleme; lossless/complete-detail bilinen RED'leri kanıtta ana feature'a bırak. Final named no-loss testlerini bu PREP içinde tamamlanmış sayma.
- Connected_edit_submission_preserves_browser_antiforgery_request_token: gerçek Razor sentetik request token'un hem JS header hem FormData ve MVC'ye fiilen forward edilen header hem form member üzerinde dört presence/equality assertion. Token'u loglama. Geçerli kontrol tek gerçek update. Header veya form member'in tek başına düşmesi dört assertion'ın ilgili tarafında RED, kalan carrier MVC'yi geçirirse bile assertion geçmez. İkisi birden eksik veya bozuk token gerçek MVC tarafından reddedilir: sıfır API update/yazma.
- Connected_edit_test_fails_when_browser_runtime_or_update_leg_is_unavailable: isolated child fixture'da Node yok, jsdom yok, runtime start/timeout/bridge failure, JS receipt yok ve update leg suppress ayrı ayrı ilgili connected testi FAIL yapar; skip/fake success/sıfır-write başarı kabulü yok. Outer guard bu beklenen child failure'ı assert eder; normal suite yeşildir. Compile failure negatif kontrol kanıtı değildir.
- workflow'da yalnız mevcut phase1 job'unda Run Phase1 Gates öncesi iki step: actions/setup-node@49933ea5288caeca8642d1e84afbd3f7d6820020 with node-version '22'; sonra working-directory frontend/Diten.Web ve run npm ci. Trigger, permissions, concurrency, diğer adımlar ve ./scripts/run_phase1_gates.sh komutu aynen. Yeni bağımlılık/package-lock değişikliği yok.
- Sabotaj worktree DIŞI snapshot'ta tek tek: browser receipt kabulü atlanır; gerçek update canned success'e dönüşür; header tek düşer; form member tek düşer; invalid token bridge'de onarılır. Her bir ilgili normal/negatif guard assertion'ını RED yapmalı. Gerçek JS çalışmadığında, güncelleme olmadığında, token onarıldığında veya taşıyıcı kaybolduğunda yalnız HTTP200 ile PASS verilmez.

YAPMA: Üretim service/API/store/controller/DTO/form.js/_Form/resx/localization değişikliği yok. Yeni test runner/JS dosyası/package/framework/global ayar yok. Başka modül/dilimin testini veya CI gate/allowlist/permissions'ı değiştirme. Gerçek gateway/service dev portu veya27017 bağlama. Sabotajı kaynak worktree'de uygulama. Eksik runtime'ı skip'e çevirme.

DOĞRULA:
1. Başlangıç feature/pvg/mod-0230-dl12-prep-web, ExpectedHEAD ve temiz status, pack729ac90976f8038b7d6e7c39b0348a51be485999ae7e094c46aea87264bcdab5 Git blob/LF ölçümü. Node22/dotnet8 sürümleri. Temiz local restore/npm ci gerçek çıktısı; npm ci frontend/Diten.Web'de mevcut lock ile.
2. dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Debug --filter FullyQualifiedName~PvgCaseIntakeTriageWebTests --logger "trx;LogFileName=pvg-prep-web.trx" --results-directory <worktree-dışı-kanıt-dizini> → 0 failed/0 skipped, gerçek yeni connected transport, runtime-negative ve antiforgery guard testleri TRX'te. Eski bu sınıf testleri korunur. Süre/load/çekirdek sayısı kaydedilir.
3. Normal transport raw browser form→gerçek MVC→gerçek API/store ve reread tek update ile ölçülür. Missing-runtime/receipt/update ve token negatif fixture'ları child FAIL + sıfır yazma; outer guard GREEN. Her iki observer'da iki carrier assertion'ı ve bağımsız sabotaj RED çıktıları.
4. Csproj yalnız tek aliased reference; workflow tam diff yalnız iki step. git diff --check temiz ve ExpectedHEAD'den değişen yollar tam izinli üç dosya. Kaynak JS/Razor/MVC/service hash'leri değişmez. Npm/cache/TRX/bin/obj stage edilmez. Gerçek GitHub Linux Phase1 henüz koşulmadı olarak raporla.
5. İzinli yolları açık stage ve tam staged diff denetiminden sonra tek yerel commit, temiz durum ve SHA ile CT'ye teslim. E2 transport final no-loss/E4/canlı kabul değildir.

Durma koşulları: Beklenmeyen başlangıç kirliliği/dal/HEAD/pack farkı, yeni paket/yol/üretim değişikliği ihtiyacı, runtime/host alias derlenmemesi, gerçek zincirin kurulamaması, payload/token onarma ihtiyacı, timeout/süreç sızıntısı, yeni açıklanamayan test kırmızısı/skip veya sabotajın GREEN kalması halinde DUR ve somut çıktıyı CT'ye ver. PREP'in hedefi olan mevcut bağlantısız test düzeneği eksikliği ve belgelenmiş final-feature RED başlı başına yeni kapsam değildir.

Rapor formatı: WP, istenen/gerçek model-effort, başlangıç/son SHA/dal/worktree; üç dosya diff özeti; temiz local prerequisite komutları, test/TRX sayıları/exit; iki observer carrier assertion ve gerçek one-update/reread kanıtı; negatif fixture ve named RED sabotaj hash/log tablosu; kaynak değişmezlik ve PID cleanup; final-feature RED/bilinmeyenler. Yerel commit ve status; push/PR yok.

