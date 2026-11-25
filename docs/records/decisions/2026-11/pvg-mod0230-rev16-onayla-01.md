# MOD-0230 revizyon 16 — sahibin «onayla» sözü

> Control Tower kaydı, 2026-11-19. Sahip (Ali, hesap GMG-ERP) bu sohbette «onayla» dedi. Onaylanan: paket sha256 `a79791745f11ae53bc126b5ae699f04a9345f7a8c1ee7f95b3fdf4c1db2992c2` (commit dd2b6bee5), bağımsız inceleme `docs/records/reviews/MOD-0230/2026-11-19-rev16-inceleme-01.md` (KOŞULLU, 0 ENGEL, bulgular kapandı).

## Onay satırı neden henüz yok (ölçüldü)

- `scripts/pack_approval.py:714-715`: yazan ile onaylayan aynı kişiyse onay geçersiz ("yazan ve onaylayan aynı kişi"). Kişi eşlemesi `execution/approvals/approvers.tsv`'den okunur.
- Revizyon 16'yı bu kule yazdı; kule sahibin hesabıyla çalışıyor (`gh api user` = GMG-ERP, K3). Sahip onaylayan olarak yazılırsa satırın yazanı ve onaylayanı aynı kişi (K3) olur. Satır dal okumasında "beklemede" görünür, ama ana çizgiye vardığında kırmızı olur.
- Emsal: revizyon 15 satırı yazan GMG-ERP, onaylayan bestepullukcu-alt (`execution/approvals/approvals.tsv:41`). 2026-11-13 tarihli satırlar (`:95-97`) "EES 2.14: yazan ≠ onaylayan" notunu taşıyor.

## Sonuç

Sahibin sözü kayda geçti. Sürüme bağlı onay satırı için listede yüksek risk yetkisi olan, sahipten farklı bir kişinin (K1 ya da K2) onayı gerekir. Hangi yolun izleneceğini sahip seçer.

## Sahibin seçimi (2026-11-19)

Sahip "Beste onaylasın" seçeneğini seçti. Satır: yazan GMG-ERP (K3), onaylayan bestepullukcu-alt (K2), Beste'nin yazılı onayı geldikten sonra yazılır. O zamana kadar işçi gönderilmez; prompt ve devir belgesi hazırlanır.
