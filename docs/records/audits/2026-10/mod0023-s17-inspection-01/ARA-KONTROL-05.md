# MOD-0023 §17 rev 3 — ara kontrol 05 (alt ajan; resmî inceleme değil)

- Hedef: paket sha256 `42ed1785…` (commit 5e3db307f). Girdi: `.ct-teslim/MOD-0023-review-rev3-r6/` (126 dosya, ZIP sha256
  `23c88e7f…`). Taze bağlamlı Explore alt ajanı, model sonnet, Codex tur 2 metni; yalnız klasörü okudu. reviews.tsv'ye satır yok.
- Sonuç: **KOŞULLU** — engel yok; önceki resmî turun üç bulgusu kapandı; AC-W8 bağımsızlığı, onay satırı atfı, tüketici listeleri,
  dilim tablosu ve E4 alıntısı tutarlı bulundu.
- CT kararı: iki bulgu kabul (F-1 kontrol sırasıyla tutarlı: WorkflowTaskTransitionSupport.cs:61-89, aktör kontrolü sonra).

## Bulgular

- F-1 ORTA: AC-W14 "farklı aktör → 403 önce ve sonra" koşulsuz; (b) geç gelen başka aktör bugün 409 (StaleVersion ya da
  INVALID_STATE), (c) yarışta 409 alır. 403 yalnız O-S17-3 (i) altında. AC-W14 vaka (a) ile sınırlanmalı, (b)/(c) seçeneğe göre
  yazılmalı, AC-W14 O-S17-3'e bağlı ölçütler listesine eklenmeli.
- F-2 DÜŞÜK: kişi alanı listesinin sınırı yazılı değil: `WorkflowTemplateVersion.PublishedBy` (:16), `SlaEscalationRule.
  EscalationPrincipalIds` (:14) ya girmeli ya da gerekçeyle dışlanmalı; TenantScopedEntity devralınan alanları bu girdiyle ölçülmedi.
