# Attempt disposition

- The initial broad restore and first Auth build became unresponsive and were stopped. The bounded rebuilds used disabled build servers and completed successfully.
- Platform initially lacked a cached PPM contracts assets file. An offline restore from the existing local cache completed, then Platform built successfully.
- The first SupplyChain launch honored its packaged `Urls` value and listened on 5061. It was stopped and relaunched with the explicit `--urls http://127.0.0.1:5661` argument.
- Sandbox Mongo bind attempts returned `EPERM`. The same isolated localhost command was run through the approved elevated execution path with `--nounixsocket`.
- Tenant-97 initially lacked the Platform tenant record needed by the accepted login-settings path. The isolated test DB received an exact clone of the accepted tenant-1 document schema with only tenant identity/display substitutions. Real Auth login then returned 200. This is test setup, not a product-source change.
- Setting a serialized past `EffectiveTo` directly in Mongo was rejected because the existing compound multikey indexes would create parallel arrays. Revocation therefore used the canonical soft-delete flag `IsDeleted=true`, then restored it to false.
- Two proposed techniques were rejected by automatic execution review: persisting an HttpOnly browser cookie for replay and persisting a raw direct-login response containing tokens. They were not retried. All later tokens remained only in process memory.
- Browser screenshot bytes had no supported durable save/export operation. The PNG row stays OPEN; no alternate extraction or security bypass was used.
- No new product-source defect was observed in this bounded lane. The Gateway's 400 response to contradictory tenant signals is recorded exactly; resource-scope isolation was independently demonstrated by separate real tenant sessions.

