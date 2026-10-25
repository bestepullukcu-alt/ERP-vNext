---
id: UNASSIGNED
slug: finance-treasury-intake
name: Finance & Treasury — kaynak projeden doğrulayarak alma (Treasury intake)
type: Delivery Capability Pack
standard: CAP-001
status: draft
status_note: "draft — yalnız planlama; production implementation tetiklemez (CAP-001 §5). DCP numarası atanmadı: mevcut dosyalarda DCP-004/005/006 birden çok kez kullanılmış, numara tahsisi sahip/EA kararıdır; o zamana kadar UNASSIGNED (mevcut UNASSIGNED-… örneği gibi). Onaydan önce bağımsız inceleme (CT-APR-02, SOP §20.4) ve sürüme bağlı onay (CT-APR-01) gerekir."
owner_domain: finance-treasury
owner: depo sahibi (K1) / Treasury Intake Control Tower
created: 2026-10-23
authoring_branch: chore/ct/treasury-intake-ct-01
canonical_source: "docs/reference/blueprint/System Capability & Implementation Blueprint - master 8.8.xlsx#Blueprint_Data"
canonical_modules: [MOD-0126, MOD-0269, MOD-0119, MOD-0120, MOD-0124, MOD-0289]
candidate_modules: [MOD-0217, MOD-0057]
runtime_code_allowed: false
runtime_code_scope: "yok — üye modüllerin hiçbirinin Recovery pack'i yok"
inputs:
  - "docs/records/decisions/2026-10/treasury-intake-owner-decisions-01.md (D0–D8)"
  - "docs/records/decisions/2026-10/treasury-mod0126-placement-decision-01.md"
  - "docs/records/audits/2026-10/treasury-intake-handover-review-01/REPORT.md"
  - "execution/domains/finance-treasury/domain-config.md (taslak)"
  - "docs/roadmap/plans/erp-finance-legal-module-intake-01/ (TP-001 v1.5 taslağı, TREASURY-MIGRATION-SEQUENCE-v1.0, DESTINATION-BRIEF, yerleşim notları, SoD değerlendirmesi)"
  - "docs/roadmap/plans/erp-finance-legal-module-intake-01/CT-QUEUE.tsv (treasury-intake)"
  - "depo dışı: /Users/natig/Projects/treasury-transfer-handoff/ (11 producer paketi + source-build-evidence-v1.0; SHA256SUMS doğrulandı 2026-10-23)"
---

# Finance & Treasury — Treasury intake (Delivery Capability Pack, TASLAK)

## 1. Identity and status

- **Kimlik:** UNASSIGNED (numara bekliyor) · **durum:** `draft` · **standart:** CAP-001.
- **Neden DCP (CAP-001 §2):** modüller bir bağımlılık sırasıyla teslim edilmek zorunda; domain'ler arası kararlar var
  (finance-treasury, master-data-management, procurement, commercial-suite); birden çok iş modülü ortak temele
  dayanıyor (MOD-0269 sözleşmesi, MOD-0003 kararı, ortak denetim iletici); kaynak paket sürümleri arasında karar kayması
  riski var.
- **Bu belge ne değildir:** module pack değildir; üye modüllerin kendi pack'lerinin yerine geçmez; kod, port, servis
  iskeleti ya da kod alma izni vermez (CAP-001 §7).

## 2. Business outcome

ERP-vNext kaynak projesinde yazılmış ve kaynakta test edilmiş Treasury modüllerinin (ödeme ve onay, borç, alacak, nakit
pozisyonu, banka ana verisi) Recovery'ye **doğrulanarak ve Recovery standartlarına uyarlanarak** alınması; Recovery'de
ödeme partisinin hazırlanıp onaylanabildiği ve onay ayrılığının (SoD) zorlandığı ilk dikey dilim.

## 3. Problem statement

- Recovery'de finance/treasury domain'i ve servisi yoktu; 8 üye/aday modülün Recovery'de kodu ve pack'i yok (0 dosya).
- Kaynak kod başka mimaride (`DitenErpTreasuryService`, `Diten.Platform`, kaynak MDM), başka portlarda (5058, 5057,
  5050) ve bilinen eksiklerle (NoOp denetim, uzak çağrının yerel kayıttan önce yapılması, `[Authorize]`-only uçlar).
- Kaynak paketler (TP-001 v1.5 producer packets) bilgi taşır, kod taşımaz; üç pakette kaynak tarafın yeni sürümü
  istendi (inceleme kaydı §4).

