# CRM-BP87-01 — DCP-006 kimlik borcu: MCR taslağı ve eşleme önerisi

> **Durum:** TASLAK / öneri — onay değil (2026-10-19). Master-first kuralı (Blueprint 8.7 `Module_Change_Requests`, CEO 2026-10-16): Master'da olmayan modül oluşturulamaz, kodlanamaz, kayda yazılamaz; yeni CAND-CAP açılmaz. Bu belge MOD numarası, kayıt satırı ya da Master satırı **yazmaz**; MCR'yi Master'a girecek kişi CEO / EA'dır.
> **Kapsam:** DCP-006 (`execution/portfolio/delivery-capability-packs/DCP-006-crm-commercial-delivery-capability-pack.md`) satır 14 ve 24'teki iki CRM yeteneği. DCP-006 bunlara kayda yazmadan "sıradaki boş numara" diye CAND-CAP-0006 / 0007 vermişti; kayıtta 0006 = Görev Merkezi → MOD-0024, 0007 = Enterprise Strategy → MOD-0352 (`execution/registries/module-id-registry.md` satır 249–250).

## 1. Okunan kaynaklar

| Kaynak | Ne okundu |
|---|---|
| `docs/reference/blueprint/System Capability & Implementation Blueprint - master 8.7.xlsx` (sha256 `b71853e3…`) | `Blueprint_Data` Commercial Suite satırları (MOD-0149…0172, 0282–0284, 0363); `Module_Change_Requests` (sütunlar + MCR-001/002); `Main_Reconciliation_v8_2` CAND-CAP satırları |
| `docs/reference/blueprint/Master 8.7 - Change Note.docx` | 8.7 değişiklikleri (Blueprint_Data 8.6c ile aynı) |
| `execution/registries/module-id-registry.md` | CAND-CAP-0006 / 0007 / 0008 / 0010 / 0011, MOD-0362 / 0363 satırları |
| `docs/roadmap/plans/cand-cap-identity-migration-2026-10-16.md` | §3.2 (çakışma + BD "Commercial Activity & Correspondence Register" bağı), §7 |
| `py -3 .antigravity/scripts/verify_module_id.py . --discover "<ad>"` | tam adlar ve kısa terimler (aşağıda) |

**Keşif çıktısı (HEAD 28ea8023e):**
- "Commercial Activity, Interaction and Commitment" → blueprint 0 · sor 0 · registry 0 · paket 0 · gateway 0 · görev 0.
- "Commercial Model and Proposition Routing" → hepsi 0.
- Kısa terimler: "Interaction" görev 1 (MVP-6 kuyruğu, ilgisiz) · "Commitment" 0 · "Proposition" 0 · "Commercial Model" 0 · "Activity" sor 1 / paket 1 (CRM dışı) · "Routing" blueprint 3 (MOD-0161 SLA Routing, MOD-0185 Routing & Load Planning, MOD-0193 BOM & Routings — üçü de ilgisiz).
- **Kısmi inceleme:** keşif paket iş kapsamını okumaz (yalnız ad / kimlik); `Blueprint_Data` SoR sütunu ayrıca elle okundu. Kayıtlı olmayan görev kaynakları ve başka makinedeki işler görülmedi.

## 2. Yetenek A — Commercial Activity, Interaction and Commitment → **MCR (yeni modül)**

**8.7'de uyan modül yok.** Kontrol edilen en yakınlar (`Blueprint_Data` SoR sütunu):

| MOD | Ad | SoR | Neden uymuyor |
|---|---|---|---|
| MOD-0150 | Contact & Relationship Management | contacts, relationship links, *interaction preferences references* | etkileşim tercihinin referansı; etkileşimin kendisi değil |
| MOD-0155 | Field Sales / Visit Planning | visit plans, visit logs, compliance evidence | yalnız saha ziyareti; e-posta / toplantı / taahhüt yok |
| MOD-0153 | Opportunity & Pipeline Management | opportunities, stages, history | fırsat yaşam döngüsü; etkinlik kaydı değil |
| MOD-0160 | Case Management | cases, … customer comms logs | servis vakasına bağlı iletişim; ticari etkinlik değil |
| MOD-0282 | Partner & Alliance Management | CRM (External SoR) + Partner overlay | ortak ilişkisi; genel etkinlik / taahhüt kaydı değil |
| MOD-0024 | Task & Checklist Engine | task templates, instances, assignments | taahhüdün **takip görevi** buraya düşebilir; taahhüt kaydının sahibi olamaz |

**Öneri — onay değil:** tek yetenek, tek MCR. BD analizindeki "Commercial Activity & Correspondence Register" boşluğu ile aynı şeydir (plan §3.2); ikisi ayrı ayrı istenmemeli.

**MCR taslağı (8.7 `Module_Change_Requests` sütunları):**

