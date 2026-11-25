@[.antigravity/agents/testing-agent.md]
WP: WP-0230-DL12-PREP-WIN · Prompt PVG-WP-0230-DL12-PREP-WIN-P v2.0
Repository: C:/Users/epenc/Documents/Codex/2026-09-29/https-github-com-bestepullukcu-alt-erp/work/ERP-vNext-git · Branch: feature/pvg/mod-0230-dl12-prep-win-r2 · Expected HEAD: 3b5cf01df7def7cc3f4eb00a664dba04f46d3e0a · Worktree: C:/Users/epenc/.codex/worktrees/pvg-dl12-prep-win-r2/ERP-vNext-git
Risk: HIGH · §9.1 sinyalleri: sözleşme, uyum
Görev sınıfı: ürün · Paket: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md
İzinli yollar: services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/Persistence/PvgMongoTestHarness.cs, services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/Persistence/PvgPersistenceBoundaryScanTests.cs, services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/PvgServiceApiHostTests.cs
Korunan yollar: .antigravity/**, AGENTS.md, execution/**, docs/**, gateway/**, frontend/**, tests/**, .github/**, services/Diten.PvgService/src/**
Kapsanan dilim: DL-12
Model · Effort (istenen): gpt-6.2-astra · high; gerçek ayar görünmüyorsa verilemiyor yaz.
Task Source Row (exact text): WP-0230-DL12-PREP-WIN	MOD-0230 DL-12 — Windows test hazırlığı: donmuş iki harness/guard test dosyası ve insan onaylı üçüncü dosyada yalnız inaccessible-directory ACL test gövdesi; üç test yolu, gerçek395/35Mongo/sabotaj; parent feature değişmez	MOD-0230	READY	PVG CT	WP-0230-DL4E,WP-0230-DL7C,WP-0230-DL10-R1	-	2026-11-20	docs/records/audits/2026-11/pvg-dl12-prep-win-r2-01/DOR.md	-

Önce oku:
1. AGENTS.md
2. .antigravity/agents/testing-agent.md
3. execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md
4. execution/domains/pharmacovigilance/domain-config.md
5. docs/records/decisions/2026-11/pvg-mod0230-rev20-owner-approval-01.md
6. docs/records/decisions/2026-11/pvg-mod0230-extra-scope-approval-01.md
7. docs/roadmap/plans/pvg-mod0230-windows-acl-scope-proposal-2026-11-20.md
8. docs/records/audits/2026-11/pvg-dl12-prep-win-01/STOP-v1.2.md
9. docs/records/audits/2026-11/pvg-dl12-prep-win-01/stopped-v1.2/WORKER-REPORT.md
10. docs/records/audits/2026-11/pvg-dl12-prep-win-01/stopped-v1.2/capture.json
11. docs/records/audits/2026-11/pvg-dl12-prep-win-01/stopped-v1.2/green-hashes.json
12. docs/records/audits/2026-11/pvg-dl12-prep-win-01/stopped-v1.2/worker-wip.diff
13. docs/records/audits/2026-11/pvg-dl12-prep-win-01/stopped-v1.2/pvg-prep-win-api.trx
14. docs/records/audits/2026-11/pvg-dl12-prep-win-r2-01/BASELINE.md
15. docs/records/audits/2026-11/pvg-dl12-prep-win-r2-01/SAVED-BASELINE.json
16. docs/records/audits/2026-11/pvg-dl12-prep-win-r2-01/WIP-CHAIN.json
17. docs/records/audits/2026-11/pvg-dl12-prep-win-r2-01/DOR.md
18. execution/domains/pharmacovigilance/work-packs/evidence/WP-0230-DL12-PREP-WIN-v2.0.py
19. docs/records/audits/2026-11/pvg-rev21-discovery-01/DECISIONS.md
20. docs/records/audits/2026-11/pvg-rev21-formal-review-01/APPROVAL-CHECK.txt
21. execution/approvals/approvals.tsv
22. execution/approvals/reviews.tsv
23. .antigravity/rules/multi-tenancy.md
24. .antigravity/rules/security-jwt.md
25. .antigravity/rules/git-safety.md
26. .antigravity/rules/code-style.md
27. .antigravity/rules/docs-organization.md
28. .antigravity/rules/cross-module-change-consent.md
29. .antigravity/rules/dev-runbook.md
30. .antigravity/rules/configuration-safety.md
31. .antigravity/rules/logging-observability.md
32. services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/Persistence/PvgMongoTestHarness.cs
33. services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/Persistence/PvgPersistenceBoundaryScanTests.cs
34. services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/PvgServiceApiHostTests.cs
35. services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/Diten.PvgService.Api.Tests.csproj
36. scripts/check_trx_no_skipped.py
37. scripts/known_failures.py
38. tests/known-failures.tsv

Sözleşme yüzeyleri:
- API: N/A — production endpoint/DTO/host/store/config değiştirilmez.
- İzin: N/A — JWT, canonical izinler, tenant çözümü veya seed/grant değiştirilmez.
- Test: «public void Configuration_scan_fails_closed_on_inaccessible_directory()» (services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/PvgServiceApiHostTests.cs:1850)
- Sabotaj: «sabotaj kanıtı olmayan guard, guard değildir» (AGENTS.md:34)

Keşif: inceleme gerekli · rapor docs/records/audits/2026-11/pvg-rev21-discovery-01/discovery-report.txt@9d0df41c5ddc8cfc6f0a9258459501c2c380607b · sha256 32c1b8864deddb6d0221efb4b6805d5ecea44273f0671918e1fcc002bb1f298a

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
Keşif kararı 30: child — Mevcut PREP-WIN bu CT’nin durdurulmuş hazırlığıdır; insanın yeni tek ACL test-metodu onayı rev21’e işlendi. Eski dirty ağaç korunur; yenilenmiş kesin sürüm/formal HIGH/normal prompt kapısı 0 olmadan temiz replacement worker kod yazmaz. E2 henüz tamamlanmadı.
Keşif kararı 31: child — Mevcut PREP-WEB aynı modülün üç dosyalık hazırlığıdır; teslim15791333 bağımsız62/62 ölçüldü, ortak kayıt kapısı2 yüzünden BLOCKED ve henüz entegre değil. Parent veri koruma kabulü ileri taşınmaz.

NE: AC-45 kapsamında donmuş iki WIN düzeltmesini temiz r2 ağacına kontrollü taşı; üçüncü test dosyasında yalnız onaylı inaccessible-directory test gövdesini Windows ACL ile uyumla. Tam395 test,35 gerçek Mongo testi ve altı sabotaj grubunu kanıtla. Ana feature ve operasyon kapalıdır.

NEDEN: Eski v1.2 full API394PASS/1FAIL/0skip ile durdu; tek hata Unix chmod çağrısının Windows'ta PlatformNotSupportedException üretmesiydi. Önceki dört sabotaj/E2 tamamlanmadı. İnsan üçüncü dosyadaki tek metodu açıkça onayladı; rev21 aynı dar kapsamı taşır, iki UYGUN ve kesin HIGHcheck0 kayıtlıdır.

NASIL:
- Bu normal HIGH ürün promptudur; exact prompt/source-pack için CT normal dağıtım gate0 gelmeden patch/ACL/build/test başlatma. ExpectedHEAD/dal/clean/pack/domain/helper hash uyuşmazlığında DUR; mesaj tek başına gate değildir.
- Pack canonicalLF SHAea4ff430d0b2f5f07c8c2482f34311e7897452be597b69b16b822da277b6f7f4; domain2411fa9500c7696b0751767be23661bc3db6c87468821ac43f7e9d56ba62ef8b. Git blob ile disk LF karşılaştır; CRLF ham hashleri ayrıca yaz.
- CT yeni temiz r2 ağacını3b5cf01df sürümüne yalnız ff ile aldı; registry düzeltmesi birleşmiştir. İşçi başlangıçtan sonra merge/dal değişimi yapmaz. Eski pvg-dl12-prep-win ağacı salt okunur korunur; stage/checkpoint/commit/patch/silme/stash/restore yok.
- Donmuş worker-wip.diff6166LFbayt SHA256bd7ebf58ac4866fdba94729d0dfb29f688ec87e8a26d7cc80d1f7bbb9c50617f. Gate0 SONRASI git apply --check ile kontrol edip yalnız bu iki dosyalı patch'i uygula; sessiz hunk düzeltme/yeniden üretme yok. WIP-CHAIN.json iki eski hamhash ve kanonikLF zinciridir. Uygulama sonrası harnessLF6981496b72735be360ed41e45771faae9a3c99f41ba10c49bcfe37a499d6eea7, boundaryLFf1dd99c82066961d4d16b59740e6e0f3a9b8b46cf880db61a44f0b9d1a925ac0 olmalı; farklıysa DUR. İlk iki dosyada başka refactor yok.
- Patch gerçek launch helper'ında Windows için --nounixsocket öğesini kaldırır; Unix içerir. Test aynı gerçek helper'ın iki dalını ölçer, actual launch OperatingSystem değerini kullanır. Binary guard/null/blank/relative/missing/fully-qualified/existence ve27017/default-port retleri; randomloopback/datadir/replica-set/transaction/PID cleanup korunur.
- Üçüncü dosyada ONLY BODY Configuration_scan_fails_closed_on_inaccessible_directory; using/helper/class/ScanFixture/privateScanner/başka test veya attribute değişikliği yok. Tam adlı mevcut.NET türleri kullanılabilir; yeni paket/framework yok.
- Windows'ta yalnız own private ScanFixture/locked hedefini doğrula; paylaşılan/parent/global ACL veya reparse hedefi yok. Özgün DACL'i bağımsız kopyala; currentSID için yalnız ListDirectory deny ve inheritance/propagation yok. FullControl/ReadPermissions/ChangePermissions reddetme; geri yükleme hakkını kapatma.
- Deny öncesi ve restore sonrası nötr locked/appsettings.json hem listelenmeli hem gerçek Scan(...).ScannedFiles içinde bulunmalı. Directory.EnumerateFileSystemEntries(...).ToArray() gerçek ve tam UnauthorizedAccessException vermeli; aynı locked dizininden gerçek Scan(fixture.Root) de tam UnauthorizedAccessException vermeli. ThrowsAny/unrelatedexception/sahte önkoşul yeterli değil.
- finally her hata yolunda özgünDACL/access-inheritance durumunu geri yükler; restore hatası yutulmaz. Yeniden erişim ve özgünACL eşitliğini, ScanFixture dispose sonrası own outer root temizliğini kanıtla; best-effort cleanup yeterli değil. Önce kendiTempaltında olduğuna/reparse olmadığına emin ol.
- Unix chmod ve restore çağrıları, permission değerleri ve güvenlik koşulları korunur; kaynak farkıyla kanıtla. Windows'taki Unix argüman testi Unix runtime PASS değildir. Skip/earlyreturn/sahteexception/gevşekassertion yok.
- Süreç ortamı: DOTNET_ROOT=C:/Users/epenc/AppData/Local/Temp/pvg-ct-tools-20261120/dotnet8; DOTNET_CLI_HOME aynı tasktempkök/dotnet-home; DOTNET_MULTILEVEL_LOOKUP=0, DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1, DOTNET_CLI_TELEMETRY_OPTOUT=1, DOTNET_NOLOGO=1, DOTNET_GENERATE_ASPNET_CERTIFICATE=false. PATH başına portable dotnet8; SDK8.0.428. Python-B/PYTHONIOENCODING=utf-8/PYTHONDONTWRITEBYTECODE=1. Mevcut görev NuGet cache'ini kullan; globalPATH/profile/feed/cert/config değiştirme.
- DITEN_TEST_MONGOD=C:/Users/epenc/Documents/Codex/2026-09-29/https-github-com-bestepullukcu-alt-erp/work/ERP-vNext-main/.tools/mongodb/mongodb-win32-x86_64-windows-7.0.43/bin/mongod.exe; varlık/sürümünü ölç, yoksa DUR. Dev uygulaması/DB açma;5000/5001/5011/27017 kullanılmaz. Yalnız kendi randomloopback PID/port/datadir süreçleri.
- Log/TRX/scratch yalnız C:/Users/epenc/AppData/Local/Temp/pvg-dl12-prep-win-r2-evidence altında benzersiz alt dizinlerde; worktree dışında, sentetik veri. Token/çerez/gerçekbağlantı sırrı loglama.

YAPMA: src/host/store/config/DTO/auth/Gateway/Web/CI/ledger/pack/domain/queue/approval/producer değişmez. AnaDL12/BE/FE/diğer hazırlık yapılmaz. Üç izinli dosya dışı veya üçüncü dosyada tek gövde dışı gereksinimde DUR. Checkpoint/kırmızı iken commit, push/PR/mainmerge/worktreecleanup yok. Mutantları kaynak ağaçta uygulama veya stage etme.

DOĞRULA:
1. Başlangıç tuple/hash/clean, araç sürümleri, processor count/load kaydet. SAVED-BASELINE.json eski395/394/1/0,35Mongo,PNSE kaydıdır; yeni test koşusu değildir. Baselineproducerexit1/acceptancefalse beklenir; tekrar eski fullsuite koşma. EskiTRXrawc3b30fb5a2ce4881d6a24e60480b2c4c6cdac69d1178a0455a6791b65c559404,GitLF1f7a5f2188bf8eb691c8c654dc9ce3a1713926a5abf9b538a9f6cdbb73f18846.
2. Yazım bittikten sonra taze fullAPI bir kez: dotnet test services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/Diten.PvgService.Api.Tests.csproj -c Debug --logger "trx;LogFileName=pvg-prep-win-r2-api.trx" --results-directory C:/Users/epenc/AppData/Local/Temp/pvg-dl12-prep-win-r2-evidence/normal. Tam395/395PASS/0FAIL/0skip;3namedtargets birerkezPASS;35gerçekMongo (19Store+10Api+6Dedicated)PASS. Snapshot-read-concern/commit-failure/no-loss ve transactionlar gerçek çalışır. OwnPID/port/datadir başlangıç/kapanış kanıtı, yalnız owncleanup; gerçekyeni hata/skip/sayıdüşüşünde DUR.
3. Altı ayrı bağımsız derlenen RED grubu, yalnız external git archive ExpectedHEAD + üç final dosyanın exact kopyasından: G1Windowskoşulsuznounixsocket→platformtestRED;G2Unixflagdrop→UnixargümanRED;G3arelativeguard-onlydrop/existencekorunmuş→relativefixtureRED;G3bexistence-onlydrop/fullyqualifiedkorunmuş→missingfixtureRED;G427017guarddrop→forbidden/defaultportRED,devDB'yeasla bağlanma;G5Windowsdenyuygulamasınıdrop→gerçekUAönkoşuluRED;G6denyvegerçekönkoşulkorunmuşken scannerIgnoreInaccessible/failopen→exactscannerexceptionassertionRED. G6scannereditiyalnızexternalmutanttaizinlidir,kaynakhelperdeğişmez. Toplam6grup/7subrun; combinedguardmasking veya compilefailureREDkanıtıdeğil. Her scratch compile0,testassertionRED,tekmutasyon/sourcediff/hash/komut/exit/TRX ve untouchedGREENçifti kaydedilir. Finalkaynak hashleri önce/sonraaynıdır.
4. DeğişmezCTproducer execution/domains/pharmacovigilance/work-packs/evidence/WP-0230-DL12-PREP-WIN-v2.0.py canonicalLF SHA f4e386487608e06a13ffd520ebaab9a81102af86d42c1758dce279d20c982d5d. Normalmod: python -B execution/domains/pharmacovigilance/work-packs/evidence/WP-0230-DL12-PREP-WIN-v2.0.py --repo C:/Users/epenc/.codex/worktrees/pvg-dl12-prep-win-r2/ERP-vNext-git --trx C:/Users/epenc/AppData/Local/Temp/pvg-dl12-prep-win-r2-evidence/normal/pvg-prep-win-r2-api.trx --out C:/Users/epenc/AppData/Local/Temp/pvg-dl12-prep-win-r2-evidence/normal-capture.json --base 3b5cf01df7def7cc3f4eb00a664dba04f46d3e0a --assembly C:/Users/epenc/.codex/worktrees/pvg-dl12-prep-win-r2/ERP-vNext-git/services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/bin/Debug/net8.0/Diten.PvgService.Api.Tests.dll. Exit0/acceptancetrue, gerçekskip/knownfail0,3exactpaths,sourcehash/assembly/TRXfreshness. Wrapper yöntem-gövdesi/ACLrestore/PID/sabotajları bağımsız kanıtlamaz; rawruntime/source ve independentE2 ayrıca gerekir. Baseline moduyla kabul ettirme.
5. Bütün normal395/35/3target,7mutant,ACLfinally/PIDcleanup beklentileri karşılanınca yalnız üç explicit izinli yol stage; git diff --cached --name-only VE TAM stageddiff denetlenir; diff--check0. Aynıonaylıprompt kendi işçi dalında tek normal yerelcommit iznidir. CommitSHA/cleanstatus/kanıtmanifest veripDUR; push/PR/CTmerge yok. CT independentlyE2denetleyip finalSHA'yımergecommitlealır.
6. Unixfinalkapanış mevcutservice-tests.yml pvg-api/ubuntu22.04 fullAPI0fail0skip, gerçekACLtarget koşusu ve finaltestedsourceH bağıdır. Aynıjob/runattempt gerçekcheckoutC/TRX/eventH/baseB; normal sentetikM için H∈parents(M) ve tree(M)==tree(H) gerçekGitobjeleri/checkoutloguyla ölçülür. APIhead_sha/metin iddiasıyetmez. Eşlikkanıtlanamazsa mevcutunfilteredworkflowdispatchfinalCTref C==H+aynıjobTRX; yeniCI/yeniworkflow kapsamıyok. Hdeğişirseöncekisonuçgeçersiz. Pending/RED/kanıtsızUnix finalPRmerge/WPDONE'uengeller; olmayanPRkoşusu PREP/E2entegrasyon veya featuredispatchönkoşuludeğildir. Phase1/Web buUnixkanıtını karşılamaz.

Durma koşulları: Başlangıç/gate/pack/domain/helper/sourcepack/hash/clean uyuşmazlığı;patchfarkı;scopegereksinimi;eksikSDK/Mongo;gerçekACLdeny/restore yapılamaması;yeni hata/skip/sayıdüşüşü;mutantcompilefailure/GREEN/maskedRED;producer/source/assembly/TRXbağıkurulamaması;ownPID/datadircleanupbelirsizliği. Somutçıktıyladur, kendinscope/guard/threshold değiştirme.

Rapor formatı: WP/model-effortgerçekveya verilemiyor;ilk/sonSHA/dal/worktree;exact3file/onlybodydiff;frozenpatch→LF→finalzinciri;395PASS/35Mongo/3target/0skip actualargv/exit/log/TRX/hash;G1/G2/G3a/G3b/G4/G5/G6 compiledRED/originalGREENtablosu;normalproducerJSON+açıksınırları;ACLbefore/deny/after/finally/privatecleanup;ownPID/port/datadirkapanışı;yerelcommit/cleanstatus;UnixfinalPRkapısıaçık. IndependentE2CTkararıdır; anaDL12BLOCKED/E4/live/operasyonkapalıdır.
