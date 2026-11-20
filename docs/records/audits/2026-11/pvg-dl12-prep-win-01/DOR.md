# WP-0230-DL12-PREP-WIN — hazırlık DoR

Durum: PLANNED; dağıtım izni yok. Modül/dilim: MOD-0230 DL-12; kaynak kuyruk pvg.
Yetki: docs/records/decisions/2026-11/pvg-mod0230-rev20-owner-approval-01.md.
Üst WP: WP-0230-DL12. Bu hazırlık parent'a bağımlı değildir; parent bu hazırlığın ölçümünü bekler.

## Tek yazıcı kapsamı

- services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/Persistence/PvgMongoTestHarness.cs
- services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/Persistence/PvgPersistenceBoundaryScanTests.cs

Yalnız gerçek platform argüman yolu ve aynı testteki sentetik mutlak binary fixture yolları. IsPathFullyQualified/exists, boş/relative/missing retleri, 27017 yasağı, loopback, temp data, replica-set/transaction ve cleanup korunur. Uygulama/API/controller/formJS/store/config/diğer test/CI/pack değişmez.

## Ölçülecek çıktı

AC45 platform flag ve gerçek Windows replica-set/ilgili store testleri, sıfır hata/skip; yalnız kendi PID/port/data ömrü ve cleanup kanıtı. Her kritik argüman koruması derlenen red/green sabotajla dış scratch kopyada ölçülür, sabotaj commit edilmez. Feasibility PASS kendi önkoşulu değildir; işin çıktısıdır. Bu çıktı parent'ın veri-koruma uygulanmış olduğunu veya E4 kabulü göstermez.

## Başlamadan gerekenler

Son hash'in resmî bağımsız inceleme/HIGH kaydı, gerçek ayrı worktree/dal/Expected HEAD, HIGH prompt incelemesi ve NORMAL prompt_gate check. --prepare izin değildir. Önceki E2 baseline öncülleri: DL4E, DL7C, DL10-R1; kayıtları parent kuyruğunda bellidir. Çakışan yazar yokluğu ölçülür. Henüz bu hazırlık için test çalışmadı.

Portable araçlar C:/Users/epenc/AppData/Local/Temp/pvg-ct-tools-20261120 altında hazır; manifest SHA256 5553836a3c21b794ef9d008047d554023ea0eecc4e182d7fcde7afc7935322d4. SDK/npm bu iş için process env ile kullanılır. DITEN_TEST_MONGOD mevcut kurulu Windows binary'sine açık mutlak yol verilerek ölçülür; development Mongo27017 kullanılmaz.
