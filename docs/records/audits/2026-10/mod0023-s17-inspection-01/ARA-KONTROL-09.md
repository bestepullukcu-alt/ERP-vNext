# MOD-0023 §17 rev 3 — ara kontrol 09 (alt ajan; resmî inceleme değil)

- Hedef: paket sha256 `f5d4ae86…` (commit 5081d2c19), Codex inceleme 2 (`2026-10-30-5512cbca.md`, UYGUN DEĞİL) düzeltmesinden
  sonra. Girdi: `.ct-teslim/MOD-0023-review-rev3-c3/` (134 dosya). Explore alt ajanı, model sonnet; yalnız klasörü okudu.
  reviews.tsv'ye satır yok.
- Sonuç: **KOŞULLU** — engel yok. Codex 2'nin F-1 (TargetPrincipalId ayrı alanı) ve F-2 (sıfır satırlı 503 sonrası ilk yazma)
  kapandı; F-3 (AC-W5) ölçütü doğru, uygulama biçimi belirsiz.

## Bulgular — kabul, düzeltildi (paket sha256 f1c05136…)

- F-1 DÜŞÜK: AC-W5 tutma konumu tek biçim olmalı: anahtar okumasından sonra, `ReserveTrustedStartAsync` sarmalayıcısında
  (StartWorkflowInstanceHandler.cs:291; WorkflowRepositories.cs:262-279); `AllReservedBarrier` yalnız destekleyici kanıt.
- F-2 DÜŞÜK: TargetPrincipalId kuralı "unavailable" yeniden okuma yolunda da (SamePayload, WorkflowTerminalTransitionWriter.cs:399-438)
  sınanmalı; `RequestInfoAsync` yardımcısına hedef ve yorum parametresi.
