#!/usr/bin/env python3
"""FLOW-01 (owner 'Evet, ölçüm' 2026-11-05) — the development flow measured every week: speed and waiting, the two
criteria that carry 45 % of the independent score and had no data. Read-only; standard library, git and `gh`.

    python3 scripts/flow_metrics.py [--weeks 4] [--until YYYY-MM-DD] [--ref origin/main] [--json]

What it measures, per calendar week (Monday–Sunday, UTC):
  Pull requests (GitHub GraphQL, merged in the week; every page of a PR's files and approvals is read)
    - count; open → merge median and p90 (hours); open → first approval median; first approval → merge median
    - kind by the files changed: product (services/, frontend/, gateway/, tests/), tool (scripts/, .github/,
      .antigravity/scripts/), process (everything else: records, packs, queues, rules)
    - share approved by the most frequent approver account
  Main check (phase1-gates runs, GitHub REST, read week by week so no answer is capped): runs, red share, median duration
  Work-package waiting (every queue in execution/delivery/ct-queue-registry.tsv, its earlier file names too): for each
    state, how long work packages stayed in it before they moved on, and the ones still in it at the cut-off with their
    age. A state starts at the commit that CHANGED that row against its own parent — a tower-branch commit included; a
    merge only where its result differs from every parent; nothing after the cut-off is applied.

WHAT IS PROTECTED (threat model, written before the review): the tool never shows a number without the data behind it
and never a number that differs from what happened. (a) no data → "veri yok" / null, never 0; (b) every source is read
in full for the window — an answer that could be capped or a list that could be cut is verified or split, so "veri yok"
means there was nothing; (c) GitHub, gh or git that cannot be read stops the run (exit 2), never a partial table;
(d) every number says what it counts; (e) how a queue stay is timed and the UTC cut-off are written in both outputs.
"""

from __future__ import annotations

import argparse
import datetime as dt
import importlib.util
import json
import os
import statistics
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REPO = ("bestepullukcu-alt", "ERP-vNext")
REGISTRY = "execution/delivery/ct-queue-registry.tsv"
STATES = "execution/delivery/ct-queue-states.tsv"
PRODUCT = ("services/", "frontend/", "gateway/", "tests/")
TOOL = ("scripts/", ".github/", ".antigravity/scripts/")
NO_DATA = "veri yok"
UNDEFINED = "(tanımsız durum)"
REST_CAP = 1000                      # GitHub answers a filtered run list with at most this many runs
BASIS = {"week": "pazartesi–pazar, UTC",
         "queue_time": "bir durum, o satırı kendi ebeveynine göre değiştiren commit'in zamanında başlar (kulenin dalındaki "
                       "commit dahil; merge yalnız sonucu her ebeveynden farklıysa); kesim anından sonraki hiçbir değişiklik "
                       "uygulanmaz; ana dala ulaşma beklemesi PR tablosundadır"}


class Unreadable(Exception):
    """GitHub, gh or git cannot be read: exit 2, never a partial table."""


_CQ = None


def queue_reader():
    """scripts/ct_queue.py — its Table is the one reader of a queue file."""
    global _CQ
    if _CQ is None:
        spec = importlib.util.spec_from_file_location("ct_queue_flow", ROOT / "scripts" / "ct_queue.py")
        _CQ = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(_CQ)
    return _CQ


def run(args: list[str], what: str) -> bytes:
    try:
        done = subprocess.run(args, capture_output=True)
    except OSError as e:                                          # gh or git missing (Codex: FileNotFoundError escaped)
        raise Unreadable(f"{what}: {args[0]} çalıştırılamadı ({type(e).__name__})")
    if done.returncode != 0:
        raise Unreadable(f"{what}: {done.stderr.decode(errors='replace').strip()[:160]}")
    return done.stdout


def gh_json(args: list[str], what: str):
    try:
        return json.loads(run(["gh", *args], what))
    except ValueError:
        raise Unreadable(f"{what}: cevap JSON değil")


def ts(s: str) -> dt.datetime:
    return dt.datetime.fromisoformat(s.replace("Z", "+00:00")).astimezone(dt.timezone.utc)


def hours(a: dt.datetime, b: dt.datetime) -> float:
    return (b - a).total_seconds() / 3600


