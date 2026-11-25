---
module: MOD-0230
work_package: WP-0230-DL12-PREP-WEB
status: prepared
date: 2026-11-20
---

# MOD-0230 DL-12 — Web test bağlantısı hazırlığı

Test · Kullanıcı rev20 kapsam/build-test/delege geliştirme ve ortak Web test/CI yollarını onayladı. Tek yazıcı yalnız `frontend/Diten.Web.Tests/Controllers/PvgCaseIntakeTriageWebTests.cs`, `frontend/Diten.Web.Tests/Diten.Web.Tests.csproj` ve `.github/workflows/phase1-gates.yml`. Üretim MVC/DTO/Razor/JS/service/store ve diğer testler korunur. Ayrı yönetilen worktree `C:/Users/epenc/.codex/worktrees/pvg-dl12-prep-web/ERP-vNext-git`, dal `feature/pvg/mod-0230-dl12-prep-web`. İşçi sohbeti `01a1f8a1-ced1-7f10-bc22-e0e36291741b`; istenen gpt-6.2-astra/high, gerçek model/effort görünmüyor.

Karar · Aynı pvg kuyruğu/DL-12, yeni kimlik/profil yok. DL4E/DL7C/DL10-R1 entegre öncüller; ana BLOCKED feature bağımlılık değildir. Keşif `pvg-rev20-discovery-01` içindeki gerçek v2 rapor ve 29 karardır. Mevcut729 sürümünün onay kapısı dilim bölümünü reddetti; tek heading prefix uyumuyla yeni pack hash'i `186ad9675e2f5219d240080f93012c54f266561f6ce70451f96aaeddb242aa01`. v1.0 prompt incelemesinin npm cache/flag eksikliği ve kapı engeli korunur; güncel sürüm incelemesi gereklidir.

Test · Çıktı mevcut C# testinde embedded Node22/jsdom ile gerçek Razor/üretim form.js → FormData → gerçek MVC binding/ToPayload → gerçek PVG host/application/store → reread bağlantısıdır. Tek pvgapi aliased Api referansı; yeni JS runner/bağımlılık yok. Gerçek browser token'u iki carrier ve iki gözlem noktasında değiştirilmeden korunur; C# köprü onarmaz. Eksik/bozuk token sıfır update; eksik runtime/receipt/update zinciri child FAIL, outer guard GREEN olmalıdır. Canned success/elle yeniden yazılmış PUT pozitif kanıt değildir. Mevcut final no-loss/complete-detail RED ayrı ana feature'a kalır, başarı gibi sabitlenmez.

Test · Phase1 yalnız mevcut job'da sabitlenmiş setup-node, Node22, npm cache, `frontend/Diten.Web/package-lock.json`; ardından frontend/Diten.Web içinde `npm ci --no-audit --no-fund`. Yalnız bu iki step eklenir; trigger/permissions/concurrency/gate komutları değişmez. Temiz local komut/test çıktısı hazırlığın E2 ürünüdür, kendi başlangıç önkoşulu değildir. Gerçek GitHub Linux Phase1 tek final PR kapanış kapısı; E4/live kabulü açılmaz.

Karar · Başlama yeni kesin sürüm resmî inceleme/HIGH kaydı, işçinin Expected HEAD/kaynak paketi bağı ve bağımsız HIGH prompt incelemesiyle normal dağıtım kapısı çıkış0 sonrası. --prepare başarı sayılmaz. İşçi üç açık yolu denetledikten sonra yalnız yerel commit eder; CT bağımsız E2 doğrulama sonrası merge commit ile toplar. İşçi dalı push edilmez, tek final PR hazırlanır, main entegrasyon güvenliği görülmeden ağaç silinmez. Operasyon kapalıdır.
