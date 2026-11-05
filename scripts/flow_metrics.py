#!/usr/bin/env python3
"""FLOW-01 (owner 'Evet, ölçüm' 2026-11-05) — the development flow measured every week: speed and waiting, the two
criteria that carry 45 % of the independent score and had no data. Read-only; standard library, git and `gh`.

    python3 scripts/flow_metrics.py [--weeks 4] [--until YYYY-MM-DD] [--json]

What it measures, per calendar week (Monday–Sunday, UTC):
  Pull requests (GitHub GraphQL, merged in the week)
    - count; open → merge median and p90 (hours); open → first approval median; first approval → merge median
    - kind by the files changed: product (services/, frontend/, gateway/, tests/), tool (scripts/, .github/,
      .antigravity/scripts/), process (everything else: records, packs, queues, rules)
    - share approved by the most frequent approver account
  Main check (phase1-gates runs, GitHub REST): runs, red share, median duration (minutes)
  Work-package waiting (every queue in execution/delivery/ct-queue-registry.tsv, read through git on main's first-parent
    line): for each state, how long work packages stayed in it before they moved on, median hours, and the open ones
    still in it at the end with their age. A state's time is counted from the commit that brought the change TO MAIN —
    when it became true on main, not when someone did it; that is said in the output.

WHAT IS PROTECTED (threat model, written before the review): a number is never shown without the data behind it. A
week, a state or a measure without data reads "veri yok", never 0; GitHub or git that cannot be read stops the run
(exit 2) instead of a partial table; every source is read for the whole window (all pages), so "veri yok" means there
was nothing, never that it was not fetched; every number says what it counts. GitHub cost — a few GraphQL pages and
one REST page per 100 runs — is printed.
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
PRODUCT = ("services/", "frontend/", "gateway/", "tests/")
TOOL = ("scripts/", ".github/", ".antigravity/scripts/")
NO_DATA = "veri yok"


class Unreadable(Exception):
    """GitHub or git cannot be read: exit 2, never a partial table."""


_CQ = None


def queue_reader():
    """scripts/ct_queue.py — its Table is the one reader of a queue file."""
    global _CQ
    if _CQ is None:
        spec = importlib.util.spec_from_file_location("ct_queue_flow", ROOT / "scripts" / "ct_queue.py")
        _CQ = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(_CQ)
    return _CQ


def ts(s: str) -> dt.datetime:
    return dt.datetime.fromisoformat(s.replace("Z", "+00:00")).astimezone(dt.timezone.utc)


def hours(a: dt.datetime, b: dt.datetime) -> float:
    return (b - a).total_seconds() / 3600


def p90(xs: list[float]) -> float:
    xs = sorted(xs)
    return xs[max(0, -(-len(xs) * 9 // 10) - 1)]                  # nearest rank


def fmt(x: float | None, unit: str = "sa") -> str:
    return NO_DATA if x is None else f"{x:.1f} {unit}"


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
    """One week's pull-request numbers; None (shown as 'veri yok') where there is nothing to measure."""
    w = [p for p in prs if in_week(ts(p["mergedAt"]), week)]
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
    return {"prs": len(w),
            "lead_median_h": statistics.median(lead) if lead else None, "lead_p90_h": p90(lead) if lead else None,
            "first_approval_median_h": statistics.median(first) if first else None,
            "approval_to_merge_median_h": statistics.median(after) if after else None,
            "product": kinds.count("ürün"), "tool": kinds.count("araç"), "process": kinds.count("süreç"),
            "approved": approved, "unapproved": len(w) - approved,
            "top_approver_share": (top[1] / approved) if top and approved else None}


QUERY = """query($cursor:String){ repository(owner:"%s", name:"%s"){
 pullRequests(states:MERGED, first:20, after:$cursor, orderBy:{field:UPDATED_AT, direction:DESC}){
  pageInfo{hasNextPage endCursor}
  nodes{ number createdAt mergedAt updatedAt
   reviews(first:30, states:[APPROVED]){nodes{submittedAt author{login}}}
   files(first:100){nodes{path}} } } } }""" % REPO