def p90(xs: list[float]) -> float:
    xs = sorted(xs)
    return xs[max(0, -(-len(xs) * 9 // 10) - 1)]                  # nearest rank


def med(xs: list[float]) -> float | None:
    return statistics.median(xs) if xs else None


def fmt(x, unit: str = "sa") -> str:
    if x is None:
        return NO_DATA
    return f"{x:.1f} {unit}" if unit else str(x)


def weeks_ending(until: dt.date, n: int) -> list[tuple[dt.date, dt.date]]:
    """The last n calendar weeks (Monday start, Sunday end) up to the week holding `until`, oldest first."""
    monday = until - dt.timedelta(days=until.weekday())
    return [(monday - dt.timedelta(weeks=k), monday - dt.timedelta(weeks=k) + dt.timedelta(days=6)) for k in range(n)][::-1]


def in_week(t: dt.datetime, week: tuple[dt.date, dt.date]) -> bool:
    return week[0] <= t.date() <= week[1]


# ------------------------------------------------------------------------------------------------ pull requests
def kind_of(paths: list[str]) -> str:
    if any(p.startswith(PRODUCT) and "/audit-ledger/" not in p for p in paths):
        return "ürün"
    if any(p.startswith(TOOL) for p in paths):
        return "araç"
    return "süreç"


def pr_stats(prs: list[dict], week: tuple[dt.date, dt.date]) -> dict:
    """One week's pull-request numbers; with no merge in the week every field is None ('veri yok'), counts too."""
    w = [p for p in prs if in_week(ts(p["mergedAt"]), week)]
    if not w:
        return dict.fromkeys(("prs", "lead_median_h", "lead_p90_h", "first_approval_median_h",
                              "approval_to_merge_median_h", "product", "tool", "process", "approved", "unapproved",
                              "top_approver_share"))
    lead = [hours(ts(p["createdAt"]), ts(p["mergedAt"])) for p in w]
    first, after, approvers = [], [], {}
    for p in w:
        times = [ts(r["submittedAt"]) for r in p["reviews"] if r.get("submittedAt")]
        if times:
            f = min(times)
            first.append(hours(ts(p["createdAt"]), f))
            after.append(hours(f, ts(p["mergedAt"])))
        for who in {r.get("author") for r in p["reviews"] if r.get("author")}:
            approvers[who] = approvers.get(who, 0) + 1
    kinds = [kind_of(p["files"]) for p in w]
    approved = sum(1 for p in w if p["reviews"])
    top = max(approvers.items(), key=lambda kv: kv[1]) if approvers else None
    return {"prs": len(w), "lead_median_h": med(lead), "lead_p90_h": p90(lead),
            "first_approval_median_h": med(first), "approval_to_merge_median_h": med(after),
            "product": kinds.count("ürün"), "tool": kinds.count("araç"), "process": kinds.count("süreç"),
            "approved": approved, "unapproved": len(w) - approved,
            "top_approver_share": (top[1] / approved) if top and approved else None}


PRS_QUERY = """query($cursor:String){ repository(owner:"%s", name:"%s"){
 pullRequests(states:MERGED, first:20, after:$cursor, orderBy:{field:UPDATED_AT, direction:DESC}){
  pageInfo{hasNextPage endCursor}
  nodes{ number createdAt mergedAt updatedAt
   reviews(first:50, states:[APPROVED]){pageInfo{hasNextPage endCursor} nodes{submittedAt author{login}}}
   files(first:100){pageInfo{hasNextPage endCursor} nodes{path}} } } } }""" % REPO
MORE_QUERY = """query($n:Int!, $cursor:String){ repository(owner:"%s", name:"%s"){ pullRequest(number:$n){
 %%s } } }""" % REPO
MORE = {"files": "files(first:100, after:$cursor){pageInfo{hasNextPage endCursor} nodes{path}}",
        "reviews": "reviews(first:50, after:$cursor, states:[APPROVED]){pageInfo{hasNextPage endCursor} "
                   "nodes{submittedAt author{login}}}"}


def _graphql(query: str, **vars_) -> dict:
    args = ["api", "graphql", "-f", f"query={query}"]
    for k, v in vars_.items():
        args += ["-F" if isinstance(v, int) else "-f", f"{k}={v}"]
    got = gh_json(args, "GitHub GraphQL")
    if not isinstance(got, dict) or got.get("errors") or "data" not in got:
        raise Unreadable(f"GitHub GraphQL hata döndü: {str(got.get('errors') if isinstance(got, dict) else got)[:160]}")
    return got["data"]


def _rest_of(number: int, field: str, first: dict) -> tuple[list[dict], int]:
    """Every node of a PR's `field` connection: the first page given, the rest asked (Codex: a 101st file and a 31st
    approval were not read)."""
    nodes, info, spent = list(first["nodes"]), first["pageInfo"], 0
    while info["hasNextPage"]:
        conn = _graphql(MORE_QUERY % MORE[field], n=number, cursor=info["endCursor"])["repository"]["pullRequest"][field]
        nodes += conn["nodes"]
        info = conn["pageInfo"]
        spent += 1
    return nodes, spent


def fetch_prs(since: dt.date) -> tuple[list[dict], int]:
    """Merged pull requests updated since `since` (a merge after it implies an update after it), and the GraphQL pages
    spent. Pages are newest-updated first; the walk stops on the first page entirely older than `since`."""
    out, cursor, spent = [], None, 0
    while True:
        try:
            page = _graphql(PRS_QUERY, **({"cursor": cursor} if cursor else {}))["repository"]["pullRequests"]
            nodes = page["nodes"]
            spent += 1
            for n in nodes:
                if n.get("mergedAt") and ts(n["mergedAt"]).date() >= since:
                    files, s1 = _rest_of(n["number"], "files", n["files"])
                    reviews, s2 = _rest_of(n["number"], "reviews", n["reviews"])
                    spent += s1 + s2
                    out.append({"number": n["number"], "createdAt": n["createdAt"], "mergedAt": n["mergedAt"],
                                "reviews": [{"submittedAt": r.get("submittedAt"), "author": (r.get("author") or {}).get("login")}
                                            for r in reviews],
                                "files": [f["path"] for f in files]})
            more = page["pageInfo"]["hasNextPage"]
        except (KeyError, TypeError) as e:
            raise Unreadable(f"GitHub GraphQL cevabı bozuk ({type(e).__name__})")
        if not more or all(ts(n["updatedAt"]).date() < since for n in nodes):
            break
        cursor = page["pageInfo"]["endCursor"]
    return out, spent


# ------------------------------------------------------------------------------------------------ main check
def _runs_between(a: dt.date, b: dt.date) -> tuple[list[dict], int]:
    """Every phase1-gates run created in [a, b] (dates, inclusive), all pages, and the REST pages spent. GitHub caps a
    filtered list at REST_CAP runs (Codex): a range above the cap is split in two; one day above it stops the run. The
    count read must equal GitHub's total_count."""
    base = f"repos/{'/'.join(REPO)}/actions/workflows/phase1-gates.yml/runs?created={a.isoformat()}..{b.isoformat()}&per_page=100"
    first = gh_json(["api", base + "&page=1"], "phase1 koşuları")
    total, spent = first.get("total_count"), 1
    if not isinstance(total, int):
        raise Unreadable("phase1 koşu cevabında total_count yok")
    if total > REST_CAP:
        if a == b:
            raise Unreadable(f"phase1: {a} günü {total} koşu — GitHub {REST_CAP}'den fazlasını vermez")
        mid = a + (b - a) // 2
        left, s1 = _runs_between(a, mid)
        right, s2 = _runs_between(mid + dt.timedelta(days=1), b)
        return left + right, spent + s1 + s2
    runs, page = list(first.get("workflow_runs") or []), 1
    while len(runs) < total:
        page += 1
        got = gh_json(["api", base + f"&page={page}"], "phase1 koşuları").get("workflow_runs") or []
        spent += 1
        if not got:
            break
        runs += got
    if len(runs) != total:
        raise Unreadable(f"phase1 {a}…{b}: {len(runs)} koşu okundu, GitHub {total} diyor — eksik okuma")
    return [{"createdAt": r["created_at"], "updatedAt": r["updated_at"], "conclusion": r.get("conclusion"),
             "status": r.get("status")} for r in runs], spent


def fetch_runs(weeks: list[tuple[dt.date, dt.date]]) -> tuple[list[dict], int]:
    runs, spent = [], 0
    for a, b in weeks:                                            # week by week: no single answer reaches the cap
        got, s = _runs_between(a, b)
        runs += got
        spent += s
    return runs, spent


def run_stats(runs: list[dict], week: tuple[dt.date, dt.date]) -> dict:
    w = [r for r in runs if r.get("status") == "completed" and r.get("conclusion") in ("success", "failure")
         and in_week(ts(r["createdAt"]), week)]
    if not w:
        return {"runs": None, "red": None, "duration_median_min": None}
    return {"runs": len(w), "red": sum(r["conclusion"] == "failure" for r in w),
            "duration_median_min": med([hours(ts(r["createdAt"]), ts(r["updatedAt"])) * 60 for r in w])}


# ------------------------------------------------------------------------------------------------ queue waiting
def git(root: str, *args: str) -> str:
    return run(["git", "-C", root, *args], f"git {' '.join(args[:2])}").decode("utf-8", errors="replace")


class Repo:
    """One root and ref: real parents (path limiting rewrites them, so they come from rev-list) and queue versions read
    once. A version is None only when NO name of the queue is in that commit's tree; a name that is there but cannot be
    shown stops the run (Codex: an error was taken for a deletion)."""

    def __init__(self, root: str, ref: str):
        self.root, self.ref, self._v = root, ref, {}
        self.parents = {}
        for line in git(root, "rev-list", "--parents", ref).splitlines():
            ids = line.split()
            self.parents[ids[0]] = ids[1:]

    def version(self, sha: str, names: list[str]) -> dict[str, str] | None:
        key = (sha, tuple(names))
        if key not in self._v:
            present = set(git(self.root, "ls-tree", "-r", "--name-only", sha, "--", *names).split("\n"))
            name = next((n for n in names if n in present), None)
            if name is None:
                self._v[key] = None
            else:
                t = queue_reader().Table(git(self.root, "show", f"{sha}:{name}"))
                i, s = t.index("id"), t.index("state")
                if i is None or s is None:
                    raise Unreadable(f"{name}@{sha[:12]}: id / state sütunu yok")
                self._v[key] = {r[i]: r[s] for r in t.rows if len(r) > max(i, s) and r[i]}
        return self._v[key]


def queue_names(repo: Repo, path: str) -> list[str]:
    """The queue's file names, current first, then the earlier ones it was moved from (Codex: a move reset the starts)."""
    names = [path]
    out = git(repo.root, "log", "--follow", "--name-only", "--format=tformat:@@", repo.ref, "--", path)
    for line in out.splitlines():
        if line and line != "@@" and line not in names:
            names.append(line)
    return names


def queue_events(repo: Repo, path: str) -> list[tuple[dt.datetime, str, str | None]]:
    """(time, work package, new state | None = removed) for every row a commit really changed against its own parent;
    a merge only for rows whose result differs from EVERY parent (Codex: whole snapshots in date order invented moves
    when two branches changed different rows)."""
    names = queue_names(repo, path)
    out = []
    for line in git(repo.root, "log", "--full-history", "--format=%H %cI", repo.ref, "--", *names).splitlines():
        sha, when = line.split(" ", 1)
        now = repo.version(sha, names) or {}
        before = [repo.version(p, names) or {} for p in repo.parents.get(sha, [])] or [{}]
        for wp in set(now).union(*before):
            new = now.get(wp)
            if all(b.get(wp) != new for b in before):
                out.append((ts(when), wp, new))
    return out


def stays_from(events: list[tuple[dt.datetime, str, str, str | None]], end: dt.datetime) -> tuple[list[tuple], list[tuple]]:
    """(closed stays, open stays): (work package, queue, state, entered, left | None). events: (time, queue, work
    package, state | None). Nothing after `end` is applied (Codex: a later change erased an earlier week's waiting). A
    work package is followed across queues (one queue was split from another's file): it leaves a state only when no
    queue holds it in that state any more."""
    held: dict[str, dict[str, tuple[str, dt.datetime]]] = {}       # wp -> queue -> (state, since)
    current: dict[str, tuple[str, str, dt.datetime]] = {}          # wp -> (queue, state, entered)
    closed = []
    for when, queue, wp, state in sorted((e for e in events if e[0] <= end), key=lambda e: (e[0], e[3] is None)):
        h = held.setdefault(wp, {})
        if state is None:
            h.pop(queue, None)
        else:
            h[queue] = (state, when)
        now = max(h.items(), key=lambda kv: kv[1][1]) if h else None   # the most recent state any queue holds
        cur = current.get(wp)
        if now is None:
            if cur:
                closed.append((wp, cur[0], cur[1], cur[2], when))
                del current[wp]
        elif cur is None or cur[1] != now[1][0]:
            if cur:
                closed.append((wp, cur[0], cur[1], cur[2], when))
            current[wp] = (now[0], now[1][0], when)
    return closed, [(wp, q, s, t, None) for wp, (q, s, t) in current.items()]


def queue_stats(root: str, ref: str, since: dt.datetime, end: dt.datetime) -> dict:
    """Per state: stays that ENDED in [since, end] with their median length, and stays still open at `end` with their
    median age. A state outside the queue's own vocabulary (STATES, by the registry's vocabulary column) is one bucket,
    UNDEFINED — free text in a state cell is not a state; a 'closed'-class state waits for nothing."""
    repo = Repo(root, ref)
    table = queue_reader().Table
    t = table(git(root, "show", f"{ref}:{REGISTRY}"))
    qi, pi, vi = t.index("queue_id"), t.index("path"), t.index("vocabulary")
    if qi is None or pi is None:
        raise Unreadable(f"{REGISTRY}: queue_id / path sütunu yok")
    vocab: dict[str, dict[str, str]] = {}
    vt = table(git(root, "show", f"{ref}:{STATES}"))
    a_, b_, c_ = vt.index("queue_id"), vt.index("state"), vt.index("class")
    for r in vt.rows:
        if a_ is not None and b_ is not None and len(r) > max(a_, b_):
            vocab.setdefault(r[a_], {})[r[b_]] = r[c_] if c_ is not None and len(r) > c_ else ""
    events, known_of, queues = [], {}, 0
    for row in t.rows:
        if len(row) <= max(qi, pi) or not row[pi]:
            continue
        queues += 1
        known_of[row[qi]] = vocab.get(row[vi]) if vi is not None and len(row) > vi else None
        events += [(when, row[qi], wp, state) for when, wp, state in queue_events(repo, row[pi])]
    closed, still = stays_from(events, end)
    name = lambda q, s: s if not known_of.get(q) or s in known_of[q] else UNDEFINED
    per_state: dict[str, list[float]] = {}
    open_now: dict[str, list[float]] = {}
    closed_states: set[str] = set()
    for _wp, q, state, a, b in closed:
        if since <= b <= end:                                     # the stay ended inside the measured window
            per_state.setdefault(name(q, state), []).append(hours(a, b))
    for _wp, q, state, a, _b in still:
        if (known_of.get(q) or {}).get(state) == "closed":
            closed_states.add(state)
            continue
        open_now.setdefault(name(q, state), []).append(hours(a, end))
    return {"queues": queues, "closed_states": sorted(closed_states), "cutoff_utc": end.isoformat(),
            "states": {s: {"left": len(v), "median_h": med(v)} for s, v in sorted(per_state.items())},
            "open": {s: {"count": len(v), "age_median_h": med(v)} for s, v in sorted(open_now.items())}}


# ------------------------------------------------------------------------------------------------ report
def render(weeks: list, pr: list[dict], ci: list[dict], q: dict, cost: str, window: tuple) -> list[str]:
    n = lambda x: NO_DATA if x is None else str(x)
    L = [f"FLOW-01 — {window[0]} … {window[1]} (hafta: {BASIS['week']}) · GitHub maliyeti: {cost}", "",
         "PR'lar (haftada birleşenler)",
         "hafta | PR | açılış→birleşme medyan / p90 | açılış→ilk onay medyan | onay→birleşme medyan | ürün/araç/süreç"
         " | onaysız | en sık onaylayan payı"]
    for (a, b), s in zip(weeks, pr):
        share = NO_DATA if s["top_approver_share"] is None else f"%{s['top_approver_share'] * 100:.0f}"
        kinds = NO_DATA if s["prs"] is None else f"{s['product']}/{s['tool']}/{s['process']}"
        L.append(f"{a:%d.%m}–{b:%d.%m} | {n(s['prs'])} | {fmt(s['lead_median_h'])} / {fmt(s['lead_p90_h'])} | "
                 f"{fmt(s['first_approval_median_h'])} | {fmt(s['approval_to_merge_median_h'])} | {kinds} | "
                 f"{n(s['unapproved'])} | {share}")
    L += ["", "Ana kontrol (phase1)", "hafta | koşu | kırmızı | süre medyan"]
    for (a, b), s in zip(weeks, ci):
        red = NO_DATA if s["runs"] is None else f"{s['red']}/{s['runs']}"
        L.append(f"{a:%d.%m}–{b:%d.%m} | {n(s['runs'])} | {red} | {fmt(s['duration_median_min'], 'dk')}")
    L += ["", f"İş paketi beklemeleri ({q['queues']} kuyruk; kesim {q['cutoff_utc']}) — {BASIS['queue_time']}",
          "durum | pencerede çıkılan | o durumda kalış medyan"]
    L += [f"{s} | {v['left']} | {fmt(v['median_h'])}" for s, v in q["states"].items()] or [f"— | {NO_DATA} | {NO_DATA}"]
    L += ["", "Kesim anında bekleyenler (kuyruğun durum listesinde 'closed' sınıfındaki "
              f"{', '.join(q.get('closed_states') or []) or 'durumlar'} sayılmaz)", "durum | iş paketi | bekleme yaşı medyan"]
    L += [f"{s} | {v['count']} | {fmt(v['age_median_h'])}" for s, v in q["open"].items()] or [f"— | {NO_DATA} | {NO_DATA}"]
    return L


def main(argv: list[str]) -> int:
    if "--self-test" in argv:
        return self_test()
    ap = argparse.ArgumentParser(prog="flow_metrics.py", description="FLOW-01 — haftalık hız ve bekleme ölçümü (salt okunur)")
    ap.add_argument("--weeks", type=int, default=4)
    ap.add_argument("--until", help="YYYY-MM-DD; verilmezse bugün (UTC)")
    ap.add_argument("--ref", default="origin/main")
    ap.add_argument("--json", action="store_true")
    a = ap.parse_args(argv[1:])
    now = dt.datetime.now(dt.timezone.utc)
    until = dt.date.fromisoformat(a.until) if a.until else now.date()
    weeks = weeks_ending(until, max(1, a.weeks))
    start = dt.datetime.combine(weeks[0][0], dt.time(), dt.timezone.utc)
    end = min(dt.datetime.combine(weeks[-1][1], dt.time(23, 59, 59), dt.timezone.utc), now)
    try:
        prs, points = fetch_prs(weeks[0][0])
        runs, pages = fetch_runs(weeks)
        pr = [pr_stats(prs, w) for w in weeks]
        ci = [run_stats(runs, w) for w in weeks]
        q = queue_stats(str(ROOT), a.ref, start, end)
    except Unreadable as e:
        print(f"sonuç: tamamlanamadı — {e}\nçıkış: 2")
        return 2
    cost = f"{points} GraphQL sayfası, {pages} REST sayfası"
    if a.json:
        print(json.dumps({"basis": {**BASIS, "cutoff_utc": end.isoformat(), "ref": a.ref},
                          "weeks": [[str(x), str(y)] for x, y in weeks], "prs": pr, "ci": ci, "queues": q, "cost": cost},
                         ensure_ascii=False, indent=1))
    else:
        print("\n".join(render(weeks, pr, ci, q, cost, (weeks[0][0], weeks[-1][1]))))
    return 0


# ------------------------------------------------------------------------------------------------ self-test
def self_test() -> int:
    cases: list[tuple[str, bool]] = []

    def attempt(fn, *args):
        """A raise is a result here: a broken guard turns its own case red instead of stopping the self-test."""
        try:
            return fn(*args)
        except Exception as e:          # noqa: BLE001
            return e
    week = (dt.date(2026, 11, 2), dt.date(2026, 11, 8))
    pr = lambda n, c, m, revs, files: {"number": n, "createdAt": c, "mergedAt": m, "files": files,
                                       "reviews": [{"submittedAt": t, "author": a} for t, a in revs]}
    prs = [pr(1, "2026-11-02T10:00:00Z", "2026-11-02T12:00:00Z", [("2026-11-02T11:00:00Z", "beste")], ["services/x.cs"]),
           pr(2, "2026-11-03T10:00:00Z", "2026-11-03T11:00:00Z", [("2026-11-03T10:30:00Z", "ali")], ["docs/records/a.md"]),
           pr(3, "2026-11-04T10:00:00Z", "2026-11-05T10:00:00Z", [], ["scripts/t.py"]),
           pr(4, "2026-10-30T10:00:00Z", "2026-11-01T10:00:00Z", [("2026-10-30T11:00:00Z", "beste")], ["services/y.cs"])]
    s = pr_stats(prs, week)
    cases.append(("PR: only the week's merges count (a merge on Sunday 1 Nov belongs to the week before)", s["prs"] == 3))
    cases.append(("PR: lead median of 2 h, 1 h, 24 h is 2 h; p90 by nearest rank is 24 h",
                  s["lead_median_h"] == 2 and s["lead_p90_h"] == 24))
    cases.append(("PR: first approval and approval→merge from approved PRs only; the unapproved one is counted",
                  s["first_approval_median_h"] == 0.75 and s["approval_to_merge_median_h"] == 0.75 and s["unapproved"] == 1))
    cases.append(("PR: kind by files — product / tool / process", (s["product"], s["tool"], s["process"]) == (1, 1, 1)))
    sunday = pr(9, "2026-11-08T20:00:00Z", "2026-11-08T23:30:00Z", [], ["services/z.cs"])
    cases.append(("sabotage PR: a merge late on the week's Sunday (UTC) belongs to that week",
                  pr_stats([sunday], week)["prs"] == 1))
    cases.append(("PR: top approver share over approved PRs (1 of 2)", s["top_approver_share"] == 0.5))
    empty = attempt(pr_stats, prs, (dt.date(2026, 10, 19), dt.date(2026, 10, 25)))
    cases.append(("sabotage PR (Codex): a week without merges is 'veri yok' in EVERY field, counts too, never 0",
                  isinstance(empty, dict) and all(v is None for v in empty.values())))
    empty = empty if isinstance(empty, dict) else dict.fromkeys(s)
    runs = [{"createdAt": "2026-11-03T10:00:00Z", "updatedAt": "2026-11-03T10:10:00Z", "conclusion": "failure", "status": "completed"},
            {"createdAt": "2026-11-04T10:00:00Z", "updatedAt": "2026-11-04T10:12:00Z", "conclusion": "success", "status": "completed"},
            {"createdAt": "2026-11-04T11:00:00Z", "updatedAt": "2026-11-04T11:01:00Z", "conclusion": "cancelled", "status": "completed"},
            {"createdAt": "2026-11-05T10:00:00Z", "updatedAt": "2026-11-05T10:05:00Z", "conclusion": None, "status": "in_progress"}]
    r = run_stats(runs, week)
    cases.append(("CI: completed success/failure runs only (cancelled and running ones are no measure); median 11 min",
                  r["runs"] == 2 and r["red"] == 1 and r["duration_median_min"] == 11))
    cases.append(("sabotage CI: a week without runs is 'veri yok' in every field",
                  all(v is None for v in run_stats(runs, (dt.date(2026, 10, 1), dt.date(2026, 10, 7))).values())))
    lines = render([week], [empty], [run_stats([], week)], {"queues": 1, "states": {}, "open": {}, "cutoff_utc": "x"},
                   "test", week)
    cases.append(("sabotage render (Codex): no data anywhere reads 'veri yok' in every cell, never a 0",
                  any(l.startswith(f"{week[0]:%d.%m}") and l.count(NO_DATA) == 8 for l in lines)
                  and not any(" 0 " in l or "0/0/0" in l or " 0.0 " in l for l in lines)))
    with tempfile.TemporaryDirectory() as td:
        env = {**os.environ, "GIT_AUTHOR_NAME": "t", "GIT_AUTHOR_EMAIL": "t@x", "GIT_COMMITTER_NAME": "t",
               "GIT_COMMITTER_EMAIL": "t@x"}
        g = lambda *a, when=None: subprocess.run(["git", "-C", td, *a], capture_output=True, check=True,
                                                 env={**env, **({"GIT_COMMITTER_DATE": when, "GIT_AUTHOR_DATE": when} if when else {})})
        g("init", "-q", "-b", "main")
        q, q_old = "execution/q/CT-QUEUE.tsv", "execution/CT-QUEUE.tsv"
        Path(td, "execution/delivery").mkdir(parents=True)
        Path(td, "execution/q").mkdir(parents=True)
        Path(td, REGISTRY).write_text("queue_id\tpath\tvocabulary\nq1\t" + q + "\tv1\n", encoding="utf-8")
        Path(td, STATES).write_text(
            "queue_id\tstate\tclass\n" + "".join(f"v1\t{s}\t{c}\n" for s, c in (
                ("PLANNED", "planned"), ("READY", "dispatchable"), ("DISPATCHED", "active"), ("ACCEPTED", "closed"))),
            encoding="utf-8")

        def version(rows: list[tuple[str, str]], when: str, path: str = q) -> None:
            Path(td, path).write_text("id\titem\tstate\n" + "".join(f"{i}\tx\t{s}\n" for i, s in rows), encoding="utf-8")
            g("add", "-A")
            g("commit", "-q", "-m", when, when=when)
        version([("WP-1", "PLANNED")], "2026-11-02T08:00:00+00:00", q_old)                    # under its earlier name
        g("mv", q_old, q)
        g("commit", "-q", "-m", "move", when="2026-11-02T09:00:00+00:00")                      # moved, same content
        version([("WP-1", "READY"), ("WP-2", "PLANNED")], "2026-11-02T10:00:00+00:00")
        version([("WP-1", "DISPATCHED"), ("WP-2", "PLANNED")], "2026-11-02T16:00:00+00:00")
        g("checkout", "-q", "-b", "a")
        version([("WP-1", "ACCEPTED"), ("WP-2", "PLANNED"), ("WP-3", "Bekliyor — notlar burada")],
                "2026-11-03T10:00:00+00:00")                                                   # tower branch a
        g("checkout", "-q", "main")
        g("checkout", "-q", "-b", "b")
        version([("WP-1", "DISPATCHED"), ("WP-2", "READY")], "2026-11-03T12:00:00+00:00")      # sibling branch b
        g("checkout", "-q", "main")
        g("merge", "-q", "--no-ff", "-m", "merge a", "a", when="2026-11-04T10:00:00+00:00")
        try:                                                    # neighbouring rows: git asks for a resolution
            g("merge", "-q", "--no-ff", "-m", "merge b", "b", when="2026-11-04T11:00:00+00:00")
        except subprocess.CalledProcessError:
            Path(td, q).write_text("id\titem\tstate\nWP-1\tx\tACCEPTED\nWP-2\tx\tREADY\nWP-3\tx\tBekliyor — notlar burada\n",
                                   encoding="utf-8")            # both branches' changes, nothing new
            g("add", "-A")
            g("commit", "-q", "-m", "merge b", when="2026-11-04T11:00:00+00:00")
        version([("WP-1", "ACCEPTED"), ("WP-2", "READY"), ("WP-3", "Bekliyor — notlar burada"), ("WP-4", "PLANNED")],
                "2026-11-06T10:00:00+00:00")                                                   # after the cut-off
        end = dt.datetime(2026, 11, 5, 10, tzinfo=dt.timezone.utc)
        qs = queue_stats(td, "main", dt.datetime(2026, 11, 2, tzinfo=dt.timezone.utc), end)
        st, op = qs["states"], qs["open"]
        cases.append(("sabotage queue (Codex): a move with the same content keeps the start — PLANNED stays WP-1 2 h (from "
                      "the commit under the earlier name, not 1 h from the move) and WP-2 26 h: median 14 h",
                      st.get("PLANNED") == {"left": 2, "median_h": 14}))
        cases.append(("queue: READY 6 h; DISPATCHED left once, after 18 h, on tower branch a's own commit",
                      st.get("READY", {}).get("median_h") == 6 and st.get("DISPATCHED") == {"left": 1, "median_h": 18}))
        cases.append(("sabotage queue (Codex): two sibling branches changing different rows invent no move — WP-1 stays "
                      "ACCEPTED, WP-2 READY since branch b's commit (22 h), no second DISPATCHED stay",
                      op.get("READY") == {"count": 1, "age_median_h": 46} and "DISPATCHED" not in op
                      and "ACCEPTED" not in op and qs["closed_states"] == ["ACCEPTED"]))
        cases.append(("sabotage queue (Codex): nothing after the cut-off is applied (WP-4 of 6 Nov is not there)",
                      sum(v["count"] for v in op.values()) == 2))
        cases.append(("sabotage queue: free text in a state cell is not a state — one '(tanımsız durum)' bucket",
                      op.get(UNDEFINED, {}).get("count") == 1 and not any("notlar" in k for k in op)))
        g("checkout", "-q", "-b", "c", "main~1")
        version([("WP-1", "ACCEPTED"), ("WP-2", "PLANNED")], "2026-11-04T12:00:00+00:00")
        g("checkout", "-q", "main~1")
        g("checkout", "-q", "-b", "d")
        try:                                                    # the merge is committed only after the change below
            g("merge", "-q", "--no-ff", "--no-commit", "c", when="2026-11-04T13:00:00+00:00")
        except subprocess.CalledProcessError:
            pass
        Path(td, q).write_text("id\titem\tstate\nWP-1\tx\tACCEPTED\nWP-2\tx\tBLOCKED\nWP-3\tx\tBekliyor — notlar burada\n",
                               encoding="utf-8")                                              # a resolution changes WP-2
        g("add", "-A")
        g("commit", "-q", "-m", "resolve", when="2026-11-04T13:00:00+00:00")
        rd = Repo(td, "d")
        is_merge = len(rd.parents[git(td, "rev-parse", "d").strip()]) == 2
        ev = queue_events(rd, q)
        cases.append(("sabotage queue (Codex): a change made in a merge commit (its result differs from every parent) is "
                      "counted", is_merge and any(wp == "WP-2" and s_ == "BLOCKED" for _t, wp, s_ in ev)))
        lines = render([week], [s], [r], qs, "test", week)
        cases.append(("render (Codex): the stay timing rule and the UTC cut-off are written",
                      any("kendi ebeveynine göre" in l and "kesim 2026-11-05T10:00:00+00:00" in l for l in lines)))
        Path(td, q).unlink()
        g("add", "-A")
        g("commit", "-q", "-m", "delete", when="2026-11-05T08:00:00+00:00")
        ev = queue_events(Repo(td, "d"), q)
        gone = dt.datetime(2026, 11, 5, 8, tzinfo=dt.timezone.utc)
        cases.append(("queue (Codex): a real deletion closes every stay of the queue (its 3 rows, at the deleting commit)",
                      sum(1 for t_, _w, s_ in ev if s_ is None and t_ == gone) == 3))
        real_git = globals()["git"]
        bad_sha = real_git(td, "rev-parse", "main~1").strip()

        def broken_git(root_, *args):
            if args[:1] == ("show",) and args[1].startswith(bad_sha + ":"):
                raise Unreadable("git show (test)")
            return real_git(root_, *args)
        globals()["git"] = broken_git
        try:
            stopped = isinstance(attempt(queue_stats, td, "main", end, end), Unreadable)
        finally:
            globals()["git"] = real_git
        cases.append(("sabotage queue (Codex): a version git lists but cannot show stops the run (exit 2), never a deletion",
                      stopped))
        try:
            queue_stats(td, "no-such-ref", end, end)
            raised = False
        except Unreadable:
            raised = True
        cases.append(("sabotage queue: an unreadable ref stops the run (exit 2), never an empty table", raised))
    # git's default history simplification drops a side branch whose merge result equals the first parent — the
    # 1-of-58-versions case; --full-history keeps the side branch's own, earlier commit
    with tempfile.TemporaryDirectory() as td:
        env = {**os.environ, "GIT_AUTHOR_NAME": "t", "GIT_AUTHOR_EMAIL": "t@x", "GIT_COMMITTER_NAME": "t",
               "GIT_COMMITTER_EMAIL": "t@x"}
        g = lambda *a, when=None: subprocess.run(["git", "-C", td, *a], capture_output=True, check=True,
                                                 env={**env, **({"GIT_COMMITTER_DATE": when, "GIT_AUTHOR_DATE": when} if when else {})})
        q = "execution/q/CT-QUEUE.tsv"
        Path(td, "execution/delivery").mkdir(parents=True)
        Path(td, "execution/q").mkdir(parents=True)
        Path(td, REGISTRY).write_text("queue_id\tpath\tvocabulary\nq1\t" + q + "\t\n", encoding="utf-8")
        Path(td, STATES).write_text("queue_id\tstate\tclass\n", encoding="utf-8")
        put_q = lambda state: Path(td, q).write_text(f"id\titem\tstate\nWP-9\tx\t{state}\n", encoding="utf-8")
        g("init", "-q", "-b", "main")
        put_q("PLANNED")
        g("add", "-A")
        g("commit", "-q", "-m", "c0", when="2026-11-02T08:00:00+00:00")
        g("checkout", "-q", "-b", "x")
        put_q("READY")
        g("commit", "-q", "-am", "x1", when="2026-11-02T09:00:00+00:00")
        g("checkout", "-q", "main")
        put_q("READY")
        g("commit", "-q", "-am", "m1", when="2026-11-02T12:00:00+00:00")
        g("merge", "-q", "--no-ff", "-m", "merge x", "x", when="2026-11-02T13:00:00+00:00")
        hs = attempt(queue_stats, td, "main", dt.datetime(2026, 11, 2, tzinfo=dt.timezone.utc),
                     dt.datetime(2026, 11, 3, tzinfo=dt.timezone.utc))
        cases.append(("sabotage queue (Codex): a side branch whose merge equals main's own version keeps its earlier "
                      "commit — WP-9 READY since 09:00 (15 h), not since main's 12:00 (12 h)",
                      isinstance(hs, dict) and hs["open"].get("READY", {}).get("age_median_h") == 15))
    # the GitHub readers against a stand-in `gh` on PATH
    with tempfile.TemporaryDirectory() as td:
        conn = lambda nodes, more, cur: {"pageInfo": {"hasNextPage": more, "endCursor": cur}, "nodes": nodes}
        node = lambda n, merged, updated, files=None, files_more=False: {
            "number": n, "createdAt": merged, "mergedAt": merged, "updatedAt": updated,
            "reviews": conn([{"submittedAt": merged, "author": {"login": f"r{k}"}} for k in range(1)], n == 5, "rv"),
            "files": conn([{"path": p} for p in (files or ["docs/a.md"])], files_more, "f1")}
        page = lambda nodes, more, cur: {"data": {"repository": {"pullRequests": conn(nodes, more, cur)}}}
        pages = {"": page([node(5, "2026-11-04T10:00:00Z", "2026-11-04T10:00:00Z", ["docs/b.md"], True),
                           node(4, "2026-10-01T10:00:00Z", "2026-11-03T10:00:00Z")], True, "c1"),
                 "c1": page([node(3, "2026-11-02T09:00:00Z", "2026-11-02T09:00:00Z")], True, "c2"),
                 "c2": page([node(2, "2026-10-20T10:00:00Z", "2026-10-20T10:00:00Z")], True, "c3"),
                 "c3": page([node(1, "2026-11-03T10:00:00Z", "2026-11-03T10:00:00Z")], False, None),
                 "f1": {"data": {"repository": {"pullRequest": {"files": conn([{"path": "services/x.cs"}], False, None)}}}},
                 "rv": {"data": {"repository": {"pullRequest": {"reviews": conn(
                     [{"submittedAt": "2026-11-04T10:00:00Z", "author": {"login": "r9"}}], False, None)}}}}}
        runs_by_range = {"2026-11-02..2026-11-08": 3, "2026-10-26..2026-11-01": 1500, "2026-10-20..2026-10-20": 1200,
                         "2026-10-26..2026-10-29": 800, "2026-10-30..2026-11-01": 700}
        Path(td, "pages.json").write_text(json.dumps(pages), encoding="utf-8")
        Path(td, "runs.json").write_text(json.dumps(runs_by_range), encoding="utf-8")
        Path(td, "gh").write_text(
            "#!/usr/bin/env python3\nimport json, os, re, sys\nd = os.path.dirname(os.path.abspath(__file__))\n"
            "if os.path.exists(os.path.join(d, 'refuse')): sys.stderr.write('HTTP 502'); sys.exit(1)\n"
            "a = sys.argv[1:]\n"
            "if a[:2] == ['api', 'graphql']:\n"
            "    cur = next((x.split('=', 1)[1] for x in a if x.startswith('cursor=')), '')\n"
            "    print(json.dumps(json.load(open(os.path.join(d, 'pages.json')))[cur])); sys.exit(0)\n"
            "url = a[1]; rng = re.search(r'created=([^&]+)', url).group(1); page = int(re.search(r'[?&]page=(\\d+)', url).group(1))\n"
            "total = json.load(open(os.path.join(d, 'runs.json')))[rng]\n"
            "short = os.path.exists(os.path.join(d, 'short'))\n"
            "n = 0 if total > 1000 else max(0, min(100, total - (page - 1) * 100 - (1 if short and page == 1 else 0)))\n"
            "a0 = rng.split('..')[0]\n"
            "print(json.dumps({'total_count': total, 'workflow_runs': [{'created_at': a0 + 'T10:00:00Z', 'updated_at': a0 + "
            "'T10:10:00Z', 'conclusion': 'success', 'status': 'completed'}] * n}))\n", encoding="utf-8")
        os.chmod(Path(td, "gh"), 0o755)
        saved = os.environ["PATH"]
        os.environ["PATH"] = td + os.pathsep + saved
        try:
            got, spent = fetch_prs(dt.date(2026, 11, 2))
            five = next((p for p in got if p["number"] == 5), {})
            cases.append(("GitHub: the PR walk reads on past a page with an old merge and stops only on a page entirely "
                          "older than the window (merges 5 and 3)", sorted(p["number"] for p in got) == [3, 5]))
            cases.append(("sabotage GitHub (Codex): a PR's second page of files and of approvals is read — PR 5 is product "
                          "work and has 2 approvers", kind_of(five.get("files", [])) == "ürün" and len(five.get("reviews", [])) == 2
                          and spent == 5))
            got_runs = attempt(fetch_runs, [(dt.date(2026, 10, 26), dt.date(2026, 11, 1)),
                                            (dt.date(2026, 11, 2), dt.date(2026, 11, 8))])
            cases.append(("sabotage GitHub (Codex): a week above GitHub's 1000-run cap is split and read in full "
                          "(800 + 700 + 3 runs)", isinstance(got_runs, tuple) and len(got_runs[0]) == 1503))
            cases.append(("sabotage GitHub: a single day above the cap stops the run (it cannot be split further)",
                          isinstance(attempt(fetch_runs, [(dt.date(2026, 10, 20), dt.date(2026, 10, 20))]), Unreadable)))
            Path(td, "short").write_text("x", encoding="utf-8")
            try:
                fetch_runs([(dt.date(2026, 11, 2), dt.date(2026, 11, 8))])
                caught = False
            except Unreadable:
                caught = True
            cases.append(("sabotage GitHub: fewer runs read than GitHub's total_count stops the run", caught))
            Path(td, "short").unlink()
            Path(td, "refuse").write_text("x", encoding="utf-8")
            for label, fn in (("PR", lambda: fetch_prs(dt.date(2026, 11, 2))),
                              ("run", lambda: fetch_runs([(dt.date(2026, 11, 2), dt.date(2026, 11, 8))]))):
                try:
                    fn()
                    stopped = False
                except Unreadable:
                    stopped = True
                cases.append((f"sabotage GitHub: a refused {label} read stops the run (exit 2), never a partial table", stopped))
            os.environ["PATH"] = os.path.join(td, "nothing-here")
            cases.append(("sabotage GitHub (Codex): no gh at all stops the run (exit 2), not a Python error",
                          isinstance(attempt(fetch_runs, [(dt.date(2026, 11, 2), dt.date(2026, 11, 8))]), Unreadable)))
        finally:
            os.environ["PATH"] = saved
    for label, ok in cases:
        print(("PASS " if ok else "FAIL ") + label)
    passed = sum(ok for _, ok in cases)
    print(f"self-test: {passed}/{len(cases)} passed")
    return 0 if passed == len(cases) else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
