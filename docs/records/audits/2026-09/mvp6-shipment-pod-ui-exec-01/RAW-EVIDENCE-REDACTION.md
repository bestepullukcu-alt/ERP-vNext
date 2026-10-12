# Raw evidence redaction

The `raw/` logs contain compiler and test-runner output only. They were scanned for bearer tokens, JWT-shaped values, passwords, connection strings and MongoDB URIs; none were present. Absolute paths and test names remain because they bind the evidence to the authorized isolated checkout. No request payload, credential, database content or owner-private value is included.

The raw evidence is intentionally limited to the decisive checks: 14 focused UI tests, the Shipment/Carrier port assertion, and the SupplyChain compile failure. Runtime HTTP, Mongo and browser logs do not exist because the fail-closed source/topology gates prevented process startup.
