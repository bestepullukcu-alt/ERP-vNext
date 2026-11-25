# PVG onuncu dalga — DoR (kart §1) kanıtı: WP-0230-DL4E

> Control Tower kaydı, 2026-11-02. `PLANNED → READY` geçişinin kanıtıdır. Dağıtım, `prompt_gate.py check` çıkış 0 ile olur. Başlatma sözü sahibin «onayla» sözüdür (`docs/records/decisions/2026-11/pvg-mod0230-onayla-01.md`).

| DoR maddesi | WP-0230-DL4E |
|---|---|
| Kimlik + sahip | MOD-0230 Case Intake & Triage; paket sahibi "PVG paket sahibi" |
| Paket yetkisi | Revizyon 15 (`1ba98662…`), `main`'de onaylı (PR #329). AC-39 ve G-24 bu revizyonla DL-4'ün bitişine yazıldı. |
| Sözleşme | AC-39 (a) ve (b). Ek (c) testi ve ek metin vakaları prompt'ta CT okuması olarak işaretli. |
| Bağımlılık | WP-0230-DL4D `ACCEPTED`. |
| Kapsam | Yalnız `PvgCaseIntakeTriageEndpoints.cs` (alan düzeyinde `JsonConverter<PvgTriageOutcome?>`) ve yeni test dosyası. Host genelinde dönüştürücü ve enum özniteliği yasak. |
| Kapsam dışı | 404 (AC-6), denetim (DL-5), izin kapısı adaptörleri (OD-PVG-14), Edit veri kaybı (G-25), frontend. |
| Git yalıtımı | Worktree `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pvg-wp0230-dl4e`, dal `feature/pvg/wp-0230-dl4e`, Expected HEAD `e6e6ef0a3`. |
| Yürütme / şerit | Risk HIGH (sözleşme, uyum). Alt ajan işçi, uzaktan erişim kısıtı (S-114). İşçi kendi yalıtılmış worktree'sinde aynı dala geçer (WP-0230-DL10-R1 dağıtım eki 01 emsali). Model Sonnet 5.5 · high. |
| Teslim (S-63, S-76) | İşçi yalnız kendi dalında commit eder, bildirir ve durur. Dalı CT dalına CT birleştirir. |
| Prompt (§36.1) | `execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL4E-v1.1.md`, sha256 `e3f73c89656176628294a44cfd311c832a05fe20541cb79e9391c9f91e7e9805`. |
| Bağımsız inceleme (HIGH) | v1.0: UYGUN (1 ORTA, 2 DÜŞÜK; v1.1'de kapandı). v1.1: UYGUN, yeni DÜŞÜK N-1 açık: tamsayı olmayan ya da `int` aralığı dışındaki sayı. Prompt'un "dönüştürücü yalnız `JsonException` fırlatır" kuralı bu durumu kapsar. İşçiye brief'te not olarak verilir; prompt değişmez. Kayıtlar `docs/records/reviews/MOD-0230/prompts/`, v1.1 kaydı `@ed8e93eac`. |
| Kabul | İşçi E2 üretir. DL-4'ün gerekli kanıtı E4; canlı yol açılana kadar kabul ertelenir (KK-8). |
