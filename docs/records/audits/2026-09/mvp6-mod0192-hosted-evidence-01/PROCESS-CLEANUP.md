# Process cleanup

- DEV HTTP processes on `127.0.0.1:56392` and `127.0.0.1:56393` were stopped by the probe; the final process transcript records exit codes.
- The dedicated MongoDB process used `127.0.0.1:57392`, replica set `rsmod192host`, and `/private/tmp/mvp6-mod0192-hosted-evidence-mongo-data` with test commands enabled.
- `shutdownServer` closed the Mongo connection as expected; the parent `mongod` process exited `0` and logged `mongod shutdown complete` at `2026-09-23T00:54:29.396+03:00`.
- Operational MongoDB port `27017` and other lane processes were not touched.
