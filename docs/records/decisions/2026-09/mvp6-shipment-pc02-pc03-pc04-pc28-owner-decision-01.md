---
decision_id: MVP6-SHIPMENT-PC02-PC03-PC04-PC28-OWNER-DECISION-01
status: approved (option A for each of PC-02, PC-03, PC-04, PC-28)
decided_at_local: 2026-09-26, ~14:36–14:48 +03:00 (approximate window given by CT; the minute of each single answer was not given)
decided_by: Natig Yusubov, Chief Executive Officer (repository owner)
decision_source: owner answer in the MVP6 Control Tower conversation (in-app question tool), 2026-09-26
approvedBy: current-role-user-message-2026-09-26
bound_to: docs/roadmap/plans/mvp6-decision-prep-02/PC-02.md sha256 8eabc0f2ed36f965444617b4f9404fdede51448bdfea0bf9c22f5d8c504f65bf; docs/roadmap/plans/mvp6-decision-prep-02/PC-03.md sha256 8ea4b01402314c3eccb8e8dbeb75eb8741cc9d0426b7ccf208884662894c2552; docs/roadmap/plans/mvp6-decision-prep-02/PC-04.md sha256 90cbc5ca9d310cc09434569ab1c793f2b5ed6c57e18067f579fd785b3c46d391; docs/roadmap/plans/mvp6-decision-prep-02/PC-28.md sha256 7fb65566e3c64b9fe0b2b7e2e9ef86b45b6549613cd63e199d8b9586db142322; docs/roadmap/plans/mvp6-decision-prep-02/SHA256SUMS sha256 7a22f9e414f482d7c46e45c5f936d50c1ba127ab9fe33a3baf9c7c32175e235d
recorded_by: AL-MVP6-REC-PACK-01 (Q71, chat lane) at 2026-09-26T14:45:53+0300 on CT instruction; CT writes no files
---

# MOD-0183 Shipment UI — policy conflicts PC-02, PC-03, PC-04, PC-28 — option A ×4

Owner answers (queue Q12): **PC-02 A, PC-03 A, PC-04 A, PC-28 A.** Four separate decisions, recorded together; each exact text is quoted below. All four apply only inside the DN-02 bounded profile (`mvp6-shipment-dn02-owner-decision-01.md`).

## PC-02 — option A

Source `docs/roadmap/plans/mvp6-decision-prep-02/PC-02.md` sha256 `8eabc0f2ed36f965444617b4f9404fdede51448bdfea0bf9c22f5d8c504f65bf`.

> PC-02 için SCOPE-AWARE-VERIFIER-PROPOSAL.md'deki önerilen hükmü, yalnız MOD-0183 Shipment bounded verifier profili kapsamında onaylıyorum: `.antigravity/scripts/verify_datatable_page.py:484-491` literal `<partial name="_Filter" />` kontrolü bu profilde, exact `_Filter.cshtml`'in derlenmesi, render edilmesi, yalnız onaylı kontrolleri göstermesi, label bağlaması ve yalnız frozen sorguyu üretmesi kontrolleriyle değiştirilir. Generic sonuç (FAIL 02) tarihsel olarak korunur. Bu karar `.antigravity` kural/verifier değişikliği, waiver, repo-geneli PASS veya ürün kaynak değişikliği yetkisi vermez.

## PC-03 — option A

Source `docs/roadmap/plans/mvp6-decision-prep-02/PC-03.md` sha256 `8ea4b01402314c3eccb8e8dbeb75eb8741cc9d0426b7ccf208884662894c2552`.

> PC-03 için SCOPE-AWARE-VERIFIER-PROPOSAL.md'deki önerilen hükmü, yalnız MOD-0183 Shipment bounded verifier profili kapsamında onaylıyorum: `frontend-datatable-template.md:20` ve `verify_datatable_page.py:118-168` Compact `_Form`/`Details` section eşitliği bu profilde uygulanmaz; bunun yerine UI183-A05 (13 create input'unun her biri erişilebilir bir create section'ında) ve UI183-A06 (gerekli her detail bilgisi bir detail section'ında; detail-only read model boş veya düzenlenebilir form section'ı üretmez) ayrı ayrı doğrulanır. Bu, PH15-UI-183 bulgusu F-183-2'yi Shipment UI için bu kapsamda karara bağlar. Generic sonuç (FAIL 03) tarihsel olarak korunur. Bu karar `.antigravity` kural/verifier değişikliği, add-module kuralında genel istisna, waiver, repo-geneli PASS veya ürün kaynak değişikliği yetkisi vermez.

## PC-04 — option A

Source `docs/roadmap/plans/mvp6-decision-prep-02/PC-04.md` sha256 `90cbc5ca9d310cc09434569ab1c793f2b5ed6c57e18067f579fd785b3c46d391`.

> PC-04 için SCOPE-AWARE-VERIFIER-PROPOSAL.md'deki önerilen hükmü, yalnız MOD-0183 Shipment bounded verifier profili kapsamında onaylıyorum: `verify_datatable_page.py:792-800` literal `<partial name="_IndexL10n" />` kontrolü bu profilde, exact `_IndexL10n.cshtml`'in derlenmesi, geçerli JSON üretmesi, `index.l10n.js`'in bunu merge etmesi, yedi kültürün doğru render edilmesi ve missing-key/console kontrollerinin geçmesi ile değiştirilir. Generic sonuç (FAIL 04) tarihsel olarak korunur; sekiz evidence-gap lokalizasyon satırı ayrıca taze kanıt gerektirir. Bu karar `.antigravity` kural/verifier değişikliği, waiver, repo-geneli PASS veya ürün kaynak değişikliği yetkisi vermez.

## PC-28 — option A

Source `docs/roadmap/plans/mvp6-decision-prep-02/PC-28.md` sha256 `7fb65566e3c64b9fe0b2b7e2e9ef86b45b6549613cd63e199d8b9586db142322`.

> PC-28 için MOD-0183 Shipment DataTable sayfasının `proxy-profile` (same-origin MVC) olarak doğrulanmasını onaylıyorum: `.antigravity/workflows/quality-gate-datatable.md` satır 31–32, 142 ve 151 uyarınca statik guard `--api-profile proxy` ile çalıştırılır; browser trafiği yalnız same-origin `/SupplyChain/Shipments/api` olmalı, MVC controller'ın yapılandırılmış `GatewayUrl` (Gateway) hedefi kod/log ile doğrulanmalı ve browser'dan Gateway/servis portuna giden her istek profili FAIL etmelidir. UI183-A01 değişmez. Varsayılan `direct-gateway` ile alınan tarihsel generic sonuç (49 PASS / 35 FAIL, FAIL 28 dahil) korunur; `--api-profile proxy` sonucu (2026-09-26 yeniden çalıştırma: 51 PASS / 34 FAIL) onun yanında kaydedilir. Bu karar `.antigravity` kural/verifier değişikliği, waiver, repo-geneli PASS, CORS veya transport değişikliği yetkisi vermez.

## Scope note (from the sources)

None of the four grants an `.antigravity` rule/verifier change, a waiver, a repo-wide PASS or a product source change. The generic results (FAIL 02, 03, 04, 28) stay as history. The rendered/network proof comes from the A14 VER on the local Mac.

The quoted text is copied byte-for-byte from the source file named above it (including its `>` quote markers); the source file itself still carries its "NOT APPROVED — prepared text only" heading, which this record supersedes for the option chosen. Source files are not edited (K4).
