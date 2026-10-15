#!/usr/bin/env python3
"""verify_module_id.py — Module ID Canonicalization Gate (DCP-002).

The Blueprint workbook
    docs/reference/blueprint/System Capability & Implementation Blueprint - master 8.1.xlsx :: Blueprint_Data
is the canonical authority for MOD IDs and canonical capability names.

Modes
-----
  --check-id MOD-XXXX --name "Canonical Name" [--parent MOD-YYYY] [--repo-only]
      Fail-closed preflight for a single new/changed canonical ID.
      Exit 0 = proven against Blueprint/registry.  Exit 2 = BLOCKED.

  --check-all
      Repository audit. Lists the unresolved legacy/reservation backlog and
      filename/id warnings (advisory). Exit 0 normally; exit 1 only on a NEW
      hard violation (a duplicate canonical ID). The known pre-existing backlog
      is reported, never silently ignored, and never blocks the safe migration.

Authority: execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md
"""
import argparse
import glob
import os
import re
import sys

BLUEPRINT = "docs/reference/blueprint/System Capability & Implementation Blueprint - master 8.1.xlsx"
REGISTRY = "execution/registries/module-id-registry.md"
PACK_GLOB = "execution/domains/*/module-packs/*.md"


def norm(s):
    s = re.sub(r"\s+", " ", str(s or "").strip().lower())
    return re.sub(r"[^a-z0-9 ]", "", s).strip()


def load_blueprint(root):
    """Return {MOD-ID: name} or None if the workbook cannot be read (fail-closed)."""
    path = os.path.join(root, BLUEPRINT)
    if not os.path.exists(path):
        return None
    try:
        import openpyxl
    except ImportError:
        return None
    try:
        wb = openpyxl.load_workbook(path, read_only=True, data_only=True)
    except Exception:
        return None
    if "Blueprint_Data" not in wb.sheetnames:
        return {}
    rows = list(wb["Blueprint_Data"].iter_rows(values_only=True))
    if not rows:
        return {}
    header = list(rows[0])
    try:
        ci = header.index("Module ID")
        cn = header.index("Module Name")
    except ValueError:
        return {}
    bp = {}
    for r in rows[1:]:
        if ci < len(r) and r[ci] and re.match(r"^MOD-\d+$", str(r[ci]).strip()):
            bp[str(r[ci]).strip()] = str(r[cn]).strip() if cn < len(r) and r[cn] else ""
    return bp


def load_registry(root):
    path = os.path.join(root, REGISTRY)
    rows = []
    if not os.path.exists(path):
        return rows
    for line in open(path, encoding="utf-8"):
        s = line.strip()
        if not s.startswith("|"):
            continue
        c = [x.strip() for x in s.strip("|").split("|")]
        if len(c) < 8 or c[0] in ("Canonical ID",) or set(c[0]) <= set("-"):
            continue
        rows.append({
            "id": c[0], "name": c[1], "type": c[3], "status": c[4],
            "alias": c[5], "replacement": c[6],
            "notes": c[8] if len(c) > 8 else "",
        })
    return rows


def is_deprecated(row):
    return "deprecated" in row["status"].lower() or "alias" in row["type"].lower() \
        or "retired" in row["status"].lower() or "standard" in row["type"].lower()


def load_packs(root):
    out = {}
    for p in glob.glob(os.path.join(root, PACK_GLOB)):
        if os.path.basename(p).lower() == "readme.md":
            continue
        fid = None
        try:
            with open(p, encoding="utf-8") as f:
                in_fm = False
                for ln in f:
                    if ln.strip() == "---":
                        if in_fm:
                            break
                        in_fm = True
                        continue
                    if in_fm:
                        m = re.match(r"\s*id:\s*(\S+)", ln)
                        if m:
                            fid = m.group(1).strip()
        except OSError:
            pass
        out[p] = fid
    return out


