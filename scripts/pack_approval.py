#!/usr/bin/env python3
"""CT-APR-01 — a Module Pack's approval bound to the version that was read (contract:
docs/roadmap/plans/ct-dev-readiness-approval-01/CONTRACT.md §3).

Approvals live outside the pack, in execution/approvals/approvals.tsv: rows are only appended (each version of the file
starts with the previous one's rows), the last row of a pack decides, a revoke is a row. A row approves the pack's
WHOLE file by its SHA-256: one changed byte and the approval no longer holds.

Risk is declared with a reason; the tool may only raise it. HIGH: a first approval; a re-approval after a revoke; a
changed künye identity field (id, domain, service); a changed line under a sensitive heading (Repo Scope, Protected
Paths, authorization / permissions, gateway / routing, schema / data / migration, integration, audit, security,
tenancy); any changed line naming .antigravity or ocelot.json. The comparison base is the pack's last approved version
before the row that no revoke followed.

A HIGH approval is measured on the MAIN line (origin/main, else main), not on the worker's ref: the row must have
reached main through a first-parent merge commit whose author is not the row commit's author (found by git blame on the
row's line; "derived from git, not verified on GitHub"). The approver must be in execution/approvals/approvers.tsv as
it was on main BEFORE that merge, with high = evet, and must not be the same person as the declared author (the
`person` column joins several accounts of one person). The approval card is regenerated and compared by SHA-256.

The rule applies from the commit that added execution/approvals/POLICY-START.md. A pack not changed since that commit
and never approved under it keeps its earlier approval (no retroactive invalidation). Once the file was added, its
absence is a refusal, not "no rule".

Trust boundary (S-40, owner decision 2026-10-22): the human step is derived from git AND confirmed on GitHub — the
bringing merge is a merged pull request into main whose merger is a listed person other than its opener; for an approval
row the merger is the row's approver, for a review row not its author (gh api; not measurable = no human step). Merges must be merge commits (a
squash or fast-forward merge leaves no merge author and a HIGH approval then fails). Since PR #153 (2026-10-19)
.github/CODEOWNERS puts the approval files, the approver list and these checks under a code-owner review.

    python3 scripts/pack_approval.py card  <pack> [--ref REF]        # the approval card and its SHA-256
    python3 scripts/pack_approval.py check <pack> [--ref REF]        # exit 0 approved · 1 not approved · 2 unreadable
    python3 scripts/pack_approval.py row   <pack> --author L --approver L --risk LOW|HIGH --reason TEXT [--ref REF]
                                                                     # prints the row; never writes the file
    python3 scripts/pack_approval.py review-row <pack> --findings F --verdict V --reviewer codex|claude --model M
            --effort E --reviewer-session S --author K… --author-session S --requested-by L [--ref REF]
                                                                     # CT-APR-02: prints the review row; never writes
    python3 scripts/pack_approval.py --self-test

CT-APR-02 (from the commit that added execution/approvals/POLICY-REVIEW-START.md; design
docs/roadmap/plans/ct-apr-02-pack-review/DESIGN.md): Delivery Capability Packs
(execution/portfolio/delivery-capability-packs/) enter the same approval. An approval row added at or after that commit
needs an independent review of the exact version (execution/approvals/reviews.tsv + a findings file bound by SHA-256)
unless every change is exempt (a date; owner / reviewer filled from TBD; whitespace) and no risk is HIGH. Every review
row of that version counts, and only when it reached main through another person's merge: any UYGUN DEĞİL or open ENGEL
stops; an ENGEL the approver accepts makes the approval HIGH. A reviewed pack carries a slice table, and
scripts/prompt_gate.py stops a WP that takes every slice without whole_pack=<WP> in the approval row.
"""

from __future__ import annotations

import difflib
import hashlib
import json
import importlib.util
import os
import re
import subprocess
import sys
import tempfile

APPROVALS = "execution/approvals/approvals.tsv"
APPROVERS = "execution/approvals/approvers.tsv"
POLICY = "execution/approvals/POLICY-START.md"
MAIN_REFS = ("origin/main", "main")
APPROVAL_COLUMNS = ["pack", "pack_sha256", "action", "risk", "risk_reason", "author", "approver", "approved_at",
                    "card_sha256", "note"]
APPROVER_COLUMNS = ["login", "person", "role", "high"]
PACK_PATH = re.compile(r"^(execution/domains/[a-z0-9][a-z0-9-]*/module-packs|execution/portfolio/delivery-capability-packs)/"
                       r"[A-Za-z0-9][A-Za-z0-9._-]*\.md$")
DCP_ROOT = "execution/portfolio/delivery-capability-packs/"
SENSITIVE_HEADING = re.compile(r"(?i)(repo scope|protected path|authori[sz]ation|permission|yetki|izin|gateway|routing|"
                               r"schema|şema|entity|data model|veri model|migration|taşıma|integration|entegrasyon|"
                               r"audit|denetim|security|güvenlik|tenan|kiracı)")
ALWAYS_HIGH = re.compile(r"(?i)(\.antigravity|ocelot\.json)")
IDENTITY_KEYS = ("id", "domain", "service")
OPEN_DECISION = re.compile(r"(?i)\b(TBD|OPEN|AÇIK KARAR|açık karar|open decision)\b")
HEX64 = re.compile(r"^[0-9a-f]{64}$")

# CT-APR-02 — independent review before approval, and sliced delivery (design: docs/roadmap/plans/ct-apr-02-pack-review/)
REVIEWS = "execution/approvals/reviews.tsv"
REVIEW_POLICY = "execution/approvals/POLICY-REVIEW-START.md"
REVIEW_COLUMNS = ["target", "target_sha256", "findings", "findings_sha256", "verdict", "blockers_open", "reviewer", "model",
                  "effort", "reviewer_session", "author", "author_session", "requested_by", "reviewed_at"]
VERDICTS = ("UYGUN", "KOŞULLU", "UYGUN DEĞİL")
REVIEWERS = ("codex", "claude")
FINDINGS_PATH = re.compile(r"^docs/records/reviews/[A-Za-z0-9][A-Za-z0-9._-]*/[A-Za-z0-9][A-Za-z0-9._-]*\.md$")
FINDING = re.compile(r"^F-(\d+)(?:@([0-9a-f]{8}))?\s*\|\s*(ENGEL|ORTA|DÜŞÜK)\s*\|(.*)$")
ACCEPTED = "kabul — onaylayan"
CLOSURES = {"ENGEL": ("açık", ACCEPTED), "ORTA": ("açık", "kapandı", ACCEPTED), "DÜŞÜK": ("açık", "kapandı", ACCEPTED)}
KEYLINE = re.compile(r"^([a-z_]+):\s*(.*?)\s*$")
DATE_KEYS = ("updated", "last_updated", "last_reviewed")
FILL_KEYS = ("owner", "reviewer")
FILLED = re.compile(r"^[^|<>`\[\]{}]{1,80}$")       # a short name or role, no markup
UNFILLED = re.compile(r"(?i)^\s*((tbd|todo)\b.*|[-—]?)\s*$")
NOT_GIVEN = re.compile(r"(?i)^\s*(-|—|tbd|todo|\.\.\.|…|yok|n/?a)?\s*$")
DATE = re.compile(r"^\d{4}-\d{2}-\d{2}$")
PERSON = re.compile(r"^K\d+$")
DCP_LOW_HEADING = re.compile(r"(?i)^#{1,6}\s*(\d+\.\s*)?(audit and reconciliation notes|future follow-ups|"
                             r"denetim ve uzlaşma notları|gelecek işler)\s*$")
WHOLE_PACK = re.compile(r"(?:^|\s)whole_pack=(\S+)")
SLICE_HEADING = re.compile(r"(?i)^##\s+(\d+\.\s*)?(teslim dilimleri|delivery slices)\s*$")
SLICE_ROW = re.compile(r"^\|\s*(DL-\d+)\s*\|")
FENCE = re.compile(r"^(`{3,}|~{3,})")


class Unreadable(Exception):
    pass


# A commit id never changes what it names: results read at a full 40-hex id (or its ^n parent) are cached for the
# process. A symbolic ref (HEAD, main) is never cached — it moves.
_CACHE: dict = {}
IMMUTABLE = re.compile(r"^[0-9a-f]{40}(\^\d+)?$")


def cached(*rev_positions: int):
    def deco(fn):
        def wrap(*args):
            if not all(isinstance(args[i], str) and IMMUTABLE.match(args[i]) for i in rev_positions if i < len(args)):
                return fn(*args)
            key = (fn.__name__,) + args
            if key not in _CACHE:
                _CACHE[key] = fn(*args)
            return _CACHE[key]
        wrap.__name__, wrap.__doc__ = fn.__name__, fn.__doc__
        return wrap
    return deco


# ------------------------------------------------------------------------------------------------ git (read-only)
def git(root: str, *args: str, ok_fail: bool = False) -> bytes | None:
    # Codex K3: `git replace` objects would make a full commit id name different history — they are ignored; a shallow
    # repository is refused (see Ctx), so a cached history answer cannot change when the history deepens
    env = dict(os.environ, GIT_OPTIONAL_LOCKS="0", GIT_NO_REPLACE_OBJECTS="1")
    done = subprocess.run(["git", "-C", root, *args], capture_output=True, env=env)
    if done.returncode != 0:
        if ok_fail:
            return None
        raise Unreadable(f"git {' '.join(args[:2])} → {done.returncode}")
    return done.stdout


def commit_of(root: str, ref: str) -> str | None:
    out = git(root, "rev-parse", "--verify", "--quiet", f"{ref}^{{commit}}", ok_fail=True) if not ref.startswith("-") else None
    return out.decode().strip() if out else None


@cached(1)
def read(root: str, commit: str, path: str) -> bytes | None:
    return git(root, "cat-file", "blob", f"{commit}:{path}", ok_fail=True)


def main_line(root: str) -> str:
    for r in MAIN_REFS:
        c = commit_of(root, r)
        if c:
            return c
    raise Unreadable("ana çizgi (origin/main ya da main) bulunamadı")


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def content_lines(text: str) -> list[str]:
    return [l.rstrip(" \r") for l in text.replace("\r\n", "\n").split("\n") if l.strip()]


def parse_tsv(text: str, columns: list[str], what: str) -> list[dict]:
    numbered = [(n, l) for n, l in enumerate(text.replace("\r\n", "\n").split("\n"), 1) if l.strip()]
    if not numbered or numbered[0][1].rstrip(" \r").split("\t") != columns:
        raise Unreadable(f"{what}: başlık tam olarak {' / '.join(columns)} olmalı")
    rows = []
    for n, line in numbered[1:]:
        cells = line.rstrip(" \r").split("\t")      # a trailing tab is an empty last cell, not whitespace
        if len(cells) != len(columns):
            raise Unreadable(f"{what} satır {n}: {len(columns)} sütun olmalı, {len(cells)} var")
        rows.append(dict(zip(columns, cells), _n=n))
    return rows


def approval_rows(root: str, commit: str, path: str) -> list[dict]:
    """The rows of `path`. Every row of the file must be well formed (canonical pack path, action, a 64-hex SHA-256):
    a row written for a pack in another spelling ('./', upper-case hex, a trailing space) would otherwise be invisible —
    a hidden revoke is a pass. One malformed row makes the file unreadable."""
    raw = read(root, commit, APPROVALS)
    if not raw:
        return []
    rows = parse_tsv(raw.decode("utf-8"), APPROVAL_COLUMNS, APPROVALS)
    for r in rows:
        if not PACK_PATH.match(r["pack"]) or "//" in r["pack"] or r["action"] not in ("approve", "revoke") \
                or not HEX64.match(r["pack_sha256"]):
            raise Unreadable(f"{APPROVALS} satır {r['_n']}: paket yolu, action ya da pack_sha256 biçimi geçersiz")
    return [r for r in rows if r["pack"] == path]



def approvers_at(root: str, commit: str) -> dict[str, dict]:
    raw = read(root, commit, APPROVERS)
    return {r["login"]: r for r in parse_tsv(raw.decode("utf-8"), APPROVER_COLUMNS, APPROVERS)} if raw else {}


# ------------------------------------------------------------------------------------------------ risk and card
def headings(lines: list[str]) -> list[str]:
    """For every line, the heading it stands under ('' before the first one); the künye lines get '@künye'."""
    out, current, in_kunye = [], "", bool(lines) and lines[0].strip() == "---"
    for i, l in enumerate(lines):
        if in_kunye:
            out.append("@künye")
            if i > 0 and l.strip() == "---":
                in_kunye = False
            continue
        if re.match(r"^#{1,6}\s", l):
            current = l
        out.append(current)
    return out


def pack_risk(now: str, base: str | None, after_revoke: bool = False) -> tuple[str, list[str]]:
    """HIGH with the reasons, or LOW. No approved base = a first approval = HIGH; a re-approval after a revoke = HIGH."""
    if base is None:
        return "HIGH", (["iptalden sonra yeniden onay — iptal edilmemiş onaylı bir önceki sürüm yok"] if after_revoke
                        else ["ilk onay — paketin onaylı bir önceki sürümü yok"])
    reasons = []       # a revoke cancels every earlier approval as a base (base_for), so after a revoke base is None
    a, b = base.replace("\r\n", "\n").split("\n"), now.replace("\r\n", "\n").split("\n")
    ha, hb = headings(a), headings(b)
    changed = []
    for op, i1, i2, j1, j2 in difflib.SequenceMatcher(None, a, b, autojunk=False).get_opcodes():
        if op != "equal":
            changed += [(a[i], ha[i]) for i in range(i1, i2)] + [(b[j], hb[j]) for j in range(j1, j2)]
    for line, heading in changed:
        if not line.strip():
            continue
        key = re.match(r"^([a-z_]+):", line.strip())
        if heading == "@künye" and key and key.group(1) in IDENTITY_KEYS:
            reasons.append(f"künye kimlik alanı değişti: {line.strip()[:80]}")
        elif ALWAYS_HIGH.search(line):
            reasons.append(f"korunan yol: {line.strip()[:80]}")
        elif heading and heading != "@künye" and SENSITIVE_HEADING.search(heading):
            reasons.append(f"'{heading.strip('# ').strip()[:40]}' altında değişti: {line.strip()[:70]}")
    if reasons:
        return "HIGH", reasons[:12]
    return "LOW", [f"{sum(1 for l, _ in changed if l.strip())} değişen satır, yüksek risk tetikleyicisi yok"]


def kunye(text: str) -> dict[str, str]:
    lines = text.replace("\r\n", "\n").split("\n")
    out = {}
    if lines and lines[0].strip() == "---":
        for l in lines[1:]:
            if l.strip() == "---":
                break
            m = re.match(r"^([a-z_]+):\s*(.*)$", l)
            if m and m.group(1) not in out:
                out[m.group(1)] = m.group(2).strip()
    return out


def section(text: str, title: str, limit: int = 30) -> list[str]:
    lines = text.split("\n")
    start = next((i for i, l in enumerate(lines) if re.match(rf"^#{{2,3}}\s*(\d+\.\s*)?{re.escape(title)}\b", l)), None)
    if start is None:
        return []
    out = []
    for l in lines[start + 1:]:
        if re.match(r"^#{1,3}\s", l):
            break
        if l.strip():
            out.append(l.rstrip())
    return out[:limit]


def card(path: str, data: bytes, risk: str, reasons: list[str]) -> str:
    text = data.decode("utf-8", errors="replace")
    k = kunye(text)
    decisions = [l.strip() for l in text.split("\n") if OPEN_DECISION.search(l)][:20]
    out = ["# Onay kartı (CT-APR-01)", "", f"- paket: `{path}`", f"- paket SHA-256: `{sha256(data)}`"]
    for key in ("id", "name", "status", "approval_status", "owner", "domain", "service"):
        if k.get(key):
            out.append(f"- {key}: {k[key]}")
    out += ["", f"## 🔴 Risk (hesaplanan): {risk}" if risk == "HIGH" else f"## Risk (hesaplanan): {risk}",
            *[f"- {r}" for r in reasons], "", "## Repo Scope", *(section(text, "Repo Scope") or ["- (bölüm yok)"]), "",
            "## Açık kararlar", *([f"- {d[:160]}" for d in decisions] or ["- (yok)"]), ""]
    return "\n".join(out)


class Ctx:
    """Everything one decision reads, measured once: the ref, the main line, the rule starts, the pack's rows."""

    def __init__(self, root: str, commit: str, path: str):
        self.root, self.commit, self.path = root, commit, path
        if git(root, "rev-parse", "--is-shallow-repository").decode().strip() == "true":
            raise Unreadable("sığ (shallow) depo — onay geçmişi tam değil; tam klonda koşun")
        self.main = main_line(root)
        added = git(root, "log", "--format=%H", "--diff-filter=A", commit, "--", POLICY).decode().split()
        self.policy = added[-1] if added else None
        # M3: the review rule and its rows are read from the MAIN line — a worker branch forked before a negative review
        # or before the rule itself cannot avoid it; the ref's own start still counts (and a deletion there refuses)
        self.rstart = review_start(root, self.main) or review_start(root, commit)
        if is_dcp(path) and self.rstart is not None:
            self.policy = self.rstart
        self.rows = approval_rows(root, commit, path)
        self.memo: dict[int, tuple] = {}

    def legacy(self) -> str | None:
        return legacy_text(self.root, self.policy, self.path) if self.policy else None


