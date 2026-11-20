---
module: MOD-0230
work_package: WP-0230-DL12-PREP-WIN
status: prepared
date: 2026-11-20
---

# MOD-0230 DL-12 — Windows test hazırlığı

Test · Kullanıcı rev20 kapsam/build-test/delege geliştirme onayını verdi. İzinli yazıcı yalnız `PvgMongoTestHarness.cs` ve `PvgPersistenceBoundaryScanTests.cs`, ikisi de mevcut PVG Api.Tests/Persistence dizininde. Üretim host/store/endpoint ve diğer dosyalar korunur. Ayrı yönetilen worktree `C:/Users/epenc/.codex/worktrees/pvg-dl12-prep-win/ERP-vNext-git`, dal `feature/pvg/mod-0230-dl12-prep-win`. İşçi sohbeti `01a1f89c-3e4d-7a31-883e-e6e0c8c3eb67`; istenen gpt-6.2-sol/high, gerçek model/effort görünmüyor.

Karar · Mevcut aynı pvg kuyruğunda DL-12 child hazırlıktır; yeni MOD/kimlik veya ayrı kayıt profili yok. DL4E, DL7C ve DL10-R1 entegrasyonlarına bağlı; BLOCKED ana feature'a bağımlı değildir. Keşif kaynağı `pvg-rev20-discovery-01/discovery-report.txt`, bütünlük doğrulanmış 29 karar DECISIONS.md'de. Önceki729 sürümünün gerçek onay kontrolü dilim bölümünü reddetti; mekanik heading düzeltmesinin yeni pack hash'i `186ad9675e2f5219d240080f93012c54f266561f6ce70451f96aaeddb242aa01`. Bu belge kapı geçmiş demek değildir.

Test · Hazırlığın çıktısı gerçek Windows Mongo replica-set/transaction testi, port/datadir/PID izolasyonu, cleanup ve platform/guard sabotajlarıdır. Portable SDK8.0.428 ve Node22 görev temp kökünde; Mongo mutlak kurulu ikili iş emrinde ölçülür. Varsayılan27017 veya geliştirme veritabanı kullanılmaz. Null/empty/relative/missing ikili ve27017 retleri korunur; yalnız iki sentetik fixture yolu taşınabilir yapılır. E2 bağımsız doğrulama ve CT entegrasyonu ana feature önkoşuludur, ana DL-12'nin E4 gereksinimi düşürülmez.

Karar · Başlama ancak yeni kesin pack'in resmî inceleme/HIGH satırı, işçinin yeni Expected HEAD'i, HIGH iş emri bağımsız incelemesi ve normal `prompt_gate.py check` çıkış0 sonrası. v1.0 olumsuz inceleme geçmişte tutulur; yeni prompt kaynakları yeniden ölçülür. Hazırlık/--prepare yürütme izni değildir. Eksik araç/bozuk replica set/skip/güvenlik kaybı completion değildir. İşçi yalnız izinli dosyaları denetledikten sonra yerel commit; push/PR yok. CT doğrulama sonrası merge commit ile toplar, tek final PR hazırlanır. Güvenli main entegrasyonu görülmeden worktree temizliği yapılmaz. Operasyon kapalıdır.