def split_id(mid):
    """Return (base, parent_if_child). FU/child suffix -> parent is the base."""
    m = re.match(r"^(MOD-\d+)(-FU\w+|-[A-Za-z0-9]+)$", mid)
    if m:
        return m.group(1), m.group(1)
    if re.match(r"^MOD-\d+$", mid):
        return mid, None
    return mid, None


def check_id(root, mid, name, parent, repo_only):
    bp = load_blueprint(root)
    reg = load_registry(root)
    fails = []

    active = [r for r in reg if not is_deprecated(r)]
    if [r["id"] for r in active].count(mid) > 1:
        fails.append(f"duplicate canonical id {mid} in registry")
    for r in active:
        if r["id"] == mid and norm(r["name"]) and norm(r["name"]) != norm(name):
            fails.append(f"registry collision: {mid} already = '{r['name']}' (!= '{name}')")

    base, inferred_parent = split_id(mid)
    parent = parent or inferred_parent

    if bp is None:
        fails.append("Blueprint workbook unreadable/openpyxl missing — cannot prove ID (fail-closed)")
        return _verdict(mid, fails)

    if inferred_parent:
        known = parent in bp or any(r["id"] == parent for r in reg)
        if not known:
            fails.append(f"parent {parent} not found in Blueprint or registry")

    if base in bp:
        if not inferred_parent and norm(name) and norm(bp[base]) and norm(name) != norm(bp[base]):
            fails.append(f"canonical-name mismatch: Blueprint {base} = '{bp[base]}' (!= '{name}')")
    else:
        if repo_only:
            evidence = [r for r in reg if r["id"] == mid
                        and ("reserv" in r["notes"].lower() or "reserv" in r["status"].lower())]
            if not evidence:
                fails.append(f"repo-only {mid} lacks explicit EA reservation evidence in registry")
        else:
            fails.append(f"{base} not in Blueprint and --repo-only not set — cannot prove ID")

    return _verdict(mid, fails)


def _verdict(mid, fails):
    if fails:
        print(f"BLOCKED  {mid}")
        for f in fails:
            print(f"   - {f}")
        print("Gate failed closed. See DCP-002.")
        return 2
    print(f"OK  {mid}: proven against Blueprint/registry.")
    return 0


def runtime_hits(root, token):
    import subprocess
    for tool in (["rg", "-l", "--"], ["grep", "-rl", "--"]):
        try:
            out = subprocess.run(tool + [token, "services", "frontend", "gateway", "tests"],
                                 cwd=root, capture_output=True, text=True, timeout=30)
            return [l for l in out.stdout.splitlines() if l.strip()]
        except FileNotFoundError:
            continue
        except Exception:
            return []
    return []


def check_candidate(root, cid, name):
    """Fail-closed gate for a temporary CAND-CAP governance identity (DCP-002)."""
    fails = []
    if not re.match(r"^CAND-CAP-\d{4}(-FU\d+)?$", cid):
        fails.append("candidate ID must match CAND-CAP-#### (optional -FUn)")
    if cid.startswith("MOD-"):
        fails.append("a candidate ID can never be a Blueprint MOD id")
    reg = load_registry(root)
    rows = [r for r in reg if r["id"] == cid]
    if not rows:
        fails.append(f"{cid} has no registry row")
    active = [r for r in rows if "candidate" in r["type"].lower()]
    if len(active) > 1:
        fails.append(f"duplicate candidate id {cid}")
    for r in active:
        if norm(r["name"]) and norm(r["name"]) != norm(name):
            fails.append(f"conflicting candidate name: registry {cid} = '{r['name']}'")
        if "pending-ea" not in r["status"].lower() and "candidate" not in r["status"].lower():
            fails.append(f"{cid} is not marked temporary/pending-EA")
    recon = os.path.join(root, "execution/portfolio/blueprint-master-plan-reconciliation.md")
    if not (os.path.exists(recon) and cid in open(recon, encoding="utf-8").read()):
        fails.append(f"{cid} not recorded in the reconciliation ledger")
    hits = runtime_hits(root, cid)
    if hits:
        fails.append(f"candidate {cid} must NOT appear in runtime paths: {hits[:3]}")
    if fails:
        print(f"BLOCKED  candidate {cid}")
        for f in fails:
            print(f"   - {f}")
        print("Candidate gate failed closed. See DCP-002.")
        return 2
    print(f"OK  candidate {cid}: temporary governance identity, pending EA, not Blueprint-backed, not in runtime.")
    return 0


