---
id: UNASSIGNED
slug: finance-treasury-intake
name: Finance & Treasury — kaynak projeden doğrulayarak alma (Treasury intake)
type: Delivery Capability Pack
standard: CAP-001
status: draft
status_note: "draft — yalnız planlama; production implementation tetiklemez (CAP-001 §5). DCP numarası atanmadı: mevcut dosyalarda DCP-004/005/006 birden çok kez kullanılmış, numara tahsisi sahip/EA kararıdır; o zamana kadar UNASSIGNED. Onaydan önce bağımsız inceleme (CT-APR-02, SOP §20.4) ve sürüme bağlı onay (CT-APR-01) gerekir."
owner_domain: finance-treasury
owner: depo sahibi (K1) / Treasury Intake Control Tower
created: 2026-10-23
updated: 2026-10-25
authoring_branch: chore/ct/treasury-intake-ct-01
canonical_source: "docs/reference/blueprint/System Capability & Implementation Blueprint - master 8.8.xlsx#Blueprint_Data"
canonical_modules: [MOD-0126, MOD-0269, MOD-0119, MOD-0120, MOD-0124, MOD-0289]
candidate_modules: [MOD-0217]
parked_modules: [MOD-0057]
runtime_code_allowed: false
runtime_code_scope: "yok — üye pack'lerin hepsi draft; hiçbiri ready-for-dev değil. Kod alma izni (D27/5) MOD-0126/0119/0120/0124 için verildi, ama kod ancak pack ready-for-dev + DCP approved olunca ve dilim sırasıyla yazılır."
inputs:
  - "docs/records/decisions/2026-10/treasury-intake-owner-decisions-01.md (D0–D8)"
  - "docs/records/decisions/2026-10/treasury-intake-owner-decisions-02.md (D9–D29)"
  - "docs/records/decisions/2026-10/treasury-mod0126-placement-decision-01.md"
  - "docs/records/decisions/2026-10/treasury-level1-acceptance-01/ (11 level-1 kaydı)"
  - "docs/records/audits/2026-10/treasury-intake-handover-review-01/ (REPORT, gap listeleri 1–19, üretici cevapları)"
  - "execution/domains/finance-treasury/domain-config.md (taslak)"
  - "docs/analysis/contracts/bank-dispatch.md (FROZEN 0.1.0), drafts/legal-entity-lookup-validation-v1.1-proposal.md, drafts/bank-account-lookup-validation-v1.0-proposal.md"
  - "docs/roadmap/plans/erp-finance-legal-module-intake-01/ (TP-001 v1.5 taslağı, TREASURY-MIGRATION-SEQUENCE-v1.0, DESTINATION-BRIEF, yerleşim notları, SoD değerlendirmesi, CT-QUEUE.tsv)"
  - "depo dışı: /Users/natig/Projects/treasury-transfer-handoff/ (11 producer paketi + kaynak kanıtları; SHA256SUMS doğrulandı)"
---

# Finance & Treasury — Treasury intake (Delivery Capability Pack, TASLAK)

> **Güncelleme (2026-10-25, gap 19/1):** bu DCP sahip kararları D20–D29 ve güncel pack'lerle hizalandı. Üyelerin hepsinin
> draft pack'i ya da sözleşmesi var; hiçbiri `ready-for-dev` değil.

## 1. Identity and status

- **Kimlik:** UNASSIGNED (numara bekliyor) · **durum:** `draft` · **standart:** CAP-001.
- **Neden DCP (CAP-001 §2):** modüller bir bağımlılık sırasıyla teslim edilmek zorunda; domain'ler arası kararlar var
  (finance-treasury, master-data-management, platform-shared-services, procurement, commercial-suite); birden çok iş
  modülü ortak temele dayanıyor (servis ortak yapı taşları, MOD-0269 sözleşmesi, MOD-0023 §17, CentralAudit).
- **Bu belge ne değildir:** module pack değildir; üye modüllerin kendi pack'lerinin yerine geçmez; kod, port, servis
  iskeleti ya da kod alma izni vermez (CAP-001 §7).

## 2. Business outcome

