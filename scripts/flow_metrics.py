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
  Where work packages wait (every queue in execution/delivery/ct-queue-registry.tsv, read on main at the cut-off): per
    state how many wait and for how many days — from the tower's own 'since' date in the row — and the five oldest.

WHAT IS PROTECTED (threat model, written before the review): the tool never shows a number without the data behind it
and never a number that differs from what happened. (a) no data → "veri yok" / null, never 0; (b) every source is read
in full for the window — an answer that could be capped or a list that could be cut is verified or split, so "veri yok"
means there was nothing; (c) GitHub, gh or git that cannot be read stops the run (exit 2), never a partial table;
(d) every number says what it counts; (e) where a waiting age comes from, main's commit read and the UTC cut-off
are written in both outputs.
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
         "queue_time": "bekleme yaşı, kulenin kuyrukta yazdığı 'since' tarihinden (gün); kuyruklar ana dalın kesim "
                       "anındaki commit'inden okunur; kulenin henüz ana dala gelmemiş değişiklikleri görünmez"}


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


def snapshot_commit(root: str, ref: str, end: dt.datetime) -> tuple[str, dt.datetime]:
    """Main as it was at the cut-off: the newest commit on `ref`'s first-parent line committed at or before `end`."""
    sha = git(root, "rev-list", "-1", "--first-parent", f"--before={end.strftime('%Y-%m-%dT%H:%M:%S+00:00')}", ref).strip()
    if not sha:
        raise Unreadable(f"{ref}: kesim anından ({end.isoformat()}) önce commit yok")
    return sha, ts(git(root, "show", "-s", "--format=%cI", sha).strip())


