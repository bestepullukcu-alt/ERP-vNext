#!/usr/bin/env python3
"""EES run record from session transcripts - what was done, how long it took (work vs waiting), what it cost.

    python3 scripts/ees_run_record.py --transcript <session.jsonl> [--transcript <other.jsonl> ...]
        [--pr <n> ...] [--repo <owner/name>] [--idle-minutes 5] [--format md|json]
        [--since <ISO>] [--until <ISO>]

Reads Claude Code session transcripts (JSONL). For each one it also reads <file without .jsonl>/subagents/*.jsonl when that
folder exists. Output is Markdown (Turkish headings) or one JSON object (--format json): summary, steps, waits,
sub-agents, rule files read and - only with --pr - the pull requests. The tool is read-only; without --pr it makes no
network call (core card C29: operation records are generated, not hand-written).

Privacy: the output never carries message text, command arguments, file contents, logins or names. Tool names are
printed; for Bash at most the command word and, for well-known tools such as git or gh, one subcommand word
("Bash: git push"). Paths are printed only for rule files (.ees/, .antigravity/rules/, AGENTS.md). Step names come from
the `title` input of mark_chapter calls, nothing else. Review authors are never read: the first reviewer is "onaylayici".

Definitions:
- A human message is a `user` event that is not meta, not a side chain, not a tool result, and not an automated
  notice (system reminders, task notifications, local commands, caveats, session or CI events, sub-agent hand-backs,
  messages from another session, "[SYSTEM NOTIFICATION"). Each human message starts a step ("Tur n"); the first
  mark_chapter title called inside the step names it.
- Active time is the sum of gaps between consecutive events that are shorter than --idle-minutes; the rest of the wall
  clock is waiting. Events of the main chat and its sub-agents share one timeline, so a long sub-agent run is work.
- Every gap at or above the threshold is classified: "izin ya da uzun komut" (it ends with the tool result of a call
  made earlier; the tool is named), "kisi bekleniyor" (it ends with a human message), otherwise "diger".
- Each assistant message id is counted once (the last streamed line wins for usage). Tool calls are counted once per
  tool_use id. token_girdi = input + cache creation + cache read; token_cikti = output.
- With several transcripts the summary uses one merged timeline: sessions that ran at the same time are not added up.

Window line (the "Bu adim" report line): the tool prints it in the summary as `Bu adim: <is> is + <bekleme> bekleme ·
<token> · <n> dokunus`. For one step, run the tool with --since set to the moment the step started.

Python 3.9+, standard library only.
"""
from __future__ import annotations

import argparse
import bisect
import json
import os
import re
import subprocess
import sys
from collections import Counter
from datetime import datetime, timedelta, timezone

TOKEN_KINDS = (
    ("girdi", "input_tokens"),
    ("onbellek_yazma", "cache_creation_input_tokens"),
    ("onbellek_okuma", "cache_read_input_tokens"),
    ("cikti", "output_tokens"),
)

NON_HUMAN_PREFIXES = (
    "<system-reminder>",
    "<task-notification",
    "<local-command",
    "Caveat:",
    "<side-sessions-event",
    "<ci-monitor-event",
    "[SYSTEM NOTIFICATION",
)
NON_HUMAN_CONTAINS = ("[Subagent hand-back]", "Another Claude session sent a message")

WAIT_TOOL = "izin ya da uzun komut"
WAIT_HUMAN = "kişi bekleniyor"
WAIT_OTHER = "diğer"

# Commands whose second word is a subcommand and may be shown ("Bash: git push").
SUBCOMMAND_TOOLS = {
    "git", "gh", "npm", "npx", "yarn", "pnpm", "dotnet", "docker", "kubectl", "pip", "pip3", "make", "cargo", "brew",
}

RULE_RE = re.compile(
    r"(\.ees/[^\s'\"`;|)&<>(]+|\.antigravity/rules/[^\s'\"`;|)&<>(]+|(?<![\w.-])AGENTS\.md(?![\w]))"
)

_TS_RE = re.compile(
    r"^(\d{4})-(\d\d)-(\d\d)(?:[T ](\d\d):(\d\d)(?::(\d\d))?(?:\.(\d+))?)?\s*(Z|[+-]\d\d:?\d\d)?$"
)


