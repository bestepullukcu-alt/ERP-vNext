---
record_id: TREASURY-PRODUCER-ANSWER-MOD0143-01
kind: üretici cevabı (MOD-0143 Invoice Capture & 3-Way Match, procurement) — Treasury'nin 2026-10-25 net/brüt sorusuna; onay değildir
received_at_local: 2026-10-25
received_from: MOD-0143 tarafı (procurement); sahip (K1) sohbete yapıştırdı
measured_by_producer_at: origin/main @05f311784; sözleşme `invoice-match.openapi.yaml` sha256 `41fe1f43…3d11` (salt okuma — üreticinin beyanı)
decisions: D26
---

# MOD-0143 cevabı (özet; kaynak metin sohbette)

- **`totalAmount` = Σ `lineAmount`**, sunucu hesaplar (`CaptureInvoiceHandler.cs:138`, `InvoiceMatchValidationRules.cs:48`).
- `lineAmount` gönderilmezse `quantity × unitPrice` (net) (`CaptureInvoiceHandler.cs:116-118`).
- Gönderilen `lineAmount` olduğu gibi kabul edilir; paketin istediği `quantity × unitPrice` tutarlılık kontrolü (MOD-0143
  pack `:53`, `:117`) kodda **yok** — vergi dahil satır kaydedilebilir.
- 3'lü eşleştirme her satırı fatura miktarı × PO birim fiyatıyla karşılaştırır (`RunThreeWayMatchHandler.cs:223-224`); PO'da
  vergi yok (requisition-po 1.1.0, MOD-0141 `:71-72`); tolerans bugün sıfır → vergi dahil tutar `AmountMismatch` istisnasına
  düşer. **Sonuç:** `Matched` ve `MatchedWithinTolerance` faturada `totalAmount` nettir (ikincisinde tolerans kadar fark
  olabilir). **İstisnalar:** `Captured` / `Exception` fatura ve tolerance-override ile çözülmüş fatura brüt tutar taşıyabilir.
- Vergi hiçbir yerde yok (INVOICE-MATCH, PO, MOD-0141/0143 pack'leri, DCP-010); sözleşme ödeme ve borcu Finance / Treasury'ye
  bırakır (`:7`).

**Üreticinin önerdiği, sahip onayına sunulan iki değişiklik:** (1) `invoice-match.openapi.yaml` yalnız ekleme — `totalAmount` /
`lineAmount` "vergi hariç (net); vergi bu sözleşmede taşınmaz" açıklaması + %20 KDV'li örnek; 1.1.0 → 1.1.1. (2) MOD-0143 kodu —
kayıtta `lineAmount ≠ quantity × unitPrice` → 422 + test + sabotaj kanıtı (paket zaten istiyor). Sahip ikisini de onayladı
(D26/3); yapılış procurement tarafında.
