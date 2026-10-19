#!/usr/bin/env python3
"""verify_module_id.py — Module ID Canonicalization Gate (DCP-002).

The Blueprint workbook
    docs/reference/blueprint/System Capability & Implementation Blueprint - master 8.7.xlsx :: Blueprint_Data
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
import unicodedata

BLUEPRINT = "docs/reference/blueprint/System Capability & Implementation Blueprint - master 8.7.xlsx"
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
    return blueprint_from_workbook(path)


def blueprint_from_workbook(source):
    """load_blueprint's parser: source is a path or a binary file object (e.g. a commit's blob). Same result rules."""
    try:
        import openpyxl
    except ImportError:
        return None
    try:
        wb = openpyxl.load_workbook(source, read_only=True, data_only=True)
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
    if not os.path.exists(path):
        return []
    with open(path, encoding="utf-8") as f:
        return registry_from_lines(f)


def registry_from_lines(lines):
    """load_registry's parser: any iterable of the registry's lines (a file, or a commit's blob split into lines)."""
    rows = []
    for line in lines:
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


REGISTRY_HEADER = ["Canonical ID", "Canonical Module Name", "Slug", "Type", "Status", "Deprecated Alias", "Replacement ID",
                   "Owner Domain", "Notes"]


def registry_table_problems(lines):
    """CT-FIX-01 (09): discovery may not lose a registry row silently. Every table in the registry file starts with the
    contract header (REGISTRY_HEADER, exactly) and a separator; each data row has its 9 columns, an id and a name. A
    table with another or a broken header — a cut header, or rows a blank line split from theirs — is a problem, not a
    table to skip. Each problem names its line. registry_from_lines (--check-id, --candidate) is not changed."""
    problems, state = [], None          # None: outside a table · "header": header read · "rows": in a valid table · "bad"
    for n, line in enumerate(lines, start=1):
        s = line.strip()
        if not s.startswith("|"):
            state = None
            continue
        c = [x.strip() for x in s.strip("|").split("|")]
        separator = all(re.fullmatch(r":?-{3,}:?", x) for x in c)
        if state is None:
            if c == REGISTRY_HEADER:
                state = "header"
            else:
                problems.append(f"satır {n}: tablo başlığı sözleşmedeki {len(REGISTRY_HEADER)} sütunluk başlık değil")
                state = "bad"
            continue
        if state == "bad":
            continue
        if state == "header":
            state = "rows"
            if not separator:
                problems.append(f"satır {n}: başlıktan sonra ayraç satırı yok")
            continue
        if separator:
            continue
        if len(c) != len(REGISTRY_HEADER):
            problems.append(f"satır {n}: {len(c)} sütun, başlık {len(REGISTRY_HEADER)} sütun")
        elif not c[0] or set(c[0]) <= set("-"):
            problems.append(f"satır {n}: kimlik hücresi boş")
        elif not c[1]:
            problems.append(f"satır {n}: ad hücresi boş")
    return problems


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
                fid = pack_id_from_lines(f)
        except OSError:
            pass
        out[p] = fid
    return out


def pack_id_from_lines(lines):
    """load_packs' parser for one pack: the front matter's last `id:` value, or None."""
    fid = None
    in_fm = False
    for ln in lines:
        if ln.strip() == "---":
            if in_fm:
                break
            in_fm = True
            continue
        if in_fm:
            m = re.match(r"\s*id:\s*(\S+)", ln)
            if m:
                fid = m.group(1).strip()
    return fid


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
        if repo_only and mid not in FROZEN_REPO_ONLY:
            fails.append(f"repo-only {mid} is not a reservation that existed before the Master-first rule — {MASTER_FIRST}")
        elif repo_only:
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


# Master-first rule (CEO 2026-10-16, Blueprint 8.7 Module_Change_Requests): the CAND-CAP namespace and the repo-only
# reservations are closed. These are the identities the registry held on main 37530d190 (2026-10-19); nothing outside
# them passes --candidate or --repo-only, whatever a registry row says. A missing capability is a Module Change Request
# in the Master, never a new row here.
FROZEN_CANDIDATES = frozenset({
    "CAND-CAP-0001", "CAND-CAP-0002", "CAND-CAP-0002-FU01", "CAND-CAP-0002-FU02", "CAND-CAP-0002-FU03",
    "CAND-CAP-0002-FU04", "CAND-CAP-0002-FU05", "CAND-CAP-0003", "CAND-CAP-0003-FU01", "CAND-CAP-0003-FU02",
    "CAND-CAP-0004", "CAND-CAP-0005", "CAND-CAP-0006", "CAND-CAP-0007", "CAND-CAP-0007-FU01", "CAND-CAP-0008",
    "CAND-CAP-0009", "CAND-CAP-0010", "CAND-CAP-0011"})
FROZEN_REPO_ONLY = frozenset({"NEW-MOD-0041"})
MASTER_FIRST = ("no new identity outside the Master (Master-first rule): raise a Module Change Request in the "
                "Blueprint's Module_Change_Requests sheet and wait for its MOD-ID or mapping")


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
        # Master-first rule (CEO 2026-10-16, Blueprint 8.7 Module_Change_Requests): no new candidate identity
        fails.append(f"{cid} has no registry row — {MASTER_FIRST}")
    if cid not in FROZEN_CANDIDATES:
        fails.append(f"{cid} is not one of the CAND-CAP identities that existed before the Master-first rule — {MASTER_FIRST}")
    for r in rows:
        if is_deprecated(r):
            fails.append(f"{cid} is a deprecated alias of {r['replacement'] or '?'} — use the canonical id "
                         f"(Blueprint 8.7 Main_Reconciliation_v8_2)")
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
    print(f"\n[active candidate (CAND-CAP) records: {len(candidates)} — historical, closed namespace (Master-first); FU allocation under a canonical parent open]")
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
#       exit 2  "tamamlanamadı" — a source could not be read or has a malformed record (missing openpyxl, unreadable
#               workbook, missing column, a partial Blueprint row, a route without its fields, a task source without
#               a matchable column or with a short row, git error) or the tool hit an unexpected error; never
#               reported as "not found"
#       Report v2: tool identity, query, origin/main SHA and last fetch, every source with sha256 and git blob id,
#       findings, gaps, every inspected ref with its commit SHA, result, exit code. Module packs are matched by file
#       name (id and slug); their work scope is not read (CT-FIX-03 R4: only the künye id, for repetition/disguise).
#       The last line is "rapor-sha256: <hex>": SHA-256 of the UTF-8 text of every line before it, joined with
#       "\n" and ending with "\n".
#       CT-FIX-03 (R4): a task row with an empty id or state, or with every matchable cell (item, module) empty, is
#       "tamamlanamadı" (V4A-F4); the task-source registry's own bytes are named by sha256 and blob (V4A-F8); every
#       visible pack blob's künye id is read by the shared reader (scripts/ct_queue.py kunye_fields) only to find a
#       repeated or disguised id — such a pack is "tamamlanamadı"; a pack without a künye is matched by name as before.
#       CT-FIX-06 (v7 Employee pilot): under the header, a bounded 'ÖZET' block (report_summary; at most SUMMARY_BUDGET
#       UTF-8 bytes): the counts; a file id on more than one matched pack path as a possible collision (a deprecated
#       alias in its own line, its paths not cleared) with those packs' status / approval_status / owner first; the
#       other matched packs' values (different values on different blobs shown unresolved, with blob and ref ids);
#       what did not fit; the limit. Information only — no finding, no decision, no gate; the BULGU lines and the other
#       modes are unchanged. A new report's bytes and hash include the block; a saved report keeps its own tool identity.
#       CT-FIX-07 (v7 C5 pilot): the block also names the open decisions (collisions; a matched pack whose owner is TBD,
#       empty or absent), the Blueprint candidates' ids and names, and the matched packs' domain-config paths with their
#       presence in HEAD's tree only (not on the pack's own ref; their content is not inspected by this command).
#   --check-report <file>
#       Recomputes rapor-sha256 of a saved report, byte for byte (parse_report: UTF-8, LF only, one final newline,
#       the hash line exactly 'rapor-sha256: <64 hex>' — CT-FIX-03 R4, V4B-B6). exit 0 = matches, exit 2 = malformed
#       or altered.

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
    # CT-FIX-03 (R4, Codex): the pack künye id is now read, only to refuse a repeated or disguised id; the pack's work
    # scope is still not read. A report from the older tool carries the older line and the gate asks for a new run.
    "EKSİK: paket iş kapsamı okunmadı — paketler dosya adından (kimlik ve ad) eşleştirildi; künyeden yalnız id, tekrar ya da gizlenme için okundu",
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


def _git(root, *args, stdin=None, raw=False):
    import subprocess
    if not args or args[0] not in ("rev-parse", "for-each-ref", "ls-tree", "show", "cat-file"):
        raise Incomplete(f"git {args[:1]} is not a read-only command allowed here")
    env = dict(os.environ, GIT_OPTIONAL_LOCKS="0")
    done = subprocess.run(["git", "-C", root, *args], capture_output=True, input=stdin, env=env)
    if done.returncode != 0:
        raise Incomplete(f"git {' '.join(args[:2])} → {done.returncode}")
    return done.stdout if raw else done.stdout.decode("utf-8", errors="replace")


def _shared_reader():
    """CT-FIX-03 (R4): the one künye grammar of the CT tools (scripts/ct_queue.py), loaded only by --discover."""
    import importlib.util
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "scripts", "ct_queue.py")
    if not os.path.isfile(path):
        raise Incomplete("ortak künye okuyucusu (scripts/ct_queue.py) yok — paket kimlikleri ölçülemez")
    spec = importlib.util.spec_from_file_location("ct_queue_shared", path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


_ONE_LINE = None


def _one_line(text):
    """CT-FIX-09 (C8 audits): ct_queue.one_line, loaded once — a file name with a control character stays on one report
    line. Without the shared reader (its absence already makes the discovery incomplete) every character that would
    start a new line is shown as '?', so the report keeps one result line either way."""
    global _ONE_LINE
    if _ONE_LINE is None:
        try:
            _ONE_LINE = _shared_reader().one_line
        except Incomplete:
            _ONE_LINE = lambda t: "".join("?" if unicodedata.category(c) == "Cc" or c in "\u2028\u2029" else c for c in str(t))
    return _ONE_LINE(text)


def pack_id_conflicts(root, blobs):
    """CT-FIX-03 (R4, Codex item 8): {path: problem} for every pack blob whose künye names its id more than once or in
    a disguised form. A pack without a künye (or with an unclosed one) is not a conflict: it is matched by name."""
    if not blobs:
        return {}
    kf = _shared_reader().kunye_fields
    data = _git(root, "cat-file", "--batch", stdin="".join(b + "\n" for b in blobs).encode(), raw=True)
    out, i = {}, 0
    while i < len(data):
        nl = data.index(b"\n", i)
        head = data[i:nl].decode().split()
        if len(head) != 3 or head[1] != "blob":
            raise Incomplete(f"paket blob'u okunamadı ({' '.join(head)[:60]})")
        size = int(head[2])
        body = data[nl + 1:nl + 1 + size].decode("utf-8", errors="replace")
        i = nl + 1 + size + 1
        _, bad = kf(body, ("id",))
        if bad and not bad.startswith(("künye yok", "künye kapanmamış")):
            for path in sorted(blobs[head[0]]):
                out[path] = bad
    return out


def _sha256_bytes(data):
    import hashlib
    return hashlib.sha256(data).hexdigest()


def _blob_id(data):
    """The git blob id of these bytes (what `git hash-object` gives without filters)."""
    import hashlib
    return hashlib.sha1(b"blob %d\0" % len(data) + data).hexdigest()


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
    for i, r in enumerate(routes, start=1):
        if not isinstance(r, dict):
            raise Incomplete(f"gateway rota {i}: nesne değil")
        up = r.get("UpstreamPathTemplate")
        if not isinstance(up, str) or not up.strip("/"):
            raise Incomplete(f"gateway rota {i}: UpstreamPathTemplate yok ya da boş")
        hosts = r.get("DownstreamHostAndPorts")
        if (not isinstance(hosts, list) or not hosts
                or not all(isinstance(h, dict) and isinstance(h.get("Port"), (int, str)) and str(h.get("Port")).strip() for h in hosts)):
            raise Incomplete(f"gateway rota {i} ({up}): DownstreamHostAndPorts / Port yok ya da bozuk")
        segs = [x for x in up.lower().strip("/").split("/") if x and not x.startswith("{") and x not in ROUTE_SKIP]
        if not segs:
            raise Incomplete(f"gateway rota {i} ({up}): kaynak adı çıkarılamadı")
        out.setdefault(segs[0], set()).update(str(h["Port"]).strip() for h in hosts)
    return out


SUMMARY_KEYS = ("status", "approval_status", "owner")
SUMMARY_BUDGET = 3500     # UTF-8 bytes of the whole ÖZET block, under the 4 KB an interface may keep (the header is 16)
SUMMARY_LINE = 420        # bytes of one ÖZET line at most
SUMMARY_TAIL = 520        # bytes kept free for the closing lines (the omission warning and the limit line)


def _cap(text, limit):
    """text cut to at most `limit` UTF-8 bytes, never inside a character; a cut text ends in '…'."""
    data = text.encode("utf-8")
    if len(data) <= limit:
        return text
    return data[:limit - 3].decode("utf-8", errors="ignore") + "…"


def _paths_capped(prefix, paths, limit):
    """prefix + as many 'path (n ref)' items as fit in `limit` bytes, then '+k yol' for the rest — the count of what is
    not shown survives the cut."""
    shown = []
    for i, (p, n) in enumerate(paths):
        item = f"{p} ({n} ref)"
        tail = f" · +{len(paths) - i - 1} yol" if i + 1 < len(paths) else ""
        if len((prefix + " · ".join(shown + [item]) + tail).encode("utf-8")) > limit:
            break
        shown.append(item)
    rest = len(paths) - len(shown)
    return _cap(prefix + " · ".join(shown) + (f" · +{rest} yol" if rest and shown else f"+{rest} yol" if rest else ""), limit)


def _list_capped(prefix, items, limit, unit):
    """CT-FIX-07: prefix + as many items as fit in `limit` bytes, then '+k <unit>' for the rest."""
    shown = []
    for i, item in enumerate(items):
        tail = f" · +{len(items) - i - 1} {unit}" if i + 1 < len(items) else ""
        if len((prefix + " · ".join(shown + [item]) + tail).encode("utf-8")) > limit:
            break
        shown.append(item)
    rest = len(items) - len(shown)
    return _cap(prefix + " · ".join(shown) + (f" · +{rest} {unit}" if rest and shown else f"+{rest} {unit}" if rest else ""), limit)


def domain_config_paths(pack_paths):
    """CT-FIX-07: the domain-config path of each matched pack's own domain folder (execution/domains/<domain>/
    module-packs/…), in order, never guessed."""
    paths = []
    for p in pack_paths:
        parts = p.split("/")
        if len(parts) > 3 and parts[:2] == ["execution", "domains"] and parts[3] == "module-packs":
            dc = f"execution/domains/{parts[2]}/domain-config.md"
            if dc not in paths:
                paths.append(dc)
    return paths


def domain_config_at_head(root, pack_paths):
    """CT-FIX-07: [(domain-config path, True | False | None)] — True when HEAD's tree holds the file as a blob, False
    when git answers that it is missing there (a file only in the working tree or only on another ref is not 'in
    HEAD'), None when the presence could not be measured (git failed or answered otherwise): an unmeasured presence is
    never reported as absent."""
    paths = domain_config_paths(pack_paths)
    if not paths:
        return []
    try:
        out = _git(root, "cat-file", "--batch-check=%(objecttype)", stdin="".join(f"HEAD:{d}\n" for d in paths).encode()).splitlines()
    except (Incomplete, OSError, ValueError):
        return [(d, None) for d in paths]
    if len(out) != len(paths):
        return [(d, None) for d in paths]
    return [(d, True if o.strip() == "blob" else False if o.strip().endswith(" missing") else None) for d, o in zip(paths, out)]


def _some(names, n=2):
    names = sorted(names)
    return ", ".join(names[:n]) + (f", +{len(names) - n}" if len(names) > n else "")


def pack_kunye_values(root, path_blobs):
    """CT-FIX-06 (v7 Employee pilot): {path: [(values or None, [(blob, refs), …])]} — the künye's status,
    approval_status and owner of every distinct blob of each path, read with the shared reader and grouped by value; each
    group keeps its blob ids and their refs (the same values in several blobs is not one current version). Information
    for the summary only, never a gate: a pack without a readable künye is (None, …), not a stop."""
    blobs = sorted({b for per in path_blobs.values() for b in per})
    if not blobs:
        return {}
    kf = _shared_reader().kunye_fields
    data = _git(root, "cat-file", "--batch", stdin="".join(b + "\n" for b in blobs).encode(), raw=True)
    values, i = {}, 0
    while i < len(data):
        nl = data.index(b"\n", i)
        head = data[i:nl].decode().split()
        if len(head) != 3 or head[1] != "blob":
            raise Incomplete(f"paket blob'u okunamadı ({' '.join(head)[:60]})")
        size = int(head[2])
        fields, bad = kf(data[nl + 1:nl + 1 + size].decode("utf-8", errors="replace"), SUMMARY_KEYS)
        values[head[0]] = None if bad else tuple(fields[k] or "-" for k in SUMMARY_KEYS)
        i = nl + 1 + size + 1
    out = {}
    for path, per in path_blobs.items():
        groups = {}
        for blob, refs in per.items():
            groups.setdefault(values.get(blob), []).append((blob, set(refs)))
        out[path] = sorted(((v, sorted(bl, key=lambda b: (-len(b[1]), b[0]))) for v, bl in groups.items()),
                           key=lambda g: (-sum(len(r) for _, r in g[1]), str(g[0])))
    return out


def _kunye_line(path, groups):
    def blobs(bl):
        return "; ".join(f"blob {b[:9]}: {_some(r)}" for b, r in bl[:2]) + (f"; +{len(bl) - 2} blob" if len(bl) > 2 else "")
    def vals(v):
        return "künye okunamadı (bilgi; kapı değil)" if v is None else " · ".join(f"{k} {x}" for k, x in zip(SUMMARY_KEYS, v))
    if len(groups) == 1:
        v, bl = groups[0]
        shown = f"{vals(v)} ({blobs(bl)})"
    else:
        shown = f"belirsiz — {len(groups)} farklı künye: " + " | ".join(f"{vals(v)} ({blobs(bl)})" for v, bl in groups[:2]) \
            + (f" | +{len(groups) - 2} değer" if len(groups) > 2 else "")
    return f"ÖZET paket künyesi: {path} — {shown}"


PRESENCE = {True: "HEAD'de var", False: "HEAD'de yok", None: "HEAD'de ölçülemedi"}


def report_summary(info, findings, matched, meta, alias_ids, failure, blueprint=(), domain_configs=()):
    """CT-FIX-06 (v7 Employee pilot: a collision in a long report was lost when the interface cut it): the 'ÖZET' lines
    under the header, within SUMMARY_BUDGET UTF-8 bytes and in this order — the counts; for every file id on more than
    one matched pack path (the tool's own file-id reading: a parent and its FU are different ids) a possible identity
    collision with those packs' künye values right after it (CT-FIX-08: the open-decision line comes before them); then the other matched packs' künye values; then, in space
    kept for them, the omission warning and the limit line. A deprecated alias is said in its own line and does not
    clear the paths it names. Values that differ between blobs are shown as unresolved with blob and ref ids, never one
    last value. The lines start with 'ÖZET': no finding, no decision, no single-line field of the report contract.
    CT-FIX-07 (v7 C5 pilot: the four other Blueprint candidates, the domain-config and the owner TBD were not used):
    after the collisions, the open decisions (each collision; each matched pack whose owner is TBD, empty or absent —
    information, not an approval), the Blueprint candidates' ids and names, and the matched packs' domain-config paths
    with whether HEAD's tree holds them — HEAD only: the config on a matched pack's own ref stays undecided, and this
    command does not inspect its content (it cannot know what a reader read elsewhere)."""
    kinds = ("blueprint", "sor", "registry", "paket", "gateway", "görev")
    count = {k: sum(f.startswith(f"BULGU {k}:") for f in findings) for k in kinds}
    items = [["ÖZET bulgular: " + " · ".join(f"{k} {count[k]}" for k in kinds)
              + f" · BİLGİ {len(info)} (eski takma ad; çakışma sayılmaz)"]]
    by_id = {}
    for path, refs in matched:
        m = re.match(r"^(MOD-\d+(?:-FU\w+)?)", os.path.basename(path))
        if m:
            by_id.setdefault(m.group(1), []).append((path, refs))
    first = set()
    for mid, paths in sorted(by_id.items()):
        if len(paths) < 2:
            continue
        if mid in alias_ids:
            group = [f"ÖZET BİLGİ: {mid} registry'de eski takma ad — takma adın kendisi çakışma sayılmaz",
                     _paths_capped(f"ÖZET kimlik çokluğu — takma ad ilişkisi belirsiz, inceleme gerekli: {mid} dosya kimliği "
                                   f"{len(paths)} paket yolunda: ", paths, SUMMARY_LINE)]
        else:
            group = [_paths_capped(f"ÖZET olası kimlik çakışması — inceleme gerekli (tek başına kesin mükerrer değil): {mid} "
                                   f"dosya kimliği {len(paths)} paket yolunda: ", paths, SUMMARY_LINE)]
        if meta is not None:
            group += [_kunye_line(p, meta.get(p, [])) for p, _ in paths[:3]]
        first.update(p for p, _ in paths[:3])
        items.append(group)
    decisions = [f"kimlik {mid} — {'takma ad ilişkisi belirsiz' if mid in alias_ids else 'olası çakışma'} ({len(paths)} paket yolu)"
                 for mid, paths in sorted(by_id.items()) if len(paths) > 1]
    for path, _ in matched:
        owners = {g[0][2] for g in (meta or {}).get(path, []) if g[0] is not None}
        unset = sorted(o for o in owners if o.strip() in ("", "-") or "TBD" in o.upper())
        if unset:
            decisions.append(f"sahibi belirsiz: {os.path.basename(path)} (owner {', '.join(repr(o) for o in unset)[:80]})")
    if decisions:   # CT-FIX-08 (v8 audit): first after the counts, so a wide query's collision groups cannot push it out
        items.insert(1, [_list_capped("ÖZET açık kararlar (bilgi; onay değil): ", decisions, SUMMARY_LINE, "karar")])
    if blueprint:
        items.append([_list_capped("ÖZET blueprint adayları: ", [f"{i} '{n[:50]}'" for i, n in blueprint], SUMMARY_LINE, "aday")])
    if domain_configs:
        items.append([_list_capped("ÖZET domain-config (eşleşen paketlerin klasörü; varlık yalnız HEAD ağacında ölçüldü, "
                                   "paketin kendi ref'indeki durum belirsiz; bu keşif komutu içeriğini incelemedi): ",
                                   [f"{d} ({PRESENCE[ok]})" for d, ok in domain_configs],
                                   SUMMARY_LINE, "yol")])
    if meta is None:
        items.append(["ÖZET paket künyeleri: okunamadı — özet eksik (bulgular tam rapordadır)"])
    else:
        items += [[_kunye_line(p, meta.get(p, []))] for p, _ in matched if p not in first]
    out, used, left = [], 0, 0
    for group in items:
        group = [_cap(_one_line(l), SUMMARY_LINE) for l in group]   # CT-FIX-09: shown on one line before the budget
        size = sum(len(l.encode("utf-8")) + 1 for l in group)
        if not left and used + size <= SUMMARY_BUDGET - SUMMARY_TAIL:
            out += group
            used += size
        else:
            left += 1          # an item is shown whole or not at all; after the first that does not fit, none follow
    if left:
        out.append(f"ÖZET EKSİK: {left} madde bayt sınırı ({SUMMARY_BUDGET}) nedeniyle gösterilmedi — tam rapora bakılmalı")
    if failure is not None:
        out.append("ÖZET EKSİK: keşif tamamlanamadı — özet yalnız okunabilen kaynaklardandır")
    out.append("ÖZET sınır: tam rapor {n} satır; bu özet bilgidir, onay ya da karar değildir; rapor bir arayüzde kesilirse "
               "kesilen kısım okunmamıştır ve inceleme kısmidir")
    return out


def discover(root, name, resource=None, sor=None):
    """Return (report lines without the hash line, exit code). CT-FIX-06: the 'ÖZET' lines stand under the header."""
    lines, code = _discover(root, name, resource, sor)
    lines = [_one_line(l) for l in lines]                   # CT-FIX-09: one logical line = one physical line
    total = len(lines) + 1                                  # with the hash line
    lines = [l.replace("{n}", str(total)) if l.startswith("ÖZET sınır: ") else l for l in lines]
    return lines, code


def _discover(root, name, resource=None, sor=None):
    query = _tokens(name)
    lines = ["KEŞİF RAPORU v2", f"sorgu: {name} | kaynak: {resource or '-'} | sor: {sor or '-'}"]
    findings, info, ref_lines = [], [], []
    matched_packs, path_blobs, alias_ids, bp_hits = [], {}, set(), []
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
        reg_file = os.path.join(root, REGISTRY)
        if os.path.exists(reg_file):
            with open(reg_file, encoding="utf-8") as f:
                broken = registry_table_problems(f.read().split("\n"))
            if broken:
                raise Incomplete(f"modül kayıt dosyası {broken[0]} (kısmi satır)"
                                 + (f" (+{len(broken) - 1})" if len(broken) > 1 else "")
                                 + " — okunamayan satır 'eşleşme yok' sayılmaz")

        # Blueprint and SoR_Map
        bp_path = os.path.join(root, BLUEPRINT)
        if not os.path.exists(bp_path):
            raise Incomplete("Blueprint dosyası yok")
        bp_rows, sor_rows = read_blueprint_sheets(bp_path)
        bp_bytes = open(bp_path, "rb").read()
        lines.append(f"kaynak blueprint: {BLUEPRINT} sha256 {_sha256_bytes(bp_bytes)} · blob {_blob_id(bp_bytes)} · "
                     f"Blueprint_Data {len(bp_rows)} satır · SoR_Map {len(sor_rows)} satır")
        for n, r in enumerate(bp_rows, start=2):
            if any(r.values()) and not (r["Module ID"] and r["Module Name"]):
                raise Incomplete(f"Blueprint_Data satır {n}: kimlik ya da ad eksik (kısmi satır)")
        matched_ids = set()
        for r in bp_rows:
            if r["Module ID"] and _similar(query, r["Module Name"]):
                matched_ids.add(r["Module ID"])
                bp_hits.append((r["Module ID"], r["Module Name"]))
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
        if not reg:
            raise Incomplete("modül kayıt dosyasından satır okunamadı")
        reg_bytes = open(reg_path, "rb").read()
        lines.append(f"kaynak registry: {REGISTRY} sha256 {_sha256_bytes(reg_bytes)} · blob {_blob_id(reg_bytes)} · {len(reg)} satır")
        for r in reg:
            if _similar(query, r["name"]) or r["id"] in matched_ids:
                if is_deprecated(r):
                    info.append(f"BİLGİ registry: {r['id']} '{r['name']}' eski takma ad/kullanımdan kalkmış — çakışma sayılmaz")
                    alias_ids.add(r["id"])
                else:
                    findings.append(f"BULGU registry: {r['id']} '{r['name']}' ({r['status']})")

        # module packs on every visible ref
        refs = [x for x in _git(root, "for-each-ref", "--format=%(refname) %(objectname)", "refs/heads", "refs/remotes").splitlines()
                if x and not x.split()[0].endswith("/HEAD")]
        specs = "\n".join(f"{x.split()[0]}:execution/domains" for x in refs) + "\n"
        trees = _git(root, "cat-file", "--batch-check=%(objectname) %(objecttype)", stdin=specs.encode()).splitlines()
        if not refs or len(trees) != len(refs):
            raise Incomplete(f"ref listesi okunamadı ({len(refs)} ref, {len(trees)} yanıt)")
        ref_lines = [f"ref: {x.split()[1]} {x.split()[0]}" for x in sorted(refs)]
        by_tree = {}
        for ref_line, tree_line in zip(refs, trees):
            parts = tree_line.split()
            if len(parts) == 2 and parts[1] == "tree":
                by_tree.setdefault(parts[0], []).append(ref_line.split()[0])
        packs, pack_blobs = {}, {}
        nul_paths = _shared_reader().nul_paths     # loaded once, not per tree (Codex)
        for tree, ref_names in by_tree.items():
            try:   # CT-FIX-08 (v8 audit): '-z' and strict UTF-8 — a quoted path ('ş', '"', '\\') was neither matched nor counted
                entries = nul_paths(_git(root, "ls-tree", "-r", "-z", tree, raw=True), "ls-tree")
            except ValueError as e:
                raise Incomplete(str(e))
            for entry in entries:
                meta, path = entry.split("\t", 1)
                if "/module-packs/" in path and path.endswith(".md") and not path.lower().endswith("readme.md"):
                    packs.setdefault("execution/domains/" + path, set()).update(ref_names)
                    pack_blobs.setdefault(meta.split()[2], set()).add("execution/domains/" + path)
                    path_blobs.setdefault("execution/domains/" + path, {}).setdefault(meta.split()[2], set()).update(ref_names)
        conflicts = pack_id_conflicts(root, pack_blobs)
        if conflicts:
            first = sorted(conflicts)[0]
            raise Incomplete(f"paket künyesinde tekrarlı ya da gizlenmiş kimlik: {first} ({conflicts[first][:90]})"
                             + (f" (+{len(conflicts) - 1})" if len(conflicts) > 1 else "")
                             + " — hangi modülün paketi olduğu belirsiz; son değer seçilmez")
        ref_digest = _sha256_bytes("\n".join(sorted(refs)).encode())
        no_dir = len(refs) - sum(len(v) for v in by_tree.values())
        lines.append(f"kaynak paketler: {len(refs)} ref ({no_dir} ref'te execution/domains yok) · {len(by_tree)} farklı "
                     f"execution/domains ağacı · {len(packs)} paket yolu · ref-listesi-sha256 {ref_digest} "
                     f"(ref adı ve commit SHA'sı satırları; liste raporun sonunda)")
        lines.append(f"kaynak paket-künyeleri: {len(pack_blobs)} farklı paket blob'unun künye id'si ortak okuyucuyla okundu "
                     "(yalnız tekrar ya da gizlenme için; iş kapsamı okunmadı); künyesiz paket dosya adından eşleşir")
        for path, ref_names in sorted(packs.items()):
            stem = os.path.basename(path)[:-3]
            pack_id = re.match(r"^(MOD-\d+(?:-FU\w+)?)", stem)
            if (pack_id and pack_id.group(1) in matched_ids) or _similar(query, stem.replace("-", " ")):
                shown = ", ".join(sorted(ref_names)[:3]) + (f", … +{len(ref_names) - 3}" if len(ref_names) > 3 else "")
                findings.append(f"BULGU paket: {path} — {len(ref_names)} ref'te ({shown})")
                matched_packs.append((path, len(ref_names)))

        # gateway resource -> service
        if main_sha:
            gw_blob = _git(root, "rev-parse", f"{main_sha}:{GATEWAY}").strip()
            gw_text, gw_where = _git(root, "cat-file", "blob", gw_blob), f"origin/main {main_sha[:9]}"
        else:
            gw_raw = open(os.path.join(root, GATEWAY), "rb").read()
            gw_blob, gw_text, gw_where = _blob_id(gw_raw), gw_raw.decode("utf-8"), "çalışma ağacı"
        resources = gateway_resources(gw_text)
        lines.append(f"kaynak gateway: {gw_where}:{GATEWAY} sha256 {_sha256_bytes(gw_text.encode())} · blob {gw_blob} · "
                     f"{len(resources)} kaynak")
        wanted = (resource or "").strip("/").lower()
        for res, ports in sorted(resources.items()):
            hit = (res == wanted) if wanted else (_tokens(res.replace("-", " ")) and _tokens(res.replace("-", " ")) <= query)
            if hit:
                note = " — AYNI KAYNAK BİRDEN ÇOK SERVİSTE" if len(ports) > 1 else ""
                findings.append(f"BULGU gateway: '{res}' → servis port(lar)ı {', '.join(sorted(ports))}{note}")

        # registered Control Tower task sources
        q_reg = os.path.join(root, QUEUE_REGISTRY)
        if not os.path.exists(q_reg):
            lines.append(f"kaynak görev-kaynakları: {QUEUE_REGISTRY} yok — kayıtlı görev kaynağı okunmadı")
        else:
            q_raw = open(q_reg, "rb").read()
            # CT-FIX-03 (R4, V4A-F8): the registry's own bytes are part of what the report read
            lines.append(f"kaynak görev-kaynak-kaydı: {QUEUE_REGISTRY} sha256 {_sha256_bytes(q_raw)} · blob {_blob_id(q_raw)}")
            q_lines = q_raw.decode("utf-8").split("\n")
            header = q_lines[0].split("\t")
            if "path" not in header:
                raise Incomplete("görev kaynağı kayıt dosyasında 'path' sütunu yok")
            for n, row in enumerate((l.split("\t") for l in q_lines[1:] if l.strip()), start=2):
                if len(row) != len(header):
                    raise Incomplete(f"görev kaynağı kayıt dosyası satır {n}: {len(row)} sütun, beklenen {len(header)}")
                qpath = row[header.index("path")]
                full = os.path.join(root, qpath)
                if not os.path.exists(full):
                    raise Incomplete(f"kayıtlı görev kaynağı '{qpath}' yok")
                raw = open(full, "rb").read()
                text = raw.decode("utf-8")
                lines.append(f"kaynak görev-kaynağı: {qpath} sha256 {_sha256_bytes(raw)} · blob {_blob_id(raw)}")
                t_lines = text.replace("\r\n", "\n").split("\n")
                t_head = t_lines[0].split("\t")
                if "id" not in t_head or "state" not in t_head or not ({"item", "module"} & set(t_head)):
                    raise Incomplete(f"görev kaynağı '{qpath}': 'id', 'state' ve 'item' ya da 'module' sütunu gerekli")
                for m, t_row in enumerate((l.split("\t") for l in t_lines[1:] if l.strip()), start=2):
                    if len(t_row) != len(t_head):
                        raise Incomplete(f"görev kaynağı '{qpath}' satır {m}: {len(t_row)} sütun, beklenen {len(t_head)}")
                    cells = dict(zip(t_head, t_row))
                    # CT-FIX-03 (R4, V4A-F4): a row that cannot be matched is "tamamlanamadı", never a silent no-match
                    if not cells["id"].strip() or not cells["state"].strip():
                        raise Incomplete(f"görev kaynağı '{qpath}' satır {m}: id ya da state boş")
                    if not any(cells[c].strip() for c in ("item", "module") if c in cells):
                        raise Incomplete(f"görev kaynağı '{qpath}' satır {m}: item ve module boş — satır eşleştirilemez")
                    if (cells.get("module") and cells["module"] in matched_ids) or _similar(query, cells.get("item", "")[:200]):
                        findings.append(f"BULGU görev: {qpath} {cells.get('id', '?')} [{cells.get('state', '?')}] {cells.get('item', '')[:70]}")
            lines.append(f"kaynak görev-kaynakları: {QUEUE_REGISTRY} · {len([l for l in q_lines[1:] if l.strip()])} kayıtlı kaynak okundu")
    except Incomplete as e:
        failure = str(e)
    except OSError as e:
        failure = type(e).__name__
    except Exception as e:  # anything unexpected is "could not complete", never a crash with an undefined exit code
        failure = f"beklenmeyen hata ({type(e).__name__})"
    else:
        failure = None
    refs_block = [f"İNCELENEN REF'LER: {len(ref_lines)}"] + ref_lines
    try:   # CT-FIX-06: the summary's künye values are information; a reading problem leaves the summary incomplete, not the report
        meta = pack_kunye_values(root, {p: path_blobs.get(p, {}) for p, _ in matched_packs})
    except (Incomplete, OSError, ValueError):
        meta = None
    dcs = domain_config_at_head(root, [p for p, _ in matched_packs])   # CT-FIX-07: information; unmeasured stays None
    lines[1:1] = report_summary(info, findings, matched_packs, meta, alias_ids, failure, bp_hits, dcs)
    lines += info + findings
    if failure is not None:
        lines += GAPS + refs_block + [f"sonuç: tamamlanamadı — {failure}", "çıkış: 2"]
        return lines, 2
    if findings:
        lines += [EXPLAIN] + GAPS + refs_block + ["sonuç: inceleme gerekli — her bulgu için karar ve gerekçe iş paketi kaydına yazılmalı", "çıkış: 3"]
        return lines, 3
    lines += GAPS + refs_block + ["sonuç: incelenen kaynaklarda eşleşme bulunamadı", "çıkış: 0"]
    return lines, 0


def report_hash(lines):
    return _sha256_bytes(("\n".join(lines) + "\n").encode("utf-8"))


REPORT_HASH_LINE = re.compile(r"^rapor-sha256: ([0-9a-f]{64})$")
REPORT_ALTERED = "rapor-sha256 içerikle uyuşmuyor"


def parse_report(data):
    """CT-FIX-03 (R4, V4B-B6): a saved report read byte for byte, as the tool prints it — UTF-8, LF only, exactly one
    final newline after the hash line, the hash line exactly 'rapor-sha256: <64 hex>'. Returns (body lines, recorded
    hash, None), or (body or None, hash or None, problem). Shared by --check-report and the prompt gate."""
    try:
        text = data.decode("utf-8")
    except UnicodeDecodeError:
        return None, None, "UTF-8 değil"
    if "\r" in text:
        return None, None, "satır sonunda CR var — üreticinin yazdığı LF baytları değil"
    if not text.endswith("\n") or text.endswith("\n\n"):
        return None, None, "tek bir son satır sonuyla bitmiyor"
    lines = text[:-1].split("\n")
    m = REPORT_HASH_LINE.match(lines[-1])
    if not m:
        return None, None, "son satır tam olarak 'rapor-sha256: <64 hex>' değil"
    body = lines[:-1]
    if report_hash(body) != m.group(1):
        return body, m.group(1), REPORT_ALTERED
    return body, m.group(1), None


def check_report(path):
    try:
        data = open(path, "rb").read()
    except OSError as e:
        print(f"RAPOR OKUNAMADI: {type(e).__name__}")
        return 2
    body, _, problem = parse_report(data)
    if problem == REPORT_ALTERED:
        print(f"RAPOR DEĞİŞTİRİLMİŞ: {REPORT_ALTERED}")
        return 2
    if problem:
        print(f"RAPOR BOZUK: {problem}")
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
