# DEC-SCMM-03 — Decision Brief: Model & policy freeze (SCMM)

> **Format:** §14 Decision Brief (çoklu karar paketi). **Durum:** ✅ `DECIDED` — **11 kararın tamamı öneri doğrultusunda onaylandı (user/owner, 2026-09-07).** SCMM-01 ✅ + SCMM-02 ✅ (baseline). **Model freeze KİLİTLİ → G2 önkoşulu sağlandı; SCMM-04 açık.**
> **Bağlam WP:** SCMM-03 (R0, gate G0→G2). **Owner:** Domain architect + content owner (docx §4/§12).

**Decision ID:** DEC-SCMM-03 (docx D02+D03 + record-model invariants)

**Context:** SCMM record modeli ve policy semantiği build'den önce dondurulmalı. En kritik iki karar: (D02) composition-template + claim sahipliği, (D03) meaning/permission/presentation ayrımı (regulated-flag decouple). HTML baseline bunları **birleştiriyor**; docx **ayırmalı** diyor ve "HTML coupling'ini supersede eder."

**Measured evidence (K2):**
- **D03 — GREENFIELD (SCMM-02 ile netleşti):** HTML/legacy `regulated` bayrağını Subject üzerinde **tek alanla iki işi** yönetiyor (dayanak/expiry **+** varsayılan kenar politikası). ⚠️ **Ama SCMM-02 ölçtü: `regulated` bayrağı vNext CrmService kodunda HİÇ YOK (grep=0).** Yani bu bir "mevcut coupling'i çöz" işi DEĞİL — **sıfırdan doğru şekilde ayrı tasarla** (daha kolay; migration yok). docx §1: *"Meaning, permission and presentation are independent"* — kod zaten coupled değil, karar yalnızca yeni modeli coupled kurmamak.
- **AudienceProfile blocker:** bugün tek-boyutlu (HTML "bugünkü tek gerçek engel") → çok-eksen + Subject-bağlı olmalı (SCMM-11 eligibility önkoşulu). *(alan gerçeği SCMM-02 ile teyit)*
- **İsim çakışması:** MOD-0167-FU04 `StrategyTemplate` (ticari play) ≠ legacy ④ Strategy Template. HTML rename önerisi: Book Design→Argument Blueprint · Strategy Template→**Message Scope** · Book→**Argument Set** · Book Pages→**Content Assembly** (onaylı değil).
- **Ownership (SCMM-01/H):** composition/release → CAND-CAP-0011; concept foundation → MOD-0162; shared evidence → MOD-0031.

**Dondurulacak kararlar + CT önerisi:**

| # | Karar | Öneri | Girdi |
|---|---|---|---|
| **D03a** | regulated-flag decouple | **Ayır:** *meaning* (semantic edge) ≠ *permission* (policy: eligibility/evidence/rights) ≠ *presentation* (template order/layout). Presentation policy'yi bypass edemez. | HTML coupling supersede |
| **D03b** | `regulated` yerleşimi + iki sorumluluğun bölünmesi | Dayanak/expiry zorunluluğu = **Policy** boyutu; varsayılan kenar politikası = **ayrı** policy dimension. Tek Subject-flag'e yıkma. Yerleşim EA kararı (Subject vs tenant vs template) | SCMM-02 |
| **D02a** | Composition-template sahibi | **CAND-CAP-0011** (composition-specific); MOD-0162 concept foundation'ı korur | SCMM-01/H |
| **D02b** | Claim sahibi | Composition-claim → CAND-CAP-0011; shared evidence → **MOD-0031** (her koşulda) | docx §3 |
| **D02c** | Component record | `KnowledgeContent`'i component kaydı olarak **reuse** et — version/variant semantiği yeterliyse | SCMM-02 |
| **RM1** | Cycle policy kapsamı | Yalnız hiyerarşik/kısıtlı ilişkilere; **her** semantic edge'e değil | docx §4 |
| **RM2** | Composition template invariants | published version · sections/parallel branches · allowed roles · min/max selections · field schema · grouping/ordering | docx §4 |
| **RM3** | Usage context/scope | product · audience dimensions · market · channel · language · use period; saved scope = inline ile aynı evaluation | docx §4 |
| **RM4** | Policy record | versioned eligibility/evidence/rights/validity/review; evaluation input+result+reasons+policy-version kaydeder | docx §4 |
| **AUD** | AudienceProfile | **çok-eksen + Subject-bağlı**'a çıkar (tek-boydan); vocab sektör-nötr kalır | SCMM-02 |
| **NAME** | Rename adopt? | Öneri: **evet** (Message Scope / Argument Set / Content Assembly / Argument Blueprint) — kod netliği + StrategyTemplate çakışmasından kaçınma | owner kararı |

**Selected options (2026-09-07, user/owner — "önerileri kabul"):** **11/11 = CT önerisi.**
- **D02a** composition-template → CAND-CAP-0011 · **D02b** claim → CAND-CAP-0011, evidence → MOD-0031 · **D02c** KnowledgeContent component reuse (alan-teyidi SCMM-09)
- **D03a** meaning/permission/presentation bağımsız (presentation policy'yi bypass edemez) · **D03b** regulated GREENFIELD — tek Subject-flag YOK; dayanak/expiry = Policy boyutu, varsayılan-kenar-politikası = ayrı policy dimension (yerleşim EA pin: Subject boolean değil, Subject-referanslı Policy kaydı)
- **RM1** cycle yalnız hiyerarşik/kısıtlı ilişkiler · **RM2** composition-template invariants (version/sections+parallel/roles/min-max/field-schema/grouping-ordering) · **RM3** usage-scope dims (product/audience/market/channel/language/period; saved=inline eval) · **RM4** policy record (versioned eligibility/evidence/rights/validity/review + eval log)
- **AUD** AudienceProfile → çok-eksen + Subject-scoped new-build (sektör-nötr vocab) · **NAME** rename adopt: Strategy Template→Message Scope · Book→Argument Set · Book Pages→Content Assembly · Book Design→Argument Blueprint
**Owner/approver:** Domain architect + content owner (+ EA: regulated yerleşimi D03b).
**Affected boundaries:** CAND-CAP-0011, MOD-0162, MOD-0031, MOD-0167 (StrategyTemplate isim), MOD-0164 (consent/policy sınırı — tüketici).
**Affected contracts:** henüz donmuş yok; SCMM-04'te bağlanır.
**Required record changes (onay sonrası):** SCMM iş planı §4 record-model'i kilitli olarak işaretle; naming adopt edilirse registry/boundary + rename notu; AudienceProfile hedef modeli SCMM-11 pack'ine girdi.
**Effective version/date:** G2 model build öncesi (SCMM-02 baseline sonrası).

**Sıra notu (GÜNCEL — SCMM-02 ✅ ACCEPTED 2026-09-07):** SCMM-02 baseline geldi; artık **tüm kararlar kilitlenebilir**. Alan-gerçeği teyitleri: D03b = `regulated` **greenfield** (kodda yok → ayrı tasarla, migration yok); AUD = AudienceProfile **tek-eksen/SubjectId yok** doğrulandı → çok-eksen+Subject-scoped new-build; D02c = KnowledgeContent version/variant reuse edilebilir (SCMM-09'da alan-teyidi). **Bu brief artık G2 için hazır — owner seçimleri (Selected options) bekleniyor.**
