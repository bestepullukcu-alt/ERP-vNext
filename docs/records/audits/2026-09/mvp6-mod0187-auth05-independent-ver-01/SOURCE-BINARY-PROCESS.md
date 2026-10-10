# Fresh lineage manifest

| Stage | Bound evidence |
|---|---|
| Immutable source archive | `eaf786e647ef019f862c7dcc4f0b2ab29b2b5a36ff1bee98d2cca8d2863cc766` |
| Source manifest | 97/97, `4ae0f286479761512f50216e5bd8be721e48ae4839c062b57cfdd29696e2daa8` |
| Program.cs | `a72a05a5e185e04d70ba221f086baacb8c8fca11ea061f289cb4b54fd430254c` before and after |
| Restore/build | exit 0 / exit 0; 0 warnings, 0 errors |
| API DLL | `3138a53a4eec057786afb2a32f1b5e73da189338a968072e38a368d338629368` |
| Test DLL | `156514e2f35d4fdf4adc1f892a93b2a4614d4b9d6f4bf06006c6f19d0651b4b5` |
| Main process | PID 86391; fresh API DLL; HTTP 5067; Mongo 27917 |
| Failure process | PID 87173; same DLL; refused dependency 59998 |
| Restart process | PID 87668; same DLL and persisted DB |
| HTTP/DB | authenticated probe, concurrency, failure, restart and JWT-negative raw files |
| Cleanup | API 5067 absent; Mongo 27917 absent; ephemeral secret absent |

Exact process timestamps, commands, probes, bodies, DB indexes/counts and exit records are inside `raw-evidence.tar.gz`.

