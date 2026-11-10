# MOD-0023 §17 rev 3 — ara kontrol 08 (alt ajan; resmî inceleme değil)

- Hedef: paket sha256 `15c8b51e…` (commit 004d4d683). Girdi: `.ct-teslim/MOD-0023-review-rev3-r9/` (132 dosya, ZIP sha256
  `e4775407…`). Taze bağlamlı Explore alt ajanı, model sonnet, Codex tur 2 metni; yalnız klasörü okudu. reviews.tsv'ye satır yok.
- Sonuç: **KOŞULLU** — yalnız DÜŞÜK; önceki resmî turun üç bulgusu kapandı; RequestInfo yinelenen anahtar eşlemesi uygulanabilir
  bulundu (emsal: StartWorkflowInstanceHandler.cs:690 MongoDB istisnası yakalama).

## Bulgular — hepsi kabul

- F-1 DÜŞÜK: "her durumda tek satır" zorlanan 503 dalında tutmaz (satır 0; Writer :379-390; Fix1.cs:156-163); zorlanan dallarda
  satır beklentisi ayrı yazılmalı.
- F-2 DÜŞÜK: AC-W11 geçiş cümlesi (paket:620-621) farklı aktör için "çatışma kodu" diyor; §17.9/7 403 diyor — bağlanmalı.
- F-3 DÜŞÜK: AC-W14 gövde tanımına RequestInfo `TargetPrincipalId` (WorkflowModels.cs:505-512; Comment'e "target:" olarak
  yazılır, Support :587-591).
- F-4 DÜŞÜK: Delegate'in aynı anahtar tekrarı (Support :388 → :1018-1030) ne kapsam içi ne dışı yazılı; ortak yardımcı değişirse
  etkilenir; çağıranlar Web `delegateTask`, WorkCenter dağıtıcısı.

## CT kararı

Dört DÜŞÜK bulgu son bir yazar turunda düzeltilir; ara kontrol yinelemesi burada durur (sekiz tur; engel ve orta bulgu kalmadı).
Son sürüm sabah resmî Codex turuna gider.