# --------------------------------------------------------------------------------------------------- time helpers
def parse_ts(value):
    """ISO-8601 text -> aware UTC datetime, or None. Naive values are taken as UTC. Works on Python 3.9."""
    if not isinstance(value, str):
        return None
    m = _TS_RE.match(value.strip())
    if not m:
        return None
    y, mo, d, hh, mi, ss, frac, tz = m.groups()
    try:
        micro = int((frac + "000000")[:6]) if frac else 0
        dt = datetime(int(y), int(mo), int(d), int(hh or 0), int(mi or 0), int(ss or 0), micro, tzinfo=timezone.utc)
    except ValueError:
        return None
    if tz and tz != "Z":
        sign = 1 if tz[0] == "+" else -1
        digits = tz[1:].replace(":", "")
        dt -= sign * timedelta(hours=int(digits[:2]), minutes=int(digits[2:4]))
    return dt


def iso(dt):
    return dt.astimezone(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ") if dt else None


def minutes(seconds):
    return round(seconds / 60.0, 1)


def fmt_dk(seconds):
    """Minutes with one decimal and a decimal comma, for tables."""
    return ("%.1f" % (seconds / 60.0)).replace(".", ",")


def fmt_dur(seconds):
    """Human duration for the report line: '45 sn', '12 dk', '3 sa 5 dk'."""
    s = int(round(seconds))
    if s < 60:
        return "%d sn" % s
    m = int(round(s / 60.0))
    if m < 120:
        return "%d dk" % m
    return "%d sa %d dk" % (m // 60, m % 60)


def fmt_int(n):
    return "{:,}".format(int(n)).replace(",", ".")


def fmt_tok(n):
    n = int(n)
    if n < 1000:
        return "%d token" % n
    if n < 1000000:
        return ("%.1f bin token" % (n / 1000.0)).replace(".", ",")
    return ("%.1f milyon token" % (n / 1000000.0)).replace(".", ",")


# ------------------------------------------------------------------------------------------------ text helpers
def clean_cell(text, limit=60):
    t = re.sub(r"\s+", " ", str(text or "")).replace("|", "/").strip()
    return t[:limit]


def bash_label(command):
    """'Bash' plus at most the command word and one well-known subcommand word. Never other arguments."""
    if not isinstance(command, str):
        return "Bash"
    segments = re.split(r"&&|\|\||;|\||\n", command)
    for seg in segments:
        words = seg.strip().split()
        while words and re.match(r"^[A-Za-z_][A-Za-z0-9_]*=", words[0]):
            words = words[1:]
        if not words or words[0] in ("cd", "export", "set", "source", "pushd", "popd", "true", ":"):
            continue
        first = words[0].replace("\\", "/").rsplit("/", 1)[-1]
        if not re.match(r"^[A-Za-z0-9._+-]+$", first):
            return "Bash"
        if first in SUBCOMMAND_TOOLS:
            rest = words[1:]
            i = 0
            while i < len(rest) and rest[i].startswith("-"):
                i += 2 if rest[i] in ("-C", "-c", "--git-dir", "--work-tree") else 1
            if i < len(rest) and re.match(r"^[a-z][a-z0-9-]*$", rest[i]):
                return "Bash: %s %s" % (first, rest[i])
        return "Bash: %s" % first
    return "Bash"


def rule_paths(text):
    if not isinstance(text, str):
        return []
    found = []
    for m in RULE_RE.finditer(text.replace("\\", "/")):
        p = m.group(1).rstrip(".,:;")
        if p:
            found.append(p)
    return found


def is_human_content(content):
    """True when a user event's content is something a person typed."""
    if isinstance(content, str):
        parts = [content]
        has_image = False
    elif isinstance(content, list):
        parts = [c.get("text", "") for c in content if isinstance(c, dict) and c.get("type") == "text"]
        has_image = any(isinstance(c, dict) and c.get("type") == "image" for c in content)
    else:
        return False
    parts = [p for p in parts if isinstance(p, str) and p.strip()]
    joined = " ".join(parts)
    if any(marker in joined for marker in NON_HUMAN_CONTAINS):
        return False
    kept = [p for p in parts if not p.strip().startswith(NON_HUMAN_PREFIXES)]
    return bool(kept) or (has_image and not parts)


# ------------------------------------------------------------------------------------------------------ parsing
def _usage_ints(usage):
    out = {}
    for key, src in TOKEN_KINDS:
        v = usage.get(src) if isinstance(usage, dict) else 0
        out[key] = v if isinstance(v, int) and not isinstance(v, bool) else 0
    return out


def parse_file(path, since, until, is_main):
    """One transcript file -> events, message usage (once per id), tool uses (once per id), humans, chapters, reads."""
    res = {"events": [], "messages": {}, "tool_uses": {}, "humans": [], "chapters": [], "reads": Counter()}
    anon = 0
    with open(path, encoding="utf-8", errors="replace") as fh:
        for line in fh:
            line = line.strip()
            if not line:
                continue
            try:
                e = json.loads(line)
            except ValueError:
                continue
            if not isinstance(e, dict):
                continue
            etype = e.get("type")
            if etype not in ("user", "assistant"):
                continue
            ts = parse_ts(e.get("timestamp"))
            if ts is None or (since and ts < since) or (until and ts > until):
                continue
            msg = e.get("message") if isinstance(e.get("message"), dict) else {}
            content = msg.get("content")
            if etype == "assistant":
                mid = msg.get("id")
                if not mid:
                    anon += 1
                    mid = "anon-%d" % anon
                rec = res["messages"].get(mid)
                if rec is None:
                    rec = res["messages"][mid] = {"ts": ts, "model": None, "usage": _usage_ints(None)}
                if msg.get("model"):
                    rec["model"] = msg.get("model")
                if isinstance(msg.get("usage"), dict):
                    rec["usage"] = _usage_ints(msg["usage"])  # last line wins
                if isinstance(content, list):
                    for item in content:
                        if not isinstance(item, dict) or item.get("type") != "tool_use":
                            continue
                        tid = item.get("id")
                        if not tid:
                            anon += 1
                            tid = "anon-tool-%d" % anon
                        if tid in res["tool_uses"]:
                            continue
                        name = item.get("name") if isinstance(item.get("name"), str) else "?"
                        inp = item.get("input") if isinstance(item.get("input"), dict) else {}
                        use = {"name": name, "ts": ts, "label": name, "subagent_type": None}
                        if name in ("Agent", "Task") and isinstance(inp.get("subagent_type"), str):
                            use["subagent_type"] = clean_cell(inp["subagent_type"], 40)
                        if name == "Bash":
                            use["label"] = bash_label(inp.get("command"))
                            for p in rule_paths(inp.get("command")):
                                res["reads"][p] += 1
                        elif name == "Read":
                            for p in rule_paths(inp.get("file_path")):
                                res["reads"][p] += 1
                        if name.endswith("mark_chapter") and isinstance(inp.get("title"), str) and inp["title"].strip():
                            res["chapters"].append((ts, clean_cell(inp["title"])))
                        res["tool_uses"][tid] = use
                res["events"].append((ts, "assistant" if is_main else "sub", None))
                continue
            # user event
            kind, payload = "other", None
            if is_main:
                ids = []
                if isinstance(content, list):
                    ids = [c.get("tool_use_id") for c in content if isinstance(c, dict) and c.get("type") == "tool_result"]
                if ids:
                    kind, payload = "result", [i for i in ids if isinstance(i, str)]
                elif not e.get("isMeta") and not e.get("isSidechain") and is_human_content(content):
                    kind = "human"
                    res["humans"].append(ts)
            else:
                kind = "sub"
            res["events"].append((ts, kind, payload))
    return res


def find_subagent_files(main_path):
    base = os.path.splitext(main_path)[0]
    folder = os.path.join(base, "subagents")
    if not os.path.isdir(folder):
        return []
    out = []
    for name in sorted(os.listdir(folder)):
        if name.endswith(".jsonl"):
            p = os.path.join(folder, name)
            meta = {}
            mp = os.path.join(folder, name[: -len(".jsonl")] + ".meta.json")
            if os.path.isfile(mp):
                try:
                    with open(mp, encoding="utf-8") as fh:
                        loaded = json.load(fh)
                    if isinstance(loaded, dict):
                        meta = loaded
                except (OSError, ValueError):
                    meta = {}
            out.append((p, meta))
    return out


# ---------------------------------------------------------------------------------------------------- analysis
def tokens_by_model(messages):
    out = {}
    for rec in messages:
        model = rec["model"] or "bilinmiyor"
        slot = out.setdefault(model, {"mesaj": 0, "girdi": 0, "onbellek_yazma": 0, "onbellek_okuma": 0, "cikti": 0})
        slot["mesaj"] += 1
        for key, _ in TOKEN_KINDS:
            slot[key] += rec["usage"][key]
    return out


def add_token_maps(*maps):
    out = {}
    for m in maps:
        for model, slot in m.items():
            tgt = out.setdefault(model, {"mesaj": 0, "girdi": 0, "onbellek_yazma": 0, "onbellek_okuma": 0, "cikti": 0})
            for k, v in slot.items():
                tgt[k] += v
    return out


def token_total(slot_map):
    return sum(slot[k] for slot in slot_map.values() for k, _ in TOKEN_KINDS)


def active_waiting(times, threshold_s):
    times = sorted(times)
    active = waiting = 0.0
    for a, b in zip(times, times[1:]):
        gap = (b - a).total_seconds()
        if gap < threshold_s:
            active += gap
        else:
            waiting += gap
    return active, waiting


def analyze_session(index, path, since, until, threshold_s):
    main = parse_file(path, since, until, True)
    subs = []
    sub_events = []
    parent_uses = main["tool_uses"]
    by_tool_id = {tid: u for tid, u in parent_uses.items()}
    for sp, meta in find_subagent_files(path):
        sres = parse_file(sp, since, until, False)
        if not sres["events"]:
            continue
        stem = os.path.splitext(os.path.basename(sp))[0]
        parent = by_tool_id.get(meta.get("toolUseId")) if isinstance(meta.get("toolUseId"), str) else None
        agent_type = None
        if parent and parent.get("subagent_type"):
            agent_type = parent["subagent_type"]
        elif isinstance(meta.get("agentType"), str) and meta["agentType"]:
            agent_type = clean_cell(meta["agentType"], 40)
        else:
            agent_type = stem
        ts_all = [t for t, _, _ in sres["events"]]
        models = Counter((r["model"] or "bilinmiyor") for r in sres["messages"].values())
        tool_counts = Counter(u["name"] for u in sres["tool_uses"].values())
        subs.append({
            "dosya": stem,
            "tur": agent_type,
            "model": ", ".join(m for m, _ in models.most_common()),
            "ilk": min(ts_all),
            "son": max(ts_all),
            "token": tokens_by_model(list(sres["messages"].values())),
            "mesajlar": list(sres["messages"].values()),
            "arac_cagrilari": tool_counts,
            "okunan": sres["reads"],
        })
        sub_events.extend(sres["events"])

    events = sorted(main["events"] + sub_events, key=lambda ev: ev[0])
    human_ts = sorted(main["humans"])

    # steps: one per human message; events before the first one form a "Başlangıç" step.
    starts = list(human_ts)
    names = []
    for n in range(1, len(human_ts) + 1):
        names.append("Tur %d" % n)
    if events and (not human_ts or events[0][0] < human_ts[0]):
        starts.insert(0, events[0][0])
        names.insert(0, "Başlangıç" if human_ts else "Tur 1")
    steps = [
        {"ad": names[i], "baslangic": starts[i], "is_s": 0.0, "bekleme_s": 0.0, "mesaj": 0, "arac": 0, "alt_ajan": 0,
         "cikti": 0, "token": 0}
        for i in range(len(starts))
    ]

    def step_of(ts):
        if not steps:
            return None
        return steps[max(0, bisect.bisect_right(starts, ts) - 1)]

    for ts, title in sorted(main["chapters"], key=lambda c: c[0]):
        st = step_of(ts)
        if st is not None and not st.get("ad_baslik"):
            st["ad_baslik"] = title
    for st in steps:
        if st.get("ad_baslik"):
            st["ad"] = st["ad_baslik"]

    for rec in main["messages"].values():
        st = step_of(rec["ts"])
        if st is not None:
            st["mesaj"] += 1
            st["cikti"] += rec["usage"]["cikti"]
            st["token"] += sum(rec["usage"].values())
    for sub in subs:
        for rec in sub["mesajlar"]:
            st = step_of(rec["ts"])
            if st is not None:
                st["token"] += sum(rec["usage"].values())
    for use in main["tool_uses"].values():
        st = step_of(use["ts"])
        if st is not None:
            st["arac"] += 1
            if use["name"] in ("Agent", "Task"):
                st["alt_ajan"] += 1

    waits = []
    for (a_ts, a_kind, _), (b_ts, b_kind, b_payload) in zip(events, events[1:]):
        gap = (b_ts - a_ts).total_seconds()
        st = step_of(a_ts)
        if gap < threshold_s:
            if st is not None:
                st["is_s"] += gap
            continue
        if st is not None:
            st["bekleme_s"] += gap
        tool = None
        if b_kind == "human":
            klass = WAIT_HUMAN
        elif b_kind == "result" and b_payload:
            klass = WAIT_TOOL
            for tid in b_payload:
                if tid in parent_uses:
                    tool = parent_uses[tid]["label"]
                    break
        else:
            klass = WAIT_OTHER
        waits.append({
            "oturum": index, "sure_s": gap, "tur": klass, "arac": tool, "baslangic": a_ts, "bitis": b_ts,
            "adim": st["ad"] if st else None,
        })

    return {
        "index": index,
        "ad": os.path.splitext(os.path.basename(path))[0][:8],
        "main": main,
        "subs": subs,
        "events": events,
        "steps": steps,
        "waits": waits,
        "main_tokens": tokens_by_model(list(main["messages"].values())),
        "sub_tokens": add_token_maps(*[s["token"] for s in subs]),
    }


def build_data(transcripts, prs, repo, idle_minutes, since, until, gh=None):
    threshold_s = idle_minutes * 60.0
    sessions = [analyze_session(i + 1, p, since, until, threshold_s) for i, p in enumerate(transcripts)]
    all_times = [t for s in sessions for t, _, _ in s["events"]]
    first = min(all_times) if all_times else None
    last = max(all_times) if all_times else None
    wall = (last - first).total_seconds() if first else 0.0
    active, waiting = active_waiting(all_times, threshold_s)

    main_tools = Counter()
    sub_tools = Counter()
    for s in sessions:
        main_tools.update(u["name"] for u in s["main"]["tool_uses"].values())
        for sub in s["subs"]:
            sub_tools.update(sub["arac_cagrilari"])
    main_tokens = add_token_maps(*[s["main_tokens"] for s in sessions])
    sub_tokens = add_token_maps(*[s["sub_tokens"] for s in sessions])
    all_tokens = add_token_maps(main_tokens, sub_tokens)
    humans = sum(len(s["main"]["humans"]) for s in sessions)
    t_total = token_total(all_tokens)
    t_in = sum(slot[k] for slot in all_tokens.values() for k in ("girdi", "onbellek_yazma", "onbellek_okuma"))
    t_out = sum(slot["cikti"] for slot in all_tokens.values())
    line = "Bu adım: %s iş + %s bekleme · %s · %d dokunuş" % (fmt_dur(active), fmt_dur(waiting), fmt_tok(t_total), humans)

    data = {
        "ozet": {
            "ilk": iso(first), "son": iso(last),
            "duvar_saati_dk": minutes(wall), "is_dk": minutes(active), "bekleme_dk": minutes(waiting),
            "sure_dk": minutes(wall), "bosta_esigi_dk": idle_minutes,
            "insan_dokunusu": humans,
            "oturum_sayisi": len(sessions),
            "arac_cagrilari_ana": dict(main_tools.most_common()),
            "arac_cagrilari_alt_ajan": dict(sub_tools.most_common()),
            "token_ana": main_tokens, "token_alt_ajan": sub_tokens, "token_toplam": all_tokens,
            "token_girdi": t_in, "token_cikti": t_out, "token_hepsi": t_total,
            "rapor_satiri": line,
        },
        "oturumlar": [],
        "beklemeler": [],
        "prler": None,
    }
    for s in sessions:
        sess = {
            "no": s["index"], "ad": s["ad"],
            "adimlar": [
                {
                    "ad": st["ad"], "baslangic": iso(st["baslangic"]), "is_dk": minutes(st["is_s"]),
                    "bekleme_dk": minutes(st["bekleme_s"]), "asistan_iletisi": st["mesaj"], "arac_cagrisi": st["arac"],
                    "alt_ajan_baslatildi": st["alt_ajan"], "cikti_token": st["cikti"], "token_toplam": st["token"],
                }
                for st in s["steps"]
            ],
            "alt_ajanlar": [
                {
                    "dosya": sub["dosya"], "tur": sub["tur"], "model": sub["model"],
                    "sure_dk": minutes((sub["son"] - sub["ilk"]).total_seconds()),
                    "token": sub["token"], "arac_cagrisi": sum(sub["arac_cagrilari"].values()),
                    "arac_cagrilari": dict(sub["arac_cagrilari"].most_common()),
                }
                for sub in s["subs"]
            ],
            "okunan_kural_dosyalari": {
                "ana": dict(sorted(s["main"]["reads"].items())),
                "alt_ajanlar": {
                    "%s (%s)" % (sub["tur"], sub["dosya"]): dict(sorted(sub["okunan"].items())) for sub in s["subs"]
                },
            },
        }
        data["oturumlar"].append(sess)
    waits = sorted((w for s in sessions for w in s["waits"]), key=lambda w: -w["sure_s"])
    data["beklemeler"] = [
        {
            "oturum": w["oturum"], "sure_dk": minutes(w["sure_s"]), "tur": w["tur"], "arac": w["arac"],
            "baslangic": iso(w["baslangic"]), "bitis": iso(w["bitis"]), "adim": w["adim"],
        }
        for w in waits
    ]
    if prs:
        getter = gh or default_gh
        data["prler"] = [pr_summary(n, repo, getter) for n in prs]
    return data


# --------------------------------------------------------------------------------------------- pull requests
def default_gh(repo, number):
    cmd = ["gh", "pr", "view", str(number)]
    if repo:
        cmd += ["--repo", repo]
    cmd += ["--json", "createdAt,mergedAt,closedAt,isDraft,reviews,commits,statusCheckRollup"]
    proc = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, universal_newlines=True, timeout=90)
    if proc.returncode != 0:
        raise RuntimeError("gh pr view failed (exit %d)" % proc.returncode)
    return json.loads(proc.stdout)


PASS_STATES = {"SUCCESS", "NEUTRAL", "SKIPPED"}
FAIL_STATES = {"FAILURE", "TIMED_OUT", "CANCELLED", "ACTION_REQUIRED", "STARTUP_FAILURE", "ERROR", "STALE"}
REVIEW_TR = {
    "APPROVED": "onaylandı", "CHANGES_REQUESTED": "değişiklik istendi", "COMMENTED": "yorum yazıldı",
    "DISMISSED": "geçersiz kılındı", "PENDING": "beklemede",
}


def pr_summary(number, repo, getter):
    try:
        raw = getter(repo, number)
    except Exception as exc:  # network, auth, missing gh: report the kind only, never the message
        return {"no": number, "hata": "okunamadı (%s)" % type(exc).__name__}
    if not isinstance(raw, dict):
        return {"no": number, "hata": "okunamadı (biçim)"}
    created = parse_ts(raw.get("createdAt"))
    merged = parse_ts(raw.get("mergedAt"))
    closed = parse_ts(raw.get("closedAt"))
    out = {"no": number, "taslak": bool(raw.get("isDraft")), "acildi": iso(created)}
    reviews = []
    for r in raw.get("reviews") or []:
        if isinstance(r, dict):
            t = parse_ts(r.get("submittedAt"))
            if t is not None:
                reviews.append((t, str(r.get("state") or "")))
    reviews.sort(key=lambda x: x[0])
    out["inceleme_sayisi"] = len(raw.get("reviews") or [])
    if reviews and created:
        t, state = reviews[0]
        out["ilk_inceleme"] = {
            "kim": "onaylayici", "durum": state, "durum_tr": REVIEW_TR.get(state, state.lower()),
            "acilistan_sonra_dk": minutes((t - created).total_seconds()),
        }
    else:
        out["ilk_inceleme"] = None
    out["birlesti"] = iso(merged)
    out["kapandi"] = iso(closed) if closed and not merged else None
    out["durum"] = "birleşti" if merged else ("kapandı" if closed else "açık")
    checks = [c for c in (raw.get("statusCheckRollup") or []) if isinstance(c, dict)]
    passed = failed = 0
    done_times = []
    for c in checks:
        state = str(c.get("conclusion") or c.get("state") or "").upper()
        if state in PASS_STATES:
            passed += 1
        elif state in FAIL_STATES:
            failed += 1
        done = parse_ts(c.get("completedAt"))
        if done is not None:
            done_times.append(done)
    out["kontrol"] = {"toplam": len(checks), "gecen": passed, "kalan": failed, "bekleyen": len(checks) - passed - failed}
    commit_times = []
    for c in raw.get("commits") or []:
        if isinstance(c, dict):
            t = parse_ts(c.get("committedDate") or c.get("authoredDate"))
            if t is not None:
                commit_times.append(t)
    out["son_commit_kontrol_bitis_dk"] = None
    out["acilistan_kontrol_bitis_dk"] = None
    if checks and len(done_times) == len(checks):
        if commit_times:
            delta = (max(done_times) - max(commit_times)).total_seconds()
            if delta >= 0:
                out["son_commit_kontrol_bitis_dk"] = minutes(delta)
        if created and max(done_times) >= created:
            out["acilistan_kontrol_bitis_dk"] = minutes((max(done_times) - created).total_seconds())
    return out


# --------------------------------------------------------------------------------------------------- render
def table(headers, rows):
    lines = ["| " + " | ".join(headers) + " |", "|" + "|".join("---" for _ in headers) + "|"]
    for r in rows:
        lines.append("| " + " | ".join(str(c) for c in r) + " |")
    return lines


def tokens_rows(label, slot_map):
    rows = []
    for model, s in sorted(slot_map.items()):
        rows.append([label, clean_cell(model, 50), fmt_int(s["mesaj"]), fmt_int(s["girdi"]), fmt_int(s["onbellek_yazma"]),
                     fmt_int(s["onbellek_okuma"]), fmt_int(s["cikti"])])
    return rows


def count_line(counter):
    return " · ".join("%s %d" % (k, v) for k, v in counter.items()) or "yok"


def render_md(data):
    o = data["ozet"]
    multi = o["oturum_sayisi"] > 1
    out = ["# EES çalıştırma kaydı (oturum dökümlerinden üretildi)", ""]
    out += ["## Özet", ""]
    out += [
        "- İlk olay: %s · son olay: %s" % (o["ilk"] or "yok", o["son"] or "yok"),
        "- Duvar saati: %s dk · iş (aktif): %s dk · bekleme: %s dk (boşta eşiği %s dk)"
        % (str(o["duvar_saati_dk"]).replace(".", ","), str(o["is_dk"]).replace(".", ","),
           str(o["bekleme_dk"]).replace(".", ","), str(o["bosta_esigi_dk"]).replace(".", ",")),
        "- İnsan dokunuşu (insan mesajı): %d" % o["insan_dokunusu"],
        "- Araç çağrıları (ana sohbet): %s" % count_line(Counter(o["arac_cagrilari_ana"])),
        "- Araç çağrıları (alt ajanlar): %s" % count_line(Counter(o["arac_cagrilari_alt_ajan"])),
    ]
    if multi:
        out.append("- %d oturum tek zaman çizelgesinde birleştirildi; aynı anda çalışan oturumların süresi toplanmaz."
                   % o["oturum_sayisi"])
    out.append("")
    rows = tokens_rows("Ana sohbet", o["token_ana"]) + tokens_rows("Alt ajanlar", o["token_alt_ajan"])
    tot = o["token_toplam"]
    rows.append([
        "**Toplam**", "", fmt_int(sum(s["mesaj"] for s in tot.values())),
        fmt_int(sum(s["girdi"] for s in tot.values())), fmt_int(sum(s["onbellek_yazma"] for s in tot.values())),
        fmt_int(sum(s["onbellek_okuma"] for s in tot.values())), fmt_int(sum(s["cikti"] for s in tot.values())),
    ])
    out += table(["Kapsam", "Model", "İleti", "Girdi", "Önbellek yazma", "Önbellek okuma", "Çıktı"], rows)
    out += ["", "Dört token türünün toplamı: %s (önbellek okuma bu toplamın büyük kısmını oluşturur)." % fmt_int(o["token_hepsi"]), ""]
    out += ["Rapor satırı:", "", "```", o["rapor_satiri"], "```", ""]

    out += ["## Adımlar", ""]
    for s in data["oturumlar"]:
        if multi:
            out += ["### Oturum %d (%s)" % (s["no"], s["ad"]), ""]
        rows = [
            [clean_cell(a["ad"]), a["baslangic"] or "", fmt_dk(a["is_dk"] * 60), fmt_dk(a["bekleme_dk"] * 60),
             a["asistan_iletisi"], a["arac_cagrisi"], a["alt_ajan_baslatildi"], fmt_int(a["cikti_token"]),
             fmt_int(a["token_toplam"])]
            for a in s["adimlar"]
        ]
        out += table(["Adım", "Başlangıç", "İş (dk)", "Bekleme (dk)", "Asistan iletisi", "Araç çağrısı",
                      "Alt ajan başlatıldı", "Çıktı token", "Toplam token (alt ajanlar dahil)"], rows)
        out.append("")

    out += ["## Beklemeler", ""]
    if data["beklemeler"]:
        heads = (["Oturum"] if multi else []) + ["Süre (dk)", "Tür", "Araç", "Başlangıç", "Bitiş", "Adım"]
        rows = []
        for w in data["beklemeler"]:
            rows.append(([w["oturum"]] if multi else []) + [
                str(w["sure_dk"]).replace(".", ","), w["tur"], clean_cell(w["arac"] or "-"), w["baslangic"], w["bitis"],
                clean_cell(w["adim"] or "-"),
            ])
        out += table(heads, rows)
    else:
        out.append("Eşiği aşan bekleme yok.")
    out.append("")

    out += ["## Alt ajanlar", ""]
    rows = []
    for s in data["oturumlar"]:
        for a in s["alt_ajanlar"]:
            tok = sum(sum(m[k] for k, _ in TOKEN_KINDS) for m in a["token"].values())
            out_tok = sum(m["cikti"] for m in a["token"].values())
            rows.append(([s["no"]] if multi else []) + [
                clean_cell(a["tur"]), clean_cell(a["model"], 50), fmt_dk(a["sure_dk"] * 60), fmt_int(tok),
                fmt_int(out_tok), a["arac_cagrisi"],
            ])
    if rows:
        out += table((["Oturum"] if multi else []) + ["Tür", "Model", "Süre (dk)", "Token (dört tür)", "Çıktı token",
                                                      "Araç çağrısı"], rows)
    else:
        out.append("Alt ajan yok.")
    out.append("")

    out += ["## Okunan kural dosyaları", ""]
    rows = []
    for s in data["oturumlar"]:
        k = s["okunan_kural_dosyalari"]
        for p, n in k["ana"].items():
            rows.append(([s["no"]] if multi else []) + ["ana sohbet", p, n])
        for who, paths in k["alt_ajanlar"].items():
            for p, n in paths.items():
                rows.append(([s["no"]] if multi else []) + [clean_cell(who, 80), p, n])
    if rows:
        out += table((["Oturum"] if multi else []) + ["Kim", "Yol", "Sayı"], rows)
    else:
        out.append("Kural dosyası okunmadı.")
    out.append("")

    if data["prler"] is not None:
        out += ["## PR'lar", ""]
        for p in data["prler"]:
            out.append("### PR #%s" % p["no"])
            out.append("")
            if p.get("hata"):
                out += ["- %s" % p["hata"], ""]
                continue
            ilk = p["ilk_inceleme"]
            out.append("- Açıldı: %s%s" % (p["acildi"] or "bilinmiyor", " (taslak)" if p["taslak"] else ""))
            if ilk:
                out.append("- İlk inceleme (onaylayıcı): %s · açılıştan %s dk sonra (toplam %d inceleme)"
                           % (ilk["durum_tr"], str(ilk["acilistan_sonra_dk"]).replace(".", ","), p["inceleme_sayisi"]))
            else:
                out.append("- İlk inceleme: yok")
            if p["birlesti"]:
                out.append("- Birleşti: %s" % p["birlesti"])
            elif p["kapandi"]:
                out.append("- Kapandı (birleşmeden): %s" % p["kapandi"])
            else:
                out.append("- Durum: açık")
            k = p["kontrol"]
            out.append("- Kontroller: %d toplam · %d geçti · %d kaldı · %d bekleyen/diğer"
                       % (k["toplam"], k["gecen"], k["kalan"], k["bekleyen"]))
            if p["son_commit_kontrol_bitis_dk"] is not None:
                out.append("- Son commit'ten tüm kontrollerin bitişine: %s dk"
                           % str(p["son_commit_kontrol_bitis_dk"]).replace(".", ","))
            if p["acilistan_kontrol_bitis_dk"] is not None:
                out.append("- Açılıştan tüm kontrollerin bitişine: %s dk"
                           % str(p["acilistan_kontrol_bitis_dk"]).replace(".", ","))
            out.append("")
    return "\n".join(out).rstrip() + "\n"


# ------------------------------------------------------------------------------------------------------ CLI
def build_parser():
    ap = argparse.ArgumentParser(
        description="EES run record: steps, work vs waiting time, tokens and PR data from Claude Code session transcripts.")
    ap.add_argument("--transcript", action="append", required=True, metavar="FILE.jsonl",
                    help="session transcript (repeatable); <name>/subagents/*.jsonl is read too when present")
    ap.add_argument("--pr", action="append", type=int, metavar="N", help="pull request number (repeatable)")
    ap.add_argument("--repo", metavar="OWNER/NAME", help="repository for --pr (default: the current one)")
    ap.add_argument("--idle-minutes", type=float, default=5.0, help="gaps of this length or longer are waiting (default 5)")
    ap.add_argument("--format", choices=("md", "json"), default="md")
    ap.add_argument("--since", metavar="ISO", help="ignore events before this time (UTC if no offset)")
    ap.add_argument("--until", metavar="ISO", help="ignore events after this time (UTC if no offset)")
    return ap


def main(argv=None, gh=None, stdout=None):
    args = build_parser().parse_args(argv)
    out = stdout or sys.stdout
    if args.idle_minutes <= 0:
        print("--idle-minutes must be positive", file=sys.stderr)
        return 2
    since = parse_ts(args.since) if args.since else None
    until = parse_ts(args.until) if args.until else None
    if (args.since and since is None) or (args.until and until is None):
        print("--since/--until must be ISO-8601 (e.g. 2026-11-16T14:10:00Z)", file=sys.stderr)
        return 2
    paths = [os.path.expanduser(p) for p in args.transcript]
    for p in paths:
        if not os.path.isfile(p):
            print("transcript not found: %s" % os.path.basename(p), file=sys.stderr)
            return 2
    data = build_data(paths, args.pr or [], args.repo, args.idle_minutes, since, until, gh=gh)
    if args.format == "json":
        text = json.dumps(data, ensure_ascii=False, indent=2, default=lambda o: dict(o))
    else:
        text = render_md(data)
    try:
        if hasattr(out, "reconfigure"):
            out.reconfigure(encoding="utf-8")
    except (ValueError, OSError):
        pass
    out.write(text if text.endswith("\n") else text + "\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