def check_all(root):
    reg = load_registry(root)
    bp = load_blueprint(root)
    packs = load_packs(root)

    active = [r for r in reg if not is_deprecated(r)]
    seen = {}
    for r in active:
        seen[r["id"]] = seen.get(r["id"], 0) + 1
    hard = [f"duplicate canonical id: {i} ({n}x)" for i, n in seen.items() if n > 1]

    warnings = []
    for p, fid in packs.items():
        fn = os.path.basename(p)[:-3]
        if fid and fn != fid and not fn.startswith(fid + "-"):
            warnings.append(f"filename/id: {os.path.basename(p)} has frontmatter id '{fid}'")

    backlog = []
    for r in active:
        if re.match(r"^(PSS|NEW)-", r["id"]):
            backlog.append(f"{r['id']} ({r['name']}) — legacy prefix; pending EA reservation")
        elif re.match(r"^MOD-\d+", r["id"]) and bp is not None and split_id(r["id"])[0] not in bp:
            backlog.append(f"{r['id']} ({r['name']}) — repo-only MOD not in Blueprint; needs EA reservation/confirm")

    print("== verify_module_id --check-all ==")
    print(f"registry active rows: {len(active)} / {len(reg)} | packs: {len(packs)} | "
          f"blueprint: {'loaded' if bp else 'UNAVAILABLE'}")
    candidates = [f"{r['id']} ({r['name']})" for r in reg if "candidate" in r["type"].lower()]
    compat = sorted({r["id"] for r in reg if "runtime compat" in r["notes"].lower()})
    active_legacy = sorted({r["id"] for r in active if re.match(r"^(PSS|NEW)-\d", r["id"])})
    print(f"\n[active candidate (CAND-CAP) records: {len(candidates)} — temporary, pending EA MOD-xxxx]")
    for c in sorted(candidates):
        print(f"   - {c}")
    print(f"\n[runtime compatibility literals (governance-aliased, do NOT rename in code): {len(compat)}]")
    for c in compat:
        print(f"   - {c}")
    print(f"\n[invalid ACTIVE legacy PSS-*/NEW-* canonical records (must be 0): {len(active_legacy)}]")
    for c in active_legacy:
        print(f"   - {c}")
    print(f"\n[unresolved legacy/reservation backlog: {len(backlog)} — advisory, does not block]")
    for b in sorted(backlog):
        print(f"   - {b}")
    print(f"\n[filename/id warnings: {len(warnings)} — advisory]")
    for w in sorted(warnings):
        print(f"   - {w}")
    print(f"\n[HARD violations: {len(hard)}]")
    for h in hard:
        print(f"   - {h}")
    return 1 if hard else 0


# ----------------------------------------------------------------------------------------------- discovery (İ3)
# Added 2026-10-15 (CT step 2). Separate, explicitly called modes; --check-id, --check-all and --candidate are unchanged.
#
#   --discover "<name>" [--resource <gateway resource>] [--sor "<SoR object>"]
#       Looks for existing or in-flight work that a new module / work package could duplicate, in: Blueprint_Data
#       (id, name, domain, capability group, SoR objects), SoR_Map, the module registry (deprecated aliases are
#       information, not findings), module packs on every git ref visible on this machine, the gateway's resource ->
#       service map, and the registered Control Tower task sources. Read-only.
#       exit 0  "incelenen kaynaklarda eşleşme bulunamadı" (absence is NOT proved: see the EKSİK lines)
#       exit 3  "inceleme gerekli" — every finding with its reason and possible explanations
#       exit 2  "tamamlanamadı" — a source could not be read (missing openpyxl, unreadable workbook, missing column,
#               malformed gateway file, git error); never reported as "not found"
#       The last line is "rapor-sha256: <hex>": SHA-256 of the UTF-8 text of every line before it, joined with
#       "\n" and ending with "\n".
#   --check-report <file>
#       Recomputes rapor-sha256 of a saved report. exit 0 = matches, exit 2 = malformed or altered.