## 4. Capability boundary

**İçeride:** ödeme partisi / talimat / onay / serbest bırakma (MOD-0126); satıcı borcu (MOD-0119); müşteri alacağı
(MOD-0120); nakit pozisyonu (MOD-0124); bankaya giden arayüz sözleşmesi, dağıtım kapalı (MOD-0269); banka ana verisi
(MOD-0289, MDM'de).
**Dışarıda:** gerçek banka dağıtımı ve banka kimlikli geri çağrılar (karar 0.1 = A); GL (karar 0.7 önerisi); Cash
Forecasting MOD-0125; Bank Reconciliation MOD-0121; MOD-0146; tedarikçi, müşteri ve LE ana kayıtları (kendi domain'leri).

## 5. Member modules and follow-ups

| modül | Blueprint (8.8) | Recovery durumu | rol |
|---|---|---|---|
| MOD-0126 Payments & Approvals | W-3, Treasury & Cash | kayıt 0 · pack 0 · kod 0 | üye — ilk |
| MOD-0269 Banks/SWIFT/Payments Gateway [External Provider] | W-1, External Provider | 0 · 0 · 0 | üye — yalnız arayüz sözleşmesi |
| MOD-0119 Accounts Payable (AP) | W-3, Financial Management | 0 · 0 · 0 | üye |
| MOD-0120 Accounts Receivable (AR) | W-3, Financial Management | 0 · 0 · 0 | üye |
| MOD-0124 Cash Positioning | W-3, Treasury & Cash | 0 · 0 · 0 | üye |
| MOD-0289 Bank Master & Bank Account Registry | W-3, Treasury & Cash | 0 · 0 · 0 | üye — MDM'de (D7, MDM sahibi onayı bekliyor) |
| MOD-0217 Contract Lifecycle Management (CLM) | Legal Governance | 0 · 0 · 0 | aday — yer MDM (D19/1); pack TRM-11 + Document Management kapısı + MDM sahibi onayından sonra |
| MOD-0057 Semantic Tagging & Taxonomy Management | W-4 | 0 · 0 · 0 | **park** (D19/2) — MOD-0053/0056 gelince yeniden açılır |

**Üye olmayan, değişiklik önerisi alacak modüller** (kendi domain sahiplerinin onayıyla, XMC-001): MOD-0220 (LE↔LE
ilişki modeli — MDM), MOD-0140 (tüketici gereksinimleri; MVP-2 değişikliği main'e girdikten sonra — procurement),
MOD-0149 (müşteri SoR ve `CustomerId` eşlemesi — commercial-suite).

## 6. Ownership map

| konu | sahibi |
|---|---|
| DCP, sıra, kapsam kararları | depo sahibi (K1) |
| finance-treasury domain'i, MOD-0126/0119/0120/0124 pack'leri | finance-treasury (Treasury Intake CT hazırlar; sahip onaylar) |
| MOD-0289 | master-data-management domain sahibi |
| MOD-0269 arayüz sözleşmesi; bankacılık sahibi | sahip (K1); bankacılık sahibi henüz adlandırılmadı |
| Port ve bant | EA |
| Gateway rotaları | yalnız `integration-agent` |
| ERP-FIN-BUNDLE arayüz sahipliği | EA (karar 0.6) |
| Kaynak paket içeriği ve yeni sürümleri | kaynak Treasury Migration CT; karar sahibi K1 |

## 7. Dependency graph

```text
MOD-0269 arayüz sözleşmesi (0.1 = A) ─┐
MOD-0023 Workflow (var) ──────────────┼─> MOD-0126 ──> (MOD-0146 dışarıda)
MOD-0021 Audit / CentralAudit (var) ──┘       │
                                              └─> MOD-0289 (MDM) <── MOD-0220 LE ilişki modeli (eksik)
MOD-0003 kararı (0.3, açık) ──> MOD-0119, MOD-0120, MOD-0124 ──> MOD-0289      <── MOD-0140 (1.3.0)
AR-BILLING-BUNDLE (0.5, açık) ──> MOD-0120
MOD-0037 (var) ──> MOD-0124
```

MOD-0126 → MOD-0289 tek yönlüdür; MOD-0126 banka hesabını opak referansla taşır (sıra belgesi Amendment 1).

## 8. Ordered delivery sequence

1. **Kararlar (Dalga 0):** 0.0 (D6 ✓), 0.1 (D5 = A ✓), 0.2 (MOD-0126 yerleşimi ✓), 0.3, 0.5, 0.6, 0.7, MOD-0289 MDM onayı,
   MOD-0217/0057 kararı (TRM-13), port (EA).
2. **Kaynak yeni sürümleri:** MOD-0126 v1.2, MOD-0119 v1.1, MOD-0289 v1.6 (istendi 2026-10-23).
3. **Level-1 paket kabulleri** (TRM-02), paket başına kayıt.
4. **Dilim TRM-S1:** MOD-0269 arayüz sözleşmesi → MOD-0126 pack → CT-APR-02/01 → `ready-for-dev` → kod alma izni →
   servis iskeleti (`Diten.FinanceTreasuryService`) → alma ve uyarlama → Recovery testleri (level-2).
5. **Dilim TRM-S2:** MOD-0003 kararından sonra MOD-0119, MOD-0120, MOD-0124 (birbirinden bağımsız, paralel).
6. **Dilim TRM-S3:** MOD-0220 ilişki modeli + MOD-0140 1.3.0 + TRM-S1 ve TRM-S2 → MOD-0289.
7. **Dilim TRM-S4:** MOD-0217 (yer MDM, D19/1; koşullar kapanınca). MOD-0057 park (D19/2), dilimde değil.

## 8.1 Delivery slices

| Slice | Kapsam | Not |
|---|---|---|
| TRM-S1 | MOD-0126, MOD-0269 | ilk dikey dilim; MOD-0269 yalnız sözleşme ve kapalı bağdaştırıcı |
| TRM-S2 | MOD-0119, MOD-0120, MOD-0124 | karar 0.3 bekliyor |
| TRM-S3 | MOD-0289 | MDM; TRM-S1, TRM-S2, MOD-0220 ve MOD-0140 değişikliklerine bağlı |
| TRM-S4 | MOD-0217 | aday; yer kararı D19/1, pack koşullu (MOD-0057 park — D19/2) |

## 9. Prerequisites

- Kaynak kod doğrulaması: bağlanan commit'te derleme ve test (kanıt `source-build-evidence-v1.0`: MOD-0126 510/510,
  0124 407/407, 0120 317/317, 0119 251/251; MOD-0289 ve MOD-0217 kırık).
- Recovery tarafında: MOD-0023 Workflow ve SoD davranışı (`SOD-APPROVER-DEPTH-ASSESSMENT-v1.0.md`), CentralAudit
  ortak iletici (`services/Diten.Building.Blocks`; bugün SupplyChain ve Inventory kullanıyor).
- Her modül için DCP-002 ön kontrolü ve kayıt satırı rezervasyonu (`verify_module_id.py --check-id`).

## 10. Architecture decisions

- **Servis:** `Diten.FinanceTreasuryService` (D2), domain `finance-treasury`; port 5071 (D10/3; AGENTS §3 ve
  `ports.md` güncellemesi ayrı onay).
- **Şirket uygunluğu (D10/1, D10/2):** ödeme yapan şirket aktif ve onaylı olmalı; MOD-0220 `lookup-validation` yanıtına
  `ApprovalStatus` eklenir (yalnız ekleme) ve MOD-0220 sözleşme belgesi yazılır (TRM-14).
- **Kod alma:** doğrula, sonra uyarlayarak al (D1); kopya değil. Her dosya `path@commit` + sha256 ile izlenir;
  bilinen kaynak kusurları taşınmaz (inceleme kaydı §7).
- **MOD-0269:** sağlayıcıdan bağımsız sözleşme, dağıtım kapalı (D5); dört kapalı akış varsayılmaz.
- **Contract-first:** `docs/analysis/contracts/` altında modül sözleşmeleri; başka servisin koleksiyonu okunmaz.
- **Denetim kaydı (CAP-001 §7.1, AUD-001):**

| servis | denetim altyapısı bugün | üyelerin yolu | altyapı yoksa kim kurar |
|---|---|---|---|
| `Diten.FinanceTreasuryService` (yeni) | yok — servis yok | `b`, CentralAudit ortak iletici (K4) üzerinden; servis-başı MDM kopyası yasak | servis iskeleti, TRM-S1'in ilk adımı; MOD-0126 ödeme ve onay komutları K2 sınıfına girerse fail-closed davranış pakette yazılır ve test edilir |
| `Diten.MdmService` (MOD-0289) | var — `PlatformAuditForwarder` (yol `b`; anahtar boşken sessiz atlama açığı K8) | `b` | — ; K8 açığı MOD-0289 pack'inde risk olarak yazılır |

## 11. Scope

Bu DCP'nin kapsamı §5'teki üyeler ve §8.1 dilimleridir; aday modüller karar verilmeden kapsama girmez.

## 12. Explicit exclusions

Gerçek banka dağıtımı, banka geri çağrıları, güvenilir non-HTTP çalışan, MOD-0121'e ekstre yayımı; MOD-0125, MOD-0121,
MOD-0118 GL, MOD-0146; veri taşıma ve canlıya geçiş (ayrı karar, TP-001 §R); kaynak projedeki her değişiklik.

## 13. Governance drift risks

- Kaynak paketlerin yeni sürümleri gelirken pack'lerin eski sürüme dayanması → her pack bağlandığı paket manifest
  SHA'sını yazar.
- Kaynak kusurlarının "kaynakta böyleydi" diye taşınması (NoOp denetim, `Guid.Empty` kiracı, sabit kiracı varsayılanı,
  `*` izin joker'i) → §M uyum delta'sı her pack'te.
- MOD-0140'ın aynı anda MVP-2 tarafından değiştirilmesi → Treasury MOD-0140'a yazmaz (tek yazar).
- TP-001'in taslak kalması → level-1 kayıtları "taslak kurala göre" etiketlenir (D6).
- DCP numarasının çakışması → numara sahip/EA tarafından atanır.

## 14. Review questions

1. TRM-S1'de MOD-0269 sözleşmesi ayrı bir pack mı ister, yoksa sözleşme dosyası + MOD-0126 pack'inde bağımlılık
   satırı yeter mi?
2. MOD-0126 ödeme onayları AUD-001 K2 (fail-closed) sınıfında mı?
3. MOD-0003 için süreli sapma (T32) önerisi kabul edilebilir mi?
4. MOD-0289'un MDM'ye yerleşimi MOD-0220 ilişki modelini aynı dilimde mi ister?

## 15. Gate criteria

- DCP `approved` olmadan hiçbir üye pack'i `ready-for-dev` olmaz.
- Bir dilim, üyelerinin pack'leri `ready-for-dev`, kod alma izni verilmiş ve level-1 paket kabulü kayıtlıyken başlar.
- MOD-0126 `ready-for-dev` için: MOD-0269 sözleşmesi, SoD tanımı (tek ve para birimli eşik), MOD-0126 v1.2 paketi ya da
  sahibin "v1.1 ile devam" kararı.

## 16. Acceptance criteria

- TRM-S1: Recovery'de ödeme partisi oluşturma, gönderme, onaylama ve reddetme; hazırlayan ve gönderen onaylayamaz;
  eşik üstü ikinci kademe; her yazma komutu denetimli; kiracılar arası 404; 7 dil; `[HasPermission]`; banka dağıtımı
  "yapılandırılmamış" döner ve kayıt bırakır.
- Her dilim: Recovery derleme + test + çalışma kanıtı (level-2), kabul kaydı (`acceptance_record.py`).

## 17. Downstream business-module impacts

- MOD-0140: tedarikçi durumunu yalnız sözleşmeden (1.3.0, aktif şirket ilişkisi) okuyan tüketiciler; doğrudan
  koleksiyon okuması yok.
- MOD-0149: `CustomerId` eşlemesi (AR).
- MOD-0220: LE↔LE ilişki modeli ve sözleşmesi.
- MOD-0023: ödeme onay şablonları.

## 18. Open decisions

0.3 MOD-0003 · 0.5 AR fatura sınırı · 0.6 ERP-FIN-BUNDLE (EA) · 0.7 GL · MOD-0289 MDM onayı · MOD-0217 ve MOD-0057
(TRM-13) · DCP numarası · bankacılık sahibi · AGENTS §2/§3/§9 ve `ports.md` değişikliği (port 5071 kararı D10/3 verildi).

## 19. Future follow-ups

Dosya ile ödeme değişimi (karar 0.1 seçenek B); gerçek sağlayıcı seçimi (C); MOD-0121 Bank Reconciliation; MOD-0125
Cash Forecasting (MOD-0004 ve MOD-0063 ile); TP-001'in `.antigravity/rules/`'a alınması; kaynak commit'lerin
`git bundle` yedeği.

## 20. Audit and reconciliation notes

Taslak; implementasyon başlamadı. Ölçüm tabanı origin/main 96bedc893. Kaynak bütünlüğü ve içerik incelemesi:
`docs/records/audits/2026-10/treasury-intake-handover-review-01/REPORT.md`.
