# Fresh source → binary → process chain

1. Base source was reconstructed from the declared 341-file snapshot plus integration overlay. Before patching, the two targets matched baseline SHA256 values `b499556f...` and `c66eca35...`.
2. Exact `product.patch` (`0bb36d...`) applied successfully. The target tree then matched `source-final-341.tsv` at 341/341; target hashes are `eaa0aa73...` and `c36c4621...`.
3. Release build completed with 0 warnings and 0 errors. API DLL SHA256 is `daeefa6c9b4b2b7159cabcf397852b23c82c962a3b0e18262b4e8032004549b7`, mtime `2026-09-21T19:57:15+0300`.
4. Main PID `95316` started `2026-09-21 20:04:31 +0300`; restart PID `96056` started `2026-09-21 20:07:51 +0300`. Both therefore started after the built DLL.
5. Main PID served the fresh 52/52 run. It was stopped, port `51863` was confirmed empty, and restart PID served the 4/4 persistent replay/list/outbox/no-stock checks against the same DB.
6. Binary SHA before and after restart is identical. After verification, API `51863` and Mongo `27286` had no listener.

Evidence: `manifests/baseline-target-341.tsv`, `raw/source-target-verify.log`, `raw/fresh-build.log`, `raw/api-binary*`, `raw/api-process-*`, `raw/main-results.json`, `raw/restart-results.json`, and listener records.