DISCOVER_STOP = {"and", "the", "for", "with", "management", "service", "services", "module", "system", "api",
                 "platform", "internal", "tenant", "admin"}
GATEWAY = "gateway/Diten.ApiGateway/ocelot.json"
QUEUE_REGISTRY = "execution/delivery/ct-queue-registry.tsv"
BLUEPRINT_COLUMNS = ["Module ID", "Module Name", "Domain / Landscape", "Capability Group", "SoR Primary Object(s)"]
SOR_COLUMNS = ["SoR Primary Object(s)", "Owning Module IDs", "Owning Module Names"]
ROUTE_SKIP = {"api", "v1", "v2", "platform", "internal", "tenant", "admin"}
EXPLAIN = "olası açıklamalar: üretici-tüketici ilişkisi · eski takma ad · onaylı devir · eski dal"
GAPS = [
    "EKSİK: bu makinede bulunmayan ya da son fetch'ten sonra itilen dallar görülmedi",
    "EKSİK: commit edilmemiş ya da başka makinedeki işler görülmedi",
    "EKSİK: kayıtlı olmayan görev kaynakları (sohbetler, notlar) görülmedi",
]


class Incomplete(Exception):
    """A discovery source could not be read: the result is 'tamamlanamadı', never 'not found'."""


def _tokens(text):
    out = set()
    for w in re.findall(r"[a-z0-9]+", str(text or "").lower()):
        if len(w) < 3 or w in DISCOVER_STOP:
            continue
        if w.endswith("ies"):
            w = w[:-3] + "y"
        elif w.endswith("s") and not w.endswith("ss"):
            w = w[:-1]
        out.add(w)
    return out


def _similar(query, text):
    t = _tokens(text)
    return len(query & t) >= 2 or (len(query) == 1 and query <= t)


def _git(root, *args, stdin=None):
    import subprocess
    if not args or args[0] not in ("rev-parse", "for-each-ref", "ls-tree", "show", "cat-file"):
        raise Incomplete(f"git {args[:1]} is not a read-only command allowed here")
    env = dict(os.environ, GIT_OPTIONAL_LOCKS="0")
    done = subprocess.run(["git", "-C", root, *args], capture_output=True, input=stdin, env=env)
    if done.returncode != 0:
        raise Incomplete(f"git {' '.join(args[:2])} → {done.returncode}")
    return done.stdout.decode("utf-8", errors="replace")


def _sha256_bytes(data):
    import hashlib
    return hashlib.sha256(data).hexdigest()


def read_blueprint_sheets(path):
    """(Blueprint_Data rows as dicts, SoR_Map rows as dicts). Raises Incomplete on any reading problem."""
    try:
        import openpyxl
    except ImportError:
        raise Incomplete("openpyxl yok — python3 -m pip install -r scripts/requirements-tools.txt")
    try:
        wb = openpyxl.load_workbook(path, read_only=True, data_only=True)
    except Exception as e:  # unreadable or not a workbook
        raise Incomplete(f"Blueprint okunamadı ({type(e).__name__})")
    out = []
    for sheet, columns in (("Blueprint_Data", BLUEPRINT_COLUMNS), ("SoR_Map", SOR_COLUMNS)):
        if sheet not in wb.sheetnames:
            raise Incomplete(f"Blueprint'te '{sheet}' sayfası yok")
        rows = list(wb[sheet].iter_rows(values_only=True))
        header = [str(h).strip() if h is not None else "" for h in (rows[0] if rows else [])]
        missing = [c for c in columns if c not in header]
        if missing:
            raise Incomplete(f"'{sheet}' sayfasında sütun yok: {', '.join(missing)}")
        idx = {c: header.index(c) for c in columns}
        out.append([{c: (str(r[i]).strip() if i < len(r) and r[i] is not None else "") for c, i in idx.items()} for r in rows[1:]])
    return out[0], out[1]


