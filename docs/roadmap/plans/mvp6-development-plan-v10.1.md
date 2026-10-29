# MVP6 development plan v10.1 — 2026-10-26

Supersedes v10.0 (2026-10-21/22) for the open work only; v10.0's history and its §3a / §4 / §5 stay valid where not
repeated here. Measured on `origin/main` `04c8fb4cf` (after #233). Accounts: **Ali = `GMG-ERP`**, **Beste =
`Bestepul` / `bestepullukcu-alt`**, **`gmg-10`** = the SCE / Control Tower author account.

## 1. What changed since v10.0

- **Merged to `main`:** #195 (WP-P1 decision draft), #196 (H2 real-identity tests), #197 (plan §4a), **#198** (central
  audit E-2 + MDM through the Gateway E-5 + FIX1) — the MOD-0147/0148 "audit path b" gate is closed; #206 (central outbox
  race).
- **A4 done:** `mvp6-g7-grant-removal-01/REPORT.md` already names Ali as `GMG-ERP` on `main`.
- **DC-02 D2 decided (2026-10-26):** Cihan answered Q3/Q4, Natig approved; the mapping is in both packs
  (`docs/roadmap/plans/mvp6-mod0147-v1-scorecard-01/DC02-D2-ELIGIBILITY-MAPPING-PROPOSAL.md`). D2 closes formally when
  Cihan's MOD-0140 Revision 2 is approved.
- **MOD-0140 Revision 2** (Cihan, `origin/feature/mvp2/supplier-tenant-01` @ `7f937c812`): `supplier.openapi.yaml` 1.3.0
  contract-first (DL-1), `SupplierEligibility`, `procurement.suppliers.eligibility.read`, service token in DL-7 (1.4.0).
  Not reviewed or approved yet; `main` still carries SUPPLIER 1.2.0.
- **MOD-0147 / MOD-0148 v1 packs:** reviewed to UYGUN (CT-APR-02, nine rounds on MOD-0148, seven on MOD-0147). Approval
  PR **#236** (`docs/sce/mod0147-0148-v1-approval-01`, packs `6842e218…` / `55d248f6…`), rebuilt on `main` so
  `reviews.tsv` stays append-only. The older branch `docs/sce/mod0147-v1-scorecard-01` is superseded (local only).
- **DC-03 canonical-code request** sent to Beste by Natig, 2026-10-26 — acceptance open.

## 2. Sequence (open work, in order)

**M — MOD-0147 / MOD-0148 (critical path)**

| # | step | owner | state |
|---|---|---|---|
| M1 | #236: CT-APR-01 rows for both versions (HIGH), merged by another person with a merge commit; no push to the PR while it is under review | **any of K2 / K3 / K4** (D5); Natig may merge | open |
| M2 | DC-03: accept the canonical-code requirement | **Beste** | sent 2026-10-26 |
| M3 | MOD-0140 Revision 2: CT-APR-02 review | **MVP-6 CT**, separate session (D3) | to start |
| M4 | MOD-0140 Revision 2: CT-APR-01 row | **Natig (K1)** (D3); another person merges | after M3 |
| M5 | MOD-0140 DL-1: `supplier.openapi.yaml` 1.3.0 FROZEN on `main` | **Cihan** | after M4 |
| M6 | Permission / composition single writer for `supplychain.supplier-performance.*` and `supplychain.supplier-portal.*` | **MVP-6 CT** (D2) | decided 2026-10-26; written with DL-1 |
| M7 | MOD-0140 S-2, MVP-6 side: evaluator role gets `mdm.legal-entities.read` (Roles → Permissions) | Natig (D1) | decided 2026-10-26; Cihan informed |
| M8 | Replay receipt amendment (Phase 1.5) | CT | open |
| M9 | **MOD-0147 v1 code:** DL-1 (create, four GETs, eligibility read against the 1.3.0 mock), then DL-2 (submit / publish). Live link after MOD-0140 DL-3 merges | CT | after M1, M5–M8 |
| M10 | MOD-0148 extra gates: DC-03 binding pack + build (Beste); MOD-0140 DL-7 service token (1.4.0) with BL-652 Option 2 and the `service_name` registration (Auth owner) | Beste, Auth owner | after M2 |
| M11 | **MOD-0148 v1 code:** DL-1, DL-2; internal only; release gate (paged-list successor, `TrustedProxies`, rate limit, shared limiter) | CT | after M9, M10 |
| M12 | MOD-0147 v1.1 (computed metrics): read contracts MOD-0141/0142/0143 + QC source | Procurement owner | request **not sent yet** (`READ-CONTRACT-REQUEST-PROCUREMENT.md`, Natig) |

