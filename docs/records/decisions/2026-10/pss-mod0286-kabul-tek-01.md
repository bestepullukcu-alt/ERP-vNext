---
record_id: PSS-MOD0286-KABUL-TEK-01
status: decided — sahip `kabul-tek` (2026-10-23)
prepared_at_local: 2026-10-23
prepared_by: PSS-UI Control Tower (Claude Code oturumu 05ce4e4b)
block: MOD-0286 UI Design System & Page Pattern Governance (P1)
pack: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md rev 06 (sha256 7f6868d9…), main'de onaylı (PR #210, c429c0338)
queue: pss-ui
---

# MOD-0286 — DL-1 ve DL-2 tek iş paketiyle kabul (PSS-UI-P1-03)

## Karar

Sahip `kabul-tek` seçti (2026-10-23): DL-1 (şartname) ve DL-2 (uyum kontrol listesi) iki ayrı iş paketi yerine tek
iş paketiyle dağıtılır, doğrulanır ve kabul edilir.

- **Yeni iş paketi:** `PSS-UI-P1-03` — DL-1 + DL-2 son ölçüm, açıkların kapatılması ve rapor; tek dağıtım prompt'u,
  tek bağımsız prompt incelemesi (paket HIGH), yazandan farklı bir doğrulayıcı, tek kabul kaydı.
- **Yerine geçilenler:** `PSS-UI-P1-01` (DL-1) ve `PSS-UI-P1-02` (DL-2) PLANNED'dan SUPERSEDED'e geçer; yerine geçen
  iş paketi `PSS-UI-P1-03` (kart §0.2 Y2; geçiş tablosu "Yerine geçen iş paketinin kimliği").

## Neden

- Hazırlık çıktılarının ikisi de main'de (PR #210): `spec.md`, `compliance-checklist.md`, üç betik, `resx-baseline/`,
  kayıtlı koşular `auto-01` ve `sabotage-01`, `manual.md`. DL-2, DL-1'e bağlı; ayrı dağıtım aynı kanıtı iki kez
  ölçtürür.
- Kart, prompt kapısını ve kabul kaydını iş paketi kimliğine bağlar: tek prompt için tek kuyruk satırı gerekir.
- Bedeli: iki dilim tek kabul kaydında birlikte kabul ya da birlikte ret olur; kabul listesi paketin bütün AC'lerini
  (AC-1…AC-16) tek tabloda taşır.

## Sınırlar

Bu kayıt kapsam birleştirmesidir: dağıtım izni değildir, kabul değildir. Paket değişmez. Dağıtım için sıra: prompt +
`prompt_gate.py check --prepare` → bağımsız prompt incelemesi (HIGH) → DoR kaydı ve READY → READY satırının main'e
girmesi → dağıtım.