def fetch_prs(since: dt.date) -> tuple[list[dict], int]:
    """Merged pull requests updated since `since` (a merge after it implies an update after it), and the GraphQL points
    spent. Pages are newest-updated first; the walk stops on the first page entirely older than `since`."""
    out, cursor, spent = [], None, 0
    while True:
        args = ["gh", "api", "graphql", "-f", f"query={QUERY}"] + (["-f", f"cursor={cursor}"] if cursor else [])
        done = subprocess.run(args, capture_output=True)
        if done.returncode != 0:
            raise Unreadable(f"GitHub GraphQL okunamadı: {done.stderr.decode(errors='replace')[:160]}")
        try:
            page = json.loads(done.stdout)["data"]["repository"]["pullRequests"]
        except (ValueError, KeyError, TypeError) as e:
            raise Unreadable(f"GitHub GraphQL cevabı bozuk ({type(e).__name__})")
        spent += 1
        nodes = page["nodes"]
        for n in nodes:
            if n.get("mergedAt") and ts(n["mergedAt"]).date() >= since:
                out.append({"number": n["number"], "createdAt": n["createdAt"], "mergedAt": n["mergedAt"],
                            "reviews": [{"submittedAt": r.get("submittedAt"), "author": (r.get("author") or {}).get("login")}
                                        for r in n["reviews"]["nodes"]],
                            "files": [f["path"] for f in n["files"]["nodes"]]})
        if not page["pageInfo"]["hasNextPage"] or all(ts(n["updatedAt"]).date() < since for n in nodes):
            break
        cursor = page["pageInfo"]["endCursor"]
    return out, spent


