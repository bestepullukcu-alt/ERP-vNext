# MOD-0148 → DC-03 binding: canonical supplier code requirement (to Beste, DC-03 owner) — sent

DURUM: GÖNDERİLDİ — Natig, 2026-10-26; Beste'nin (DC-03 sahibi) kabulü bekleniyor, kabul gelene kadar dış bağımlılık açık. Kaynak: MOD-0148 §0.0 (D2 eligibility, v1 gates), CT-APR-02 on `d22c00f4` (F-1) and Natig's
check (2026-10-26). XMC-001: this asks the DC-03 owner; it changes nothing in the DC-03 pack by itself.

## Requirement

1. The DC-03 `supplier_user` binding stores the supplier as the **canonical `SUP-` code that MOD-0140 returned at bind
   time** (SUPPLIER 1.3.0 `SupplierEligibility.supplierId`), never a legacy code or the master uuid.
2. When MOD-0140 changes a supplier's canonical code (e.g. a merge), **the DC-03 owner corrects the binding**; MOD-0140 has
   no such duty (its DL-2 migration runs on dev/test data only).
3. Until a binding is corrected, MOD-0148 answers 503 for that supplier (the eligibility answer names another code); it
   never maps the request to a different supplier.

## Message text (sent by Natig, 2026-10-26)

```text
MVP-6 CT → Beste (DC-03): MOD-0148 portalı, tedarikçi bağlamasını MOD-0140'ın uygunluk cevabıyla birebir karşılaştırıyor.
Bu yüzden DC-03 paketine iki gereksinim rica ediyoruz: (1) bağlama, bağlama anında MOD-0140'ın döndürdüğü kanonik SUP- kodunu
tutsun (eski kod ya da uuid değil); (2) MOD-0140 bir tedarikçinin kanonik kodunu değiştirirse (birleştirme) bağlamayı DC-03
sahibi düzeltsin — MOD-0140'ın böyle bir görevi yok. Düzeltilene kadar portal o tedarikçi için 503 döner, asla başka bir
tedarikçiye eşlemez. Ayrıntı: docs/roadmap/plans/mvp6-mod0147-v1-scorecard-01/DC03-BINDING-CANONICAL-CODE-REQUEST.md.
```
