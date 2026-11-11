---
status: approved
decided: 2026-10-27
module: MOD-0230
decisions: OD-PVG-7
---

# PVG-MOD0230-ODPVG7-01 — 5011 portu PVG servisine verildi (OD-PVG-7)

> **Karar sahibi:** sahip, Enterprise Architecture olarak (`docs/records/decisions/2026-10/pvg-mod0230-mimari-sahibi-01.md`, MIMARI-01) ve depo sahibi olarak (`.claude/launch.json`). Sahibin sözü: "port-evet" (2026-10-27, PVG Control Tower sohbeti).
> **Kaydı tutan:** PVG Control Tower.

## Ölçüm (origin/main @ bc687b701)

- `AGENTS.md` §3 tablosunda 5011 yok. Satır 86, portu "tabloda olmayan ama kullanılan, EA kararı bekliyor" olarak listeliyor: `5011 (PvgService)`.
- `.claude/launch.json` içindeki `web-verify` girdisi (satır 27 ve 29), Diten.Web'in ikinci bir örneğini 5011'de açıyor. PVG servisi ile aynı anda açılırsa çakışıyor.
- `gateway/Diten.ApiGateway/Program.cs:110` CORS listesinde `http://localhost:5011` yer alıyor. Bu girdi `web-verify` için konmuş.
- 5002 boş: depoda kod, yapılandırma ya da betik olarak hiçbir yerde geçmiyor. 5002, `.antigravity/rules/ports.md`'deki frontend portu 5001'in hemen yanında ve mikroservis bandının (5011–5071) dışında.

## Karar

1. 5011, `Diten.PvgService` portu olarak `AGENTS.md` §3 tablosuna yazılır. "EA kararı bekliyor" listesinden çıkarılır.
2. `web-verify` 5002'ye taşınır (`.claude/launch.json`).
3. Gateway'in CORS listesinde `http://localhost:5011` yerine `http://localhost:5002` yazılır, çünkü bu girdi `web-verify` içindir.
4. `.antigravity/AGENTS-REFERENCE.md` §3'teki tarihli notlara dokunulmaz; o notlar tarihseldir. `.antigravity/**` değişmez.

## Uygulama

Değişiklik PVG Control Tower dalında, ayrı bir commit olarak yapılır:
- `AGENTS.md` değişir; ardından `python3 .antigravity/scripts/sync_adapters.py` koşulur.
- `.claude/launch.json` ve `gateway/Diten.ApiGateway/Program.cs` değişir; ardından Gateway test projesi koşulur.

Sahibin "tek-pr" ve "tüm dalgalar bitince PR" kararları gereği değişiklik dalga sonu PR'ı ile `main`'e gider.

## Gerileme değerlendirmesi

- `web-verify`'ı 5011'de açan biri artık 5002'yi kullanır.
- `docs/reference/modules/tenant/workcenter/workcenter-onboarding-sop.md:22`, ölçüm ortamı olarak hâlâ `localhost:5011 (Diten.Web)` yazıyor. Bu belge WorkCenter alanına ait; PVG değiştirmez (XMC-001) ve bu kayıtta bilgi olarak duruyor.
- Kullanıcıya görünen bir değişiklik yok.