def base_for(ctx: "Ctx", i: int) -> tuple[str | None, bool]:
    """(the text of the last VALID approved version among rows[:i] that no later revoke cancelled, any revoke there?)
    A row that never passed (B1: e.g. an author's own LOW approval of a HIGH change) is not a base: comparing with it
    would let the next row skip the review, the HIGH risk and the human step. A revoke cancels every approval before it
    as a comparison base: re-approving after a revoke is a first approval."""
    prior = ctx.rows[:i]
    revoked = any(r["action"] == "revoke" for r in prior)
    for j in range(len(prior) - 1, -1, -1):
        r = prior[j]
        if r["action"] != "approve" or any(x["action"] == "revoke" for x in prior[j + 1:]):
            continue
        fails = judge(ctx, j)[0]
        if not fails:
            return version_with_sha(ctx.root, ctx.commit, ctx.path, r["pack_sha256"]), revoked
    return None, revoked


def judge(ctx: "Ctx", i: int) -> tuple[list[str], list[str], str, bool]:
    """(failures, report lines, effective risk, review needed) for the approve row rows[i] against its own pack version."""
    if i in ctx.memo:
        return ctx.memo[i]
    root, path, row = ctx.root, ctx.path, ctx.rows[i]
    if row["risk"] not in ("LOW", "HIGH"):
        raise Unreadable(f"onay satırı {row['_n']}: risk değeri geçersiz")
    data = version_bytes(root, ctx.commit, path, row["pack_sha256"])
    if data is None:
        ctx.memo[i] = (["onay satırının paket sürümü geçmişte yok"], [], row["risk"], True)
        return ctx.memo[i]
    text = data.decode("utf-8", errors="replace")
    base, after_revoke = base_for(ctx, i)
    risk, reasons = risk_of(path, text, base, after_revoke)
    out = [f"hesaplanan risk: {risk}"] + [f"  - {r}" for r in reasons]
    fails = []
    if risk == "HIGH" and row["risk"] == "LOW":
        fails.append("beyan LOW, hesaplanan HIGH — risk yalnız yükseltilebilir; HIGH onay gerekli")
    effective = "HIGH" if "HIGH" in (risk, row["risk"]) else "LOW"
    if WHOLE_PACK.search(row["note"]):
        effective = "HIGH"            # B2: taking the whole pack in one WP is granted only by a HIGH approval
        out.append("whole_pack işareti — onay HIGH sayılır (yetkili ikinci kişi ve insan adımı)")
    needed, accepted_by = False, set()
    added_by = row_commit(root, ctx.commit, row["_n"])
    if ctx.rstart is not None and not strictly_before(root, added_by, ctx.rstart):
        needed, why = review_needed(path, text, base, after_revoke, row["risk"], ctx.legacy())
        if not needed:
            out.append("inceleme (CT-APR-02): gerekmez — yalnız muaf alanlar değişti")
            # Codex K9 / K10: the exemption only lifts the need for a NEW positive review. What this version's reviews
            # said when the row was added still decides it: an ENGEL open then (or a UYGUN DEĞİL) stops the row even if
            # someone accepts it later (today's reading would then see it closed — Codex K10), and an ENGEL already
            # accepted makes the approval HIGH. today_problems adds the negatives that came after the row
            e_fails, _e_notes, accepted_by, _e = review_problems(root, ctx.main, path, data, added_by)
            fails += [x for x in e_fails if not x.startswith("inceleme yok")]
            if accepted_by:
                effective = "HIGH"
                out.append("  - bu sürümde kabul edilmiş ENGEL var — onay HIGH sayılır")
        else:
            out.append("inceleme (CT-APR-02): gerekli — " + "; ".join(why[:3]))
            ids = slice_ids(path, text) or []
            if not ids:
                fails.append("dilim tablosu yok — " + ("'## 8.1 Delivery slices'" if is_dcp(path) else
                             "'## Teslim dilimleri' (DL-n)") + " gerekli (CT-APR-02)")
            elif len(ids) == 1 and not WHOLE_PACK.search(row["note"]):
                fails.append("tek dilim — paket tek seferde uygulanır; istisna yalnız onay satırında whole_pack=<WP>")
            # Codex K2: the review comes BEFORE the approval — only reviews already in the approval row's history decide
            # it (a later positive review needs a new approval row); for today's decision every review on main also
            # applies, so a later UYGUN DEĞİL or open ENGEL still stops it
            r_fails, r_notes, accepted_by, _engels = review_problems(root, ctx.main, path, data, added_by)
            if r_fails and any(x.startswith("inceleme yok") for x in r_fails):
                later = review_problems(root, ctx.main, path, data, ctx.main)[0]
                if not any(x.startswith("inceleme yok") for x in later):
                    r_fails = ["inceleme onay satırından sonra geldi — inceleme onaydan önce yapılır (aynı commit'te gelen "
                               "inceleme ve onay, aynı sürümün birlikte kabulüdür); yeni bir onay satırı gerekir"] + \
                              [x for x in r_fails if not x.startswith("inceleme yok")]
            out += [f"  - {x}" for x in r_notes]
            fails += r_fails
            if accepted_by:
                effective = "HIGH"
    else:
        out.append("inceleme (CT-APR-02): kural dışı — onay satırı kural başlangıcından önce" if ctx.rstart
                   else "inceleme (CT-APR-02): kural yürürlükte değil")
    if effective == "HIGH":
        merge, why = main_merge(root, ctx.main, added_by)
        if merge is None:
            fails.append("insan adımı yok: " + why)
        else:
            ok, why = other_merger(root, added_by, merge)
            if ok:      # Codex F-6: the merge must also be a merged pull request's, merged by another person (GitHub)
                ok, why2 = pr_person_check(root, merge, approver=row["approver"])
                why = f"{why}; {why2}" if ok else why2
            (out.append("insan adımı: " + why) if ok else fails.append("insan adımı yok: " + why))
            listed = approvers_at(root, f"{merge}^1")       # the list as it was on main before this merge
            person = lambda login: listed[login]["person"] if login in listed else f"@{login}"
            w = listed.get(row["approver"])
            if w is None:
                fails.append(f"onaylayan {row['approver']} merge'den önceki onaylayıcı listesinde yok")
            elif w["high"] != "evet":
                fails.append(f"onaylayan {row['approver']} ({w['role']}) yüksek riskli işi onaylayamaz")
            if person(row["approver"]) == person(row["author"]):
                fails.append("yüksek riskli işi yazan (ya da aynı kişinin başka hesabı) onaylayamaz")
            if accepted_by and accepted_by != {person(row["approver"])}:
                fails.append(f"ENGEL kabulü onaylayana ait değil ({', '.join(sorted(accepted_by))}; onaylayan "
                             f"{person(row['approver'])}) — riski yalnız onaylayan üstlenir")
    else:
        listed = approvers_at(root, list_point(root, ctx.main, added_by))   # Codex K2″: historical, not today's main
        if row["approver"] != row["author"] and row["approver"] not in listed:
            fails.append(f"onaylayan {row['approver']} onaylayıcı listesinde yok ve yazan değil")
    if row["card_sha256"] != sha256(card(path, data, risk, reasons).encode("utf-8")):
        fails.append("onay kartının özeti paketin şimdiki kartıyla aynı değil — kart elle değişti ya da satır yeniden "
                     "üretilmeli (aynı pakete eşzamanlı onay geldiyse satır, birleştirmeden sonra yeniden üretilir)")
    ctx.memo[i] = (fails, out, effective, needed)
    return ctx.memo[i]


def list_point(root: str, main: str, added: str) -> str:
    """Where the approver list is read for a row (Codex K2″): the main line just before the row reached it — the merge's
    first parent, or the row commit's parent when it was written on the main line — or, while the row is not on main,
    the branch's last common commit with main. A person added to the list later, or on the row's own branch, does
    not count for that row."""
    if git(root, "merge-base", "--is-ancestor", added, main, ok_fail=True) is not None:
        if added in git(root, "rev-list", "--first-parent", main).decode().split():
            return f"{added}^1"
        merge = main_merge(root, main, added)[0]
        if merge is not None:
            return f"{merge}^1"
    return git(root, "merge-base", added, main).decode().strip()


def today_problems(ctx: "Ctx", i: int) -> list[str]:
    """Today's decision on top of the historical one (judge): for every row under the rule (exempt or not), every review
    of its version on main applies its NEGATIVES now — a later UYGUN DEĞİL, an open ENGEL, a broken record. Never used to choose a base, so
    the row generator and the check compute the same base (Codex K7)."""
    fails, _out, _eff, needed = judge(ctx, i)
    added = row_commit(ctx.root, ctx.commit, ctx.rows[i]["_n"])
    if ctx.rstart is None or strictly_before(ctx.root, added, ctx.rstart):
        return []
    # Codex K9: also for an exempt row — the version's later negatives and later ENGELs apply today
    data = version_bytes(ctx.root, ctx.commit, ctx.path, ctx.rows[i]["pack_sha256"])
    if data is None:
        return []
    then_engels = review_problems(ctx.root, ctx.main, ctx.path, data, added)[3]
    now, _notes, _acc, now_engels = review_problems(ctx.root, ctx.main, ctx.path, data, ctx.main)
    out = [x for x in now if x not in fails and not x.startswith("inceleme yok")]
    if now_engels - then_engels:
        # Codex K8: an ENGEL raised after the approval is never settled by that approval — even when someone accepts it
        # later, the acceptance must come with a NEW approval row, which is then HIGH and checked for its approver
        out.append(f"onaydan sonra {len(now_engels - then_engels)} ENGEL geldi — kabul edilmiş olsa bile yeni bir onay satırı "
                   "gerekir (kabul HIGH onayla ve onaylayanın kendisiyle olur)")
    return out



# ------------------------------------------------------------------------------------------------ history checks
@cached(1)
def append_only_problems(root: str, commit: str, path: str = APPROVALS) -> list[str]:
    """No row ever disappears. Every commit in the full history that changed the file is compared with EVERY parent:
    the first parent's rows are the new version's prefix; every other parent's rows appear in it in the same order. The
    default history simplification and a first-parent-only comparison miss a merge that drops a row (B3); a
    multiset comparison misses a merge that reorders an approval behind a revoke (Codex K5)."""
    problems = []
    lines = lambda c: content_lines(read(root, c, path).decode("utf-8", errors="replace")) if read(root, c, path) else []
    for entry in git(root, "log", "--full-history", "--format=%H %P", commit, "--", path).decode().split("\n"):
        ids = entry.split()
        if not ids:
            continue
        c, parents = ids[0], ids[1:]
        new = lines(c)
        for parent in parents or [None]:
            old = lines(parent) if parent else []
            if len(parents) <= 1 or parent == parents[0]:
                bad = new[:len(old)] != old          # the line the merge lands on keeps its order: a prefix
            else:
                it = iter(new)                       # Codex K5: another parent's rows keep their ORDER (subsequence);
                bad = not all(l in it for l in old)  # a multiset check let A0,R0,A1 + R1 become A0,R0,R1,A1
            if bad:
                problems.append(f"{path} {c[:12]}'de yalnız ekleme değil (satır silindi, değişti ya da yeri değişti)")
                break
    return problems



@cached(1)
def version_bytes(root: str, commit: str, path: str, digest: str) -> bytes | None:
    for c in git(root, "log", "--format=%H", commit, "--", path).decode().split():
        data = read(root, c, path)
        if data is not None and sha256(data) == digest:
            return data
    return None


@cached(1)
def version_with_sha(root: str, commit: str, path: str, digest: str) -> str | None:
    for c in git(root, "log", "--format=%H", commit, "--", path).decode().split():
        data = read(root, c, path)
        if data is not None and sha256(data) == digest:
            return data.decode("utf-8", errors="replace")
    return None


@cached(1)
def row_commit(root: str, commit: str, line_no: int, path: str = APPROVALS) -> str:
    blame = git(root, "blame", "--porcelain", "-L", f"{line_no},{line_no}", commit, "--", path).decode().split()
    if not blame or not re.fullmatch(r"[0-9a-f]{40}", blame[0]):
        raise Unreadable(f"{path} satırını ekleyen commit bulunamadı")
    return blame[0]


@cached(1, 2)
def other_merger(root: str, added: str, merge: str) -> tuple[bool, str]:
    """Is the merge's author another person than the row commit's author? Derived from git, not verified on GitHub."""
    who = lambda c: git(root, "log", "-1", "--format=%an\t%ae", c).decode().strip().lower().split("\t")
    a, b = who(added), who(merge)
    if a[0] == b[0] or a[1] == b[1]:
        return False, f"satırı ekleyen ve merge eden aynı yazar ({a[0]}) — türetildi"
    return True, f"satırı ekleyen {a[0]}, merge eden {b[0]} ({merge[:12]}) — türetildi, GitHub'da doğrulanmadı"


PR_VERIFY_ENV = "PACK_APPROVAL_PR_VERIFY"
_PR_CACHE: dict = {}


def github_slug(root: str) -> str | None:
    """owner/repo of any GitHub remote (host case-insensitive, trailing '/' and '.git' allowed; git applies
    url.<base>.insteadOf to `remote -v`), else None."""
    out = git(root, "remote", "-v", ok_fail=True)
    for line in (out.decode(errors="replace") if out else "").splitlines():
        m = re.search(r"github\.com[:/]+([^/\s]+)/([^/\s]+?)(?:\.git)?/*\s+\((?:fetch|push)\)", line, re.I)
        if m:
            return f"{m.group(1)}/{m.group(2)}"
    return None


def _in_temp(root: str) -> bool:
    return os.path.realpath(root).startswith(os.path.realpath(tempfile.gettempdir()) + os.sep)


def _gh_json(path: str):
    done = subprocess.run(["gh", "api", path], capture_output=True, timeout=60)
    if done.returncode != 0:
        raise Unreadable(f"gh api {path}: {done.stderr.decode(errors='replace').strip()[:160]} (çıkış {done.returncode})")
    return json.loads(done.stdout.decode())


def _test_pr(root: str, merge: str) -> dict | None:
    """Self-test stand-in for GitHub: a merge whose subject is 'Merge <branch>' is a pull request merged into main by
    the merge commit's author, opened by the author of its second parent's tip."""
    subject = git(root, "log", "-1", "--format=%s", merge).decode().strip()
    if not re.fullmatch(r"Merge (?!main\b)\S+", subject):
        return None
    who = lambda c: git(root, "log", "-1", "--format=%an", c).decode().strip()
    return {"number": 0, "opener": who(f"{merge}^2"), "merger": who(merge), "base": "main"}


def pr_of_merge(root: str, merge: str) -> dict | None:
    """{number, opener, merger, base} of the merged pull request whose merge commit is `merge` (GitHub), or None when
    `merge` is no pull request's merge. Raises Unreadable when it cannot be measured. Only a successful read is cached.
    PACK_APPROVAL_PR_VERIFY=test selects the stand-in only in a temporary-directory repository with no GitHub remote."""
    slug = github_slug(root)
    if slug is None:
        if os.environ.get(PR_VERIFY_ENV) == "test" and _in_temp(root):
            return _test_pr(root, merge)
        raise Unreadable("origin bir GitHub deposu değil")
    key = (slug, merge)
    if key in _PR_CACHE:
        return _PR_CACHE[key]
    pulls = _gh_json(f"repos/{slug}/commits/{merge}/pulls")
    if not isinstance(pulls, list):
        raise Unreadable("GitHub cevabı bir liste değil")
    hits = [x for x in pulls if isinstance(x, dict) and x.get("merge_commit_sha") == merge and x.get("merged_at")]
    info = None
    if hits:
        pr = _gh_json(f"repos/{slug}/pulls/{hits[0].get('number')}")
        if not isinstance(pr, dict):
            raise Unreadable("GitHub PR cevabı bir nesne değil")
        if pr.get("merged_at") and pr.get("merge_commit_sha") == merge:
            info = {"number": pr.get("number"), "opener": (pr.get("user") or {}).get("login"),
                    "merger": (pr.get("merged_by") or {}).get("login"), "base": (pr.get("base") or {}).get("ref")}
    _PR_CACHE[key] = info
    return info


