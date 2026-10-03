# Attempt disposition

- The initial four builds referenced a nonexistent repository-root `NuGet.Config`. Those exit-1 logs are setup attempts and are not product failures. The controlling cache-based builds passed.
- The first sandboxed frontend test run hit an MSBuild named-pipe `SocketException (Permission denied)` and was stopped. The independent rerun in the approved local test context passed 14/14.
- A preliminary route invocation ran before the test project had been built and emitted no TRX. The controlling built invocation and recorded rerun passed 24/24.
- CUA attempted the exact Web URL and received `net::ERR_CONNECTION_REFUSED` because browser automation could not enter the approval-isolated localhost process namespace. No screenshot or browser PASS is claimed.
- The Carrier Auth handoff intentionally contains no reusable bearer/session/secret and its prior runtime fixture was cleaned. No token was reconstructed or fabricated.
- SupplyChain module-registration warnings were caused by the deliberately absent Platform process at 5857. They did not prevent Shipment API startup or health; module registration acceptance is outside this VER.

