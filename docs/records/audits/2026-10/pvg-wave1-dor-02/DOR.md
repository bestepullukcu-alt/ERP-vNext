# PVG ilk dalga — DoR (kart §1) kanıtı: WP-0230-DL3A

> Control Tower kaydı: `PLANNED → READY` geçişinin kanıtıdır. Dağıtım izni değildir: dağıtım yalnız kuyruk satırı
> `main`'e girdikten sonra, `prompt_gate.py check` dağıtım kipinde (`--prepare` olmadan) çıkış 0 verirse yapılır.
> Tarih: 2026-10-23 · CT dalı `chore/ct/pvg-ct-01`. DL2A ve DL8'in DoR kaydı: `docs/records/audits/2026-10/pvg-wave1-dor-01/DOR.md`.

| DoR maddesi | WP-0230-DL3A |
|---|---|
| Kimlik + sahip | MOD-0230 Case Intake & Triage; paket sahibi "PVG paket sahibi" |
| Paket yetkisi | `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` sha256 `10fdbe5c…`, `status: ready-for-dev`; `pack_approval.py check` çıkış 0 (onaylayan bestepullukcu-alt, 2026-10-23) |
| Sözleşme | "Startup Fail-Closed Requirements", BG-8; prompt'ta satırıyla alıntılı (14 alıntı) |
| Bağımlılıklar | PVG-M1-02 INSPECTED; BG-9 WP-0230-DL3B'de, BG-10 (OD-PVG-6) kapsam dışı |
| Kapsam | İzinli yollar: `Program.cs`, `PvgServiceApiHost.cs`, `PvgServiceApiHostTests.cs` (DL-3 "Yollar") |
| Git yalıtımı | Worktree `.claude/worktrees/pvg-wp0230-dl3`, dal `feature/pvg/wp-0230-dl3`, Expected HEAD 95563dae1, temiz |
| Yürütme / şerit | Hat B (DL3B aynı hatta sonra, DB-2); risk HIGH (yetki, kiracı, uyum) |
| Hedef ajan | `.antigravity/agents/backend-architect.md` |
| Prompt (§36.1) | `execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL3A-v1.1.md` sha256 `0e5e0057…` @ 201be5991 |
| Bağımsız inceleme (HIGH) | `docs/records/reviews/MOD-0230/prompts/WP-0230-DL3A-v1.1-inceleme-01.md` @ 201be5991 — kritik 0, engelleyici 0; ölçüm yolu net8 deneme projesiyle doğrulandı |
| Kabul | BG-8 (beş bağımlılık ayrı ayrı, sağlık hazırlığı gerçek uçtan, Program.cs yeni kurulumu kullanır, eski koruma testi korunur) |
| Keşif | `docs/records/audits/2026-10/pvg-m1-01-discovery-02/MOD-0230-case-intake-triage.txt` @ 4ebe4b15f |
| Bölme kararı | `docs/records/decisions/2026-10/pvg-mod0230-dl3-bolme-01.md` @ 3d0583d14 |

Kabulde ayrıca bakılacak inceleme notları ("diğer"): tek bağımlılık listesi ve `PVG_` kod biçiminin ölçülmemesi;
`Program.cs`'in ortam ve adres ayarlarını okuyup okumadığı; "kayıtlı ama çözülemeyen bağımlılık" ve sıfırdan farklı
süreç çıkış kodu raporun "Known gaps" bölümünde adıyla yazılı olmalı.
