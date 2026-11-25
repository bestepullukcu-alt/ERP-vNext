@[.antigravity/agents/testing-agent.md]
WP: WP-0230-DL12-REGISTRY-01 · Prompt PVG-WP-0230-DL12-REGISTRY-01-P v1.0
Repository: C:/Users/epenc/Documents/Codex/2026-09-29/https-github-com-bestepullukcu-alt-erp/work/ERP-vNext-git · Branch: feature/pvg/mod-0230-dl12-registry · Expected HEAD: 6902667db14e9ba2f0bd5518fccf228608913260 · Worktree: C:/Users/epenc/.codex/worktrees/pvg-dl12-registry/ERP-vNext-git
Risk: HIGH · §9.1 sinyalleri: modüller-arası-yazma, uyum
Görev sınıfı: yönetişim · Kapsam kaydı: docs/records/decisions/2026-11/pvg-mod0230-extra-scope-approval-01.md
İzinli yollar: tests/known-failures.tsv
Korunan yollar: .antigravity/**, AGENTS.md, scripts/**, execution/**, docs/**, services/**, frontend/**, gateway/**, .github/**, tests/known-failures-items.json, tests/architecture/**
Önce oku:
1. AGENTS.md
2. .antigravity/agents/testing-agent.md
3. .antigravity/rules/git-safety.md
4. .antigravity/rules/multi-tenancy.md
5. .antigravity/rules/security-jwt.md
6. .antigravity/rules/code-style.md
7. .antigravity/rules/docs-organization.md
8. .antigravity/rules/cross-module-change-consent.md
9. .antigravity/rules/status-reporting-and-evidence.md
10. docs/records/decisions/2026-11/pvg-mod0230-extra-scope-approval-01.md
11. docs/records/audits/2026-11/pvg-dl12-prep-web-verify-01/REGISTRY-BLOCKER.md
12. docs/records/audits/2026-11/pvg-dl12-prep-web-verify-01/REGISTRY-PROPOSAL-REVIEW.md
13. docs/records/audits/2026-11/pvg-dl12-prep-web-verify-01/independent.trx
14. docs/records/audits/2026-11/pvg-dl12-registry-01/BASELINE.md
15. docs/records/audits/2026-11/pvg-dl12-registry-01/baseline-red.json
16. execution/domains/pharmacovigilance/work-packs/evidence/WP-0230-DL12-REGISTRY-01.py
17. scripts/known_failures.py
18. tests/known-failures.tsv
19. tests/known-failures-items.json
20. tests/architecture/TenantArchitecture.ArchitectureTests/DocsPathGuardTests.cs

Sözleşme yüzeyleri:
- API: N/A — yalnız mevcut yönetişim TSV tetikleyici hücreleri; endpoint veya payload değişmez.
- İzin: N/A — JWT/RBAC/tenant/veri izinleri veya seed/grant değişmez.
- Test: «def validate(rows: list[dict], exists=commit_exists, tracked: list[str] | None = None) -> list[str]:» (scripts/known_failures.py:344)
- Sabotaj: «elif tracked is not None and not any(fnmatch.fnmatch(p, g) for p in tracked):» (scripts/known_failures.py:358)

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

NE: Yalnız tests/known-failures.tsv dosyasındaki mevcut beş fixed DocsPathGuardTests kaydının rerun_trigger hücresinden birer literal ,*.xml öğesini kaldır. Başka hücre, token sırası, kayıt, fingerprint, status veya dosya değişmez. Bu ayrı yönetişim işi PREP-WIN/WEB veya ana veri koruma feature'ını uygulamaz.

NEDEN: Son28 eski audit XML dosyası main'de silinmiş; beş fixed satırdaki ölü glob yüzünden gerçek known_failures CLI tüm suite'lerde fail-closed duruyor. Ana dalda da aynı olan kayıt sapması için doğrudan insan XMC onayı verildi; gerçek XML tarama kodu ve guard korunur. Bellek veya alternatif registry sonucunu gerçek CLI PASS yerine kullanma.

NASIL: Önce dal/HEAD/temizstatus ve kaynak hashlerini ölç. İstenen gpt-6.2-astra/high; gerçek model/effort görülemiyorsa verilemiyor. Kaynak ledger Git blob6038f88ef70db0bedfbde183527f8022b9ca0c64 ve LF SHA5192f9a681a3057de3b68dbd43962c281cf5a55cb96b857cf444c52605976504. Dosyadaki yalnız mevcut satır4–8, suite TenantArchitecture.ArchitectureTests/status fixed olan beş kaydı onay kaydındaki test adlarıyla birebir eşleştir. Her birinin rerun_trigger hücresindeki tek ,*.xml öğesini kaldır; diğer hücreleri ve tüm diğer satırları koru. Dış görev Temp dizinine kanıt yaz; repo veya kullanıcı ayarı/cache/guard değiştirme. Source assertion ile baseline690'a karşı yalnız bu beş hücrenin değiştiğini ölç. Kabul üreticisi değiştirilmeyecek: execution/domains/pharmacovigilance/work-packs/evidence/WP-0230-DL12-REGISTRY-01.py, canonical-LF SHA256 a115f4e8b4cc54bc390e07a3879473841d0e1e5c2d29046d1f219a1fbb6577ca. Hash tutmazsa dur. Bu üretici gerçek CLI validate/check'i çalıştırır; ayrıca yalnız bellek içinde bir eski token geri koyarak aynı üretim validator'ının mevcut izlenen kaynaklarla reddettiğini ölçer. Hiçbir guard kaynağı veya gerçek dosyaya sabotaj yazılmaz.

YAPMA: Yeni module/WP/pack/kural, status/fingerprint değişimi, *.xml dosyası ekleme, açık/fixed kaydı silme, test/guard gevşetme, alternatif registry/override/ghauth, SDK/npm/Mongo/download/appsunucusu başlatma veya Web testini tekrar koşturma yok. Kaynak üreticiyi/baseline'ı/gateway/CI'ı değiştirme. Normal gate0'dan önce yazma. Başka ağaç/main/CT dalına dokunma; push/PR/merge-main/force/reset/clean/amend/hook atlama yok.

DOĞRULA:
1. Gerçek temiz başlangıç ve hedefSHA690. Üretici ve kaynak ledger'ın LF içerik/hash ölçümü; onaylı tam beş kayıt/hücre farkı ve bütün diğer kaynakların değişmezliği.
2. Kendi ağacında python -B execution/domains/pharmacovigilance/work-packs/evidence/WP-0230-DL12-REGISTRY-01.py --repo <kendi-mutlak-worktree> --out <worktree-dışı-kanıt>/final-green.json. PythonioUTF8 ve PYTHONDONTWRITEBYTECODE=1 yalnız bu süreçte. Çıkış0, exact_five_cell_diff=true, gerçek validate0/check0, tek eski token için guard_red=true. Baseline-red.json gerçek1/2 çıktılarıyla finalin0/0 sonucunu birlikte raporla.
3. Canonical-LF production guard/XML scan/items JSON kaynakları başlangıçla aynı; git diff --check0. Kayıtlı62/62 Web TRX'i yeniden çalıştırılmış gibi gösterme; bu iş yalnız o korunmuş TRX'in gerçek regresyon karşılaştırmasını yeniden açar.
4. Yalnız tests/known-failures.tsv açık yoluyla stage; staged dosya listesi ve TAM içerik farkını denetle. Başarılı ölçümlerden sonra kendi dalında tek normal yerel commit oluştur, sonSHA/cleanstatus ölç ve DUR. Commit mesajı fix(governance): remove stale XML rerun triggers. Push ve PR yok.

Durma koşulları: Başlangıç dal/HEAD/hash/kirlilik uyuşmazlığı, beş exact fixed kayıt veya tek tokenın farklı olması, ek dosya/hücre ihtiyacı, gerçek validate/check0 olmaması, bellek guard RED olmaması, test/guard kaynak değişikliği, beklenmeyen Git veya kanıt hatası halinde DUR ve somut çıktıyı CT'ye ver. Kapsamı genişletme, başarısızlığı gizleme.

Rapor formatı: WP, istenen/gerçek model-effort, ilk/sonHEAD/dal/worktree/status, tam beş hücre diff özeti, üretici/source/TSV/TRX SHA çiftleri, gerçek argv/exit ve baseline→final1/2→0/0, tek-token bellek RED hatası, dış evidence yolları, yerel commit/cleanstatus. Sınırlamalar: runtime/test tekrar başarısı veya ürün/ana DL12 E2 iddiası yok. Push/PR yok.
