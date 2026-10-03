# Discarded attempts

The first HTTP probe used field names from an earlier informal fixture shape.
The real controller rejected scenario creation with `400 INVALID_REQUEST`.
`api-first-failed.log` is preserved and is not counted as a product failure or
PASS. The probe was corrected to the current domain record names without any
product-source change, the lane-owned database was dropped, and the complete
scenario was rerun from a clean database.

The first post-probe Mongo count used a BSON UUID selector while this source
stores the scoped UUID values as strings. Its zero counts are preserved in
`db-evidence-discarded.json` and are not used. `db-evidence.json` uses the exact
stored tenant/legal-entity string representation and is the controlling query.

