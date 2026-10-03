# MOD-0186 Returns — owner sign-off for the UI pack revision

**STATUS: NOT APPROVED — AND NOTHING TO SIGN YET.** No Returns UI revision patch exists (see `SOP-22-PACK-REVISION.md`).
Do not approve a Returns pack change from this file.

## Decision needed now (dispatch, not sign-off)

> I ask CT to dispatch a MOD-0186 Returns pack-alignment-02 on the pattern of `mvp6-pack-alignment-02-claims`. It must: take the shared pack
> `07a8a0159b5ab02fecc7b1aa6314106d81b1584150169f457cb9a395614a04b7` to the owner-promoted `ready-for-dev` pack `1c80cca7…` plus the
> `mvp6-mod0186-wp-acceptance-01` binding, declare preimage and target SHA-256, prove `git apply --check` and `patch --fuzz=0`, and change
> no business rule. The historical `mvp6-final-pack-delta-01/proposed-pack.patch` (`731622d2…`) is not used as a preimage source.
> After that, a follow-up lane prepares the Returns UI revision from the approved drafts on the new target, with a sign-off that binds both patches in order.

This is a proposal; it becomes an instruction only when the owner or CT states it.

## What stays unauthorized meanwhile

Any Returns pack edit, UI code, gateway/permission/navigation/L10n edits, contract changes, `done` status, commit and push.
