/*
 * WP-LANE-GUARDS-02 — one place for "this row is another team's, it came with main": the owner each path belongs to,
 * and the reason such a row may carry (one per main-sync). The guards that name main-sync exceptions (field icons, the shared
 * confirm's input box, the opt-in select) read it here, so an owner is never spelled two ways.
 *
 * The rule each of those guards holds (XMC-001: another team's view or script is not ours to change):
 *   - a row names the OWNER of its path (`SupplyChain/` ↔ SCE, `Procurement/` ↔ Procurement, `CRM/` ↔ CRM) and the
 *     main-sync reason, or it is red;
 *   - the list of rows is pinned by its exact keys, so a new row is red unless the pin changes in the same diff;
 *   - a row its owner fixed is red until it leaves the list (the list only shrinks).
 */
const SCE = "SCE / MVP-6 (gmg-10)";
const PROCUREMENT = "Satın Alma / Procurement (Cihan)";
const CRM = "CRM";
// WP-LANE-MAIN-SYNC-02 (main-sync 2026-10-14, origin/main 2af4e55a9) — the teams whose screens came with the second sync.
const STOCK = "Stok / MVP-1 (MOD-0173…0175)";
const MANUFACTURING = "Üretim / MVP-3 (MOD-0193)";
const ESIGN = "E-imza / MOD-0022";
const OWNER_BY_PATH = [["SupplyChain/", SCE], ["Procurement/", PROCUREMENT], ["CRM/", CRM],
  ["Inventory/", STOCK], ["Quarantine/", STOCK], ["Traceability/", STOCK], ["Manufacturing/", MANUFACTURING],
  ["ESignature/", ESIGN]];
const MAIN_SYNC_WHY = "main-sync (2026-10-12) ile geldi; sahibi başka ekip; düzeltme sahibin işi";
const MAIN_SYNC_2_WHY = "main-sync (2026-10-14) ile geldi; sahibi başka ekip; düzeltme sahibin işi";
const MAIN_SYNC_REASONS = [MAIN_SYNC_WHY, MAIN_SYNC_2_WHY];

/** The owner a path belongs to, or undefined when it is none of the other teams'. */
const ownerOf = (key) => (OWNER_BY_PATH.find(([segment]) => key.includes(segment)) || [])[1];

/** The rows that do not name their path's owner, or that carry another reason than the main-sync one. */
const misnamedRows = (rows) => Object.entries(rows)
  .filter(([key, row]) => !ownerOf(key) || !row || row.owner !== ownerOf(key) || !MAIN_SYNC_REASONS.includes(row.why))
  .map(([key]) => key);

module.exports = { SCE, PROCUREMENT, CRM, STOCK, MANUFACTURING, ESIGN, OWNER_BY_PATH, MAIN_SYNC_WHY, MAIN_SYNC_2_WHY, ownerOf, misnamedRows };
