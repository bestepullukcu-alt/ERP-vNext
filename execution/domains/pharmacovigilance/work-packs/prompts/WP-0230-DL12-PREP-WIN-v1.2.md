@[.antigravity/agents/testing-agent.md]
WP: WP-0230-DL12-PREP-WIN · Prompt PVG-WP-0230-DL12-PREP-WIN-P v1.2
Repository: C:/Users/epenc/Documents/Codex/2026-09-29/https-github-com-bestepullukcu-alt-erp/work/ERP-vNext-git · Branch: feature/pvg/mod-0230-dl12-prep-win · Expected HEAD: 58920519a17e8f4c085738642b33e36689bc8fee · Worktree: C:/Users/epenc/.codex/worktrees/pvg-dl12-prep-win/ERP-vNext-git
Risk: HIGH · §9.1 sinyalleri: sözleşme, uyum
Görev sınıfı: ürün · Paket: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md
İzinli yollar: services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/Persistence/PvgMongoTestHarness.cs, services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/Persistence/PvgPersistenceBoundaryScanTests.cs
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
16. services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/Persistence/PvgMongoTestHarness.cs
17. services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/Persistence/PvgPersistenceBoundaryScanTests.cs
18. docs/records/audits/2026-11/pvg-rev20-formal-review-01/ROUND-03-DECISION.md
19. docs/records/audits/2026-11/pvg-rev20-formal-review-01/SLICE-TABLE-CORRECTION.md
20. execution/approvals/approvals.tsv
21. execution/approvals/reviews.tsv

Sözleşme yüzeyleri:
- API: N/A — üretim endpoint, yanıt ve store değiştirilmez; yalnız test başlatma ve güvenlik ölçümü.
- İzin: N/A — JWT/izin/tenant uygulaması değişmez, hiçbir seed veya grant eklenmez.
- Test: «Pvg_mongo_test_harness_refuses_port_27017_and_fails_without_mongod» (services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/Persistence/PvgPersistenceBoundaryScanTests.cs:199)
- Sabotaj: «sabotaj kanıtı olmayan guard, guard değildir» (AGENTS.md:34)

Keşif: inceleme gerekli · rapor docs/records/audits/2026-11/pvg-rev20-discovery-02/discovery-report.txt@773fdf178dc5df34d31066b630a44d19f2a01467 · sha256 ce6c78b5e30d5aa8a9600ebd1bbaa10c1cd85a6b576cf9b178f4f2c67dcde36a

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
Keşif kararı 30: child — WP-0230-DL12-PREP-WIN aynı MOD-0230 DL-12 için bu CT tarafından açılan iki dosyalık hazırlıktır; READY yalnız kuyruğun durumudur, normal HIGH kapı çıkış0 olmadan işçi başlamaz. Yeni veya mükerrer modül yok.
Keşif kararı 31: child — WP-0230-DL12-PREP-WEB aynı MOD-0230 DL-12 altında bu CT'nin üç dosyalık test/CI hazırlığıdır; ayrı yazıcı alanı ve normal HIGH kapısı vardır, ana feature iki hazırlığın E2 entegrasyonunu bekler. Yeni kimlik yok.

NE: AC-45 kapsamında Windows Mongo test başlatma/fixture taşınabilirliğini yalnız iki izinli dosyada tamamla; ana DL-12 özelliğine dokunma.

NEDEN: Mevcut harness Windows'ta desteklenmeyen --nounixsocket kullanıyor ve sentetik /opt/fake/mongod yolu fully-qualified değil. Hazırlık kendi gerçek Windows güvenlik/test kanıtını üretir; ana feature ancak iki PREP bağımsız doğrulama/entegrasyonundan sonra başlar.