def pr_person_check(root: str, merge: str, approver: str | None = None, author_key: str | None = None) -> tuple[bool, str]:
    """Codex F-6 + independent review F-1/F-3: the bringing merge is a merged pull request into main (GitHub); its
    merger is listed (approvers.tsv on main before the merge) and another person than its opener; for an approval row
    the merger IS the row's approver (S-41 — the approver merges); for a review row the merger is not the row's author.
    Anything not measurable is no human step."""
    try:
        info = pr_of_merge(root, merge)
    except (Unreadable, OSError, ValueError, subprocess.TimeoutExpired) as e:
        return False, f"GitHub'a sorulamadı — merge'ün PR merge'ü olduğu doğrulanamadı ({str(e)[:160]})"
    if info is None:
        return False, f"{merge[:12]} bir PR'ın merge commit'i değil (GitHub) — dal korumasını atlayan bir push olabilir"
    n, opener, merger, base = info["number"], info["opener"], info["merger"], info["base"]
    if base != "main":
        return False, f"PR #{n}'nin hedefi main değil ({base}) (GitHub)"
    listed = approvers_at(root, f"{merge}^1")
    pm, po = listed.get(merger or "", {}).get("person"), listed.get(opener or "", {}).get("person")
    if pm is None:
        return False, f"PR #{n}'yi merge eden ({merger}) onaylayıcı listesinde yok (GitHub)"
    if merger == opener or (po is not None and po == pm):
        return False, f"PR #{n}'yi açan ve merge eden aynı kişi ({merger}) (GitHub)"
    if approver is not None and listed.get(approver, {}).get("person") != pm:
        return False, (f"PR #{n}'yi onaylayan ({approver}) değil {merger} merge etti — onay satırını onaylayan kişi "
                       "merge etmeli (kart §7a/7) (GitHub)")
    if author_key is not None and pm == author_key:
        return False, f"PR #{n}'yi satırın yazarı ({author_key}) merge etti (GitHub)"
    return True, f"PR #{n}: açan {opener}, merge eden {merger} (GitHub)"


def human_step(root: str, main: str, added: str, author_key: str | None = None) -> tuple[str | None, str]:
    """(the main-line merge that brought `added` in, why) when another person merged it — in git (other_merger) AND on
    GitHub (pr_person_check); else (None, why)."""
    merge, why = main_merge(root, main, added)
    if merge is None:
        return None, why
    ok, why = other_merger(root, added, merge)
    if not ok:
        return None, why
    ok, why2 = pr_person_check(root, merge, author_key=author_key)
    return (merge if ok else None), (f"{why}; {why2}" if ok else why2)


@cached(1, 2)
def main_merge(root: str, main: str, added: str) -> tuple[str | None, str]:
    """The first-parent merge of the main line that brought `added` in, or (None, why)."""
    if git(root, "merge-base", "--is-ancestor", added, main, ok_fail=True) is None:
        return None, "onay satırı henüz ana çizgide (main) değil"
    line = git(root, "rev-list", "--first-parent", main).decode().split()
    if added in line:
        return None, f"onay satırı {added[:12]} ile doğrudan ana çizgiye yazılmış — merge ile gelen ikinci kişi adımı yok"
    # S-40: the oldest first-parent commit of main that descends from `added` brought it in; it must be a merge. The
    # descendants are walked on the FULL graph (--ancestry-path without --first-parent) and then kept to the main
    # line: with --first-parent the walk misses a merge whose branch took main after the row (card §7a), because the
    # row is then reachable only through that merge's second parent, not its own branch tip.
    below = set(git(root, "rev-list", "--ancestry-path", f"{added}..{main}").decode().split())
    chain = [c for c in line if c in below]
    if chain:
        m = chain[-1]
        parents = git(root, "rev-list", "--parents", "-n", "1", m).decode().split()[1:]
        # independent review F-1/F-2: the row must be on the merged branch's OWN line (first-parent chain of a non-first
        # parent); a foxtrot (someone pulls main with a merge and fast-forwards main past the author's self-merge) or a
        # nested merge (the author merges the row's branch into another branch himself) does not count
        if len(parents) > 1 and any(added in git(root, "rev-list", "--first-parent", p).decode().split() for p in parents[1:]):
            return m, ""
        if len(parents) > 1:
            return None, (f"onay satırı ana çizgiye {m[:12]} ile geldi ama o merge'ün dalının kendi çizgisinde değil (iç içe "
                          "ya da ters merge) — satırın PR'ı main'e başka bir kişi tarafından doğrudan merge edilmeli")
    return None, "onay satırını ana çizgiye getiren merge commit'i bulunamadı"


# ------------------------------------------------------------------------------------------------ CT-APR-02
def is_dcp(path: str) -> bool:
    return path.startswith(DCP_ROOT)


def dcp_risk(now: str, base: str | None, after_revoke: bool = False) -> tuple[str, list[str]]:
    """A Delivery Capability Pack: HIGH unless every changed line stands under a heading named LOW (audit notes, future
    follow-ups). The front matter, the lines before the first heading and any heading not on that list are HIGH — an
    unrecognised structure is never LOW."""
    if base is None:
        return pack_risk(now, base, after_revoke)
    a, b = base.replace("\r\n", "\n").split("\n"), now.replace("\r\n", "\n").split("\n")
    ha, hb = headings(a), headings(b)
    reasons, n = [], 0
    for op, i1, i2, j1, j2 in difflib.SequenceMatcher(None, a, b, autojunk=False).get_opcodes():
        if op == "equal":
            continue
        for line, heading in [(a[i], ha[i]) for i in range(i1, i2)] + [(b[j], hb[j]) for j in range(j1, j2)]:
            if not line.strip():
                continue
            n += 1
            if heading and heading != "@künye" and DCP_LOW_HEADING.match(heading.strip()) and not ALWAYS_HIGH.search(line):
                continue
            where = "ön bilgi" if heading == "@künye" else (heading.strip("# ").strip()[:40] or "ilk başlıktan önce")
            reasons.append(f"DCP '{where}' değişti: {line.strip()[:70]}")
    if reasons:
        return "HIGH", reasons[:12]
    return "LOW", [f"{n} değişen satır, yalnız denetim notları / gelecek işler altında"]


def risk_of(path: str, now: str, base: str | None, after_revoke: bool = False) -> tuple[str, list[str]]:
    return dcp_risk(now, base, after_revoke) if is_dcp(path) else pack_risk(now, base, after_revoke)


def split_front(text: str) -> tuple[list[str], list[str]]:
    lines = text.replace("\r\n", "\n").split("\n")
    if lines and lines[0].strip() == "---":
        for i in range(1, len(lines)):
            if lines[i].strip() == "---":
                return lines[1:i], lines[i + 1:]
        return lines[1:], []
    return [], lines


def review_exempt(now: str, base: str | None) -> tuple[bool, list[str]]:
    """True only when every change against the approved base is on the exemption list (fail-closed): a künye date
    field; owner / reviewer filled from TBD or empty (an existing owner changing is not exempt); whitespace. The size
    of a change is not a criterion — splitting a change into small approvals gains nothing."""
    if base is None:
        return False, ["onaylı önceki sürüm yok (ilk onay ya da iptal sonrası)"]
    fa, ba = split_front(base)
    fb, bb = split_front(now)
    norm = lambda ls: [l.rstrip(" \t\r") for l in ls]      # only trailing whitespace: indentation and blank lines
    #                                                          change how Markdown renders (code block, table break)
    why = []
    if norm(ba) != norm(bb):
        why.append("künye dışında içerik değişti")
    if len(fa) != len(fb):
        why.append("künye satır sayısı değişti")
    else:
        for x, y in zip(fa, fb):
            if " ".join(x.split()) == " ".join(y.split()):
                continue
            mx, my = KEYLINE.match(x), KEYLINE.match(y)
            if not (mx and my and mx.group(1) == my.group(1)):
                why.append(f"künye satırı değişti: {y.strip()[:60]}")
                continue
            key = mx.group(1)
            if sum(1 for l in fb if (KEYLINE.match(l) or [None, None])[1] == key) != 1:
                why.append(f"künye alanı '{key}' birden çok kez var")
            elif key in DATE_KEYS and DATE.match(my.group(2)):
                continue
            elif key in FILL_KEYS and UNFILLED.match(mx.group(2)) and FILLED.match(my.group(2)):
                continue
            else:
                why.append(f"künye alanı '{key}' değişti" + (" (var olan değer)" if key in FILL_KEYS else ""))
    return not why, why


def review_needed(path: str, text: str, base: str | None, after_revoke: bool, declared: str,
                  legacy: str | None = None) -> tuple[bool, list[str]]:
    """One decision for evaluate and slice_rule: a review is needed unless every change is exempt and neither the
    computed nor the declared risk is HIGH. A pack never approved under the rule compares with `legacy`, its version
    when the rule started (no retroactive review): its first approval is HIGH under CT-APR-01 by construction, so that
    forced HIGH is not counted here; a revoke has no legacy base."""
    if base is None and not after_revoke and legacy is not None:
        base, declared = legacy, "LOW"
    risk, _ = risk_of(path, text, base, after_revoke)
    exempt, why = review_exempt(text, base)
    if exempt and "HIGH" not in (risk, declared):
        return False, []
    return True, why or ["risk HIGH"]


def slice_ids(path: str, text: str) -> list[str] | None:
    """A Module Pack's 'Teslim dilimleri' DL-n ids, or a DCP's '## 8.1 Delivery slices' Slice ids read by the routing
    tool's own fence-aware parser (scripts/ct_route.py parse_slices); None if missing or not readable."""
    if not is_dcp(path):
        return slices(text)
    try:
        spec = importlib.util.spec_from_file_location("ct_route_for_pack_approval",
                                                      os.path.join(os.path.dirname(os.path.abspath(__file__)), "ct_route.py"))
        route = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(route)
    except Exception as e:            # m4: a missing or broken routing tool is 'unreadable' (exit 2), never a traceback
        raise Unreadable(f"scripts/ct_route.py yüklenemedi — DCP dilim tablosu okunamadı ({type(e).__name__})")
    _front, found, problems = route.parse_slices(text)
    return list(found) if found and not problems else None


def review_rows(root: str, commit: str) -> list[dict]:
    """Every row must name a canonical target with a 64-hex SHA-256 and a known verdict; otherwise unreadable (M1)."""
    raw = read(root, commit, REVIEWS)
    if not raw:
        return []
    rows = parse_tsv(raw.decode("utf-8"), REVIEW_COLUMNS, REVIEWS)
    for r in rows:
        if not PACK_PATH.match(r["target"]) or "//" in r["target"] or not HEX64.match(r["target_sha256"]) \
                or r["verdict"] not in VERDICTS:
            raise Unreadable(f"{REVIEWS} satır {r['_n']}: hedef yolu, target_sha256 ya da sonuç biçimi geçersiz")
    return rows



def parse_findings(text: str, target: str, digest: str) -> tuple[list[dict], list[str]]:
    """The finding lines of a review record and its problems. A line with '|', one that starts with 'F-', or one that
    names ENGEL / ORTA / DÜŞÜK in UPPER case but is not a valid finding line is a problem, never ignored."""
    found, problems, seen = [], [], set()
    lines = text.replace("\r\n", "\n").split("\n")
    if sum(1 for l in lines if l.strip() == f"hedef: {target} @ {digest}") != 1:
        problems.append(f"bulgu dosyasında tek bir 'hedef: {target} @ <sha256>' satırı bu sürümle olmalı")
    for n, line in enumerate(lines, 1):
        m = FINDING.match(line.strip())
        if not m:
            # a line with '|' is always a finding line: a lookalike (U+2010, Cyrillic Е) or lower case cannot hide one.
            # The level words count only in UPPER case (case-sensitive): prose such as "bir engel değil" is allowed.
            if "|" in line or line.strip().upper().startswith("F") and "-" in line[:4] or re.search(r"\b(?:ENGEL|ORTA|DÜŞÜK)\b", line):
                problems.append(f"bozuk bulgu satırı {n}: {line.strip()[:60]}")
            continue
        parts = [x.strip() for x in m.group(4).rsplit("|", 3)]
        if len(parts) != 4:
            problems.append(f"bulgu satırı {n}: 'bulgu | kapanış | sahip | son tarih' olmalı")
            continue
        f = {"n": int(m.group(1)), "ref": m.group(2), "sev": m.group(3), "text": parts[0], "closure": parts[1],
             "owner": parts[2], "due": parts[3], "line": n}
        key = (f["ref"], f["n"])
        if key in seen:
            problems.append(f"bulgu satırı {n}: F-{f['n']} birden çok kez")
        seen.add(key)
        if NOT_GIVEN.match(f["text"]):
            problems.append(f"bulgu satırı {n}: bulgu metni boş")
        if f["closure"] not in CLOSURES[f["sev"]]:
            problems.append(f"bulgu satırı {n}: {f['sev']} için kapanış '{f['closure'][:30]}' bilinmiyor "
                            f"({' / '.join(CLOSURES[f['sev']])})")
        if f["ref"] and not (f["sev"] == "ENGEL" and f["closure"] == ACCEPTED):
            problems.append(f"bulgu satırı {n}: başka kayda başvuru yalnız ENGEL kabulünde ('{ACCEPTED}')")
        if f["sev"] != "ENGEL" and f["closure"] == "açık" and (NOT_GIVEN.match(f["owner"]) or not DATE.match(f["due"])):
            problems.append(f"bulgu satırı {n}: açık {f['sev']} koşulunun sahibi ve son tarihi (YYYY-AA-GG) yazılmalı")
        found.append(f)
    return found, problems


@cached(1, 4)
def review_problems(root: str, main: str, path: str, data: bytes, at: str) -> tuple[list[str], list[str], set[str], set]:
    """(failures, notes, the person keys that accepted an ENGEL) for the reviews AS THEY WERE at `at`: the review file,
    its append-only history and every findings file are read at `at`; a row counts only if it reached `main` through
    another person's merge. `at` = the approval row's commit is the historical reading (Codex K2′: a findings file
    repaired later cannot validate an old approval); `at` = main is today's reading. Every review row of this exact
    version counts — the most restrictive reading: any UYGUN DEĞİL stops, any ENGEL left open stops; a later UYGUN does
    not override. A UYGUN verdict carries no ENGEL line."""
    commit = at
    digest = sha256(data)
    rows = [r for r in review_rows(root, commit) if r["target"] == path]
    cur = [r for r in rows if r["target_sha256"] == digest]
    notes = []
    older = [r for r in rows if r["target_sha256"] != digest]
    if older:
        notes.append(f"başka sürümlere {len(older)} inceleme (UYGUN DEĞİL: "
                     f"{sum(1 for r in older if r['verdict'] == 'UYGUN DEĞİL')}) — bu sürüm için sayılmaz")
    if not cur:
        return [f"inceleme yok — bu sürüm ({digest[:12]}) için {REVIEWS}'de satır yok" +
                (" (var olanlar bayat: paket incelemeden sonra değişti)" if older else "")], notes, set(), set()
    fails, engel, opened, accepted = [], {}, {}, set()
    problems = append_only_problems(root, commit, REVIEWS)
    if problems:
        return problems + ["inceleme dosyası güvenilmez — hiçbir inceleme sayılmaz"], notes, set(), set()
    for r in cur:
        tag = f"inceleme satır {r['_n']}"
        bad = []
        merge, why = human_step(root, main, row_commit(root, commit, r["_n"], REVIEWS), r["author"])
        if merge is None:
            bad.append(f"main'e insan adımıyla gelmedi — {why} (dal üzerindeki inceleme satırı sayılmaz)")
        if r["verdict"] not in VERDICTS:
            bad.append(f"sonuç '{r['verdict'][:20]}' geçersiz")
        if r["reviewer"] not in REVIEWERS:
            bad.append(f"inceleyen '{r['reviewer'][:20]}' codex ya da claude olmalı")
        for col in ("model", "effort", "reviewer_session", "author_session", "requested_by"):
            if NOT_GIVEN.match(r[col]):
                bad.append(f"{col} boş")
        if not PERSON.match(r["author"]):
            bad.append("author kişi anahtarı (K…) olmalı")
        if r["reviewer_session"] == r["author_session"] and not NOT_GIVEN.match(r["reviewer_session"]):
            bad.append("inceleme yazanın oturumunda yapılmış (beyan)")
        if not DATE.match(r["reviewed_at"]):
            bad.append("reviewed_at YYYY-AA-GG olmalı")
        if not r["blockers_open"].isdigit():
            bad.append("blockers_open sayı olmalı")
        if not FINDINGS_PATH.match(r["findings"]) or "//" in r["findings"] or not HEX64.match(r["findings_sha256"]):
            bad.append("bulgu dosyası yolu (docs/records/reviews/<kimlik>/<dosya>.md) ya da özeti geçersiz")
        if bad:
            fails += [f"{tag}: {b}" for b in bad]
            continue
        fdata = read(root, commit, r["findings"])
        if fdata is None:
            fails.append(f"{tag}: bulgu dosyası {r['findings']} bu noktada ({commit[:12]}) yok")
            continue
        if sha256(fdata) != r["findings_sha256"]:
            fails.append(f"{tag}: bulgu dosyası kayıttan sonra değişti (özet eşleşmiyor)")
            continue
        found, problems = parse_findings(fdata.decode("utf-8", errors="replace"), path, digest)
        if problems:
            fails += [f"{tag}: {x}" for x in problems]
            continue
        own = r["findings_sha256"][:8]
        mine = sum(1 for f in found if f["sev"] == "ENGEL" and not f["ref"] and f["closure"] == "açık")
        if mine != int(r["blockers_open"]):
            fails.append(f"{tag}: blockers_open {r['blockers_open']}, dosyada açık ENGEL {mine}")
        if r["verdict"] == "UYGUN DEĞİL":
            fails.append(f"{tag}: sonuç UYGUN DEĞİL — bu sürüm onaylanamaz; paket değişmeli ve yeniden incelenmeli")
        if r["verdict"] == "UYGUN" and any(f["sev"] == "ENGEL" for f in found):
            fails.append(f"{tag}: UYGUN sonucunda ENGEL satırı olamaz (kabul satırı KOŞULLU bir incelemeye yazılır)")
        for f in found:
            if f["sev"] != "ENGEL":
                continue
            if f["ref"]:
                key = (f["ref"], f["n"])
                if key not in engel:
                    fails.append(f"{tag}: F-{f['n']}@{f['ref']} bu sürümün önceki bir incelemesinde ENGEL değil")
                    continue
                opened.pop(key, None)
                accepted.add(f["owner"])
                continue
            engel[(own, f["n"])] = f
            if f["closure"] == "açık":
                opened[(own, f["n"])] = f"F-{f['n']}@{own}: {f['text'][:60]}"
            else:
                accepted.add(f["owner"])
    fails += [f"açık ENGEL: {v}" for v in opened.values()]
    if accepted:
        notes.append("ENGEL kabulü var — riski onaylayan üstlenir; onay HIGH sayılır")
    notes.append(f"bu sürüm için {len(cur)} inceleme: " + ", ".join(f"{r['reviewer']} {r['verdict']}" for r in cur))
    return fails, notes, accepted, set(engel)


