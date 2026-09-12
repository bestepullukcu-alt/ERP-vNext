# DEC-SCMM-01 — Decision Brief: SCMM capability ownership + BL-316 çözümü + CAND-CAP-0011 rezervasyonu

> **Format:** §14 Decision Brief. **Durum:** ✅ `DECIDED` — **Seçenek H onaylandı (user/owner, 2026-09-07).** Record changes uygulandı; **G0 AÇIK.**
> **Bağlam WP:** SCMM-01 (R0, gate G0) — SCMM iş planının linchpin'i. Bu brief bir öneridir; kaydı değiştirmez. Onay sonrası §"Required record changes" uygulanır.
> **Kapsam notu:** Bu karar yalnız **SCMM/UCLN capability sahipliği** ile ilgilidir. HR&TEP / PVG sheet'leri ve diğer blokların foundation'ları **bu kararın dışındadır ve değiştirilmez** (yalnız tüketici olarak okunur).

---

**Decision ID:** DEC-SCMM-01 (docx D01)

**Context:**
SCMM (Structured Content & Messaging Management → Content Studio) legacy UCLN/Marketing akışının vNext karşılığıdır. Herhangi bir build'den önce **tek, kanonik bir kayıt-sahibi** gerekir (docx §1 ownership principle). Bugün UCLN sahipliği **çelişkili** ve concept-model kodu **beyan edilen sınırın dışına** yazılmış (BL-316). Bu netleşmeden SCMM-03 (model freeze) ve tüm R1+ dispatch edilemez (DoR / K12).

**Measured evidence (K2):**
- **BL-316 AÇIK/sahipsiz** — `docs/product-backlog.md:3328` (ölçüm 2026-09-01): 5 ConceptGraph aggregate (`ConceptType/Node/Relationship/ChainTemplate/KnowledgeContentConceptLink`) MOD-0162 koduna yazılmış (`services/Diten.CrmService/.../Domain/Entities/`), ama MOD-0162'nin beyan SoR'u yalnız "knowledge articles / article feedback".
- **UCLN üç yerde üç sahip:** Blueprint'te hiçbir modül; **registry `MOD-0167` "Owns Segment/TargetCustomer/UCLN"** (`module-id-registry.md:259`) + `crm-sor-boundary.md:21`; kodda **MOD-0162**. → registry iddiası ölçülen kodla çelişiyor.
- **CAND-CAP-0011 boşta** (registry son kimlik CAND-CAP-0010; teyit edildi) — yeni Marketing owner adayı, **rezerve değil**, DCP-002 kapısından geçmemiş.
- **Current-state (HTML J.2):** alt katman var (①②③⑥+içerik %40-95), üst katman (④⑤+onay) %0. Yani "yeni sahip" esas olarak **yeni üst-katman semantiğini** (composition/scope/release/approval) sahiplenecek; mevcut concept foundation zaten çalışıyor.
- **docx §3 önerisi:** composition-specific kayıtlar → yeni Marketing owner; mevcut knowledge kayıtları → MOD-0162; shared evidence → MOD-0031.

**Authority concern (§4.1):**
Canonical identity + capability boundary → **enterprise architect** (registry/DCP-002) + **business sponsor** (portfolio). CT önerir ve kaydeder; kararı bu ikili verir. CT scope'u genişletemez.

**Options:**

| # | Seçenek (BL-316) | Ne yapar |
|---|---|---|
| **A** | MOD-0162 SoR'unu genişlet (blueprint 8.2: `+ concept model`) | Concept model MOD-0162'de kalır; en az dosya değişikliği |
| **B** | MOD-0057 (Taxonomy) + MOD-0058 (Knowledge Graph) ile böl | Kavramsal olarak en doğru eşleşme |
| **C** | Yeni Marketing capability'sine taşı (CAND-CAP-0011) | Composition/release için yeni owner |
| **H (hibrit)** | **CAND-CAP-0011 = yeni Marketing owner** yalnız **üst katmanı** (content sets, optional scopes, composition revisions, release manifests, eligibility application, impact/withdrawal + onay akışı) sahiplenir; **MOD-0162** mevcut concept foundation'ı (ConceptType/Node/Relationship/ChainTemplate + KnowledgeContent) **korur**; shared evidence **MOD-0031**'de kalır | docx'in önerdiği yol |