def queue_snapshot(root: str, ref: str, end: dt.datetime) -> dict:
    """Where work packages wait at the cut-off, from the towers' own record: every queue in the registry, read at main's
    commit at the cut-off, each row's state and its 'since' date (the day the tower wrote that the row entered its
    state). No git history is rebuilt (owner 2026-11-05 after Codex round 2: branches, merges and same-second commits
    made a rebuilt history wrong in new corners every round; all 708 rows carry a since date). Per state: how many wait
    and their age in days (median, oldest). A 'closed'-class state waits for nothing; a state outside the queue's own
    vocabulary is one UNDEFINED bucket; a since date that cannot be read or lies after the cut-off gives no age and is
    counted. A work package with more than one row (a queue that appended a row per change — measured: 79 in one queue;
    the first row showed Q15 waiting 41 days for a decision taken on 27 Sep) is counted once, by its row with the latest
    since date — the latest state entry is the current state; on a tie the later row."""
    sha, when = snapshot_commit(root, ref, end)
    table = queue_reader().Table
    t = table(git(root, "show", f"{sha}:{REGISTRY}"))
    qi, pi, vi = t.index("queue_id"), t.index("path"), t.index("vocabulary")
    if qi is None or pi is None:
        raise Unreadable(f"{REGISTRY}: queue_id / path sütunu yok")
    vocab: dict[str, dict[str, str]] = {}
    vt = table(git(root, "show", f"{sha}:{STATES}"))
    a_, b_, c_ = vt.index("queue_id"), vt.index("state"), vt.index("class")
    for r in vt.rows:
        if a_ is not None and b_ is not None and len(r) > max(a_, b_):
            vocab.setdefault(r[a_], {})[r[b_]] = r[c_] if c_ is not None and len(r) > c_ else ""
    count: dict[str, int] = {}
    ages: dict[str, list[int]] = {}
    oldest, latest, order = [], {}, 0
    closed_states, bad_since, future_since, repeats, queues = set(), 0, 0, 0, 0

    def day_of(since: str) -> dt.date | None:
        try:
            return dt.date.fromisoformat(since) if len(since) == 10 else None
        except ValueError:
            return None
    for row in t.rows:
        if len(row) <= max(qi, pi) or not row[pi]:
            continue
        queues += 1
        known = vocab.get(row[vi]) if vi is not None and len(row) > vi else None
        q = table(git(root, "show", f"{sha}:{row[pi]}"))           # a listed queue that cannot be read stops the run
        i, s, d = q.index("id"), q.index("state"), q.index("since")
        if i is None or s is None or d is None:
            raise Unreadable(f"{row[pi]}@{sha[:12]}: id / state / since sütunu yok")
        for r in q.rows:
            if len(r) <= max(i, s, d) or not r[i]:
                continue
            order += 1
            since = r[d].strip()
            key = (day_of(since) or dt.date.min, order)              # the latest since date wins, then the later row
            if r[i] in latest:
                repeats += 1
                if key <= latest[r[i]][0]:
                    continue
            latest[r[i]] = (key, row[qi], known, r[s], since)
    for wp, (_key, queue_id, known, state, since) in latest.items():
        if known and known.get(state) == "closed":
            closed_states.add(state)
            continue
        bucket = state if not known or state in known else UNDEFINED
        count[bucket] = count.get(bucket, 0) + 1
        day = day_of(since)
        if day is None:
            bad_since += 1
        elif day > end.date():
            future_since += 1
        else:
            age = (end.date() - day).days
            ages.setdefault(bucket, []).append(age)
            if bucket != UNDEFINED:
                oldest.append((age, wp, queue_id, bucket))
    return {"snapshot": sha, "snapshot_time": when.isoformat(), "cutoff_utc": end.isoformat(), "queues": queues,
            "closed_states": sorted(closed_states), "bad_since": bad_since, "future_since": future_since,
            "repeats": repeats,
            "open": {k: {"count": count[k], "age_median_d": med(ages.get(k, [])),
                         "age_max_d": max(ages[k]) if ages.get(k) else None} for k in sorted(count)},
            "oldest": [{"wp": w, "queue": qn, "state": st, "age_d": ag}
                       for ag, w, qn, st in sorted(oldest, key=lambda x: (-x[0], x[1]))[:5]]}


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
    L += ["", f"İş paketleri nerede bekliyor ({q['queues']} kuyruk; ana dal {q['snapshot'][:12]} @ {q['snapshot_time']}, "
              f"kesim {q['cutoff_utc']}) — {BASIS['queue_time']}",
          f"durum | iş paketi | bekleme medyan | en eski  ('closed' sınıfındaki "
          f"{', '.join(q.get('closed_states') or []) or 'durumlar'} sayılmaz)"]
    L += [f"{k} | {v['count']} | {fmt(v['age_median_d'], 'gün')} | {fmt(v['age_max_d'], 'gün')}" for k, v in q["open"].items()] \
        or [f"— | {NO_DATA} | {NO_DATA} | {NO_DATA}"]
    L += ["", "En uzun bekleyen 5 iş (tanımsız durumlar hariç)"]
    L += [f"{o['wp']} · {o['queue']} · {o['state']} · {o['age_d']} gün" for o in q["oldest"]] or [NO_DATA]
    L += [f"Veri kalitesi: since tarihi okunamayan {q['bad_since']} iş ve kesimden sonra olan {q['future_since']} iş "
          f"(bunlara yaş verilmez); aynı işin ek satırı {q['repeats']} (en yeni since tarihli satır geçerli)"]
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
    end = min(dt.datetime.combine(weeks[-1][1], dt.time(23, 59, 59), dt.timezone.utc), now)
    try:
        prs, points = fetch_prs(weeks[0][0])
        runs, pages = fetch_runs(weeks)
        pr = [pr_stats(prs, w) for w in weeks]
        ci = [run_stats(runs, w) for w in weeks]
        q = queue_snapshot(str(ROOT), a.ref, end)
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
    q_empty = {"snapshot": "0" * 40, "snapshot_time": "x", "cutoff_utc": "x", "queues": 1, "closed_states": [],
               "bad_since": 0, "future_since": 0, "repeats": 0, "open": {}, "oldest": []}
    lines = render([week], [empty], [run_stats([], week)], q_empty, "test", week)
    rows = [l for l in lines if l.startswith(f"{week[0]:%d.%m}")]
    cases.append(("sabotage render (Codex): no data anywhere reads 'veri yok' in every cell of the weekly rows and the "
                  "queue tables, never a 0",
                  len(rows) == 2 and rows[0].count(NO_DATA) == 8 and rows[1].count(NO_DATA) == 3
                  and not any(" 0 " in l or "0/0/0" in l or " 0.0 " in l for l in rows)
                  and f"— | {NO_DATA} | {NO_DATA} | {NO_DATA}" in lines and NO_DATA in lines))
    # the queue snapshot: the towers' own since dates, on main at the cut-off
    with tempfile.TemporaryDirectory() as td:
        env = {**os.environ, "GIT_AUTHOR_NAME": "t", "GIT_AUTHOR_EMAIL": "t@x", "GIT_COMMITTER_NAME": "t",
               "GIT_COMMITTER_EMAIL": "t@x"}
        g = lambda *a, when=None: subprocess.run(["git", "-C", td, *a], capture_output=True, check=True,
                                                 env={**env, **({"GIT_COMMITTER_DATE": when, "GIT_AUTHOR_DATE": when} if when else {})})
        q1, q2 = "execution/q1/CT-QUEUE.tsv", "execution/q2/CT-QUEUE.tsv"
        for d in ("execution/delivery", "execution/q1", "execution/q2"):
            Path(td, d).mkdir(parents=True)
        reg = "queue_id\tpath\tvocabulary\nq1\t" + q1 + "\tv1\nq2\t" + q2 + "\t\n"
        Path(td, REGISTRY).write_text(reg, encoding="utf-8")
        Path(td, STATES).write_text("queue_id\tstate\tclass\n" + "".join(f"v1\t{s_}\t{c}\n" for s_, c in (
            ("PLANNED", "planned"), ("READY", "dispatchable"), ("DISPATCHED", "active"), ("ACCEPTED", "closed"))),
            encoding="utf-8")
        put = lambda path, rows_: Path(td, path).write_text(
            "id\titem\tstate\tsince\n" + "".join(f"{w}\tx\t{st}\t{sd}\n" for w, st, sd in rows_), encoding="utf-8")
        put(q1, [("WP-1", "READY", "2026-11-01"), ("WP-2", "ACCEPTED", "2026-11-02"), ("WP-9", "READY", "2026-11-02"),
                 ("WP-3", "Bekliyor — notlar burada", "2026-10-01"), ("WP-4", "PLANNED", "geçen hafta"),
                 ("WP-8", "PLANNED", "2026-10-20"), ("WP-8", "READY", "2026-11-03"),      # a row appended per change
                 ("WP-9", "PLANNED", "2026-10-25")])                                       # an older row added later
        put(q2, [("WP-5", "PLANNED", "2026-10-20"), ("WP-1", "READY", "2026-11-01"), ("WP-7", "READY", "2026-11-09")])
        g("init", "-q", "-b", "main")
        g("add", "-A")
        g("commit", "-q", "-m", "c1", when="2026-11-04T09:00:00+00:00")
        c1 = git(td, "rev-parse", "HEAD").strip()
        put(q1, [("WP-1", "DISPATCHED", "2026-11-06"), ("WP-2", "ACCEPTED", "2026-11-02"),
                 ("WP-3", "Bekliyor — notlar burada", "2026-10-01"), ("WP-4", "PLANNED", "geçen hafta"),
                 ("WP-6", "PLANNED", "2026-11-06")])
        g("commit", "-q", "-am", "c2", when="2026-11-06T09:00:00+00:00")       # after the cut-off
        end = dt.datetime(2026, 11, 5, 10, tzinfo=dt.timezone.utc)
        qs = attempt(queue_snapshot, td, "main", end)
        ok = isinstance(qs, dict)
        op = qs["open"] if ok else {}
        cases.append(("sabotage queue: main is read as it was at the cut-off — the commit of 4 Nov, not the later one "
                      "(WP-1 still READY, WP-6 not there)", ok and qs["snapshot"] == c1 and "DISPATCHED" not in op))
        cases.append(("queue: age in days from the tower's own since date — READY: 4 waiting, ages 4, 3, 2 days (WP-7's "
                      "date lies after the cut-off: counted, no age)",
                      ok and op.get("READY") == {"count": 4, "age_median_d": 3, "age_max_d": 4} and qs["future_since"] == 1))
        cases.append(("sabotage queue: a since date that cannot be read gives no age and is counted — PLANNED: WP-4 "
                      "(no age) and WP-5 of a queue without vocabulary (16 days)",
                      ok and op.get("PLANNED") == {"count": 2, "age_median_d": 16, "age_max_d": 16} and qs["bad_since"] == 1))
        cases.append(("sabotage queue: a 'closed'-class state waits for nothing (WP-2 ACCEPTED not listed)",
                      ok and "ACCEPTED" not in op and qs["closed_states"] == ["ACCEPTED"]))
        cases.append(("sabotage queue: free text in a state cell is one '(tanımsız durum)' bucket",
                      ok and op.get(UNDEFINED, {}).get("count") == 1 and not any("notlar" in k for k in op)))
        cases.append(("sabotage queue: a work package with several rows counts once, by its row with the latest since "
                      "date — WP-8's appended READY row (2 days), WP-9's READY row although an older PLANNED row comes "
                      "after it, WP-1 in two queues; 3 extra rows counted",
                      ok and qs["repeats"] == 3 and "WP-8" in [o["wp"] for o in qs["oldest"]]
                      and next(o for o in qs["oldest"] if o["wp"] == "WP-8")["state"] == "READY"
                      and next(o for o in qs["oldest"] if o["wp"] == "WP-9")["state"] == "READY"))
        cases.append(("queue: the five oldest, undefined states left out, oldest first (WP-5 16, WP-1 4, WP-9 3, WP-8 2)",
                      ok and [o["wp"] for o in qs["oldest"]] == ["WP-5", "WP-1", "WP-9", "WP-8"]))
        lines = render([week], [s], [r], qs if ok else q_empty, "test", week)
        cases.append(("render (Codex): where an age comes from, main's commit read and the UTC cut-off are written",
                      ok and any("'since' tarihinden" in l and c1[:12] in l and "kesim 2026-11-05T10:00:00+00:00" in l
                                 for l in lines)))
        cases.append(("sabotage queue: no commit on main before the cut-off stops the run (exit 2)",
                      isinstance(attempt(queue_snapshot, td, "main", dt.datetime(2026, 11, 1, tzinfo=dt.timezone.utc)),
                                 Unreadable)))
        g("checkout", "-q", "-b", "bad", c1)
        Path(td, REGISTRY).write_text(reg + "q3\texecution/q3/CT-QUEUE.tsv\t\n", encoding="utf-8")
        g("commit", "-q", "-am", "a listed queue without its file", when="2026-11-04T10:00:00+00:00")
        cases.append(("sabotage queue: a listed queue whose file cannot be read stops the run (exit 2), never a skipped "
                      "queue", isinstance(attempt(queue_snapshot, td, "bad", end), Unreadable)))
        g("checkout", "-q", "-b", "nosince", c1)
        Path(td, q2).write_text("id\titem\tstate\nWP-5\tx\tPLANNED\n", encoding="utf-8")
        g("commit", "-q", "-am", "no since column", when="2026-11-04T10:00:00+00:00")
        cases.append(("sabotage queue: a queue without its since column stops the run (exit 2)",
                      isinstance(attempt(queue_snapshot, td, "nosince", end), Unreadable)))
        cases.append(("sabotage queue: an unreadable ref stops the run (exit 2), never an empty table",
                      isinstance(attempt(queue_snapshot, td, "no-such-ref", end), Unreadable)))
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
