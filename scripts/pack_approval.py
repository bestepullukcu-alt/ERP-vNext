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

Trust boundary of this version (owner decision 2026-10-19, no GitHub accounts yet): declarations are checked for
consistency and the human step is derived from git; identities are not verified. Merges must be merge commits (a
squash or fast-forward merge leaves no merge author and a HIGH approval then fails). Since PR #153 (2026-10-19)
.github/CODEOWNERS puts the approval files, the approver list and these checks under a code-owner review.

    python3 scripts/pack_approval.py card  <pack> [--ref REF]        # the approval card and its SHA-256
    python3 scripts/pack_approval.py check <pack> [--ref REF]        # exit 0 approved · 1 not approved · 2 unreadable
    python3 scripts/pack_approval.py row   <pack> --author L --approver L --risk LOW|HIGH --reason TEXT [--ref REF]
                                                                     # prints the row; never writes the file
    python3 scripts/pack_approval.py --self-test
"""

from __future__ import annotations

import difflib
import hashlib
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
PACK_PATH = re.compile(r"^execution/domains/[a-z0-9][a-z0-9-]*/module-packs/[A-Za-z0-9][A-Za-z0-9._-]*\.md$")
SENSITIVE_HEADING = re.compile(r"(?i)(repo scope|protected path|authori[sz]ation|permission|yetki|izin|gateway|routing|"
                               r"schema|şema|entity|data model|veri model|migration|taşıma|integration|entegrasyon|"
                               r"audit|denetim|security|güvenlik|tenan|kiracı)")
ALWAYS_HIGH = re.compile(r"(?i)(\.antigravity|ocelot\.json)")
IDENTITY_KEYS = ("id", "domain", "service")
OPEN_DECISION = re.compile(r"(?i)\b(TBD|OPEN|AÇIK KARAR|açık karar|open decision)\b")
HEX64 = re.compile(r"^[0-9a-f]{64}$")


class Unreadable(Exception):
    pass


# ------------------------------------------------------------------------------------------------ git (read-only)
def git(root: str, *args: str, ok_fail: bool = False) -> bytes | None:
    env = dict(os.environ, GIT_OPTIONAL_LOCKS="0")
    done = subprocess.run(["git", "-C", root, *args], capture_output=True, env=env)
    if done.returncode != 0:
        if ok_fail:
            return None
        raise Unreadable(f"git {' '.join(args[:2])} → {done.returncode}")
    return done.stdout


def commit_of(root: str, ref: str) -> str | None:
    out = git(root, "rev-parse", "--verify", "--quiet", f"{ref}^{{commit}}", ok_fail=True) if not ref.startswith("-") else None
    return out.decode().strip() if out else None


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
    raw = read(root, commit, APPROVALS)
    return [r for r in parse_tsv(raw.decode("utf-8"), APPROVAL_COLUMNS, APPROVALS) if r["pack"] == path] if raw else []


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


def base_for(root: str, commit: str, path: str, prior: list[dict]) -> tuple[str | None, bool]:
    """(the text of the last approved version among `prior` rows that no later revoke cancelled, any revoke in prior?)
    A revoke cancels every approval before it as a comparison base: re-approving after a revoke is a first approval."""
    revoked = any(r["action"] == "revoke" for r in prior)
    for i in range(len(prior) - 1, -1, -1):
        r = prior[i]
        if r["action"] != "approve":
            continue
        if any(x["action"] == "revoke" for x in prior[i + 1:]):
            continue
        text = version_with_sha(root, commit, path, r["pack_sha256"])
        if text is not None:
            return text, revoked
    return None, revoked


# ------------------------------------------------------------------------------------------------ history checks
def append_only_problems(root: str, commit: str) -> list[str]:
    """Every version of the approvals file starts with the previous version's rows (content lines; a newline-only
    or trailing-space change is not a deletion)."""
    problems = []
    for c in git(root, "log", "--format=%H", commit, "--", APPROVALS).decode().split():
        parent = commit_of(root, f"{c}^1")
        before = read(root, parent, APPROVALS) if parent else None
        after = read(root, c, APPROVALS)
        old = content_lines(before.decode("utf-8", errors="replace")) if before else []
        new = content_lines(after.decode("utf-8", errors="replace")) if after else []
        if new[:len(old)] != old:
            problems.append(f"onay dosyası {c[:12]}'de yalnız ekleme değil (satır silindi, değişti ya da yeri değişti)")
    return problems


def version_with_sha(root: str, commit: str, path: str, digest: str) -> str | None:
    for c in git(root, "log", "--format=%H", commit, "--", path).decode().split():
        data = read(root, c, path)
        if data is not None and sha256(data) == digest:
            return data.decode("utf-8", errors="replace")
    return None


def row_commit(root: str, commit: str, line_no: int) -> str:
    blame = git(root, "blame", "--porcelain", "-L", f"{line_no},{line_no}", commit, "--", APPROVALS).decode().split()
    if not blame or not re.fullmatch(r"[0-9a-f]{40}", blame[0]):
        raise Unreadable("onay satırını ekleyen commit bulunamadı")
    return blame[0]


def main_merge(root: str, main: str, added: str) -> tuple[str | None, str]:
    """The first-parent merge of the main line that brought `added` in, or (None, why)."""
    if git(root, "merge-base", "--is-ancestor", added, main, ok_fail=True) is None:
        return None, "onay satırı henüz ana çizgide (main) değil"
    if added in git(root, "rev-list", "--first-parent", main).decode().split():
        return None, f"onay satırı {added[:12]} ile doğrudan ana çizgiye yazılmış — merge ile gelen ikinci kişi adımı yok"
    for m in reversed(git(root, "rev-list", "--first-parent", "--merges", main).decode().split()):
        if git(root, "merge-base", "--is-ancestor", added, m, ok_fail=True) is not None:
            return m, ""
    return None, "onay satırını ana çizgiye getiren merge commit'i bulunamadı"


# ------------------------------------------------------------------------------------------------ the decision
def evaluate(root: str, ref: str, path: str) -> tuple[int, list[str]]:
    head = [f"PACK-APPROVAL v1 — {path} @ {ref}"]
    try:
        if not PACK_PATH.match(path or "") or "//" in path:
            raise Unreadable("paket yolu kanonik değil (execution/domains/<domain>/module-packs/<dosya>.md; ./ ya da // yok)")
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
        policy_commit = added[-1]
        last_change = git(root, "log", "-1", "--format=%H", commit, "--", path).decode().strip()
        rows = approval_rows(root, commit, path)
        unchanged = git(root, "merge-base", "--is-ancestor", last_change, policy_commit, ok_fail=True) is not None
        if unchanged and not rows:
            return 0, head + [f"paket kural başlangıcından ({policy_commit[:12]}) beri değişmedi ve yeni kurala göre onay "
                              "satırı yok — eski onay geçerli, geriye dönük düşürülmez", "çıkış: 0"]
        problems = append_only_problems(root, commit)
        if problems:
            return 1, head + problems + ["sonuç: onay dosyası güvenilmez — hiçbir onay geçmez", "çıkış: 1"]
        if not rows:
            return 1, head + ["sonuç: onay yok — paket kural başlangıcından sonra değişti; onay satırı gerekli", "çıkış: 1"]
        row = rows[-1]
        if row["action"] == "revoke":
            return 1, head + [f"sonuç: son satır iptal ({row['approved_at']}) — onay yok", "çıkış: 1"]
        if row["action"] != "approve" or not HEX64.match(row["pack_sha256"]) or row["risk"] not in ("LOW", "HIGH"):
            raise Unreadable("son onay satırının action / pack_sha256 / risk değeri geçersiz")
        if row["pack_sha256"] != sha256(data):
            base = version_with_sha(root, commit, path, row["pack_sha256"])
            n = (sum(1 for l in difflib.unified_diff(base.split("\n"), text.split("\n"), lineterm="", n=0)
                     if l[:1] in "+-" and not l.startswith(("+++", "---"))) if base is not None else "?")
            return 1, head + [f"sonuç: paket onaydan sonra değişti ({n} satır) — yeniden onay gerekli", "çıkış: 1"]
        base, after_revoke = base_for(root, commit, path, rows[:-1])
        risk, reasons = pack_risk(text, base, after_revoke)
        out = head + [f"hesaplanan risk: {risk}"] + [f"  - {r}" for r in reasons]
        fails = []
        if risk == "HIGH" and row["risk"] == "LOW":
            fails.append("beyan LOW, hesaplanan HIGH — risk yalnız yükseltilebilir; HIGH onay gerekli")
        effective = "HIGH" if "HIGH" in (risk, row["risk"]) else "LOW"
        if effective == "HIGH":
            main = main_line(root)
            added_by = row_commit(root, commit, row["_n"])
            merge, why = main_merge(root, main, added_by)
            if merge is None:
                fails.append("insan adımı yok: " + why)
            else:
                who = lambda c: git(root, "log", "-1", "--format=%an\t%ae", c).decode().strip().lower().split("\t")
                a, b = who(added_by), who(merge)
                if a[0] == b[0] or a[1] == b[1]:
                    fails.append(f"insan adımı yok: satırı ekleyen ve merge eden aynı yazar ({a[0]}) — türetildi")
                else:
                    out.append(f"insan adımı: satırı ekleyen {a[0]}, merge eden {b[0]} ({merge[:12]}) — türetildi, "
                               "GitHub'da doğrulanmadı")
                listed = approvers_at(root, f"{merge}^1")       # the list as it was on main before this merge
                person = lambda login: listed[login]["person"] if login in listed else f"@{login}"
                w = listed.get(row["approver"])
                if w is None:
                    fails.append(f"onaylayan {row['approver']} merge'den önceki onaylayıcı listesinde yok")
                elif w["high"] != "evet":
                    fails.append(f"onaylayan {row['approver']} ({w['role']}) yüksek riskli işi onaylayamaz")
                if person(row["approver"]) == person(row["author"]):
                    fails.append("yüksek riskli işi yazan (ya da aynı kişinin başka hesabı) onaylayamaz")
        else:
            listed = approvers_at(root, main_line(root))
            if row["approver"] != row["author"] and row["approver"] not in listed:
                fails.append(f"onaylayan {row['approver']} onaylayıcı listesinde yok ve yazan değil")
        if row["card_sha256"] != sha256(card(path, data, risk, reasons).encode("utf-8")):
            fails.append("onay kartının özeti paketin şimdiki kartıyla aynı değil — kart elle değişti ya da satır yeniden "
                         "üretilmeli (aynı pakete eşzamanlı onay geldiyse satır, birleştirmeden sonra yeniden üretilir)")
        if fails:
            return 1, out + [f"ENGEL: {f}" for f in fails] + ["sonuç: onay geçersiz", "çıkış: 1"]
        return 0, out + [f"sonuç: onaylı — {effective} · onaylayan {row['approver']} · {row['approved_at']}", "çıkış: 0"]
    except (Unreadable, UnicodeDecodeError, IndexError, KeyError, ValueError) as e:
        return 2, head + [f"sonuç: okunamadı — {e}", "çıkış: 2"]


def make_row(root: str, ref: str, path: str, author: str, approver: str, risk: str, reason: str, when: str,
             note: str = "") -> str:
    commit = commit_of(root, ref)
    if commit is None or not PACK_PATH.match(path or ""):
        raise Unreadable("ref ya da paket yolu geçersiz")
    data = read(root, commit, path)
    if data is None:
        raise Unreadable(f"paket {path} yok")
    base, after_revoke = base_for(root, commit, path, approval_rows(root, commit, path))
    computed, reasons = pack_risk(data.decode("utf-8", errors="replace"), base, after_revoke)
    cells = [path, sha256(data), "approve", risk, reason, author, approver, when,
             sha256(card(path, data, computed, reasons).encode("utf-8")), note]
    if any("\t" in c or "\n" in c for c in cells):
        raise Unreadable("bir hücre sekme ya da satır sonu içeriyor")
    return "\t".join(cells)


# ------------------------------------------------------------------------------------------------ self-test
def self_test() -> int:
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
    for label, ok in cases:
        print(("PASS " if ok else "FAIL ") + label)
    passed = sum(1 for _, ok in cases if ok)
    print(f"self-test: {passed}/{len(cases)} passed")
    return 0 if passed == len(cases) else 1


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
            base, after_revoke = base_for(root, commit, a.pack, approval_rows(root, commit, a.pack))
            risk, reasons = pack_risk(data.decode("utf-8", errors="replace"), base, after_revoke)
            text = card(a.pack, data, risk, reasons)
            print(text)
            print(f"card_sha256: {sha256(text.encode('utf-8'))}")
            return 0
        print(make_row(root, a.ref, a.pack, a.author, a.approver, a.risk, a.reason, a.date, a.note))
        return 0
    except Unreadable as e:
        print(f"okunamadı — {e}")
        return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv))
