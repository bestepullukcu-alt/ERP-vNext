# SCMM — Structured Content & Messaging Management (Content Studio) — İş Planı

> **Control Tower kaydı (SoR).** Kaynaklar (otorite): `DiTEN SCMM IT Transformation Plan v1.1.docx` (6 Eyl 2026), `ucln-workflow-standalone.html` (UCLN baseline, Figma-kaynaklı), `Project ongoing status report v 06 SEP 2026.xlsx` (sheet "Content Messaging Cap Block" + PVG/HR&TEP/AI readiness). Bu dosya üç kaynağın CT sentezidir + blueprint çapraz-referansı. Chat SoR değildir (K11).
> **Branch:** feature/structured-content-messaging. **Tarih:** 2026-09-07.
> ⚠️ **SCMM-xx = planlama referansı, kayıtlı MOD/FU DEĞİL.** Kanonik kimlik R0'da (G0) verilir.

## 0. Yönetici özeti — "yapılmış mı?"

**Hayır — capability olarak yapılmamış; ama ALT KATMANI büyük ölçüde hazır.** UCLN gerçek kapsam ölçüsü (HTML J.2): **~%46 = "temeli hazır, bina yok."**
- **Reuse/extend (var):** kavram tipleri, ilişkiler, template disiplini, içerik deposu (①②③⑥ + KnowledgeContent).
- **New-build (sıfır):** çözülmüş içerik-seti/kapsam yaşam döngüsü, composition/release semantiği, onay akışı (④⑤ + Review/Release).
- Excel register: **28 WP (SCMM-01…28) hepsi "Not started", closure 0.**
- **Hiçbir shared foundation'da runtime PASS yok** (PVG static audit 2026-08-03) → baseline doğrulama (SCMM-02) zorunlu ön koşul.

## 1. Kimlik & blueprint çapraz-referansı (§4.1)

| Bulgu | Durum | Aksiyon |
|---|---|---|
| "Structured Content and Messaging Management" | Registry/blueprint'te **kayıtlı modül yok** | Kanonik kimlik R0/G0'da |
| **CAND-CAP-0011** (yeni Marketing owner adayı — ④⑤⑥+onay) | Registry'de **boşta/rezerve değil** (teyit edildi) | SCMM-01 + DCP-002 kapısı ile rezerve et |
| **BL-316** (ConceptGraph MOD-0162 SoR dışına yazıldı; UCLN 3 yerde 3 sahip) | `docs/product-backlog.md` **AÇIK/sahipsiz** | **SCMM-01/D01 linchpin** — G0'da owner kararı |
| **MOD-0162** Knowledge Base | registry `reserved/planned` ama **kod inşa edilmiş** (ConceptGraph) — registry drift | reuse temeli; SoR genişletme G0 |
| **MOD-0040 / MOD-0288** (docx: identifier / position-delegation) | registry ikisi de **"Tenant Org Foundation → deprecated"**; PVG MOD-0040 = WRONG_BOUNDARY/CONF-01 | 🔴 **ID uyuşmazlığı** — G0'da netleştir |
| **MOD-0057 / 0058** (taxonomy/graph), **MOD-0025** (rules), **MOD-0066/0068/0069** (AI) | registry'de **kayıtlı değil** / AI %0-5 | "if adopted"; R3 AI deps mevcut değil |
| **MOD-0290** Product | `canonical` ✓ | hazır |
| **MOD-0165/0166** Campaign/Journey | `reserved/planned`; **MOD-0166 Excel'de deferred** | R2 tüketici; Journey ayrı pack+runtime şartı |

## 2. Current-state reuse-vs-build haritası (UCLN ①–⑥, HTML J.2)

| Legacy ekran | vNext karşılığı (aggregate) | % | Karar |
|---|---|---|---|
| ① UCLN Type | `ConceptType` | 90 | **reuse** + color/isGroup/isList/parent ekle |
| ② UCLN Connection | `ConceptRelationship` | 90 | **reuse** + birleşik değer+kenar yazma ergonomisi |
| ③ Book Design | `ConceptChainTemplate` | 50 | **extend**: paralel hat, kardinalite, moderator, for-whom ekseni |
| ④ Strategy Template | **yok** (isim çakışması: MOD-0167-FU04 StrategyTemplate alakasız) | 0 | **new-build** → "Message Scope" |
| ⑤ UCLN Book | **yok** (KnowledgePath ≠ Book) | 0 | **new-build** → "Argument Set" (çözülmüş kavram zinciri) |
| ⑥ Book Pages | `KnowledgePath` Steps builder | 40 | **extend**: Parent/Child + iki-panel → "Content Assembly" |
| İçerik deposu | `KnowledgeContent` (video/message-script tipleri hazır) | 95 | **reuse** |
| Onay akışı | **yok** | 0 | **new-build** (MOD-0023 üstünde) |