| Sütun | Değer |
|---|---|
| MCR ID | *(Master'a giren verir; bu taslak numara uydurmaz)* |
| Date Raised | 2026-10-19 |
| Raised By | CRM (CRM-BP87-01) — kullanıcı onayıyla |
| Needed Capability | Commercial activity, interaction and commitment register: a governed record of commercial interactions (call, meeting, e-mail / correspondence, note) and of the commitments made in them (who promised what to whom, by when, status), linked to account, contact, opportunity and visit. |
| Why Needed (feature / dependency / module needing it) | DCP-006 intake row 14 (Activity / Interaction / Commitment) and row 15 (Commercial Activity Timeline, a derived view over this register + MOD-0153 / MOD-0155 events); Partner / BD analysis gap "Commercial Activity & Correspondence Register" (2026-10-16). No module records non-visit commercial interactions or commitments. |
| Existing Modules Checked (closest MOD-IDs) | Checked: MOD-0150 (interaction preferences references only), MOD-0155 (visit logs only), MOD-0153 (opportunities), MOD-0160 (service case comms logs), MOD-0282 (partner overlay), MOD-0024 (follow-up tasks, not the commitment record) — none owns commercial interactions and commitments. |
| Proposed Domain | 4) Enterprise Application Ecosystem |
| Proposed Suite / Capability Group | Commercial Suite (CRM + O2C) / CRM Core |
| Proposed Wave | W-4 (after MOD-0150 / MOD-0153 / MOD-0155, as DCP-006 §4 item 10 sequences it) |
| Proposed SoR Object(s) | commercial activities (interaction records), correspondence references, commitments (owner, counterparty, due date, status), activity–object links; follow-up tasks stay in MOD-0024, documents in MOD-0028, visit logs in MOD-0155 |
| Status / Decision / Decided By / Decision Date / Resulting MOD-ID / Master Version Applied | *(boş — Master kararı)* |

## 3. Yetenek B — Commercial Model and Proposition Routing → **eşleme önerisi (MOD-0152) + MCR olarak eşleme onayı**

**8.7'de bu adla modül yok**, ama iki parçası mevcut modüllere oturuyor:
- **Routing** (talebi doğru ticari modele / teklife yönlendirme kuralı) → **MOD-0152 Lead Management**; 8.7 SoR'u açıkça "*lead routing rules*" içerir.
- **Commercial model** sözlüğü (ör. distribütör / lisans / co-promotion; GMG belge listesinde "Ideal Client Profile Matrix by Primary Commercial Model", "Sales Segmentation and Cooperation Proposition Matrix") → referans veri (MOD-0048 BRD, tüketilir); fırsat üzerinde seçilen model → MOD-0153 alanı.
- Kontrol edilen ve uymayanlar: MOD-0283 Pursuit & Proposal (RFP/RFI teklif çalışma alanı; yönlendirme kuralı değil), MOD-0284 Deal Desk (fiyat / şart istisna onayı), MOD-0167 Segmentation (segment; yönlendirme değil).

**Öneri — onay değil:** yeni modül istenmez. MCR "approved mapping to an existing module" kararı istenir: *Commercial Model and Proposition Routing = MOD-0152 lead routing rules + BRD commercial-model sözlüğü (+ MOD-0153 fırsat alanı)*. Kod yok, acele yok; eşleme kayda ancak Master kararından sonra yazılır.

**Açık nokta (TBD):** DCP-006 intake satırı tek cümle; "proposition routing"in kastedilen anlamı (lead → model yönlendirmesi mi, teklif içeriğinin modele göre seçimi mi) yazılı değil. İkinci anlam çıkarsa en yakın ev MOD-0283 olur. Anlamı kullanıcı / iş sahibi doğrulamalı.

**KARAR (kullanıcı, 2026-10-19):** anlam = gelen talebin doğru ticari modele (distribütör / lisans / co-promotion) yönlendirilmesi → **MOD-0152 Lead Management** (yönlendirme kuralı) + BRD commercial-model sözlüğü + MOD-0153 fırsat alanı. MOD-0283 seçeneği düştü; yeni modül yok. Master'daki eşleme satırını (MCR "approved mapping") CEO / EA girer; `module-id-registry.md`'ye satır Master kararından sonra yazılır (Master-first). Yetenek A (MCR yeni modül) hâlâ CEO / EA kararında.

**MCR taslağı (eşleme):**

| Sütun | Değer |
|---|---|
| Needed Capability | Commercial model and proposition routing: classify a demand by commercial model / proposition and route it to the owning lane. |
| Why Needed | DCP-006 intake row 24; no Blueprint identity; the earlier CAND-CAP-0007 number collides with MOD-0352. |
| Existing Modules Checked | MOD-0152 (lead routing rules — fits the routing part), MOD-0153 (opportunity attribute), MOD-0283 (proposal workspace), MOD-0284 (deal exceptions), MOD-0167 (segments), MOD-0048 (reference data). |
| Proposed Domain | 4) Enterprise Application Ecosystem |
| Proposed Suite / Capability Group | Commercial Suite (CRM + O2C) / Sales |
| Proposed Wave | W-3 (MOD-0152 wave) |
| Proposed SoR Object(s) | none new: routing rules in MOD-0152; commercial-model vocabulary as a BRD set; selected model on the MOD-0153 opportunity |
| Requested decision | Approved mapping → MOD-0152 (no new MOD-ID) |

## 4. Açık onay / sahiplik kararları

1. **A:** MCR'nin Master'a girmesi ve yeni MOD numarası — CEO / EA.
2. **B:** MOD-0152 eşlemesinin onayı — CEO / EA; "proposition routing" anlamının doğrulanması — kullanıcı / iş sahibi.
3. Karar sonrası kayıt satırı (`module-id-registry.md`) ve DCP-006 güncellemesi — ayrı iş paketi (kayıt / Kapanış hattı).