ERP-vNext kaynak projesinde yazılmış Treasury modüllerinin (ödeme ve onay, borç, alacak, nakit pozisyonu, banka ana
verisi) Recovery'ye **doğrulanarak ve Recovery standartlarına uyarlanarak** alınması (D1); Recovery'de ödeme partisinin
hazırlanıp onaylanabildiği ve onay ayrılığının (SoD) zorlandığı ilk dikey dilim.

## 3. Problem statement

- Intake başında (2026-10-23) Recovery'de finance / treasury domain'i, servisi, üye modüllerin kaydı, pack'i ve kodu yoktu.
- Kaynak kod başka mimaride (`DitenErpTreasuryService`, `Diten.Platform`, kaynak MDM), başka portlarda ve bilinen
  eksiklerle (NoOp denetim, uzak çağrının yerel kayıttan önce yapılması, `[Authorize]`-only uçlar, `Guid.Empty` kiracı).
- Kaynak paketler (TP-001 v1.5 producer packets) bilgi taşır, kod taşımaz.
- **Bugün (2026-10-25):** domain-config taslağı, beş üye pack'i (draft), MOD-0269 sözleşmesi (FROZEN), iki dış modül eki
  (MOD-0220 §22, MOD-0023 §17, ikisi de `proposed`) ve 11 level-1 kaydı var; kod yok.

## 4. Capability boundary