def gateway_resources(text):
    """{resource: {service port, ...}} from an ocelot.json text. Raises Incomplete when the file is not usable."""
    import json
    try:
        data = json.loads(text)
    except ValueError:
        raise Incomplete("gateway dosyası geçerli JSON değil")
    routes = data.get("Routes") if isinstance(data, dict) else None
    if not isinstance(routes, list) or not routes:
        raise Incomplete("gateway dosyasında rota yok")
    out = {}
    for r in routes:
        segs = [x for x in str(r.get("UpstreamPathTemplate", "")).lower().strip("/").split("/")
                if x and not x.startswith("{") and x not in ROUTE_SKIP]
        hosts = r.get("DownstreamHostAndPorts") or [{}]
        if segs:
            out.setdefault(segs[0], set()).add(str(hosts[0].get("Port") or r.get("ServiceName") or "?"))
    return out


def discover(root, name, resource=None, sor=None):
    """Return (report lines without the hash line, exit code)."""
    query = _tokens(name)
    lines = ["KEŞİF RAPORU v1", f"sorgu: {name} | kaynak: {resource or '-'} | sor: {sor or '-'}"]
    findings, info = [], []
    try:
        script = os.path.abspath(__file__)
        head = _git(root, "rev-parse", "HEAD").strip()
        lines.append(f"araç: .antigravity/scripts/verify_module_id.py sha256 {_sha256_bytes(open(script, 'rb').read())} · depo HEAD {head}")
        try:
            main_sha = _git(root, "rev-parse", "--verify", "origin/main^{commit}").strip()
        except Incomplete:
            main_sha = None
        fetch_head = os.path.join(root, _git(root, "rev-parse", "--git-common-dir").strip(), "FETCH_HEAD")
        import datetime
        fetched = (datetime.datetime.fromtimestamp(os.path.getmtime(fetch_head)).strftime("%Y-%m-%dT%H:%M")
                   if os.path.exists(fetch_head) else "doğrulanamadı")
        lines.append(f"origin/main: {main_sha or 'yok'} · son fetch: {fetched} · uzak durum doğrulanmadı")
        if not query:
            raise Incomplete("sorgu adında anlamlı sözcük yok")

        # Blueprint and SoR_Map
        bp_path = os.path.join(root, BLUEPRINT)
        if not os.path.exists(bp_path):
            raise Incomplete("Blueprint dosyası yok")
        bp_rows, sor_rows = read_blueprint_sheets(bp_path)
        lines.append(f"kaynak blueprint: {BLUEPRINT} sha256 {_sha256_bytes(open(bp_path, 'rb').read())} · "
                     f"Blueprint_Data {len(bp_rows)} satır · SoR_Map {len(sor_rows)} satır")
        matched_ids = set()
        for r in bp_rows:
            if r["Module ID"] and _similar(query, r["Module Name"]):
                matched_ids.add(r["Module ID"])
                findings.append(f"BULGU blueprint: {r['Module ID']} '{r['Module Name']}' ({r['Domain / Landscape']} / "
                                f"{r['Capability Group']}) — ad benzer; veri nesneleri: {r['SoR Primary Object(s)'][:80] or '-'}")
        for r in sor_rows:
            obj = r["SoR Primary Object(s)"]
            if (sor and sor.lower() in obj.lower()) or _similar(query, obj):
                findings.append(f"BULGU sor: '{obj[:80]}' sahibi {r['Owning Module IDs']} ({r['Owning Module Names'][:60]})")

        # module registry (deprecated aliases are information only)
        reg_path = os.path.join(root, REGISTRY)
        if not os.path.exists(reg_path):
            raise Incomplete("modül kayıt dosyası yok")
        reg = load_registry(root)
        lines.append(f"kaynak registry: {REGISTRY} sha256 {_sha256_bytes(open(reg_path, 'rb').read())} · {len(reg)} satır")
        for r in reg:
            if _similar(query, r["name"]) or r["id"] in matched_ids:
                if is_deprecated(r):
                    info.append(f"BİLGİ registry: {r['id']} '{r['name']}' eski takma ad/kullanımdan kalkmış — çakışma sayılmaz")
                else:
                    findings.append(f"BULGU registry: {r['id']} '{r['name']}' ({r['status']})")

        # module packs on every visible ref
        refs = [x for x in _git(root, "for-each-ref", "--format=%(refname) %(objectname)", "refs/heads", "refs/remotes").splitlines()
                if x and not x.split()[0].endswith("/HEAD")]
        specs = "\n".join(f"{x.split()[0]}:execution/domains" for x in refs) + "\n"
        trees = _git(root, "cat-file", "--batch-check=%(objectname) %(objecttype)", stdin=specs.encode()).splitlines()
        by_tree = {}
        for ref_line, tree_line in zip(refs, trees):
            parts = tree_line.split()
            if len(parts) == 2 and parts[1] == "tree":
                by_tree.setdefault(parts[0], []).append(ref_line.split()[0])
        packs = {}
        for tree, ref_names in by_tree.items():
            for path in _git(root, "ls-tree", "-r", "--name-only", tree).splitlines():
                if "/module-packs/" in path and path.endswith(".md") and not path.lower().endswith("readme.md"):
                    packs.setdefault("execution/domains/" + path, set()).update(ref_names)
        ref_digest = _sha256_bytes("\n".join(sorted(refs)).encode())
        lines.append(f"kaynak paketler: {len(refs)} ref · {len(by_tree)} farklı execution/domains ağacı · "
                     f"{len(packs)} paket yolu · ref-listesi-sha256 {ref_digest}")
        for path, ref_names in sorted(packs.items()):
            stem = os.path.basename(path)[:-3]
            pack_id = re.match(r"^(MOD-\d+(?:-FU\w+)?)", stem)
            if (pack_id and pack_id.group(1) in matched_ids) or _similar(query, stem.replace("-", " ")):
                shown = ", ".join(sorted(ref_names)[:3]) + (f", … +{len(ref_names) - 3}" if len(ref_names) > 3 else "")
                findings.append(f"BULGU paket: {path} — {len(ref_names)} ref'te ({shown})")

        # gateway resource -> service
        if main_sha:
            gw_text, gw_where = _git(root, "show", f"{main_sha}:{GATEWAY}"), f"origin/main {main_sha[:9]}"
        else:
            gw_text, gw_where = open(os.path.join(root, GATEWAY), encoding="utf-8").read(), "çalışma ağacı"
        resources = gateway_resources(gw_text)
        lines.append(f"kaynak gateway: {gw_where}:{GATEWAY} sha256 {_sha256_bytes(gw_text.encode())} · {len(resources)} kaynak")
        wanted = (resource or "").strip("/").lower()
        for res, ports in sorted(resources.items()):
            hit = (res == wanted) if wanted else (_tokens(res.replace("-", " ")) and _tokens(res.replace("-", " ")) <= query)
            if hit:
                note = " — AYNI KAYNAK BİRDEN ÇOK SERVİSTE" if len(ports) > 1 else ""
                findings.append(f"BULGU gateway: '{res}' → servis port(lar)ı {', '.join(sorted(ports))}{note}")

        # registered Control Tower task sources
        q_reg = os.path.join(root, QUEUE_REGISTRY)
        if os.path.exists(q_reg):
            q_lines = open(q_reg, encoding="utf-8").read().split("\n")
            header = q_lines[0].split("\t")
            if "path" not in header:
                raise Incomplete("görev kaynağı kayıt dosyasında 'path' sütunu yok")
            for row in (l.split("\t") for l in q_lines[1:] if l.strip()):
                qpath = row[header.index("path")]
                full = os.path.join(root, qpath)
                if not os.path.exists(full):
                    raise Incomplete(f"kayıtlı görev kaynağı '{qpath}' yok")
                text = open(full, encoding="utf-8").read()
                lines.append(f"kaynak görev-kaynağı: {qpath} sha256 {_sha256_bytes(text.encode())}")
                t_lines = text.split("\n")
                t_head = t_lines[0].split("\t")
                for t_row in (l.split("\t") for l in t_lines[1:] if l.strip()):
                    cells = dict(zip(t_head, t_row))
                    if (cells.get("module") and cells["module"] in matched_ids) or _similar(query, cells.get("item", "")[:200]):
                        findings.append(f"BULGU görev: {qpath} {cells.get('id', '?')} [{cells.get('state', '?')}] {cells.get('item', '')[:70]}")
    except Incomplete as e:
        lines += info + findings + GAPS + [f"sonuç: tamamlanamadı — {e}", "çıkış: 2"]
        return lines, 2
    except OSError as e:
        lines += info + findings + GAPS + [f"sonuç: tamamlanamadı — {type(e).__name__}", "çıkış: 2"]
        return lines, 2
    lines += info + findings
    if findings:
        lines += [EXPLAIN] + GAPS + ["sonuç: inceleme gerekli — her bulgu için karar ve gerekçe iş paketi kaydına yazılmalı", "çıkış: 3"]
        return lines, 3
    lines += GAPS + ["sonuç: incelenen kaynaklarda eşleşme bulunamadı", "çıkış: 0"]
    return lines, 0


