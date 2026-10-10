#!/usr/bin/env python3
"""CANDIDATE — NOT ACTIVE. MVP6 evidence kit phase K04b: disposable Gateway runtime with lane-only routes.

The committed gateway/Diten.ApiGateway/ocelot.json routes to the OPERATIONAL ports (5004, 5011, 5056-5064).
Prior lanes ran a disposable gateway-runtime/ocelot.json (hash recorded in a08 raw/runtime-and-input-hashes.txt).
This phase makes that reproducible and fail-closed:
  * copy the freshly built Gateway output (bin/Release/net8.0) to W/gateway-runtime — the DLL hash must stay equal;
  * rewrite each DownstreamHostAndPorts entry: mapped operational port -> lane port; host -> 127.0.0.1;
  * EVERY unmapped downstream -> the lane sink port (kept closed), so no route can reach a live operational service;
  * GlobalConfiguration.BaseUrl -> the lane gateway URL.
The repository file is only read. Writes W/gateway-runtime/ocelot.json and E/raw/gateway-routes.tsv.

Usage: k04b_gateway_routes.py --work W --evidence E [--map "5056=auth 5057=platform 5059=mdm 5061=supplychain"]
"""
import argparse, collections, hashlib, json, pathlib, shutil, sys


def sha(p):
    return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--work", required=True)
    ap.add_argument("--evidence", required=True)
    ap.add_argument("--map", default="5056=auth 5057=platform 5059=mdm 5061=supplychain")
    a = ap.parse_args()
    work = pathlib.Path(a.work)
    ports = json.loads((work / "ports.json").read_text())["ports"]
    mapping = {}
    for item in a.map.split():
        op, svc = item.split("=")
        if svc in ports:
            mapping[int(op)] = ports[svc]
    sink = ports["sink"]

    built = work / "source/gateway/Diten.ApiGateway/bin/Release/net8.0"
    if not (built / "Diten.ApiGateway.dll").exists():
        raise SystemExit("build the Gateway (K06 build step) before K04b")
    rt = work / "gateway-runtime"
    if rt.exists():
        shutil.rmtree(rt)
    shutil.copytree(built, rt)
    if sha(built / "Diten.ApiGateway.dll") != sha(rt / "Diten.ApiGateway.dll"):
        raise SystemExit("copied Gateway DLL hash differs")

    src_ocelot = work / "source/gateway/Diten.ApiGateway/ocelot.json"
    cfg = json.loads(src_ocelot.read_text(encoding="utf-8-sig"))
    counts = collections.Counter()
    for route in cfg.get("Routes", []):
        for hp in route.get("DownstreamHostAndPorts", []):
            op = int(hp.get("Port", 0))
            new = mapping.get(op, sink)
            counts[(op, new, "mapped" if op in mapping else "sink")] += 1
            hp["Host"], hp["Port"] = "127.0.0.1", new
        if route.get("DownstreamScheme", "http") != "http":
            route["DownstreamScheme"] = "http"
    gc = cfg.setdefault("GlobalConfiguration", {})
    gc["BaseUrl"] = f"http://127.0.0.1:{ports['gateway']}"
    out = rt / "ocelot.json"
    out.write_text(json.dumps(cfg, indent=2) + "\n")

    raw = pathlib.Path(a.evidence, "raw"); raw.mkdir(parents=True, exist_ok=True)
    with open(raw / "gateway-routes.tsv", "w") as f:
        f.write("operational_port\tlane_port\tdisposition\tdownstream_entries\n")
        for (op, new, disp), n in sorted(counts.items()):
            f.write(f"{op}\t{new}\t{disp}\t{n}\n")
        f.write(f"#source_ocelot_sha256\t{sha(src_ocelot)}\n#runtime_ocelot_sha256\t{sha(out)}\n"
                f"#gateway_dll_sha256\t{sha(rt / 'Diten.ApiGateway.dll')}\n")
    print(f"K04b PASS: {sum(counts.values())} downstream entries; mapped={sum(n for k, n in counts.items() if k[2] == 'mapped')} "
          f"sink={sum(n for k, n in counts.items() if k[2] == 'sink')}; runtime ocelot {sha(out)}")


if __name__ == "__main__":
    main()