# ------------------------------------------------------------------------------------------------ main check
def fetch_runs(since: dt.date) -> tuple[list[dict], int]:
    """Every phase1-gates run created since `since`, all pages (a fixed '--limit 300' reached back only 10 days and the
    weeks before read 'veri yok' although the runs existed — measured 2026-11-05), and the REST pages spent."""
    done = subprocess.run(["gh", "api", "--paginate", f"repos/{'/'.join(REPO)}/actions/workflows/phase1-gates.yml/runs"
                           f"?created=%3E%3D{since.isoformat()}&per_page=100", "-q",
                           ".workflow_runs[] | {createdAt: .created_at, updatedAt: .updated_at, conclusion, status}"],
                          capture_output=True)
    if done.returncode != 0:
        raise Unreadable(f"phase1 koşuları okunamadı: {done.stderr.decode(errors='replace')[:160]}")
    try:
        runs = [json.loads(line) for line in done.stdout.decode().splitlines() if line.strip()]
    except ValueError:
        raise Unreadable("phase1 koşu cevabı bozuk")
    return runs, max(1, -(-len(runs) // 100))


def run_stats(runs: list[dict], week: tuple[dt.date, dt.date]) -> dict:
    w = [r for r in runs if r.get("status") == "completed" and r.get("conclusion") in ("success", "failure")
         and in_week(ts(r["createdAt"]), week)]
    d = [hours(ts(r["createdAt"]), ts(r["updatedAt"])) * 60 for r in w]
    return {"runs": len(w), "red": sum(r["conclusion"] == "failure" for r in w),
            "duration_median_min": statistics.median(d) if d else None}


# ------------------------------------------------------------------------------------------------ queue waiting
def git(root: str, *args: str) -> str:
    done = subprocess.run(["git", "-C", root, *args], capture_output=True)
    if done.returncode != 0:
        raise Unreadable(f"git {' '.join(args[:2])} okunamadı")
    return done.stdout.decode("utf-8", errors="replace")


_MERGES: dict[tuple[str, str], set[str]] = {}


def _merges(root: str, ref: str) -> set[str]:
    key = (root, ref)
    if key not in _MERGES:
        _MERGES[key] = set(git(root, "rev-list", "--merges", ref).split())
    return _MERGES[key]


def queue_history(root: str, ref: str, path: str) -> list[tuple[dt.datetime, dict[str, str]]]:
    """[(commit time, {work package: state})] of every version of the queue in `ref`'s history, oldest first — the
    towers' own commits on their branches included (a queue changes on a tower branch and reaches main in one merge:
    main's first-parent line would date every change to that merge; measured 2026-11-05: every open stay the same
    3.7 h). --full-history: git's default simplification kept 1 of 58 versions of the PVG queue (measured). Merge commits
    change no state by themselves and are skipped by their real parents — path limiting rewrites parents, so a merge
    can look like a plain commit there."""
    table = queue_reader().Table
    merges = _merges(root, ref)
    out = []
    for line in git(root, "log", "--full-history", "--date-order", "--reverse", "--format=%H %cI", ref, "--",
                    path).splitlines():
        sha, when = line.split(" ", 1)
        if sha in merges:
            continue
        data = subprocess.run(["git", "-C", root, "show", f"{sha}:{path}"], capture_output=True)
        if data.returncode != 0:
            continue                                              # the file was deleted in this version
        t = table(data.stdout.decode("utf-8", errors="replace"))
        i, s = t.index("id"), t.index("state")
        if i is None or s is None:
            continue
        out.append((ts(when), {r[i]: r[s] for r in t.rows if len(r) > max(i, s) and r[i]}))
    return out


def stays(history: list[tuple[dt.datetime, dict[str, str]]], end: dt.datetime) -> tuple[list[tuple], list[tuple]]:
    """(closed stays, open stays): (work package, state, entered, left | None) from the versions of one queue."""
    closed, entered = [], {}
    for when, states in history:
        for wp, state in states.items():
            prev = entered.get(wp)
            if prev is None or prev[0] != state:
                if prev is not None:
                    closed.append((wp, prev[0], prev[1], when))
                entered[wp] = (state, when)
        for wp in [w for w in entered if w not in states]:        # a row removed from the queue ends its stay
            closed.append((wp, entered[wp][0], entered[wp][1], when))
            del entered[wp]
    return closed, [(wp, s, t, None) for wp, (s, t) in entered.items() if t <= end]


UNDEFINED = "(tanımsız durum)"


def queue_stats(root: str, ref: str, since: dt.datetime, end: dt.datetime) -> dict:
    """Per state: stays that ENDED in [since, end] with their median length, and stays still open at `end` with their
    median age. A state outside the queue's own vocabulary (execution/delivery/ct-queue-states.tsv, by the registry's
    vocabulary column) is one bucket, UNDEFINED — free text in a state cell is not a state (measured: one queue holds
    notes there)."""
    table = queue_reader().Table
    t = table(git(root, "show", f"{ref}:{REGISTRY}"))
    qi, pi, vi = t.index("queue_id"), t.index("path"), t.index("vocabulary")
    if qi is None or pi is None:
        raise Unreadable(f"{REGISTRY}: queue_id / path sütunu yok")
    vocab: dict[str, dict[str, str]] = {}                          # vocabulary -> {state: class}
    vt = table(git(root, "show", f"{ref}:execution/delivery/ct-queue-states.tsv"))
    a_, b_, c_ = vt.index("queue_id"), vt.index("state"), vt.index("class")
    for r in vt.rows:
        if a_ is not None and b_ is not None and len(r) > max(a_, b_):
            vocab.setdefault(r[a_], {})[r[b_]] = r[c_] if c_ is not None and len(r) > c_ else ""
    closed_states: set[str] = set()
    per_state: dict[str, list[float]] = {}
    open_now: dict[str, list[float]] = {}
    queues = 0
    for row in t.rows:
        if len(row) <= max(qi, pi) or not row[pi]:
            continue
        queues += 1
        known = vocab.get(row[vi]) if vi is not None and len(row) > vi else None
        name = (lambda s: s if s in known else UNDEFINED) if known else (lambda s: s)
        closed, still = stays(queue_history(root, ref, row[pi]), end)
        for _wp, state, a, b in closed:
            if since <= b <= end:                                 # the stay ended inside the measured window
                per_state.setdefault(name(state), []).append(hours(a, b))
        for _wp, state, a, _b in still:
            if known and known.get(state) == "closed":            # a finished work package waits for nothing
                closed_states.add(state)
                continue
            open_now.setdefault(name(state), []).append(hours(a, end))
    return {"queues": queues, "closed_states": sorted(closed_states),
            "states": {s: {"left": len(v), "median_h": statistics.median(v)} for s, v in sorted(per_state.items())},
            "open": {s: {"count": len(v), "age_median_h": statistics.median(v)} for s, v in sorted(open_now.items())}}


# ------------------------------------------------------------------------------------------------ report
def render(weeks: list, pr: list[dict], ci: list[dict], q: dict, cost: str, window: tuple) -> list[str]:
    L = [f"FLOW-01 — {window[0]} … {window[1]} (UTC hafta: pazartesi–pazar) · GitHub maliyeti: {cost}", "",
         "PR'lar (haftada birleşenler)",
         "hafta | PR | açılış→birleşme medyan / p90 | açılış→ilk onay medyan | onay→birleşme medyan | ürün/araç/süreç"
         " | onaysız | en sık onaylayan payı"]
    for (a, b), s in zip(weeks, pr):
        share = NO_DATA if s["top_approver_share"] is None else f"%{s['top_approver_share'] * 100:.0f}"
        L.append(f"{a:%d.%m}–{b:%d.%m} | {s['prs'] or NO_DATA} | {fmt(s['lead_median_h'])} / {fmt(s['lead_p90_h'])} | "
                 f"{fmt(s['first_approval_median_h'])} | {fmt(s['approval_to_merge_median_h'])} | "
                 f"{s['product']}/{s['tool']}/{s['process']} | {s['unapproved']} | {share}")
    L += ["", "Ana kontrol (phase1)", "hafta | koşu | kırmızı | süre medyan"]
    for (a, b), s in zip(weeks, ci):
        L.append(f"{a:%d.%m}–{b:%d.%m} | {s['runs'] or NO_DATA} | "
                 f"{(str(s['red']) + '/' + str(s['runs'])) if s['runs'] else NO_DATA} | {fmt(s['duration_median_min'], 'dk')}")
    L += ["", f"İş paketi beklemeleri ({q['queues']} kuyruk; süre, durumu değiştiren commit'in zamanından sayılır — "
              "kulenin kendi dalındaki commit dahil; ana dala ulaşma beklemesi PR tablosundadır)",
          "durum | pencerede çıkılan | o durumda kalış medyan"]
    L += [f"{s} | {v['left']} | {fmt(v['median_h'])}" for s, v in q["states"].items()] or [f"— | {NO_DATA} | {NO_DATA}"]
    L += ["", "Şu an bekleyenler (pencerenin sonunda; kuyruğun durum listesinde 'closed' sınıfındaki "
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
    until = dt.date.fromisoformat(a.until) if a.until else dt.datetime.now(dt.timezone.utc).date()
    weeks = weeks_ending(until, max(1, a.weeks))
    start = dt.datetime.combine(weeks[0][0], dt.time(), dt.timezone.utc)
    end = dt.datetime.combine(weeks[-1][1], dt.time(23, 59, 59), dt.timezone.utc)
    try:
        prs, points = fetch_prs(weeks[0][0])
        runs, pages = fetch_runs(weeks[0][0])
        pr = [pr_stats(prs, w) for w in weeks]
        ci = [run_stats(runs, w) for w in weeks]
        q = queue_stats(str(ROOT), a.ref, start, min(end, dt.datetime.now(dt.timezone.utc)))
    except Unreadable as e:
        print(f"sonuç: tamamlanamadı — {e}\nçıkış: 2")
        return 2
    cost = f"{points} GraphQL sayfası, {pages} REST sayfası"
    if a.json:
        print(json.dumps({"weeks": [[str(x), str(y)] for x, y in weeks], "prs": pr, "ci": ci, "queues": q, "cost": cost},
                         ensure_ascii=False, indent=1))
    else:
        print("\n".join(render(weeks, pr, ci, q, cost, (weeks[0][0], weeks[-1][1]))))
    return 0


# ------------------------------------------------------------------------------------------------ self-test
def self_test() -> int:
    cases: list[tuple[str, bool]] = []
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
    empty = pr_stats(prs, (dt.date(2026, 10, 19), dt.date(2026, 10, 25)))
    cases.append(("sabotage PR: a week without merges is 'veri yok', never 0 h",
                  empty["prs"] == 0 and empty["lead_median_h"] is None and empty["top_approver_share"] is None
                  and fmt(empty["lead_median_h"]) == NO_DATA))
    runs = [{"createdAt": "2026-11-03T10:00:00Z", "updatedAt": "2026-11-03T10:10:00Z", "conclusion": "failure", "status": "completed"},
            {"createdAt": "2026-11-04T10:00:00Z", "updatedAt": "2026-11-04T10:12:00Z", "conclusion": "success", "status": "completed"},
            {"createdAt": "2026-11-04T11:00:00Z", "updatedAt": "2026-11-04T11:01:00Z", "conclusion": "cancelled", "status": "completed"},
            {"createdAt": "2026-11-05T10:00:00Z", "updatedAt": "2026-11-05T10:05:00Z", "conclusion": None, "status": "in_progress"}]
    r = run_stats(runs, week)
    cases.append(("CI: completed success/failure runs only (cancelled and running ones are no measure); median 11 min",
                  r["runs"] == 2 and r["red"] == 1 and r["duration_median_min"] == 11))
    cases.append(("sabotage CI: a week without runs is 'veri yok'", run_stats(runs, (dt.date(2026, 10, 1), dt.date(2026, 10, 7)))
                  ["duration_median_min"] is None))
    with tempfile.TemporaryDirectory() as td:
        env = {**os.environ, "GIT_AUTHOR_NAME": "t", "GIT_AUTHOR_EMAIL": "t@x", "GIT_COMMITTER_NAME": "t",
               "GIT_COMMITTER_EMAIL": "t@x"}
        g = lambda *a, when=None: subprocess.run(["git", "-C", td, *a], capture_output=True, check=True,
                                                 env={**env, **({"GIT_COMMITTER_DATE": when, "GIT_AUTHOR_DATE": when} if when else {})})
        g("init", "-q", "-b", "main")
        q = "execution/q/CT-QUEUE.tsv"
        Path(td, "execution/delivery").mkdir(parents=True)
        Path(td, "execution/q").mkdir(parents=True)
        Path(td, REGISTRY).write_text("queue_id\tpath\tvocabulary\nq1\t" + q + "\tv1\n", encoding="utf-8")
        Path(td, "execution/delivery/ct-queue-states.tsv").write_text(
            "queue_id\tstate\tclass\n" + "".join(f"v1\t{s}\t{c}\n" for s, c in (
                ("PLANNED", "planned"), ("READY", "dispatchable"), ("DISPATCHED", "active"), ("ACCEPTED", "closed"))),
            encoding="utf-8")

        def version(rows: list[tuple[str, str]], when: str) -> None:
            Path(td, q).write_text("id\titem\tstate\n" + "".join(f"{i}\tx\t{s}\n" for i, s in rows), encoding="utf-8")
            g("add", "-A")
            g("commit", "-q", "-m", when, when=when)
        version([("WP-1", "PLANNED")], "2026-11-02T08:00:00+00:00")
        version([("WP-1", "READY"), ("WP-2", "PLANNED")], "2026-11-02T10:00:00+00:00")
        version([("WP-1", "DISPATCHED"), ("WP-2", "PLANNED")], "2026-11-02T16:00:00+00:00")
        g("checkout", "-q", "-b", "side")
        version([("WP-1", "ACCEPTED"), ("WP-2", "PLANNED"), ("WP-3", "Bekliyor — notlar burada")],
                "2026-11-03T10:00:00+00:00")                                         # done on a tower branch …
        g("checkout", "-q", "main")
        g("merge", "-q", "--no-ff", "-m", "merge", "side", when="2026-11-04T10:00:00+00:00")   # … reaches main a day later
        end = dt.datetime(2026, 11, 5, 10, tzinfo=dt.timezone.utc)
        qs = queue_stats(td, "main", dt.datetime(2026, 11, 2, tzinfo=dt.timezone.utc), end)
        st = qs["states"]
        cases.append(("queue: PLANNED 2 h, READY 6 h, from every version of the queue",
                      st.get("PLANNED", {}).get("median_h") == 2 and st.get("READY", {}).get("median_h") == 6))
        cases.append(("sabotage queue: a change on a tower branch counts from its own commit, not from the merge that "
                      "brought it to main (DISPATCHED 18 h, not 42 h)", st.get("DISPATCHED", {}).get("median_h") == 18))
        cases.append(("queue: still-open stays are listed with their age (WP-2 PLANNED 72 h); a work package in a "
                      "'closed' state (WP-1 ACCEPTED) waits for nothing and is not listed",
                      qs["open"].get("PLANNED", {}).get("age_median_h") == 72 and "ACCEPTED" not in qs["open"]
                      and qs["closed_states"] == ["ACCEPTED"]))
        cases.append(("sabotage queue: free text in a state cell is not a state — one '(tanımsız durum)' bucket",
                      qs["open"].get(UNDEFINED, {}).get("count") == 1 and not any("notlar" in k for k in qs["open"])))
        lines = render([week], [s], [r], qs, "test", week)
        cases.append(("render: every measure names what it counts and how a stay is timed",
                      any("durumu değiştiren commit'in zamanından" in l for l in lines)))
        empty_q = queue_stats(td, "main", dt.datetime(2026, 12, 1, tzinfo=dt.timezone.utc),
                              dt.datetime(2026, 12, 7, tzinfo=dt.timezone.utc))
        cases.append(("queue: a window in which no stay ended has no state rows (nothing is invented)",
                      empty_q["states"] == {}))
        lines = render([week], [empty], [run_stats([], week)], {"queues": 1, "states": {}, "open": {}}, "test", week)
        cases.append(("sabotage render: no data anywhere reads 'veri yok' in every table, never a 0 h number",
                      sum(NO_DATA in l for l in lines) == 4 and not any(" 0.0 sa" in l or " 0.0 dk" in l for l in lines)))
        try:
            queue_stats(td, "no-such-ref", end, end)
            raised = False
        except Unreadable:
            raised = True
        cases.append(("sabotage queue: an unreadable ref stops the run (exit 2), never an empty table", raised))
    # the GitHub readers against a stand-in `gh` on PATH: every page is read, the PR walk stops only on a page entirely
    # older than the window, a refusal stops the run
    with tempfile.TemporaryDirectory() as td:
        page = lambda nodes, more, cur: {"data": {"repository": {"pullRequests": {
            "pageInfo": {"hasNextPage": more, "endCursor": cur}, "nodes": nodes}}}}
        node = lambda n, merged, updated: {"number": n, "createdAt": merged, "mergedAt": merged, "updatedAt": updated,
                                           "reviews": {"nodes": []}, "files": {"nodes": [{"path": "services/a.cs"}]}}
        pages = {"": page([node(5, "2026-11-04T10:00:00Z", "2026-11-04T10:00:00Z"),
                          node(4, "2026-10-01T10:00:00Z", "2026-11-03T10:00:00Z")], True, "c1"),
                 "c1": page([node(3, "2026-11-02T09:00:00Z", "2026-11-02T09:00:00Z")], True, "c2"),
                 "c2": page([node(2, "2026-10-20T10:00:00Z", "2026-10-20T10:00:00Z")], True, "c3"),
                 "c3": page([node(1, "2026-11-03T10:00:00Z", "2026-11-03T10:00:00Z")], False, None)}
        Path(td, "pages.json").write_text(json.dumps(pages), encoding="utf-8")
        runs = [{"created_at": "2026-11-03T10:00:00Z", "updated_at": "2026-11-03T10:10:00Z", "conclusion": "success",
                 "status": "completed"}] * 3
        Path(td, "runs.json").write_text("\n".join(json.dumps({"createdAt": r["created_at"], "updatedAt": r["updated_at"],
                                                               "conclusion": r["conclusion"], "status": r["status"]})
                                                   for r in runs) + "\n", encoding="utf-8")
        Path(td, "gh").write_text(
            "#!/usr/bin/env python3\nimport json, os, sys\nd = os.path.dirname(os.path.abspath(__file__))\n"
            "if os.path.exists(os.path.join(d, 'refuse')): sys.stderr.write('HTTP 502'); sys.exit(1)\n"
            "a = sys.argv[1:]\n"
            "if a[:2] == ['api', 'graphql']:\n"
            "    cur = next((x.split('=', 1)[1] for x in a if x.startswith('cursor=')), '')\n"
            "    print(json.dumps(json.load(open(os.path.join(d, 'pages.json')))[cur]))\n"
            "else:\n    sys.stdout.write(open(os.path.join(d, 'runs.json')).read())\n", encoding="utf-8")
        os.chmod(Path(td, "gh"), 0o755)
        saved = os.environ["PATH"]
        os.environ["PATH"] = td + os.pathsep + saved
        try:
            got, spent = fetch_prs(dt.date(2026, 11, 2))
            cases.append(("GitHub: the PR walk reads on past a page with an old merge and stops only on a page entirely "
                          "older than the window (merges 5 and 3; 4 and 2 are older, 1 is behind the stop)",
                          sorted(p["number"] for p in got) == [3, 5] and spent == 3))
            got_runs, rpages = fetch_runs(dt.date(2026, 11, 2))
            cases.append(("GitHub: every run line is read", len(got_runs) == 3 and rpages == 1))
            Path(td, "refuse").write_text("x", encoding="utf-8")
            for label, fn in (("PR", lambda: fetch_prs(dt.date(2026, 11, 2))), ("run", lambda: fetch_runs(dt.date(2026, 11, 2)))):
                try:
                    fn()
                    stopped = False
                except Unreadable:
                    stopped = True
                cases.append((f"sabotage GitHub: a refused {label} read stops the run (exit 2), never a partial table", stopped))
        finally:
            os.environ["PATH"] = saved
    for label, ok in cases:
        print(("PASS " if ok else "FAIL ") + label)
    passed = sum(ok for _, ok in cases)
    print(f"self-test: {passed}/{len(cases)} passed")
    return 0 if passed == len(cases) else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