NASIL:
- Araçlar yalnız süreç ortamında: DOTNET_ROOT=C:/Users/epenc/AppData/Local/Temp/pvg-ct-tools-20261120/dotnet8; DOTNET_CLI_HOME aynı kök/dotnet-home; DOTNET_MULTILEVEL_LOOKUP=0, DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1, DOTNET_CLI_TELEMETRY_OPTOUT=1. PATH başına bu dotnet8 ve node22/node-v22.23.5-win-x64 ekle. node.exe v22.23.5, dotnet SDK8.0.428; npm gerekirse node.exe ile node_modules/npm/bin/npm-cli.js çağrılır. Global PATH/profile/feed/registry değiştirme. NuGet/npm cache yalnız görev temp kökünde; ağ/yazma için dar escalation gerekebilir. Gerçek model/effort görünmüyorsa verilemiyor yaz.
- Test log/TRX/sabotaj kopyaları worktree DIŞINDA C:/Users/epenc/AppData/Local/Temp/pvg-dl12-prep-kanıt/<iş-id> altında; çıktı adı benzersiz olsun. Sahte sentetik test verisi; token/çerez/gizli bağlantı değeri loglama. Gerçek uygulama/dev servisi açma; 5000/5001/5011/27017 bağlanma; yalnız kendi rastgele loopback test süreçleri ve PID cleanup.
- Güvenlik mevcut DL4B kodundan okunur: doğrulanmış tenant_id=X-Tenant-Id, aktör doğrulanmış principal, pvg.case-intake-triage.*. Paket içindeki eski başlık/JWT notları tarihsel; geriye döndürme. ROUND-02-DECISION üç açık orta bulguyu ana feature'a bırakır.
- Git: yalnız bu işçi dalı; başlangıçtan sonra main/CT merge veya dal değiştirme yok. Bu iş emri yalnız İzinli yollar için yerel commit iznidir. Kontroller yeşil olduktan sonra açık yolları stage et, git diff --cached --name-only VE tam git diff --cached içeriğini denetle, --check temizliğini ölç ve tek yerel commit oluştur. Push/PR/merge/cleanup yok; CT'ye bildirip DUR. Başkasının dosyasını silme/stash/reset/restore yapma.
- Gerçek launch'ın kullandığı argüman üretimini aynı harness dosyasında sınanabilir yap; Unix --nounixsocket içerir, Windows içermez. Test gerçek launch helper'ını çağırır; testte paralel argüman kopyası üretme. Random loopback/non27017, temporary data, replica-set/oplog/transaction koşulları ve PID cleanup aynen korunur.
- Pvg_mongo_test_harness_refuses_port_27017_and_fails_without_mongod içinde YALNIZ missing/positive sentetik binary yollarını platform fully-qualified yap. Null/boş/relative/missing redleri, Path.IsPathFullyQualified, existence ve bütün 27017/default-port ret assertion'ları kalır. ResolveBinary güvenliğini gevşetme.
- Pvg_mongo_launch_arguments_follow_the_host_platform_without_weakening_isolation testini mevcut boundary scan test sınıfına ekle. Hem Windows hem Unix argüman dalını aynı testte gerçek helper parametresi üzerinden ölç; actual launch gerçek OperatingSystem değerini kullanır.
- DITEN_TEST_MONGOD=C:/Users/epenc/Documents/Codex/2026-09-29/https-github-com-bestepullukcu-alt-erp/work/ERP-vNext-main/.tools/mongodb/mongodb-win32-x86_64-windows-7.0.43/bin/mongod.exe. Mutlak dosyanın varlığını ve --version çıkışını ölç; yoksa DUR.
- Değişiklik öncesi ilgili güvenlik testinin Windows fixture kırmızısını ve harness argümanını kaydet; bilinen bu iki taşınabilirlik açığı görevdir, sürpriz başka kırmızı yeni kapsam gerekçesidir. Tam API süitini yazım tamamlandıktan sonra bir kez çalıştır.
- Sabotajları yalnız worktree DIŞI git archive ExpectedHEAD + kendi iki değişmiş dosyanın kopyası üzerinde ayrı ayrı uygula: (1) unconditional --nounixsocket Windows argüman testini kırmızı yapar, (2) Unix'ten flag düşürmek Unix dalını kırmızı yapar, (3) missing/relative guard kaldırmak mevcut güvenlik testini kırmızı yapar, (4)27017 yasağı kaldırmak mevcut testini kırmızı yapar. Her kopya hash/komut/test/exit ve GREEN/RED kanıtını kaydet. Derleme hatası yerine assertion kırmızısı aranır. Kaynak worktree sabotaj görmez.

YAPMA: Üretim host/store/config/endpoint, uygulama/DTO, Web/CI, paket/defter/kuyruk/kural dosyaları değişmez. Yeni paket/bağımlılık/framework/exception/skip ekleme. Gerçek tenant/devDB/27017 kullanma. Hiçbir ret testi yumuşatılmaz; eksik binary veya bozuk replica set başarı/skip değildir.

DOĞRULA:
1. Önce dal/HEAD/status temizliği ve pack186ad9675e2f5219d240080f93012c54f266561f6ce70451f96aaeddb242aa01 Git blob/disk LF karşılaştırması. Model ve araç sürümlerini ölç.
2. Portable dotnet ile dotnet test services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/Diten.PvgService.Api.Tests.csproj -c Debug --logger "trx;LogFileName=pvg-prep-win-api.trx" --results-directory <worktree-dışı-kanıt-dizini>; DITEN_TEST_MONGOD yukarıdaki mutlak ikili. 0 failed/0 skipped, yeni platform ve mevcut guard testleri sonuçta bulunur; gerçek replica-set/store/transaction testleri de listelenir. Test toplamı ve load/çekirdek sayısını kaydet.
3. Her sandbox fixture ayrı test datadir/port kullanır. Başlatılan süreç PID'lerini ve kapanışını kanıtla; yalnız kendi PID'ini temizle. Replica-set/transaction başarısını gerçek test ad/sonucuyla bağla, argüman okumasını runtime başarısı sanma.
4. Dört ayrı sabotaj kırmızı; normal dosyalar GREEN ve aynı hash'te. git diff --check temiz; git diff ExpectedHEAD yalnız iki izinli dosya. Cache/log/generated çıktı stage edilmez.
5. GIT denetimi sonrası yerel commit; son SHA, temiz git status ve açık kalan sınırlamalar. E2 iddiası komut/TRX/hash/gerçek süreç kanıtına bağlı; E4/live yok.

Durma koşulları: Başlangıç dal/HEAD/pack uyuşmazlığı, beklenmeyen kirlilik, izinli iki dosya dışı gereksinim, eksik SDK/Mongo veya başarısız gerçek replica-set, güvenlik assertion kaybı, yeni açıklanamayan test kırmızısı, test skip, sabotajın yeşil kalması halinde DUR ve somut çıktıyı CT'ye bildir. Kendi işinden kalan kirliliği başlangıç kirliliği sayma.

Rapor formatı: WP, gerçek model/effort veya verilemiyor, başlangıç/son SHA ve dal/worktree; izinli dosya diff özeti; komut/exit/test sayıları/TRX yolları; named GREEN/RED sabotaj tablosu ve hash çifti; PID/port/datadir/cleanup; bilinmeyenler ve açık risk. Yerel commit varsa hash, stage ve temiz durum. Push/PR yok. Kanıtları loglamadan token değeri açıklama.
