---
decision_id: PVG-MOD0230-DL3-BOLME-01
status: approved — sahip DL3'ün ikiye bölünmesini kabul etti (2026-10-23, sohbette "bol")
prepared_at_local: 2026-10-23
prepared_by: PVG Control Tower (Claude Code oturumu); bu kayıt kendi adına karar vermez
decided_by: depo sahibi (rol); kişi adı ve e-posta yazılmaz
supplements: docs/records/decisions/2026-10/pvg-mod0230-dagitim-plani-01.md (DP-1); o kayıt değiştirilmez — docs-organization K4
scope: MOD-0230 DL-3 dilimi (BG-8, BG-9); paket değişmez
---

# MOD-0230 — WP-0230-DL3'ün WP-0230-DL3A ve WP-0230-DL3B olarak bölünmesi

## Olgu (ölçüldü, 2026-10-23)

WP-0230-DL3 prompt'u dört sürüm boyunca bağımsız incelemeden engelleyici bulgusuz geçemedi: v1.0 1, v1.1 1, v1.2 2,
v1.3 2 engelleyici (kayıtlar `docs/records/reviews/MOD-0230/prompts/WP-0230-DL3-v1.*-inceleme-01.md`). Her tur bir
öncekini kapattı; prompt büyüdükçe (v1.3: 10 ek test, 10 sabotaj) yeni bir "kısmi gerçekleştirme geçer" yolu bulundu.
Neden: DL-3 iki ayrı iş taşıyor — servisin güvenli açılışı (BG-8) ve yapılandırma dosyalarını tarayan bir denetim (BG-9).

## Karar

| # | Karar (sahip, 2026-10-23) |
|---|---|
| DB-1 | WP-0230-DL3 kuyrukta `SUPERSEDED` olur; yerine **WP-0230-DL3A** (DL-3 kısım A: host açılışı, BG-8; açılış reddi, sağlık hazırlığı, `Program.cs`'in yeni kurulum yöntemini gerçekten çağırması) ve **WP-0230-DL3B** (DL-3 kısım B: yapılandırma taraması, BG-9) açılır. BG-10 (OD-PVG-6) ikisinde de kapsam dışıdır. |
| DB-2 | İki parça paketin DL-3 "Yollar"ı içinde kalır ve aynı test dosyasına (`PvgServiceApiHostTests.cs`) dokunur; bu yüzden aynı hatta (worktree `pvg-wp0230-dl3`) **sırayla** gider: DL3A önce, DL3B DL3A kabul edildikten sonra. DL3B'nin prompt'u DL3A'nın sonucuna göre yazılır (Expected HEAD o zaman ölçülür). |
| DB-3 | DL3 v1.3 incelemesinin iki engelleyicisi taşınır: "`Program.cs` yeni kurulum yöntemini çağırıyor mu" → DL3A; "`ProductionLike=true` alt kuralının ayrı ölçülmesi, `DOTNET_ENVIRONMENT`'ın üretim dışı sayılmaması" → DL3B. v1.0–v1.3 prompt'ları ve inceleme kayıtları iz olarak saklanır, dağıtılmaz. |

## Yerini alan / yerine geçen

- Kuyruk satırı WP-0230-DL3 → WP-0230-DL3A + WP-0230-DL3B.
