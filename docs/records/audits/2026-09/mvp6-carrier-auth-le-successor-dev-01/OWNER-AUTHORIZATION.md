# Owner authorization binding

The user message that dispatched `MVP6-CARRIER-AUTH-LE-SUCCESSOR-DEV-01` is the controlling implementation authority.

It explicitly authorizes isolated implementation and verification of:

- successor patch `e6b2a3d335ee8f76270fe6ace09793a08d69e57174874be2b2db99941b01f673`;
- source manifest `275f204c29113b2bb80b9aa10c87e08b81bd2ab2c71c59a92a98d18130ccb93d`;
- source archive `1c497022407788a5b38f5c554c867d446cc08541cf00c0d3390704c466301846`;
- the exact 22 Auth, Platform and MDM paths in the source manifest;
- isolated login, refresh, MFA, forced-password, zero/multiple/inactive/revoked-scope and dependency-failure verification.

The authority does not extend the earlier 10-path decision by inference. It is a new, exact hash-bound authorization supplied in the dispatch message. It excludes global internal bypass changes, broad MDM permissions, Carrier UI changes, Supplier concurrence, gateway/guard changes, rollout and Git mutation.