**Yapısal blocker (HTML):** `AudienceProfile` bugün **tek-boyutlu** → çok-eksenli + Subject-bağlı olmalı (SCMM-11 eligibility önkoşulu).
**Legacy migrasyon eşlemesi (docx §11):** Type/Connection→concept/link · Book Design→composition template · Strategy Template→context/scope · Book→content set · Book Pages→assembly refs. "Situation Clarification" ve bilinmeyen Overview → iş kararına kadar çözümsüz. "Completed" ≠ onay kanıtı.

## 3. Shared-foundation readiness (Excel PVG — static audit 2026-08-03, runtime PASS YOK)

| Modül | SCMM rolü | PVG durumu | CT gate hedefi |
|---|---|---|---|
| MOD-0018 RBAC | authz (SCMM-05) | 62–75%, **HARDEN** (ABAC/data-scope+admin UI eksik) | G1 runtime proof |
| MOD-0021 Audit | audit (SCMM-05) | 70–80%, runtime-not-proven, **HARDEN** | G1 authenticated smoke |
| MOD-0023 Workflow | review routing (SCMM-06) | 60–72%, **HARDEN** (golden-flow smoke + designer UI) | G1 |
| MOD-0028 Doc | doc/evidence (SCMM-07) | 68–78%, **HARDEN** (FU06 Mongo partial-index startup blocker) | G1 |
| **MOD-0031 Evidence Linking** | evidence (SCMM-07) | **SPEC_ONLY 8–12%, %0 prod, CONTRACT_ONLY** | 🔴 **COMPLETE_FOUNDATION** — neredeyse yok |
| MOD-0262 Internal Document Repository | binary (SCMM-07) | planned/missing %0 | pinned version / **R1** (Blueprint 8.1: internal platform service, W-1, R1-PPM MVP, RC=Y — external repo DEĞİL) |
| MOD-0040 | identifier (SCMM-08) | 25–35%, **WRONG_BOUNDARY/REBUILD** (CONF-01) | G0 boundary kararı |
| MOD-0032 Gateway | consumption (SCMM-04/08) | review/partial, "integrate" | G1 |
| MOD-0290 Product | ref (SCMM-08) | canonical ✓ | hazır |
| MOD-0026 Scheduler / MOD-0027 Notif | output/impact (SCMM-16/18) | done / approved | hazır |
| MOD-0066/0068/0069 AI | R3 (SCMM-25) | **0–5% not runtime-ready** | R3 conditional gate |