**Trade-offs:**
- **A:** ✅ en düşük ilk-maliyet; ❌ Marketing işi `Service` capability grubunda kalır (grup hedefi=servis teslimi/SLA — yanlış grup), sınır bulanık kalır, gelecekte tekrar açılır (K8 deferral maliyeti).
- **B:** ✅ en temiz kavramsal eşleşme; ❌ **MOD-0057/0058 registry'de kayıtlı bile değil** → sert bağımlılık/blocker yaratır (K12), SCMM'i olmayan modüllere bağlar.
- **C (saf):** ✅ temiz sonuç; ❌ mevcut çalışan concept foundation'ı da taşımak = yüksek regresyon riski, sıfır işlevsel kazanç (BL-316 "taşıma maliyeti" notu).
- **H:** ✅ yeni semantiğe temiz owner + mevcut kodu taşımadan koruma (düşük regresyon) + doğru capability grubu (Marketing); ❌ MOD-0162 ile CAND-CAP-0011 arasında **composition-template sınırı** G0'da net çizilmeli (D02).

**Recommendation:** **Seçenek H (hibrit).** Gerekçe: current-state ölçümü (üst katman %0, alt katman çalışıyor) tam olarak "yeni owner yeni semantiği alır, mevcut foundation yerinde kalır"ı destekler; B'nin bağımlılık blocker'ından ve C'nin regresyon riskinden kaçınır; A'nın grup/sınır borcunu ödemez.

**Selected option:** **H (hibrit)** — user/owner onayı 2026-09-07. Blueprint çapraz-referansı (MOD-0162=Service grubu yanlış; Marketing grubunda content/mesaj sahibi yok = gap; MOD-0167 SoR=segment-only) A ve B'yi eledi; H, C'nin regresyon riski olmadan blueprint yönünü (Marketing owner) karşıladığı için seçildi.

**Rejected alternatives:** A (MOD-0162 Service grubunda kalır — yanlış capability grubu) · B (MOD-0057/0058 kayıtlı bile değil — blocker) · C (çalışan concept foundation'ı taşımak — yüksek regresyon, sıfır kazanç).

**Applied record changes (2026-09-07):** ✅ CAND-CAP-0011 rezerve (registry) · ✅ MOD-0167'den UCLN kaldırıldı (registry + crm-sor-boundary) · ✅ MOD-0162 SoR notu (foundation retained) · ✅ BL-316 → RESOLVED. ⏳ Blueprint 8.2 spreadsheet güncellemesi = EA'nın repo-dışı governance adımı (follow-up).

**Owner/approver:** Enterprise architect (kimlik/DCP-002) + business sponsor (portfolio). **Named individuals + karar tarihi G0'dan ÖNCE atanmalı** (docx §10).

**Affected boundaries:** MOD-0162 (retained concept foundation + SoR genişletme), yeni **CAND-CAP-0011** (Marketing composition/release owner), MOD-0167 (UCLN yanlış-iddia kaldırılır), MOD-0057/0058 (H'de tüketilmez), MOD-0031 (shared evidence korunur).

**Affected contracts:** Henüz donmuş contract yok (G0 öncesi). Contract'lar SCMM-04'te bağlanır.

**Required record changes (yalnız ONAY sonrası):**
1. `execution/registries/module-id-registry.md` — CAND-CAP-0011'i DCP-002 kapısıyla **rezerve et** (Marketing, commercial-suite); MOD-0162 satırına concept-foundation SoR genişletme notu.
2. **MOD-0167'den "UCLN" iddiasını kaldır** (`module-id-registry.md:259` + `crm-sor-boundary.md:21`) — **seçenekten bağımsız, yanlış; bugün de düzeltilebilir.**
3. Blueprint 8.2 — concept model + Marketing composition/release owner yerleşimi.
4. `docs/product-backlog.md` BL-316 → `RESOLVED` (seçilen seçenek + bu brief referansı).
5. DCP-002 çocuk-preflight kaydı CAND-CAP-0011 için.

**Effective version/date:** G0 onayında.

---

## Ek — CAND-CAP-0011 DCP-002 rezervasyon ön-kontrolü (SCMM-01 alt-iş)
- **Neden yeni kimlik:** Blueprint'te SCMM/Content-Studio'yu sahiplenen mevcut modül yok; MOD-0162 (Service grubu) uygun grup değil; MOD-0167 (Segment) yanlış eşleşme.
- **DCP-002 gereği:** temporary candidate (Blueprint match yoksa) → EA rezervasyonu + kalıcı MOD numarası blueprint 8.2'de. `CAND-CAP-0010` emsali (Working Calendar) bu deseni izler.
- **CT notu:** rezervasyon bir **governance yazımı** — CT önerir, EA yazar. Onaysız SCMM-02→04 karar işleri paralel yürüyebilir (kod bloklamaz), ama **SCMM-09+ (build) G0/rezervasyon olmadan dispatch edilmez.**