Open low findings carried to the next pack revision (not pushed while #236 is under review): MOD-0147 round 7 F-1
(contract `supplierId` examples and the 422 wording), MOD-0148 round 9 F-1 (closed in fact by sending the DC-03 request;
closure note in the next revision).

**I — Integration (unchanged from v10.0 §4a)**

| # | step | owner | state |
|---|---|---|---|
| I3 | WP-P1: G2 reservation, G3 REVERSAL source, G4 two-phase multi-line | Natig (D4) | decided 2026-10-26; next: update the §8 pack patches, then CT-APR-02 → CT-APR-01 |
| I4 | Wave 2 code DL-1 … DL-6 | CT | after I3 approval |

**A / B — carried**

- **A1** Q358 production run: Ali, after Natig says production authorizations are updated — not yet (2026-10-26).
- **B1–B5** main reds stay with their owners (#174, #175, #180, MOD-0022, BL-615); not re-measured for this revision.
  `phase1-gates` is red on every `main` push since at least #219.

**Owner decisions 2026-10-26** (`docs/records/decisions/2026-10/mvp6-owner-decisions-2026-10-26.md`): D1 S-2 role
permission, D2 CT writes the permission keys, D3 MVP-6 CT reviews MOD-0140 and Natig approves it, D4 WP-P1 G2–G4, D5 any
available non-author approver on #236 / #237. The messages in §4 below are now **for information**, not questions.

## 3. What Natig and the CT can do without waiting

1. Natig sends `READ-CONTRACT-REQUEST-PROCUREMENT.md` (M12) and the information messages in §4.
2. CT runs the MOD-0140 Revision 2 CT-APR-02 review in a separate session (M3).
3. CT updates the WP-P1 §8 pack patches for G2–G4 (I3).
4. CT watches #236 / #237 CI and prepares the next MOD-0147/0148 revision locally (two low findings), without pushing to
   #236 while it is under review.

## 4. Messages (information — Natig sends)

1. **MVP-6 CT → MVP-1 CT:** "Bilgi: (1) WP-P1 G2–G4 kararlaştırıldı — G2 ilk dilim rezervasyonsuz (reservationId sonra,
   DL-6), G3 REVERSAL `MOD-0173`/`REVERSAL` ile, G4 satır başına anahtar + geri alma; Inventory kuralı ve sözleşmesi
   değişmiyor. (2) Cihan'ın MOD-0140 Revizyon 2 incelemesini MVP-6 CT yapıyor, onaylayıcı Natig. (3) #237 (MOD-0176/0177
   nav anahtarları, phase1'i yeşile çevirir) açık; paketler sizin, bilginize."
2. **MVP-6 CT → Cihan:** "Bilgi: MOD-0147 v1'de değerlendirici rolü `mdm.legal-entities.read`'i Roles → Permissions'tan
   alacak (BL-652 ara yolu); MOD-0140 S-2'nin kendi cevabı senin. MOD-0140 Revizyon 2'nin CT-APR-02 incelemesini MVP-6 CT
   yapıyor, CT-APR-01 onaylayıcısı Natig. Onaydan sonra 1.3.0'ı yazıp dondurman yeter."
3. **MVP-6 CT → Beste / Ali / Cihan:** "#236 (MOD-0147/0148 onay satırları) ve #237 (nav anahtarları) için yazar olmayan,
   müsait olan biri onaylasın; merge'ü Natig yapabilir."