🔴 **Kanıt çelişkisi (SCMM-02'nin çözeceği):** HR&TEP sheet MOD-0028 **ve** MOD-0031'i "Done" işaretliyor; PVG aynı modülleri partial / spec-only-%0 diyor. **SCMM-02 bunu adlandırılmış revizyon+ortamda uzlaştırmalı.** (Diğer blokları düşürme / yeni defect iddia etme — sadece uzlaştır.)

## 4. Work-package register (SCMM-01…28) — release · owner · modül · bağımlılık · gate

### R0 — Baseline & kararlar (Gate G0)
| WP | İş | Owner | Modül | Dep | CT notu |
|---|---|---|---|---|---|
| SCMM-01 | BL-316 çöz + capability ownership | Architecture+sponsor | MOD-0162 + CAND-CAP-0011 | — | ✅ **DECIDED (H, 2026-09-07, DEC-SCMM-01)** — CAND-CAP-0011 rezerve, MOD-0167'den UCLN kaldırıldı, BL-316 RESOLVED. **G0 owner-kimlik kapısı AÇIK.** |
| SCMM-02 | Kod+runtime baseline doğrula | Tech lead | tüm shared deps | — | ✅ **ACCEPTED (E1, 2026-09-07)** — WP-SCMM-02. `regulated`=greenfield, AudienceProfile blocker teyitli, reuse/build haritası kanıtlı. Açık: MOD-0031 riski + E3 (fleet cold). |
| SCMM-03 | Model+policy kararlarını dondur | Domain arch+content owner | MOD-0162 + CAND-CAP-0011 | 01,02 | ✅ **DECIDED (2026-09-07)** — DEC-SCMM-03, 11/11 karar kilitli (regulated greenfield-decouple, AudienceProfile multi-axis, rename adopt). **G2 önkoşulu sağlandı.** |
| SCMM-04 | Integration+delivery spec onayı | API+security+QA | MOD-0032 + domain | 03 | ✅ **APPROVED (2026-09-07)** — DEC-SCMM-04; 6 contract framework kilitli, blocker'lar (C1-evidence/MOD-0031, C3/MOD-0023, C2/MOD-0025, C1-position/MOD-0288) G1'e. **→ G0 ✅ KAPALI. R1 (SCMM-05→08) açık.** |

### R1 — Shared foundation (Gate G1: runtime proof + remediation)
| WP | İş | Owner | Modül | Dep | CT notu |
|---|---|---|---|---|---|
| SCMM-05 | Authz+audit harden | Platform+security | MOD-0018+0021 | 04 | 🟠 **GATE NOT PASSED (2026-09-07, WP-SCMM-05)** — MOD-0018/0021 HARDEN; release yolu 🔴 BLOCKED (audit fail-soft). Remediation: **R1** MOD-0021 durable-audit (owning-team) · **R2** MOD-0018 ABAC (owning-team) · **S1** canonical key seed / **S2** Knowledge audit-wiring / **S3** SoD (bizim). **S1 → SCMM-09 authz'ını açar (owning-team'e bağlı değil).** |
| SCMM-06 | Review routing+delegation kanıtla | Workflow lead | MOD-0023(+0288?) | 04 | 🔴 MOD-0288 kimlik netleştir |
| SCMM-07 | Doc+evidence servisleri kanıtla | Doc/evidence lead | MOD-0028+0031+0262 | 04 | 🔴 MOD-0031 spec-only → foundation tamamla |
| SCMM-08 | Ref+integration servisleri qualify | Integration lead | MOD-0290+0288+0032 | 04 | MOD-0040 boundary |

### R1 — Model & eligibility (Gate G2)
| WP | İş | Owner | Modül | Dep |
|---|---|---|---|---|
| SCMM-09 | Concept catalog+relationships extend (①②) | CRM/domain | MOD-0162 | 03,04,05 |
| SCMM-10 | Versioned composition templates (③) | Domain+frontend | MOD-0162 (composition owner G0) | 09 |
| SCMM-11 | Context & eligibility evaluation | Domain | Marketing owner + MOD-0025 | 03,08,10 · **AudienceProfile çok-eksen önkoşul** |

### R1 — Content & authoring (Gate G2)
| WP | İş | Owner | Modül | Dep |
|---|---|---|---|---|
| SCMM-12 | Reusable components + claims (⑥ malzeme) | Content domain | MOD-0162 + claim owner (G0) | 07,09,11 |
| SCMM-13 | İki dil varyant takibi | Content+localization QA | Content+Marketing owner | 12 |
| SCMM-14 | **Content Studio** + optional scopes (④⑤⑥) | Frontend+domain | Marketing owner | 10,11,12,13 · clone-to-draft, provenance, no inherited approval |

### R1 — Review & release (Gate G3)
| WP | İş | Owner | Modül | Dep |
|---|---|---|---|---|
| SCMM-15 | Revizyon freeze + review kararları bağla | Domain+workflow | Marketing owner + MOD-0023 | 06,14 |
| SCMM-16 | Tek output format üret+doğrula (1 PDF) | Publishing lead | Marketing owner + MOD-0026 | 14,15 |
| SCMM-17 | Release yetki + managed withdrawal | Domain+release eng | Marketing owner | 15,16 |

### R1 — Impact, migration & pilot (Gate G4)
| WP | İş | Owner | Modül | Dep |
|---|---|---|---|---|
| SCMM-18 | Dependency impact + actions | Domain+integration | Marketing owner + MOD-0024/0027 | 12,13,15,17 |
| SCMM-19 | Legacy migration + rollback provası | Data migration | MOD-0162 + Marketing owner | 14,17,18 |
| SCMM-20 | **Tam pilot + release gate** | QA+business owner | Capability block | 05,06,07,08,17,18,19 |

