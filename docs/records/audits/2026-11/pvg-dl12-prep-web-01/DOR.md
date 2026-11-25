# WP-0230-DL12-PREP-WEB — hazırlık DoR

Durum: PLANNED; dağıtım izni yok. Modül/dilim: MOD-0230 DL-12; kaynak kuyruk pvg.
Yetki: docs/records/decisions/2026-11/pvg-mod0230-rev20-owner-approval-01.md.
Üst WP: WP-0230-DL12. Bu hazırlık parent'a bağımlı değildir; parent bu hazırlığın ölçümünü bekler.

## Tek yazıcı kapsamı

- frontend/Diten.Web.Tests/Controllers/PvgCaseIntakeTriageWebTests.cs
- frontend/Diten.Web.Tests/Diten.Web.Tests.csproj
- .github/workflows/phase1-gates.yml

Tek aliased PVG Api referansı, yalnız mevcut C# dosyasında gömülü Node/jsdom/RPC ve CI'deki iki pinned Node22/npm-ci hazırlık adımı. Yeni package/framework/runner dosyası veya mevcut CI trigger/permission/concurrency/kapı değişikliği yok. Uygulama/API/controller/formJS/_Form/l10n/store/host registration/diğer test/pack değişmez.

## Ölçülecek çıktı

Alias derlenir; gerçek Razor/production form.js -> gerçek MVC -> gerçek PVG test host/application/in-memory store -> yeniden okuma hattı aynı test çalışma alanında yürür. Mevcut davranışla sınırlı transport/araç uygulanabilirliği ölçülür. Henüz yazılmamış final AC42 kayıpsız-veri davranışının PASS olması bu hazırlığın önkoşulu değildir; bilinen veri-koruma kusuru gizlenmez veya bug davranışına kabul testi kilitlenmez.

Node/jsdom yokluğu, kopmuş JS/submit/gerçek update bacağı fail olur; skip/fake-success/hidden install/token repair yok. FormData ad/değer/sıra/tekrar/boşluk ve browser-visible sentetik antiforgery request tokenı değişmeden taşınır; cookie/JWT C# tarafındadır. Sadece izinli test istekleri, bounded child process ve nonce/request eşleştirmesi.

Temiz yerel çalışma alanında açık Node22/npm-ci hazırlığı ve hedefli gerçek dotnet testi E2 çıktısıdır; başka runner kurulumu bu kanıt değildir. Gerçek GitHub Linux Phase1 sonucu tek sonuç PR'ının kapanış kapısıdır; açılmamış PR koşusu önkoşul yapılmaz.

## Başlamadan gerekenler

Son hash'in resmî bağımsız inceleme/HIGH kaydı, gerçek ayrı worktree/dal/Expected HEAD, HIGH prompt incelemesi ve NORMAL prompt_gate check. --prepare izin değildir. Önceki E2 baseline öncülleri DL4E/DL7C/DL10-R1; ilgili ortak test/CI üzerinde tek yazıcı ve çakışma yokluğu ölçülür. Feasibility bu işin çıktısıdır. Henüz bu hazırlık için build/restore/npm-ci/test çalışmadı.

Portable .NET8/Node22/npm C:/Users/epenc/AppData/Local/Temp/pvg-ct-tools-20261120 altında hazır; manifest SHA256 5553836a3c21b794ef9d008047d554023ea0eecc4e182d7fcde7afc7935322d4. Global ortam değişmez.