def review_start(root: str, commit: str) -> str | None:
    """The commit that added POLICY-REVIEW-START (oldest), or None when the rule never started. Once added, its absence
    at `commit` is a refusal (Unreadable), not 'no rule'."""
    added = git(root, "log", "--format=%H", "--diff-filter=A", commit, "--", REVIEW_POLICY).decode().split()
    if not added:
        return None
    if read(root, commit, REVIEW_POLICY) is None:
        raise Unreadable("POLICY-REVIEW-START eklenmişti ama bu ref'te yok — kural silinerek kapatılamaz")
    return added[-1]


def legacy_text(root: str, start: str, path: str) -> str | None:
    """The pack as it was when its rule started (CT-APR-01 for a Module Pack, CT-APR-02 for a DCP), if it existed."""
    data = read(root, start, path)
    return data.decode("utf-8", errors="replace") if data is not None else None


def strictly_before(root: str, c: str, start: str) -> bool:
    return c != start and git(root, "merge-base", "--is-ancestor", c, start, ok_fail=True) is not None


def slices(text: str) -> list[str] | None:
    """The DL-n ids of the pack's one 'Teslim dilimleri' / 'Delivery Slices' table, or None. Strict (M2): outside fenced
    code only; the section holds no HTML comment and no indented line; its first non-blank line starts one contiguous
    table (header, separator, rows of '| DL-n | … |'); no other table line and no sub-heading inside the section."""
    lines, fence = [], None
    for l in text.replace("\r\n", "\n").split("\n"):
        m = FENCE.match(l.strip())
        if fence is None and m:
            fence = m.group(1)
        elif fence is not None and m and l.strip() == m.group(1) and m.group(1)[0] == fence[0] and len(m.group(1)) >= len(fence):
            fence = None
        elif fence is None:
            lines.append(l)                 # lines inside a fenced block do not count: an example is not the table
    if fence is not None:
        return None
    at = [i for i, l in enumerate(lines) if SLICE_HEADING.match(l.strip())]
    if len(at) != 1:
        return None
    end = next((k for k in range(at[0] + 1, len(lines)) if re.match(r"^#{1,6}\s", lines[k])), len(lines))
    body = lines[at[0] + 1:end]
    if any("<!--" in l or "-->" in l or re.match(r"^( {4}|\t)", l) for l in body):
        return None
    k = 0
    while k < len(body) and not body[k].strip():
        k += 1
    table = []
    while k < len(body) and body[k].startswith("|"):
        table.append(body[k].strip())
        k += 1
    if any(l.strip().startswith("|") for l in body[k:]) or len(table) < 3:
        return None
    if not re.fullmatch(r"\|(\s*:?-+:?\s*\|)+", table[1].replace(" ", "")):
        return None
    cells = lambda row: [c.strip() for c in row.strip()[1:-1].split("|")] if row.strip().endswith("|") else None
    head = cells(table[0])
    if head is None or len(head) < 3:
        return None
    titles = {re.sub(r"^\d+(\.\d+)*\.?\s*", "", l.lstrip("#").strip()).lower()
              for l in lines if re.match(r"^#{2,6}\s", l)}
    out = []
    for row in table[2:]:
        m, c = SLICE_ROW.match(row), cells(row)
        # Codex K1: every row has the header's cells, none empty, and the sections it covers are headings of the pack
        if not m or m.group(1) in out or c is None or len(c) != len(head) or any(NOT_GIVEN.match(x) for x in c):
            return None
        if any(sec.strip().lower() not in titles for sec in (c[2] if len(c) > 2 else "").split(",")):
            return None
        out.append(m.group(1))
    return out



def slice_rule(root: str, ref: str, path: str) -> tuple[bool, list[str] | None, str | None]:
    """(does the CT-APR-02 slice rule bind this pack at `ref`, its slice ids, the WP its last approval row allows to
    take the whole pack). It binds once an approval row at or after the start needed a review; an old pack, or one only
    re-approved for an exempt change (owner filled, a date), keeps the earlier rule (a warning only). whole_pack is read
    only from a last row that passes on its own — a HIGH approval by another person (B2)."""
    commit = commit_of(root, ref)
    if commit is None:
        raise Unreadable(f"ref '{ref[:40]}' çözülemedi")
    data = read(root, commit, path)
    if data is None:
        raise Unreadable(f"paket {path} bu ref'te yok")
    ctx = Ctx(root, commit, path)
    if ctx.rstart is None or ctx.policy is None:
        return False, None, None
    binds = any(r["action"] == "approve" and judge(ctx, i)[3] for i, r in enumerate(ctx.rows))
    whole = None
    last = len(ctx.rows) - 1
    if ctx.rows and ctx.rows[-1]["action"] == "approve" and not judge(ctx, last)[0] and not today_problems(ctx, last):
        m = WHOLE_PACK.search(ctx.rows[-1]["note"])
        whole = m.group(1) if m else None
    return binds, slice_ids(path, data.decode("utf-8", errors="replace")), whole



# ------------------------------------------------------------------------------------------------ the decision
def evaluate(root: str, ref: str, path: str) -> tuple[int, list[str]]:
    head = [f"PACK-APPROVAL v1 — {path} @ {ref}"]
    try:
        if not PACK_PATH.match(path or "") or "//" in path:
            raise Unreadable("paket yolu kanonik değil (execution/domains/<domain>/module-packs/<dosya>.md ya da "
                             "execution/portfolio/delivery-capability-packs/<dosya>.md; ./ ya da // yok)")
        commit = commit_of(root, ref)
        if commit is None:
            raise Unreadable(f"ref '{ref[:40]}' çözülemedi")
        added = git(root, "log", "--format=%H", "--diff-filter=A", commit, "--", POLICY).decode().split()
        if not added:
            return 0, head + ["kural yürürlükte değil (POLICY-START hiç eklenmedi) — eski onay kuralı geçerli", "çıkış: 0"]
        if read(root, commit, POLICY) is None:
            return 1, head + ["sonuç: POLICY-START eklenmişti ama bu ref'te yok — kural silinerek kapatılamaz", "çıkış: 1"]
        data = read(root, commit, path)
        if data is None:
            raise Unreadable(f"paket {path} bu ref'te yok")
        text = data.decode("utf-8", errors="replace")
        ctx = Ctx(root, commit, path)
        if is_dcp(path) and ctx.rstart is None:
            return 0, head + ["DCP onay kuralı yürürlükte değil (POLICY-REVIEW-START hiç eklenmedi)", "çıkış: 0"]
        last_change = git(root, "log", "-1", "--format=%H", commit, "--", path).decode().strip()
        rows = ctx.rows
        unchanged = git(root, "merge-base", "--is-ancestor", last_change, ctx.policy, ok_fail=True) is not None
        if unchanged and not rows:
            return 0, head + [f"paket kural başlangıcından ({ctx.policy[:12]}) beri değişmedi ve yeni kurala göre onay "
                              "satırı yok — eski onay geçerli, geriye dönük düşürülmez", "çıkış: 0"]
        problems = append_only_problems(root, commit) + append_only_problems(root, ctx.main)
        # Codex K4: only THIS pack's approve / revoke chain on main must be the ref's chain prefix (in order); another
        # pack's new approval on main does not force a branch update
        on_main = [r for r in approval_rows(root, ctx.main, path)]
        strip = lambda rs: [{k: v for k, v in r.items() if k != "_n"} for r in rs]
        if strip(rows[:len(on_main)]) != strip(on_main):
            problems.append("bu ref, main'deki bu paketin onay / iptal satırlarını aynı sırayla içermiyor (ör. bir iptal) — "
                            "dalı main ile güncelleyin")
        if problems:
            return 1, head + problems + ["sonuç: onay dosyası güvenilmez — hiçbir onay geçmez", "çıkış: 1"]
        if not rows:
            return 1, head + ["sonuç: onay yok — paket kural başlangıcından sonra değişti; onay satırı gerekli", "çıkış: 1"]
        row = rows[-1]
        if row["action"] == "revoke":
            return 1, head + [f"sonuç: son satır iptal ({row['approved_at']}) — onay yok", "çıkış: 1"]
        if row["pack_sha256"] != sha256(data):
            base = version_with_sha(root, commit, path, row["pack_sha256"])
            n = (sum(1 for l in difflib.unified_diff(base.split("\n"), text.split("\n"), lineterm="", n=0)
                     if l[:1] in "+-" and not l.startswith(("+++", "---"))) if base is not None else "?")
            return 1, head + [f"sonuç: paket onaydan sonra değişti ({n} satır) — yeniden onay gerekli", "çıkış: 1"]
        fails, out, effective, _ = judge(ctx, len(rows) - 1)
        fails = fails + today_problems(ctx, len(rows) - 1)
        out = head + out
        if fails:
            return 1, out + [f"ENGEL: {f}" for f in fails] + ["sonuç: onay geçersiz", "çıkış: 1"]
        return 0, out + [f"sonuç: onaylı — {effective} · onaylayan {row['approver']} · {row['approved_at']}", "çıkış: 0"]
    except (Unreadable, UnicodeDecodeError, IndexError, KeyError, ValueError, OSError) as e:
        return 2, head + [f"sonuç: okunamadı — {e}", "çıkış: 2"]


def make_row(root: str, ref: str, path: str, author: str, approver: str, risk: str, reason: str, when: str,
             note: str = "") -> str:
    commit = commit_of(root, ref)
    if commit is None or not PACK_PATH.match(path or ""):
        raise Unreadable("ref ya da paket yolu geçersiz")
    data = read(root, commit, path)
    if data is None:
        raise Unreadable(f"paket {path} yok")
    ctx = Ctx(root, commit, path)
    base, after_revoke = base_for(ctx, len(ctx.rows))
    computed, reasons = risk_of(path, data.decode("utf-8", errors="replace"), base, after_revoke)
    cells = [path, sha256(data), "approve", risk, reason, author, approver, when,
             sha256(card(path, data, computed, reasons).encode("utf-8")), note]
    if any("\t" in c or "\n" in c for c in cells):
        raise Unreadable("bir hücre sekme ya da satır sonu içeriyor")
    return "\t".join(cells)



def make_review_row(root: str, ref: str, path: str, findings: str, verdict: str, reviewer: str, model: str,
                    effort: str, reviewer_session: str, author: str, author_session: str, requested_by: str,
                    when: str) -> str:
    """CT-APR-02: the review row for the pack's version at `ref`, with both digests and the ENGEL count taken from the
    findings file itself; refuses what check would refuse in the row and the file."""
    commit = commit_of(root, ref)
    if commit is None or not PACK_PATH.match(path or "") or not FINDINGS_PATH.match(findings or ""):
        raise Unreadable("ref, paket yolu ya da bulgu dosyası yolu geçersiz")
    data, fdata = read(root, commit, path), read(root, commit, findings)
    if data is None or fdata is None:
        raise Unreadable("paket ya da bulgu dosyası bu ref'te yok (bulgu dosyası önce commit edilir)")
    found, problems = parse_findings(fdata.decode("utf-8", errors="replace"), path, sha256(data))
    if problems:
        raise Unreadable("; ".join(problems[:3]))
    if verdict not in VERDICTS or reviewer not in REVIEWERS or not PERSON.match(author) or not DATE.match(when):
        raise Unreadable("sonuç, inceleyen, yazan (K…) ya da tarih geçersiz")
    blockers = sum(1 for f in found if f["sev"] == "ENGEL" and not f["ref"] and f["closure"] == "açık")
    cells = [path, sha256(data), findings, sha256(fdata), verdict, str(blockers), reviewer, model, effort,
             reviewer_session, author, author_session, requested_by, when]
    if any("\t" in c or "\n" in c or not c.strip() for c in cells):
        raise Unreadable("bir hücre boş ya da sekme / satır sonu içeriyor")
    return "\t".join(cells)