def report_hash(lines):
    return _sha256_bytes(("\n".join(lines) + "\n").encode("utf-8"))


def check_report(path):
    try:
        text = open(path, encoding="utf-8").read()
    except (OSError, UnicodeDecodeError) as e:
        print(f"RAPOR OKUNAMADI: {type(e).__name__}")
        return 2
    lines = text.rstrip("\n").split("\n")
    if not lines or not lines[-1].startswith("rapor-sha256: "):
        print("RAPOR BOZUK: son satır 'rapor-sha256: <hex>' değil")
        return 2
    body, recorded = lines[:-1], lines[-1][len("rapor-sha256: "):].strip()
    if report_hash(body) != recorded:
        print("RAPOR DEĞİŞTİRİLMİŞ: rapor-sha256 içerikle uyuşmuyor")
        return 2
    print(f"RAPOR GEÇERLİ: {next((l for l in body if l.startswith('sonuç:')), 'sonuç satırı yok')}")
    return 0


def main():
    ap = argparse.ArgumentParser(description="Module ID Canonicalization Gate (DCP-002)")
    ap.add_argument("root", nargs="?", default=".")
    ap.add_argument("--check-id")
    ap.add_argument("--name", default="")
    ap.add_argument("--parent")
    ap.add_argument("--repo-only", action="store_true")
    ap.add_argument("--candidate")
    ap.add_argument("--check-all", action="store_true")
    ap.add_argument("--discover", help="look for existing or in-flight work a new module / work package could duplicate")
    ap.add_argument("--resource", help="with --discover: a gateway resource name, e.g. position-assignments")
    ap.add_argument("--sor", help="with --discover: a SoR primary object text")
    ap.add_argument("--check-report", help="recompute rapor-sha256 of a saved --discover report")
    a = ap.parse_args()
    if a.check_report or a.discover:
        if hasattr(sys.stdout, "reconfigure"):
            sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        if a.check_report:
            sys.exit(check_report(a.check_report))
        report, code = discover(a.root, a.discover, a.resource, a.sor)
        print("\n".join(report))
        print(f"rapor-sha256: {report_hash(report)}")
        sys.exit(code)
    if a.check_all:
        sys.exit(check_all(a.root))
    if a.candidate:
        if not a.name:
            print("ERROR: --name is required with --candidate (fail-closed).")
            sys.exit(2)
        sys.exit(check_candidate(a.root, a.candidate, a.name))
    if a.check_id:
        if not a.name:
            print("ERROR: --name is required with --check-id (fail-closed).")
            sys.exit(2)
        sys.exit(check_id(a.root, a.check_id, a.name, a.parent, a.repo_only))
    ap.print_help()
    sys.exit(2)


if __name__ == "__main__":
    main()
