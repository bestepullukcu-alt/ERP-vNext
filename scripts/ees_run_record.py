#!/usr/bin/env python3
"""EES run record from session transcripts - what was done, how long it took (work vs waiting), what it cost.

    python3 scripts/ees_run_record.py --transcript <session.jsonl> [--transcript <other.jsonl> ...]
        [--pr <n> ...] [--repo <owner/name>] [--idle-minutes 5] [--format md|json]
        [--since <ISO>] [--until <ISO>] [--adim N/M] [--tur-adlari] [--uyari-bekleme-dk N] [--uyari-dokunus N]
        [--uyari-cikti-token N] [--uyari-toplam-token N] [--uyari-ileti N] [--uyari-ajan-tekrar N] [--uyari-hata N]
        [--uyari-pr-gun N]

Reads Claude Code session transcripts (JSONL). For each one it also reads <file without .jsonl>/subagents/*.jsonl when that
folder exists. Output is Markdown (Turkish headings) or one JSON object (--format json): summary, turns, waits,
sub-agents, rule files read and - only with --pr - the pull requests. The tool is read-only; without --pr it makes no
network call (core card C29: operation records are generated, not hand-written).

Words: a "tur" (turn) is what starts with one human message; an "adim" (step) is a step of the plan and is only used
for the report line (`--adim N/M`), because the plan is not in the transcript.

Privacy: no free text reaches the output. Message text, command arguments, file contents, logins, file names and
sub-agent descriptions are never printed. Tool, model and sub-agent-type names are printed only when they are plain
identifiers ([A-Za-z0-9._:-], 1-60 characters), otherwise "?". Bash is shown as the command word of a fixed list of
well-known commands plus, for a fixed list of (command, subcommand) pairs, the subcommand ("Bash: git push"); anything
else is just "Bash". Paths are printed only for rule files (.ees/, .antigravity/rules/, AGENTS.md) and only when
every character is in [A-Za-z0-9._/-], no segment is "..", and the marker starts at a path boundary. Turn names are
"Tur n"; the `title` of mark_chapter calls is used only with --tur-adlari, and then e-mail addresses, path-like and
token-like words are removed and the text is cut to 60 characters. Review authors are never read: the first reviewer
is "onaylayici". Transcript and sub-agent files appear as "oturum-n" / "alt-ajan-n".

Definitions:
- A human message is a `user` event with `origin.kind == "human"`; when the event has no `origin.kind` the text
  heuristics decide (not meta, not a side chain, not a tool result, not a compact summary, and the text does not start
  with a system reminder, task notification, local command, bash output, hook output, the "Caveat: The messages below were
  generated" notice, a side-session or CI event, a sub-agent hand-back, a message from another session or "[SYSTEM
  NOTIFICATION"). A message typed while the assistant is working is stored as an `attachment` event of type
  `queued_command`; it is a human message when its `origin.kind` is "human" (or, without origin, its commandMode is
  "prompt" and it is not meta). Human messages are de-duplicated by event uuid across all transcripts.
- Assistant messages are de-duplicated by message id across all transcripts (a resumed or forked session that copies
  history is not counted twice); the last streamed line of a message wins for usage. Tool calls count once per tool_use id.
- Everything comes from one merged timeline of all transcripts and sub-agents. Active time is the sum of gaps shorter
  than --idle-minutes; gaps of that length or longer are waiting.
- Every waiting gap is classified: "izin ya da uzun komut" when at least one tool call is still open during it (the tool
  open longest is named; a sub-agent launcher is named only when nothing else is open), "kişi bekleniyor" when the gap
  ends with a human message, otherwise "diğer".
- token_girdi = input + cache creation + cache read; token_cikti = output.

Report line: the summary prints `Bu adım: <is> iş + <bekleme> bekleme · <token> · <n> dokunuş` for the whole window.
For one plan step, run the tool with --since set to the moment the step started. The progress bar comes from the plan:
`--adim 4/8` prints `İlerleme: ▓▓▓▓░░░░ 4/8 adım`. The estimated finish is not measured; the reporter writes it.

Warnings ("Uyarılar", printed right after the summary; `uyarilar` in json): fixed threshold rules over numbers the tool
already measures - no judgement, no extra network call, the exit code stays 0. Each rule has a flag; 0 switches it off.
  U1 long wait (>= --uyari-bekleme-dk, default 60 min; waits shorter than --idle-minutes are not measured as waits),
  U2 human touches in the interval (> --uyari-dokunus, 3), U3 output tokens (> --uyari-cikti-token, 150000) and the
  four-kind token total (> --uyari-toplam-token, 20000000), U4 long chat (per session, whole transcript, not only the
  --since interval: assistant messages > --uyari-ileti, 400, or at least one compaction summary), U5 repetition (one
  sub-agent type started > --uyari-ajan-tekrar times, 2; tool results flagged as errors > --uyari-hata, 10), U6 only with
  --pr (failed checks > 0; a PR open more than --uyari-pr-gun days, 5). Warning text holds only numbers, fixed words,
  "oturum-n", durations and the already whitelisted tool / sub-agent names.

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

# Text that does not come from a person. Matched at the START of a message part only.
NON_HUMAN_PREFIXES = (
    "<system-reminder>",
    "<task-notification",
    "<local-command",
    "Caveat: The messages below were generated",
    "<side-sessions-event",
    "<ci-monitor-event",
    "<bash-stdout",
    "<bash-stderr",
    "<user-prompt-submit-hook",
    "[SYSTEM NOTIFICATION",
    "[Subagent hand-back]",
    "Another Claude session sent a message",
)

# Warning thresholds (flags --uyari-*); 0 switches a rule off.
DEFAULT_LIMITS = {
    "bekleme_dk": 60, "dokunus": 3, "cikti_token": 150000, "toplam_token": 20000000, "ileti": 400,
    "ajan_tekrar": 2, "hata": 10, "pr_gun": 5,
}
MAX_WAIT_LINES = 3

WAIT_TOOL = "izin ya da uzun komut"
WAIT_HUMAN = "kişi bekleniyor"
WAIT_OTHER = "diğer"

# Bash command words that may be shown; any other first word is shown as plain "Bash".
KNOWN_COMMANDS = {
    "git", "gh", "dotnet", "npm", "npx", "yarn", "pnpm", "node", "python", "python3", "pip", "pip3", "make", "cargo",
    "docker", "kubectl", "brew", "curl", "wget", "ssh", "scp", "rsync", "sshpass",
    "cat", "ls", "grep", "rg", "find", "sed", "awk", "head", "tail", "wc", "sort", "uniq", "diff", "echo", "printf",
    "sleep", "bash", "sh", "zsh", "cp", "mv", "rm", "mkdir", "touch", "chmod", "xargs", "tee", "jq", "test", "pwd",
    "which", "open", "tar", "unzip", "ps", "kill", "pgrep", "date", "env", "dig", "ping", "stat", "file", "du", "df",
    "sqlite3", "psql", "mongosh", "redis-cli", "powershell", "pwsh", "cmd",
}
# (command, subcommand) pairs whose subcommand may be shown. Everything else shows the command word only.
ALLOWED_SUBCOMMANDS = {
    "git": {"push", "pull", "fetch", "commit", "merge", "status", "diff", "log", "worktree"},
    "gh": {"pr", "api", "run"},
    "dotnet": {"test", "build"},
    "npm": {"test", "run"},
}

# Rule-file markers. A marker must start the token or follow a "/".
_RULE_MARKER = re.compile(r"(?:^|/)(\.ees/|\.antigravity/rules/)")
_RULE_OK = re.compile(r"[A-Za-z0-9._/-]+")
_RULE_SPLIT = re.compile(r"[\s'\"`;|&<>()]+")

_IDENT = re.compile(r"[A-Za-z0-9._:-]{1,60}")
_TS_RE = re.compile(
    r"^(\d{4})-(\d\d)-(\d\d)(?:[T ](\d\d):(\d\d)(?::(\d\d))?(?:\.(\d+))?)?\s*(Z|[+-]\d\d:?\d\d)?$"
)
_DATE_ONLY = re.compile(r"^\d{4}-\d\d-\d\d$")


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


def progress_bar(done, total):
    """`İlerleme: ▓▓▓▓░░░░ 4/8 adım` - 8 cells, filled = round(8 * done / total)."""
    filled = int(8 * done / float(total) + 0.5)
    return "İlerleme: %s%s %d/%d adım" % ("▓" * filled, "░" * (8 - filled), done, total)


# ------------------------------------------------------------------------------------------------ text helpers
def cell(value, limit=None):
    """The one cleaning function for everything printed in a table or a list: no control characters, no pipes."""
    t = re.sub(r"[\x00-\x1f\x7f  ]+", " ", str(value)).replace("|", "/")
    t = re.sub(r"\s+", " ", t).strip()
    return t[:limit] if limit else t


def ident(value):
    """A name from a transcript (tool, model, sub-agent type) is printed only when it is a plain identifier."""
    return value if isinstance(value, str) and _IDENT.fullmatch(value) else "?"


def sanitize_title(title):
    """--tur-adlari: drop e-mail, path-like and token-like words, cut to 60 characters."""
    keep = []
    for w in str(title).split():
        if any(c in w for c in "@/\\=") or re.search(r"[A-Za-z0-9_-]{20,}", w):
            continue
        keep.append(w)
    return cell(" ".join(keep), 60)


def bash_label(command):
    """'Bash' plus, for well-known commands, the command word and for listed pairs the subcommand. No other text."""
    if not isinstance(command, str):
        return "Bash"
    for seg in re.split(r"&&|\|\||;|\||\n", command):
        words = seg.strip().split()
        while words and re.match(r"^[A-Za-z_][A-Za-z0-9_]*=", words[0]):
            words = words[1:]
        if not words or words[0] in ("cd", "export", "set", "source", "pushd", "popd", "true", ":"):
            continue
        first = words[0].replace("\\", "/").rsplit("/", 1)[-1]
        if first not in KNOWN_COMMANDS:
            return "Bash"
        allowed = ALLOWED_SUBCOMMANDS.get(first)
        if allowed:
            rest = words[1:]
            i = 0
            while i < len(rest) and rest[i].startswith("-"):
                i += 2 if rest[i] in ("-C", "-c", "--git-dir", "--work-tree") else 1
            if i < len(rest) and rest[i] in allowed:
                return "Bash: %s %s" % (first, rest[i])
        return "Bash: %s" % first
    return "Bash"


def _rule_from_token(token):
    m = _RULE_MARKER.search(token)
    if m:
        path = token[m.start(1):]
        if _RULE_OK.fullmatch(path) and ".." not in path.split("/"):
            return path
        return None
    if token == "AGENTS.md" or token.endswith("/AGENTS.md"):
        return "AGENTS.md"
    return None


def rule_paths(text, whole=False):
    """Rule-file paths in text. whole=True: the text is one path (Read tool); otherwise it is split like a shell line."""
    if not isinstance(text, str):
        return []
    text = text.replace("\\", "/")
    tokens = [text.strip()] if whole else _RULE_SPLIT.split(text)
    out = []
    for tok in tokens:
        p = _rule_from_token(tok) if tok else None
        if p:
            out.append(p)
    return out


def human_text_ok(content):
    """Text heuristic (used only when the event has no origin.kind)."""
    if isinstance(content, str):
        parts = [content]
        has_image = False
    elif isinstance(content, list):
        parts = [c.get("text", "") for c in content if isinstance(c, dict) and c.get("type") == "text"]
        has_image = any(isinstance(c, dict) and c.get("type") == "image" for c in content)
    else:
        return False
    parts = [p for p in parts if isinstance(p, str) and p.strip()]
    kept = [p for p in parts if not p.lstrip().startswith(NON_HUMAN_PREFIXES)]
    return bool(kept) or (has_image and not parts)


def is_human_event(e, content):
    """True when a main-chat `user` event (not a tool result) is something a person typed."""
    if e.get("isSidechain") or e.get("isCompactSummary"):
        return False
    origin = e.get("origin")
    if isinstance(origin, dict) and "kind" in origin:
        return origin.get("kind") == "human"
    if e.get("isMeta"):
        return False
    return human_text_ok(content)


def is_queued_human(att):
    """An `attachment` of type queued_command typed by a person while the assistant was working."""
    if not isinstance(att, dict) or att.get("type") != "queued_command":
        return False
    origin = att.get("origin")
    if isinstance(origin, dict) and "kind" in origin:
        return origin.get("kind") == "human"
    return att.get("commandMode") == "prompt" and not att.get("isMeta")


# ------------------------------------------------------------------------------------------------------ parsing
def _usage_ints(usage):
    out = {}
    for key, src in TOKEN_KINDS:
        v = usage.get(src) if isinstance(usage, dict) else 0
        out[key] = v if isinstance(v, int) and not isinstance(v, bool) else 0
    return out


def parse_file(path, fk, since, until, is_main, owners):
    """One transcript file -> events, messages (once per id), tool uses (once per id), humans, chapters, reads.

    `owners` is shared by all files of one run: the first file that holds a message id (assistant) or an event uuid
    (user, attachment) owns it; copies in other files are skipped, so a resumed session is not counted twice.
    Events are (ts, file key, kind, payload); kind is assistant / result / human / human_q / other.
    """
    res = {"events": [], "messages": {}, "tool_uses": {}, "humans": [], "queued": 0, "chapters": [], "reads": Counter(),
           "errors": 0}
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
            if etype not in ("user", "assistant", "attachment"):
                continue
            ts = parse_ts(e.get("timestamp"))
            if ts is None or (since and ts < since) or (until and ts > until):
                continue
            uuid = e.get("uuid") if isinstance(e.get("uuid"), str) else None
            if etype == "attachment":
                if not is_main or not is_queued_human(e.get("attachment")):
                    continue
                if uuid:
                    if "u:" + uuid in owners:
                        continue
                    owners["u:" + uuid] = fk
                res["humans"].append(ts)
                res["queued"] += 1
                res["events"].append((ts, fk, "human_q", None))
                continue
            msg = e.get("message") if isinstance(e.get("message"), dict) else {}
            content = msg.get("content")
            if etype == "assistant":
                mid = msg.get("id") if isinstance(msg.get("id"), str) and msg.get("id") else None
                if not mid:
                    anon += 1
                    mid = "anon-%d" % anon
                elif owners.setdefault("m:" + mid, fk) != fk:
                    continue
                rec = res["messages"].get(mid)
                if rec is None:
                    rec = res["messages"][mid] = {"ts": ts, "model": None, "usage": _usage_ints(None)}
                if isinstance(msg.get("model"), str):
                    rec["model"] = ident(msg["model"])
                if isinstance(msg.get("usage"), dict):
                    rec["usage"] = _usage_ints(msg["usage"])  # last line wins
                opened = []
                if isinstance(content, list):
                    for item in content:
                        if not isinstance(item, dict) or item.get("type") != "tool_use":
                            continue
                        tid = item.get("id") if isinstance(item.get("id"), str) and item.get("id") else None
                        if not tid:
                            anon += 1
                            tid = "anon-tool-%d" % anon
                        if tid in res["tool_uses"]:
                            continue
                        raw = item.get("name") if isinstance(item.get("name"), str) else ""
                        inp = item.get("input") if isinstance(item.get("input"), dict) else {}
                        use = {"name": ident(raw), "ts": ts, "label": ident(raw), "subagent_type": None,
                               "launcher": raw in ("Agent", "Task")}
                        if use["launcher"] and isinstance(inp.get("subagent_type"), str):
                            use["subagent_type"] = ident(inp["subagent_type"])
                        if raw == "Bash":
                            use["label"] = bash_label(inp.get("command"))
                            for p in rule_paths(inp.get("command")):
                                res["reads"][p] += 1
                        elif raw == "Read":
                            for p in rule_paths(inp.get("file_path"), whole=True):
                                res["reads"][p] += 1
                        if raw.endswith("mark_chapter") and isinstance(inp.get("title"), str) and inp["title"].strip():
                            res["chapters"].append((ts, inp["title"]))
                        res["tool_uses"][tid] = use
                        opened.append((tid, use["label"], use["launcher"]))
                res["events"].append((ts, fk, "assistant", {"mid": mid, "opened": opened}))
                continue
            # user event
            if uuid:
                if "u:" + uuid in owners:
                    continue                        # an event uuid is seen once, in this file or in another
                owners["u:" + uuid] = fk
            kind, payload = "other", None
            ids = []
            if isinstance(content, list):
                ids = [c.get("tool_use_id") for c in content if isinstance(c, dict) and c.get("type") == "tool_result"]
            if isinstance(content, list):
                res["errors"] += sum(1 for c in content if isinstance(c, dict) and c.get("type") == "tool_result"
                                     and c.get("is_error") is True)
            if ids:
                kind, payload = "result", [i for i in ids if isinstance(i, str)]
            elif is_main and is_human_event(e, content):
                kind = "human"
                res["humans"].append(ts)
            res["events"].append((ts, fk, kind, payload))
    return res


def find_subagent_files(main_path):
    folder = os.path.join(os.path.splitext(main_path)[0], "subagents")
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


def session_overview(path):
    """Whole transcript, no --since/--until: (assistant messages once per id, compaction summary events). Main chat only."""
    seen = set()
    anon = compactions = 0
    with open(path, encoding="utf-8", errors="replace") as fh:
        for line in fh:
            try:
                e = json.loads(line)
            except ValueError:
                continue
            if not isinstance(e, dict):
                continue
            if e.get("isCompactSummary") is True:
                compactions += 1
            if e.get("type") == "assistant" and not e.get("isSidechain"):
                msg = e.get("message") if isinstance(e.get("message"), dict) else {}
                mid = msg.get("id")
                if isinstance(mid, str) and mid:
                    seen.add(mid)
                else:
                    anon += 1
    return len(seen) + anon, compactions


def build_warnings(limits, waits, humans, t_out, t_total, chats, launches, errors, prs, now):
    """Fixed threshold rules over measured numbers -> (list of {kod, metin, deger, esik}, count of warnings).

    A rule whose limit is 0 is off. Nothing but numbers, fixed words and already whitelisted names goes into a text.
    """
    out = []
    count = 0

    def add(kod, metin, deger, esik):
        out.append({"kod": kod, "metin": cell(metin), "deger": deger, "esik": esik})

    lim = limits["bekleme_dk"]
    if lim > 0:
        long_waits = sorted((w for w in waits if w["sure_s"] >= lim * 60.0), key=lambda w: -w["sure_s"])
        count += len(long_waits)
        for w in long_waits[:MAX_WAIT_LINES]:
            parts = ["Uzun bekleme: %s" % w["tur"]] + ([w["arac"]] if w["arac"] else []) + [fmt_dur(w["sure_s"])]
            add("U1", " · ".join(parts), minutes(w["sure_s"]), lim)
        if len(long_waits) > MAX_WAIT_LINES:
            more = len(long_waits) - MAX_WAIT_LINES
            add("U1", "ve %d bekleme daha" % more, more, lim)
    lim = limits["dokunus"]
    if lim > 0 and humans > lim:
        count += 1
        add("U2", "İnsan dokunuşu hedefin üstünde: %d (hedef: en çok %d)" % (humans, lim), humans, lim)
    lim = limits["cikti_token"]
    if lim > 0 and t_out > lim:
        count += 1
        add("U3", "Çıktı token'ı yüksek: %s (eşik %s)" % (fmt_int(t_out), fmt_int(lim)), t_out, lim)
    lim = limits["toplam_token"]
    if lim > 0 and t_total > lim:
        count += 1
        add("U3", "Toplam token yüksek: %s (eşik %s)" % (fmt_int(t_total), fmt_int(lim)), t_total, lim)
    lim = limits["ileti"]
    if lim > 0:
        for ad, n_msg, n_compact in chats:
            if n_msg > lim or n_compact >= 1:
                count += 1
                text = "Sohbet uzadı: %s %d ileti" % (ad, n_msg)
                if n_compact:
                    text += ", %d kez özetlendi" % n_compact
                add("U4", text + "; yeni sohbete devir düşünülmeli", n_msg, lim)
    lim = limits["ajan_tekrar"]
    if lim > 0:
        for kind, n in sorted(launches.items(), key=lambda kv: (-kv[1], kv[0] or "")):
            if n > lim:
                count += 1
                add("U5", "Aynı alt ajan %d kez başlatıldı: %s" % (n, kind or "alt ajan"), n, lim)
    lim = limits["hata"]
    if lim > 0 and errors > lim:
        count += 1
        add("U5", "Hatalı araç çağrısı çok: %d" % errors, errors, lim)
    for p in prs or []:
        if p.get("hata"):
            continue
        failed = p["kontrol"]["kalan"]
        if failed > 0:
            count += 1
            add("U6", "Otomatik kontrollerde kırmızı: %d (PR #%d)" % (failed, p["no"]), failed, 0)
        lim = limits["pr_gun"]
        created = parse_ts(p.get("acildi"))
        if lim > 0 and p["durum"] == "açık" and created is not None:
            days = int((now - created).total_seconds() // 86400)
            if days > lim:
                count += 1
                add("U6", "Birleştirme isteği %d gündür açık (PR #%d)" % (days, p["no"]), days, lim)
    return out, count


# ---------------------------------------------------------------------------------------------------- analysis
def tokens_by_model(messages):
    out = {}
    for rec in messages:
        model = rec["model"] or "?"
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


def build_data(transcripts, prs, repo, idle_minutes, since, until, gh=None, tur_adlari=False, adim=None, limits=None,
               now=None):
    limits = dict(DEFAULT_LIMITS, **(limits or {}))
    now = now or datetime.now(timezone.utc)
    threshold_s = idle_minutes * 60.0
    owners = {}
    files = []          # every parsed file: dict(fk, session, kind, res, ...)
    sessions = []
    for si, path in enumerate(transcripts, 1):
        main_res = parse_file(path, ("m", si), since, until, True, owners)
        sess = {"no": si, "ad": "oturum-%d" % si, "main": main_res, "subs": [], "yol": path}
        sessions.append(sess)
        files.append(main_res)
    parent_uses = {}
    for sess in sessions:
        parent_uses.update(sess["main"]["tool_uses"])
    for sess, path in zip(sessions, transcripts):
        for n, (sp, meta) in enumerate(find_subagent_files(path), 1):
            sres = parse_file(sp, ("s", sess["no"], n), since, until, False, owners)
            if not sres["events"]:
                continue
            parent = parent_uses.get(meta.get("toolUseId")) if isinstance(meta.get("toolUseId"), str) else None
            cand = None
            if parent and parent.get("subagent_type") and parent["subagent_type"] != "?":
                cand = parent["subagent_type"]
            elif isinstance(meta.get("agentType"), str) and ident(meta["agentType"]) != "?":
                cand = meta["agentType"]
            ts_all = [ev[0] for ev in sres["events"]]
            sess["subs"].append({
                "no": len(sess["subs"]) + 1,
                "tur": cand or "alt-ajan-%d" % (len(sess["subs"]) + 1),
                "ilk": min(ts_all), "son": max(ts_all), "res": sres,
                "token": tokens_by_model(list(sres["messages"].values())),
                "arac": Counter(u["name"] for u in sres["tool_uses"].values()),
            })
            files.append(sres)

    events = []
    for f in files:
        events.extend(f["events"])
    events.sort(key=lambda ev: ev[0])

    # turns: one per human message; events before the first one form a "Başlangıç" turn.
    human_ts = sorted(ts for f in files for ts in f["humans"])
    starts = list(human_ts)
    names = ["Tur %d" % n for n in range(1, len(human_ts) + 1)]
    if events and (not human_ts or events[0][0] < human_ts[0]):
        starts.insert(0, events[0][0])
        names.insert(0, "Başlangıç" if human_ts else "Tur 1")
    turns = [
        {"ad": names[i], "baslangic": starts[i], "is_s": 0.0, "bekleme_s": 0.0, "mesaj": 0, "arac": 0, "alt_ajan": 0,
         "cikti": 0, "token": 0}
        for i in range(len(starts))
    ]

    def turn_of(ts):
        if not turns:
            return None
        return turns[max(0, bisect.bisect_right(starts, ts) - 1)]

    if tur_adlari:
        chapters = sorted((c for s in sessions for c in s["main"]["chapters"]), key=lambda c: c[0])
        named = set()
        for ts, title in chapters:
            t = turn_of(ts)
            if t is not None and id(t) not in named:
                title = sanitize_title(title)
                if title:
                    t["ad"] = title
                    named.add(id(t))

    for f in files:
        for rec in f["messages"].values():
            t = turn_of(rec["ts"])
            if t is not None:
                t["token"] += sum(rec["usage"].values())
    for sess in sessions:
        for rec in sess["main"]["messages"].values():
            t = turn_of(rec["ts"])
            if t is not None:
                t["mesaj"] += 1
                t["cikti"] += rec["usage"]["cikti"]
        for use in sess["main"]["tool_uses"].values():
            t = turn_of(use["ts"])
            if t is not None:
                t["arac"] += 1
                if use["launcher"]:
                    t["alt_ajan"] += 1

    # merged timeline: active/waiting, wait classification by open tool calls
    open_calls = {}     # (file key, tool id) -> (opened at, label, launcher, message id)
    waits = []
    active = waiting = 0.0
    for i, (ts, fk, kind, payload) in enumerate(events):
        if kind == "assistant":
            for key in [k for k, v in open_calls.items() if k[0] == fk and v[3] != payload["mid"]]:
                del open_calls[key]                  # an earlier message's call that never got a result: orphan
            for tid, label, launcher in payload["opened"]:
                open_calls[(fk, tid)] = (ts, label, launcher, payload["mid"])
        elif kind == "result":
            for tid in payload or []:
                open_calls.pop((fk, tid), None)
        elif kind == "human":
            for key in [k for k in open_calls if k[0] == fk]:
                del open_calls[key]                  # a new human message ends the previous turn's open calls
        if i + 1 == len(events):
            break
        nts, _, nkind, _ = events[i + 1]
        gap = (nts - ts).total_seconds()
        t = turn_of(ts)
        if gap < threshold_s:
            active += gap
            if t is not None:
                t["is_s"] += gap
        else:
            waiting += gap
            if t is not None:
                t["bekleme_s"] += gap
            tool = None
            if open_calls:
                real = [v for v in open_calls.values() if not v[2]] or list(open_calls.values())
                tool = min(real, key=lambda v: v[0])[1]
                klass = WAIT_TOOL
            elif nkind in ("human", "human_q"):
                klass = WAIT_HUMAN
            else:
                klass = WAIT_OTHER
            waits.append({"sure_s": gap, "tur": klass, "arac": tool, "baslangic": ts, "bitis": nts,
                          "turn": t["ad"] if t else None})

    all_times = [ev[0] for ev in events]
    first = min(all_times) if all_times else None
    last = max(all_times) if all_times else None
    wall = (last - first).total_seconds() if first else 0.0

    main_tools = Counter()
    sub_tools = Counter()
    for s in sessions:
        main_tools.update(u["name"] for u in s["main"]["tool_uses"].values())
        for sub in s["subs"]:
            sub_tools.update(sub["arac"])
    main_tokens = add_token_maps(*[tokens_by_model(list(s["main"]["messages"].values())) for s in sessions])
    sub_tokens = add_token_maps(*[sub["token"] for s in sessions for sub in s["subs"]])
    all_tokens = add_token_maps(main_tokens, sub_tokens)
    humans = sum(len(s["main"]["humans"]) for s in sessions)
    queued = sum(s["main"]["queued"] for s in sessions)
    t_total = token_total(all_tokens)
    t_in = sum(slot[k] for slot in all_tokens.values() for k in ("girdi", "onbellek_yazma", "onbellek_okuma"))
    t_out = sum(slot["cikti"] for slot in all_tokens.values())
    line = "Bu adım: %s iş + %s bekleme · %s · %d dokunuş" % (fmt_dur(active), fmt_dur(waiting), fmt_tok(t_total), humans)

    launches = Counter()
    errors = 0
    for f in files:
        errors += f["errors"]
        for u in f["tool_uses"].values():
            if u["launcher"]:
                launches[u["subagent_type"] if u["subagent_type"] not in (None, "?") else None] += 1

    data = {
        "ozet": {
            "ilk": iso(first), "son": iso(last),
            "duvar_saati_dk": minutes(wall), "is_dk": minutes(active), "bekleme_dk": minutes(waiting),
            "sure_dk": minutes(wall), "bosta_esigi_dk": idle_minutes,
            "insan_dokunusu": humans, "calisma_sirasinda_yazilan": queued,
            "oturum_sayisi": len(sessions),
            "arac_cagrilari_ana": dict(main_tools.most_common()),
            "arac_cagrilari_alt_ajan": dict(sub_tools.most_common()),
            "token_ana": main_tokens, "token_alt_ajan": sub_tokens, "token_toplam": all_tokens,
            "token_girdi": t_in, "token_cikti": t_out, "token_hepsi": t_total,
            "rapor_satiri": line,
            "ilerleme": progress_bar(adim[0], adim[1]) if adim else None,
        },
        "turlar": [
            {
                "ad": t["ad"], "baslangic": iso(t["baslangic"]), "is_dk": minutes(t["is_s"]),
                "bekleme_dk": minutes(t["bekleme_s"]), "asistan_iletisi": t["mesaj"], "arac_cagrisi": t["arac"],
                "alt_ajan_baslatildi": t["alt_ajan"], "cikti_token": t["cikti"], "token_toplam": t["token"],
            }
            for t in turns
        ],
        "beklemeler": [
            {"sure_dk": minutes(w["sure_s"]), "tur": w["tur"], "arac": w["arac"], "baslangic": iso(w["baslangic"]),
             "bitis": iso(w["bitis"]), "tur_adi": w["turn"]}
            for w in sorted(waits, key=lambda w: -w["sure_s"])
        ],
        "oturumlar": [],
        "prler": None,
        "uyarilar": [],
    }
    for s in sessions:
        data["oturumlar"].append({
            "no": s["no"], "ad": s["ad"],
            "alt_ajanlar": [
                {
                    "no": sub["no"], "tur": sub["tur"], "model": ", ".join(sorted(sub["token"])),
                    "sure_dk": minutes((sub["son"] - sub["ilk"]).total_seconds()),
                    "token": sub["token"], "arac_cagrisi": sum(sub["arac"].values()),
                    "arac_cagrilari": dict(sub["arac"].most_common()),
                }
                for sub in s["subs"]
            ],
            "okunan_kural_dosyalari": {
                "ana": dict(sorted(s["main"]["reads"].items())),
                "alt_ajanlar": {(sub["tur"] if sub["tur"].startswith("alt-ajan-") else "%s (%d)" % (sub["tur"], sub["no"])):
                                dict(sorted(sub["res"]["reads"].items())) for sub in s["subs"]},
            },
        })
    if prs:
        getter = gh or default_gh
        data["prler"] = [pr_summary(n, repo, getter) for n in prs]
    chats = []
    for s in sessions:
        n_msg, n_compact = session_overview(s["yol"])
        chats.append((s["ad"], n_msg, n_compact))
    data["uyarilar"], data["ozet"]["uyari_sayisi"] = build_warnings(
        limits, waits, humans, t_out, t_total, chats, launches, errors, data["prler"], now)
    return data


# --------------------------------------------------------------------------------------------- pull requests
def default_gh(repo, number):
    cmd = ["gh", "pr", "view", str(number)]
    if repo:
        cmd += ["--repo", repo]
    cmd += ["--json", "createdAt,mergedAt,closedAt,isDraft,reviews,commits,statusCheckRollup"]
    proc = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, universal_newlines=True, timeout=90)
    if proc.returncode != 0:
        raise RuntimeError("gh pr view failed")
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
    except FileNotFoundError:
        return {"no": number, "hata": "gh bulunamadı"}
    except Exception:  # network, auth, bad output: say it in plain words, never echo the error text
        return {"no": number, "hata": "PR okunamadı"}
    if not isinstance(raw, dict):
        return {"no": number, "hata": "PR okunamadı"}
    created = parse_ts(raw.get("createdAt"))
    merged = parse_ts(raw.get("mergedAt"))
    closed = parse_ts(raw.get("closedAt"))
    out = {"no": number, "taslak": bool(raw.get("isDraft")), "acildi": iso(created)}
    reviews = []
    for r in raw.get("reviews") or []:
        if isinstance(r, dict):
            t = parse_ts(r.get("submittedAt"))
            if t is not None:
                reviews.append((t, r.get("state")))
    reviews.sort(key=lambda x: x[0])
    out["inceleme_sayisi"] = len(raw.get("reviews") or [])
    if reviews and created:
        t, state = reviews[0]
        known = isinstance(state, str) and state in REVIEW_TR
        out["ilk_inceleme"] = {
            "kim": "onaylayici", "durum": state if known else "diger", "durum_tr": REVIEW_TR[state] if known else "diğer",
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
    lines = ["| " + " | ".join(cell(h) for h in headers) + " |", "|" + "|".join("---" for _ in headers) + "|"]
    for r in rows:
        lines.append("| " + " | ".join(cell(c) for c in r) + " |")
    return lines


def tokens_rows(label, slot_map):
    rows = []
    for model, s in sorted(slot_map.items()):
        rows.append([label, model, fmt_int(s["mesaj"]), fmt_int(s["girdi"]), fmt_int(s["onbellek_yazma"]),
                     fmt_int(s["onbellek_okuma"]), fmt_int(s["cikti"])])
    return rows


def count_line(counter):
    return " · ".join("%s %d" % (cell(k), v) for k, v in counter.items()) or "yok"


def dk(value):
    return str(value).replace(".", ",")


def render_md(data):
    o = data["ozet"]
    multi = o["oturum_sayisi"] > 1
    out = ["# EES çalıştırma kaydı (oturum dökümlerinden üretildi)", ""]
    out += ["## Özet", ""]
    out += [
        "- İlk olay: %s · son olay: %s" % (o["ilk"] or "yok", o["son"] or "yok"),
        "- Duvar saati: %s dk · iş (aktif): %s dk · bekleme: %s dk (boşta eşiği %s dk)"
        % (dk(o["duvar_saati_dk"]), dk(o["is_dk"]), dk(o["bekleme_dk"]), dk(o["bosta_esigi_dk"])),
        "- İnsan dokunuşu (insan mesajı): %d · bunlardan asistan çalışırken yazılan: %d"
        % (o["insan_dokunusu"], o["calisma_sirasinda_yazilan"]),
        "- Araç çağrıları (ana sohbet): %s" % count_line(Counter(o["arac_cagrilari_ana"])),
        "- Araç çağrıları (alt ajanlar): %s" % count_line(Counter(o["arac_cagrilari_alt_ajan"])),
    ]
    out.append("- Uyarı sayısı: %d" % o["uyari_sayisi"])
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
    out += ["Rapor satırı:", "", "```", o["rapor_satiri"]]
    if o["ilerleme"]:
        out.append(o["ilerleme"])
    out += ["```", ""]
    if o["ilerleme"]:
        out += ["Adım sayısı plandan gelir (`--adim`); \"tahmini bitiş\" ölçülmez, raporu yazan kendi tahminiyle ekler.", ""]

    out += ["## Uyarılar", ""]
    if data["uyarilar"]:
        out += ["- %s" % cell(w["metin"]) for w in data["uyarilar"]]
    else:
        out.append("Uyarı yok.")
    out.append("")

    out += ["## Turlar", ""]
    rows = [
        [a["ad"], a["baslangic"] or "", fmt_dk(a["is_dk"] * 60), fmt_dk(a["bekleme_dk"] * 60),
         a["asistan_iletisi"], a["arac_cagrisi"], a["alt_ajan_baslatildi"], fmt_int(a["cikti_token"]),
         fmt_int(a["token_toplam"])]
        for a in data["turlar"]
    ]
    out += table(["Tur", "Başlangıç", "İş (dk)", "Bekleme (dk)", "Asistan iletisi", "Araç çağrısı",
                  "Alt ajan başlatıldı", "Çıktı token", "Toplam token (alt ajanlar dahil)"], rows)
    out.append("")

    out += ["## Beklemeler", ""]
    if data["beklemeler"]:
        rows = [[dk(w["sure_dk"]), w["tur"], w["arac"] or "-", w["baslangic"], w["bitis"], w["tur_adi"] or "-"]
                for w in data["beklemeler"]]
        out += table(["Süre (dk)", "Tür", "Araç", "Başlangıç", "Bitiş", "Tur"], rows)
    else:
        out.append("Eşiği aşan bekleme yok.")
    out.append("")

    out += ["## Alt ajanlar", ""]
    rows = []
    for s in data["oturumlar"]:
        for a in s["alt_ajanlar"]:
            tok = sum(sum(m[k] for k, _ in TOKEN_KINDS) for m in a["token"].values())
            out_tok = sum(m["cikti"] for m in a["token"].values())
            rows.append(([s["ad"]] if multi else []) + [
                a["tur"], a["model"], fmt_dk(a["sure_dk"] * 60), fmt_int(tok), fmt_int(out_tok), a["arac_cagrisi"],
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
            rows.append(([s["ad"]] if multi else []) + ["ana sohbet", p, n])
        for who, paths in k["alt_ajanlar"].items():
            for p, n in paths.items():
                rows.append(([s["ad"]] if multi else []) + [who, p, n])
    if rows:
        out += table((["Oturum"] if multi else []) + ["Kim", "Yol", "Sayı"], rows)
    else:
        out.append("Kural dosyası okunmadı.")
    out.append("")

    if data["prler"] is not None:
        out += ["## PR'lar", ""]
        for p in data["prler"]:
            out += ["### PR #%s" % p["no"], ""]
            if p.get("hata"):
                out += ["- %s" % p["hata"], ""]
                continue
            ilk = p["ilk_inceleme"]
            out.append("- Açıldı: %s%s" % (p["acildi"] or "bilinmiyor", " (taslak)" if p["taslak"] else ""))
            if ilk:
                out.append("- İlk inceleme (onaylayıcı): %s · açılıştan %s dk sonra (toplam %d inceleme)"
                           % (ilk["durum_tr"], dk(ilk["acilistan_sonra_dk"]), p["inceleme_sayisi"]))
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
                out.append("- Son commit'ten tüm kontrollerin bitişine: %s dk" % dk(p["son_commit_kontrol_bitis_dk"]))
            if p["acilistan_kontrol_bitis_dk"] is not None:
                out.append("- Açılıştan tüm kontrollerin bitişine: %s dk" % dk(p["acilistan_kontrol_bitis_dk"]))
            out.append("")
    return "\n".join(out).rstrip() + "\n"


# ------------------------------------------------------------------------------------------------------ CLI
def build_parser():
    ap = argparse.ArgumentParser(
        description="EES run record: turns, work vs waiting time, tokens and PR data from Claude Code session "
                    "transcripts. Times without an offset (--since, --until) are UTC.")
    ap.add_argument("--transcript", action="append", required=True, metavar="FILE.jsonl",
                    help="session transcript (repeatable); <name>/subagents/*.jsonl is read too when present")
    ap.add_argument("--pr", action="append", type=int, metavar="N", help="pull request number (repeatable)")
    ap.add_argument("--repo", metavar="OWNER/NAME", help="repository for --pr (default: the current one)")
    ap.add_argument("--idle-minutes", type=float, default=5.0, help="gaps of this length or longer are waiting (default 5)")
    ap.add_argument("--format", choices=("md", "json"), default="md")
    ap.add_argument("--since", metavar="ISO", help="ignore events before this time; times without an offset are UTC; "
                                                   "a date alone means the start of that day")
    ap.add_argument("--until", metavar="ISO", help="ignore events after this time; times without an offset are UTC; "
                                                   "a date alone means the END of that day")
    ap.add_argument("--adim", metavar="N/M", help="plan step N of M: prints the progress bar line (the plan is not "
                                                  "in the transcript, so the count is given here)")
    ap.add_argument("--tur-adlari", action="store_true",
                    help="name turns after mark_chapter titles (sanitised, 60 characters); default: Tur n")
    for flag, key, kind, what in (
            ("--uyari-bekleme-dk", "bekleme_dk", float, "a single wait this long (minutes) or longer"),
            ("--uyari-dokunus", "dokunus", int, "more human messages than this in the interval"),
            ("--uyari-cikti-token", "cikti_token", int, "more output tokens than this in the interval"),
            ("--uyari-toplam-token", "toplam_token", int, "a four-kind token total above this in the interval"),
            ("--uyari-ileti", "ileti", int, "a session with more assistant messages than this (whole transcript) or "
                                            "with a compaction summary"),
            ("--uyari-ajan-tekrar", "ajan_tekrar", int, "the same sub-agent type started more often than this"),
            ("--uyari-hata", "hata", int, "more tool results flagged as errors than this"),
            ("--uyari-pr-gun", "pr_gun", float, "a PR open more than this many days (only with --pr)")):
        ap.add_argument(flag, dest="uyari_" + key, type=kind, default=DEFAULT_LIMITS[key], metavar="N",
                        help="warn on %s (default %s; 0 switches the rule off)" % (what, DEFAULT_LIMITS[key]))
    return ap


def main(argv=None, gh=None, stdout=None, now=None):
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
    if args.until and _DATE_ONLY.match(args.until.strip()):
        until = until + timedelta(days=1) - timedelta(microseconds=1)
    limits = {k[len("uyari_"):]: v for k, v in vars(args).items() if k.startswith("uyari_")}
    if any(v < 0 for v in limits.values()):
        print("--uyari-* limits must be 0 (off) or positive", file=sys.stderr)
        return 2
    adim = None
    if args.adim:
        m = re.fullmatch(r"(\d+)/(\d+)", args.adim.strip())
        if not m or int(m.group(2)) < 1 or int(m.group(1)) > int(m.group(2)):
            print("--adim must look like 4/8 (0 <= N <= M, M >= 1)", file=sys.stderr)
            return 2
        adim = (int(m.group(1)), int(m.group(2)))
    paths = [os.path.expanduser(p) for p in args.transcript]
    for p in paths:
        if not os.path.isfile(p):
            print("transcript not found", file=sys.stderr)
            return 2
    data = build_data(paths, args.pr or [], args.repo, args.idle_minutes, since, until, gh=gh,
                      tur_adlari=args.tur_adlari, adim=adim, limits=limits, now=now)
    if args.format == "json":
        text = json.dumps(data, ensure_ascii=False, indent=2)
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
