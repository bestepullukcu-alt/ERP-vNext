"""CT capture of existing test/skip/regression tools; never edits product sources."""
import argparse
import datetime
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET

p = argparse.ArgumentParser()
p.add_argument("--repo", required=True)
p.add_argument("--trx", required=True)
p.add_argument("--out", required=True)
p.add_argument("--base")
p.add_argument("--assembly")
p.add_argument("--saved-baseline", action="store_true")
a = p.parse_args()
repo = Path(a.repo).resolve()
trx = Path(a.trx).resolve()
out = Path(a.out).resolve()
assert not out.is_relative_to(repo), "Evidence output must stay outside the worker tree"
raw = trx.read_bytes()
lf = raw.replace(b"\r\n", b"\n")
sha = lambda b: hashlib.sha256(b).hexdigest()
r = ET.fromstring(raw)
counters = r.find(".//{*}Counters")
assert counters is not None, "No real TRX counters"
results = r.findall(".//{*}UnitTestResult")
targets = ["Pvg_mongo_launch_arguments_follow_the_host_platform_without_weakening_isolation",
           "Pvg_mongo_test_harness_refuses_port_27017_and_fails_without_mongod",
           "Configuration_scan_fails_closed_on_inaccessible_directory"]
target_results = {n: [x.attrib for x in results if x.get("testName", "").endswith(n)] for n in targets}
mongo = [x.attrib for x in results if any(prefix in x.get("testName", "") for prefix in
         [".Persistence.PvgMongoStoreTests.", ".Persistence.PvgMongoApiTests.", ".Persistence.PvgMongoDedicatedTests."])]
argv = [sys.executable, "-B", str(repo / "scripts/check_trx_no_skipped.py"), "--suite", "pvg-api", str(trx)]
skip = subprocess.run(argv, capture_output=True, text=True, encoding="utf-8")
commands = [dict(argv=argv, exit_code=skip.returncode, stdout=skip.stdout, stderr=skip.stderr)]
record = dict(kind="saved pre-ACL baseline; not a new test run" if a.saved_baseline else "capture of supplied normal run",
              wrapper_lf_sha256=sha(Path(__file__).read_bytes().replace(b"\r\n", b"\n")),
              repository=str(repo), trx_path=str(trx), trx_raw_sha256=sha(raw), trx_lf_sha256=sha(lf),
              counters=counters.attrib, result_count=len(results), target_results=target_results,
              real_mongo_test_count=len(mongo), commands=commands)
if a.saved_baseline:
    assert sha(lf) == "1f7a5f2188bf8eb691c8c654dc9ce3a1713926a5abf9b538a9f6cdbb73f18846"
    failed = [x for x in results if x.get("outcome") == "Failed"]
    assert len(results) == 395 and len(failed) == 1 and failed[0].get("testName", "").endswith(targets[2])
    assert "PlatformNotSupportedException" in "".join(failed[0].itertext())
    record["acceptance_passed"] = False
    record["baseline_source"] = dict(head="58920519a17e8f4c085738642b33e36689bc8fee",
          patch_sha256="bd7ebf58ac4866fdba94729d0dfb29f688ec87e8a26d7cc80d1f7bbb9c50617f",
          original_raw_trx_sha256="c3b30fb5a2ce4881d6a24e60480b2c4c6cdac69d1178a0455a6791b65c559404")
else:
    assert a.base and a.assembly, "Normal capture requires exact ExpectedHEAD and freshly compiled test assembly"
    argv = [sys.executable, "-B", str(repo / "scripts/known_failures.py"), "check", str(trx), "--suite", "pvg-api"]
    known = subprocess.run(argv, capture_output=True, text=True, encoding="utf-8")
    commands.append(dict(argv=argv, exit_code=known.returncode, stdout=known.stdout, stderr=known.stderr))
    prefix = "services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/"
    allowed = [prefix+"Persistence/PvgMongoTestHarness.cs", prefix+"Persistence/PvgPersistenceBoundaryScanTests.cs",
               prefix+"PvgServiceApiHostTests.cs"]
    changed = subprocess.check_output(["git", "-C", str(repo), "diff", "--name-only", a.base, "--"]).decode().splitlines()
    sources = []
    for rel in allowed:
        path = repo/rel
        b = path.read_bytes()
        sources.append(dict(path=rel, raw_sha256=sha(b), lf_sha256=sha(b.replace(b"\r\n", b"\n")),
                            mtime_ns=path.stat().st_mtime_ns))
    assembly = Path(a.assembly).resolve()
    assert assembly.is_relative_to(repo) and assembly.name == "Diten.PvgService.Api.Tests.dll"
    record["source_scope"] = dict(base=a.base, changed=changed, only_three_paths=set(changed)==set(allowed), sources=sources)
    record["assembly"] = dict(path=str(assembly),sha256=sha(assembly.read_bytes()),mtime_ns=assembly.stat().st_mtime_ns,
                             newer_than_sources=assembly.stat().st_mtime_ns >= max(x["mtime_ns"] for x in sources))
    times = r.find("./{*}Times")
    assert times is not None and times.get("start") and times.get("finish")
    started = datetime.datetime.fromisoformat(times.get("start").replace("Z", "+00:00"))
    finished = datetime.datetime.fromisoformat(times.get("finish").replace("Z", "+00:00"))
    assert started.tzinfo is not None and finished.tzinfo is not None
    fresh_run = finished >= started and started.timestamp() >= assembly.stat().st_mtime
    record["trx_run_freshness"] = dict(start=times.get("start"),finish=times.get("finish"),
                                       started_after_compiled_assembly=fresh_run)
    passed = (len(results)==395 and all(x.get("outcome")=="Passed" for x in results)
              and len(mongo)==35 and all(len(v)==1 and v[0].get("outcome")=="Passed" for v in target_results.values())
              and set(changed)==set(allowed) and all(x["exit_code"]==0 for x in commands)
              and record["assembly"]["newer_than_sources"] and fresh_run)
    record["acceptance_passed"] = passed
record["limits"] = "Normal capture does not independently prove ACL rollback, process cleanup, method-only scope or six sabotage groups; those still require raw source/runtime evidence and independent E2."
out.parent.mkdir(parents=True, exist_ok=True)
out.write_bytes((json.dumps(record, ensure_ascii=False, indent=2)+"\n").encode())
print(json.dumps(dict(acceptance_passed=record["acceptance_passed"], results=len(results), mongo_tests=len(mongo),
                     command_exits=[x["exit_code"] for x in commands])))
sys.exit(0 if record["acceptance_passed"] else 1)