**İçeride:** ödeme partisi / talimat / onay / serbest bırakma (MOD-0126); satıcı borcu (MOD-0119); müşteri alacağı,
kısıtlı yerel MVP (MOD-0120, D27/2); nakit pozisyonu (MOD-0124); bankaya giden arayüz sözleşmesi, dağıtım kapalı (MOD-0269);
banka ana verisi ve banka hesabı sicili (MOD-0289, MDM'de).
**Dışarıda:** gerçek banka dağıtımı ve banka kimlikli geri çağrılar (karar 0.1 = A, D5); GL (0.7 = kapsam dışı, D27/4);
ERP-FIN-BUNDLE (0.6, D27/3); faturalama entegrasyonu (S-2, D27/2); Cash Forecasting MOD-0125; Bank Reconciliation MOD-0121;
MOD-0146; tedarikçi, müşteri ve LE ana kayıtları (kendi domain'leri).

## 5. Member modules and follow-ups

| modül | Blueprint (8.8) | Recovery durumu (kayıt · pack · kod) | rol |
|---|---|---|---|
| MOD-0126 Payments & Approvals | W-3, Treasury & Cash | `reserved / planned` · draft (AC-1…41 + 6a) · 0 | üye — ilk (TRM-S1); kod alma izni D27/5 |
| MOD-0269 Banks/SWIFT/Payments Gateway [External Provider] | W-1, External Provider | kayıt yok (dış sağlayıcı) · **pack yok, sözleşme `bank-dispatch.md` FROZEN 0.1.0** (D27/7) · 0 | üye — yalnız arayüz sözleşmesi + kapalı bağdaştırıcı (MOD-0126 içinde) |
| MOD-0119 Accounts Payable (AP) | W-3, Financial Management | `reserved / planned` · draft (AC-1…41) · 0 | üye (TRM-S2); kod alma izni D27/5 |
| MOD-0120 Accounts Receivable (AR) | W-3, Financial Management | `reserved / planned` · draft (AC-1…43) · 0 | üye (TRM-S2); kod alma izni D27/5 |
| MOD-0124 Cash Positioning | W-3, Treasury & Cash | `reserved / planned` · draft (AC-1…37) · 0 | üye (TRM-S2); kod alma izni D27/5 |
| MOD-0289 Bank Master & Bank Account Registry | W-3, Treasury & Cash | `reserved / planned` · draft (AC-1…58, master-data-management) · 0 | üye (TRM-S3) — MDM'de (D20/1); kod alma izni **verilmedi** |
| MOD-0217 Contract Lifecycle Management (CLM) | Legal Governance | 0 · 0 · 0 | aday — yer MDM (D19/1, D20/2); pack TRM-11 cevabı + Document Management bağımlılığından sonra |
| MOD-0057 Semantic Tagging & Taxonomy Management | W-4 | 0 · 0 · 0 | **park** (D19/2) — MOD-0053/0056 gelince yeniden açılır |

**Üye olmayan, değişiklik ya da teyit alacak modüller** (XMC-001; D9 izniyle yalnız ekleme):

| modül | ne | durum |
|---|---|---|
| MOD-0220 Legal Entity (MDM) | §22 `approvalStatus` eki, LEGAL-ENTITY-LOOKUP 1.1.0 (TRM-14); LE↔LE ilişki modeli **gerekmez** (TRM-07) | belge PASS; §22 `proposed` |
| MOD-0023 Workflow (platform-shared-services) | §17: silmeme politikası + guard, okuma alanları, fingerprint düzeltmesi, yarış testleri (TRM-15) | belge PASS; kod MOD-0023 sahibinin WP'siyle (D25/1) |
| MOD-0143 Invoice Match (procurement) | `invoice-match` 1.1.1 "net" açıklaması + `lineAmount` kontrolü (D26/3) | procurement tarafında |
| MOD-0140 Supplier (procurement) | `supplier.openapi.yaml` 1.3.0 (aktif şirket ilişkisi) — bugün 1.2.0 | MVP-2 main'e girince |
| MOD-0149 Customer 360 (commercial-suite) | müşteri doğrulama + dış referans eki (AR O-3, D29/1 sahip payı); kayıt / pack durum çelişkisi (D27/12) | sahibine not |
| MOD-0048 Reference Data (platform-shared-services) | para birimi küsurat hanesi; `payment-method` kümesi (MOD-0126 O-13, D28) | üretici bağımlılığı, açık |

## 6. Ownership map

| konu | sahibi |
|---|---|
| DCP, sıra, kapsam kararları | depo sahibi (K1) |
| finance-treasury domain'i, MOD-0126/0119/0120/0124 pack'leri | finance-treasury (Treasury Intake CT hazırlar; sahip onaylar) |
| MOD-0289, MOD-0217 yerleşimi | master-data-management — MDM sahibi kararını sahip (K1) verdi (D20) |
| MOD-0269 arayüz sözleşmesi; bankacılık sahibi | sahip (K1) — 0.1 için bankacılık sahibi K1 (D27/8); gerçek sağlayıcı seçilince ayrı sahip |
| Port ve bant | 5071 sahip kararı (D10/3); AGENTS §2/§3/§9 ve `ports.md` güncellendi (D30) |
| Gateway rotaları | yalnız `integration-agent` |
| ERP-FIN-BUNDLE | **KARAR D27/3**: v1'de yok, modül sözleşmeleri |
| RCS-001 önekleri | `PAY` / `APD` / `ARI` / `CPS` / `CTP` / `BNK` / `BAC` (D29/2); RCS-001 §3 tablosuna yazıldı (D30) |
| Kaynak paket içeriği ve yeni sürümleri | kaynak Treasury Migration CT; karar sahibi K1 |

## 7. Dependency graph

```text
MOD-0269 BANK-DISPATCH 0.1.0 FROZEN ──┐
MOD-0023 Workflow + §17 (sahibin WP'si) ┼─> MOD-0126 (servis iskeleti + ortak yapı taşları, DL-1) ──> MOD-0119, MOD-0120, MOD-0124
CentralAudit (Building.Blocks, var) ───┘           │
MOD-0220 + §22 approvalStatus (TRM-14) ───────────>┤ (dört Treasury modülü: şirket Active + Approved, D10/1)
MOD-0048 para birimi hanesi + payment-method ─────>┘
MOD-0003 yok ──(D27/1: süreli T32 sapması, sözleşmeler docs/analysis/contracts/)──> dört Treasury modülü
MOD-0143 invoice-match 1.1.0 (+1.1.1 açıklama) ──> MOD-0119 · MOD-0140 supplier 1.3.0 ──> MOD-0119, MOD-0289
MOD-0149 müşteri eki ──> MOD-0120 · MOD-0037 (var) ──> MOD-0124
MOD-0289 (MDM) ──BANK-ACCOUNT-LOOKUP 1.0.0 (taslak)──> MOD-0126, MOD-0119, MOD-0120, MOD-0124 (opak Guid referans)
```

MOD-0126 → MOD-0289 bağı opak referanstır; MOD-0289 gelene kadar Treasury pack'leri opak referans alanıyla çalışır
(MOD-0126 O-15, MOD-0124 O-15 — D28).

## 8. Ordered delivery sequence

1. **Kararlar (Dalga 0):** 0.0 (D6 ✓), 0.1 (D5 = A ✓), 0.2 (MOD-0126 yerleşimi ✓), 0.3 (D27/1 ✓), 0.5 (D27/2 ✓), 0.6
   (D27/3 ✓), 0.7 (D27/4 ✓), MOD-0289 MDM onayı (D20/1 ✓), MOD-0217/0057 (D19 ✓), port 5071 (D10/3 ✓; AGENTS / ports.md D30 ✓),
   kod alma izni (D27/5 ✓ — MOD-0126/0119/0120/0124), pack açık kararları (D28 ✓, D29 sahip payı ✓).
2. **Kaynak yeni sürümleri:** ✓ alındı ve doğrulandı (MOD-0126 v1.2, MOD-0119 v1.1, MOD-0289 v1.7, MOD-0220 v1.3; batch 7).
3. **Level-1 paket kabulleri:** ✓ 11/11 koşullu kabul (TRM-02).
4. **Onay:** belge onay PR'ı (D18; sahip push / PR onayı bekleniyor) → CT-APR-02 bağımsız inceleme → CT-APR-01 onay satırları
   → DCP numarası + `approved` (AGENTS / ports ve RCS yamaları D30 ile uygulandı).
5. **Dilim TRM-S1:** MOD-0126 `ready-for-dev` → servis iskeleti (`Diten.FinanceTreasuryService`, port 5071) + ortak yapı
   taşları (DL-1) → alma ve uyarlama → Recovery testleri (dilim kabul kapısı). DL-2 MOD-0023 §17 WP'si main'de olunca.
6. **Dilim TRM-S2:** MOD-0119, MOD-0120, MOD-0124 (MOD-0126 DL-1'den sonra; birbirinden bağımsız, paralel).
7. **Dilim TRM-S3:** MOD-0289 (MOD-0140 1.3.0, O-6 güvenlik onayı ve ayrı kod alma izninden sonra).
8. **Dilim TRM-S4:** MOD-0217 (koşullar kapanınca). MOD-0057 park.

## 8.1 Delivery slices

| Slice | Kapsam | Not |
|---|---|---|
| TRM-S1 | MOD-0126, MOD-0269 | ilk dikey dilim; MOD-0269 yalnız FROZEN sözleşme ve kapalı bağdaştırıcı |
| TRM-S2 | MOD-0119, MOD-0120, MOD-0124 | kapı kararları verildi (D27/1–4); MOD-0126 DL-1'e bağlı |
| TRM-S3 | MOD-0289 | MDM (D20/1); draft pack (AC-1…58); MOD-0140 1.3.0, O-6 güvenlik onayı, ayrı kod alma izni |
| TRM-S4 | MOD-0217 | aday; yer MDM (D19/1), pack koşullu |

## 9. Prerequisites

- Kaynak kod doğrulaması: bağlanan commit'te derleme ve test (kanıt `source-build-evidence-v1.0`: MOD-0126 510/510, 0124
  407/407, 0120 317/317, 0119 251/251; MOD-0289 ve MOD-0217 kırık — MOD-0289 testleri Recovery'de yeniden kurulur).
- Recovery tarafında: MOD-0023 §17 WP'si (D25/1); CentralAudit ortak iletici (`services/Diten.Building.Blocks`); MOD-0220
  §22; MOD-0048 küsurat hanesi ve `payment-method` kümesi; SEC-002 veri kapsamı tüketim yolu; RCS-001 ortak kod servisi
  (BL-565).
- Her modül için DCP-002 ön kontrolü ✓ ve kayıt satırı rezervasyonu ✓ (MOD-0126, 0119, 0120, 0124, 0289).

## 10. Architecture decisions

- **Servis:** `Diten.FinanceTreasuryService` (D2), domain `finance-treasury`, dal kodu `fin` (D3); port 5071 (D10/3).
- **Şirket uygunluğu (D10/1, D14/4):** para hareketi yaratan / değiştiren yazmalarda şirket `Active` + `Approved`;
  LEGAL-ENTITY-LOOKUP 1.1.0 sınıflandırması (geçersiz / çelişkili → 503, tanımlı uygun değil → 422).
- **Kod alma:** doğrula, sonra uyarlayarak al (D1); kopya değil; izin D27/5 (dört Treasury modülü, S `da4abb20`).
- **MOD-0269:** sağlayıcıdan bağımsız süreç içi port, dağıtım kapalı (D5); `bank-dispatch.md` FROZEN 0.1.0 (D27/7).
- **Contract-first, MOD-0003'süz:** sözleşmeler `docs/analysis/contracts/` altında dondurulmuş dosyalar (D27/1, T32 sapması);
  başka servisin koleksiyonu okunmaz.
- **Belirsiz uzak sonuç:** tek niyet durum sözlüğü; 404, zaman aşımı, 5xx, 409 `WORKFLOW_TRANSITION_CONFLICT` kesin ret
  değildir (D21/7, D25); yeniden yazma yalnız asıl aktör ve MOD-0023 §17 güvenceleriyle.
- **Denetim kaydı (CAP-001 §7.1, AUD-001):**

| servis | denetim altyapısı bugün | üyelerin yolu | altyapı yoksa kim kurar |
|---|---|---|---|
| `Diten.FinanceTreasuryService` (yeni) | yok — servis yok | `b`, CentralAudit ortak iletici (K4); para kararları K2 (D14/3) | servis iskeleti, MOD-0126 DL-1 |
| `Diten.MdmService` (MOD-0289) | iki yol: merkezi iletim (en iyi çaba) ve `mdm-guvenilir-kaynak-niyeti` (kapalı-başarısız, yalnız ürün koleksiyonları) | güvenilir niyet yolu genişletilir, K2 (D24/2); yeni defter satırları K2 (D27/11) | MOD-0289 DL-1 |

## 11. Scope

Bu DCP'nin kapsamı §5'teki üyeler ve §8.1 dilimleridir; aday ve park modüller kapsama girmez.

## 12. Explicit exclusions

Gerçek banka dağıtımı, banka geri çağrıları, güvenilir non-HTTP çalışan, MOD-0121'e ekstre yayımı; MOD-0125, MOD-0121,
MOD-0118 GL, MOD-0146; ERP-FIN-BUNDLE; faturalama entegrasyonu; veri taşıma ve canlıya geçiş (ayrı karar, TP-001 §R);
kaynak projedeki her değişiklik.

## 13. Governance drift risks

- Kaynak paketlerin yeni sürümleri gelirken pack'lerin eski sürüme dayanması → her pack bağlandığı paket manifest SHA'sını yazar.
- Kaynak kusurlarının taşınması (NoOp denetim, `Guid.Empty` kiracı, sabit kiracı varsayılanı, `*` izin joker'i) → pack'lerde
  taşınmayacaklar listesi.
- MOD-0140'ın aynı anda MVP-2 tarafından değiştirilmesi → Treasury MOD-0140'a yazmaz (tek yazar).
- TP-001'in taslak kalması → level-1 kayıtları "taslak kurala göre" etiketlenir (D6).
- T32 sapmasının süresiz kalması → bitiş: MOD-0003 çıkana kadar, en geç Treasury v1 sonu (D27/1); S-2: MOD-0169
  `ready-for-dev` ya da v1 sonu (D28/2).
- DCP numarasının çakışması → numara sahip / EA tarafından atanır.

## 14. Review questions

1. ~~TRM-S1'de MOD-0269 sözleşmesi ayrı bir pack mı ister?~~ — sözleşme dosyası (`bank-dispatch.md` FROZEN) + MOD-0126
   bağımlılık satırı ve kapalı bağdaştırıcı; ayrı pack yok (D27/7; sahip incelemeleri gap 7–8 PASS).
2. ~~MOD-0126 ödeme onayları AUD-001 K2 sınıfında mı?~~ — **evet (D14/3)**.
3. ~~MOD-0003 için süreli sapma (T32) kabul edilebilir mi?~~ — **KARAR D27/1: evet.**
4. ~~MOD-0289'un MDM'ye yerleşimi MOD-0220 ilişki modelini aynı dilimde mi ister?~~ — **hayır**; LE↔LE ilişki modeli
   eklenmez (TRM-07), MOD-0220 için yalnız §22 eki (TRM-14).

## 15. Gate criteria

- DCP `approved` olmadan hiçbir üye pack'i `ready-for-dev` olmaz.
- Bir dilim, üyelerinin pack'leri `ready-for-dev`, kod alma izni verilmiş ve level-1 paket kabulü kayıtlıyken başlar.
- Dilim kabulü (merge) dilim kabul kapısından geçer: AC geçişleri, gerçek replika seti testleri, build, tüketici kanıtı
  (gap 16/P2 — ready-for-dev kapısından ayrıdır).
- MOD-0126 `ready-for-dev` için: MOD-0269 sözleşmesi ✓ (FROZEN), SoD tanımı ✓ (D11), MOD-0126 v1.2 paketi ✓, MOD-0023 §17
  ve MOD-0220 §22 onayı, CT-APR-02/01.

## 16. Acceptance criteria

- TRM-S1: Recovery'de ödeme partisi oluşturma, gönderme, onaylama ve reddetme; hazırlayan ve gönderen onaylayamaz;
  eşik üstü ikinci kademe; her yazma komutu denetimli; kiracılar arası 404; 7 dil; `[HasPermission]`; banka dağıtımı
  "yapılandırılmamış" döner ve kayıt bırakır.
- Her dilim: Recovery derleme + test + çalışma kanıtı (level-2), kabul kaydı (`acceptance_record.py`).

## 17. Downstream business-module impacts

- MOD-0140: tedarikçi durumunu yalnız sözleşmeden (1.3.0, aktif şirket ilişkisi) okuyan tüketiciler.
- MOD-0149: müşteri doğrulama + dış referans eki (AR); kayıt durum çelişkisi sahibine bildirildi (D27/12).
- MOD-0220: §22 `approvalStatus` eki (TRM-14); 13 okuma noktasında beklenen uyumluluk, tüketici testleri sahiplerinde.
- MOD-0023: §17 (TRM-15) — kod sahibinin WP'siyle (D25/1).
- MOD-0143: `invoice-match` 1.1.1 açıklaması ve `lineAmount` kontrolü (D26/3).
- MOD-0048: küsurat hanesi ve `payment-method` kümesi.

## 18. Open decisions

**Kapandı (2026-10-25):** 0.3 (D27/1), 0.5 (D27/2), 0.6 (D27/3), 0.7 (D27/4), MOD-0289 MDM onayı (D20/1), MOD-0217 ve
MOD-0057 (D19), bankacılık sahibi (D27/8), kod alma izni dört Treasury modülü (D27/5), pack'lerde sahibe ait açık kararlar
(D28), ortak kararlarda sahip payı ve RCS önekleri (D29).

**Kapandı (D30):** AGENTS §2/§3/§9 + `ports.md` 5071 ve RCS-001 §3 önekleri uygulandı.

**Açık:** DCP numarası ve DCP onayı; ortak sahipli kararlarda diğer sahiplerin teyidi
(D29/1); CT-APR-02 / CT-APR-01; MOD-0289 O-6 güvenlik onayı; MOD-0289 ve MOD-0269 kod alma izni; AR O-8 muafiyet kısmı.

## 19. Future follow-ups

Dosya ile ödeme değişimi (karar 0.1 seçenek B); gerçek sağlayıcı seçimi (C); MOD-0121 Bank Reconciliation; MOD-0125
Cash Forecasting (MOD-0004 ve MOD-0063 ile); MOD-0003 Data Contract Registry (platform-shared-services, T32 bitişinden önce);
TP-001'in `.antigravity/rules/`'a alınması.

## 20. Audit and reconciliation notes

Taslak; implementasyon başlamadı. İlk ölçüm tabanı origin/main 96bedc893; son güncelleme dal
`chore/ct/treasury-intake-ct-01` üzerinde (2026-10-25). Kaynak bütünlüğü, içerik incelemesi ve gap listeleri:
`docs/records/audits/2026-10/treasury-intake-handover-review-01/`.