### R2 — Scale (Gate G5)
| WP | İş | Modül | Dep |
|---|---|---|---|
| SCMM-21 | Campaign delivery + recall | MOD-0165 (MOD-0166 conditional/deferred) | 20 |
| SCMM-22 | Bulk import + clone scaling | Marketing owner | 20 |
| SCMM-23 | Localization + ek output'lar (XLIFF R2) | Marketing owner | 20 |
| SCMM-24 | Scale monitoring + operasyonel kontrol | Marketing owner | 20 |

### R3 — Optional automation (Gate G6, koşullu)
| WP | İş | Modül | Dep |
|---|---|---|---|
| SCMM-25 | AI governance deps qualify | MOD-0066/0068/0069 (0–5%) | 20 |
| SCMM-26 | Doc extraction + link suggestions | MOD-0162 + governed AI | 25,22 |
| SCMM-27 | Modular video + variants | Marketing owner + publishing adapter | 18,23,24 |
| SCMM-28 | Benefit + production expansion onayı | Capability block | 26,27 |

## 5. Gate & acceptance modeli

**Gates:** G0 baseline+SoR karar (enterprise architect) · G1 shared-foundation runtime proof+remediation (platform lead) · G2 model+authoring · G3 review+release · G4 pilot kabul (business owner + IT release authority — spec/ekran varlığı ≠ completion) · G5 scale · G6 optional.
**Acceptance testleri (AT01–AT10):** model config · component reuse · eligibility/policy · review (stale/double-submit) · output+release (manifest-bound, retry no-dup) · source impact (her iki kullanım+dil) · restriction+withdrawal · **security+durability (tenant/role denial, optimistic conflict, audit/redaction, L3 persistence)** · migration (count/link/provenance reconcile, unknown→quarantine) · operations+benefit. Her test: code revizyon+ortam+actor+data+beklenen/gerçek+kanıt; build/unit tek başına yetmez — authenticated gateway/browser flow + denial + duplicate-submit + restart/replay şart.

## 6. R0'da kilitlenmesi gereken kararlar (docx §12)
- **D01/SCMM-01:** BL-316 seçeneği + kanonik owner (Architecture+sponsor).
- **D02:** template/component/claim ownership (öneri: composition-specific → Marketing owner; mevcut knowledge → MOD-0162; shared evidence → MOD-0031).
- **D03/SCMM-03:** meaning/permission/presentation ayrımı — **regulated-flag decouple** (HTML tek-flag coupling'ini bilerek reddet).
- **MOD-0040/0288 kimlik uyuşmazlığı** (registry deprecated vs docx kullanımı).
- Named R0 owner + karar tarihleri **G0'dan önce** atanmalı.

## 7. CT risk kaydı
- 🔴 Hiçbir shared foundation'da runtime PASS yok (static audit) → G1 kapıları gerçek runtime kanıtı ister.
- 🔴 MOD-0031 (evidence linking) spec-only %0 — SCMM-07'nin gerçek foundation-build'i, "harden" değil.
- 🔴 Kanıt çelişkisi (HR&TEP "Done" vs PVG partial) — SCMM-02 uzlaştırır.
- 🟠 İsim çakışması StrategyTemplate → migrasyon/kod karışıklığı; rename kararı G0.
- 🟠 Estimate'ler (HTML %46, J.1) completion değil; `module-implementation-status.md` bayat (2026-06-23).
- 🟠 Pilot'un tam marketing suite'e taşması → R1 exclusions + change control (sponsor).

## 8. CT önerisi — ilk dispatch (R0, gate G0)
Sadece **R0 (SCMM-01→04)** hazır; hepsi **karar/inspection ağırlıklı (Profile C/karar)**, kod değil:
1. **SCMM-01** — BL-316 + ownership + CAND-CAP-0011 DCP-002 rezerve (owner kararı).
2. **SCMM-02** — baseline doğrulama WP (VER lane): adlandırılmış revizyon+ortamda shared-foundation runtime + HR&TEP↔PVG uzlaştırma.
3. **SCMM-03/04** — model+policy freeze + contract onayı (kararlar).

R1+ implementasyonu **G0 geçmeden dispatch edilmez** (DoR: kimlik+contract+SoR açık olmalı).
