# PV Final GO / NO-GO Report (J + M)

## J. Ağırlıklı tamamlanma yüzdeleri

**Ağırlık şeması (capability, dosya adedi DEĞİL):** Domain/veri %20 · Core PV vaka %25 · Regulatory/compliance %15 · Workflow/audit/evidence %15 · Security/multi-tenancy %10 · Integration/migration %10 · UI/operability %5.

### 1. Legacy fonksiyonel tamamlanma → **%35–45 (Medium)**
`0.20·60 + 0.25·50 + 0.15·35 + 0.15·15 + 0.10·20 + 0.10·15 + 0.05·75 ≈ %39`.
Kanıt: intake/CRUD/attachment/list PROVEN; ama versioning/follow-up/duplicate/narrative/coding/workflow/audit yok.

### 2. Legacy üretim/uyumluluk güvenilirliği → **%10–20 (Medium-High)**
Audit=0, e-sig=0, endpoint-auth=0, test≈0, validation=0, host-based izolasyon. Güçlü şekilde kanıtlı negatifler → düşük güvenilirlik.

### 3. Legacy kod doğrudan yeniden kullanılabilirlik → **%5–10 (High)**
Farklı mimari + güvenlik borcu + testsizlik → lift-and-shift önerilmez. Direct reuse ~%0.

### 4. Legacy iş kuralı yeniden kullanılabilirlik → **%55–70 (Medium)**
Alan setleri, MA lifecycle (0-5), LCPPV süreci, seriousness/causality vokabülerleri değerlidir ve yeniden uygulanabilir.

### 5. Legacy veri migrate edilebilirlik → **%50–70 (Medium-Low)**
Çekirdek koleksiyonlar taşınabilir; ancak ExternalReference, attachment yeniden yerleştirme, tenant yeniden eşleme, versiyon/follow-up geçmişi kaybı şartlı.

### 6. ERP-vNext PV hedefini mevcut karşılama → **%10–18 (Low-Medium)**
`0.20·10 + 0.25·0 + 0.15·0 + 0.15·25 + 0.10·70 + 0.10·5 + 0.05·0 ≈ %13`.
PV-özel = ~%0; değer paylaşımlı platformda (RBAC/Audit/Notification/DocMgmt/Reference/Org).

### 7. ERP-vNext'e taşıma için kalan geliştirme → **%82–90 (Low-Medium)**
(100 − #6). Çoğu PV çekirdeği greenfield.

### 8. Doküman iddialarının kanıtlanması → **%0–10 (High)**
11 iddianın hiçbiri koddan TRUE değil.

> Kanıt seviyesi: #2/#3/#8 High (kod-tabanlı negatifler); #1/#4 Medium; #5/#6/#7 Low-Medium (runtime doğrulanmadı).

## M. Nihai karar — soru cevapları

1. **Legacy PV ERP-vNext'e taşınabilir mi?** Evet, ama **yeniden inşa + veri migrasyonu** olarak — kod taşıma olarak değil.
2. **Kodun ne kadarı doğrudan taşınabilir?** ~%5–10 (pratikte ~0 önerilir).
3. **İş kurallarının ne kadarı korunabilir?** ~%55–70.
4. **Verinin ne kadarı migrate edilebilir?** ~%50–70 (normalizasyon + ExternalReference + attachment + geçmiş-kaybı kabulü ile).
5. **En riskli alanlar?** Endpoint-auth yokluğu + host-based tenant izolasyonu (P0); audit/e-sig/validation yokluğu (P0/GxP); attachment disk depolama; case versiyon/follow-up geçmiş kaybı; "validated Di10-PV" doküman iddiaları.
6. **Korunması gereken gerçek değer?** PV domain vokabülerinin ve iş kurallarının olgunluğu (MA lifecycle, LCPPV, safety case alan seti) + mevcut operasyonel veri.
7. **Kesinlikle taşınmaması gereken kod?** `TenantResolutionMiddleware` (host-based), legacy auth altyapısı, SQL Server config, `DitenPvLookup` (tek entity), CORS AllowAll, local-disk attachment, senkron Flurl kuplajı, god-entity `SafetyReport`.
8. **İlk yapılması gereken modül/paket?** (a) PV/Regulatory domain boundary'sini registry'de aç; (b) referans/product/org hizalaması (MOD-0048/0290/0288); (c) **Safety Case aggregate + intake (auth + audit ile)**.
9. **Shared Di10-PV database iddiası doğru mu?** Hayır — "Di10-PV" marka adı; validated shared DB kanıtı yok; UNPROVEN/FALSE.
10. **Mevcut dokümanlar düzeltilmeden kullanılabilir mi?** Hayır. "validated Di10-PV database", reconciliation kaynağı ve FSAD ifadeleri düzeltilmeden compliance dayanağı yapılmamalı.
11. **Sonuç:** **CONDITIONAL GO.**

### GO/NO-GO gerekçesi
- **GO değil (koşulsuz):** Legacy'nin GxP güvenilirliği düşük; doküman iddiaları kanıtsız.
- **NO-GO değil:** PV domaini iş açısından değerli, veri taşınabilir, ERP-vNext platformu güçlü temel sunuyor.
- **CONDITIONAL GO koşulları:** (1) Doküman iddialarının düzeltilmesi (özellikle "validated Di10-PV database"); (2) greenfield yeniden inşa kararı (lift-and-shift YOK); (3) paylaşımlı platform (Audit/Workflow/RBAC/DocMgmt/Notification/Reference) zorunlu tüketimi; (4) Phase 7 validation kanıtı olmadan cutover yok.

> **Önemli:** GO kararı yalnızca "kod bulundu" diye verilmemiştir. Kararın dayanağı kod-yolu kanıtı + kanıtlanmış eksikliklerdir.
