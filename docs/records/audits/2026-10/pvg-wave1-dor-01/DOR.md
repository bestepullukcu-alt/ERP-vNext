# PVG ilk dalga — DoR (kart §1) kanıtı: WP-0230-DL2A ve WP-0230-DL8

> Control Tower kaydı: `PLANNED → READY` geçişinin kanıtıdır (ct-sop7-transitions: "DoR kontrol listesi geçti (kart §1)").
> Dağıtım izni değildir: dağıtım yalnız kuyruk satırları `main`'e girdikten sonra, `prompt_gate.py check` dağıtım
> kipinde (`--prepare` olmadan) çıkış 0 verirse yapılır. Tarih: 2026-10-23 · CT dalı `chore/ct/pvg-ct-01`.

| DoR maddesi | WP-0230-DL2A | WP-0230-DL8 |
|---|---|---|
| Kimlik + sahip | MOD-0230 Case Intake & Triage; paket sahibi "PVG paket sahibi" | aynı |
| Paket yetkisi | `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` sha256 `10fdbe5c…`, `status: ready-for-dev`; `pack_approval.py check` çıkış 0 @ 95563dae1 (onaylayan bestepullukcu-alt, 2026-10-23) | aynı |
| Sözleşme | Paket "Validation Rules" update concurrency (G-15), BG-12, BG-13; prompt'ta satırıyla alıntılı | "Gateway / API Routing Decision", BG-16; prompt'ta satırıyla alıntılı |
| Bağımlılıklar | PVG-M1-02 INSPECTED (inceleme notu @ afe613de8); sözleşme bekleyen ölçütler kapsam dışı | aynı |
| Kapsam | İzinli / korunan yollar prompt'ta (DL-2 "Yollar"ının kayıt sürümü kısmı) | İzinli yol yalnız `gateway/Diten.ApiGateway/ocelot.json` (dört MOD-0230 girdisi) |
| Git yalıtımı | Worktree `.claude/worktrees/pvg-wp0230-dl2`, dal `feature/pvg/wp-0230-dl2`, Expected HEAD 95563dae1, temiz | Worktree `.claude/worktrees/pvg-wp0230-dl8`, dal `feature/pvg/wp-0230-dl8`, Expected HEAD 95563dae1, temiz |
| Yürütme / şerit | Hat A (DL2B aynı hatta sonra); risk HIGH (sözleşme, kiracı) | Hat C; risk HIGH (gateway, kiracı); canlı deneme tek-CT kuralına bağlı (kart §7a/5) |
| Hedef ajan | `.antigravity/agents/backend-architect.md` | `.antigravity/agents/integration-agent.md` |
| Prompt (§36.1) | `execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL2A-v1.1.md` sha256 `1e59bc96…` @ 66a7ca7aa | `…/prompts/WP-0230-DL8-v1.0.md` sha256 `e4d42513…` @ 66a7ca7aa |
| Bağımsız inceleme (HIGH) | `docs/records/reviews/MOD-0230/prompts/WP-0230-DL2A-v1.1-inceleme-01.md` @ 66a7ca7aa — kritik 0, engelleyici 0 | `…/WP-0230-DL8-v1.0-inceleme-01.md` @ 66a7ca7aa — kritik 0, engelleyici 0 |
| Kabul | BG-12 (paketteki üç adlı test + CT ekleri), BG-13; `pvg-regpv-base` 0 kırmızı | BG-16; Gateway üzerinden smoke |
| Kapı (`--prepare`) | `prompt_gate.py check --queue pvg --prepare --review …@66a7ca7aa`: tek ENGEL "kuyruk PLANNED" (bu kayıtla READY olur) | aynı |
| Keşif | `docs/records/audits/2026-10/pvg-m1-01-discovery-02/MOD-0230-case-intake-triage.txt` @ 4ebe4b15f, 12 kararın hepsi prompt'ta | aynı |

Kabulde ayrıca bakılacak inceleme notları ("diğer"): DL2A — özetteki sürümün API okuma yanıtına girmesi ve sürümsüz
güncellemenin DL-4'e kadar çakışma dönmesi raporun "Known gaps" bölümünde yazılı olmalı; sabotaj A'nın üç çağrı yerini
kapsaması; eşzamanlılık testinin zaman aşımı. DL8 — kuyruk satırının `main`'de olması (bu PR ile).