# ------------------------------------------------------------------------------------------------ self-test
def self_test() -> int:
    os.environ[PR_VERIFY_ENV] = "test"          # the temp repositories have no GitHub origin
    cases = []
    tmp = tempfile.TemporaryDirectory()
    root = tmp.name
    DEV, LEAD, LEAD2, JUN = ("dev", "dev@x.invalid"), ("lead", "lead@x.invalid"), ("lead2", "lead2@x.invalid"), ("jun", "jun@x.invalid")

    def g(*args, who=DEV):
        env = dict(os.environ, GIT_AUTHOR_NAME=who[0], GIT_AUTHOR_EMAIL=who[1], GIT_COMMITTER_NAME=who[0],
                   GIT_COMMITTER_EMAIL=who[1], GIT_CONFIG_GLOBAL="/dev/null", GIT_CONFIG_SYSTEM="/dev/null")
        return subprocess.run(["git", "-C", root, *args], capture_output=True, env=env, check=True).stdout.decode()

    def put(rel, text):
        p = os.path.join(root, rel)
        os.makedirs(os.path.dirname(p), exist_ok=True)
        with open(p, "w", encoding="utf-8") as f:
            f.write(text)

    def get(rel):
        with open(os.path.join(root, rel), encoding="utf-8") as f:
            return f.read()

    def commit(msg, who=DEV):
        g("add", "-A", who=who)
        g("commit", "-q", "-m", msg, who=who)

    def via_branch(name, change, author=DEV, merger=LEAD):
        """Make `change` on a new branch from main as `author`, merge it into main with --no-ff as `merger`."""
        g("checkout", "-q", "main")
        g("checkout", "-q", "-b", name)
        change()
        commit(name, who=author)
        g("checkout", "-q", "main")
        g("merge", "-q", "--no-ff", "-m", f"Merge {name}", name, who=merger)

    def add_row(author, approver, risk, when, card_override=None):
        r = make_row(root, "HEAD", PACK, author, approver, risk, "test", when)
        if card_override:
            c = r.split("\t"); c[8] = card_override; r = "\t".join(c)
        cur = get(APPROVALS)
        put(APPROVALS, cur + ("" if cur.endswith("\n") else "\n") + r + "\n")

    def check(ref="HEAD"):
        return evaluate(root, ref, PACK)

    PACK = "execution/domains/x/module-packs/MOD-0001-x.md"
    v1 = "---\nid: MOD-0001\nname: X\nstatus: approved\n---\n# X\n\n## Repo Scope\n- services/Diten.X/**\n\n## Notes\n- a\n"
    g("init", "-q", "-b", "main")
    put(PACK, v1)
    put(APPROVERS, "login\tperson\trole\thigh\nlead\tK1\tkıdemli\tevet\nlead-alt\tK1\tkıdemli\tevet\n"
                   "lead2\tK2\tgeliştirici\tevet\njun\tK3\tjunior\thayır\ndev\tK4\tgeliştirici\tevet\n")
    commit("pack before the rule")
    code, out = check()
    cases.append(("no POLICY-START ever -> the earlier rule, exit 0", code == 0 and "yürürlükte değil" in out[-2]))
    put(POLICY, "# start\n")
    put(APPROVALS, "\t".join(APPROVAL_COLUMNS) + "\n")
    commit("policy start", who=LEAD)
    code, out = check()
    cases.append(("a pack unchanged since the start keeps its earlier approval", code == 0 and "geriye dönük" in out[-2]))
    put(PACK, v1 + "- b\n")
    commit("pack changed after the start")
    code, out = check()
    cases.append(("sabotage: changed after the start, no row -> exit 1", code == 1 and "onay yok" in out[-2]))

    via_branch("approve-1", lambda: add_row("dev", "lead", "HIGH", "2026-10-20"))
    code, out = check()
    cases.append(("positive control: first approval HIGH by a listed other person, merged by another -> exit 0", code == 0))

    # a Notes-only change is LOW and may be approved by its author; a Repo Scope change is HIGH
    put(PACK, get(PACK) + "- c\n")
    commit("notes line")
    code, out = check()
    cases.append(("sabotage: the pack changed after approval -> 'yeniden onay gerekli'", code == 1 and "yeniden onay" in out[-2]))
    via_branch("approve-2", lambda: add_row("dev", "dev", "LOW", "2026-10-21"), merger=DEV)
    code, out = check()
    cases.append(("positive control: a change under a non-sensitive heading is LOW; its author approves it -> exit 0",
                  code == 0 and "hesaplanan risk: LOW" in out))
    put(PACK, get(PACK).replace("- services/Diten.X/**\n", "- services/Diten.X/**\n- services/Diten.Y/**\n"))
    commit("scope line")
    via_branch("approve-3", lambda: add_row("dev", "dev", "LOW", "2026-10-22"), merger=LEAD)
    code, out = check()
    cases.append(("sabotage: declared LOW but a Repo Scope line changed -> HIGH required, exit 1",
                  code == 1 and any("yalnız yükseltilebilir" in l for l in out)))

    def high_attempt(name, approver, author=DEV, merger=LEAD, card_override=None):
        via_branch(name, lambda: add_row(author[0], approver, "HIGH", "2026-10-23", card_override), author=author, merger=merger)
        return check()

    code, out = high_attempt("h1", "dev")
    cases.append(("sabotage: HIGH approved by its own author -> exit 1", code == 1 and any("onaylayamaz" in l for l in out)))
    code, out = high_attempt("h2", "lead-alt", author=LEAD, merger=LEAD2)
    cases.append(("sabotage: HIGH approved by another account of the author (same person) -> exit 1",
                  code == 1 and any("aynı kişinin başka hesabı" in l for l in out)))
    code, out = high_attempt("h3", "jun")
    cases.append(("sabotage: HIGH approved by a junior (high = hayır) -> exit 1", code == 1 and any("onaylayamaz" in l for l in out)))
    code, out = high_attempt("h4", "ghost")
    cases.append(("sabotage: HIGH approved by someone not in the list -> exit 1", code == 1 and any("listesinde yok" in l for l in out)))

    def self_listing():
        put(APPROVERS, get(APPROVERS) + "dev-alt\tK9\tgeliştirici\tevet\n")
        add_row("dev", "dev-alt", "HIGH", "2026-10-24")
    via_branch("h5", self_listing, merger=LEAD)
    code, out = check()
    cases.append(("sabotage: the approver added to the list in the same PR as the row -> exit 1",
                  code == 1 and any("merge'den önceki onaylayıcı listesinde yok" in l for l in out)))
    code, out = high_attempt("h6", "lead", merger=DEV)
    cases.append(("sabotage: the row's author merged it himself -> no human step, exit 1",
                  code == 1 and any("insan adımı yok" in l for l in out)))
    g("checkout", "-q", "main")
    add_row("dev", "lead", "HIGH", "2026-10-25")
    commit("row straight on main")
    code, out = check()
    cases.append(("sabotage: the row written straight onto the main line -> no human step, exit 1",
                  code == 1 and any("doğrudan ana çizgiye" in l for l in out)))
    code, out = high_attempt("h7", "lead", card_override="0" * 64)
    cases.append(("sabotage: a hand-made card digest -> exit 1", code == 1 and any("kartının özeti" in l for l in out)))
    code, out = high_attempt("h8", "lead")
    cases.append(("positive control: a valid HIGH approval after the refusals -> exit 0", code == 0))

    # S-40: the row's branch takes main AFTER the row (card §7a: merge main before the push), then another person merges
    # it -> the merge that brought the row in is found; the same with the row's author merging it -> still no human step
    def row_then_main(name, merger):
        g("checkout", "-q", "main")
        put(PACK, get(PACK) + f"- {name}\n")
        commit(f"pack change {name}")
        g("checkout", "-q", "-b", name)
        add_row("dev", "lead", "HIGH", "2026-10-26")
        commit(f"{name} row", who=DEV)
        g("checkout", "-q", "main")
        put(f"other-{name}.txt", "x\n")
        commit(f"unrelated main commit {name}", who=LEAD2)
        g("checkout", "-q", name)
        g("merge", "-q", "--no-ff", "-m", f"Merge main into {name}", "main", who=DEV)
        g("checkout", "-q", "main")
        g("merge", "-q", "--no-ff", "-m", f"Merge {name}", name, who=merger)
        return check()
    code, out = row_then_main("s40a", LEAD)
    cases.append(("S-40: the row's branch merged main after the row, then another person merged it -> the bringing merge "
                  "is found, exit 0", code == 0))
    code, out = row_then_main("s40b", DEV)
    cases.append(("sabotage S-40: the same shape merged by the row's own author -> no human step, exit 1",
                  code == 1 and any("insan adımı yok" in l for l in out)))

    def row_branch(name):
        g("checkout", "-q", "main")
        put(PACK, get(PACK) + f"- {name}\n")
        commit(f"pack change {name}")
        g("checkout", "-q", "-b", name)
        add_row("dev", "lead", "HIGH", "2026-10-27")
        commit(f"{name} row", who=DEV)
    # F-5: the author merges the row himself, later another person merges something else -> the OLDEST bringing commit
    # (the author's own merge) decides, never a later merge by someone else
    row_branch("s40c")
    g("checkout", "-q", "main")
    g("merge", "-q", "--no-ff", "-m", "Merge s40c", "s40c", who=DEV)
    via_branch("s40c-other", lambda: put("other-s40c.txt", "x\n"), merger=LEAD)
    code, out = check()
    cases.append(("sabotage S-40/F-5: the author merged the row himself, another person merged something later -> the "
                  "oldest bringing merge (the author's) decides, exit 1", code == 1 and any("insan adımı yok" in l for l in out)))
    # F-1: foxtrot — the author self-merges the row; another person pulls main into an older branch with a merge and
    # main is fast-forwarded to it, so the author's merge leaves the first-parent line
    g("checkout", "-q", "main")
    g("checkout", "-q", "-b", "s40d-lead")
    put("lead-work-s40d.txt", "x\n")
    commit("lead work s40d", who=LEAD)
    row_branch("s40d")
    g("checkout", "-q", "main")
    g("merge", "-q", "--no-ff", "-m", "Merge s40d", "s40d", who=DEV)
    g("checkout", "-q", "s40d-lead")
    g("merge", "-q", "--no-ff", "-m", "Merge main into lead work", "main", who=LEAD)
    g("checkout", "-q", "main")
    g("merge", "-q", "--ff-only", "s40d-lead", who=LEAD)
    code, out = check()
    cases.append(("sabotage S-40/F-1: foxtrot — another person's pull-merge fast-forwarded past the author's self-merge "
                  "-> not on the merged branch's own line, exit 1", code == 1 and any("kendi çizgisinde değil" in l for l in out)))
    # F-2 (owner decision pending; fail-closed default): the author merges the row's branch into another branch himself,
    # another person merges that branch into main -> the row's own PR had no second person, exit 1
    row_branch("s40e")
    g("checkout", "-q", "main")
    g("checkout", "-q", "-b", "s40e-outer")
    put("outer-s40e.txt", "x\n")
    commit("outer s40e", who=LEAD)
    g("merge", "-q", "--no-ff", "-m", "Merge s40e into outer", "s40e", who=DEV)
    g("checkout", "-q", "main")
    g("merge", "-q", "--no-ff", "-m", "Merge outer", "s40e-outer", who=LEAD)
    code, out = check()
    cases.append(("sabotage S-40/F-2: nested — the author merged the row's branch into another branch, another person "
                  "merged that one -> exit 1", code == 1 and any("kendi çizgisinde değil" in l for l in out)))
    # positive control after the refusals: the S-40 shape merged by another person still passes
    code, out = row_then_main("s40f", LEAD)
    cases.append(("S-40: positive control after the refusals -> exit 0", code == 0))
    # Codex F-6: the row written straight onto main, an unrelated commit after it, then another person's branch pulls
    # main with a merge and main is fast-forwarded to it -> the graph looks like a PR merge; GitHub says it is not one
    g("checkout", "-q", "main")
    put(PACK, get(PACK) + "- s40g\n")
    commit("pack change s40g")
    g("checkout", "-q", "-b", "s40g-lead")
    put("lead-work-s40g.txt", "x\n")
    commit("lead work s40g", who=LEAD)
    g("checkout", "-q", "main")
    add_row("dev", "lead", "HIGH", "2026-10-28")
    commit("row straight on main s40g", who=DEV)
    put("after-s40g.txt", "x\n")
    commit("unrelated after the row", who=DEV)
    g("checkout", "-q", "s40g-lead")
    g("merge", "-q", "--no-ff", "-m", "Merge main into s40g-lead", "main", who=LEAD)
    g("checkout", "-q", "main")
    g("merge", "-q", "--ff-only", "s40g-lead", who=LEAD)
    code, out = check()
    cases.append(("sabotage S-40/Codex F-6: a row straight on main carried by another person's pull-merge and a "
                  "fast-forward -> not a pull request's merge (GitHub), exit 1",
                  code == 1 and any("PR'ın merge commit'i değil" in l for l in out)))
    # the test stand-in is refused in a repository with a GitHub remote: the real GitHub answer is required (F-4: any
    # remote name, host case, trailing '/'), and a fake GitHub drives every decision line (independent review F-2)
    saved = globals()["_gh_json"]
    pr_merge_sha = git(root, "rev-parse", "HEAD").decode().strip()
    answers: dict = {}
    def fake_gh(path):
        if path not in answers:
            raise Unreadable("ağ yok (test)")
        return answers[path]
    globals()["_gh_json"] = fake_gh
    g("remote", "add", "upstream", "https://GitHub.com/example-owner/example-repo/")
    _PR_CACHE.clear()
    ok, why = pr_person_check(root, pr_merge_sha, approver="lead")
    cases.append(("sabotage S-40/F-4: PACK_APPROVAL_PR_VERIFY=test is ignored with a GitHub remote (named upstream, "
                  "'GitHub.com', trailing '/') -> GitHub is asked; unreachable = no human step",
                  not ok and "GitHub'a sorulamadı" in why))
    slug = "example-owner/example-repo"
    def pr(**over):
        base = {"number": 7, "merged_at": "2026-10-22T10:00:00Z", "merge_commit_sha": pr_merge_sha,
                "user": {"login": "dev"}, "merged_by": {"login": "lead"}, "base": {"ref": "main"}}
        base.update(over)
        return base
    def ask(approver=None, author_key=None, **over):
        _PR_CACHE.clear()
        answers.clear()
        answers[f"repos/{slug}/commits/{pr_merge_sha}/pulls"] = [pr(**over)]
        answers[f"repos/{slug}/pulls/7"] = pr(**over)
        return pr_person_check(root, pr_merge_sha, approver=approver, author_key=author_key)
    ok, why = ask(approver="lead")
    cases.append(("S-40 GitHub: a merged PR into main, opened by dev, merged by the approver lead -> human step", ok))
    for label, kw, text in (
            ("merged by someone other than the approval row's approver (F-1)", dict(approver="lead2"), "onaylayan"),
            ("the merger is the review row's author (F-1)", dict(author_key="K1"), "satırın yazarı"),
            ("opened and merged by the same person", dict(approver="lead", user={"login": "lead"}), "aynı kişi"),
            ("opened by another account of the merger (same person key)", dict(approver="lead", user={"login": "lead-alt"}), "aynı kişi"),
            ("merged by a login not on the approver list", dict(merged_by={"login": "ghost"}), "listesinde yok"),
            ("merged into another branch, not main (F-3)", dict(approver="lead", base={"ref": "dev"}), "hedefi main değil"),
            ("no merged_at (closed, not merged)", dict(approver="lead", merged_at=None), "merge commit'i değil"),
            ("another merge commit sha", dict(approver="lead", merge_commit_sha="0" * 40), "merge commit'i değil")):
        ok, why = ask(**kw)
        cases.append((f"sabotage S-40 GitHub: {label} -> no human step", not ok and text in why))
    _PR_CACHE.clear()
    answers.clear()
    answers[f"repos/{slug}/commits/{pr_merge_sha}/pulls"] = {"message": "not a list"}
    ok, why = pr_person_check(root, pr_merge_sha, approver="lead")
    cases.append(("sabotage S-40 GitHub (F-6): a 2xx answer that is not a list -> not measurable, no human step",
                  not ok and "liste değil" in why))
    g("remote", "remove", "upstream")
    globals()["_gh_json"] = saved
    _PR_CACHE.clear()

    # B2 (independent review): the human step is measured on the MAIN line, not on the checked ref.
    # (a) a worker branch forked BEFORE the row reached main, later merged main in by the row's own author
    g("checkout", "-q", "main")
    put(PACK, get(PACK) + "- e\n")
    commit("notes before the worker forks")
    g("checkout", "-q", "-b", "worker")
    put("work.txt", "w\n")
    commit("worker change")
    via_branch("approve-w", lambda: add_row("dev", "lead", "HIGH", "2026-10-25"))     # dev's row, merged by lead
    g("checkout", "-q", "worker")
    g("merge", "-q", "--no-ff", "-m", "Merge main into worker", "main", who=DEV)
    code_w, out_w = check("worker")
    cases.append(("a worker branch forked before the row and synced with main by the row's author -> exit 0 like main "
                  "(no false refusal)", code_w == 0))
    # (b) a branch forked BEFORE a row written straight onto main, synced with main by someone else
    g("checkout", "-q", "main")
    put(PACK, get(PACK) + "- f\n")
    commit("notes again")
    g("checkout", "-q", "-b", "laundry")
    put("l.txt", "l\n")
    commit("other work", who=LEAD)
    g("checkout", "-q", "main")
    add_row("dev", "lead", "HIGH", "2026-10-26")
    commit("row straight on main again")
    g("checkout", "-q", "laundry")
    g("merge", "-q", "--no-ff", "-m", "Merge main into laundry", "main", who=LEAD)
    code_l, out_l = check("laundry")
    cases.append(("sabotage: a row written straight onto main, checked on a branch someone else synced -> exit 1",
                  code_l == 1 and any("doğrudan ana çizgiye" in l for l in out_l)))

    # revoke, then the author re-approves the same bytes himself as LOW
    g("checkout", "-q", "main")
    via_branch("approve-9", lambda: add_row("dev", "lead", "HIGH", "2026-10-27"))
    rows = [l for l in get(APPROVALS).split("\n") if l]
    last = rows[-1].split("\t"); last[2], last[7] = "revoke", "2026-10-28"
    via_branch("revoke-1", lambda: put(APPROVALS, get(APPROVALS) + "\t".join(last) + "\n"), author=LEAD, merger=LEAD2)
    code, out = check()
    cases.append(("sabotage: the last row revokes -> exit 1", code == 1 and "iptal" in out[-2]))
    via_branch("reapprove-self", lambda: add_row("dev", "dev", "LOW", "2026-10-29"), merger=DEV)
    code, out = check()
    cases.append(("sabotage: after a revoke, the author re-approves the same bytes as LOW himself -> exit 1",
                  code == 1 and any("iptalden sonra" in l for l in out)))

    # a newline-only change of the approvals file is not a deletion; a deleted row is
    g("checkout", "-q", "main")
    put(APPROVALS, get(APPROVALS).rstrip("\n"))
    commit("strip the final newline", who=LEAD)
    via_branch("approve-10", lambda: add_row("dev", "lead", "HIGH", "2026-10-30"))
    code, out = check()
    cases.append(("a newline-only edit of the approvals file does not lock approvals; a fresh HIGH approval -> exit 0",
                  code == 0))
    lines = get(APPROVALS).split("\n")
    put(APPROVALS, "\n".join(lines[:2] + lines[3:]))
    commit("delete a row", who=LEAD)
    code, out = check()
    cases.append(("sabotage: a row deleted from the approvals file -> untrusted, exit 1",
                  code == 1 and any("yalnız ekleme değil" in l for l in out)))

    # the rule file cannot be switched off; aliases and bad refs are unreadable
    os.remove(os.path.join(root, POLICY))
    commit("remove the policy", who=LEAD)
    code, out = check()
    cases.append(("sabotage: POLICY-START deleted after it was added -> exit 1", code == 1 and "silinerek" in out[-2]))
    code, out = evaluate(root, "HEAD", "./" + PACK)
    cases.append(("sabotage: a './' path alias -> unreadable (exit 2), never a pass", code == 2))
    code, out = evaluate(root, "mian", PACK)
    cases.append(("sabotage: a mistyped ref -> unreadable (exit 2), never 'rule not in force'", code == 2))

    cases.append(("risk: a first approval is HIGH", pack_risk("a", None)[0] == "HIGH"))
    cases.append(("risk: prose under a non-sensitive heading that says 'gateway' is LOW",
                  pack_risk("# A\n## Notes\n- the gateway is fine\n", "# A\n## Notes\n- ok\n")[0] == "LOW"))
    cases.append(("risk: a changed künye id is HIGH", pack_risk("---\nid: MOD-0002\n---\n", "---\nid: MOD-0001\n---\n")[0] == "HIGH"))
    cases.append(("risk: a line naming ocelot.json anywhere is HIGH",
                  pack_risk("# A\n## Notes\n- see ocelot.json\n", "# A\n## Notes\n- ok\n")[0] == "HIGH"))
    tmp.cleanup()
    self_test_review(cases)
    for label, ok in cases:
        print(("PASS " if ok else "FAIL ") + label)
    passed = sum(1 for _, ok in cases if ok)
    print(f"self-test: {passed}/{len(cases)} passed")
    return 0 if passed == len(cases) else 1


