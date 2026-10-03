# R01–R30 evidence classification

|R|Executable evidence|Remaining status|
|---|---|---|
|R01|none for this decision; existing check is declaration/schema/hash/process|STATIC_DECLARATION + UNVERIFIED|
|R02|none for this decision; existing check is declaration/schema/hash/process|STATIC_DECLARATION + UNVERIFIED|
|R03|none for this decision; existing check is declaration/schema/hash/process|STATIC_DECLARATION + UNVERIFIED|
|R04|none for this decision; existing check is declaration/schema/hash/process|STATIC_DECLARATION + UNVERIFIED|
|R05|none for this decision; existing check is declaration/schema/hash/process|STATIC_DECLARATION + UNVERIFIED|
|R06|Approved boundaries/retention and non-Approved amount rejection in adapter; BSON not tested|EXECUTABLE_PARTIAL + STATIC + UNVERIFIED|
|R07|duplicate-key-independent creates only; notes/finance static|EXECUTABLE_PARTIAL + STATIC + UNVERIFIED|
|R08|none for this decision; existing check is declaration/schema/hash/process|STATIC_DECLARATION + UNVERIFIED|
|R09|none for this decision; existing check is declaration/schema/hash/process|STATIC_DECLARATION + UNVERIFIED|
|R10|New integrated lexical amount/time and nullable/evidence-default fingerprint tests; full schema/transport capacity remains UNVERIFIED|EXECUTABLE_PARTIAL + DECLARATION + UNVERIFIED|
|R11|Withdrawn create403/investigate200|EXECUTABLE_PARTIAL + STATIC + UNVERIFIED|
|R12|Closed grant and invalid Open target|EXECUTABLE_PARTIAL + STATIC + UNVERIFIED|
|R13|none for this decision; existing check is declaration/schema/hash/process|STATIC_DECLARATION + UNVERIFIED|
|R14|none for this decision; existing check is declaration/schema/hash/process|STATIC_DECLARATION + UNVERIFIED|
|R15|EXECUTABLE scoped auth-before-receipt/root-before-payload in new Store; HTTP/JWT/header trace remains UNVERIFIED|EXECUTABLE_PARTIAL + DECLARATION + UNVERIFIED|
|R16|EXECUTABLE integrated changed valid payload409, wrong root precedence, original replay; no runtime claim|EXECUTABLE_PARTIAL + DECLARATION + UNVERIFIED|
|R17|49 lifecycle code and payload helper code|EXECUTABLE_PARTIAL + STATIC + UNVERIFIED|
|R18|none for this decision; existing check is declaration/schema/hash/process|STATIC_DECLARATION + UNVERIFIED|
|R19|EXECUTABLE tenant+LE+operation+target+key separation; no Mongo unique index proof|EXECUTABLE_PARTIAL + DECLARATION + UNVERIFIED|
|R20|EXECUTABLE current grant, actor excluded from receipt identity, original-result replay and no extra writes; deletion/TTL and HTTP unverified|EXECUTABLE_PARTIAL + DECLARATION + UNVERIFIED|
|R21|10 helper root fixtures; missing/null both None; no producer uptake|EXECUTABLE_PARTIAL + STATIC + UNVERIFIED|
|R22|none for this decision; existing check is declaration/schema/hash/process|STATIC_DECLARATION + UNVERIFIED|
|R23|different-key duplicate in-memory accepted|EXECUTABLE_PARTIAL + STATIC + UNVERIFIED|
|R24|none for this decision; existing check is declaration/schema/hash/process|STATIC_DECLARATION + UNVERIFIED|
|R25|EXECUTABLE four reached write stages with distinct prefixes/counts and full state rollback; committed unknown/lost response samekey recovery; Mongo/CAS/restart UNVERIFIED|EXECUTABLE_PARTIAL + DECLARATION + UNVERIFIED|
|R26|none for this decision; existing check is declaration/schema/hash/process|STATIC_DECLARATION + UNVERIFIED|
|R27|none for this decision; existing check is declaration/schema/hash/process|STATIC_DECLARATION + UNVERIFIED|
|R28|none for this decision; existing check is declaration/schema/hash/process|STATIC_DECLARATION + UNVERIFIED|
|R29|none for this decision; existing check is declaration/schema/hash/process|STATIC_DECLARATION + UNVERIFIED|
|R30|none for this decision; existing check is declaration/schema/hash/process|STATIC_DECLARATION + UNVERIFIED|

All rows retain static normative declarations. Schema/hash checks are executable tooling but are not behavioral evidence for the business decision. No row establishes runtime acceptance.

Rework classification: only R10/15/16/19/20/25 gain the named new model evidence. All other rows preserve previous partial/declaration/unverified limits. Historical executable rows were not freshly rerun in this narrow rework. New stateful_model takes already validated command bodies and resolved authoritative root; it is not an amount/reference/schema/JWT/lifecycle-complete implementation. Transaction stages are in-memory dictionaries/lists, not MongoDB.
