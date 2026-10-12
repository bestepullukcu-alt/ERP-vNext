---
decision_id: MVP6-SHIPMENT-DN02-OWNER-DECISION-01
status: approved (option A, refreshed text)
decided_at_local: 2026-09-26, ~14:36–14:48 +03:00 (approximate window given by CT; the minute of each single answer was not given)
decided_by: Natig Yusubov, Chief Executive Officer (repository owner)
decision_source: owner answer in the MVP6 Control Tower conversation (in-app question tool), 2026-09-26
approvedBy: current-role-user-message-2026-09-26
bound_to: docs/roadmap/plans/mvp6-decision-prep-02/DN-02.md sha256 709b14af3854c75d2df75b67e089f6098914e522cfca0da395416ee43768dbe0; docs/roadmap/plans/mvp6-decision-prep-02/SHA256SUMS sha256 7a22f9e414f482d7c46e45c5f936d50c1ba127ab9fe33a3baf9c7c32175e235d
recorded_by: AL-MVP6-REC-PACK-01 (Q71, chat lane) at 2026-09-26T14:45:53+0300 on CT instruction; CT writes no files
---

# MOD-0183 Shipment UI — DN-02 bounded DataTable verification profile (UI183-A14) — option A

## Decision

Owner answer to DN-02 (queue Q11, blocker B04): **A — approve the refreshed text below** (the text that names SCR-07, SCR-14, SCR-22 and SCR-34; not the original `DECISION-NEEDS.md` DN-02 text).

## Exact decision text (source `docs/roadmap/plans/mvp6-decision-prep-02/DN-02.md` sha256 `709b14af3854c75d2df75b67e089f6098914e522cfca0da395416ee43768dbe0`)

> MOD-0183 Shipment UI için aynı-origin MVC, read/create/detail/transition/POD bounded DataTable doğrulama profilini onaylıyorum. Bu profile Edit, delete, bulk delete, QuickView, direct-Gateway, generic Active/Passive ve onaylanmamış import/export/save-view/column-visibility beklentileri pozitif gereksinim olarak dahil değildir; ayrıca şu dört generic kontrol de açıkça kapsam dışıdır ve yalnız yokluk (absence) olarak doğrulanır: SCR-07 generic `Unknown` status anahtarı, SCR-14 `AreYouSure`, SCR-22 `ShowAll` toolbar aracı ve SCR-34 `reloadWithToast` delete yaşam döngüsü. Toplam 23 OUT satırının her biri final kaynakta yokluk assertion'ı olarak kalır. Profile, UI183-A14'teki DataTables v2, `window.DtDefaults`, `stateSave:false`, server paging, responsive control/action access, exact bounded localization ve unsupported-action absence hükümlerini fail-closed doğrular. Generic `verify_datatable_page.py` (sha256 `00148e13a259623ffa2df2ba1290cf327af4b8065c9b5c14c09a79e06ba2748c`) sonucu 49 PASS / 35 FAIL tarihsel ham kanıt olarak korunur (SCR-00: acceptance girdisi değildir); bounded profile sonucu generic gate waiver'ı, repo-geneli PASS veya ürün davranışı ekleme yetkisi sayılmaz. PC-02, PC-03, PC-04 ve PC-28 ayrı kararlarla belirlenir; dördü ve sekiz evidence-gap satırının taze kanıtı tamamlanmadan UI183-A14 PASS olamaz. Bu karar `.antigravity` kuralı/verifier değişikliği yetkisi vermez.

## Consequences stated in the source

- The profile is built as a lane-local checker inside a VER evidence folder; no `.antigravity` change.
- UI183-A14 closes only after DN-02, PC-02, PC-03, PC-04, PC-28 (recorded in `mvp6-shipment-pc02-pc03-pc04-pc28-owner-decision-01.md`) and fresh evidence for the eight evidence-gap rows (SCR-08, 10, 15, 16, 19, 20, 21, 25), run on the local Mac.

The quoted text is copied byte-for-byte from the source file named above it (including its `>` quote markers); the source file itself still carries its "NOT APPROVED — prepared text only" heading, which this record supersedes for the option chosen. Source files are not edited (K4).
