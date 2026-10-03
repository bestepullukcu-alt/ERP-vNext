# Attempt disposition

- Sandboxed VSTest loopback attempts could not open the runner socket and are retained as environmental failures. Escalated isolated reruns are the controlling TRX results.
- The first restart probe used the wrong lane-owned test database and returned 404. It is a harness configuration failure, not a product result. The same binary was relaunched against the create database and the restart probe passed.
- The fresh create recorder's informational binary field contains a literal shell expression. The payload, HTTP, and persistence capture remains valid; binary provenance is controlled by `SOURCE-BINARY-PROCESS.tsv` and the correct restart evidence.
- Platform module registration at the deliberately unused port 59999 failed after bounded retries. This is expected in the isolated direct-service scope and does not affect Shipment HTTP acceptance.
