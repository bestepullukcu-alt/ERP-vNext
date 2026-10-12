# Process cleanup

Verifier-owned `mongod` PID 32340 used `rsmod192`, `127.0.0.1:57192`, unique dbpath `/private/tmp/mvp6-mod0192-fault-rever01-mm1_83w8/mongo-db`, and `enableTestCommands=1`. `mongosh ... shutdownServer()` closed the connection as expected; final `lsof -nP -iTCP:57192 -sTCP:LISTEN` returned no listener. Operational port 27017 and other lane processes were not touched.
