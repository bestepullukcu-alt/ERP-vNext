# Attempt disposition

1. The initial `--no-restore` build failed because the immutable predecessor
   archive intentionally contained no `obj/project.assets.json`. This is not a
   product failure. Native .NET 8 restore completed before the controlling build.
2. The first sandboxed VSTest run aborted because the sandbox denied its local
   IPC socket. The authorized rerun produced the controlling 33/33 TRX.
3. The first HTTP matrix produced 48/49. `expired-inside-skew` was signed before
   a slow Mongo profiler reset and expired beyond its five-second window before
   the request reached the API. This is retained as failed evidence. The
   evidence-only harness was corrected to reset profiling before signing; no
   product or policy changed. The controlling rerun produced 49/49.
4. A fresh three-service Auth login/refresh chain was not completed in the
   writer lane. The predecessor result is source-bound historical evidence only;
   the different-agent verifier must reproduce it against this final source.