def self_test_review(cases: list) -> None:
    """CT-APR-02: review before approval, DCPs, exemptions and slices — each rule with its sabotage."""
    tmp = tempfile.TemporaryDirectory()
    root = tmp.name
    DEV, LEAD, LEAD2 = ("dev", "dev@x.invalid"), ("lead", "lead@x.invalid"), ("lead2", "lead2@x.invalid")

    def g(*args, who=DEV):
        env = dict(os.environ, GIT_AUTHOR_NAME=who[0], GIT_AUTHOR_EMAIL=who[1], GIT_COMMITTER_NAME=who[0],
                   GIT_COMMITTER_EMAIL=who[1], GIT_CONFIG_GLOBAL="/dev/null", GIT_CONFIG_SYSTEM="/dev/null")
        return subprocess.run(["git", "-C", root, *args], capture_output=True, env=env, check=True).stdout.decode()

    def put(rel, text):
        full = os.path.join(root, rel)
        os.makedirs(os.path.dirname(full), exist_ok=True)
        with open(full, "w", encoding="utf-8") as f:
            f.write(text)

    def get(rel):
        with open(os.path.join(root, rel), encoding="utf-8") as f:
            return f.read()

    def append(rel, line):
        cur = get(rel)
        put(rel, cur + ("" if cur.endswith("\n") else "\n") + line + "\n")

    def commit(msg, who=DEV):
        g("add", "-A", who=who)
        g("commit", "-q", "-m", msg, who=who)

    def via_branch(name, change, author=DEV, merger=LEAD, merge=True):
        g("checkout", "-q", "main")
        g("checkout", "-q", "-b", name)
        change()
        commit(name, who=author)
        g("checkout", "-q", "main")
        if merge:
            g("merge", "-q", "--no-ff", "-m", f"Merge {name}", name, who=merger)

    def approve(path, author, approver, risk, note=""):
        append(APPROVALS, make_row(root, "HEAD", path, author, approver, risk, "test", "2026-10-21", note))

    n = [0]

    def review(path, verdict, findings, blockers, reviewer="claude", r_session="rs-1", a_session="as-1"):
        """Writes a findings file for the pack's current version and appends its review row."""
        n[0] += 1
        digest = sha256(get(path).encode("utf-8"))
        rel = f"docs/records/reviews/t/{n[0]:02d}-{digest[:8]}.md"
        put(rel, f"# İnceleme\n\nhedef: {path} @ {digest}\n\n" + "".join(l + "\n" for l in findings))
        fsha = sha256(get(rel).encode("utf-8"))
        append(REVIEWS, "\t".join([path, digest, rel, fsha, verdict, str(blockers), reviewer, "claude-opus-5-5", "high",
                                   r_session, "K4", a_session, "dev", "2026-10-21"]))
        return rel, fsha

    def check(path, ref="HEAD"):
        return evaluate(root, ref, path)

    has = lambda out, text: any(text in l for l in out)
    PACK = "execution/domains/x/module-packs/MOD-0001-x.md"
    DCP = DCP_ROOT + "DCP-001-x.md"
    v1 = ("---\nid: MOD-0001\nname: X\nstatus: approved\nowner: TBD\nupdated: 2026-10-01\n---\n# X\n\n## Notes\n- a\n\n"
          "## Teslim dilimleri\n| dilim | ad | bölümler |\n|---|---|---|\n| DL-1 | çekirdek | Notes |\n| DL-2 | ekran | Notes |\n")
    d1 = ("---\nid: DCP-001\nslug: x\nstatus: approved\n---\n# DCP-001\n\n## 8. Ordered delivery sequence\n1. MOD-0001\n\n"
          "## 20. Audit and reconciliation notes\n- n\n")
    PACK2 = "execution/domains/x/module-packs/MOD-0002-y.md"
    v2 = "---\nid: MOD-0002\nname: Y\nstatus: approved\nowner: TBD\n---\n# Y\n\n## Notes\n- a\n"
    g("init", "-q", "-b", "main")
    put(PACK, v1)
    put(PACK2, v2)
    put(DCP, d1)
    put(APPROVERS, "login\tperson\trole\thigh\nlead\tK1\tkıdemli\tevet\nlead2\tK2\tgeliştirici\tevet\n"
                   "dev\tK4\tgeliştirici\tevet\n")
    put(POLICY, "# start\n")
    put(APPROVALS, "\t".join(APPROVAL_COLUMNS) + "\n")
    commit("CT-APR-01 start", who=LEAD)
    code, out = check(DCP)
    cases.append(("CT-APR-02: before POLICY-REVIEW-START a DCP has no approval rule -> exit 0",
                  code == 0 and "yürürlükte değil" in out[-2]))
    put(PACK, get(PACK).replace("- a\n", "- a\n- b\n"))
    commit("pack change under CT-APR-01")
    via_branch("old-approval", lambda: approve(PACK, "dev", "lead", "HIGH"))
    put(REVIEW_POLICY, "# review start\n")
    put(REVIEWS, "\t".join(REVIEW_COLUMNS) + "\n")
    commit("CT-APR-02 start", who=LEAD)
    code, out = check(PACK)
    cases.append(("CT-APR-02: an approval row from before the start needs no review (no retroactive burden) -> exit 0",
                  code == 0 and has(out, "kural dışı")))
    cases.append(("CT-APR-02: an unchanged pack approved before the start is not bound by the slice rule",
                  slice_rule(root, "HEAD", PACK)[0] is False))
    code, out = check(DCP)
    cases.append(("CT-APR-02: a DCP unchanged since the start keeps its earlier approval -> exit 0", code == 0))

    # a body change approved after the start, without a review
    put(PACK, get(PACK).replace("- b\n", "- b\n- c\n"))
    commit("pack body change")
    via_branch("a1", lambda: approve(PACK, "dev", "dev", "LOW"), merger=DEV)
    code, out = check(PACK)
    cases.append(("sabotage CT-APR-02: a body change approved LOW without a review -> exit 1 'inceleme yok'",
                  code == 1 and has(out, "inceleme yok")))
    via_branch("r1-branch-only", lambda: review(PACK, "UYGUN", [], 0), merge=False)
    code, out = check(PACK, "r1-branch-only")
    cases.append(("sabotage CT-APR-02: a review row only on the worker branch does not count -> exit 1",
                  code == 1 and has(out, "inceleme yok")))
    via_branch("r1-self-merge", lambda: review(PACK, "UYGUN", [], 0), merger=DEV)
    code, out = check(PACK)
    cases.append(("sabotage CT-APR-02: a review row merged by its own author does not count -> exit 1",
                  code == 1 and has(out, "aynı yazar")))
    g("reset", "-q", "--hard", "HEAD~1")
    via_branch("r1", lambda: review(PACK, "UYGUN", ["F-1 | DÜŞÜK | yazım | kapandı | - | -"], 0))
    code, out = check(PACK)
    cases.append(("sabotage CT-APR-02 (Codex K2): a positive review that arrives AFTER the approval row does not make it "
                  "valid -> exit 1", code == 1 and has(out, "onay satırından sonra geldi")))
    via_branch("a1b", lambda: approve(PACK, "dev", "dev", "LOW"), merger=LEAD)
    code, out = check(PACK)
    cases.append(("positive control CT-APR-02: a new approval row after a valid UYGUN review merged by another -> exit 0",
                  code == 0 and has(out, "inceleme (CT-APR-02): gerekli")))
    findings = [l for l in g("ls-files", "docs/records/reviews").split() if l]
    keep = g("rev-parse", "HEAD").strip()
    via_branch("edit-findings", lambda: put(findings[-1], get(findings[-1]) + "ek\n"))
    code, out = check(PACK)
    cases.append(("sabotage CT-APR-02: the findings file edited after the review (merged to main) -> exit 1 (digest)",
                  code == 1 and has(out, "özet eşleşmiyor")))
    g("reset", "-q", "--hard", keep)
    g("checkout", "-q", "-b", "edit-pack")
    put(PACK, get(PACK).replace("- c\n", "- c\n- d\n"))
    commit("pack changed after the review")
    via_branch_sha = check(PACK)
    cases.append(("sabotage CT-APR-02: the pack changed after the approval -> exit 1", via_branch_sha[0] == 1))
    g("checkout", "-q", "main")

    # exemptions: owner filled from TBD and a date — no review; an existing owner changing needs one
    put(PACK, get(PACK).replace("owner: TBD", "owner: AT").replace("updated: 2026-10-01", "updated: 2026-10-21"))
    commit("owner filled")
    via_branch("a2", lambda: approve(PACK, "dev", "dev", "LOW"), merger=DEV)
    code, out = check(PACK)
    cases.append(("positive control CT-APR-02: owner filled from TBD + a date -> exempt, no review, exit 0",
                  code == 0 and has(out, "gerekmez")))
    put(PACK, get(PACK).replace("owner: AT", "owner: BT"))
    commit("owner changed")
    via_branch("a3", lambda: approve(PACK, "dev", "dev", "LOW"), merger=DEV)
    code, out = check(PACK)
    cases.append(("sabotage CT-APR-02: an existing owner changed -> not exempt, no review -> exit 1",
                  code == 1 and has(out, "inceleme yok")))

    # KOŞULLU with an open ENGEL stops; an approver's acceptance in a later review closes it and makes the approval HIGH
    via_branch("r2", lambda: review(PACK, "KOŞULLU", ["F-1 | ENGEL | kapsam belirsiz | açık | - | -"], 1))
    code, out = check(PACK)
    cases.append(("sabotage CT-APR-02: KOŞULLU with an open ENGEL -> exit 1", code == 1 and has(out, "açık ENGEL")))
    sha8 = [l for l in get(REVIEWS).split("\n") if l.strip()][-1].split("\t")[3][:8]
    via_branch("r3", lambda: review(PACK, "UYGUN", [], 0))
    code, out = check(PACK)
    cases.append(("sabotage CT-APR-02: a later UYGUN does not override an earlier open ENGEL -> exit 1",
                  code == 1 and has(out, "açık ENGEL")))
    via_branch("r4x", lambda: review(PACK, "KOŞULLU", [f"F-1@{sha8} | ENGEL | kapsam belirsiz | {ACCEPTED} | K99 | -"], 0),
               merge=False)
    keep = g("rev-parse", "HEAD").strip()
    g("merge", "-q", "--no-ff", "-m", "Merge r4x", "r4x", who=LEAD)
    via_branch("a4x", lambda: approve(PACK, "dev", "lead", "HIGH"))
    code, out = check(PACK)
    cases.append(("sabotage CT-APR-02 (m3): an ENGEL accepted in the name of someone other than the approver -> exit 1",
                  code == 1 and has(out, "ENGEL kabulü onaylayana ait değil")))
    g("reset", "-q", "--hard", keep)
    via_branch("r4", lambda: review(PACK, "KOŞULLU", [f"F-1@{sha8} | ENGEL | kapsam belirsiz | {ACCEPTED} | K1 | -"], 0))
    via_branch("a4low", lambda: approve(PACK, "dev", "dev", "LOW"), merger=DEV)
    code, out = check(PACK)
    cases.append(("CT-APR-02: an accepted ENGEL makes the approval HIGH — the author's LOW self-approval then fails",
                  code == 1 and has(out, "ENGEL kabulü") and has(out, "insan adımı yok")))
    via_branch("a4", lambda: approve(PACK, "dev", "lead", "HIGH"))
    code, out = check(PACK)
    cases.append(("positive control CT-APR-02: the accepted ENGEL with a HIGH approval by another person -> exit 0",
                  code == 0 and has(out, "ENGEL kabulü")))

    # UYGUN DEĞİL stops for that version, a later UYGUN cannot lift it; a declared count must match the file
    put(PACK, get(PACK).replace("- d\n", "").replace("- c\n", "- c\n- e\n"))
    commit("pack change 2")
    via_branch("a5", lambda: (approve(PACK, "dev", "lead", "HIGH"),
                              review(PACK, "UYGUN DEĞİL", ["F-1 | ENGEL | yanlış modül | açık | - | -"], 1)))
    via_branch("r5", lambda: review(PACK, "UYGUN", [], 0))
    code, out = check(PACK)
    cases.append(("sabotage CT-APR-02: UYGUN DEĞİL on this version, then a UYGUN row -> still exit 1",
                  code == 1 and has(out, "UYGUN DEĞİL — bu sürüm onaylanamaz")))
    put(PACK, get(PACK).replace("- e\n", "- e\n- f\n"))
    commit("pack change 3")
    via_branch("a6", lambda: (approve(PACK, "dev", "lead", "HIGH"),
                              review(PACK, "KOŞULLU", ["F-1 | ENGEL | eksik test | açık | - | -"], 0)))
    code, out = check(PACK)
    cases.append(("sabotage CT-APR-02: blockers_open 0 while the file has an open ENGEL -> exit 1",
                  code == 1 and has(out, "blockers_open 0, dosyada açık ENGEL 1")))
    via_branch("a6b", lambda: review(PACK, "UYGUN", [], 0, r_session="same", a_session="same"))
    code, out = check(PACK)
    cases.append(("sabotage CT-APR-02: a review declared in the author's own session -> exit 1",
                  code == 1 and has(out, "yazanın oturumunda")))

    # findings lines: broken, unknown closure, open condition without owner and date
    probe = lambda lines: parse_findings("hedef: P @ D\n" + "\n".join(lines) + "\n", "P", "D")[1]
    cases.append(("sabotage CT-APR-02: a malformed finding line is a problem, not ignored",
                  bool(probe(["F-1 ENGEL kapsam"])) and bool(probe(["- ENGEL: gizli"]))))
    cases.append(("sabotage CT-APR-02: an unknown closure ('düzeltildi@abc') is refused",
                  bool(probe(["F-1 | ENGEL | x | düzeltildi@abc | - | -"]))))
    cases.append(("sabotage CT-APR-02: an open ORTA condition needs an owner and a due date",
                  bool(probe(["F-1 | ORTA | x | açık | - | -"])) and not probe(["F-1 | ORTA | x | açık | K2 | 2026-11-01"])))
    cases.append(("sabotage CT-APR-02: a findings file bound to another target is refused",
                  bool(parse_findings("hedef: Q @ D\n", "P", "D")[1])))

    # DCP risk: only audit notes / future follow-ups are LOW; front matter, delivery sequence, unknown heading are HIGH
    cases.append(("CT-APR-02 DCP: a line under 'Audit and reconciliation notes' is LOW",
                  dcp_risk(d1 + "- m\n", d1)[0] == "LOW"))
    cases.append(("CT-APR-02 DCP: the delivery sequence, the front matter or an unknown heading is HIGH",
                  dcp_risk(d1.replace("1. MOD-0001", "1. MOD-0002"), d1)[0] == "HIGH"
                  and dcp_risk(d1.replace("slug: x", "slug: y"), d1)[0] == "HIGH"
                  and dcp_risk(d1 + "\n## Misc\n- z\n", d1)[0] == "HIGH"))
    put(DCP, d1 + "- m\n")
    commit("dcp change")
    code, out = check(DCP)
    cases.append(("sabotage CT-APR-02: a DCP changed after the start without an approval row -> exit 1",
                  code == 1 and "onay yok" in out[-2]))

    # exemption edge cases (production function)
    base = "---\nowner: TBD\nupdated: 2026-10-01\n---\n# A\n- x\n"
    cases.append(("CT-APR-02 exempt: trailing whitespace only", review_exempt(base.replace("- x", "- x   "), base)[0]
                  and not review_exempt(base.replace("- x", "    - x"), base)[0]))
    cases.append(("sabotage CT-APR-02 exempt: a second owner line is not exempt",
                  not review_exempt(base.replace("owner: TBD\n", "owner: TBD\nowner: K\n"), base)[0]))
    cases.append(("sabotage CT-APR-02 exempt: a first approval is never exempt", not review_exempt(base, None)[0]))

    # slices: the rule binds a pack changed after the start; whole_pack comes from the last approval row's note
    binds, ids, whole = slice_rule(root, "HEAD", PACK)
    cases.append(("CT-APR-02 slices: bound after the start, DL-1/DL-2 read from the pack, no whole-pack mark",
                  binds and ids == ["DL-1", "DL-2"] and whole is None))
    put(PACK, get(PACK).replace("- f\n", "- f\n- h\n"))
    commit("pack change 5")
    via_branch("a7self", lambda: (review(PACK, "UYGUN", [], 0), approve(PACK, "dev", "dev", "LOW", note="whole_pack=WP-9")),
               merger=LEAD)
    code, out = check(PACK)
    cases.append(("sabotage CT-APR-02 (B2): the author grants whole_pack to himself with a LOW row -> exit 1, no whole_pack",
                  code == 1 and has(out, "whole_pack işareti") and slice_rule(root, "HEAD", PACK)[2] is None))
    via_branch("a7", lambda: approve(PACK, "dev", "lead", "HIGH", note="whole_pack=WP-9"))
    cases.append(("CT-APR-02 slices: whole_pack=<WP> is read from the last approval row",
                  slice_rule(root, "HEAD", PACK)[2] == "WP-9"))
    # an old pack re-approved only for an exempt change stays unbound; a reviewed body change needs a slice table
    put(PACK2, get(PACK2).replace("owner: TBD", "owner: AT"))
    commit("pack2 owner filled")
    via_branch("b1", lambda: approve(PACK2, "dev", "lead", "HIGH"))      # CT-APR-01: a first approval is HIGH
    code, out = check(PACK2)
    cases.append(("CT-APR-02: an old pack's first approval for an exempt change (owner filled) needs no review and is "
                  "not bound by slices -> exit 0", code == 0 and has(out, "gerekmez") and slice_rule(root, "HEAD", PACK2)[0] is False))
    put(PACK2, get(PACK2) + "- b\n")
    commit("pack2 body change")
    via_branch("b2", lambda: (approve(PACK2, "dev", "lead", "HIGH"), review(PACK2, "UYGUN", [], 0)))
    code, out = check(PACK2)
    cases.append(("sabotage CT-APR-02: a reviewed change of a pack without a 'Teslim dilimleri' table -> exit 1",
                  code == 1 and has(out, "dilim tablosu yok")))
    put(PACK2, get(PACK2) + "\n## Teslim dilimleri\n| dilim | ad | bölümler |\n|---|---|---|\n| DL-1 | tümü | Notes |\n")
    commit("pack2 one slice")
    via_branch("b3", lambda: (approve(PACK2, "dev", "lead", "HIGH"), review(PACK2, "UYGUN", [], 0)))
    code, out = check(PACK2)
    cases.append(("sabotage CT-APR-02: a single-slice pack without whole_pack in the approval row -> exit 1",
                  code == 1 and has(out, "tek dilim")))
    cases.append(("CT-APR-02: once a review was needed, a later exempt re-approval does not unbind the pack",
                  slice_rule(root, "HEAD", PACK2)[0] is True))
    dcp_sl = d1.replace("## 20.", "## 8.1 Delivery slices\n\n| Slice | Kapsam | Not |\n|---|---|---|\n| MVP1 | MOD-0001 | a |\n"
                                  "| MVP2 | MOD-0002 | b |\n\n## 20.")
    cases.append(("CT-APR-02 DCP: slice ids are read from '## 8.1 Delivery slices'",
                  slice_ids(DCP, dcp_sl) == ["MVP1", "MVP2"] and slice_ids(DCP, d1) is None))
    fenced = v2 + "\n```markdown\n## Teslim dilimleri\n| dilim | ad | bölümler |\n|---|---|---|\n| DL-1 | a | Notes |\n| DL-2 | c | Notes |\n```\n"
    cases.append(("sabotage CT-APR-02: a slice table inside a code fence (an example) is not the pack's table",
                  slices(fenced) is None and slice_ids(DCP, d1 + "\n```\n## 8.1 Delivery slices\n\n| Slice | Kapsam | Not |\n"
                                                         "|---|---|---|\n| MVP1 | MOD-0001 | a |\n```\n") is None))
    cases.append(("sabotage CT-APR-02 DCP: a slice table the routing parser refuses (a WP id as scope) is no table",
                  slice_ids(DCP, dcp_sl.replace("| MVP2 | MOD-0002 |", "| MVP2 | WP-1 |")) is None))
    put(PACK, get(PACK).replace("- f\n", "- f\n- g\n"))
    put("docs/records/reviews/t/cli.md", "x\n")
    commit("pack change 4 + findings placeholder")
    put("docs/records/reviews/t/cli.md", f"hedef: {PACK} @ {sha256(get(PACK).encode('utf-8'))}\n"
                                         "F-1 | ORTA | adlandırma | açık | K2 | 2026-11-01\n")
    commit("findings")
    row = make_review_row(root, "HEAD", PACK, "docs/records/reviews/t/cli.md", "KOŞULLU", "codex", "gpt", "high", "rs-9",
                          "K4", "as-9", "dev", "2026-10-21")
    via_branch("cli", lambda: (approve(PACK, "dev", "lead", "HIGH"), append(REVIEWS, row)))
    code, out = check(PACK)
    cases.append(("positive control CT-APR-02: review-row output (digests and ENGEL count from the file) passes check",
                  code == 0 and row.split("\t")[5] == "0"))
    # Codex K4: another pack's new approval on main does not stop this pack's branch
    k4 = g("rev-parse", "HEAD").strip()
    via_branch("k4-other", lambda: approve(PACK2, "dev", "lead", "HIGH"))
    code, out = check(PACK, k4)
    cases.append(("CT-APR-02 (Codex K4): an unrelated pack's new row on main does not stop this pack's older ref -> exit 0",
                  code == 0))
    g("reset", "-q", "--hard", k4)
    # Codex K5: a merge that moves a revoke in front of the approval it revoked is not 'append only'
    # the first parent's rows stay a prefix; only the ORDER of the other parent's rows (A1 then R1) is broken
    lines_now = [l for l in get(APPROVALS).split("\n") if l.strip()]
    a1 = lines_now[-1].split("\t"); a1[7] = "2026-10-30"
    r1 = list(a1); r1[2], r1[7] = "revoke", "2026-10-31"
    g("checkout", "-q", "-b", "k5")
    append(APPROVALS, "\t".join(a1))
    commit("A1 on a branch")
    append(APPROVALS, "\t".join(r1))
    commit("R1 on the branch")
    g("checkout", "-q", "main")
    put("y.txt", "main side\n")
    commit("main moves")
    g("merge", "-q", "--no-commit", "--no-ff", "k5", who=LEAD)
    put(APPROVALS, "\n".join(lines_now + ["\t".join(r1), "\t".join(a1)]) + "\n")
    commit("merge k5, R1 placed before A1", who=LEAD)
    cases.append(("sabotage CT-APR-02 (Codex K5): a merge that keeps every row but moves the approval A1 behind its revoke "
                  "R1 -> append-only problem", bool(append_only_problems(root, "HEAD"))))
    g("reset", "-q", "--hard", k4)
    # Codex K3: a shallow clone is refused (cached history answers could change as it deepens)
    stmp = tempfile.TemporaryDirectory()
    shallow = os.path.join(stmp.name, "s")
    subprocess.run(["git", "clone", "-q", "--depth", "1", "file://" + root, shallow], capture_output=True, check=True)
    code, out = evaluate(shallow, "HEAD", PACK)
    stmp.cleanup()
    cases.append(("sabotage CT-APR-02 (Codex K3): a shallow repository is refused (exit 2)", code == 2 and has(out, "sığ")))
    put("docs/records/reviews/t/cli.md", get("docs/records/reviews/t/cli.md") + "F-2 ENGEL bozuk\n")
    commit("broken findings")
    try:
        make_review_row(root, "HEAD", PACK, "docs/records/reviews/t/cli.md", "UYGUN", "codex", "gpt", "high", "rs",
                        "K4", "as", "dev", "2026-10-21")
        refused = False
    except Unreadable:
        refused = True
    cases.append(("sabotage CT-APR-02: review-row refuses a malformed findings file", refused))
    # B1: an invalid approval row is never the comparison base
    PACK3 = "execution/domains/x/module-packs/MOD-0003-z.md"
    v3 = "---\nid: MOD-0003\nstatus: approved\nupdated: 2026-10-01\n---\n# Z\n\n## Security\n- kimlik doğrulama\n"
    put(PACK3, v3)
    commit("new pack3")
    via_branch("b1a", lambda: approve(PACK3, "dev", "dev", "LOW"), merger=LEAD)
    put(PACK3, get(PACK3).replace("updated: 2026-10-01", "updated: 2026-10-02"))
    commit("pack3 date only")
    via_branch("b1b", lambda: approve(PACK3, "dev", "dev", "LOW"), merger=LEAD)
    code, out = check(PACK3)
    cases.append(("sabotage CT-APR-02 (B1): two LOW self-approvals of a new pack (first invalid, second 'exempt') -> exit 1",
                  code == 1 and has(out, "ilk onay")))
    # B3: a merge that drops a review row is caught by the full-history, every-parent append-only check
    fork = g("rev-parse", "HEAD").strip()
    via_branch("b3neg", lambda: review(PACK3, "UYGUN DEĞİL", ["F-1 | ENGEL | yanlış | açık | - | -"], 1))
    g("checkout", "-q", "-b", "b3drop", fork)
    put("x.txt", "worker\n")
    commit("worker change")
    mine = get(REVIEWS)
    g("merge", "-q", "--no-commit", "main", who=DEV)
    put(REVIEWS, mine)
    commit("merge main, keep my reviews.tsv")
    g("checkout", "-q", "main")
    g("merge", "-q", "--no-ff", "-m", "Merge b3drop", "b3drop", who=LEAD)
    cases.append(("sabotage CT-APR-02 (B3): a merge that dropped a review row -> append-only problem",
                  bool(append_only_problems(root, "HEAD", REVIEWS))))
    g("reset", "-q", "--hard", "HEAD~1")
    # M3: a worker branch forked before the negative review still sees it (reviews are read from main)
    put(PACK3, get(PACK3) + "- iki\n")
    commit("pack3 body")
    w_fork = g("rev-parse", "HEAD").strip()
    via_branch("m3neg", lambda: (approve(PACK3, "dev", "lead", "HIGH"),
                                 review(PACK3, "UYGUN DEĞİL", ["F-1 | ENGEL | yanlış | açık | - | -"], 1)))
    g("checkout", "-q", "-b", "m3w", w_fork)
    put(APPROVALS, get(APPROVALS))
    approve(PACK3, "dev", "lead", "HIGH")
    commit("worker approval")
    code, out = check(PACK3, "m3w")
    cases.append(("sabotage CT-APR-02 (M3): a worker branch forked before main's rows cannot avoid them -> exit 1",
                  code == 1))
    g("checkout", "-q", "main")
    # M3 (revoke): a worker branch forked before main's revoke must not keep the earlier approval
    put(PACK2, get(PACK2) + "- üç\n")
    commit("pack2 change for the revoke test")
    via_branch("rv-appr", lambda: (approve(PACK2, "dev", "lead", "HIGH", note="whole_pack=WP-1"), review(PACK2, "UYGUN", [], 0)))
    stale = g("rev-parse", "HEAD").strip()
    code_before = check(PACK2)[0]

    def revoke_last():
        last = [l for l in get(APPROVALS).split("\n") if l.strip()][-1].split("\t")
        last[2], last[7] = "revoke", "2026-10-29"
        append(APPROVALS, "\t".join(last))
    via_branch("rv", revoke_last)
    code, out = check(PACK2, stale)
    cases.append(("sabotage CT-APR-02 (M3): a ref that lacks main's revoke row -> exit 1 (the branch must hold main's rows)",
                  code_before == 0 and code == 1 and has(out, "onay / iptal satırlarını aynı sırayla içermiyor")))
    # M3 (reviews from main): a branch that carries the pack's approval chain but not main's later UYGUN DEĞİL still stops
    PACK4 = "execution/domains/x/module-packs/MOD-0004-w.md"
    put(PACK4, "---\nid: MOD-0004\nstatus: approved\n---\n# W\n\n## Notes\n- a\n\n## Teslim dilimleri\n"
               "| dilim | ad | bölümler |\n|---|---|---|\n| DL-1 | a | Notes |\n| DL-2 | b | Notes |\n")
    commit("pack4")
    via_branch("p4a", lambda: (review(PACK4, "UYGUN", [], 0), approve(PACK4, "dev", "lead", "HIGH")))
    p4_fork = g("rev-parse", "HEAD").strip()
    via_branch("p4neg", lambda: review(PACK4, "UYGUN DEĞİL", ["F-1 | ENGEL | yanlış kapsam | açık | - | -"], 1))
    g("checkout", "-q", "-b", "p4w", p4_fork)
    put("w4.txt", "worker\n")
    commit("worker work")
    g("checkout", "-q", "main")
    code, out = check(PACK4, "p4w")
    cases.append(("sabotage CT-APR-02 (M3): a branch without main's later UYGUN DEĞİL review still stops (reviews are read "
                  "from main) -> exit 1", code == 1 and has(out, "UYGUN DEĞİL")))
    # Codex K2′: a findings file repaired after the approval does not validate that approval
    slice_tail = "\n## Teslim dilimleri\n| dilim | ad | bölümler |\n|---|---|---|\n| DL-1 | a | Notes |\n| DL-2 | b | Notes |\n"
    PACK5 = "execution/domains/x/module-packs/MOD-0005-v.md"
    put(PACK5, "---\nid: MOD-0005\nstatus: approved\n---\n# V\n\n## Notes\n- a\n" + slice_tail)
    k2_rel = "docs/records/reviews/t/k2-v5.md"
    k2_digest = sha256(get(PACK5).encode("utf-8"))
    k2_good = f"hedef: {PACK5} @ {k2_digest}\n"
    put(k2_rel, k2_good + "sonradan düzeltilecek satır\n")
    commit("pack5 + a findings file that does not match its row")
    k2_row = "\t".join([PACK5, k2_digest, k2_rel, sha256(k2_good.encode("utf-8")), "UYGUN", "0", "claude", "m", "high",
                        "rs-k2", "K4", "as-k2", "dev", "2026-10-21"])
    via_branch("k2a", lambda: (append(REVIEWS, k2_row), approve(PACK5, "dev", "lead", "HIGH")))
    k2_first = check(PACK5)[0]
    via_branch("k2fix", lambda: put(k2_rel, k2_good))
    code, out = check(PACK5)
    cases.append(("sabotage CT-APR-02 (Codex K2′): a findings file repaired AFTER the approval does not validate it -> exit 1",
                  k2_first == 1 and code == 1))
    via_branch("k2b", lambda: approve(PACK5, "dev", "lead", "HIGH"))
    cases.append(("positive control CT-APR-02 (Codex K2′): a new approval row after the repair -> exit 0", check(PACK5)[0] == 0))
    # Codex K7: valid approval → later UYGUN DEĞİL → pack fixed → new UYGUN review → generated row: generator and check agree
    PACK6 = "execution/domains/x/module-packs/MOD-0006-u.md"
    put(PACK6, "---\nid: MOD-0006\nstatus: approved\n---\n# U\n\n## Notes\n- a\n" + slice_tail)
    commit("pack6")
    via_branch("k7a", lambda: (review(PACK6, "UYGUN", [], 0), approve(PACK6, "dev", "lead", "HIGH")))
    k7_first = check(PACK6)[0]
    via_branch("k7neg", lambda: review(PACK6, "UYGUN DEĞİL", ["F-1 | ENGEL | eksik test | açık | - | -"], 1))
    k7_neg = check(PACK6)[0]
    put(PACK6, get(PACK6).replace("- a\n", "- a\n- test eklendi\n"))
    commit("pack6 fixed")
    via_branch("k7b", lambda: (review(PACK6, "UYGUN", [], 0), approve(PACK6, "dev", "lead", "HIGH")))
    code, out = check(PACK6)
    cases.append(("CT-APR-02 (Codex K7): today's UYGUN DEĞİL stops the old approval, and after the fix a generated row passes "
                  "(the generator and the check use the same historical base)",
                  k7_first == 0 and k7_neg == 1 and code == 0 and not has(out, "kart")
                  and has(out, "hesaplanan risk: LOW")))     # the base is the historical approval, not 'first approval'

    # a review row merged by another person only on the worker branch (not on main) does not count
    PACK7 = "execution/domains/x/module-packs/MOD-0007-t.md"
    put(PACK7, "---\nid: MOD-0007\nstatus: approved\n---\n# T\n\n## Notes\n- a\n" + slice_tail)
    commit("pack7")
    g("checkout", "-q", "-b", "wb7")
    g("checkout", "-q", "-b", "wb7-review")
    review(PACK7, "UYGUN", [], 0)
    commit("review on a sub-branch")
    g("checkout", "-q", "wb7")
    g("merge", "-q", "--no-ff", "-m", "Merge review into the worker branch", "wb7-review", who=LEAD)
    approve(PACK7, "dev", "lead", "LOW")
    commit("approval on the worker branch")
    g("checkout", "-q", "main")
    code, out = check(PACK7, "wb7")
    cases.append(("sabotage CT-APR-02: a review merged by another person only on the worker branch (not main) does not "
                  "count -> exit 1", code == 1 and has(out, "main'e insan adımıyla gelmedi")))
    # Codex K2″: a LOW approver added to the list later does not validate an earlier row
    PACK8 = "execution/domains/x/module-packs/MOD-0008-s.md"
    put(PACK8, "---\nid: MOD-0008\nstatus: approved\nowner: TBD\n---\n# S\n\n## Notes\n- a\n" + slice_tail)
    commit("pack8")
    via_branch("k8a", lambda: (review(PACK8, "UYGUN", [], 0), approve(PACK8, "dev", "lead", "HIGH")))
    put(PACK8, get(PACK8).replace("owner: TBD", "owner: AT"))
    commit("pack8 owner filled")
    via_branch("k8b", lambda: approve(PACK8, "dev", "newlead", "LOW"))
    k2b_first = check(PACK8)
    via_branch("k8list", lambda: append(APPROVERS, "newlead\tK8\tkıdemli\tevet"))
    code, out = check(PACK8)
    cases.append(("sabotage CT-APR-02 (Codex K2″): a LOW approver listed only AFTER the row does not validate it -> exit 1",
                  k2b_first[0] == 1 and has(k2b_first[1], "listesinde yok") and code == 1 and has(out, "listesinde yok")))
    via_branch("k8c", lambda: approve(PACK8, "dev", "newlead", "LOW"))
    cases.append(("positive control CT-APR-02 (Codex K2″): a new row after the listing -> exit 0", check(PACK8)[0] == 0))
    # Codex K8: an ENGEL raised and accepted AFTER a valid LOW approval needs a new (HIGH) approval row
    PACK9 = "execution/domains/x/module-packs/MOD-0009-r.md"
    put(PACK9, "---\nid: MOD-0009\nstatus: approved\n---\n# R\n\n## Notes\n- a\n" + slice_tail)
    commit("pack9")
    via_branch("k9a", lambda: (review(PACK9, "UYGUN", [], 0), approve(PACK9, "dev", "lead", "HIGH")))
    put(PACK9, get(PACK9).replace("- a\n", "- a\n- b\n"))
    commit("pack9 body")
    via_branch("k9b", lambda: (review(PACK9, "UYGUN", [], 0), approve(PACK9, "dev", "dev", "LOW")))
    k9_low = check(PACK9)[0]
    via_branch("k9eng", lambda: review(PACK9, "KOŞULLU", ["F-1 | ENGEL | eksik | açık | - | -"], 1))
    k9_open = check(PACK9)[0]
    k9_sha8 = [l for l in get(REVIEWS).split("\n") if l.strip()][-1].split("\t")[3][:8]
    via_branch("k9acc", lambda: review(PACK9, "KOŞULLU", [f"F-1@{k9_sha8} | ENGEL | eksik | {ACCEPTED} | K1 | -"], 0))
    code, out = check(PACK9)
    cases.append(("sabotage CT-APR-02 (Codex K8): an ENGEL raised and accepted after a LOW approval -> exit 1 until a new "
                  "approval row", k9_low == 0 and k9_open == 1 and code == 1 and has(out, "onaydan sonra 1 ENGEL geldi")))
    via_branch("k9c", lambda: approve(PACK9, "dev", "lead", "HIGH"))
    code, out = check(PACK9)
    cases.append(("positive control CT-APR-02 (Codex K8): a new HIGH approval by the accepting approver -> exit 0",
                  code == 0 and has(out, "ENGEL kabulü")))
    # Codex K8′: with an ENGEL accepted by K1 on this version, a new LOW self-approval of the SAME version does not pass
    via_branch("k9low", lambda: approve(PACK9, "dev", "dev", "LOW"), merger=DEV)
    code, out = check(PACK9)
    cases.append(("sabotage CT-APR-02 (Codex K8′): a new LOW self-approval of the same version after an accepted ENGEL -> "
                  "exit 1 (HIGH, other person)", code == 1 and has(out, "kabul edilmiş ENGEL var")))
    # Codex K9: valid LOW approval → UYGUN DEĞİL on the same version → a new LOW approval of the unchanged pack still stops
    PACK10 = "execution/domains/x/module-packs/MOD-0010-q.md"
    put(PACK10, "---\nid: MOD-0010\nstatus: approved\n---\n# Q\n\n## Notes\n- a\n" + slice_tail)
    commit("pack10")
    via_branch("q1", lambda: (review(PACK10, "UYGUN", [], 0), approve(PACK10, "dev", "lead", "HIGH")))
    put(PACK10, get(PACK10).replace("- a\n", "- a\n- b\n"))
    commit("pack10 body")
    via_branch("q2", lambda: (review(PACK10, "UYGUN", [], 0), approve(PACK10, "dev", "dev", "LOW")))
    q_ok = check(PACK10)[0]
    via_branch("q3", lambda: review(PACK10, "UYGUN DEĞİL", ["F-1 | ENGEL | yanlış | açık | - | -"], 1))
    q_neg = check(PACK10)[0]
    via_branch("q4", lambda: approve(PACK10, "dev", "dev", "LOW"), merger=DEV)
    code, out = check(PACK10)
    cases.append(("sabotage CT-APR-02 (Codex K9): a new LOW approval of the unchanged pack after UYGUN DEĞİL -> exit 1",
                  q_ok == 0 and q_neg == 1 and code == 1 and has(out, "UYGUN DEĞİL")))
    # Codex K9 (today): a UYGUN DEĞİL that arrives AFTER an exempt approval of the same version stops it today
    PACK11 = "execution/domains/x/module-packs/MOD-0011-p.md"
    put(PACK11, "---\nid: MOD-0011\nstatus: approved\nowner: TBD\n---\n# P\n\n## Notes\n- a\n" + slice_tail)
    commit("pack11")
    via_branch("p1", lambda: (review(PACK11, "UYGUN", [], 0), approve(PACK11, "dev", "lead", "HIGH")))
    put(PACK11, get(PACK11).replace("owner: TBD", "owner: AT"))
    commit("pack11 owner filled")
    via_branch("p2", lambda: approve(PACK11, "dev", "dev", "LOW"), merger=DEV)
    p_ok = check(PACK11)[0]
    via_branch("p3", lambda: review(PACK11, "UYGUN DEĞİL", ["F-1 | ENGEL | sahip yanlış | açık | - | -"], 1))
    code, out = check(PACK11)
    cases.append(("sabotage CT-APR-02 (Codex K9, today): a UYGUN DEĞİL after an exempt approval of the same version -> exit 1",
                  p_ok == 0 and code == 1 and has(out, "UYGUN DEĞİL")))
    # Codex K10: open ENGEL → an exempt LOW self-approval of the same version → the ENGEL accepted later → still exit 1
    PACK12 = "execution/domains/x/module-packs/MOD-0012-o.md"
    put(PACK12, "---\nid: MOD-0012\nstatus: approved\n---\n# O\n\n## Notes\n- a\n" + slice_tail)
    commit("pack12")
    via_branch("o1", lambda: (review(PACK12, "UYGUN", [], 0), approve(PACK12, "dev", "lead", "HIGH")))
    put(PACK12, get(PACK12).replace("- a\n", "- a\n- b\n"))
    commit("pack12 body")
    via_branch("o2", lambda: (review(PACK12, "UYGUN", [], 0), approve(PACK12, "dev", "dev", "LOW")))
    via_branch("o3", lambda: review(PACK12, "KOŞULLU", ["F-1 | ENGEL | eksik | açık | - | -"], 1))
    o_open = check(PACK12)[0]
    o_sha8 = [l for l in get(REVIEWS).split("\n") if l.strip()][-1].split("\t")[3][:8]
    via_branch("o4", lambda: approve(PACK12, "dev", "dev", "LOW"), merger=DEV)
    o_low = check(PACK12)[0]
    via_branch("o5", lambda: review(PACK12, "KOŞULLU", [f"F-1@{o_sha8} | ENGEL | eksik | {ACCEPTED} | K1 | -"], 0))
    code, out = check(PACK12)
    cases.append(("sabotage CT-APR-02 (Codex K10): open ENGEL → exempt LOW self-approval → ENGEL accepted later -> still "
                  "exit 1 (the row was added while the ENGEL was open)", o_open == 1 and o_low == 1 and code == 1
                  and has(out, "açık ENGEL")))
    via_branch("o6", lambda: approve(PACK12, "dev", "lead", "HIGH"))
    cases.append(("positive control CT-APR-02 (Codex K10): a new HIGH approval by the accepting approver -> exit 0",
                  check(PACK12)[0] == 0))
    # M1: a malformed review row (upper-case hex, './' target) makes the file unreadable, never invisible
    g("checkout", "-q", "-b", "m1")
    digest = sha256(get(PACK3).encode("utf-8"))
    append(REVIEWS, "\t".join(["./" + PACK3, digest.upper(), "docs/records/reviews/t/x.md", "0" * 64, "UYGUN DEĞİL", "1",
                               "claude", "m", "high", "s1", "K4", "s2", "dev", "2026-10-21"]))
    commit("malformed review row")
    try:
        review_rows(root, "HEAD")
        m1 = False
    except Unreadable:
        m1 = True
    cases.append(("sabotage CT-APR-02 (M1): a review row with a non-canonical target or SHA is refused", m1))
    g("checkout", "-q", "main")
    # M2, m1, m2: hidden slices, a free-text date, lookalike findings
    hidden = v2 + "\n## Teslim dilimleri\n| dilim | ad | bölümler |\n|---|---|---|\n| DL-1 | a | Notes |\n<!--\n| DL-2 | c | Notes |\n-->\n"
    cases.append(("sabotage CT-APR-02 (M2): a slice row inside an HTML comment makes the table invalid; rows under a "
                  "sub-heading are not the pack's slices",
                  slices(hidden) is None and slices(hidden.replace("<!--\n", "### Eski\n").replace("-->\n", "")) == ["DL-1"]))
    valid = v2 + "\n## Teslim dilimleri\n| dilim | ad | bölümler |\n|---|---|---|\n| DL-1 | a | Notes |\n| DL-2 | c | Notes |\n"
    cases.append(("sabotage CT-APR-02 (Codex K1): a slice row with missing cells, an empty cell or a section the pack does "
                  "not have -> no valid table",
                  slices(valid.replace("| DL-2 | c | Notes |", "| DL-2 |")) is None
                  and slices(valid.replace("| DL-2 | c | Notes |", "| DL-2 | - | Notes |")) is None
                  and slices(valid.replace("| DL-2 | c | Notes |", "| DL-2 | c | Yok Böyle |")) is None))
    cases.append(("CT-APR-02 (M2): any HTML comment in the slice section refuses the table (strict; the split-table rule is "
                  "the other guard)", slices(valid) == ["DL-1", "DL-2"] and slices(valid + "<!-- not -->\n") is None))
    based = "---\nupdated: 2026-10-01\n---\n# A\n"
    cases.append(("sabotage CT-APR-02 (m1): free text in a date field is not exempt",
                  not review_exempt(based.replace("2026-10-01", "kapsam genişledi"), based)[0]))
    cases.append(("sabotage CT-APR-02 (m2): lookalike or lower-case finding lines are refused, not ignored",
                  bool(probe(["F\u20101 | \u0415NGEL | x | açık | - | -"])) and bool(probe(["f-1 | engel | x | açık | - | -"]))))
    cases.append(("sabotage CT-APR-02 (S-3): ordinary prose with lower-case or capitalised level words is allowed; upper-case "
                  "level words outside a finding line are not",
                  not probe(["Bu bir engel değil.", "orta vadede ele alınır; Engel yok, düşük risk."])
                  and bool(probe(["- ENGEL: gizli"])) and bool(probe(["Bu ORTA bir sorun"])) and bool(probe(["DÜŞÜK etki"]))
                  and bool(probe(["F-1 engel yok"])) and bool(probe(["bulgu | prose"]))))
    g("checkout", "-q", "-b", "drop-review-policy")
    g("rm", "-q", REVIEW_POLICY)
    commit("drop the review start")
    code, out = check(PACK)
    cases.append(("sabotage CT-APR-02: deleting POLICY-REVIEW-START is a refusal, never 'no rule'", code != 0))
    g("checkout", "-q", "main")
    # Codex F-6 on the REVIEW path (human_step): a review row written straight onto main, an unrelated commit after it,
    # another person's branch pulls main with a merge and main is fast-forwarded -> not a pull request's merge
    put(PACK2, get(PACK2) + "- f6\n")
    commit("pack2 change f6")
    g("checkout", "-q", "-b", "f6-lead")
    put("lead-f6.txt", "x\n")
    commit("lead work f6", who=LEAD)
    g("checkout", "-q", "main")
    review(PACK2, "UYGUN", ["F-1 | DÜŞÜK | yazım | kapandı | - | -"], 0)
    commit("review row straight on main f6", who=DEV)
    put("after-f6.txt", "x\n")
    commit("unrelated after the review row", who=DEV)
    g("checkout", "-q", "f6-lead")
    g("merge", "-q", "--no-ff", "-m", "Merge main into f6-lead", "main", who=LEAD)
    g("checkout", "-q", "main")
    g("merge", "-q", "--ff-only", "f6-lead", who=LEAD)
    head = git(root, "rev-parse", "HEAD").decode().strip()
    fails = review_problems(root, head, PACK2, get(PACK2).encode("utf-8"), head)[0]
    cases.append(("sabotage S-40/Codex F-6 (review path): a review row straight on main carried by another person's "
                  "pull-merge and a fast-forward -> not a pull request's merge, the row does not count",
                  any("PR'ın merge commit'i değil" in f for f in fails)))
    tmp.cleanup()


