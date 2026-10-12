# SANDOP-CAPACITY proposed final release decisions — UNAPPROVED

The following are separate copyable decisions. None is recorded as granted. The 2026-09-22 owner messages authorize candidate preparation and the exact MOD-0192 executor design; the release review recommends 2.0.0 but explicitly says that no final version or consumer inventory was selected. These blocks therefore must be decided against the exact bytes below.

## A. Contract owner: final version and known-consumer inventory

> I select SANDOP-CAPACITY `info.version: 2.0.0`, `x-status: FROZEN`, wire `contractVersion: v1`, and annex `sandop-capacity-semantics-v2.0.0.md` as the proposed final release metadata. I accept the breaking-change classification and the same-route compatibility risk described in `mvp6-sandop-capacity-r2-release-review-01/SOP-22.md`. The proposed final YAML SHA-256 is `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3`; annex SHA-256 is `e2599fd9b8cc7cf7e4a39b4d5d1bccb3fa985c156fec6c6082f5227334770442`. My known repository consumer inventory is MOD-0190, MOD-0192 and their script/mock/test tooling; the repository search found no SANDOP-specific running controller, gateway route or generated client. This is a known-repository inventory, not proof that no external consumer exists. Any discovered external/running consumer needs its own migration disposition before same-route cutover. This decision alone does not publish the contract or grant consumer consent.

## B. MOD-0190 consumer release consent

> As MOD-0190 consumer owner, I have reviewed SANDOP-CAPACITY proposed final YAML SHA-256 `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3`, annex SHA-256 `e2599fd9b8cc7cf7e4a39b4d5d1bccb3fa985c156fec6c6082f5227334770442`, and publication patch SHA-256 `0ebc6a6fa6a867170b1d32545295e88676d74fb47794879a671c9f6c0375bd04`. I consent to the MOD-0190 six-operation status/error, lifecycle, receipt, correlation and fixture behavior at these exact hashes and will repin its pack/test/mock expectations before runtime dispatch. I understand the same-route wire-v1 cutover and that this consent is not runtime uptake or pack promotion.

## C. MOD-0192 consumer release consent

> As MOD-0192 consumer owner, I have reviewed SANDOP-CAPACITY proposed final YAML SHA-256 `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3`, annex SHA-256 `e2599fd9b8cc7cf7e4a39b4d5d1bccb3fa985c156fec6c6082f5227334770442`, and publication patch SHA-256 `0ebc6a6fa6a867170b1d32545295e88676d74fb47794879a671c9f6c0375bd04`. I consent to the MOD-0192 six-operation fixture, evaluation, error/replay and exact executor-annex behavior at these hashes and will repin its pack/test/mock expectations before runtime dispatch. I understand that the same-route wire-v1 cutover, Pending-only outbox and test-only DEMAND/constraint limits remain, and that this consent is not runtime uptake or pack promotion.

## D. Separate contract publication authority

> After decisions A–C and any newly discovered consumer disposition are recorded, I authorize the single contract publication owner to apply `publication-proposed.patch` SHA-256 `0ebc6a6fa6a867170b1d32545295e88676d74fb47794879a671c9f6c0375bd04` to canonical SANDOP-CAPACITY baseline SHA-256 `c255e92923ba91714cec8daf229262101b5d45648b7f714a03ab402811db683c`, producing canonical YAML SHA-256 `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3` and annex SHA-256 `e2599fd9b8cc7cf7e4a39b4d5d1bccb3fa985c156fec6c6082f5227334770442`. The owner must recheck the baseline, patch and output hashes and exact two-file scope before applying. This is publication authority only; it does not authorize runtime, Program.cs, pack promotion, migration, rollout, commit or push.

Changing any of the three output hashes or the consumer inventory requires a new disposition. Draft text is not consent.