def main(argv: list[str]) -> int:
    import argparse
    import datetime
    if argv[1:] == ["--self-test"]:
        return self_test()
    ap = argparse.ArgumentParser(prog="pack_approval.py", description="CT-APR-01 — sürüme bağlı paket onayı "
                                 "(sözleşme: docs/roadmap/plans/ct-dev-readiness-approval-01/CONTRACT.md §3). "
                                 "Ayrıca: --self-test.")
    sub = ap.add_subparsers(dest="cmd", required=True)
    c = sub.add_parser("card", help="onay kartı ve SHA-256'sı")
    c.add_argument("pack"); c.add_argument("--ref", default="HEAD")
    k = sub.add_parser("check", help="0 onaylı · 1 onaylı değil · 2 okunamadı")
    k.add_argument("pack"); k.add_argument("--ref", default="HEAD")
    r = sub.add_parser("row", help="onay dosyasına eklenecek satırı basar (dosyaya yazmaz)")
    r.add_argument("pack"); r.add_argument("--ref", default="HEAD")
    r.add_argument("--author", required=True, help="paketi yazanın onaylayıcı listesindeki hesabı")
    r.add_argument("--approver", required=True, help="onaylayanın onaylayıcı listesindeki hesabı")
    r.add_argument("--risk", required=True, choices=["LOW", "HIGH"])
    r.add_argument("--reason", required=True, help="risk beyanının gerekçesi")
    r.add_argument("--note", default="")
    r.add_argument("--date", default=datetime.date.today().isoformat())
    v = sub.add_parser("review-row", help="CT-APR-02 inceleme satırını basar (dosyaya yazmaz)")
    v.add_argument("pack"); v.add_argument("--ref", default="HEAD")
    v.add_argument("--findings", required=True, help="docs/records/reviews/<kimlik>/<dosya>.md (commit edilmiş)")
    v.add_argument("--verdict", required=True, choices=list(VERDICTS))
    v.add_argument("--reviewer", required=True, choices=list(REVIEWERS))
    for opt in ("--model", "--effort", "--reviewer-session", "--author", "--author-session", "--requested-by"):
        v.add_argument(opt, required=True)
    v.add_argument("--date", default=datetime.date.today().isoformat())
    a = ap.parse_args(argv[1:])
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    if a.cmd == "check":
        code, out = evaluate(root, a.ref, a.pack)
        print("\n".join(out))
        return code
    try:
        if a.cmd == "card":
            commit = commit_of(root, a.ref)
            data = read(root, commit, a.pack) if commit else None
            if data is None:
                print(f"paket {a.pack} ya da ref {a.ref} yok")
                return 2
            ctx = Ctx(root, commit, a.pack)
            base, after_revoke = base_for(ctx, len(ctx.rows))
            risk, reasons = risk_of(a.pack, data.decode("utf-8", errors="replace"), base, after_revoke)
            text = card(a.pack, data, risk, reasons)
            print(text)
            print(f"card_sha256: {sha256(text.encode('utf-8'))}")
            return 0
        if a.cmd == "review-row":
            print(make_review_row(root, a.ref, a.pack, a.findings, a.verdict, a.reviewer, a.model, a.effort,
                                  a.reviewer_session, a.author, a.author_session, a.requested_by, a.date))
            return 0
        print(make_row(root, a.ref, a.pack, a.author, a.approver, a.risk, a.reason, a.date, a.note))
        return 0
    except Unreadable as e:
        print(f"okunamadı — {e}")
        return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv))
